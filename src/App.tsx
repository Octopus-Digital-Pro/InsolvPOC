import {useState} from "react";
import Header from "./components/Header";
import LoginScreen from "./components/LoginScreen";
import UploadModal from "./components/UploadModal";
import CompaniesSidebar from "./components/CompaniesSidebar";
import ErrorAlert from "./components/ErrorAlert";
import CompanyDetailView from "./components/CompanyDetailView";
import CaseDetail from "./components/CaseDetail";
import ProcessingOverlay from "./components/ProcessingOverlay";
import AttachToCompanyStep from "./components/AttachToCompanyStep";
import {useCases} from "./hooks/useCases";
import {useCompanies} from "./hooks/useCompanies";
import {useTasks} from "./hooks/useTasks";
import {processFile} from "./services/fileProcessor";
import {extractContractInfo} from "./services/openai";
import {getBestMatchingCompany} from "./services/companyMatch";
import {
  getCurrentUser,
  setCurrentUser,
  clearCurrentUser,
} from "./services/storage";
import type {Company, ContractCase, User} from "./types";
import TaskTable from "./components/TaskTable";
import TaskFormModal from "./components/TaskFormModal";
import CompanyCard from "./components/CompanyCard";
import FloatingUploadCTA from "./components/FloatingUploadCTA";

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
  const {companies, addCompany, updateCompany} = useCompanies();
  const {myTasks, getByCompany, addTask, updateTask, deleteTask} =
    useTasks(user.id);

  const [isProcessing, setIsProcessing] = useState(false);
  const [processingFileName, setProcessingFileName] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [sidebarOpen, setSidebarOpen] = useState(true);
  const [pendingCase, setPendingCase] = useState<ContractCase | null>(null);
  const [pendingExtraction, setPendingExtraction] = useState<Awaited<
    ReturnType<typeof extractContractInfo>
  > | null>(null);
  const [showAddCompany, setShowAddCompany] = useState(false);
  const [newCompanyName, setNewCompanyName] = useState("");
  const [newCompanyCuiRo, setNewCompanyCuiRo] = useState("");
  const [newCompanyAddress, setNewCompanyAddress] = useState("");
  const [addCompanyError, setAddCompanyError] = useState<string | null>(null);
  const [selectedCompanyId, setSelectedCompanyId] = useState<string | null>(
    null,
  );
  const [suggestedCompanyId, setSuggestedCompanyId] = useState<string | null>(
    null,
  );
  const [draftCase, setDraftCase] = useState<ContractCase | null>(null);
  const [uploadModalOpen, setUploadModalOpen] = useState(false);
  const [selectedTaskId, setSelectedTaskId] = useState<string | null>(null);

  const handleFileAccepted = async (file: File) => {
    setError(null);
    setIsProcessing(true);
    setProcessingFileName(file.name);
    setActiveCaseId(null);
    setPendingCase(null);
    setPendingExtraction(null);
    setSuggestedCompanyId(null);

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

      const matchedCompany = getBestMatchingCompany(companies, result);
      setSuggestedCompanyId(matchedCompany?.id ?? null);
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
    setDraftCase({...pendingCase, companyId});
    setPendingCase(null);
    setPendingExtraction(null);
    setSuggestedCompanyId(null);
  };

  const handleAttachCancel = () => {
    setPendingCase(null);
    setPendingExtraction(null);
    setSuggestedCompanyId(null);
  };

  const handleSaveDraft = async () => {
    if (!draftCase) return;
    await addCase(draftCase);
    setDraftCase(null);
  };

  const handleDiscardDraft = () => {
    setDraftCase(null);
  };

  const handleDraftUpdate = (id: string, updates: Partial<ContractCase>) => {
    setDraftCase((prev) =>
      prev && prev.id === id ? {...prev, ...updates} : prev,
    );
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
  const isAssignedToMe = (company: Company): boolean =>
    company.assignedTo === user.id;
  const myCompanies = companies.filter(isAssignedToMe);
  const otherCompanies = companies.filter((c) => !isAssignedToMe(c));
  const caseCountByCompanyId = new Map<string, number>();
  for (const c of cases) {
    if (c.companyId) {
      caseCountByCompanyId.set(
        c.companyId,
        (caseCountByCompanyId.get(c.companyId) ?? 0) + 1,
      );
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
      : (companyById.get(selectedCompanyId) ?? null);

  const handleUploadFileAccepted = (file: File) => {
    setUploadModalOpen(false);
    handleFileAccepted(file);
  };

  const closeUploadModal = () => {
    setUploadModalOpen(false);
    setError(null);
  };

  return (
    <div className="flex h-screen flex-col bg-background">
      <Header user={user} onLogout={onLogout} />

      <UploadModal
        open={uploadModalOpen}
        onClose={closeUploadModal}
        onFileAccepted={handleUploadFileAccepted}
        isProcessing={isProcessing}
        error={error}
      />

      <FloatingUploadCTA
        onUploadClick={() => setUploadModalOpen(true)}
        onFileAccepted={handleUploadFileAccepted}
        isProcessing={isProcessing}
      />

      <div className="flex flex-1 overflow-hidden">
        <aside
          className={`
            flex flex-col border-r border-sidebar-border bg-sidebar transition-all duration-200
            ${sidebarOpen ? "w-80" : "w-0"}
          `}
        >
          <CompaniesSidebar
            open={sidebarOpen}
            companies={companies}
            onUploadClick={() => setUploadModalOpen(true)}
            showAddCompany={showAddCompany}
            onToggleAddCompany={(show) => {
              setShowAddCompany(show);
              if (!show) setAddCompanyError(null);
            }}
            newCompanyName={newCompanyName}
            newCompanyCuiRo={newCompanyCuiRo}
            newCompanyAddress={newCompanyAddress}
            addCompanyError={addCompanyError}
            onNewCompanyNameChange={setNewCompanyName}
            onNewCompanyCuiRoChange={setNewCompanyCuiRo}
            onNewCompanyAddressChange={setNewCompanyAddress}
            onSaveNewCompany={handleSaveNewCompany}
          />
        </aside>

        {/* Sidebar toggle */}
        <button
          onClick={() => setSidebarOpen(!sidebarOpen)}
          className="flex items-center border-r border-border bg-background px-1 text-muted-foreground hover:text-foreground"
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
              <div className="h-8 w-8 animate-spin rounded-full border-4 border-border border-t-primary" />
            </div>
          ) : isProcessing ? (
            <ProcessingOverlay fileName={processingFileName} />
          ) : pendingCase && pendingExtraction ? (
            <AttachToCompanyStep
              draftCase={pendingCase}
              extractionResult={pendingExtraction}
              companies={companies}
              suggestedCompanyId={suggestedCompanyId}
              onCreateCompany={addCompany}
              onAttach={handleAttachToCompany}
              onCancel={handleAttachCancel}
              createdBy={user.name}
            />
          ) : draftCase ? (
            <CaseDetail
              contractCase={draftCase}
              company={
                draftCase.companyId
                  ? companyById.get(draftCase.companyId)
                  : undefined
              }
              companies={companies}
              currentUserName={user.name}
              onUpdate={handleDraftUpdate}
              onUpdateCompany={updateCompany}
              onDelete={handleDiscardDraft}
              onBack={handleDiscardDraft}
              isDraft
              onSave={handleSaveDraft}
            />
          ) : activeCase ? (
            <CaseDetail
              contractCase={activeCase}
              company={
                activeCase.companyId
                  ? companyById.get(activeCase.companyId)
                  : undefined
              }
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
              companyTasks={
                selectedCompany ? getByCompany(selectedCompany.id) : []
              }
              activeCaseId={activeCaseId}
              onSelectCase={(id) => setActiveCaseId(id)}
              onBack={() => setSelectedCompanyId(null)}
              onUpdateCompany={updateCompany}
              onUpdateCase={updateCase}
              onUploadClick={() => setUploadModalOpen(true)}
              onAddTask={(task) =>
                addTask({ ...task, assignedTo: task.assignedTo ?? user.id })
              }
              onUpdateTask={updateTask}
              onDeleteTask={deleteTask}
            />
          ) : (
            <div className="mx-auto max-w-3xl pt-12">
              {error && (
                <div className="mb-6">
                  <ErrorAlert
                    message={error}
                    title="Processing Error"
                    onDismiss={() => setError(null)}
                  />
                </div>
              )}
              <h2 className="text-xl font-semibold text-foreground">
                Dashboard
              </h2>
              <p className="mt-2 mb-4 text-sm text-muted-foreground">
                Tasks sorted by deadline (closest first).
              </p>
              <div className="rounded-xl border border-border bg-card overflow-hidden">
                <TaskTable
                  tasks={myTasks}
                  companyNameById={(id) => companyById.get(id)?.name ?? id}
                  onOpenCompany={(companyId) => handleCompanyClick(companyId)}
                  onTaskClick={(task) => setSelectedTaskId(task.id)}
                />
              </div>
              {selectedTaskId != null && (() => {
                const selectedTask = myTasks.find(
                  (t) => t.id === selectedTaskId,
                );
                if (!selectedTask) return null;
                return (
                  <TaskFormModal
                    open
                    onClose={() => setSelectedTaskId(null)}
                    companyId={selectedTask.companyId}
                    task={selectedTask}
                    mode="view"
                    companyName={companyById.get(selectedTask.companyId)?.name}
                    onSubmit={(payload, existingTask) => {
                      if (existingTask) {
                        updateTask(existingTask.id, payload);
                      }
                      setSelectedTaskId(null);
                    }}
                  />
                );
              })()}

              <section className="mt-8">
                <h3 className="mb-3 text-sm font-semibold uppercase tracking-wide text-muted-foreground">
                  Assigned to me ({myCompanies.length})
                </h3>
                {myCompanies.length === 0 ? (
                  <p className="py-4 text-sm text-muted-foreground">
                    No companies
                  </p>
                ) : (
                  <div className="grid gap-3 sm:grid-cols-1">
                    {myCompanies.map((company) => (
                      <CompanyCard
                        key={company.id}
                        company={company}
                        documentCount={
                          caseCountByCompanyId.get(company.id) ?? 0
                        }
                        isActive={selectedCompanyId === company.id}
                        onClick={() => handleCompanyClick(company.id)}
                      />
                    ))}
                  </div>
                )}
              </section>

              <section className="mt-8">
                <h3 className="mb-3 text-sm font-semibold uppercase tracking-wide text-muted-foreground">
                  Other
                </h3>
                {otherCompanies.length === 0 && noCompanyCount === 0 ? (
                  <p className="py-4 text-sm text-muted-foreground">
                    No companies
                  </p>
                ) : (
                  <div className="grid gap-3 sm:grid-cols-1">
                    {otherCompanies.map((company) => (
                      <CompanyCard
                        key={company.id}
                        company={company}
                        documentCount={
                          caseCountByCompanyId.get(company.id) ?? 0
                        }
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
            </div>
          )}
        </main>
      </div>
    </div>
  );
}

export default App;
