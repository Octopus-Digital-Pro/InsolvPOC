# Plan: Per-Case Email System with SES Inbound + Responsive Settings Nav + Localization

## TL;DR
Add per-case email addresses (≤10 chars @insolvio.io), use SES for both outbound (already working) and inbound email reception, poll S3 for incoming emails, parse and link them to cases, extract attachments as case documents, create dashboard notifications, and allow creating response tasks with calendar integration. Add an Email Settings tab for CC preferences. Make the settings sidebar responsive (mobile burger nav takeover with back button). Localize all new UI strings in EN/RO/HU.

## Architecture Overview

**Inbound flow:** SES Receipt Rule → S3 bucket → Backend polling service → Parse email → Match to case → Create ScheduledEmail (Direction=Inbound) → Extract attachments → Create notification → Dashboard badge

**Outbound flow (enhanced):** Compose email → Set From: "{User Name} <case-address@insolvio.io>" → CC user's personal email (+ practitioner/admin per settings) → Send via SES SMTP (existing)

---

## Phase 1: Case Email Address Generation

### Step 1.1 — Add `CaseEmailAddress` to InsolvencyCase entity
- File: `Insolvio.Domain/Entities/InsolvencyCase.cs`
- Add `public string? CaseEmailAddress { get; set; }` property
- Add EF migration for the new column

### Step 1.2 — Email Address Generator Service
- New file: `Insolvio.Core/Services/CaseEmailAddressGenerator.cs`
- Interface: `Insolvio.Core/Abstractions/ICaseEmailAddressGenerator.cs`
- Algorithm: sanitize DebtorName (remove diacritics, special chars, common suffixes like "SRL"/"SA"/"LLC"), take first N chars, append hyphen + case number suffix, ensure total ≤ 10 chars before @
- Examples:
  - "ABC Solutions SRL" + case #42 → `abcsol-42@insolvio.io`
  - "XY Corp" + case #1234 → `xy-1234@insolvio.io`  
  - "Metropolis Trade" + case #7 → `metrop-7@insolvio.io`
- Must validate uniqueness against DB
- If collision, append incrementing letter (a,b,c)

### Step 1.3 — Auto-assign on case creation
- Modify `CaseService` (or wherever cases are created) to call generator
- Also add a backfill migration/endpoint for existing cases
- Display email address in CaseDetailView header area

---

## Phase 2: Notification System

### Step 2.1 — Notification Entity
- New file: `Insolvio.Domain/Entities/Notification.cs`
- Fields: `Id`, `TenantId`, `UserId` (recipient), `Title`, `Message`, `Category` (Email, Task, Deadline, System), `IsRead`, `CreatedAt`, `ReadAt`
- `RelatedCaseId`, `RelatedEmailId`, `RelatedTaskId` — navigation links
- `ActionUrl` — deep link to relevant page

### Step 2.2 — Notification Service
- New file: `Insolvio.Core/Services/NotificationService.cs`
- Interface: `Insolvio.Core/Abstractions/INotificationService.cs`
- Methods: `CreateAsync()`, `GetUnreadCountAsync()`, `GetRecentAsync()`, `MarkReadAsync()`, `MarkAllReadAsync()`

### Step 2.3 — Notification Controller
- New file: `Insolvio.API/Controllers/NotificationsController.cs`
- Endpoints: `GET /api/notifications` (paginated), `GET /api/notifications/unread-count`, `PUT /api/notifications/{id}/read`, `PUT /api/notifications/read-all`

### Step 2.4 — Frontend Notification Bell
- Modify `Insolvio.Web/src/components/Header.tsx` — add bell icon with unread count badge
- New component: `NotificationDropdown.tsx` — dropdown panel showing recent notifications
- Each notification clickable → navigates to case email thread
- Poll unread count every 30 seconds (or use existing polling pattern)

---

## Phase 3: SES Inbound Email Reception

### Step 3.1 — AWS SES Receipt Rule Configuration (Infrastructure)
- Configure SES to receive mail for `*@insolvio.io` domain (or specific verified addresses)
- Receipt Rule: store raw email (.eml) in S3 bucket `insolvio` under prefix `inbound-emails/`
- Optionally publish SNS notification (for future real-time support)
- Document the AWS console / Terraform / CloudFormation steps

### Step 3.2 — Inbound Email Polling Service
- New file: `Insolvio.API/BackgroundServices/InboundEmailPollingService.cs`
- Polls S3 bucket prefix `inbound-emails/` every 60 seconds
- Lists new objects, downloads raw .eml content
- Passes to `InboundEmailProcessorService` for parsing
- Moves processed emails to `inbound-emails/processed/` prefix
- Tracks processed S3 keys to avoid reprocessing (use S3 metadata or DB table)

### Step 3.3 — Inbound Email Processor Service
- New file: `Insolvio.Core/Services/InboundEmailProcessorService.cs`
- Interface: `Insolvio.Core/Abstractions/IInboundEmailProcessorService.cs`
- Uses `MimeKit` NuGet package to parse raw .eml files
- Extracts: From, To, Cc, Subject, Body (HTML + plain text), Attachments, Message-ID, In-Reply-To, References headers
- **Case matching:** Extract the `To` address, parse the local part, match against `InsolvencyCase.CaseEmailAddress`
- **Thread matching:** Use `In-Reply-To` / `References` headers to match `ScheduledEmail.ProviderMessageId` for threading
- Creates `ScheduledEmail` record with Direction = "Inbound", populates FromName, Body, Subject, ThreadId, AttachmentsJson

### Step 3.4 — Attachment Extraction
- Save each attachment to S3 under `emails/{caseId}/{guid}_{filename}` (existing pattern)
- Optionally create `InsolvencyDocument` records for extracted attachments (with Purpose = "EmailAttachment" or similar)
- Store attachment metadata in `ScheduledEmail.AttachmentsJson`

### Step 3.5 — Create notifications on inbound email
- After processing, call `NotificationService.CreateAsync()` for:
  - The assigned practitioner on the case
  - Any users who have sent outbound emails in the same thread
  - Admin users (if configured in email settings)
- Notification category = "Email", includes case link and email subject

---

## Phase 4: Enhanced Outbound Email (From Case Address as User)

### Step 4.1 — Send as case address with user's display name
- Modify `SmtpEmailService.SendAsync()` in `Insolvio.Integrations/Services/SmtpEmailService.cs`
- Set `From: "{CurrentUser.FullName}" <{case.CaseEmailAddress}>` instead of generic noreply@
- Set `Reply-To: {case.CaseEmailAddress}` to ensure replies go to case inbox
- Store `ProviderMessageId` (Message-ID header) on sent email for thread matching

### Step 4.2 — Auto-CC logic
- When composing/sending, auto-add CC recipients based on Email Settings:
  - Current user's personal email (always, per requirement)
  - Practitioner email (if enabled in settings)
  - Admin email(s) (if enabled in settings)
- Modify `CaseEmailService.ComposeAsync()` and `ScheduleAsync()` to inject CC addresses
- Read CC preferences from new `EmailSettings` configuration

---

## Phase 5: Email → Task Creation

### Step 5.1 — "Create Task from Email" action
- Add endpoint: `POST /api/cases/{caseId}/emails/{emailId}/create-task`
- In `CaseEmailsController` or new endpoint in `TasksController`
- Creates a `CompanyTask` with:
  - Title: "Reply to: {email subject}"
  - Description: link to email thread
  - `RelatedEmailId` on the task (or store in task's metadata/notes)
  - Category: "EmailResponse"
  - Deadline: user-specified date (passed in request body)
- Also creates a `CalendarEvent` with EventType = "Task" or "Reminder" linked to the task

### Step 5.2 — Add `RelatedEmailId` to CompanyTask
- File: `Insolvio.Domain/Entities/CompanyTask.cs`
- Add `public Guid? RelatedEmailId { get; set; }` — links task back to source email
- EF migration

### Step 5.3 — Frontend: Create Task button in email view
- Modify `CaseEmailsTab.tsx` — add "Create Task" button on each email card (especially inbound)
- Opens `TaskFormModal.tsx` pre-filled with email subject, default deadline
- Date picker for response deadline → added to calendar

### Step 5.4 — Frontend: Open email from task
- Modify `TaskDetailModal.tsx` — if task has `RelatedEmailId`, show "View Email" link
- Clicking navigates to case detail → emails tab → scrolls to/expands that email thread
- Reply button on the email pre-fills compose modal with correct thread context

---

## Phase 6: Email Settings Tab

### Step 6.1 — EmailSettings Entity
- New file: `Insolvio.Domain/Entities/EmailSettings.cs` (or add to SystemConfig as grouped keys)
- Fields: `TenantId`, `AutoCcUser` (bool, default true), `AutoCcPractitioner` (bool), `AutoCcAdmin` (bool), `AdminEmailAddresses` (string, comma-separated)
- Could also use the existing `SystemConfig` key-value store with group = "Email"

### Step 6.2 — Email Settings API
- Add to `SettingsController.cs`:
  - `GET /api/settings/email-preferences` — get CC settings
  - `PUT /api/settings/email-preferences` — update CC settings
- Or use existing `GET /api/settings/config` with group = "Email"

### Step 6.3 — Frontend: New Email Settings Tab
- New route: `/settings/email-preferences` in settings layout
- New page or tab component showing:
  - Toggle: "Auto-CC sending user on all outbound emails" (default: ON)
  - Toggle: "Auto-CC assigned practitioner"
  - Toggle: "Auto-CC admin users"
  - Text field: "Admin email addresses" (comma-separated)
  - Display: SES configuration status / health check
- Add to `SettingsLayout.tsx` sidebar navigation

---

## Phase 7: Dashboard Integration

### Step 7.1 — Dashboard email notifications panel
- Modify `DashboardService.cs` to include recent unread email count and latest inbound emails
- Add to `DashboardDto`: `UnreadEmails` count, `RecentInboundEmails` list
- Modify `DashboardPage.tsx` to show email notification cards

### Step 7.2 — Visible notice for new emails
- Add a prominent "New Emails" section or banner on dashboard when unread inbound emails exist
- Each item shows: case name, sender, subject, time ago
- Clicking navigates to case email thread

---

## Phase 8: Responsive Settings Navigation (Mobile)

### Current Problem
SettingsLayout.tsx renders a static w-64 sidebar always visible. On mobile (<md), it eats the entire screen. ProtectedLayout has a burger menu but the settings sidebar doesn't interact with it.

### Step 8.1 — SettingsLayout: Add mobile state + burger overlay
- File: Insolvio.Web/src/components/SettingsLayout.tsx
- Add useState for settingsNavOpen (default false on mobile)
- Desktop (md:): keep static sidebar unchanged
- Mobile: hide aside by default, add slide-in overlay pattern (same as ProtectedLayout)

### Step 8.2 — Back to Navigation button at top of mobile settings nav
- When settings nav is open on mobile, show "← Back to Main Nav" at top
- Navigates to /dashboard (exits settings)
- Use t.settings.backToMainNav key

### Step 8.3 — Mobile top bar for settings pages
- Add md:hidden top bar: burger button + "Settings" title
- Auto-close on nav item click

### Step 8.4 — ProtectedLayout: hide mobile top bar on settings routes
- File: Insolvio.Web/src/App.tsx (ProtectedLayout)
- Use useLocation() to detect /settings routes
- Hide ProtectedLayout mobile top bar when on settings (SettingsLayout provides its own)

---

## Phase 9: Localization of All New Features

### Step 9.1 — types.ts: add notifications, emailSettings, caseEmail sections
### Step 9.2 — en.ts: English translations
### Step 9.3 — ro.ts: Romanian translations
### Step 9.4 — hu.ts: Hungarian translations
### Step 9.5 — Use t.* keys in all new/modified components (no hardcoded English)

---

## Relevant Files

**Domain Entities (modify):**
- `Insolvio.Domain/Entities/InsolvencyCase.cs` — add CaseEmailAddress
- `Insolvio.Domain/Entities/CompanyTask.cs` — add RelatedEmailId
- `Insolvio.Domain/Entities/ScheduledEmail.cs` — add IsRead field for inbound tracking

**Domain Entities (new):**
- `Insolvio.Domain/Entities/Notification.cs` — notification records
- `Insolvio.Domain/Entities/EmailSettings.cs` — or use SystemConfig

**Services (new):**
- `Insolvio.Core/Services/CaseEmailAddressGenerator.cs` — address generation
- `Insolvio.Core/Services/NotificationService.cs` — notification CRUD
- `Insolvio.Core/Services/InboundEmailProcessorService.cs` — parse .eml, match case, create records

**Background Services (new):**
- `Insolvio.API/BackgroundServices/InboundEmailPollingService.cs` — poll S3 for inbound

**Controllers (new/modify):**
- `Insolvio.API/Controllers/NotificationsController.cs` — new
- `Insolvio.API/Controllers/CaseEmailsController.cs` — add create-task endpoint
- `Insolvio.API/Controllers/SettingsController.cs` — add email preferences endpoints

**Integrations (modify):**
- `Insolvio.Integrations/Services/SmtpEmailService.cs` — From address, Reply-To, Message-ID capture

**Frontend (new):**
- `Insolvio.Web/src/components/NotificationDropdown.tsx`
- `Insolvio.Web/src/pages/EmailSettingsPage.tsx`
- `Insolvio.Web/src/services/api/notifications.ts`

**Frontend (modify):**
- `Insolvio.Web/src/components/Header.tsx` — notification bell
- `Insolvio.Web/src/components/CaseEmailsTab.tsx` — create task button, inbound display
- `Insolvio.Web/src/components/CaseDetailView.tsx` — show case email address
- `Insolvio.Web/src/components/EmailComposeModal.tsx` — auto-CC injection
- `Insolvio.Web/src/components/TaskDetailModal.tsx` — link to email
- `Insolvio.Web/src/components/SettingsLayout.tsx` — add email settings nav item + mobile responsive
- `Insolvio.Web/src/pages/DashboardPage.tsx` — email notification panel
- `Insolvio.Web/src/App.tsx` — add email settings route + hide mobile top bar on /settings

**Frontend (modify) — Localization:**
- `Insolvio.Web/src/i18n/types.ts` — add notifications, emailSettings, caseEmail sections
- `Insolvio.Web/src/i18n/en.ts` — English translations
- `Insolvio.Web/src/i18n/ro.ts` — Romanian translations
- `Insolvio.Web/src/i18n/hu.ts` — Hungarian translations

**NuGet Packages (new):**
- `MimeKit` — for parsing raw .eml email files

**Infrastructure (manual/documented):**
- AWS SES Receipt Rules for `*@insolvio.io`
- S3 bucket policy for SES to write to `inbound-emails/` prefix
- DNS: MX record for insolvio.io pointing to SES

---

## Verification

1. **Unit tests:** CaseEmailAddressGenerator — verify ≤10 char constraint, uniqueness, special char handling, diacritic removal
2. **Unit tests:** InboundEmailProcessorService — parse sample .eml files, verify case matching, thread linking, attachment extraction
3. **Integration test:** Send test email to case address → verify it appears in S3 → polling picks it up → ScheduledEmail created with Direction=Inbound → Notification created
4. **Integration test:** Compose outbound email → verify From is case address with user name, CC includes user email, Reply-To is case address
5. **Frontend test:** Notification bell shows count, dropdown shows recent, clicking navigates to case
6. **Frontend test:** Create Task from email → task appears in task list and calendar → opening task shows email link
7. **Manual verification:** Email Settings tab — toggle CC preferences, send email, verify CC recipients match settings
8. **Manual verification:** Dashboard shows inbound email notices prominently
9. **Responsive verification:** Resize browser to <768px on settings page → ProtectedLayout top bar hidden → SettingsLayout top bar visible with burger → tap burger → settings nav slides in with "Back to Nav" at top → tap nav item → nav closes → tap overlay → nav closes
10. **Responsive verification:** On mobile, navigate dashboard → settings → only one burger menu, no competing menus
11. **Localization verification:** Switch locale to RO → all new strings in Romanian. Switch to HU → Hungarian. No hardcoded English.

---

## Decisions

- **Reuse ScheduledEmail for inbound** (with Direction="Inbound") rather than creating a separate InboundEmail entity — the entity already has all needed fields (ThreadId, Direction, FromName, CaseEmailAddress, AttachmentsJson)
- **Add `IsRead` to ScheduledEmail** for tracking read/unread state on inbound emails
- **Use MimeKit** for .eml parsing (industry standard, well-maintained .NET library)
- **Polling over webhooks** for inbound — simpler deployment, no need for publicly exposed webhook endpoint, matches existing EmailBackgroundService pattern
- **Email address format:** `{name}{-casenum}@insolvio.io` with max 10 chars local part — user-friendly, deterministic
- **Use SystemConfig** for email settings (CC preferences) rather than a new entity — follows existing settings pattern
- **Scope exclusion:** Real-time push notifications (WebSocket/SignalR) — out of scope, use polling; SMS notifications — out of scope
- **Responsive settings nav — Option A:** SettingsLayout provides its own mobile top bar; ProtectedLayout hides its top bar on /settings routes. Single burger context, no competition.
- **Mobile settings "Back" button:** Navigates to /dashboard (exits settings), consistent with existing backToApp pattern

## Further Considerations

1. **SES Sending Identity:** Outbound emails from per-case addresses require either domain-level verification (insolvio.io verified in SES) or individual email verification. Domain verification is required — is insolvio.io already verified in SES? *Recommendation: Verify the domain if not already done.*
2. **Email address collision handling:** If two cases for "ABC Corp" generate the same prefix, the plan appends a/b/c. An alternative is to always include a unique hash segment. *Recommendation: Keep the friendly format with letter suffix for rare collisions.*
3. **S3 lifecycle policy:** Raw inbound emails in S3 should have a retention policy. *Recommendation: Move processed to `/processed/` prefix, set 90-day lifecycle expiry on `/processed/`.*
