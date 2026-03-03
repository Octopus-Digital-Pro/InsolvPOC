# Current State — Navigation-Based Feature Map (2026-03-03)

> This document describes every navigable area of the Insolvex application, the functionality available under each, and the role-based permissions that govern access.

---

## Roles & Permission Model

Four roles exist, each a superset of the one below it:

| Role | Scope |
|------|-------|
| **GlobalAdmin** | All permissions. Cross-tenant operations, system config, demo reset, tenant AI config. |
| **TenantAdmin** | Everything except `SystemConfigView`, `SystemConfigEdit`, `DemoReset`, `TenantAiConfigEdit`. |
| **Practitioner** | Full case work — case CRUD, documents, signing, tasks, meetings, templates, AI chat, assets, emails, dashboard. Cannot manage users, tenants, or system config. |
| **Secretary** | View-heavy — can view/create/edit cases, parties, documents (no delete), tasks (no delete), templates (no manage), companies (no edit/delete), assets (no delete). No workflow advancement, no meeting creation, no summary generation. |

Permissions are enforced on every API endpoint via `[RequirePermission(Permission.X)]` attributes.

---

## 1. Login & Authentication

| URL | Type |
|-----|------|
| `/login` | UI |
| `/api/auth/login` | API |
| `/api/auth/me` | API |
| `/api/auth/change-password` | API |
| `/api/auth/forgot-password` | API |
| `/api/auth/reset-password` | API |

### Functionality
- **Login form** authenticates against backend; on success a JWT is issued and stored client-side.
- `AuthProvider` context wraps the app; checks `/auth/me` on load to restore session.
- Unauthenticated users are redirected to `/login`; authenticated users on `/login` are redirected to `/dashboard`.
- **Change password** — in-session, requires current password.
- **Forgot / reset password** — generates a time-limited token; user follows link to set new password.

### Permissions by role
| | GlobalAdmin | TenantAdmin | Practitioner | Secretary |
|--|:-:|:-:|:-:|:-:|
| Login | ✅ | ✅ | ✅ | ✅ |
| Change own password | ✅ | ✅ | ✅ | ✅ |
| Forgot/reset password | ✅ | ✅ | ✅ | ✅ |

---

## 2. Tenant Context & Switching

| URL | Type |
|-----|------|
| Sidebar tenant selector | UI (app shell) |
| `/admin/tenants` | UI |
| `/api/tenants` | API |

### Functionality
- **Tenant selector** appears in the sidebar for GlobalAdmin only when multiple tenants exist.
- Selecting a tenant persists the choice to `localStorage` and forces a full page reload (`window.location.reload()`) to flush all client caches — zero data leakage.
- `TenantProvider` context resolves the active tenant from `localStorage → current → first available`.
- `TenantResolutionMiddleware` on the API reads the tenant from the JWT and stores `ResolvedTenantId` for every request.
- **EF Core global query filters** enforce row-level isolation — every query is scoped to the selected tenant.
- **Tenant Admin page** (`/admin/tenants`) — GlobalAdmin-only CRUD: create/edit/delete tenants, view stats (users, companies, cases per tenant), set plan (Free/Professional/Enterprise), set subscription expiry date.

### Permissions by role
| | GlobalAdmin | TenantAdmin | Practitioner | Secretary |
|--|:-:|:-:|:-:|:-:|
| See tenant selector | ✅ | ❌ | ❌ | ❌ |
| Switch tenants | ✅ | ❌ | ❌ | ❌ |
| Tenant CRUD | ✅ | ❌ | ❌ | ❌ |

---

## 3. User Management

| URL | Type |
|-----|------|
| `/settings/users` | UI |
| `/accept-invitation` | UI (public) |
| `/api/users` | API |
| `/api/users/{id}/reset-password` | API |
| `/api/users/invite` | API |
| `/api/users/accept-invitation` | API |
| `/api/users/roles` | API |
| `/api/users/my-permissions` | API |

### Functionality
- **User list** — table of all tenant users with name, email, role, active status.
- **Inline edit modal** — click user → modal with fields for name, role dropdown, active toggle, save.
- **Admin password reset** — inside edit modal, admin can force-reset a user's password. GlobalAdmin can reset any user; TenantAdmin cannot reset GlobalAdmin passwords.
- **Invite flow** — admin enters email + role; system generates invitation token (7-day expiry). Token is displayed for manual copy or sent via email.
- **Accept invitation page** (`/accept-invitation`) — public route; invitee enters name + password to self-register.
- **Invitation management** — list pending invitations, revoke individual invitations.
- **Role & permission introspection** — `/api/users/roles` returns the role catalog; `/api/users/my-permissions` returns the effective permission set for the current user so the frontend can conditionally show/hide UI elements.

### Permissions by role
| | GlobalAdmin | TenantAdmin | Practitioner | Secretary |
|--|:-:|:-:|:-:|:-:|
| View users | ✅ (`UserView`) | ✅ | ❌ | ❌ |
| Edit users | ✅ (`UserEdit`) | ✅ | ❌ | ❌ |
| Invite users | ✅ (`UserInvite`) | ✅ | ❌ | ❌ |
| Deactivate users | ✅ (`UserDeactivate`) | ✅ | ❌ | ❌ |
| Reset passwords | ✅ | ✅ (not GlobalAdmin users) | ❌ | ❌ |
| View own permissions | ✅ | ✅ | ✅ | ✅ |

---

## 4. Dashboard

| URL | Type |
|-----|------|
| `/dashboard` | UI |
| `/api/dashboard` | API |

### Functionality
- Landing page after login. Aggregates operational metrics: active cases count, overdue tasks, upcoming deadlines, recent activity.
- Visible to all authenticated roles.

### Permissions by role
| | GlobalAdmin | TenantAdmin | Practitioner | Secretary |
|--|:-:|:-:|:-:|:-:|
| View dashboard | ✅ (`DashboardView`) | ✅ | ✅ | ✅ |

---

## 5. Companies

| URL | Type |
|-----|------|
| `/companies` | UI — list |
| `/companies/new` | UI — create form |
| `/companies/{id}` | UI — detail |
| `/companies/{id}/edit` | UI — edit form |
| `/api/companies` | API |
| `/api/companies/search` | API |
| `/api/companies/export-csv` | API |
| `/api/companies/{companyId}/parties` | API |

### Functionality
- **Company list** — paginated/filterable table with name, CUI, county, status.
- **Create / Edit forms** — full company identity fields (name, CUI, trade register no., CAEN code, address, locality, county, postal code, phone, status, founding year, capital).
- **Company detail page** — displays company metadata and linked parties.
- **Search** — free-text query with result limit.
- **CSV export** — downloads all company data as CSV.
- **Company parties** — lists stakeholders linked to a specific company.

### Permissions by role
| | GlobalAdmin | TenantAdmin | Practitioner | Secretary |
|--|:-:|:-:|:-:|:-:|
| View companies | ✅ (`CompanyView`) | ✅ | ✅ | ✅ |
| Create company | ✅ (`CompanyCreate`) | ✅ | ✅ | ✅ |
| Edit company | ✅ (`CompanyEdit`) | ✅ | ✅ | ❌ |
| Delete company | ✅ (`CompanyDelete`) | ✅ | ❌ | ❌ |

---

## 6. Tasks

| URL | Type |
|-----|------|
| `/tasks` | UI |
| `/api/tasks` | API |
| `/api/tasks/{id}` | API |
| `/api/tasks/{taskId}/notes` | API |
| `/api/cases/{caseId}/tasks` | API |

### Functionality
- **Task list page** — global workload view across all cases. Filter by status, assignee, category.
- **Task CRUD** — create tasks with title, description, deadline, category (Document / Email / Filing / Meeting / Call / Review / Payment / Report / Compliance), assigned user, and linked case.
- **Task detail** — view/edit individual task fields and status.
- **Task notes** — threaded notes per task for collaboration (add, edit, delete notes).
- **Case-scoped tasks** — tasks shown within the case detail Tasks tab, filtered by case ID.
- **Task summary** — aggregated task statistics endpoint per case.

### Permissions by role
| | GlobalAdmin | TenantAdmin | Practitioner | Secretary |
|--|:-:|:-:|:-:|:-:|
| View tasks | ✅ (`TaskView`) | ✅ | ✅ | ✅ |
| Create tasks | ✅ (`TaskCreate`) | ✅ | ✅ | ✅ |
| Edit tasks | ✅ (`TaskEdit`) | ✅ | ✅ | ✅ |
| Delete tasks | ✅ (`TaskDelete`) | ✅ | ✅ | ❌ |

---

## 7. Cases

| URL | Type |
|-----|------|
| `/cases` | UI — list |
| `/cases/new` | UI — create |
| `/cases/{id}` | UI — detail (tabbed workspace) |
| `/api/cases` | API |
| `/api/cases/{id}` | API |
| `/api/cases/export-csv` | API |

### Functionality
- **Case list** — filterable/sortable table showing case number, debtor, court, status, procedure type, assigned user.
- **New case** — form with case number, debtor name/CUI, court, section, judge syndic, procedure type (Insolvență / Faliment / Faliment Simplificat / Reorganizare / Concordat Preventiv / Mandat Ad-Hoc), opening date, claims deadline, law reference, linked company, linked tribunal.
- **Case detail** — full-page workspace with header showing case metadata and financial summary, plus a tab bar for sub-areas.

### Case Detail Header
The header renders: case number, debtor name, status badge, court info, judge syndic, procedure type, law reference, practitioner, assigned user (editable dropdown), opening date, next hearing, claims deadline, BPI publication number, opening decision number. Financial summary shows total/secured/unsecured/budgetary/employee claims and estimated asset value.

**Action buttons** in the header area:
- **Call Creditor Meeting** — opens meeting scheduling modal.
- **Generate Mandatory Report** — renders the `mandatoryReport` template with configurable past/future task date ranges, opens preview modal.
- **Send Email** — opens email compose modal.
- **Close Case** / **Reopen Case** — lifecycle controls (admin/practitioner only).

### Permissions by role
| | GlobalAdmin | TenantAdmin | Practitioner | Secretary |
|--|:-:|:-:|:-:|:-:|
| View cases | ✅ (`CaseView`) | ✅ | ✅ | ✅ |
| Create case | ✅ (`CaseCreate`) | ✅ | ✅ | ✅ |
| Edit case | ✅ (`CaseEdit`) | ✅ | ✅ | ✅ |
| Delete case | ✅ (`CaseDelete`) | ✅ | ❌ | ❌ |
| Export CSV | ✅ (`CaseExport`) | ✅ | ✅ | ✅ |
| Close case | ✅ (`CaseClose`) | ✅ | ✅ | ❌ |
| Reopen case | ✅ (`CaseReopen`) | ✅ | ❌ | ❌ |

---

### 7.1 Tab: Overview / AI

The first tab. Behaviour depends on whether AI is enabled for the case:

**AI disabled** — shows an AI Case Summary panel:
- "Generate" button calls `POST /api/cases/{caseId}/generate` to produce a text summary.
- Displays the latest summary if one exists, with a "Refresh" button.

**AI enabled** — renders the `CaseAiTab` component:
- Full conversational AI chat interface scoped to the case context.
- Chat history retrieval, message submission, and history clearing via `/api/cases/{caseId}/ai/chat`.
- AI summary generation/retrieval via `/api/cases/{caseId}/ai/summary`.

| | GlobalAdmin | TenantAdmin | Practitioner | Secretary |
|--|:-:|:-:|:-:|:-:|
| View summary | ✅ (`SummaryView`) | ✅ | ✅ | ✅ |
| Generate summary | ✅ (`SummaryGenerate`) | ✅ | ✅ | ❌ |
| AI chat | ✅ (`AiChatUse`) | ✅ | ✅ | ✅ |

---

### 7.2 Tab: Workflow

Renders `CaseWorkflowPanel` — the stage-by-stage legal process tracker.

**Functionality:**
- On first access for a case, the engine loads all active global `WorkflowStageDefinition` records plus tenant overrides (by matching `StageKey`), filters by the case's `ProcedureType`, and creates one `CaseWorkflowStage` row per applicable stage, ordered by `SortOrder`. The first stage auto-starts.
- Each stage card shows: name, status (NotStarted / InProgress / Completed / Skipped), deadline, linked document and task progress.
- **Validate** — checks prerequisite gates before allowing completion: required fields on the case entity, required party roles, required document types, required completed tasks.
- **Start / Complete / Skip / Reopen** — stage lifecycle actions.
- **Stage deadline** — editable per-stage due date.
- **Case Close / Reopen** — end-of-process controls with closeability check endpoint.

| API | Method |
|-----|--------|
| `/api/cases/{caseId}/workflow` | GET |
| `/api/cases/{caseId}/workflow/{stageKey}/validate` | GET |
| `/api/cases/{caseId}/workflow/{stageKey}/start` | POST |
| `/api/cases/{caseId}/workflow/{stageKey}/complete` | POST |
| `/api/cases/{caseId}/workflow/{stageKey}/skip` | POST |
| `/api/cases/{caseId}/workflow/{stageKey}/reopen` | POST |
| `/api/cases/{caseId}/workflow/{stageKey}/deadline` | PUT |
| `/api/cases/{caseId}/workflow/closeability` | GET |
| `/api/cases/{caseId}/workflow/close` | POST |
| `/api/cases/{caseId}/workflow/reopen` | POST |

| | GlobalAdmin | TenantAdmin | Practitioner | Secretary |
|--|:-:|:-:|:-:|:-:|
| View workflow | ✅ (`PhaseView`, `StageView`) | ✅ | ✅ | ✅ |
| Start/complete/skip/reopen stages | ✅ (`PhaseEdit`, `StageAdvance`) | ✅ | ✅ | ❌ |
| Override deadline | ✅ (`PhaseDeadlineOverride`) | ✅ | ❌ | ❌ |
| Initialize workflow | ✅ (`PhaseInitialize`) | ✅ | ✅ | ❌ |

---

### 7.3 Tab: Tasks

Renders `CaseTasksTab` — case-scoped task management.

- Lists tasks linked to the case with status, assignee, deadline, category.
- Create new tasks directly on the case.
- Edit task status/details inline.
- Task count shown in the tab label.

---

### 7.4 Tab: Documents

- Lists all documents attached to the case with type, name, upload date.
- **Upload** — select document type from dropdown (CourtOpeningDecision, CreditorClaim, ReportArt97, etc.), then choose file (`.pdf`, `.doc`, `.docx`, images).
- **Download ZIP** — downloads all case documents as a single archive.
- **Document type selector** — uses `CASE_DOCUMENT_TYPES` constant for the dropdown.
- **Folder initialization** — `POST /api/cases/{caseId}/documents/ensure-folders` creates the standard folder structure.

| | GlobalAdmin | TenantAdmin | Practitioner | Secretary |
|--|:-:|:-:|:-:|:-:|
| View documents | ✅ (`DocumentView`) | ✅ | ✅ | ✅ |
| Upload documents | ✅ (`DocumentUpload`) | ✅ | ✅ | ✅ |
| Edit document metadata | ✅ (`DocumentEdit`) | ✅ | ✅ | ❌ |
| Delete documents | ✅ (`DocumentDelete`) | ✅ | ✅ | ❌ |
| Download | ✅ (`DocumentDownload`) | ✅ | ✅ | ✅ |

---

### 7.5 Tab: Parties

- Lists case parties with role (Debtor, InsolvencyPractitioner, SecuredCreditor, UnsecuredCreditor, BudgetaryCreditor, EmployeeCreditor, JudgeSyndic, CourtExpert, CreditorsCommittee, SpecialAdministrator, Guarantor, ThirdParty), name, contact info.
- Add party modal — select role, enter name and details.
- Remove party — delete with confirmation.

| | GlobalAdmin | TenantAdmin | Practitioner | Secretary |
|--|:-:|:-:|:-:|:-:|
| View parties | ✅ (`PartyView`) | ✅ | ✅ | ✅ |
| Add party | ✅ (`PartyCreate`) | ✅ | ✅ | ✅ |
| Edit party | ✅ (`PartyEdit`) | ✅ | ✅ | ✅ |
| Delete party | ✅ (`PartyDelete`) | ✅ | ✅ | ❌ |

---

### 7.6 Tab: Assets

Renders `CaseAssetsTab` — asset inventory for the debtor.

- CRUD for case assets: description, category, estimated value, status.

| | GlobalAdmin | TenantAdmin | Practitioner | Secretary |
|--|:-:|:-:|:-:|:-:|
| View assets | ✅ (`AssetView`) | ✅ | ✅ | ✅ |
| Create asset | ✅ (`AssetCreate`) | ✅ | ✅ | ✅ |
| Edit asset | ✅ (`AssetEdit`) | ✅ | ✅ | ✅ |
| Delete asset | ✅ (`AssetDelete`) | ✅ | ✅ | ❌ |

---

### 7.7 Tab: Emails

Renders `CaseEmailsTab` — case communication management.

- Lists sent/scheduled emails with subject, recipients, date, status.
- **Compose** — `EmailComposeModal` with subject, body, attachment selection, party-based recipient picker.
- **Bulk email** — send to multiple parties at once via `/api/cases/{caseId}/bulk-email`.
- **Delete** emails from the case record.
- Email count shown in the tab label.

| | GlobalAdmin | TenantAdmin | Practitioner | Secretary |
|--|:-:|:-:|:-:|:-:|
| View emails | ✅ (`EmailView`) | ✅ | ✅ | ✅ |
| Send emails | ✅ (`EmailCreate`) | ✅ | ✅ | ✅ |
| Delete emails | ✅ (`EmailDelete`) | ✅ | ❌ | ❌ |

---

### 7.8 Tab: Calendar

- Displays case events and deadlines on a calendar view.
- Create/edit calendar events.
- Unified calendar endpoint merges deadlines, hearings, meetings, and custom events.

| API | Method |
|-----|--------|
| `/api/cases/{caseId}/calendar` | GET / POST |
| `/api/cases/{caseId}/calendar/{eventId}` | PUT |
| `/api/cases/{caseId}/calendar/unified` | GET |

---

### 7.9 Tab: Templates

- Shows templates available for the case context.
- Generate documents from templates — renders with case data, previews output, and can save to case documents.
- Renders `TemplatePreviewModal` for reviewing generated output before saving.

---

### 7.10 Tab: Activity

- Chronological event feed for the case sourced from `/api/cases/{caseId}/events`.
- Shows audit entries, status changes, document uploads, party changes.

---

## 8. Reports

| URL | Type |
|-----|------|
| `/reports` | UI |

### Functionality
- Reports workspace page — admin-only area for generating and viewing reports.
- Case summary generation/history via `/api/cases/{caseId}/generate`, `/api/cases/{caseId}/latest`, `/api/cases/{caseId}/history`.

### Permissions by role
| | GlobalAdmin | TenantAdmin | Practitioner | Secretary |
|--|:-:|:-:|:-:|:-:|
| View reports page | ✅ | ✅ | ❌ | ❌ |

---

## 9. Audit Trail

| URL | Type |
|-----|------|
| `/audit-trail` | UI |
| `/api/auditlogs` | API |

### Functionality
- Lists all audit log entries with expandable detail rows.
- Filters by category (Auth, Case, Document, Task, Party, Workflow, Signing, Meeting, Settings, User, System), severity (Info, Warning, Critical), date range.
- Stats dashboard showing action counts, severity distribution.
- Each log record includes: user full name, tenant name, entity name, case number, IP, description, old→new JSON diff.

### Permissions by role
| | GlobalAdmin | TenantAdmin | Practitioner | Secretary |
|--|:-:|:-:|:-:|:-:|
| View audit trail | ✅ (`AuditLogView`) | ✅ | ❌ | ❌ |

---

## 10. Settings

Settings uses a dedicated sidebar layout (`SettingsLayout`) with grouped navigation. Below is each settings sub-page.

---

### 10.1 Organisation (`/settings`)

| API | Method |
|-----|--------|
| `/api/settings/tenant` | GET / PUT |
| `/api/settings/firm` | GET / PUT |

**Functionality:**
- Organisation name, contact details, address.
- Firm profile (practitioner firm identity used in generated documents): firm name, registration, address, bank details, logo.

| | GlobalAdmin | TenantAdmin | Practitioner | Secretary |
|--|:-:|:-:|:-:|:-:|
| View | ✅ (`SettingsView`) | ✅ | ✅ | ✅ |
| Edit | ✅ (`SettingsEdit`) | ✅ | ❌ | ❌ |

---

### 10.2 Team & Users (`/settings/users`)

See **Section 3 — User Management** above.

---

### 10.3 E-Signing (`/settings/signing`)

| API | Method |
|-----|--------|
| `/api/signing/keys/upload` | POST |
| `/api/signing/keys` | GET |
| `/api/signing/keys/status` | GET |
| `/api/signing/keys/{id}` | DELETE |
| `/api/signing/keys/windows-certs` | GET |
| `/api/signing/preferences` | GET / PUT |
| `/api/signing/sign/{documentId}` | POST |
| `/api/signing/upload-signed/{documentId}` | POST |
| `/api/signing/verify/{documentId}` | GET |
| `/api/signing/my-signatures` | GET |

**Functionality:**
- **PFX upload** — upload a `.pfx` digital certificate with password.
- **Key list** — shows thumbprint, validity dates, last used.
- **Windows certificate discovery** — enumerates certificates from the Windows cert store.
- **Signing preferences** — per-user signature behavior defaults.
- **Sign document** — server-side signing using stored PFX key.
- **Upload externally signed** — upload a document signed outside the platform.
- **Verify** — validates digital signature integrity.
- **My recent signatures** — lists documents signed by the current user with validity status.
- **Deactivate** — remove expired/unused signing keys.

| | GlobalAdmin | TenantAdmin | Practitioner | Secretary |
|--|:-:|:-:|:-:|:-:|
| Manage signing keys | ✅ (`SigningKeyManage`) | ✅ | ✅ | ✅ |
| Sign documents | ✅ (`DocumentSign`) | ✅ | ✅ | ✅ |
| Verify signatures | ✅ (`SignatureVerify`) | ✅ | ✅ | ✅ |

---

### 10.4 Firms Database / ONRC (`/settings/firms-database`)

| API | Method |
|-----|--------|
| `/api/onrc/search` | GET |
| `/api/onrc/search/cui` | GET |
| `/api/onrc/search/name` | GET |
| `/api/onrc/import` | POST |
| `/api/onrc/stats` | GET |

**Functionality:**
- **Region selector** — Romania (supported) or Hungary (placeholder, import not yet available).
- **Database stats** — shows total records, last import timestamp.
- **CSV import** — opens `CsvUploadModal` for bulk import of ONRC open-data CSV files. Delimiter: `^` (caret). Supports files up to ~700 MB. Expected columns: CUI, Denumire, Nr. Reg. Com., CAEN, Adresa, Localitate, Judet, Cod Postal, Telefon, Stare, An Infiintare, Capital Social.
- **Search** — free-text search against imported ONRC records; displays results with name, CUI, trade register number, county, locality, CAEN, and active/inactive status badge.
- **Note**: The ONRC database is a local mirror to reduce external API dependency and enable fast lookups during case/company creation.

---

### 10.5 Tribunals (`/settings/tribunals`)

| API | Method |
|-----|--------|
| `/api/tribunals` | GET / POST |
| `/api/tribunals/{id}` | GET / PUT / DELETE |
| `/api/tribunals/import-csv` | POST |
| `/api/tribunals/export-csv` | GET |

**Functionality:**
- CRUD for court/tribunal contact records: name, section, locality, county, address, postal code, registry phone/fax/email, registry hours, website, contact person, notes.
- **Global vs. Tenant override pattern** — GlobalAdmin imports master data (global, `TenantId = null`). TenantAdmin creates custom overrides (`OverridesGlobalId` links to parent).
- CSV import (multipart/form-data) and CSV export.

---

### 10.6 Finance / ANAF (`/settings/finance`)

| API | Method |
|-----|--------|
| `/api/finance-authorities` | GET / POST |
| `/api/finance-authorities/{id}` | GET / PUT / DELETE |
| `/api/finance-authorities/import-csv` | POST |
| `/api/finance-authorities/export-csv` | GET |
| `/api/finance-authorities/scrape-anaf` | POST |

**Functionality:**
- Same CRUD + CSV import/export pattern as Tribunals.
- **ANAF scrape** — endpoint to refresh data from the Romanian fiscal authority's public sources.

---

### 10.7 Local Government (`/settings/localgov`)

| API | Method |
|-----|--------|
| `/api/local-governments` | GET / POST |
| `/api/local-governments/{id}` | GET / PUT / DELETE |
| `/api/local-governments/import-csv` | POST |
| `/api/local-governments/export-csv` | GET |

**Functionality:**
- Same CRUD + CSV import/export pattern as Tribunals.
- Stores local council / city hall contact information used for municipal notifications.

---

### 10.8 Deadlines (`/settings/deadlines`)

| API | Method |
|-----|--------|
| `/api/deadline-settings` | GET |
| `/api/deadline-settings/tenant` | GET / PUT |
| `/api/deadline-settings/preview` | GET |
| `/api/deadline-settings/is-working-day` | GET |
| `/api/deadline-settings/case/{caseId}/overrides` | GET / POST |
| `/api/deadline-settings/case/{caseId}/overrides/{overrideId}` | DELETE |
| `/api/cases/{caseId}/deadlines` | GET / POST |
| `/api/cases/{caseId}/deadlines/{id}` | PUT / DELETE |

**Functionality:**
- **Tenant deadline policy** — configure default deadline calculation rules (business-day vs calendar-day, standard offsets for each deadline type).
- **Preview calculator** — validate a computed deadline before persisting.
- **Working-day check** — endpoint to determine if a specific date is a business day.
- **Case-specific overrides** — attach override records to a case for judicial exceptions or bespoke schedules.
- **Case deadlines** — CRUD for individual deadline entries on a case.

---

### 10.9 Templates (`/settings/templates`) — DEEP DIVE

| API | Method |
|-----|--------|
| `/api/document-templates` | GET / POST |
| `/api/document-templates/{id}` | GET / PUT / DELETE |
| `/api/document-templates/import-word` | POST |
| `/api/document-templates/placeholders` | GET |
| `/api/document-templates/{id}/render` | POST |
| `/api/document-templates/{id}/render-pdf` | POST |
| `/api/document-templates/{id}/save-to-case` | POST |
| `/api/document-templates/render-html-to-pdf` | POST |
| `/api/document-templates/save-html-to-case` | POST |
| `/api/document-templates/incoming-reference/{type}` | GET / POST |
| `/api/document-templates/incoming-reference/{type}/file` | GET |
| `/api/document-templates/incoming-reference/{type}/annotations` | GET / POST |
| `/api/document-templates/incoming-reference/{type}/profile` | GET |
| `/api/document-templates/incoming-reference/{type}/analyse` | POST |
| `/api/mailmerge/templates` | GET |
| `/api/mailmerge/generate/{caseId}` | POST |
| `/api/mailmerge/generate-all/{caseId}` | POST |
| `/api/mailmerge/download` | GET |

The template page has **three tabs**: Required Templates, Custom Templates, and Incoming Documents.

#### Tab 1: Required (System) Templates

Pre-defined template records tied to insolvency workflow stages. Users cannot create or delete these; they can only author/edit the HTML body content.

Each system template:
- Has a `templateType` key (e.g. `creditorNotificationBpi`, `reportArt97`, `mandatoryReport`).
- Is mapped to a legal stage (e.g. "Notificare Creditori — Etapa Colectare Creanțe").
- Shows a status badge: "Content defined" (green) or "No content" (amber).

**Editing a system template** opens the full template editor (see below).

#### Tab 2: Custom Templates

User-created templates for any purpose. Full lifecycle:
- **Create** — form with name, description (optional), category (optional).
- **Edit** — opens the full template editor.
- **Delete** — with confirmation dialog.
- Shows badges for category, content status, active/inactive.

#### Tab 3: Incoming Documents — Document Parsing & Classification

This tab manages **reference PDFs** that the system uses to auto-recognize and classify documents uploaded into cases.

**Incoming document types** (each has its own card):
- `CourtOpeningDecision` — Hotărârea de deschidere
- `CreditorClaim` — Declarație de creanță
- `BpiNotification` — Notificare BPI
- (and additional types defined in `INCOMING_DOCUMENT_LABELS`)

**Per-type workflow:**
1. **Upload reference PDF** — drag-and-drop or click-to-upload. Only PDF accepted. Progress bar shown during upload.
2. **PDF Annotator** — after upload, `PdfAnnotatorModal` opens automatically. The annotator allows the admin to:
   - View the uploaded PDF inline.
   - Draw annotation regions on the PDF to highlight key fields.
   - Save annotations via `/api/document-templates/incoming-reference/{type}/annotations`.
3. **AI analysis** — `POST .../analyse` triggers AI processing of the annotated PDF. The system:
   - Reads the PDF content + annotations.
   - Generates an AI profile including: summary (in EN/RO/HU), confidence score, analysis date.
   - The profile is stored and used at runtime to auto-classify future uploads.
4. **Recognition status** — once configured, the card shows:
   - "Reference uploaded" badge (green).
   - Annotation count badge (blue, e.g. "4 fields annotated").
   - "AI profile" badge (purple) with confidence percentage.
5. **Replace reference** — re-upload a new PDF to update the recognition baseline.

**How classification works at runtime:**
- When a document is uploaded into a case, the system compares it against all configured incoming reference profiles.
- AI matches the document's layout/content to the closest reference profile and assigns the appropriate document type automatically.
- The `DocumentReviewPage` (`/documents/{id}/review`) shows the AI-extracted data and lets the user confirm or correct.

#### Template Editor (shared by System + Custom templates)

A full rich-text HTML editor built on **Tiptap** (ProseMirror-based) with:

**Toolbar features:**
- Undo / Redo
- Bold, Italic, Underline, Strikethrough, Inline Code, Superscript, Subscript, Highlight
- Font color picker
- Headings (H1, H2, H3)
- Bullet list, Numbered list
- Text alignment (left, center, right, justify)
- Blockquote, Horizontal rule
- Link insert/edit/remove
- Image insert (by URL)
- Table operations: insert 3×3, add/delete column, add/delete row, delete table, merge/split cells, toggle header row
- Clear formatting
- Electronic signature placeholder insertion (`{{ElectronicSignature}}`)

**Tiptap extensions loaded:**
StarterKit, Underline, TextAlign, Table, TableRow, TableCell, TableHeader, Highlight, Link, Superscript, Subscript, TextStyle, Color, Image, Placeholder.

**Dual editing mode:**
- **Rich-text view** — WYSIWYG editing via Tiptap.
- **HTML source view** — raw HTML textarea with code toggle. Handlebars `{{#each}}` / `{{#if}}` blocks are preserved in HTML view (ProseMirror strips them from rich-text).

**Placeholder sidebar:**
- API returns grouped placeholder fields via `/api/document-templates/placeholders`.
- Groups are organized by data source (Case, Debtor, Creditors, etc.).
- Three placeholder types detected from group names:
  - **Scalar** — `{{FieldName}}` — clicked to insert at cursor.
  - **Repeater** (`{{#each Collection}}`) — "Insert full table" button generates a complete `<table>` with `<thead>` and `{{#each}}` loop. Individual fields can also be inserted.
  - **Conditional** (`{{#if Key}}`) — wraps content in `{{#if key}}…{{/if}}` block.

**Word import:**
- "Import Word" button accepts `.docx` files.
- `POST /api/document-templates/import-word` converts Word to HTML and uses AI to detect and insert `{{Placeholder}}` tokens automatically.
- Success banner shows filename and detected placeholder count.

**Preview:**
- Toggle preview renders the current HTML in a styled container.

**Save:**
- Saves HTML body + metadata (name, description, category, active flag) via `PUT /api/document-templates/{id}`.

**Render & Generate:**
- `POST /api/document-templates/{id}/render` — merge template with case data, return rendered HTML.
- `POST /api/document-templates/{id}/render-pdf` — merge template and return PDF.
- `POST /api/document-templates/{id}/save-to-case` — render and attach output as a case document.
- Mail merge endpoints for batch generation across all templates for a case.

| | GlobalAdmin | TenantAdmin | Practitioner | Secretary |
|--|:-:|:-:|:-:|:-:|
| View templates | ✅ (`TemplateView`) | ✅ | ✅ | ✅ |
| Generate from templates | ✅ (`TemplateGenerate`) | ✅ | ✅ | ✅ |
| Manage templates (create/edit/delete/configure incoming) | ✅ (`TemplateManage`) | ✅ | ❌ | ❌ |

---

### 10.10 Workflow Stages (`/settings/workflow-stages`) — DEEP DIVE

| API | Method |
|-----|--------|
| `/api/workflow-stages` | GET (effective resolved list) |
| `/api/workflow-stages/global` | GET / POST |
| `/api/workflow-stages/{id}` | GET |
| `/api/workflow-stages/override` | POST |
| `/api/workflow-stages/override/{stageKey}` | DELETE |
| `/api/workflow-stages/global/{stageKey}` | DELETE |

#### Overview
The Workflow Stages page manages the **global blueprint** that governs how cases progress through legal procedures. It is the admin configuration layer for the workflow engine described in Case Detail > Workflow tab.

#### Stage List View

- Loads all **effective** stages (tenant override → global fallback) alongside all document templates.
- Each stage renders as a `StageCard` showing:
  - Sort order number
  - Stage name and `stageKey` (machine identifier, e.g. `preliminary_table`)
  - **Global** badge (blue globe) or **Tenant Override** badge (blue building)
  - Template count badge (e.g. "3 templates")
  - Inactive badge if disabled
  - Description (truncated)
- **"New Stage"** button opens a creation form to define a new global stage (key, name, description, sort order).
- Footer summary: total stages, override count, inactive count.

#### Stage Detail Editor

Clicking a stage opens a full-page editor with these sections:

**1. Basic fields:**
- **Stage Name** — human-readable display name
- **Sort Order** — integer controlling stage sequence (lower = earlier)
- **Applicable Procedure Types** — toggle chips for each procedure type (FalimentSimplificat, Faliment, Insolventa, Reorganizare, ConcordatPreventiv, MandatAdHoc, Other). Stage only appears in cases of selected types. Empty = all types.
- **Description** — longer explanation shown in UI tooltip
- **Active** toggle — inactive stages are excluded from new case initialization

**2. Linked Templates:**
- List of document templates associated with this stage.
- Each link row shows: template name, "Is Required" checkbox, sort order field, remove button.
- "Add Template" button opens a picker showing unlinked templates filtered from the template library.
- Links are saved as `UpsertStageTemplateItem` records with `documentTemplateId`, `isRequired`, `sortOrder`, `notes`.

**3. Advanced Configuration (collapsible):**
Configures the validation gates and output prompts using JSON fields:

| Config Field | Purpose | UI Control |
|-------------|---------|------------|
| **Required Fields** (`requiredFieldsJson`) | Case entity properties that must be non-null before stage completion. | Checkbox grid with 18 options: CaseNumber, DebtorName, CourtName, CourtSection, JudgeSyndic, ProcedureType, LawReference, NoticeDate, OpeningDate, ClaimsDeadline, ContestationsDeadline, BpiPublicationNo, OpeningDecisionNo, PractitionerName, PractitionerRole, DebtorCui, DebtorAddress, DebtorTradeRegisterNo. |
| **Required Party Roles** (`requiredPartyRolesJson`) | Party roles that must be present in the case's party list. | Checkbox grid: Debtor, InsolvencyPractitioner, SecuredCreditor, UnsecuredCreditor, BudgetaryCreditor, EmployeeCreditor. |
| **Required Document Types** (`requiredDocTypesJson`) | Document types that must exist on the case. | Checkbox grid: creditorNotificationBpi, reportArt97, mandatoryReport, preliminaryClaimsTable, creditorsMeetingMinutes, definitiveClaimsTable, finalReportArt167, creditorNotificationHtml. |
| **Validation Rules** (`validationRulesJson`) | Custom JSON validation rules (e.g. `{"minCreditors": 1}`). | Free-form JSON textarea with validation on blur. |
| **Output Document Types** (`outputDocTypesJson`) | Document types the system suggests generating when stage activates. Advisory only — not gating. | Free-form JSON textarea. |
| **Allowed Transitions** (`allowedTransitionsJson`) | Stage keys this stage can transition to (advisory, used for UI guidance). | Free-form JSON textarea. |

**4. Required Task Templates:**
- Task templates that must have `Status = Done` before the stage can be completed.
- Each row: title, deadline in days, category dropdown (Document / Email / Filing / Meeting / Call / Review / Payment / Report / Compliance).
- Add / remove rows.

**5. Default Task Templates (Output Tasks):**
- Task templates automatically surfaced when the stage becomes active.
- Same row format as required tasks.
- These are created as `CompanyTask` records either automatically or via "Add suggested tasks" in the case UI.

#### Save Operations
Two save buttons:
- **Save Global** — writes changes to the global definition (affects all tenants without overrides).
- **Save as Override** — writes changes as a tenant-specific override for the current tenant only.
- **Revert to Global** — deletes the tenant override, falling back to the global definition. Requires confirmation.

#### Procedure Types Supported
Stages can be scoped to any combination of:
- Insolvență Generală (Insolventa)
- Faliment (Faliment)
- Faliment Simplificat (FalimentSimplificat)
- Reorganizare Judiciară (Reorganizare)
- Concordat Preventiv (ConcordatPreventiv)
- Mandat Ad-Hoc (MandatAdHoc)

---

### 10.11 Scheduled Emails (`/settings/emails`)

| API | Method |
|-----|--------|
| `/api/settings/emails` | GET / POST |
| `/api/settings/emails/{id}` | DELETE |

**Functionality:**
- View scheduled/sent emails.
- Create new scheduled email entries.
- Delete email records.
- Background service (`EmailBackgroundService`) processes the `ScheduledEmail` queue every 60 seconds, with up to 3 retries and exponential backoff, in batches of 20.

---

### 10.12 Error Logs (`/settings/errors`)

| API | Method |
|-----|--------|
| `/api/errorlogs` | GET |
| `/api/errorlogs/{id}/resolve` | PUT |
| `/api/settings/errors` | GET |
| `/api/settings/errors/{id}/resolve` | PUT |
| `/api/settings/errors/client` | POST |

**Functionality:**
- Lists error records with stack trace, request path, user ID, timestamp.
- **Resolve** action — mark an error as addressed.
- **Client error reporting** — frontend can POST browser/runtime exceptions to the backend for centralized tracking.
- `GlobalExceptionHandler` middleware catches all unhandled backend exceptions automatically.

| | GlobalAdmin | TenantAdmin | Practitioner | Secretary |
|--|:-:|:-:|:-:|:-:|
| View errors | ✅ (`ErrorLogView`) | ✅ | ✅ | ❌ |
| Resolve errors | ✅ (`ErrorLogResolve`) | ✅ | ❌ | ❌ |

---

### 10.13 Permissions (`/settings/permissions`)

**Functionality:**
- Read-only reference view of the role → permission mapping.
- Shows which permissions each role has for transparency.

---

### 10.14 Demo Reset (`/settings/demo`) — GlobalAdmin only

| API | Method |
|-----|--------|
| `/api/settings/demo/reset` | POST |

**Functionality:**
- Resets the environment to demo state (seed data restoration).
- Only available to GlobalAdmin role.

---

### 10.15 AI Configuration (`/settings/ai-config`) — GlobalAdmin only

| API | Method |
|-----|--------|
| `/api/settings/ai-config` | GET / PUT |

**Functionality:**
- Configure global AI provider settings (OpenAI API key, model selection, feature toggles).
- Only accessible to GlobalAdmin.

---

### 10.16 Tenant AI Config (`/settings/tenant-ai`) — GlobalAdmin only

| API | Method |
|-----|--------|
| `/api/settings/tenant-ai-config` | GET |
| `/api/settings/tenant-ai-config/{tenantId}` | GET / PUT |

**Functionality:**
- Per-tenant AI configuration: enable/disable AI features, set per-tenant API key.
- GlobalAdmin can view and configure settings for any tenant.

---

### 10.17 My AI Settings (`/settings/my-ai`) — TenantAdmin only

| API | Method |
|-----|--------|
| `/api/settings/tenant-ai-config/own-key` | PUT |

**Functionality:**
- TenantAdmin self-service: set their own tenant's AI API key without GlobalAdmin involvement.

| | GlobalAdmin | TenantAdmin | Practitioner | Secretary |
|--|:-:|:-:|:-:|:-:|
| View AI config | ✅ | ✅ (`TenantAiConfigView`) | ✅ (`TenantAiConfigView`) | ✅ (`TenantAiConfigView`) |
| Edit global AI config | ✅ (`TenantAiConfigEdit`) | ❌ | ❌ | ❌ |
| Edit tenant AI key | ✅ | ✅ (`TenantAiKeyEdit`) | ❌ | ❌ |

---

## 11. Document Upload & Review Flow

| URL | Type |
|-----|------|
| `/documents/{id}/review` | UI |
| `/api/documents/upload` | API |
| `/api/documents/upload/{id}` | API |
| `/api/documents/upload/{id}/confirm` | API |

### Functionality — Document Parsing & Classification (DEEP DIVE)

This is the two-step upload pipeline for intelligent document ingestion:

**Step 1: Upload**
- `POST /api/documents/upload` accepts a file and runs AI extraction.
- The backend uses either OpenAI GPT-4o or configured AI provider to:
  - Classify the document type (mapping to known incoming reference profiles if configured).
  - Extract structured data: case number, debtor name, debtor CUI, court name, court section, judge syndic, procedure type, opening date, next hearing date, claims deadline, contestations deadline.
  - Extract parties: list of `ExtractedParty` objects with name and role.
  - Determine `recommendedAction`: either `"newCase"` (create a new case from this document) or `"filing"` (attach to existing case).
  - Attempt to match against existing cases (`matchedCaseId`) and companies (`matchedCompanyId`).

**Step 2: Review (`/documents/{id}/review`)**
The `DocumentReviewPage` presents the extracted data for human verification:
- All extracted fields are shown in editable form inputs pre-populated with AI values.
- **Procedure type** dropdown with all 7 procedure types.
- **Tribunal matcher** — auto-matches extracted court name to tribunal records; user can override via dropdown or create new tribunal inline.
- **Debtor company matcher** — searches company database for debtor name matches; user can select existing company or create new inline.
- **Party list editor** — shows extracted parties with role dropdowns (Debtor, InsolvencyPractitioner, Court, SecuredCreditor, UnsecuredCreditor, BudgetaryCreditor, EmployeeCreditor, JudgeSyndic, CourtExpert, CreditorsCommittee, SpecialAdministrator, Guarantor, ThirdParty); user can add/remove parties, change roles.
- **Action choice**: "Create New Case" or "File into Existing Case" with case dropdown.
- **Confirm** — `POST /api/documents/upload/{id}/confirm` creates/updates the case with reviewed data and attaches the document.

---

## Cross-Cutting: Background Services

| Service | Purpose |
|---------|---------|
| `EmailBackgroundService` | Processes scheduled email queue every 60s, batch of 20, 3 retries with backoff. |
| `DeadlineReminderService` | Sends reminder notifications for approaching deadlines. |
| `TemplateEnforcementService` | Ensures required templates are configured for active workflow stages. |

---

## Cross-Cutting: Internationalization

- **3 languages**: English (`en.ts`), Romanian (`ro.ts`), Hungarian (`hu.ts`).
- `LanguageProvider` context with `useTranslation()` hook.
- Language choice persisted to `localStorage`.
- All UI labels, dropdown options, error messages, form placeholders, and table headers are translated.
- Type-safe translation keys via `types.ts`.

---

## Complete Permission Matrix

| Permission | Code | GlobalAdmin | TenantAdmin | Practitioner | Secretary |
|-----------|------|:-:|:-:|:-:|:-:|
| CaseView | 100 | ✅ | ✅ | ✅ | ✅ |
| CaseCreate | 101 | ✅ | ✅ | ✅ | ✅ |
| CaseEdit | 102 | ✅ | ✅ | ✅ | ✅ |
| CaseDelete | 103 | ✅ | ✅ | ❌ | ❌ |
| CaseExport | 104 | ✅ | ✅ | ✅ | ✅ |
| CaseClose | 105 | ✅ | ✅ | ✅ | ❌ |
| CaseReopen | 106 | ✅ | ✅ | ❌ | ❌ |
| PartyView | 200 | ✅ | ✅ | ✅ | ✅ |
| PartyCreate | 201 | ✅ | ✅ | ✅ | ✅ |
| PartyEdit | 202 | ✅ | ✅ | ✅ | ✅ |
| PartyDelete | 203 | ✅ | ✅ | ✅ | ❌ |
| PhaseView | 300 | ✅ | ✅ | ✅ | ✅ |
| PhaseEdit | 301 | ✅ | ✅ | ✅ | ❌ |
| PhaseInitialize | 302 | ✅ | ✅ | ✅ | ❌ |
| PhaseAdvance | 303 | ✅ | ✅ | ✅ | ❌ |
| PhaseDeadlineOverride | 304 | ✅ | ✅ | ❌ | ❌ |
| StageView | 350 | ✅ | ✅ | ✅ | ✅ |
| StageAdvance | 351 | ✅ | ✅ | ✅ | ❌ |
| DocumentView | 400 | ✅ | ✅ | ✅ | ✅ |
| DocumentUpload | 401 | ✅ | ✅ | ✅ | ✅ |
| DocumentEdit | 402 | ✅ | ✅ | ✅ | ❌ |
| DocumentDelete | 403 | ✅ | ✅ | ✅ | ❌ |
| DocumentDownload | 404 | ✅ | ✅ | ✅ | ✅ |
| SigningKeyManage | 450 | ✅ | ✅ | ✅ | ✅ |
| DocumentSign | 451 | ✅ | ✅ | ✅ | ✅ |
| SignatureVerify | 452 | ✅ | ✅ | ✅ | ✅ |
| TaskView | 500 | ✅ | ✅ | ✅ | ✅ |
| TaskCreate | 501 | ✅ | ✅ | ✅ | ✅ |
| TaskEdit | 502 | ✅ | ✅ | ✅ | ✅ |
| TaskDelete | 503 | ✅ | ✅ | ✅ | ❌ |
| CompanyView | 600 | ✅ | ✅ | ✅ | ✅ |
| CompanyCreate | 601 | ✅ | ✅ | ✅ | ✅ |
| CompanyEdit | 602 | ✅ | ✅ | ✅ | ❌ |
| CompanyDelete | 603 | ✅ | ✅ | ❌ | ❌ |
| MeetingView | 700 | ✅ | ✅ | ✅ | ✅ |
| MeetingCreate | 701 | ✅ | ✅ | ✅ | ❌ |
| TemplateView | 800 | ✅ | ✅ | ✅ | ✅ |
| TemplateGenerate | 801 | ✅ | ✅ | ✅ | ✅ |
| TemplateManage | 802 | ✅ | ✅ | ❌ | ❌ |
| SummaryView | 850 | ✅ | ✅ | ✅ | ✅ |
| SummaryGenerate | 851 | ✅ | ✅ | ✅ | ❌ |
| AiChatUse | 852 | ✅ | ✅ | ✅ | ✅ |
| TenantAiConfigView | 860 | ✅ | ✅ | ✅ | ✅ |
| TenantAiConfigEdit | 861 | ✅ | ❌ | ❌ | ❌ |
| SettingsView | 900 | ✅ | ✅ | ✅ | ✅ |
| SettingsEdit | 901 | ✅ | ✅ | ❌ | ❌ |
| SystemConfigView | 910 | ✅ | ❌ | ❌ | ❌ |
| SystemConfigEdit | 911 | ✅ | ❌ | ❌ | ❌ |
| DemoReset | 912 | ✅ | ❌ | ❌ | ❌ |
| UserView | 950 | ✅ | ✅ | ❌ | ❌ |
| UserCreate | 951 | ✅ | ✅ | ❌ | ❌ |
| UserEdit | 952 | ✅ | ✅ | ❌ | ❌ |
| UserDeactivate | 953 | ✅ | ✅ | ❌ | ❌ |
| UserInvite | 954 | ✅ | ✅ | ❌ | ❌ |
| EmailView | 970 | ✅ | ✅ | ✅ | ✅ |
| EmailCreate | 971 | ✅ | ✅ | ✅ | ✅ |
| EmailDelete | 972 | ✅ | ✅ | ❌ | ❌ |
| ErrorLogView | 980 | ✅ | ✅ | ✅ | ❌ |
| ErrorLogResolve | 981 | ✅ | ✅ | ❌ | ❌ |
| AuditLogView | 990 | ✅ | ✅ | ❌ | ❌ |
| DashboardView | 995 | ✅ | ✅ | ✅ | ✅ |
| AssetView | 1000 | ✅ | ✅ | ✅ | ✅ |
| AssetCreate | 1001 | ✅ | ✅ | ✅ | ✅ |
| AssetEdit | 1002 | ✅ | ✅ | ✅ | ✅ |
| AssetDelete | 1003 | ✅ | ✅ | ✅ | ❌ |
| TenantAiKeyEdit | 1010 | ✅ | ✅ | ❌ | ❌ |
