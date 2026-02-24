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
}

export interface TenantDto {
  id: string;
  name: string;
  domain: string | null;
  isActive: boolean;
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
}

export interface CaseDto {
  id: string;
  caseNumber: string;
  courtName: string | null;
  courtSection: string | null;
  judgeSyndic: string | null;
  debtorName: string;
  debtorCui: string | null;
  procedureType: string;
  stage: string;
  lawReference: string | null;
  practitionerName: string | null;
  practitionerRole: string | null;
  practitionerFiscalId: string | null;
  practitionerDecisionNo: string | null;
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
  phases: CasePhaseDto[] | null;
}

export interface CasePartyDto {
  id: string;
  caseId: string;
  companyId: string;
  companyName: string | null;
  role: string;
  roleDescription: string | null;
  claimAmountRon: number | null;
  claimAccepted: boolean | null;
  joinedDate: string | null;
  notes: string | null;
}

export interface CasePhaseDto {
  id: string;
  caseId: string;
  phaseType: string;
  status: string;
  sortOrder: number;
  startedOn: string | null;
  completedOn: string | null;
  dueDate: string | null;
  notes: string | null;
  courtDecisionRef: string | null;
  updatedByUserId: string | null;
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
  title: string;
  description: string | null;
  labels: string | null;
  deadline: string | null;
  status: "open" | "blocked" | "done";
  assignedToUserId: string | null;
  assignedToName: string | null;
  createdOn: string;
}

export interface DashboardDto {
  totalCases: number;
  openCases: number;
  totalCompanies: number;
  pendingTasks: number;
  overdueTasks: number;
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
  userId: string | null;
  userEmail: string | null;
  entityType: string | null;
  entityId: string | null;
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
  courtName: string | null;
  courtSection: string | null;
  judgeSyndic: string | null;
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
}

export interface ExtractedParty {
  role: string;
  name: string;
  fiscalId: string | null;
  claimAmount: number | null;
}
