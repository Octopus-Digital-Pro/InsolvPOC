🧩 PHASE 0 — NOTICE UPLOAD → CASE CREATION
Input

Document uploaded:

“Notificare privind deschiderea procedurii falimentului” 

1.Notificare creditori deschide…

System actions
ACTION: Classify Document

Type = Opening notice

Extract:

Case number

Tribunal

Judge

Notice date (04.04.2023)

Claim submission deadline (19.05.2023)

Preliminary table date (29.05.2023)

Definitive table date (23.06.2023)

First creditors meeting date (30.05.2023)

ACTION: Create Case

CaseCreationDate = NoticeDate

ProcedureType = simplified bankruptcy

Tribunal = Tribunal Covasna

Judge = Munteanu Cosmin

ACTION: Create Deadlines

From notice:

Claim submission deadline → TASK (Critical)

Preliminary table deadline → TASK (Critical)

Definitive table deadline → TASK (Critical)

First creditors meeting → EVENT + TASK

ACTION: Create Parties

Debtor

Lichidator

Tribunal

Creditors = “Unknown” placeholder

OUTPUT

Case exists with scheduled statutory milestones.

Validation to move forward

Notice date confirmed

Tribunal selected

Procedure type selected

Core deadlines stored

🧾 PHASE 1 — CREDITOR NOTIFICATION & CLAIM COLLECTION
Goal

Collect claims and notify all creditors.

TASKS
Type	Description	To
DOCUMENT	Generate BPI notification	All known creditors
EMAIL	Send notification	Known creditors
EVENT	Publish in BPI	System log
TASK	Monitor claim deadline	Case owner
Data Collection

Claims received (document upload)

For each claim:

Creditor identity

Amount claimed

Nature of claim

Supporting documents

Validation Gate

Claim submission deadline passed OR manual override

All received claims reviewed

OUTPUT

Preliminary table of claims
(see uploaded Tabel preliminar) 

3.Tabel prel

📊 PHASE 2 — PRELIMINARY TABLE
TASKS
Type	Action
ACTION	Verify claims
ACTION	Accept/reject
DOCUMENT	Generate preliminary table
FILING	Submit to tribunal
EMAIL	Notify creditors of table
Data Collection

Accepted amount

% share

Claim nature (budgetary, secured, etc.)

OUTPUT

“Tabel preliminar al creanțelor” 

3.Tabel prel

Validation

All claims reviewed

Objection period recorded

👥 PHASE 3 — CREDITORS MEETING

Triggered automatically from notice.

From your uploaded minutes: 

4.proces verbal AGC confirmare …

ACTION: Call Meeting (Sidebar Function)

User selects:

Date

Agenda

System auto:

Generates convocation

Emails to creditors

Creates calendar event

Adds task to record minutes

Meeting Outcomes

Confirm liquidator

Fix remuneration

Decide committee formation

Validation

Minutes uploaded

Votes recorded

OUTPUT

Proces verbal AGC 

4.proces verbal AGC confirmare …

📋 PHASE 4 — DEFINITIVE TABLE

After objections period.

TASKS

Review objections (if any)

Update claim amounts

Generate definitive table

File to tribunal

Email creditors

OUTPUT

Tabel definitiv 

5.Tabel DEFINITIV

Validation

Objection window closed

All objections resolved

📈 PHASE 5 — 40 DAY REPORT (CAUSES REPORT)

Your document: 

2.Raport 40 zile_AM

This is statutory under Art. 97.

Data Collection Tasks

Financial statements

Asset list

Debt breakdown

Litigation search

Fraud review

Administrator interview

Each is a TASK with deadline.

DOCUMENT OUTPUT

Raport asupra cauzelor insolvenței 

2.Raport 40 zile_AM

Validation

Report generated

Filed to tribunal

Uploaded and locked

🏚 PHASE 6 — ASSET INVESTIGATION & LIQUIDATION

In your case:

No saleable assets 

7.Raport final_AM

TASKS

Inventory assets

Evaluate sale value

Sell assets (if any)

Recover receivables

File avoidance actions (if needed)

If NO assets:

Create statement: “No distributable assets”

If assets exist:

Create distribution schedule

OUTPUT

Distribution statement OR zero recovery statement

🧮 PHASE 7 — FINAL REPORT

Your document: 

7.Raport final_AM

TASKS

Prepare final accounting

Prepare liquidation balance sheet

Prepare final report

File to tribunal

Call final creditors meeting (if required)

OUTPUT

Raport final + request for closure 

7.Raport final_AM

Validation

All statutory reports submitted

No pending litigation

No open asset tasks

🔄 VARIATIONS
❓ What if there are NO creditors?

Possible if:

No claims submitted

System logic:

Create empty table

Skip meeting if legally allowed

Move directly to final report

Still must:

Generate preliminary and definitive tables (even if empty)

Respect deadlines

👤 What if there is ONE creditor? (Your case)

Meeting quorum = 100%

No committee formed 

4.proces verbal AGC confirmare …

Faster progression

System should:

Auto-detect single creditor

Adjust meeting logic

🔁 What if it's restructuring (not simplified bankruptcy)?

Then workflow changes:

Instead of:

Liquidation

Asset sale

Closure

You add:

Additional Phases

Observation period

Plan proposal

Plan voting

Plan confirmation

Plan monitoring (1–3 years)

Implementation reports

New outputs:

Reorganization plan

Voting report

Confirmation decision

Periodic compliance reports

Your system must branch by ProcedureType.

📧 BEST EMAIL ARCHITECTURE FOR CASE TRAIL

You absolutely want:

✅ Custom email address per case

Example:

467-119-2023@cases.yourdomain.ro
How it works:

Configure inbound email webhook

All emails to that address:

Auto-attach to case

Parse sender

Store attachments

Thread by message-id

Outbound:

All emails sent from:

467-119-2023@cases.yourdomain.ro

So replies come back into system.

Infrastructure Recommendation
Outbound:

Dedicated SMTP provider (SendGrid, Mailgun, Amazon SES)

Inbound:

Webhook route:
/api/email/inbound

Database structure:

Email table:

CaseId

From

To

Subject

Body

Attachments

InReplyTo

ThreadId

SentAt

Direction (Inbound/Outbound)

Then show:
📨 Full email thread timeline in case view.

🔐 EMAIL BEST PRACTICES

Store raw MIME for evidentiary purposes

Store delivery status

Log opens (optional)

Lock sent statutory emails from editing

Version mail-merged documents

🎯 IDEAL WORKFLOW ENGINE DESIGN

Each phase has:

Phase {
   requiredDocuments[]
   requiredTasks[]
   autoGeneratedTemplates[]
   validationRules[]
   allowedTransitions[]
}

Transition only allowed if:

Required documents exist

Required tasks completed

Required deadlines reached

🏁 HOW EACH WORKFLOW ENDS

Every workflow should end with:

Procedure Type	Final Output
Simplified Bankruptcy	Final Report + Closure Request
Reorganization	Plan Completion Report
No assets	Zero distribution statement
With assets	Distribution report

In your uploaded case:
Final output = Raport final + closure request 

7.Raport final_AM

**Data Structure 
1) Core principle: separate “facts” from “workflow”
Facts (data model)

Case, Parties, Claims, Assets, Documents, Tasks, Emails, Events.

Workflow (configuration)

Phase definitions (requirements + validations + outputs) should be config, not hard-coded:

required fields

required documents (by type)

required tasks (by template)

rules (date-based, state-based)

generated outputs (templates + filings)

That lets you support:

“simplified bankruptcy”

“general procedure”

“restructuring”

“no creditor claims”
…by switching workflow definitions, not rewriting code.

2) Data structure: what tables you actually want
A) Case (the “state container”)

Minimum:

CaseId, TenantId

ProcedureType (SimplifiedBankruptcy / Bankruptcy / Restructuring / Observation / etc.)

StageId (current phase)

NoticeDate (this is your case creation anchor)

CaseNumber, TribunalId, JudgeName (or party ref)

AssignedCaseOwnerUserId

Status (Active / Suspended / Closed)

Structured deadlines
Don’t store deadlines only as loose tasks. Store key statutory dates as fields too:

ClaimSubmissionDeadline

PreliminaryTableDeadline

DefinitiveTableDeadline

CreditorsMeetingDate

Report40DaysDue

FinalReportDue

But also keep the “source”:

DeadlineSource per field (NoticeExtracted / CompanyDefault / ManualOverride)

This gives you clean UX (“key dates” panel) and robust validation.

B) CaseFieldValue (optional but powerful)

If you anticipate a lot of procedure-specific fields, implement an “extension” table:

CaseFieldDefinition (key, label, type, rules, appliesToProcedureTypes)

CaseFieldValue (caseId, fieldKey, valueJson, source, updatedBy)

This is how you avoid adding 200 columns later.

C) PhaseInstance (track what actually happened)

You need phase history, not just current stage:

PhaseInstanceId, CaseId, PhaseId

StartedAt, CompletedAt

CompletedByUserId

CompletionReason (normal / skipped / merged / terminated)

This is important for auditability and for computing “phase-specific requirements”.

D) Parties (Debtor, Creditor, Tribunal, Practitioner, etc.)

Use a single Party table + roles.

Party

PartyId, TenantId

Name, Identifiers (CUI, J-number), Address, Email, Phone

PartyType (Company / Person / Institution)

CaseParty (join + role)

CasePartyId, CaseId, PartyId

Role (Debtor / Creditor / Tribunal / Judge / Liquidator / TaxAuthority / Bank / EmployeeRep)

IsPrimary (true for primary debtor, main tribunal)

PhaseScope (optional: which phases they’re involved in)

Creditor-specific fields
Don’t put creditor-only fields on Party. Put them on:

CreditorClaim (below)

E) Creditor claims (support “no creditors” cleanly)

CreditorClaim

ClaimId, CaseId, CreditorPartyId

DeclaredAmount, AdmittedAmount

Rank (Budgetary/Secured/Chirographary/etc.)

Status (Received / UnderReview / Admitted / Rejected / NeedsInfo)

SupportingDocumentIds[]

ReceivedAt

ReviewedBy, ReviewedAt

Then “Preliminary table” and “Definitive table” become report outputs over these rows.

No creditors scenario

Claims table is empty

Validation rule can allow phase completion if:

claim deadline passed AND zero claims received

You still generate “empty table” documents.

F) Assets and recoveries (bankruptcy path)

Asset

AssetId, CaseId

AssetType (Vehicle/RealEstate/Receivable/Inventory/Equipment/IP/etc.)

Description

EstimatedValue

EncumbranceDetails (secured creditor refs)

Status (Identified / Valued / ForSale / Sold / Unrecoverable)

SaleProceeds

Receivable
If receivables are a big deal, split them:

ReceivableId, CaseId, DebtorName, Amount, DueDate, RecoverabilityScore, Status

G) Documents (the spine of your system)

Document

DocumentId, CaseId

DocType + Confidence + IsKeyDocument

UploadedBy, UploadedAt

Summary (required, AI-generated + editable)

ExtractedDates[] (typed!)

ExtractedParties[]

ExtractedActions[] → becomes proposed tasks

Important: treat “extraction acceptance” as an explicit event:

DocumentExtractionReview (Accepted/Edited/Rejected, reviewer, timestamp)

This is your audit trail for “why did a deadline change?”

H) Tasks (Actions)

Task

TaskId, CaseId, PhaseInstanceId

TaskType (Action/Event/Email/Document/Filing)

Title, Description

AssigneeUserId (required)

Deadline (required)

Status (Open/InProgress/Done/Blocked/Cancelled/Overdue)

DependsOnTaskIds[]

RelatedDocumentId, RelatedPartyId, RelatedEmailId, RelatedEventId

For “never miss” items:

IsCritical = true

EscalationPolicyId

I) Emails and threading (case-wide evidence)

EmailMessage

EmailId, CaseId

Direction (Inbound/Outbound)

From, To, Cc, Bcc

Subject, BodyHtml, BodyText

SentAt, ReceivedAt

ProviderMessageId

ThreadKey (derived from Message-ID/In-Reply-To/References)

Attachments[] (link to Document or separate attachment table)

This supports a clean “Email Timeline” view.

3) Phase validation: how to make it robust
A) Validation should produce:

pass/fail

reasons (human readable)

fix actions (links to create missing tasks/docs)

Store evaluation results:
PhaseValidationResult

CaseId, PhaseInstanceId, ValidatedAt

IsValid

Errors[] (structured codes + messages + pointers)

This allows “why can’t I progress?” UX without guesswork.

B) Use 4 types of requirements per phase
1) Required fields

Example (Phase 0):

Case.NoticeDate

Case.TribunalId

Debtor Party exists

2) Required documents (by doc type)

Example:

Phase “Preliminary table” requires:

DocType = PreliminaryClaimsTable generated + filed

3) Required tasks completed

Example:

“Send notice to all known creditors” done

“Schedule meeting” done

4) Conditional rules (branch logic)

Example:

If Claims.Count == 0 and ClaimDeadlinePassed == true → allow

If procedure type = restructuring → require “Plan drafted” instead of “Asset inventory”

This is the key to handling your “what if no creditors / what if restructuring” questions.

C) Represent phase requirements in config (recommended)

Something like:

RequiredCaseFields[]

RequiredPartyRoles[]

RequiredDocTypes[]

RequiredTaskTemplates[]

Rules[] (small DSL or code-based predicates)

This keeps validation consistent and testable.

4) Handling “outputs per phase” cleanly

Instead of making phases “end with a document” rigidly, model phase outputs explicitly:

PhaseOutputDefinition

PhaseId

OutputType (DocumentGenerated / FilingSubmitted / MeetingHeld / RegisterPublished)

TemplateId (if doc)

DocType

RecipientRoles[] (for emails)

FilingTarget (Tribunal/BPI/etc.)

DueDateRule (notice-derived or offset)

Then the phase completion condition can be:

“All required outputs exist and are marked submitted/sent.”

5) My recommended minimum entity set (to ship v1 fast)

If you want to ship quickly but not paint yourself into a corner, I’d do:

Case

PhaseInstance

Party + CaseParty

Document (+ extraction review)

Task

CreditorClaim

Asset

EmailMessage

CalendarEvent

CompanySettings (deadline rules + workflow selection)

Then add “CaseFieldValue” later if you start seeing lots of bespoke fields.

6) Practical validation examples (based on your real docs)
“Move from Intake → Claims Collection”

Must have:

Notice doc uploaded & accepted

NoticeDate set

Tribunal + Case number set

ClaimSubmissionDeadline set (notice extracted or computed)

“Move from Claims Collection → Preliminary Table”

Either:

Now >= ClaimSubmissionDeadline AND all received claims reviewed
OR

manual override with reason

“Move to Meeting phase”

Must have:

Meeting event created OR explicitly skipped (if rules allow)

Invitations/notices sent (email proof OR print queue logged)

“Move to Definitive Table”

Must have:

objection window closed

objections resolved OR none received

definitive table generated and marked filed

7) Email trail: what I’d do (strong recommendation)
Best practice: one inbound/outbound address per case

{caseNumberOrCaseId}@cases.yourdomain.ro

Why it’s best:

Every reply auto-attaches to the case

No manual filing

Perfect audit trail

How:

Use an email provider with inbound routing (Mailgun/SendGrid/SES)

Inbound webhook → creates EmailMessage + Documents for attachments

Outbound uses the same provider SMTP/API

Store raw MIME + thread headers