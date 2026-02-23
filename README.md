# Insolvex — Insolvency Case Management Platform

Multi-tenant SaaS application for Romanian insolvency practitioners. Built with **.NET 8** backend, **React + TypeScript** frontend, and **SQL Server** database.

## Quick Start

```bash
# Clone
git clone https://github.com/Octopus-Digital-Pro/InsolvPOC.git
cd insolvex

# Run everything (applies migrations, seeds data, starts API + frontend)
start-app.cmd
```

**Demo credentials**: `admin@insolvex.local` / `Admin123!`

---

## Architecture

```
Insolvex.sln
├── Insolvex.Domain/       # Entities, Enums (Permission, CaseStage, UserRole, etc.)
├── Insolvex.Core/         # DTOs, Abstractions (IAuditService, ICurrentUserService), Mapping
├── Insolvex.API/      # ASP.NET 8 Web API
│   ├── Controllers/          # 20 controllers with granular RBAC
│   ├── Services/ # Auth, Audit, Workflow, Classification, Signing
│   ├── Authorization/        # Permission-based policy system
│   ├── Middleware/         # Audit, Error, Tenant, Security Headers
│ └── Data/        # EF Core DbContext, Migrations, Seeder
├── Insolvex.Web/   # React 18 + Vite + Tailwind CSS
│   └── src/
│       ├── pages/            # 12 pages (Dashboard, Cases, Companies, Tasks, Settings, etc.)
│       ├── components/     # 32 components + 9 UI primitives
│     ├── services/api/     # 12 API client files (Axios)
│       ├── contexts/         # Auth, Language, Tenant
│       └── i18n/             # English, Romanian, Hungarian
├── Insolvex.Tests/   # Unit tests
└── netlify/functions/        # Serverless AI document extraction
```

## Key Features

### Multi-Tenancy
- Row-level tenant isolation via EF Core global query filters
- `TenantScopedEntity` base class auto-sets `TenantId` on save
- Tenant resolution from JWT claims
- GlobalAdmin can switch tenants via sidebar selector

### RBAC (Role-Based Access Control)
- **4 roles**: GlobalAdmin → TenantAdmin → Practitioner → Secretary
- **40 granular permissions** (e.g., `CaseView`, `DocumentSign`, `TemplateGenerate`)
- `[RequirePermission(Permission.X)]` attribute on every endpoint
- `RolePermissions` static map defines what each role can do
- Policies auto-registered in `Program.cs` via `Enum.GetValues<Permission>()`

### Audit Trail
- **Every action logged** with user, timestamp, IP, entity type, severity, category
- **Automatic old→new JSON diffing** on entity updates
- **11 categories**: Auth, Case, Document, Task, Party, Workflow, Signing, Meeting, Settings, User, System
- **3 severities**: Info, Warning, Critical
- `AuditMiddleware` captures request path, method, status code, duration
- Frontend: expandable detail rows, severity badges, category/severity filters, stats dashboard

### Document Workflow
- AI-powered document classification and data extraction
- 8-stage insolvency workflow: Intake → EligibilitySetup → FormalNotifications → CreditorClaims → AssetAssessment → CreditorMeeting → RealisationDistributions → ReportingCompliance → Closure
- Digital document signing with PFX certificate upload
- Mail-merge template engine for generating official documents

### User Management
- JWT authentication with BCrypt password hashing
- Token-based invitation system (7-day expiry)
- Password reset flow (token generation + validation)
- Role-based settings visibility (Practitioner/Secretary can view but not edit)

### Internationalization
- 3 languages: English, Romanian, Hungarian
- All UI labels, dropdowns, error messages, and form placeholders translated
- Language switcher persisted in localStorage

## Tech Stack

| Layer | Technology |
|---|---|
| Backend | ASP.NET 8, EF Core 8, SQL Server |
| Frontend | React 18, TypeScript, Vite, Tailwind CSS, shadcn/ui |
| Auth | JWT Bearer, BCrypt.Net |
| AI | OpenAI GPT-4o (document extraction) |
| Signing | System.Security.Cryptography (PFX/X509) |

## API Endpoints (20 Controllers)

| Controller | Endpoints | Auth |
|---|---|---|
| Auth | login, me, change-password, forgot-password, reset-password | AllowAnonymous / Authorize |
| Cases | CRUD + search + export | CaseView/Create/Edit/Delete |
| CaseParties | CRUD per case | PartyView/Create/Edit/Delete |
| CasePhases | initialize, update, advance | PhaseView/Edit/Initialize/Advance |
| StageTransition | current, advance | StageView/Advance |
| Companies | CRUD + search | CompanyView/Create/Edit/Delete |
| Documents | CRUD + submission-check + by-company | DocumentView/Upload/Edit/Delete |
| DocumentUpload | upload + confirm | DocumentUpload/CaseCreate |
| DocumentSigning | keys, sign, verify, download/upload-signed | SigningKeyManage/DocumentSign/SignatureVerify |
| Tasks | CRUD + my-tasks | TaskView/Create/Edit/Delete |
| CreditorMeeting | list + create | MeetingView/Create |
| MailMerge | generate + generate-all | TemplateView/Generate |
| CaseSummary | get + generate | SummaryView/Generate |
| Settings | tenant, firm, emails, errors, users, config, templates, demo | SettingsView/Edit, SystemConfig, etc. |
| Users | CRUD + invite + accept-invitation + roles + my-permissions | UserView/Edit/Invite/Deactivate |
| Tenants | CRUD | SystemConfigView |
| AuditLogs | list + count + categories + stats | AuditLogView |
| ErrorLogs | list | ErrorLogView |
| Dashboard | stats + calendar | DashboardView |
| DeadlineSettings | get + update | SettingsView |

## Development

```bash
# Backend
cd Insolvex.API
dotnet run

# Frontend
cd Insolvex.Web
npm run dev

# Run tests
dotnet test Insolvex.Tests

# Add migration
cd Insolvex.API
dotnet ef migrations add <Name> --output-dir Data/Migrations

# Apply migrations
dotnet ef database update
```

## Environment

Requires:
- .NET 8 SDK
- Node.js 18+
- SQL Server (LocalDB or full instance)
- Connection string in `appsettings.json` → `ConnectionStrings:DefaultConnection`
