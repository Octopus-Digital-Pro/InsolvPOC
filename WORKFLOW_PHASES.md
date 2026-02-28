# Insolvex — Workflow Phases & Configuration Reference

> **Legea nr. 85/2014** privind procedurile de prevenire a insolvenței și de insolvență  
> Last updated: 2026-02-28

---

## 1. Overview

Insolvex uses a **configurable, procedure-type-aware workflow engine** to guide practitioners through every mandatory step of a Romanian insolvency procedure.

The system has three layers:

```
WorkflowStageDefinition   ← global blueprint (seeded, tenant-overridable)
        │
        ▼
CaseWorkflowStage         ← per-case instance (created when first accessed)
        │
        ▼
Validation engine         ← checks RequiredFields / Docs / Roles / Tasks before allowing "Complete"
```

When a case's workflow is viewed for the first time, the engine:
1. Loads all **active global** stage definitions + any **tenant overrides** for the same `StageKey`.
2. Picks the tenant-specific version if one exists, otherwise falls back to the global definition.
3. **Filters** the resolved set to only include stages whose `ApplicableProcedureTypes` contains the case's `ProcedureType`.
4. Creates one `CaseWorkflowStage` row per filtered stage, ordered by `SortOrder`.
5. Auto-starts the first stage (`Status = InProgress`).

---

## 2. Stage Definition Fields

| Field | Type | Purpose |
|-------|------|---------|
| `StageKey` | `string` | Stable machine key, e.g. `"preliminary_table"`. Shared between global and tenant override. |
| `Name` | `string` | Human-readable display name. |
| `Description` | `string?` | Longer explanation shown in the UI tooltip. |
| `SortOrder` | `int` | Controls ordered display. Lower = earlier. |
| `ApplicableProcedureTypes` | `string?` | Comma-separated list of `ProcedureType` enum values. `null`/empty = applies to all types. |
| `IsActive` | `bool` | Inactive stages are excluded from new-case initialization. |
| `RequiredFieldsJson` | `JSON array` | **Gate:** Case entity property names that must be non-null/non-empty before the stage can be marked Complete. |
| `RequiredPartyRolesJson` | `JSON array` | **Gate:** Party role strings (e.g. `"Debtor"`, `"InsolvencyPractitioner"`) that must be present in the case's parties. |
| `RequiredDocTypesJson` | `JSON array` | **Gate:** `InsolvencyDocument.DocType` values that must exist on the case. |
| `RequiredTaskTemplatesJson` | `JSON array` | **Gate:** Task titles that must have `Status = Done` on the case. |
| `OutputDocTypesJson` | `JSON array` | **Prompt:** Document types the system suggests generating when entering this stage. |
| `OutputTasksJson` | `JSON array` | **Prompt:** Default tasks automatically surfaced (and optionally auto-created) when entering this stage. |
| `AllowedTransitionsJson` | `JSON array` | **Advisory:** Stage keys that this stage can logically transition to (UI guidance only). |

---

## 3. RequiredDocTypesJson vs. OutputDocTypesJson — Key Distinction

These are the two most commonly confused configuration fields.

### `RequiredDocTypesJson` — **Prerequisite (gate)**

Defines which document types **must already exist** in the case before a stage can be marked **Complete**.

- The validation engine queries `InsolvencyDocument.DocType` for the case.
- If any required doc type is missing, `CanComplete = false` and the stage shows a blocker.
- Think of this as: *"You cannot leave this stage without having produced this document."*

**Example:** Stage `preliminary_table` has  
`RequiredDocTypesJson = ["TabelPreliminar"]`  
→ The practitioner cannot complete this stage until a document with `DocType = "TabelPreliminar"` is attached to the case.

---

### `OutputDocTypesJson` — **Suggested outputs (prompt)**

Defines which document types the system **should offer to generate** when a stage becomes active or is being worked on.

- These are **not checked** by the validation engine — they are prompts, not gates.
- The UI uses this list to show a "Generate document" chip/button for each type listed.
- Think of this as: *"When you enter this stage, these documents are the expected outputs — click to generate them."*

**Example:** Stage `intake` has  
`OutputDocTypesJson = ["NotificareCreditori", "NotificareDebitor", "NotificareBPI", "NotificareORC", "NotificareANAF"]`  
→ As soon as the case enters the Intake stage, the system offers one-click generation for all 5 notification documents. The stage does **not** require them to exist before completing (that happens in the next stage's `RequiredDocTypesJson`).

---

### Summary Table

| | `RequiredDocTypesJson` | `OutputDocTypesJson` |
|--|------------------------|----------------------|
| **Checked by validation?** | ✅ Yes — blocks Complete | ❌ No — advisory only |
| **Purpose** | Ensure doc exists before leaving stage | Prompt user to generate expected docs |
| **Trigger** | Evaluated when clicking "Complete Stage" | Shown when stage is active |
| **User action** | Must upload/generate the doc | Optional — can ignore |

---

## 4. OutputTasksJson — Default Task Templates

Each stage can define a list of **default tasks** that should be created when the stage becomes active.

### Structure (JSON array of task objects)

```json
[
  {
    "title": "Publică notificarea în BPI",
    "description": "Publicarea în Buletinul Procedurilor de Insolvență (BPI)",
    "deadlineDays": 5,
    "category": "Filing"
  }
]
```

| Field | Type | Description |
|-------|------|-------------|
| `title` | `string` | Task title (also used as key for `RequiredTaskTemplatesJson` matching) |
| `description` | `string?` | Longer description of what needs to be done |
| `deadlineDays` | `int?` | Days from stage start to set the task deadline |
| `category` | `string?` | One of: `Document`, `Email`, `Filing`, `Meeting`, `Call`, `Review`, `Payment`, `Report`, `Compliance` |

**Important:** `OutputTasksJson` is **template metadata** stored on the stage definition. The actual `CompanyTask` records are created either:
- **Automatically** by the workflow engine when the stage starts (if auto-task creation is enabled), or
- **Manually** by the practitioner clicking "Add suggested tasks" in the UI.

---

## 5. RequiredTaskTemplatesJson — Task Completion Gates

Defines task **titles** that must have `Status = Done` before the stage can be completed.

```json
["Depune raportul Art. 97 la judecătorul sindic", "Publică raportul Art. 97 în BPI"]
```

The validation engine compares these strings against `CompanyTask.Title` (case-insensitive) where `CaseId` matches and `Status = Done`.

---

## 6. Procedure Types & Their Stage Sets

### 6.1  `Insolventa` — Insolvență generală (Art. 66–69)

The observation-period procedure. The debtor continues operations under practitioner supervision while the court decides whether reorganization or bankruptcy follows.

| # | StageKey | Name | SortOrder | Art. |
|---|----------|------|-----------|------|
| 1 | `intake` | Deschidere procedură | 10 | 66–70 |
| 2 | `observation` | Perioadă de observație | 20 | 67–69 |
| 3 | `claims_collection` | Colectare declarații de creanță | 30 | 104–110 |
| 4 | `causes_report` | Raport cauze insolvență (Art. 97 / 40 zile) | 40 | 97 |
| 5 | `preliminary_table` | Tabel preliminar de creanțe | 50 | 111–113 |
| 6 | `contestations` | Soluționare contestații tabel preliminar | 60 | 113–114 |
| 7 | `creditors_meeting` | Adunarea generală a creditorii (AGC) | 70 | 78–88 |
| 8 | `definitive_table` | Tabel definitiv de creanțe | 80 | 122 |
| 9 | `final_report` | Raport final și închidere procedură | 200 | 167 |

> **Note:** In practice, after `definitive_table`, if no reorganization plan is proposed, the court converts to `Faliment` and the ProcedureType on the case is updated — the system then re-initializes the workflow with the full bankruptcy stages.

---

### 6.2  `Faliment` — Faliment (Art. 143–167)

Full bankruptcy liquidation procedure. Includes all observation/claims stages plus the full asset lifecycle.

| # | StageKey | Name | SortOrder | Art. |
|---|----------|------|-----------|------|
| 1 | `intake` | Deschidere procedură | 10 | 66–70 |
| 2 | `observation` | Perioadă de observație | 20 | 67–69 |
| 3 | `claims_collection` | Colectare declarații de creanță | 30 | 104–110 |
| 4 | `causes_report` | Raport cauze insolvență (Art. 97 / 40 zile) | 40 | 97 |
| 5 | `preliminary_table` | Tabel preliminar de creanțe | 50 | 111–113 |
| 6 | `contestations` | Soluționare contestații | 60 | 113–114 |
| 7 | `creditors_meeting` | Adunarea generală a creditorii (AGC) | 70 | 78–88 |
| 8 | `definitive_table` | Tabel definitiv de creanțe | 80 | 122 |
| 9 | `asset_inventory` | Inventarierea averii debitorului | 90 | 150 |
| 10 | `asset_valuation` | Evaluarea bunurilor | 100 | 154 |
| 11 | `asset_liquidation` | Lichidarea activelor | 110 | 154–157 |
| 12 | `distribution` | Distribuirea sumelor către creditori | 120 | 159–163 |
| 13 | `final_report` | Raport final și închidere procedură | 200 | 167 |

---

### 6.3  `FalimentSimplificat` — Faliment Simplificat (Art. 38)

Direct bankruptcy without an observation period. Applied when: debtor has no assets, has disappeared, no accounting records found, or ONRC signals it.

| # | StageKey | Name | SortOrder | Difference from Faliment |
|---|----------|------|-----------|--------------------------|
| 1 | `intake` | Deschidere procedură | 10 | — |
| ~~2~~ | ~~`observation`~~ | ~~Perioadă de observație~~ | — | **SKIPPED** — Art. 38 goes directly to liquidation |
| 2 | `claims_collection` | Colectare declarații de creanță | 30 | — |
| ~~4~~ | ~~`causes_report`~~ | ~~Raport Art. 97~~ | — | **SKIPPED** — Art. 97 report not required in simplified path |
| 3 | `preliminary_table` | Tabel preliminar de creanțe | 50 | — |
| 4 | `contestations` | Soluționare contestații | 60 | — |
| 5 | `creditors_meeting` | Adunarea generală a creditorii (AGC) | 70 | — |
| 6 | `definitive_table` | Tabel definitiv de creanțe | 80 | — |
| 7 | `asset_inventory` | Inventarierea averii | 90 | — |
| 8 | `asset_valuation` | Evaluarea bunurilor | 100 | — |
| 9 | `asset_liquidation` | Lichidarea activelor | 110 | — |
| 10 | `distribution` | Distribuirea sumelor | 120 | — |
| 11 | `final_report` | Raport final și închidere | 200 | — |

---

### 6.4  `Reorganizare` — Reorganizare judiciară (Art. 133–142)

The most complex procedure. Includes all common stages **plus** a full 6-stage reorganization plan cycle.

| # | StageKey | Name | SortOrder | Art. |
|---|----------|------|-----------|------|
| 1 | `intake` | Deschidere procedură | 10 | 66–70 |
| 2 | `observation` | Perioadă de observație | 20 | 67–69 |
| 3 | `claims_collection` | Colectare declarații de creanță | 30 | 104–110 |
| 4 | `causes_report` | Raport cauze insolvență (Art. 97) | 40 | 97 |
| 5 | `preliminary_table` | Tabel preliminar de creanțe | 50 | 111–113 |
| 6 | `contestations` | Soluționare contestații | 60 | 113–114 |
| 7 | `creditors_meeting` | Adunarea generală a creditorii (AGC) | 70 | 78–88 |
| 8 | `definitive_table` | Tabel definitiv de creanțe | 80 | 122 |
| 9 | `plan_elaboration` | Elaborare plan de reorganizare | 90 | 133–135 |
| 10 | `plan_admission` | Admitere plan de tribunal | 100 | 136–137 |
| 11 | `plan_vote` | Votul creditorii asupra planului | 110 | 138 |
| 12 | `plan_confirmation` | Confirmare plan de instanță | 120 | 139 |
| 13 | `plan_implementation` | Implementare plan de reorganizare | 130 | 140 |
| 14 | `plan_monitoring` | Monitorizare implementare plan (trimestrial) | 140 | 140–142 |
| 15 | `final_report` | Finalizare plan / Raport final și închidere | 200 | 167 |

#### Plan adoption threshold (Art. 138)
A class votes in favour if: *creditors holding more than 50% of the claims in the class vote in favour.*  
The plan is adopted if: *at least one consenting class AND overall majority of classes vote in favour.*

If the plan fails or is not executed: court converts to `Faliment` (Art. 143).

---

### 6.5  `ConcordatPreventiv` — Concordat Preventiv (Art. 31–50)

A pre-insolvency instrument. The debtor is in **financial difficulty but not yet insolvent**. The procedure is faster and less formal than full insolvency.

| # | StageKey | Name | SortOrder | Art. |
|---|----------|------|-----------|------|
| 1 | `concordat_request` | Cerere deschidere concordat preventiv | 10 | 17 |
| 2 | `concordat_negotiation` | Notificarea creditorii și negocieri | 30 | 21–23 |
| 3 | `concordat_drafting` | Elaborare act de concordat | 40 | 25 |
| 4 | `concordat_vote` | Votul creditorii (min. 75% din creanțe) | 50 | 26 |
| 5 | `concordat_homologation` | Omologarea concordatuli de instanță | 60 | 28 |
| 6 | `concordat_implementation` | Executarea concordatuli (max. 24 luni) | 70 | 31–35 |
| 7 | `concordat_completion` | Finalizare / Reziliere concordat | 80 | 36 |

#### Key legal thresholds
- Voting threshold: **75%** of total claim value to adopt the concordat.
- Execution period: maximum **24 months** from homologation.
- Effect: binding on all creditors (including dissenters) once homologated.

---

### 6.6  `MandatAdHoc` — Mandat Ad-Hoc (Art. 21–30)

A confidential, informal negotiation procedure. No BPI publication. No publicly registered effect. Used when the debtor wants to negotiate with key creditors before any formal proceedings.

| # | StageKey | Name | SortOrder | Art. |
|---|----------|------|-----------|------|
| 1 | `mandate_request` | Cerere numire mandatar ad-hoc | 10 | 7 |
| 2 | `mandate_appointment` | Numire mandatar de tribunal | 20 | 8 |
| 3 | `mandate_negotiation` | Negocieri confidențiale cu creditorii | 30 | 10–12 |
| 4 | `mandate_agreement` | Acord negociat cu creditorii | 40 | 13 |
| 5 | `mandate_termination` | Încetare mandat | 50 | 14 |

#### Key characteristics
- **Confidential** — not published in BPI.
- Agreement binds **only the signatories** (not all creditors).
- No court homologation required.
- If negotiation fails, debtor can immediately open concordat or insolvency.

---

## 7. Tenant Overrides

A tenant can customise any global stage without affecting other tenants:

1. Navigate to **Settings → Workflow Stages**.
2. Click **"Override"** on a global stage.
3. Edit any field (name, description, required fields, output tasks, etc.).
4. Save as a **tenant override** (creates a new `WorkflowStageDefinition` with `TenantId = <current tenant>`).

**Resolution rule:** When initialising a case's workflow, the system picks the **tenant-specific version** of a stage if one exists for the current tenant's `TenantId`; otherwise it falls back to the **global (TenantId = null)** definition.

To revert to the global default, delete the tenant override from the Workflow Stages admin page.

---

## 8. Stage Progression Rules

| Rule | Detail |
|------|--------|
| **Linear ordering** | A stage at `SortOrder = N` cannot be started until all stages with `SortOrder < N` are `Completed` or `Skipped`. |
| **Skip** | Any stage can be skipped with a mandatory reason note. Skipped stages are treated as done for progression purposes. |
| **Reopen** | A completed or skipped stage can be reopened (reverts to `InProgress`). |
| **Complete gate** | Clicking "Complete" evaluates all four requirement arrays. All must pass for `CanComplete = true`. |
| **Force-close** | An admin can close a case with pending stages by providing a mandatory explanation (logged to audit trail). |

---

## 9. Document Type String Convention

`RequiredDocTypesJson` and `OutputDocTypesJson` reference documents by their `DocType` string stored on `InsolvencyDocument`. The convention used throughout the seeded stages is **PascalCase Romanian identifiers**:

| DocType string | Description |
|----------------|-------------|
| `NotificareCreditori` | Notificare creditori deschidere procedură |
| `NotificareDebitor` | Notificare debitor deschidere procedură |
| `NotificareBPI` | Publicare BPI deschidere procedură |
| `NotificareORC` | Notificare ONRC / ORC |
| `NotificareANAF` | Notificare administrație fiscală |
| `RaportCauze` | Raport cauze insolvență (Art. 97) |
| `TabelPreliminar` | Tabel preliminar de creanțe |
| `TabelPreliminarRectificat` | Tabel preliminar rectificat după contestații |
| `ConvocareAGC` | Convocare adunare generală creditori |
| `ProcesVerbalAGC` | Proces-verbal adunare generală creditori |
| `TabelDefinitiv` | Tabel definitiv de creanțe |
| `PlanReorganizare` | Plan de reorganizare |
| `ProcesVerbalVotPlan` | Proces-verbal vot plan de reorganizare |
| `RaportImplementarePlan` | Raport trimestrial implementare plan |
| `RaportInventar` | Raport inventariere avere debitor |
| `RaportEvaluare` | Raport evaluare bunuri (ANEVAR) |
| `RaportLichidare` | Raport periodic lichidare active |
| `AnuntLicitatie` | Anunț publicitate licitație |
| `PlanDistributie` | Plan de distribuire fonduri |
| `RaportFinal` | Raport final (Art. 167) |
| `CerereConcordat` | Cerere deschidere concordat preventiv |
| `ActConcordat` | Actul de concordat preventiv |
| `ProcesVerbalVotConcordat` | Proces-verbal vot creditori concordat |
| `RaportExecutareConcordat` | Raport trimestrial executare concordat |
| `AcordAdHoc` | Acord negociat mandat ad-hoc |

When uploading a document to a case, set `DocType` to one of these strings to have it recognised by the validation engine.

---

## 10. Adding a New Stage

To add a custom stage globally (system admin) or for a single tenant:

1. Use the `POST /api/workflow-stages` endpoint (or the UI Workflow Stages page).
2. Set `StageKey` to a new unique string (e.g. `"onrc_notification_check"`).
3. Set `ApplicableProcedureTypes` to the relevant procedure types.
4. Set `SortOrder` to insert it between existing stages (use a value between existing sort orders, e.g. `15` to insert between `intake` (10) and `observation` (20)).
5. Configure `RequiredDocTypesJson`, `OutputDocTypesJson`, and `OutputTasksJson` as needed.
6. For existing cases, stages are **not retroactively added** — they apply only to cases whose workflow has not yet been initialized.

---

## 11. Legal References (Legea nr. 85/2014)

| Procedure | Chapter | Key Articles |
|-----------|---------|-------------|
| Deschidere procedură generală | II, §1 | Art. 66–70 |
| Perioadă de observație | II, §2 | Art. 67–69 |
| Colectare creanțe | III | Art. 104–110 |
| Tabel preliminar | III | Art. 111–113 |
| Contestații | III | Art. 113–114 |
| Adunarea creditorii | I, §4 | Art. 78–88 |
| Tabel definitiv | III | Art. 122 |
| Raport cauze (Art. 97) | II | Art. 97 |
| Plan de reorganizare | IV | Art. 133–142 |
| Deschidere faliment | V | Art. 143–148 |
| Inventariere avere | V | Art. 150 |
| Valorificare active | V | Art. 154–157 |
| Distribuire | V | Art. 159–163 |
| Raport final | V | Art. 167 |
| Mandat ad-hoc | I (Titlul II) | Art. 21–30 |
| Concordat preventiv | I (Titlul II) | Art. 31–50 |
