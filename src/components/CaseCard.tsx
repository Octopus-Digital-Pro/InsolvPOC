import type { ContractCase } from '../types';

interface CaseCardProps {
  contractCase: ContractCase;
  isActive: boolean;
  onClick: () => void;
}

export default function CaseCard({ contractCase, isActive, onClick }: CaseCardProps) {
  const date = new Date(contractCase.createdAt);
  const formattedDate = date.toLocaleDateString('en-GB', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
  });
  const formattedTime = date.toLocaleTimeString('en-GB', {
    hour: '2-digit',
    minute: '2-digit',
  });

  const subtitle = [contractCase.beneficiary, contractCase.contractor]
    .filter((v) => v && v !== 'Not found')
    .join(' / ');

  return (
    <button
      onClick={onClick}
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
        {contractCase.title || 'Untitled'}
      </h3>
      {subtitle && (
        <p className="mt-0.5 truncate text-xs text-gray-500">{subtitle}</p>
      )}
      <div className="mt-1.5 flex items-center gap-2 text-xs text-gray-400">
        <span>{formattedDate}, {formattedTime}</span>
        {contractCase.createdBy && (
          <>
            <span className="text-gray-300">|</span>
            <span className="truncate">{contractCase.createdBy}</span>
          </>
        )}
      </div>
    </button>
  );
}
