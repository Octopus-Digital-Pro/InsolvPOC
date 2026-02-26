/* =========================================================
   Insolvent - SQL Server Schema (Copilot-ready)
   =========================================================
   - Use UNIQUEIDENTIFIER PKs for distributed safety
   - TenantId on every row for hard scoping
   - Strong auditing fields on key entities
   - Tasks: mandatory Deadline + Assignee
   - Email trail supports inbound/outbound and threading
   ========================================================= */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* =========================
   0) Helper: schemas
   ========================= */
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'core')
    EXEC('CREATE SCHEMA core');
GO

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'wf')
    EXEC('CREATE SCHEMA wf');
GO

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'docs')
    EXEC('CREATE SCHEMA docs');
GO

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'comms')
    EXEC('CREATE SCHEMA comms');
GO


/* =========================
   1) Tenant + Users
   ========================= */

CREATE TABLE core.Tenant (
    TenantId            UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Tenant PRIMARY KEY,
    Name                NVARCHAR(200) NOT NULL,
    CreatedAt           DATETIME2(0) NOT NULL CONSTRAINT DF_Tenant_CreatedAt DEFAULT SYSUTCDATETIME()
);
GO

CREATE TABLE core.[User] (
    UserId              UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_User PRIMARY KEY,
    TenantId            UNIQUEIDENTIFIER NOT NULL,
    Email               NVARCHAR(320) NOT NULL,
    DisplayName         NVARCHAR(200) NOT NULL,
    IsActive            BIT NOT NULL CONSTRAINT DF_User_IsActive DEFAULT 1,
    CreatedAt           DATETIME2(0) NOT NULL CONSTRAINT DF_User_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_User_Tenant FOREIGN KEY (TenantId) REFERENCES core.Tenant(TenantId),
    CONSTRAINT UQ_User_Tenant_Email UNIQUE (TenantId, Email)
);
GO

/* =========================
   2) Company Settings (deadline defaults)
   ========================= */

CREATE TABLE core.CompanySettings (
    CompanySettingsId   UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_CompanySettings PRIMARY KEY,
    TenantId            UNIQUEIDENTIFIER NOT NULL,
    Name                NVARCHAR(200) NOT NULL, -- e.g. "Default"
    TimezoneId          NVARCHAR(64) NOT NULL CONSTRAINT DF_CompanySettings_Timezone DEFAULT 'Europe/Bucharest',

    -- Default deadline periods (days from NoticeDate)
    ClaimDeadlineDaysFromNotice              INT NULL,
    PreliminaryTableDaysFromNotice           INT NULL,
    DefinitiveTableDaysFromNotice            INT NULL,
    Report40DaysFromNotice                   INT NULL,
    MeetingNoticeMinimumDays                 INT NULL,

    CreatedAt           DATETIME2(0) NOT NULL CONSTRAINT DF_CompanySettings_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt           DATETIME2(0) NULL,

    CONSTRAINT FK_CompanySettings_Tenant FOREIGN KEY (TenantId) REFERENCES core.Tenant(TenantId),
    CONSTRAINT UQ_CompanySettings_Tenant_Name UNIQUE (TenantId, Name)
);
GO


/* =========================
   3) Parties
   ========================= */

CREATE TABLE core.Party (
    PartyId             UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Party PRIMARY KEY,
    TenantId            UNIQUEIDENTIFIER NOT NULL,
    PartyType           NVARCHAR(20) NOT NULL, -- COMPANY|PERSON|INSTITUTION
    Name                NVARCHAR(300) NOT NULL,

    -- Identifiers (Romania examples)
    CUI                 NVARCHAR(32) NULL,
    TradeRegisterNo     NVARCHAR(64) NULL,

    Email               NVARCHAR(320) NULL,
    Phone               NVARCHAR(50) NULL,

    AddressLine1        NVARCHAR(200) NULL,
    AddressLine2        NVARCHAR(200) NULL,
    City                NVARCHAR(100) NULL,
    County              NVARCHAR(100) NULL,
    PostalCode          NVARCHAR(20) NULL,
    Country             NVARCHAR(100) NULL,

    CreatedAt           DATETIME2(0) NOT NULL CONSTRAINT DF_Party_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt           DATETIME2(0) NULL,

    CONSTRAINT FK_Party_Tenant FOREIGN KEY (TenantId) REFERENCES core.Tenant(TenantId)
);
GO

CREATE INDEX IX_Party_Tenant_Name ON core.Party(TenantId, Name);
GO


/* =========================
   4) Case
   ========================= */

CREATE TABLE core.[Case] (
    CaseId              UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Case PRIMARY KEY,
    TenantId            UNIQUEIDENTIFIER NOT NULL,

    ProcedureType       NVARCHAR(40) NOT NULL, -- SIMPLIFIED_BANKRUPTCY|BANKRUPTCY|RESTRUCTURING|...
    [Status]            NVARCHAR(20) NOT NULL CONSTRAINT DF_Case_Status DEFAULT 'ACTIVE',

    CaseNumber          NVARCHAR(100) NULL,
    TribunalName        NVARCHAR(300) NULL,
    JudgeName           NVARCHAR(200) NULL,

    -- Anchor date: CaseCreationDate MUST equal NoticeDate
    NoticeDate          DATE NOT NULL,

    -- Key statutory dates (for UX + validation). Task engine still drives execution.
    ClaimSubmissionDeadline      DATE NULL,
    PreliminaryTableDeadline     DATE NULL,
    DefinitiveTableDeadline      DATE NULL,
    CreditorsMeetingDateTime     DATETIME2(0) NULL,
    Report40DaysDue              DATE NULL,
    FinalReportDue               DATE NULL,

    -- Source tracking (optional but recommended)
    ClaimDeadlineSource          NVARCHAR(30) NULL, -- NOTICE_EXTRACTED|COMPANY_DEFAULT|MANUAL_OVERRIDE|SYSTEM_COMPUTED
    PreliminaryTableSource       NVARCHAR(30) NULL,
    DefinitiveTableSource        NVARCHAR(30) NULL,
    Report40DaysSource           NVARCHAR(30) NULL,

    CompanySettingsId    UNIQUEIDENTIFIER NULL,
    CaseOwnerUserId      UNIQUEIDENTIFIER NULL,

    CreatedAt           DATETIME2(0) NOT NULL CONSTRAINT DF_Case_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt           DATETIME2(0) NULL,

    CONSTRAINT FK_Case_Tenant FOREIGN KEY (TenantId) REFERENCES core.Tenant(TenantId),
    CONSTRAINT FK_Case_CompanySettings FOREIGN KEY (CompanySettingsId) REFERENCES core.CompanySettings(CompanySettingsId),
    CONSTRAINT FK_Case_Owner FOREIGN KEY (CaseOwnerUserId) REFERENCES core.[User](UserId),

    CONSTRAINT CK_Case_Status CHECK ([Status] IN ('ACTIVE','SUSPENDED','CLOSED','CANCELLED'))
);
GO

CREATE INDEX IX_Case_Tenant_NoticeDate ON core.[Case](TenantId, NoticeDate);
CREATE INDEX IX_Case_Tenant_CaseNumber ON core.[Case](TenantId, CaseNumber);
GO


/* =========================
   5) Case Parties (roles)
   ========================= */

CREATE TABLE core.CaseParty (
    CasePartyId         UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_CaseParty PRIMARY KEY,
    TenantId            UNIQUEIDENTIFIER NOT NULL,
    CaseId              UNIQUEIDENTIFIER NOT NULL,
    PartyId             UNIQUEIDENTIFIER NOT NULL,

    [Role]              NVARCHAR(30) NOT NULL, -- DEBTOR|CREDITOR|TRIBUNAL|JUDGE|PRACTITIONER|...
    IsPrimary           BIT NOT NULL CONSTRAINT DF_CaseParty_IsPrimary DEFAULT 0,

    -- Delivery preferences for this case context
    PreferredDelivery   NVARCHAR(20) NULL, -- EMAIL|POST|BOTH
    Notes               NVARCHAR(1000) NULL,

    CreatedAt           DATETIME2(0) NOT NULL CONSTRAINT DF_CaseParty_CreatedAt DEFAULT SYSUTCDATETIME(),

    CONSTRAINT FK_CaseParty_Tenant FOREIGN KEY (TenantId) REFERENCES core.Tenant(TenantId),
    CONSTRAINT FK_CaseParty_Case FOREIGN KEY (CaseId) REFERENCES core.[Case](CaseId),
    CONSTRAINT FK_CaseParty_Party FOREIGN KEY (PartyId) REFERENCES core.Party(PartyId),

    CONSTRAINT UQ_CaseParty UNIQUE (CaseId, PartyId, [Role])
);
GO

CREATE INDEX IX_CaseParty_Case_Role ON core.CaseParty(CaseId, [Role]);
GO


/* =========================
   6) Workflow Phase Definitions + Instances
   ========================= */

CREATE TABLE wf.PhaseDefinition (
    PhaseId             UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PhaseDefinition PRIMARY KEY,
    TenantId            UNIQUEIDENTIFIER NOT NULL,

    ProcedureType       NVARCHAR(40) NOT NULL,      -- SIMPLIFIED_BANKRUPTCY etc.
    PhaseKey            NVARCHAR(64) NOT NULL,      -- e.g. INTAKE, CLAIMS, PREL_TABLE, MEETING, DEF_TABLE, REPORT_40, LIQUIDATION, FINAL
    Name                NVARCHAR(200) NOT NULL,
    SortOrder           INT NOT NULL,

    -- Optional: JSON rules for validation/requirements (enforced in app layer)
    RequirementsJson    NVARCHAR(MAX) NULL,         -- e.g. required roles, docs, outputs, conditional rules

    CreatedAt           DATETIME2(0) NOT NULL CONSTRAINT DF_PhaseDefinition_CreatedAt DEFAULT SYSUTCDATETIME(),

    CONSTRAINT FK_PhaseDefinition_Tenant FOREIGN KEY (TenantId) REFERENCES core.Tenant(TenantId),
    CONSTRAINT UQ_PhaseDefinition UNIQUE (TenantId, ProcedureType, PhaseKey)
);
GO

CREATE TABLE wf.PhaseInstance (
    PhaseInstanceId     UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PhaseInstance PRIMARY KEY,
    TenantId            UNIQUEIDENTIFIER NOT NULL,
    CaseId              UNIQUEIDENTIFIER NOT NULL,
    PhaseId             UNIQUEIDENTIFIER NOT NULL,

    [Status]            NVARCHAR(20) NOT NULL CONSTRAINT DF_PhaseInstance_Status DEFAULT 'IN_PROGRESS',
    StartedAt           DATETIME2(0) NOT NULL CONSTRAINT DF_PhaseInstance_StartedAt DEFAULT SYSUTCDATETIME(),
    CompletedAt         DATETIME2(0) NULL,
    CompletedByUserId   UNIQUEIDENTIFIER NULL,
    CompletionReason    NVARCHAR(500) NULL,

    CONSTRAINT FK_PhaseInstance_Tenant FOREIGN KEY (TenantId) REFERENCES core.Tenant(TenantId),
    CONSTRAINT FK_PhaseInstance_Case FOREIGN KEY (CaseId) REFERENCES core.[Case](CaseId),
    CONSTRAINT FK_PhaseInstance_PhaseDef FOREIGN KEY (PhaseId) REFERENCES wf.PhaseDefinition(PhaseId),
    CONSTRAINT FK_PhaseInstance_CompletedBy FOREIGN KEY (CompletedByUserId) REFERENCES core.[User](UserId),

    CONSTRAINT CK_PhaseInstance_Status CHECK ([Status] IN ('NOT_STARTED','IN_PROGRESS','COMPLETED','SKIPPED'))
);
GO

CREATE INDEX IX_PhaseInstance_Case ON wf.PhaseInstance(CaseId, StartedAt);
GO

CREATE TABLE wf.PhaseValidationResult (
    PhaseValidationResultId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PhaseValidationResult PRIMARY KEY,
    TenantId                UNIQUEIDENTIFIER NOT NULL,
    CaseId                  UNIQUEIDENTIFIER NOT NULL,
    PhaseInstanceId         UNIQUEIDENTIFIER NOT NULL,

    ValidatedAt             DATETIME2(0) NOT NULL CONSTRAINT DF_PhaseValidationResult_ValidatedAt DEFAULT SYSUTCDATETIME(),
    IsValid                 BIT NOT NULL,
    ErrorsJson              NVARCHAR(MAX) NULL,  -- structured array: codes/messages/pointers
    WarningsJson            NVARCHAR(MAX) NULL,

    CONSTRAINT FK_PhaseValidationResult_Tenant FOREIGN KEY (TenantId) REFERENCES core.Tenant(TenantId),
    CONSTRAINT FK_PhaseValidationResult_Case FOREIGN KEY (CaseId) REFERENCES core.[Case](CaseId),
    CONSTRAINT FK_PhaseValidationResult_PhaseInstance FOREIGN KEY (PhaseInstanceId) REFERENCES wf.PhaseInstance(PhaseInstanceId)
);
GO


/* =========================
   7) Documents + Extraction
   ========================= */

CREATE TABLE docs.Document (
    DocumentId           UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Document PRIMARY KEY,
    TenantId             UNIQUEIDENTIFIER NOT NULL,
    CaseId               UNIQUEIDENTIFIER NOT NULL,

    FileName             NVARCHAR(260) NOT NULL,
    ContentType          NVARCHAR(100) NULL,
    StorageUrl           NVARCHAR(1000) NOT NULL,
    FileHashSha256       VARBINARY(32) NULL,
    FileSizeBytes        BIGINT NULL,

    DocType              NVARCHAR(50) NOT NULL CONSTRAINT DF_Document_DocType DEFAULT 'UNKNOWN',
    DocTypeConfidence    DECIMAL(5,4) NULL,
    IsKeyDocument        BIT NOT NULL CONSTRAINT DF_Document_IsKey DEFAULT 0,

    Summary              NVARCHAR(MAX) NULL,   -- AI-generated + editable
    ExtractedPartiesJson NVARCHAR(MAX) NULL,   -- [{name, roleGuess, ids...}]
    ExtractedDatesJson   NVARCHAR(MAX) NULL,   -- [{date, type, label}]
    ExtractedActionsJson NVARCHAR(MAX) NULL,   -- [{title, suggestedDeadline, partyRef, ...}]
    ExtractedFieldsJson  NVARCHAR(MAX) NULL,   -- mapped fields, e.g. notice date, deadlines, tribunal etc.

    UploadedByUserId     UNIQUEIDENTIFIER NOT NULL,
    UploadedAt           DATETIME2(0) NOT NULL CONSTRAINT DF_Document_UploadedAt DEFAULT SYSUTCDATETIME(),

    CONSTRAINT FK_Document_Tenant FOREIGN KEY (TenantId) REFERENCES core.Tenant(TenantId),
    CONSTRAINT FK_Document_Case FOREIGN KEY (CaseId) REFERENCES core.[Case](CaseId),
    CONSTRAINT FK_Document_UploadedBy FOREIGN KEY (UploadedByUserId) REFERENCES core.[User](UserId)
);
GO

CREATE INDEX IX_Document_Case_DocType ON docs.Document(CaseId, DocType);
GO

CREATE TABLE docs.DocumentExtractionReview (
    DocumentExtractionReviewId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_DocumentExtractionReview PRIMARY KEY,
    TenantId                   UNIQUEIDENTIFIER NOT NULL,
    DocumentId                 UNIQUEIDENTIFIER NOT NULL,

    ReviewStatus               NVARCHAR(20) NOT NULL CONSTRAINT DF_DocumentExtractionReview_Status DEFAULT 'PENDING',
    ReviewedByUserId           UNIQUEIDENTIFIER NULL,
    ReviewedAt                 DATETIME2(0) NULL,

    -- Store final accepted/edited extracted values (for audit)
    FinalExtractedPartiesJson  NVARCHAR(MAX) NULL,
    FinalExtractedDatesJson    NVARCHAR(MAX) NULL,
    FinalExtractedActionsJson  NVARCHAR(MAX) NULL,
    FinalExtractedFieldsJson   NVARCHAR(MAX) NULL,

    Notes                      NVARCHAR(2000) NULL,

    CONSTRAINT FK_DocumentExtractionReview_Tenant FOREIGN KEY (TenantId) REFERENCES core.Tenant(TenantId),
    CONSTRAINT FK_DocumentExtractionReview_Document FOREIGN KEY (DocumentId) REFERENCES docs.Document(DocumentId),
    CONSTRAINT FK_DocumentExtractionReview_ReviewedBy FOREIGN KEY (ReviewedByUserId) REFERENCES core.[User](UserId),

    CONSTRAINT CK_DocumentExtractionReview_Status CHECK (ReviewStatus IN ('PENDING','ACCEPTED','EDITED','REJECTED'))
);
GO


/* =========================
   8) Tasks + Dependencies
   ========================= */

CREATE TABLE wf.Task (
    TaskId              UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Task PRIMARY KEY,
    TenantId            UNIQUEIDENTIFIER NOT NULL,
    CaseId              UNIQUEIDENTIFIER NOT NULL,
    PhaseInstanceId     UNIQUEIDENTIFIER NULL,

    TaskType            NVARCHAR(20) NOT NULL, -- ACTION|EVENT|EMAIL|DOCUMENT|FILING|REVIEW
    Title               NVARCHAR(300) NOT NULL,
    [Description]       NVARCHAR(MAX) NULL,

    AssigneeUserId      UNIQUEIDENTIFIER NOT NULL,
    CreatedByUserId     UNIQUEIDENTIFIER NOT NULL,

    Deadline            DATETIME2(0) NOT NULL,  -- mandatory
    DeadlineSource      NVARCHAR(30) NULL,      -- NOTICE_EXTRACTED|COMPANY_DEFAULT|MANUAL_OVERRIDE|SYSTEM_COMPUTED
    IsCritical          BIT NOT NULL CONSTRAINT DF_Task_IsCritical DEFAULT 0,

    [Status]            NVARCHAR(20) NOT NULL CONSTRAINT DF_Task_Status DEFAULT 'OPEN',
    CompletedAt         DATETIME2(0) NULL,

    RelatedDocumentId   UNIQUEIDENTIFIER NULL,
    RelatedCasePartyId  UNIQUEIDENTIFIER NULL,
    RelatedEmailId      UNIQUEIDENTIFIER NULL,
    RelatedEventId      UNIQUEIDENTIFIER NULL,

    CreatedAt           DATETIME2(0) NOT NULL CONSTRAINT DF_Task_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt           DATETIME2(0) NULL,

    CONSTRAINT FK_Task_Tenant FOREIGN KEY (TenantId) REFERENCES core.Tenant(TenantId),
    CONSTRAINT FK_Task_Case FOREIGN KEY (CaseId) REFERENCES core.[Case](CaseId),
    CONSTRAINT FK_Task_PhaseInstance FOREIGN KEY (PhaseInstanceId) REFERENCES wf.PhaseInstance(PhaseInstanceId),
    CONSTRAINT FK_Task_Assignee FOREIGN KEY (AssigneeUserId) REFERENCES core.[User](UserId),
    CONSTRAINT FK_Task_CreatedBy FOREIGN KEY (CreatedByUserId) REFERENCES core.[User](UserId),
    CONSTRAINT FK_Task_RelatedDocument FOREIGN KEY (RelatedDocumentId) REFERENCES docs.Document(DocumentId),
    CONSTRAINT FK_Task_RelatedCaseParty FOREIGN KEY (RelatedCasePartyId) REFERENCES core.CaseParty(CasePartyId),

    CONSTRAINT CK_Task_Type CHECK (TaskType IN ('ACTION','EVENT','EMAIL','DOCUMENT','FILING','REVIEW')),
    CONSTRAINT CK_Task_Status CHECK ([Status] IN ('OPEN','IN_PROGRESS','BLOCKED','DONE','OVERDUE','CANCELLED'))
);
GO

CREATE INDEX IX_Task_Case_Status_Deadline ON wf.Task(CaseId, [Status], Deadline);
CREATE INDEX IX_Task_Assignee_Status_Deadline ON wf.Task(AssigneeUserId, [Status], Deadline);
GO

CREATE TABLE wf.TaskDependency (
    TaskDependencyId     UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_TaskDependency PRIMARY KEY,
    TenantId             UNIQUEIDENTIFIER NOT NULL,
    TaskId               UNIQUEIDENTIFIER NOT NULL,
    DependsOnTaskId      UNIQUEIDENTIFIER NOT NULL,

    CONSTRAINT FK_TaskDependency_Tenant FOREIGN KEY (TenantId) REFERENCES core.Tenant(TenantId),
    CONSTRAINT FK_TaskDependency_Task FOREIGN KEY (TaskId) REFERENCES wf.Task(TaskId),
    CONSTRAINT FK_TaskDependency_DependsOn FOREIGN KEY (DependsOnTaskId) REFERENCES wf.Task(TaskId),
    CONSTRAINT UQ_TaskDependency UNIQUE (TaskId, DependsOnTaskId),
    CONSTRAINT CK_TaskDependency_NoSelf CHECK (TaskId <> DependsOnTaskId)
);
GO


/* =========================
   9) Creditor Claims
   ========================= */

CREATE TABLE core.CreditorClaim (
    ClaimId             UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_CreditorClaim PRIMARY KEY,
    TenantId            UNIQUEIDENTIFIER NOT NULL,
    CaseId              UNIQUEIDENTIFIER NOT NULL,
    CreditorCasePartyId UNIQUEIDENTIFIER NOT NULL, -- must be CaseParty with Role=CREDITOR

    DeclaredAmount      DECIMAL(18,2) NOT NULL,
    AdmittedAmount      DECIMAL(18,2) NULL,
    Rank                NVARCHAR(30) NOT NULL CONSTRAINT DF_CreditorClaim_Rank DEFAULT 'UNKNOWN',
    [Status]            NVARCHAR(20) NOT NULL CONSTRAINT DF_CreditorClaim_Status DEFAULT 'RECEIVED',

    ReceivedAt          DATETIME2(0) NOT NULL CONSTRAINT DF_CreditorClaim_ReceivedAt DEFAULT SYSUTCDATETIME(),
    ReviewedByUserId    UNIQUEIDENTIFIER NULL,
    ReviewedAt          DATETIME2(0) NULL,

    Notes               NVARCHAR(MAX) NULL,

    CONSTRAINT FK_CreditorClaim_Tenant FOREIGN KEY (TenantId) REFERENCES core.Tenant(TenantId),
    CONSTRAINT FK_CreditorClaim_Case FOREIGN KEY (CaseId) REFERENCES core.[Case](CaseId),
    CONSTRAINT FK_CreditorClaim_CaseParty FOREIGN KEY (CreditorCasePartyId) REFERENCES core.CaseParty(CasePartyId),
    CONSTRAINT FK_CreditorClaim_ReviewedBy FOREIGN KEY (ReviewedByUserId) REFERENCES core.[User](UserId),

    CONSTRAINT CK_CreditorClaim_Status CHECK ([Status] IN ('RECEIVED','UNDER_REVIEW','ADMITTED','REJECTED','NEEDS_INFO'))
);
GO

CREATE INDEX IX_CreditorClaim_Case_Status ON core.CreditorClaim(CaseId, [Status]);
GO

CREATE TABLE core.CreditorClaimDocument (
    CreditorClaimDocumentId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_CreditorClaimDocument PRIMARY KEY,
    TenantId                UNIQUEIDENTIFIER NOT NULL,
    ClaimId                 UNIQUEIDENTIFIER NOT NULL,
    DocumentId              UNIQUEIDENTIFIER NOT NULL,

    CONSTRAINT FK_CreditorClaimDocument_Tenant FOREIGN KEY (TenantId) REFERENCES core.Tenant(TenantId),
    CONSTRAINT FK_CreditorClaimDocument_Claim FOREIGN KEY (ClaimId) REFERENCES core.CreditorClaim(ClaimId),
    CONSTRAINT FK_CreditorClaimDocument_Document FOREIGN KEY (DocumentId) REFERENCES docs.Document(DocumentId),
    CONSTRAINT UQ_CreditorClaimDocument UNIQUE (ClaimId, DocumentId)
);
GO


/* =========================
   10) Assets
   ========================= */

CREATE TABLE core.Asset (
    AssetId             UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Asset PRIMARY KEY,
    TenantId            UNIQUEIDENTIFIER NOT NULL,
    CaseId              UNIQUEIDENTIFIER NOT NULL,

    AssetType           NVARCHAR(50) NOT NULL, -- VEHICLE|REAL_ESTATE|RECEIVABLE|EQUIPMENT|...
    [Description]       NVARCHAR(1000) NOT NULL,
    EstimatedValue      DECIMAL(18,2) NULL,

    EncumbranceJson     NVARCHAR(MAX) NULL, -- secured creditor refs, liens etc.
    [Status]            NVARCHAR(20) NOT NULL CONSTRAINT DF_Asset_Status DEFAULT 'IDENTIFIED',

    SoldAt              DATETIME2(0) NULL,
    SaleProceeds        DECIMAL(18,2) NULL,

    CreatedAt           DATETIME2(0) NOT NULL CONSTRAINT DF_Asset_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt           DATETIME2(0) NULL,

    CONSTRAINT FK_Asset_Tenant FOREIGN KEY (TenantId) REFERENCES core.Tenant(TenantId),
    CONSTRAINT FK_Asset_Case FOREIGN KEY (CaseId) REFERENCES core.[Case](CaseId),
    CONSTRAINT CK_Asset_Status CHECK ([Status] IN ('IDENTIFIED','VALUED','FOR_SALE','SOLD','UNRECOVERABLE'))
);
GO

CREATE INDEX IX_Asset_Case_Status ON core.Asset(CaseId, [Status]);
GO


/* =========================
   11) Templates + Generated Documents
   ========================= */

CREATE TABLE docs.Template (
    TemplateId          UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Template PRIMARY KEY,
    TenantId            UNIQUEIDENTIFIER NOT NULL,

    Name                NVARCHAR(200) NOT NULL,
    Locale              NVARCHAR(10) NOT NULL CONSTRAINT DF_Template_Locale DEFAULT 'ro',
    PhaseKey            NVARCHAR(64) NULL, -- link to phase config by key
    DocType             NVARCHAR(50) NOT NULL, -- DocumentType name
    MergeSchemaJson     NVARCHAR(MAX) NULL, -- required merge fields

    StorageUrl          NVARCHAR(1000) NOT NULL, -- where the template file lives
    IsActive            BIT NOT NULL CONSTRAINT DF_Template_IsActive DEFAULT 1,

    CreatedAt           DATETIME2(0) NOT NULL CONSTRAINT DF_Template_CreatedAt DEFAULT SYSUTCDATETIME(),

    CONSTRAINT FK_Template_Tenant FOREIGN KEY (TenantId) REFERENCES core.Tenant(TenantId),
    CONSTRAINT UQ_Template UNIQUE (TenantId, Name, Locale)
);
GO

CREATE TABLE docs.GeneratedDocument (
    GeneratedDocumentId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_GeneratedDocument PRIMARY KEY,
    TenantId            UNIQUEIDENTIFIER NOT NULL,
    CaseId              UNIQUEIDENTIFIER NOT NULL,
    TemplateId          UNIQUEIDENTIFIER NOT NULL,

    DocumentId          UNIQUEIDENTIFIER NULL, -- links to docs.Document after rendering
    [Status]            NVARCHAR(20) NOT NULL CONSTRAINT DF_GeneratedDocument_Status DEFAULT 'GENERATED',

    RenderedAt          DATETIME2(0) NOT NULL CONSTRAINT DF_GeneratedDocument_RenderedAt DEFAULT SYSUTCDATETIME(),
    SubmittedAt         DATETIME2(0) NULL,
    SentAt              DATETIME2(0) NULL,

    MergeDataJson       NVARCHAR(MAX) NULL, -- snapshot of merge values used (audit)

    CONSTRAINT FK_GeneratedDocument_Tenant FOREIGN KEY (TenantId) REFERENCES core.Tenant(TenantId),
    CONSTRAINT FK_GeneratedDocument_Case FOREIGN KEY (CaseId) REFERENCES core.[Case](CaseId),
    CONSTRAINT FK_GeneratedDocument_Template FOREIGN KEY (TemplateId) REFERENCES docs.Template(TemplateId),
    CONSTRAINT FK_GeneratedDocument_Document FOREIGN KEY (DocumentId) REFERENCES docs.Document(DocumentId),

    CONSTRAINT CK_GeneratedDocument_Status CHECK ([Status] IN ('GENERATED','SUBMITTED','SENT','FAILED'))
);
GO


/* =========================
   12) Emails (trail + threading + attachments)
   ========================= */

CREATE TABLE comms.EmailMessage (
    EmailId              UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_EmailMessage PRIMARY KEY,
    TenantId             UNIQUEIDENTIFIER NOT NULL,
    CaseId               UNIQUEIDENTIFIER NOT NULL,

    Direction            NVARCHAR(10) NOT NULL, -- INBOUND|OUTBOUND
    [Status]             NVARCHAR(20) NOT NULL, -- DRAFT|SCHEDULED|SENT|FAILED|RECEIVED

    -- Headers / addressing
    FromAddress          NVARCHAR(320) NOT NULL,
    ToAddresses          NVARCHAR(MAX) NOT NULL, -- JSON array recommended in app, stored as text here
    CcAddresses          NVARCHAR(MAX) NULL,
    BccAddresses         NVARCHAR(MAX) NULL,

    Subject              NVARCHAR(500) NULL,
    BodyText             NVARCHAR(MAX) NULL,
    BodyHtml             NVARCHAR(MAX) NULL,

    -- Provider & threading
    ProviderMessageId    NVARCHAR(200) NULL,
    MessageIdHeader      NVARCHAR(500) NULL,
    InReplyToHeader      NVARCHAR(500) NULL,
    ReferencesHeader     NVARCHAR(MAX) NULL,
    ThreadKey            NVARCHAR(200) NULL, -- derived stable key

    -- Scheduling / timestamps
    ScheduledSendAt      DATETIME2(0) NULL,
    SentAt               DATETIME2(0) NULL,
    ReceivedAt           DATETIME2(0) NULL,

    RawMimeStorageUrl    NVARCHAR(1000) NULL, -- optional: store raw .eml

    CreatedByUserId      UNIQUEIDENTIFIER NULL, -- outbound drafts
    CreatedAt            DATETIME2(0) NOT NULL CONSTRAINT DF_EmailMessage_CreatedAt DEFAULT SYSUTCDATETIME(),

    CONSTRAINT FK_EmailMessage_Tenant FOREIGN KEY (TenantId) REFERENCES core.Tenant(TenantId),
    CONSTRAINT FK_EmailMessage_Case FOREIGN KEY (CaseId) REFERENCES core.[Case](CaseId),
    CONSTRAINT FK_EmailMessage_CreatedBy FOREIGN KEY (CreatedByUserId) REFERENCES core.[User](UserId),

    CONSTRAINT CK_EmailMessage_Direction CHECK (Direction IN ('INBOUND','OUTBOUND')),
    CONSTRAINT CK_EmailMessage_Status CHECK ([Status] IN ('DRAFT','SCHEDULED','SENT','FAILED','RECEIVED'))
);
GO

CREATE INDEX IX_EmailMessage_Case_CreatedAt ON comms.EmailMessage(CaseId, CreatedAt);
CREATE INDEX IX_EmailMessage_Case_ThreadKey ON comms.EmailMessage(CaseId, ThreadKey);
GO

CREATE TABLE comms.EmailAttachment (
    EmailAttachmentId    UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_EmailAttachment PRIMARY KEY,
    TenantId             UNIQUEIDENTIFIER NOT NULL,
    EmailId              UNIQUEIDENTIFIER NOT NULL,
    DocumentId           UNIQUEIDENTIFIER NOT NULL,

    CONSTRAINT FK_EmailAttachment_Tenant FOREIGN KEY (TenantId) REFERENCES core.Tenant(TenantId),
    CONSTRAINT FK_EmailAttachment_Email FOREIGN KEY (EmailId) REFERENCES comms.EmailMessage(EmailId),
    CONSTRAINT FK_EmailAttachment_Document FOREIGN KEY (DocumentId) REFERENCES docs.Document(DocumentId),
    CONSTRAINT UQ_EmailAttachment UNIQUE (EmailId, DocumentId)
);
GO


/* =========================
   13) Calendar Events
   ========================= */

CREATE TABLE comms.CalendarEvent (
    CalendarEventId      UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_CalendarEvent PRIMARY KEY,
    TenantId             UNIQUEIDENTIFIER NOT NULL,
    CaseId               UNIQUEIDENTIFIER NOT NULL,

    EventType            NVARCHAR(20) NOT NULL, -- DEADLINE|MEETING|HEARING|INTERNAL|OTHER
    Title                NVARCHAR(300) NOT NULL,
    [Description]        NVARCHAR(MAX) NULL,

    StartAt              DATETIME2(0) NOT NULL,
    EndAt                DATETIME2(0) NULL,
    Location             NVARCHAR(300) NULL,

    ParticipantsJson     NVARCHAR(MAX) NULL, -- emails or party ids
    IcsStorageUrl        NVARCHAR(1000) NULL,

    CreatedByUserId      UNIQUEIDENTIFIER NOT NULL,
    CreatedAt            DATETIME2(0) NOT NULL CONSTRAINT DF_CalendarEvent_CreatedAt DEFAULT SYSUTCDATETIME(),

    CONSTRAINT FK_CalendarEvent_Tenant FOREIGN KEY (TenantId) REFERENCES core.Tenant(TenantId),
    CONSTRAINT FK_CalendarEvent_Case FOREIGN KEY (CaseId) REFERENCES core.[Case](CaseId),
    CONSTRAINT FK_CalendarEvent_CreatedBy FOREIGN KEY (CreatedByUserId) REFERENCES core.[User](UserId),

    CONSTRAINT CK_CalendarEvent_Type CHECK (EventType IN ('DEADLINE','MEETING','HEARING','INTERNAL','OTHER'))
);
GO

CREATE INDEX IX_CalendarEvent_Case_StartAt ON comms.CalendarEvent(CaseId, StartAt);
GO


/* =========================
   14) Optional: Case inbound email address
   ========================= */

CREATE TABLE comms.CaseMailbox (
    CaseMailboxId         UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_CaseMailbox PRIMARY KEY,
    TenantId              UNIQUEIDENTIFIER NOT NULL,
    CaseId                UNIQUEIDENTIFIER NOT NULL,

    MailboxAddress        NVARCHAR(320) NOT NULL,  -- e.g. 467-119-2023@cases.yourdomain.ro
    IsActive              BIT NOT NULL CONSTRAINT DF_CaseMailbox_IsActive DEFAULT 1,

    CreatedAt             DATETIME2(0) NOT NULL CONSTRAINT DF_CaseMailbox_CreatedAt DEFAULT SYSUTCDATETIME(),

    CONSTRAINT FK_CaseMailbox_Tenant FOREIGN KEY (TenantId) REFERENCES core.Tenant(TenantId),
    CONSTRAINT FK_CaseMailbox_Case FOREIGN KEY (CaseId) REFERENCES core.[Case](CaseId),
    CONSTRAINT UQ_CaseMailbox UNIQUE (TenantId, MailboxAddress),
    CONSTRAINT UQ_CaseMailbox_Case UNIQUE (CaseId)
);
GO


/* =========================
   15) Recommended check constraints for enums-on-NVARCHAR
   ========================= */

ALTER TABLE core.Party
ADD CONSTRAINT CK_Party_PartyType CHECK (PartyType IN ('COMPANY','PERSON','INSTITUTION'));
GO

ALTER TABLE core.CaseParty
ADD CONSTRAINT CK_CaseParty_Role CHECK ([Role] IN ('DEBTOR','CREDITOR','TRIBUNAL','JUDGE','PRACTITIONER','TAX_AUTHORITY','BANK','EMPLOYEE_REP','OTHER'));
GO

ALTER TABLE core.[Case]
ADD CONSTRAINT CK_Case_ProcedureType CHECK (ProcedureType IN ('SIMPLIFIED_BANKRUPTCY','BANKRUPTCY','RESTRUCTURING','OBSERVATION','LIQUIDATION'));
GO

ALTER TABLE docs.Document
ADD CONSTRAINT CK_Document_DocType CHECK (DocType IN (
    'UNKNOWN',
    'NOTICE_OPENING_PROCEDURE',
    'CLAIM_SUBMISSION',
    'PRELIMINARY_CLAIMS_TABLE',
    'DEFINITIVE_CLAIMS_TABLE',
    'CREDITORS_MEETING_MINUTES',
    'REPORT_40_DAYS_CAUSES',
    'FINAL_REPORT',
    'ASSET_VALUATION',
    'SALE_AGREEMENT',
    'DISTRIBUTION_STATEMENT',
    'FILING_CONFIRMATION',
    'OTHER'
));
GO

ALTER TABLE wf.Task
ADD CONSTRAINT CK_Task_DeadlineSource CHECK (DeadlineSource IS NULL OR DeadlineSource IN ('NOTICE_EXTRACTED','COMPANY_DEFAULT','MANUAL_OVERRIDE','SYSTEM_COMPUTED'));
GO

ALTER TABLE core.[Case]
ADD CONSTRAINT CK_Case_DeadlineSources CHECK (
    (ClaimDeadlineSource IS NULL OR ClaimDeadlineSource IN ('NOTICE_EXTRACTED','COMPANY_DEFAULT','MANUAL_OVERRIDE','SYSTEM_COMPUTED')) AND
    (PreliminaryTableSource IS NULL OR PreliminaryTableSource IN ('NOTICE_EXTRACTED','COMPANY_DEFAULT','MANUAL_OVERRIDE','SYSTEM_COMPUTED')) AND
    (DefinitiveTableSource IS NULL OR DefinitiveTableSource IN ('NOTICE_EXTRACTED','COMPANY_DEFAULT','MANUAL_OVERRIDE','SYSTEM_COMPUTED')) AND
    (Report40DaysSource IS NULL OR Report40DaysSource IN ('NOTICE_EXTRACTED','COMPANY_DEFAULT','MANUAL_OVERRIDE','SYSTEM_COMPUTED'))
);
GO


/* =========================================================
   End schema
   ========================================================= */