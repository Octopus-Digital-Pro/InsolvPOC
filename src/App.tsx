import { useState } from 'react';
import Header from './components/Header';
import LoginScreen from './components/LoginScreen';
import FileDropZone from './components/FileDropZone';
import CaseCard from './components/CaseCard';
import CaseDetail from './components/CaseDetail';
import ProcessingOverlay from './components/ProcessingOverlay';
import { useCases } from './hooks/useCases';
import { processFile } from './services/fileProcessor';
import { extractContractInfo } from './services/openai';
import { getCurrentUser, setCurrentUser, clearCurrentUser } from './services/storage';
import type { ContractCase, User } from './types';

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

function MainApp({ user, onLogout }: { user: User; onLogout: () => void }) {
  const { cases, activeCase, activeCaseId, setActiveCaseId, addCase, updateCase, deleteCase } =
    useCases();

  const [isProcessing, setIsProcessing] = useState(false);
  const [processingFileName, setProcessingFileName] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [sidebarOpen, setSidebarOpen] = useState(true);

  const handleFileAccepted = async (file: File) => {
    setError(null);
    setIsProcessing(true);
    setProcessingFileName(file.name);
    setActiveCaseId(null);

    try {
      const { images, fileName } = await processFile(file);
      const result = await extractContractInfo(images);

      const contractCase: ContractCase = {
        id: crypto.randomUUID(),
        title: result.contractTitleOrSubject !== 'Not found'
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

      addCase(contractCase);
    } catch (err) {
      const message = err instanceof Error ? err.message : 'An unexpected error occurred';
      setError(message);
    } finally {
      setIsProcessing(false);
      setProcessingFileName('');
    }
  };

  return (
    <div className="flex h-screen flex-col bg-gray-50">
      <Header user={user} onLogout={onLogout} />

      <div className="flex flex-1 overflow-hidden">
        {/* Sidebar */}
        <aside
          className={`
            flex flex-col border-r border-gray-200 bg-white transition-all duration-200
            ${sidebarOpen ? 'w-80' : 'w-0'}
          `}
        >
          {sidebarOpen && (
            <>
              <div className="flex items-center justify-between border-b border-gray-100 px-4 py-3">
                <h2 className="text-sm font-semibold text-gray-700">Cases</h2>
                <div className="flex items-center gap-2">
                  <span className="rounded-full bg-gray-100 px-2 py-0.5 text-xs text-gray-500">
                    {cases.length}
                  </span>
                  <button
                    onClick={() => setActiveCaseId(null)}
                    title="Upload new document"
                    className="flex h-6 w-6 items-center justify-center rounded-md text-gray-400 hover:bg-blue-50 hover:text-blue-500 transition-colors"
                  >
                    <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                      <path strokeLinecap="round" strokeLinejoin="round" d="M12 4.5v15m7.5-7.5h-15" />
                    </svg>
                  </button>
                </div>
              </div>

              <div className="flex-1 overflow-y-auto p-3 space-y-2">
                {cases.length === 0 ? (
                  <p className="px-2 py-8 text-center text-xs text-gray-400">
                    No cases yet. Upload a document to get started.
                  </p>
                ) : (
                  cases.map((c) => (
                    <CaseCard
                      key={c.id}
                      contractCase={c}
                      isActive={c.id === activeCaseId}
                      onClick={() => setActiveCaseId(c.id)}
                    />
                  ))
                )}
              </div>
            </>
          )}
        </aside>

        {/* Sidebar toggle */}
        <button
          onClick={() => setSidebarOpen(!sidebarOpen)}
          className="flex items-center border-r border-gray-200 bg-white px-1 text-gray-400 hover:text-gray-600"
          title={sidebarOpen ? 'Collapse sidebar' : 'Expand sidebar'}
        >
          <svg
            className={`h-4 w-4 transition-transform ${sidebarOpen ? '' : 'rotate-180'}`}
            fill="none"
            viewBox="0 0 24 24"
            stroke="currentColor"
            strokeWidth={2}
          >
            <path strokeLinecap="round" strokeLinejoin="round" d="M15 19l-7-7 7-7" />
          </svg>
        </button>

        {/* Main content */}
        <main className="flex-1 overflow-y-auto p-8">
          {isProcessing ? (
            <ProcessingOverlay fileName={processingFileName} />
          ) : activeCase ? (
            <CaseDetail contractCase={activeCase} currentUserName={user.name} onUpdate={updateCase} onDelete={deleteCase} onBack={() => setActiveCaseId(null)} />
          ) : (
            <div className="mx-auto max-w-xl pt-12">
              <div className="mb-8 text-center">
                <h2 className="text-xl font-semibold text-gray-800">Upload a Contract</h2>
                <p className="mt-1 text-sm text-gray-500">
                  Upload a contract document to automatically extract key information
                </p>
              </div>

              <FileDropZone onFileAccepted={handleFileAccepted} isProcessing={isProcessing} />

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
                      <h4 className="text-sm font-medium text-red-800">Processing Error</h4>
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
