# INSOLVEX — Complete Implementation Plan

**Date:** 2026-03-09  
**Author:** Senior Software Architect  
**Stack:** .NET 8 API · React/TypeScript SPA · PostgreSQL/SQL Server · EF Core · Multi-tenant

---

## System Architecture Changes (Cross-Cutting)

### Audit Service (used by all features)

The `AuditLog` entity already exists with `OldValues`/`NewValues` JSON fields and a `Changes` diff field. A shared `IAuditService` must be injected in every service layer that mutates domain data. The service must:

- Accept `entityType`, `entityId`, `action`, `oldValues`, `newValues`
- Serialize old/new as JSON diffs
- Set `TenantId`, `UserId`, `Timestamp`, `Category`, `Severity`
- Write to `AuditLogs` table asynchronously (fire-and-forget with bounded queue or inline on same DbContext transaction)

### Tenant Scoping

All new tables must include `TenantId` (if derived from `TenantScopedEntity`) and all queries must filter by tenant.

### Notification Service

Feature 1 and Feature 6 require multi-recipient email notifications. A single `INotificationService` should dispatch to all relevant recipients.

---

## Database Schema Updates

### Feature 1 — Multiple Task Assignees

```sql
-- New junction table
CREATE TABLE TaskAssignees (
    Id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    TenantId    UUID NOT NULL REFERENCES Tenants(Id),
    TaskId      UUID NOT NULL REFERENCES CompanyTasks(Id) ON DELETE CASCADE,
    UserId      UUID NOT NULL REFERENCES Users(Id) ON DELETE CASCADE,
    AssignedAt  TIMESTAMPTZ NOT NULL DEFAULT now(),
    AssignedBy  UUID REFERENCES Users(Id),
    UNIQUE (TaskId, UserId)
);
CREATE INDEX ix_taskassignees_userid ON TaskAssignees(UserId);
CREATE INDEX ix_taskassignees_taskid ON TaskAssignees(TaskId);
```

**Migration note:** The existing `CompanyTasks.AssignedToUserId` column is kept and becomes the **primary assignee** (owned). After migration, seed one row in `TaskAssignees` for every task that currently has `AssignedToUserId IS NOT NULL`.

---

### Feature 3 — Workflow Builder Changes

**A) Remove LinkedTemplates** — `WorkflowStageTemplate` table and its FK are **not dropped** from the schema but the UI no longer exposes editing them. They remain for historical data integrity.

**B–E) `WorkflowStageDefinition` column changes**

```sql
-- B: OutputDocTypesJson already exists (JSON array of strings)
-- No schema change; only the UI changes from single to checkbox list.

-- C: Replace free-text ValidationRulesJson with structured JSON schema
-- Column already exists; only the stored format changes.
-- Example structured format:
-- [{"ruleType":"RequiredField","field":"NoticeDate","condition":null,"errorMessage":"Data de notificare este obligatorie"}]
-- No DDL change needed; document the new JSON contract.

-- D: AllowedTransitionsJson already exists (JSON array of stage keys)
-- No schema change; the UI changes from single to multi-select.

-- E: OutputTasksJson — add "required" boolean to each task definition object
-- Example: [{"title":"...", "category":"...", "required": true}]
-- No DDL change; update JSON contract documentation.

-- F: RequiredTaskTemplatesJson — column stays in DB but UI hides it.
-- No DDL change.
```

---

### Feature 4 — Procedure Type Change

```sql
-- Track procedure type history on the case
CREATE TABLE CaseProcedureHistory (
    Id             UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    TenantId       UUID NOT NULL REFERENCES Tenants(Id),
    CaseId         UUID NOT NULL REFERENCES InsolvencyCases(Id) ON DELETE CASCADE,
    ChangedAt      TIMESTAMPTZ NOT NULL DEFAULT now(),
    ChangedByUserId UUID REFERENCES Users(Id),
    OldProcedureType INT NOT NULL,   -- matches ProcedureType enum int value
    NewProcedureType INT NOT NULL,
    Reason         TEXT,
    WorkflowStagesRemovedJson TEXT    -- JSON array of stage keys removed
);
```

---

### Feature 5 — Case Action Logging

`AuditLog` entity is already comprehensive. Add indexes for common query patterns:

```sql
CREATE INDEX ix_auditlogs_entitytype_entityid ON AuditLogs(EntityType, EntityId);
CREATE INDEX ix_auditlogs_casenumber ON AuditLogs(CaseNumber);
CREATE INDEX ix_auditlogs_userid_timestamp ON AuditLogs(UserId, Timestamp DESC);
CREATE INDEX ix_auditlogs_category_timestamp ON AuditLogs(Category, Timestamp DESC);
```

Introduce a typed `AuditAction` constant class (server-side):

```csharp
public static class AuditActions
{
    public const string CaseFieldEdited        = "case.field_edited";
    public const string WorkflowTransition     = "workflow.transition";
    public const string DocumentGenerated      = "document.generated";
    public const string DocumentSavedToCase    = "document.saved_to_case";
    public const string TaskCreated            = "task.created";
    public const string TaskCompleted          = "task.completed";
    public const string TaskAssigneeAdded      = "task.assignee_added";
    public const string TaskAssigneeRemoved    = "task.assignee_removed";
    public const string ProcedureTypeChanged   = "case.procedure_type_changed";
    public const string ReportGenerated        = "report.generated";
    public const string StageStarted           = "workflow.stage_started";
    public const string StageClosed            = "workflow.stage_closed";
}
```

---

### Feature 6 — Mandatory Report Generation

```sql
-- Configure per-tenant report reminder interval
-- Add column to Tenants table (or Settings table if one exists):
ALTER TABLE Tenants ADD COLUMN IF NOT EXISTS ReportReminderIntervalDays INT NOT NULL DEFAULT 90;
```

No other schema changes; task creation reuses `CompanyTasks` and calendar event reuses `CalendarEvents`.

---

### Feature 9 — Ad-Hoc Task Creation

No new table needed. `CompanyTasks` already supports nullable `CaseId` (company-level tasks). Add a column to identify ad-hoc tasks:

```sql
ALTER TABLE CompanyTasks ADD COLUMN IF NOT EXISTS IsAdHoc BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE CompanyTasks ADD COLUMN IF NOT EXISTS WorkflowStageId UUID REFERENCES CaseWorkflowStages(Id);
-- WorkflowStageId null = ad-hoc; non-null = workflow-driven
```

---

## API Endpoints

### Feature 1 — Multiple Task Assignees

| Method   | Path                                     | Description                                                       |
| -------- | ---------------------------------------- | ----------------------------------------------------------------- |
| `GET`    | `/api/tasks/{taskId}/assignees`          | List all assignees                                                |
| `POST`   | `/api/tasks/{taskId}/assignees`          | Add assignee (body: `{ userId }`)                                 |
| `DELETE` | `/api/tasks/{taskId}/assignees/{userId}` | Remove assignee                                                   |
| `GET`    | `/api/tasks/my`                          | Returns all tasks where caller is assignee (primary or secondary) |

Existing `GET /api/tasks` gains query param `?assignedToUserId=me` that joins `TaskAssignees`.

Request/Response contracts:

```typescript
// POST /api/tasks/{taskId}/assignees
interface AddAssigneeRequest {
  userId: string;
}

// GET /api/tasks/{taskId}/assignees
interface AssigneeDto {
  userId: string;
  fullName: string;
  email: string;
  avatarUrl?: string;
  assignedAt: string; // ISO datetime
  isPrimary: boolean;
}
```

---

### Feature 2 — Case Field Editing

Existing `PUT /api/cases/{id}` endpoint handles case updates. Changes:

- Enforce role guard: `[Authorize(Roles = "Admin,Practitioner")]`
- Inject `IAuditService` to log every changed field with old/new values

No new endpoints needed. Existing case update endpoint already partially audits. The `UpdateCaseRequest` DTO should expose all editable fields including `CourtName`, `CourtSection`, `JudgeSyndic`, `Registrar`, dates, debtor data, practitioner data.

---

### Feature 3 — Workflow Builder Changes

| Method   | Path                        | Description                                  |
| -------- | --------------------------- | -------------------------------------------- |
| `GET`    | `/api/workflow/stages`      | List all stage definitions for tenant        |
| `GET`    | `/api/workflow/stages/{id}` | Get single stage definition                  |
| `POST`   | `/api/workflow/stages`      | Create stage definition                      |
| `PUT`    | `/api/workflow/stages/{id}` | Update stage definition (all changed fields) |
| `DELETE` | `/api/workflow/stages/{id}` | Soft-delete (IsActive = false)               |

Updated `WorkflowStageDefinitionDto`:

```typescript
interface WorkflowStageDefinitionDto {
  id: string;
  stageKey: string;
  name: string;
  description?: string;
  sortOrder: number;
  applicableProcedureTypes: string[];

  // B: Output Document Types — array of DocumentTemplateType strings
  outputDocTypes: string[];

  // C: Structured Validation Rules
  validationRules: ValidationRule[];

  // D: Allowed Transitions — array of stage keys
  allowedTransitions: string[];

  // E: Default Tasks with "required" flag
  outputTasks: OutputTaskDefinition[];

  requiredFields: string[];
  requiredPartyRoles: string[];
  isActive: boolean;
}

interface ValidationRule {
  ruleType: "RequiredField" | "RequiredDocument" | "RequiredParty" | "Custom";
  field?: string;
  condition?: string;
  errorMessage: string;
}

interface OutputTaskDefinition {
  title: string;
  description?: string;
  category?: string;
  deadlineOffsetDays?: number;
  required: boolean; // NEW: if true, stage cannot close until task is Done
}
```

---

### Feature 4 — Procedure Type Change

| Method | Path                                    | Description                 |
| ------ | --------------------------------------- | --------------------------- |
| `POST` | `/api/cases/{id}/change-procedure-type` | Change procedure type       |
| `GET`  | `/api/cases/{id}/procedure-history`     | List procedure type history |

```typescript
// POST /api/cases/{id}/change-procedure-type
interface ChangeProcedureTypeRequest {
  newProcedureType: string; // ProcedureType enum name
  reason: string; // required — logged to audit trail
}

// Response
interface ChangeProcedureTypeResult {
  removedStages: string[]; // stage keys removed
  addedStages: string[]; // new stage keys from new procedure type
  preservedTasks: number; // count of completed tasks kept
}
```

---

### Feature 5 — Case Action Logging

| Method | Path                      | Description                                     |
| ------ | ------------------------- | ----------------------------------------------- |
| `GET`  | `/api/audit-logs`         | List audit logs (admin only), paged, filterable |
| `GET`  | `/api/cases/{id}/history` | Case-specific audit log                         |
| `GET`  | `/api/tasks/{id}/history` | Task-specific audit log                         |

Query params for `/api/audit-logs`: `?category=&entityType=&userId=&from=&to=&page=&pageSize=`

---

### Feature 6 — Mandatory Report Generation

| Method | Path                                        | Description               |
| ------ | ------------------------------------------- | ------------------------- |
| `POST` | `/api/cases/{id}/reports/mandatory`         | Generate mandatory report |
| `GET`  | `/api/cases/{id}/reports/mandatory/preview` | Preview report content    |

```typescript
interface GenerateMandatoryReportRequest {
  activityRangeStart: string; // ISO date
  activityRangeEnd: string; // ISO date — report covers only this range
  createFollowUpTask: boolean; // default true
  reminderOffsetDays?: number; // override tenant default
}

interface GenerateMandatoryReportResult {
  documentId: string;
  followUpTaskId?: string;
  calendarEventId?: string;
  reminderDate: string;
}
```

---

### Feature 7 — Flexible Workflow Execution

| Method | Path                                                  | Description                                   |
| ------ | ----------------------------------------------------- | --------------------------------------------- |
| `POST` | `/api/cases/{id}/workflow/stages/{stageKey}/start`    | Start a stage (even if previous not complete) |
| `POST` | `/api/cases/{id}/workflow/stages/{stageKey}/close`    | Close a stage (passes validation)             |
| `GET`  | `/api/cases/{id}/workflow/stages/{stageKey}/validate` | Dry-run validation check                      |

```typescript
interface StartStageRequest {
  acknowledgeWarnings: boolean; // user confirmed they saw the incomplete-tasks warning
}

interface StageValidationResult {
  canClose: boolean;
  warnings: ValidationWarning[];
  blockers: ValidationBlocker[];
}

interface ValidationWarning {
  type: "IncompletePreviousStage" | "MissingOptionalDocument";
  message: string;
  stageKey?: string;
}

interface ValidationBlocker {
  type:
    | "RequiredTaskIncomplete"
    | "MissingRequiredField"
    | "MissingRequiredDocument";
  message: string;
  fieldName?: string;
  taskId?: string;
}
```

---

### Feature 8 — Document Generation with Save to Case

| Method | Path                                          | Description                                                                                   |
| ------ | --------------------------------------------- | --------------------------------------------------------------------------------------------- |
| `POST` | `/api/cases/{id}/documents/generate`          | Generate document from template                                                               |
| `POST` | `/api/cases/{id}/documents/generate-and-save` | Generate + save DOCX + PDF to case                                                            |
| `POST` | `/api/cases/{id}/documents/{docId}/sign`      | Sign PDF with digital certificate                                                             |
| `GET`  | `/api/documents/sign/certificates`            | List detected signing certificates (client-side call via browser extension or Web Crypto API) |

```typescript
interface GenerateAndSaveRequest {
  templateId: string;
  mergeData?: Record<string, unknown>; // optional overrides
  signWithCertificateThumbprint?: string;
}

interface GenerateAndSaveResult {
  docxDocumentId: string;
  pdfDocumentId: string;
  signed: boolean;
}

interface SignDocumentRequest {
  certificateThumbprint: string;
  signatureReason?: string;
}
```

---

### Feature 9 — Ad-Hoc Task Creation

| Method | Path               | Description        |
| ------ | ------------------ | ------------------ |
| `POST` | `/api/tasks/adhoc` | Create ad-hoc task |

```typescript
interface CreateAdHocTaskRequest {
  caseId?: string; // optional — link to a case
  companyId: string;
  title: string;
  description?: string;
  deadline?: string; // ISO datetime
  additionalAssigneeIds?: string[]; // caller is always primary assignee
}
```

---

## UI Changes

### Feature 1 — Multiple Task Assignees

**Task Detail Panel / Task Edit Modal:**

- Replace single `AssignedTo` dropdown with an "Assignees" section
- Primary assignee (existing field) shown first with a crown/star icon
- Below it: list of additional assignees, each with a Remove (×) button
- "+ Add Assignee" button opens a searchable user picker (fetches from tenant users list)
- "My Tasks" view filters by `assignedToUserId=me` which checks both primary and `TaskAssignees` table

**Task Card (Kanban / List):**

- Show avatar stack of all assignees (max 3 visible, +N overflow)

---

### Feature 2 — Case Field Editing

**Case Detail Page:**

- All editable fields (dates, procedure details, debtor data, court info) always render as editable form controls for `Admin` and `Practitioner` roles
- No toggle between "view mode" and "edit mode" — fields are inline-editable
- On blur/save, display a brief "Saved" toast with undo option (5 s window)
- A collapsible "Change History" section at the bottom of the case page shows the audit trail for that case

---

### Feature 3 — Workflow Builder

**Stage Configuration Form (Settings → Workflow):**

| Section                 | Old UI                   | New UI                                                                                                        |
| ----------------------- | ------------------------ | ------------------------------------------------------------------------------------------------------------- |
| Linked Templates        | Single-select dropdown   | **Removed**                                                                                                   |
| Output Document Types   | Single text/select       | Checkbox list of `DocumentTemplateType` values                                                                |
| Validation Rules        | Free-text textarea       | Structured rule builder (table of rows: rule type dropdown, field picker, condition text, error message text) |
| Allowed Transitions     | Single dropdown          | Multi-select chip list of stage names                                                                         |
| Default Tasks           | List with title/deadline | Same list + "Required" checkbox per row                                                                       |
| Required Task Templates | Shown                    | **Hidden** (data intact in DB)                                                                                |

---

### Feature 4 — Procedure Type Change

**Case Header / Settings:**

- `ProcedureType` field gets an "Edit" pencil icon
- Clicking opens a modal: "Change Procedure Type"
  - New procedure type dropdown
  - Required "Reason" text field
  - Impact preview: "X stages will be removed, Y new stages will be added"
  - Warning: "This action cannot be undone. Completed tasks are preserved."
  - Confirm button

---

### Feature 5 — Case Action Logging

**Case Detail — History Tab:**

- Timeline view sorted by timestamp desc
- Each entry: icon (based on `Category`), timestamp, user avatar+name, action description, expandable diff (old → new)
- Filter by action type (dropdown), date range picker

**Admin Panel — Audit Logs:**

- Full table with columns: Timestamp, User, Action, Entity, Case Number, Severity
- Export to CSV button
- Filter panel: date range, user, category, entity type

---

### Feature 6 — Mandatory Report Generation

**Report Generator Page:**

- Add "Activity Range" date range picker (Start Date, End Date) — required
- Remove "Future Tasks" section from preview
- After generation: show "Follow-up task created" confirmation with task link and reminder date
- Optional: allow user to override the reminder interval for this generation

---

### Feature 7 — Flexible Workflow Execution

**Case Workflow Panel:**

- Each stage card has "Start Stage" button regardless of previous stage status
- If previous stage(s) are not complete when starting, a warning banner appears: "Stage X is not yet complete. You may continue, but tasks from that stage remain open."
- "Complete Stage" button triggers validation:
  - If required tasks incomplete → blocked message with list of blocking tasks
  - If optional tasks incomplete → warning with ability to confirm completion anyway
- Stages are not auto-closed — each has independent Start / Complete lifecycle

---

### Feature 8 — Document Generation

**Document Generator:**

- Add "Save to Case" button next to "Download"
- On click: generates both DOCX and PDF server-side, stores both in case documents
- "Sign PDF" button (conditionally shown if tenant has signing enabled):
  - Detects certificates via Web Crypto API / local signing agent
  - Dropdown to select certificate
  - PAdES signature applied on server using the selected cert
- After save, document appears in case "Documents" tab immediately

---

### Feature 9 — Ad-Hoc Task Creation

**Task Panel / "New Task" FAB button:**

- "+ New Task" button always visible on case page and dashboard
- Opens a lightweight modal:
  - Title (required)
  - Description (optional, rich text)
  - Deadline (date picker)
  - Assignees section (caller pre-populated; add more via user picker)
  - "Not linked to workflow stage" label
- Submit creates ad-hoc task; appears in task list with "Ad-hoc" badge

---

## Migration Steps

### Step 1 — Database migrations (EF Core)

Run in order:

```
1. AddTaskAssigneesTable
   - Create TaskAssignees table
   - Seed existing CompanyTasks.AssignedToUserId into TaskAssignees

2. AddCaseProcedureHistoryTable
   - Create CaseProcedureHistory table

3. AddAuditLogIndexes
   - Add composite indexes on AuditLogs

4. AddWorkflowTaskRequiredFlag
   - No DDL; JSON contract change only (backward compatible — missing "required" = false)

5. AddIsAdHocAndWorkflowStageIdToTasks
   - Add IsAdHoc BOOLEAN DEFAULT FALSE
   - Add WorkflowStageId UUID nullable FK

6. AddReportReminderIntervalDaysToTenants
   - Add ReportReminderIntervalDays INT DEFAULT 90 to Tenants
```

### Step 2 — Data migration scripts

```sql
-- Seed TaskAssignees from existing primary assignees
INSERT INTO TaskAssignees (Id, TenantId, TaskId, UserId, AssignedAt, AssignedBy)
SELECT gen_random_uuid(), ct.TenantId, ct.Id, ct.AssignedToUserId,
       ct.CreatedOn, ct.CreatedByUserId
FROM CompanyTasks ct
WHERE ct.AssignedToUserId IS NOT NULL
ON CONFLICT (TaskId, UserId) DO NOTHING;

-- Mark existing workflow-generated tasks as non-adhoc
UPDATE CompanyTasks SET IsAdHoc = FALSE WHERE IsAdHoc IS NULL;
```

### Step 3 — API backward compatibility

- Existing `/api/tasks` responses add `assignees: AssigneeDto[]` field (empty array if none, always includes primary)
- Existing task create/update requests still accept `assignedToUserId` — it sets the primary assignee AND creates a row in `TaskAssignees`

### Step 4 — Frontend feature flags

Deploy each UI section behind a feature flag in `appsettings.json`:

```json
"FeatureFlags": {
  "MultipleTaskAssignees": true,
  "FlexibleWorkflowExecution": true,
  "DocumentSaveToCase": true,
  "AdHocTasks": true
}
```

Enable flags progressively per tenant.

---

## Security Considerations

### Multi-tenancy

- `TaskAssignees`, `CaseProcedureHistory` must include `TenantId` and all repository queries must scope by `TenantId` from the authenticated user's claim
- User picker for assignees fetches only users within the same tenant (enforced at API level, not just UI)

### Authorization

| Feature                         | Required Role                                   |
| ------------------------------- | ----------------------------------------------- |
| Case field editing              | `Admin`, `Practitioner`                         |
| Workflow builder (stage config) | `Admin`                                         |
| Procedure type change           | `Admin`, `Practitioner`                         |
| View audit logs (full)          | `Admin`                                         |
| View case history               | `Admin`, `Practitioner`                         |
| Generate mandatory report       | `Practitioner`                                  |
| Digital signature               | Any authenticated user with a valid certificate |
| Ad-hoc task creation            | `Admin`, `Practitioner`                         |

### Audit integrity

- `AuditLog` records must never be updated or deleted (append-only)
- Consider a separate append-only DB role for INSERT-only access to `AuditLogs` table

### Document signing

- Private keys never leave the client machine — signing must happen client-side using Web Crypto API or a local signing agent
- The server receives only the signature (PKCS#7 / CAdES) and the public certificate for verification and embedding into the PDF
- Validate certificate chain server-side (OCSP or CRL check) before embedding

### Input validation

- Structured validation rules (Feature 3C) are evaluated server-side; `condition` field is a whitelist-enum expression, never `eval()`-executed
- `ChangeProcedureTypeRequest.reason` max length 2000 chars, stored verbatim (no HTML)

---

## Testing Plan

### Feature 1 — Multiple Task Assignees

**Unit tests:**

- `TaskAssigneeService.AddAssignee` — duplicate assignee returns 409 Conflict
- `TaskAssigneeService.RemoveAssignee` — removing primary assignee shifts primary to next (or clears)
- `TaskQuery.GetMyTasks` — returns tasks where user is primary OR secondary assignee

**Integration tests:**

- POST assignee → GET task → verify assignee list contains new user
- DELETE assignee → GET task → verify assignee absent
- Notification dispatch → all assignees receive email on task status change

**Acceptance criteria:**

- [ ] Task can have 1–N assignees
- [ ] "My Tasks" shows tasks for all assignees
- [ ] Removing all assignees leaves task unassigned
- [ ] Notifications sent to every assignee on status change

---

### Feature 2 — Case Field Editing

**Unit tests:**

- `CaseService.UpdateCaseFields` — audit log created with old/new values for each changed field
- Unauthorized role (e.g. `Viewer`) → 403 response

**Integration tests:**

- PUT case with changed `CourtName` → audit log row exists with `OldValues.CourtName` and `NewValues.CourtName`

**Acceptance criteria:**

- [ ] Practitioner and Admin can edit any case field at any time
- [ ] Each save creates an audit log entry with field-level diff
- [ ] Viewer role cannot edit case fields

---

### Feature 3 — Workflow Builder

**Unit tests:**

- `WorkflowStageValidator.Validate` — required task with `required: true` blocks stage close
- `ValidationRuleEngine.Evaluate` — structured rule with `ruleType: RequiredField, field: NoticeDate` returns error when field null

**UI tests (Playwright/Cypress):**

- Workflow stage edit form: check all checkboxes in Output Document Types → save → reload → same boxes checked
- Add validation rule row → fill fields → save → rule persists

**Acceptance criteria:**

- [ ] Output document types are a multi-select checkbox list
- [ ] Validation rules are structured (no free text)
- [ ] Allowed transitions support multi-select
- [ ] Required task flag blocks stage completion
- [ ] Linked Templates section is absent from UI

---

### Feature 4 — Procedure Type Change

**Unit tests:**

- `ProcedureTypeChangeService` — only `NotStarted` stages removed; `InProgress` and `Completed` stages remain
- `ProcedureTypeChangeService` — new stages created from new procedure type workflow definition
- Audit log entry created with old and new procedure types

**Integration tests:**

- POST `/change-procedure-type` with valid payload → case `ProcedureType` updated, `CaseProcedureHistory` row created
- GET `/procedure-history` → history list includes the change

**Acceptance criteria:**

- [ ] Only not-started stages and their tasks are removed
- [ ] Completed tasks are preserved
- [ ] New workflow stages generated from new procedure type
- [ ] Full audit trail recorded

---

### Feature 5 — Case Action Logging

**Unit tests:**

- Each auditable service method produces exactly one `AuditLog` entry
- `AuditLog.Changes` is valid JSON with `oldValue` and `newValue` keys

**Integration tests:**

- Perform 10 distinct case/task actions → query `/api/cases/{id}/history` → verify 10 entries

**Acceptance criteria:**

- [ ] Every listed action type produces an audit log entry
- [ ] Each entry contains timestamp, user, action type, entity, old value, new value
- [ ] Audit logs cannot be modified or deleted via any API endpoint

---

### Feature 6 — Mandatory Report Generation

**Unit tests:**

- `MandatoryReportService.Generate` — output contains only events within `activityRangeStart`–`activityRangeEnd`
- `MandatoryReportService.Generate` — follow-up task created with `Deadline = generationDate + reminderIntervalDays`
- `MandatoryReportService.Generate` — calendar event created with same reminder date

**Acceptance criteria:**

- [ ] Report contains only past activity within selected range
- [ ] No future tasks appear in report
- [ ] Follow-up task automatically created on generation
- [ ] Calendar reminder set to configurable interval days after generation date

---

### Feature 7 — Flexible Workflow Execution

**Unit tests:**

- `WorkflowStageService.StartStage` — can start stage even if previous stage `InProgress`
- `WorkflowStageService.CloseStage` — returns `canClose: false` when required tasks incomplete
- `WorkflowStageService.CloseStage` — returns `canClose: true` when all required tasks done

**Acceptance criteria:**

- [ ] Any stage can be started independently
- [ ] Warning shown when starting with previous stage incomplete
- [ ] Stage cannot close if required tasks are incomplete
- [ ] Stage can close with optional tasks incomplete (user confirms)
- [ ] Previous stage is never auto-closed

---

### Feature 8 — Document Generation

**Unit tests:**

- `DocumentGenerationService.GenerateAndSave` — produces DOCX and PDF artifacts
- `DocumentGenerationService.GenerateAndSave` — both documents persisted in `InsolvencyDocuments` with correct `CaseId`
- `PdfSigningService.Sign` — invalid certificate thumbprint returns 400

**Integration tests:**

- POST generate-and-save → GET case documents → DOCX and PDF both visible

**Acceptance criteria:**

- [ ] "Save to Case" button always available on document generator
- [ ] Both DOCX and PDF saved to case document repository
- [ ] Signed PDF has verifiable digital signature
- [ ] Unsigned and signed copies both accessible
- [ ] Audit log entry for document generation and save

---

### Feature 9 — Ad-Hoc Task Creation

**Unit tests:**

- `TaskService.CreateAdHoc` — `IsAdHoc = true`, `WorkflowStageId = null`
- `TaskService.CreateAdHoc` — creator is always primary assignee

**Acceptance criteria:**

- [ ] Ad-hoc task can be created from any case page or dashboard
- [ ] Creator auto-assigned as primary assignee
- [ ] Additional assignees can be added at creation time
- [ ] Ad-hoc tasks appear in task list with "Ad-hoc" badge
- [ ] Ad-hoc tasks appear in My Tasks of all assignees
- [ ] Ad-hoc tasks are independent from workflow stage lifecycle

---

## Diagrams

### Task Assignee Data Model

```
CompanyTasks
┌────────────────────────────────────┐
│ Id (PK)                            │
│ TenantId                           │
│ AssignedToUserId (primary, FK)     │  ←── still primary assignee
│ IsAdHoc                            │
│ WorkflowStageId (FK, nullable)     │
└────────────────────────────────────┘
            │ 1
            │
            │ N
┌───────────────────────────┐
│ TaskAssignees             │
│ Id (PK)                   │
│ TenantId                  │
│ TaskId (FK)               │
│ UserId (FK)               │
│ AssignedAt                │
│ AssignedBy (FK, nullable) │
└───────────────────────────┘
```

### Flexible Workflow Stage State Machine

```
NotStarted ──[Start (any time)]──▶ InProgress
                                        │
                    ┌───────────────────┘
                    │  [Validate: required tasks done + fields present]
                    ▼
               (warning if optional tasks open)
                    │
                    ▼
               Completed
                    │
               (or Skipped via Admin override)
```

### Procedure Type Change Flow

```
User selects new ProcedureType
         │
         ▼
  Load current CaseWorkflowStages
         │
         ├──▶ Stages with Status = NotStarted ──▶ DELETE
         │
         ├──▶ Stages with Status = InProgress/Completed/Skipped ──▶ KEEP
         │
         ▼
  Load WorkflowStageDefinitions for new ProcedureType
         │
         ▼
  Insert new CaseWorkflowStages (NotStarted)
  for stages not already present
         │
         ▼
  Write CaseProcedureHistory row
  Write AuditLog entry
  Send notification to case practitioner
```

### Document Generate-and-Save Flow

```
User clicks "Save to Case"
         │
         ▼
POST /api/cases/{id}/documents/generate-and-save
         │
         ├──▶ Render template → DOCX bytes (server, DocX / Aspose)
         │
         ├──▶ Convert DOCX → PDF (LibreOffice headless / Gotenberg)
         │
         ├──▶ [Optional] Apply digital signature (PAdES)
         │
         ├──▶ Save DOCX to IFileStorageService → InsolvencyDocuments row
         │
         ├──▶ Save PDF  to IFileStorageService → InsolvencyDocuments row
         │
         ├──▶ Write AuditLog (DocumentGenerated + DocumentSavedToCase)
         │
         └──▶ Return { docxDocumentId, pdfDocumentId, signed }
```
