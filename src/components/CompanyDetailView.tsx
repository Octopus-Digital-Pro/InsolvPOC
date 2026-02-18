import type { Company, ContractCase } from '../types';
import { USERS } from '../types';
import DocumentCard from './DocumentCard';

interface CompanyDetailViewProps {
  company: Company | null;
  cases: ContractCase[];
  activeCaseId: string | null;
  onSelectCase: (id: string) => void;
  onBack: () => void;
  onUpdateCompany?: (id: string, updates: Partial<Company>) => void;
}

export default function CompanyDetailView({
  company,
  cases,
  activeCaseId,
  onSelectCase,
  onBack,
  onUpdateCompany,
}: CompanyDetailViewProps) {
  const assigneeName = company?.assignedTo
    ? USERS.find((u) => u.id === company.assignedTo)?.name
    : null;

  const sortedCases = [...cases].sort(
    (a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()
  );

  return (
    <div className="mx-auto max-w-3xl pb-12">
      <button
        onClick={onBack}
        type="button"
        className="mb-4 flex items-center gap-1.5 text-sm text-gray-400 hover:text-blue-500 transition-colors"
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
            d="M15 19l-7-7 7-7"
          />
        </svg>
        Back to companies
      </button>

      <div className="mb-6 rounded-xl border border-gray-100 bg-white p-4">
        <h1 className="text-xl font-bold text-gray-900">
          {company ? company.name : 'No company'}
        </h1>
        {company && (
          <>
            {company.cuiRo && (
              <p className="mt-1 text-sm text-gray-600">
                CUI/RO: {company.cuiRo}
              </p>
            )}
            {company.address && (
              <p className="mt-0.5 text-sm text-gray-600">{company.address}</p>
            )}
          </>
        )}
        <div className="mt-3 flex items-center gap-2">
          <span className="text-xs font-semibold uppercase tracking-wide text-gray-400">
            Assigned to
          </span>
          {company && onUpdateCompany ? (
            <select
              value={company.assignedTo ?? ''}
              onChange={(e) =>
                onUpdateCompany(company.id, {
                  assignedTo: e.target.value || undefined,
                })
              }
              className="rounded-md border border-gray-200 px-2 py-1 text-sm text-gray-700"
            >
              <option value="">— Unassigned —</option>
              {USERS.map((u) => (
                <option key={u.id} value={u.id}>
                  {u.name}
                </option>
              ))}
            </select>
          ) : (
            <span className="text-sm text-gray-700">
              {assigneeName ?? '— Unassigned —'}
            </span>
          )}
        </div>
      </div>

      <h2 className="mb-3 text-xs font-semibold uppercase tracking-wide text-gray-500">
        Documents ({sortedCases.length})
      </h2>
      {sortedCases.length === 0 ? (
        <p className="py-6 text-sm text-gray-400">No documents attached.</p>
      ) : (
        <div className="space-y-2">
          {sortedCases.map((c) => (
            <DocumentCard
              key={c.id}
              contractCase={c}
              isActive={c.id === activeCaseId}
              onClick={() => onSelectCase(c.id)}
            />
          ))}
        </div>
      )}
    </div>
  );
}
