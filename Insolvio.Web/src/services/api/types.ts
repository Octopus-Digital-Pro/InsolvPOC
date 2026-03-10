// Shared API types matching the .NET DTOs

export interface UserDto {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  fullName: string;
  role: "globalAdmin" | "tenantAdmin" | "practitioner" | "secretary";
  isActive: boolean;
  lastLoginDate: string | null;
  avatarUrl: string | null;
  tenantId: string;
  useSavedSigningKey: boolean;
}

export interface TenantDto {
  id: string;
  name: string;
  domain: string | null;
  isActive: boolean;
  isDemo: boolean;
  subscriptionExpiry: string | null;
  planName: string | null;
  region?: string;
}

export interface CompanyDto {
  id: string;
  name: string;
  companyType: string;
  cuiRo: string | null;
  tradeRegisterNo: string | null;
  vatNumber: string | null;
  address: string | null;
  locality: string | null;
  county: string | null;
  country: string | null;
  postalCode: string | null;
  caen: string | null;
  incorporationYear: string | null;
  shareCapitalRon: number | null;
  phone: string | null;
  email: string | null;
  contactPerson: string | null;
  iban: string | null;
  bankName: string | null;
  assignedToUserId: string | null;
  assignedToName: string | null;
  createdOn: string;
  caseCount: number;
  caseNumbers: string[] | null;
}

export interface CaseDto {
  id: string;
  caseNumber: string;
  courtName: string | null;
  courtSection: string | null;
  judgeSyndic: string | null;
  registrar: string | null;
  debtorName: string;
  debtorCui: string | null;
  procedureType: string;
  status: string;
  lawReference: string | null;
  practitionerName: string | null;
  practitionerRole: string | null;
  practitionerFiscalId: string | null;
  practitionerDecisionNo: string | null;
  noticeDate: string | null;
  openingDate: string | null;
  nextHearingDate: string | null;
  claimsDeadline: string | null;
  contestationsDeadline: string | null;
  definitiveTableDate: string | null;
  reorganizationPlanDeadline: string | null;
  closureDate: string | null;
  totalClaimsRon: number | null;
  securedClaimsRon: number | null;
  unsecuredClaimsRon: number | null;
  budgetaryClaimsRon: number | null;
  employeeClaimsRon: number | null;
  estimatedAssetValueRon: number | null;
  bpiPublicationNo: string | null;
  bpiPublicationDate: string | null;
  openingDecisionNo: string | null;
  notes: string | null;
  companyId: string | null;
  companyName: string | null;
  assignedToUserId: string | null;
  assignedToName: string | null;
  createdOn: string;
  documentCount: number;
  partyCount: number;
}

export interface CasePartyDto {
  id: string;
  caseId: string;
  companyId: string | null;
  companyName: string | null;
  email: string | null;
  role: string;
  roleDescription: string | null;
  claimAmountRon: number | null;
  claimAccepted: boolean | null;
  joinedDate: string | null;
  notes: string | null;
  name: string | null;
  identifier: string | null;
}

export interface CreditorClaimDto {
  id: string;
  caseId: string;
  creditorPartyId: string;
  creditorName: string;
  creditorIdentifier: string | null;
  creditorRole: string;
  rowNumber: number;
  declaredAmount: number;
  admittedAmount: number | null;
  rank: string;
  natureDescription: string | null;
  status: string;
  receivedAt: string | null;
  notes: string | null;
  createdOn: string;
}

export interface AssetDto {
  id: string;
  caseId: string;
  assetType: string;
  description: string;
  estimatedValue: number | null;
  encumbranceDetails: string | null;
  securedCreditorPartyId: string | null;
  securedCreditorName: string | null;
  status: string;
  saleProceeds: number | null;
  disposedAt: string | null;
  notes: string | null;
  createdOn: string;
}

export interface InsolvencyFirmDto {
  id: string;
  tenantId: string;
  firmName: string;
  cuiRo: string | null;
  tradeRegisterNo: string | null;
  vatNumber: string | null;
  unpirRegistrationNo: string | null;
  unpirRfo: string | null;
  address: string | null;
  locality: string | null;
  county: string | null;
  country: string | null;
  postalCode: string | null;
  phone: string | null;
  fax: string | null;
  email: string | null;
  website: string | null;
  contactPerson: string | null;
  iban: string | null;
  bankName: string | null;
  secondaryIban: string | null;
  secondaryBankName: string | null;
  logoUrl: string | null;
}

export interface DocumentDto {
  id: string;
  caseId: string;
  sourceFileName: string;
  docType: string;
  documentDate: string | null;
  uploadedBy: string;
  uploadedAt: string;
  rawExtraction: string | null;
}

export interface TaskDto {
  id: string;
  companyId: string;
  companyName: string | null;
  caseId: string | null;
  caseNumber: string | null;
  title: string;
  description: string | null;
  labels: string | null;
  category: string | null;
  deadline: string | null;
  status: "open" | "inProgress" | "blocked" | "done";
  blockReason: string | null;
  assignedToUserId: string | null;
  assignedToName: string | null;
  createdOn: string;
  reportSummary: string | null;
  isAdHoc?: boolean;
}

export interface TaskNoteDto {
  id: string;
  taskId: string;
  content: string;
  createdByName: string;
  createdOn: string;
  updatedOn: string | null;
}

export interface CompanyCasePartyDto {
  id: string;
  caseId: string;
  caseNumber: string | null;
  debtorName: string | null;
  companyId: string;
  companyName: string | null;
  role: string;
  roleDescription: string | null;
  claimAmountRon: number | null;
  claimAccepted: boolean | null;
  joinedDate: string | null;
  notes: string | null;
}


export interface DashboardDto {
  totalCases: number;
  openCases: number;
  totalCompanies: number;
  pendingTasks: number;
  overdueTasks: number;
  unreadEmailCount: number;
  upcomingDeadlines: UpcomingDeadlineDto[];
  calendarEvents: CalendarEventDto[];
  recentTasks: TaskDto[];
}

export interface UpcomingDeadlineDto {
  caseId: string;
  caseNumber: string;
  debtorName: string;
  deadlineType: string;
  deadlineDate: string;
  companyName: string | null;
}

export interface CalendarEventDto {
  id: string;
  title: string;
  start: string;
  end: string | null;
  type: "hearing" | "task" | string;
  caseId: string | null;
  companyId: string | null;
  metadata: string | null;
}

export interface AuditLogDto {
  id: string;
  action: string;
  description: string | null;
  userId: string | null;
  userEmail: string | null;
  entityType: string | null;
  entityId: string | null;
  caseNumber: string | null;
  changes: string | null;
  oldValues: string | null;
  newValues: string | null;
  ipAddress: string | null;
  userAgent: string | null;
  requestMethod: string | null;
  requestPath: string | null;
  responseStatusCode: number | null;
  durationMs: number | null;
  severity: string;
  category: string;
  correlationId: string | null;
  timestamp: string;
}

export interface AuditLogListResponse {
  items: AuditLogDto[];
  total: number;
  page: number;
  pageSize: number;
}

export interface AuditLogStats {
  total: number;
  byCategory: { category: string; count: number }[];
  bySeverity: { severity: string; count: number }[];
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface LoginResponse {
  token: string;
  user: UserDto;
}

export interface UploadData {
  id: string;
  fileName: string;
  fileSize: number;
  recommendedAction: string;
  docType: string | null;
  caseNumber: string | null;
  debtorName: string | null;
  debtorCui: string | null;
  courtName: string | null;
  courtSection: string | null;
  judgeSyndic: string | null;
  registrar: string | null;
  matchedCaseId: string | null;
  matchedCompanyId: string | null;
  confidence: number;
  procedureType: string | null;
  openingDate: string | null;
  nextHearingDate: string | null;
  claimsDeadline: string | null;
  contestationsDeadline: string | null;
  parties: ExtractedParty[];
  extractedText: string | null;
  isAiExtracted: boolean;
}

export interface ExtractedParty {
  role: string;
  name: string;
  fiscalId: string | null;
  claimAmount: number | null;
}
