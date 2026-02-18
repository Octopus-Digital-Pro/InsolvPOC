import {useState} from "react";
import Header from "./components/Header";
import LoginScreen from "./components/LoginScreen";
import FileDropZone from "./components/FileDropZone";
import CompanyCard from "./components/CompanyCard";
import CompanyDetailView from "./components/CompanyDetailView";
import CaseDetail from "./components/CaseDetail";
import ProcessingOverlay from "./components/ProcessingOverlay";
import AttachToCompanyStep from "./components/AttachToCompanyStep";
import {useCases} from "./hooks/useCases";
import {useCompanies} from "./hooks/useCompanies";
import {processFile} from "./services/fileProcessor";
import {extractContractInfo} from "./services/openai";
import {
  getCurrentUser,
  setCurrentUser,
  clearCurrentUser,
} from "./services/storage";
import type {Company, ContractCase, User} from "./types";

function App() {
  const [currentUser, setUser] = useState<User | null>(() => getCurrentUser());

  const handleLogin = (user: User) => {
    setCurrentUser(user);
    setUser(user);
  };

  const handleLogout = () => {
    clearCurrentUser();
    setUser(null);
  };

  if (!currentUser) {
    return <LoginScreen onLogin={handleLogin} />;
  }

  return <MainApp user={currentUser} onLogout={handleLogout} />;
}

function MainApp({user, onLogout}: {user: User; onLogout: () => void}) {
  const {
    cases,
    activeCase,
    activeCaseId,
    setActiveCaseId,
    addCase,
    updateCase,
    deleteCase,
    loading,
  } = useCases();
  const { companies, addCompany, updateCompany } = useCompanies();

  const [isProcessing, setIsProcessing] = useState(false);
  const [processingFileName, setProcessingFileName] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [sidebarOpen, setSidebarOpen] = useState(true);
  const [pendingCase, setPendingCase] = useState<ContractCase | null>(null);
  const [pendingExtraction, setPendingExtraction] = useState<Awaited<ReturnType<typeof extractContractInfo>> | null>(null);
  const [showAddCompany, setShowAddCompany] = useState(false);
  const [newCompanyName, setNewCompanyName] = useState("");
  const [newCompanyCuiRo, setNewCompanyCuiRo] = useState("");
  const [newCompanyAddress, setNewCompanyAddress] = useState("");
  const [addCompanyError, setAddCompanyError] = useState<string | null>(null);
  const [selectedCompanyId, setSelectedCompanyId] = useState<string | null>(null);

  const handleFileAccepted = async (file: File) => {
    setError(null);
    setIsProcessing(true);
    setProcessingFileName(file.name);
    setActiveCaseId(null);
    setPendingCase(null);
    setPendingExtraction(null);

    try {
      const {images, fileName} = await processFile(file);
      const result = await extractContractInfo(images);

      const contractCase: ContractCase = {
        id: crypto.randomUUID(),
        title:
          result.contractTitleOrSubject !== "Not found"
            ? result.contractTitleOrSubject
            : result.beneficiary,
        sourceFileName: fileName,
        createdAt: new Date().toISOString(),
        createdBy: user.name,

        beneficiary: result.beneficiary,
        beneficiaryAddress: result.beneficiaryAddress,
        beneficiaryIdentifiers: result.beneficiaryIdentifiers,
        contractor: result.contractor,
        contractorAddress: result.contractorAddress,
        contractorIdentifiers: result.contractorIdentifiers,
        subcontractors: result.subcontractors,

        contractTitleOrSubject: result.contractTitleOrSubject,
        contractNumberOrReference: result.contractNumberOrReference,
        procurementProcedure: result.procurementProcedure,
        cpvCodes: result.cpvCodes,

        contractDate: result.contractDate,
        effectiveDate: result.effectiveDate,
        contractPeriod: result.contractPeriod,

        signatories: result.signatories,
        signingLocation: result.signingLocation,

        otherImportantClauses: result.otherImportantClauses,
        rawJson: result.rawJson,
      };

      setPendingCase(contractCase);
      setPendingExtraction(result);
    } catch (err) {
      const message =
        err instanceof Error ? err.message : "An unexpected error occurred";
      setError(message);
    } finally {
      setIsProcessing(false);
      setProcessingFileName("");
    }
  };

  const handleAttachToCompany = (companyId: string) => {
    if (!pendingCase) return;
    addCase({ ...pendingCase, companyId });
    setPendingCase(null);
    setPendingExtraction(null);
  };

  const handleAttachCancel = () => {
    setPendingCase(null);
    setPendingExtraction(null);
  };

  const handleSaveNewCompany = async () => {
    const name = newCompanyName.trim();
    if (!name) {
      setAddCompanyError("Name is required");
      return;
    }
    setAddCompanyError(null);
    try {
      await addCompany({
        id: crypto.randomUUID(),
        name,
        cuiRo: newCompanyCuiRo.trim(),
        address: newCompanyAddress.trim(),
        createdAt: new Date().toISOString(),
        createdBy: user.name,
      });
      setNewCompanyName("");
      setNewCompanyCuiRo("");
      setNewCompanyAddress("");
      setShowAddCompany(false);
    } catch (err) {
      setAddCompanyError(err instanceof Error ? err.message : "Failed to save");
    }
  };

  const companyById = new Map<string, Company>(companies.map((c) => [c.id, c]));
  const isAssignedToMe = (company: Company): boolean => company.assignedTo === user.id;
  const myCompanies = companies.filter(isAssignedToMe);
  const otherCompanies = companies.filter((c) => !isAssignedToMe(c));
  const caseCountByCompanyId = new Map<string, number>();
  for (const c of cases) {
    if (c.companyId) {
      caseCountByCompanyId.set(c.companyId, (caseCountByCompanyId.get(c.companyId) ?? 0) + 1);
    }
  }
  const noCompanyCases = cases.filter((c) => !c.companyId);
  const noCompanyCount = noCompanyCases.length;

  const handleCompanyClick = (companyId: string | "none") => {
    setSelectedCompanyId(companyId);
    setActiveCaseId(null);
  };

  const casesForSelectedCompany =
    selectedCompanyId === null
      ? []
      : selectedCompanyId === "none"
        ? noCompanyCases
        : cases.filter((c) => c.companyId === selectedCompanyId);
  const selectedCompany =
    selectedCompanyId === null || selectedCompanyId === "none"
      ? null
      : companyById.get(selectedCompanyId) ?? null;

  return (
    <div className="flex h-screen flex-col bg-gray-50">
      <Header user={user} onLogout={onLogout} />

      <div className="flex flex-1 overflow-hidden">
        {/* Sidebar */}
        <aside
          className={`
            flex flex-col border-r border-gray-200 bg-white transition-all duration-200
            ${sidebarOpen ? "w-80" : "w-0"}
          `}
        >
          {sidebarOpen && (
            <>
              <div className="flex items-center justify-between border-b border-gray-100 px-4 py-3">
                <h2 className="text-sm font-semibold text-gray-700">Companies</h2>
                <div className="flex items-center gap-2">
                  <span className="rounded-full bg-gray-100 px-2 py-0.5 text-xs text-gray-500">
                    {companies.length}
                  </span>
                  <button
                    onClick={() => { setActiveCaseId(null); setSelectedCompanyId(null); }}
                    title="Upload new document"
                    className="flex h-6 w-6 items-center justify-center rounded-md text-gray-400 hover:bg-blue-50 hover:text-blue-500 transition-colors"
                  >
                    <svg
                      className="h-4 w-4"
                      fill="none"
                      viewBox="0 0 24 24"
                      stroke="currentColor"
                      strokeWidth={2}
                    >
                      <path
                        strokeLinecap="round"
                        strokeLinejoin="round"
                        d="M12 4.5v15m7.5-7.5h-15"
                      />
                    </svg>
                  </button>
                </div>
              </div>

              {showAddCompany ? (
                <div className="border-b border-gray-100 p-3 space-y-2 bg-gray-50">
                  <h3 className="text-xs font-semibold uppercase tracking-wide text-gray-500">New company</h3>
                  <input
                    type="text"
                    value={newCompanyName}
                    onChange={(e) => setNewCompanyName(e.target.value)}
                    placeholder="Company name"
                    className="w-full rounded-md border border-gray-200 px-2 py-1.5 text-sm"
                  />
                  <input
                    type="text"
                    value={newCompanyCuiRo}
                    onChange={(e) => setNewCompanyCuiRo(e.target.value)}
                    placeholder="CUI / RO"
                    className="w-full rounded-md border border-gray-200 px-2 py-1.5 text-sm"
                  />
                  <textarea
                    value={newCompanyAddress}
                    onChange={(e) => setNewCompanyAddress(e.target.value)}
                    placeholder="Address"
                    rows={2}
                    className="w-full rounded-md border border-gray-200 px-2 py-1.5 text-sm"
                  />
                  {addCompanyError && <p className="text-xs text-red-600">{addCompanyError}</p>}
                  <div className="flex gap-2">
                    <button
                      type="button"
                      onClick={handleSaveNewCompany}
                      className="rounded-md bg-blue-500 px-3 py-1.5 text-xs font-medium text-white hover:bg-blue-600"
                    >
                      Save
                    </button>
                    <button
                      type="button"
                      onClick={() => { setShowAddCompany(false); setAddCompanyError(null); }}
                      className="rounded-md border border-gray-200 px-3 py-1.5 text-xs font-medium text-gray-600 hover:bg-gray-100"
                    >
                      Cancel
                    </button>
                  </div>
                </div>
              ) : (
                <div className="border-b border-gray-100 px-4 py-2">
                  <button
                    type="button"
                    onClick={() => setShowAddCompany(true)}
                    className="text-xs font-medium text-blue-600 hover:text-blue-700"
                  >
                    + Add company
                  </button>
                </div>
              )}

              <div className="flex-1 overflow-y-auto p-3 space-y-4 gap-y-10">
                {companies.length === 0 && noCompanyCount === 0 ? (
                  <p className="px-2 py-8 text-center text-xs text-gray-400">
                    No companies yet. Add a company or upload a document.
                  </p>
                ) : (
                  <>
                    <section className="mb-10">
                      <h3 className="mb-2 px-1 text-xs font-semibold uppercase tracking-wide text-gray-500">
                        Assigned to me
                      </h3>
                      {myCompanies.length === 0 ? (
                        <p className="px-2 py-2 text-xs text-gray-400">
                          No companies
                        </p>
                      ) : (
                        <div className="space-y-2">
                          {myCompanies.map((company) => (
                            <CompanyCard
                              key={company.id}
                              company={company}
                              documentCount={caseCountByCompanyId.get(company.id) ?? 0}
                              isActive={selectedCompanyId === company.id}
                              onClick={() => handleCompanyClick(company.id)}
                            />
                          ))}
                        </div>
                      )}
                    </section>
                    <section>
                      <h3 className="mb-2 px-1 text-xs font-semibold uppercase tracking-wide text-gray-500">
                        Other
                      </h3>
                      {otherCompanies.length === 0 && noCompanyCount === 0 ? (
                        <p className="px-2 py-2 text-xs text-gray-400">
                          No companies
                        </p>
                      ) : (
                        <div className="space-y-2">
                          {otherCompanies.map((company) => (
                            <CompanyCard
                              key={company.id}
                              company={company}
                              documentCount={caseCountByCompanyId.get(company.id) ?? 0}
                              isActive={selectedCompanyId === company.id}
                              onClick={() => handleCompanyClick(company.id)}
                            />
                          ))}
                          {noCompanyCount > 0 && (
                            <CompanyCard
                              company={null}
                              documentCount={noCompanyCount}
                              isActive={selectedCompanyId === "none"}
                              onClick={() => handleCompanyClick("none")}
                            />
                          )}
                        </div>
                      )}
                    </section>
                  </>
                )}
              </div>
            </>
          )}
        </aside>

        {/* Sidebar toggle */}
        <button
          onClick={() => setSidebarOpen(!sidebarOpen)}
          className="flex items-center border-r border-gray-200 bg-white px-1 text-gray-400 hover:text-gray-600"
          title={sidebarOpen ? "Collapse sidebar" : "Expand sidebar"}
        >
          <svg
            className={`h-4 w-4 transition-transform ${sidebarOpen ? "" : "rotate-180"}`}
            fill="none"
            viewBox="0 0 24 24"
            stroke="currentColor"
            strokeWidth={2}
          >
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              d="M15 19l-7-7 7-7"
            />
          </svg>
        </button>

        {/* Main content */}
        <main className="flex-1 overflow-y-auto p-8">
          {loading ? (
            <div className="flex h-full items-center justify-center">
              <div className="h-8 w-8 animate-spin rounded-full border-4 border-blue-200 border-t-blue-600" />
            </div>
          ) : isProcessing ? (
            <ProcessingOverlay fileName={processingFileName} />
          ) : pendingCase && pendingExtraction ? (
            <AttachToCompanyStep
              draftCase={pendingCase}
              extractionResult={pendingExtraction}
              companies={companies}
              onCreateCompany={addCompany}
              onAttach={handleAttachToCompany}
              onCancel={handleAttachCancel}
              createdBy={user.name}
            />
          ) : activeCase ? (
            <CaseDetail
              contractCase={activeCase}
              company={activeCase.companyId ? companyById.get(activeCase.companyId) : undefined}
              companies={companies}
              currentUserName={user.name}
              onUpdate={updateCase}
              onUpdateCompany={updateCompany}
              onDelete={deleteCase}
              onBack={() => setActiveCaseId(null)}
            />
          ) : selectedCompanyId !== null ? (
            <CompanyDetailView
              company={selectedCompany}
              cases={casesForSelectedCompany}
              activeCaseId={activeCaseId}
              onSelectCase={(id) => setActiveCaseId(id)}
              onBack={() => setSelectedCompanyId(null)}
              onUpdateCompany={updateCompany}
            />
          ) : (
            <div className="mx-auto max-w-xl pt-12">
              <div className="mb-8 text-center">
                <h2 className="text-xl font-semibold text-gray-800">
                  Upload a Contract
                </h2>
                <p className="mt-1 text-sm text-gray-500">
                  Upload a contract document to automatically extract key
                  information
                </p>
              </div>

              <FileDropZone
                onFileAccepted={handleFileAccepted}
                isProcessing={isProcessing}
              />

              {error && (
                <div className="mt-4 rounded-lg border border-red-200 bg-red-50 p-4">
                  <div className="flex items-start gap-3">
                    <svg
                      className="mt-0.5 h-5 w-5 shrink-0 text-red-400"
                      fill="none"
                      viewBox="0 0 24 24"
                      stroke="currentColor"
                      strokeWidth={2}
                    >
                      <path
                        strokeLinecap="round"
                        strokeLinejoin="round"
                        d="M12 9v3.75m9-.75a9 9 0 1 1-18 0 9 9 0 0 1 18 0Zm-9 3.75h.008v.008H12v-.008Z"
                      />
                    </svg>
                    <div>
                      <h4 className="text-sm font-medium text-red-800">
                        Processing Error
                      </h4>
                      <p className="mt-1 text-sm text-red-600">{error}</p>
                    </div>
                  </div>
                </div>
              )}
            </div>
          )}
        </main>
      </div>
    </div>
  );
}

export default App;
