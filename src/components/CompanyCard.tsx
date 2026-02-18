import type { Company } from '../types';
import { USERS } from '../types';

interface CompanyCardProps {
  company: Company | null;
  documentCount: number;
  isActive: boolean;
  onClick: () => void;
}

export default function CompanyCard({
  company,
  documentCount,
  isActive,
  onClick,
}: CompanyCardProps) {
  const title = company ? company.name : 'No company';
  const subtitle = company
    ? [company.cuiRo, company.address].filter(Boolean).join(' · ') || undefined
    : 'Cases not linked to a company';

  const assigneeName = company?.assignedTo
    ? USERS.find((u) => u.id === company.assignedTo)?.name
    : null;

  return (
    <button
      onClick={onClick}
      type="button"
      className={`
        w-full text-left rounded-xl px-4 py-3 transition-all duration-150
        ${isActive
          ? 'bg-blue-50 border border-blue-200 shadow-sm'
          : 'bg-white border border-gray-100 hover:bg-gray-50 hover:border-gray-200'
        }
      `}
    >
      <h3
        className={`truncate text-sm font-semibold ${isActive ? 'text-blue-900' : 'text-gray-800'}`}
      >
        {title}
      </h3>
      {subtitle && (
        <p className="mt-0.5 truncate text-xs text-gray-500" title={subtitle}>
          {subtitle}
        </p>
      )}
      <div className="mt-1.5 flex flex-wrap items-center gap-x-2 gap-y-0.5 text-xs text-gray-400">
        <span>{documentCount} document{documentCount !== 1 ? 's' : ''}</span>
        {assigneeName && (
          <>
            <span className="text-gray-300">|</span>
            <span className="truncate">Assigned to {assigneeName}</span>
          </>
        )}
      </div>
    </button>
  );
}
