import type { ContractCase } from '../types';

interface DocumentCardProps {
  contractCase: ContractCase;
  isActive: boolean;
  onClick: () => void;
}

function formatDate(iso: string): string {
  const d = new Date(iso);
  return d.toLocaleDateString('en-GB', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
  });
}

function formatDateTime(iso: string): string {
  const d = new Date(iso);
  return (
    d.toLocaleDateString('en-GB', {
      day: 'numeric',
      month: 'short',
      year: 'numeric',
    }) +
    ', ' +
    d.toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' })
  );
}

export default function DocumentCard({
  contractCase,
  isActive,
  onClick,
}: DocumentCardProps) {
  const creationDate = formatDateTime(contractCase.createdAt);
  const dueLabel = contractCase.contractDate && contractCase.contractDate !== 'Not found'
    ? contractCase.contractDate
    : '—';
  const alertLabel = contractCase.alertAt
    ? formatDate(contractCase.alertAt)
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
        {contractCase.title || 'Untitled'}
      </h3>
      <div className="mt-2 space-y-1 text-xs text-gray-600">
        <p>
          <span className="text-gray-500">Created:</span> {creationDate}
        </p>
        <p>
          <span className="text-gray-500">Contract date:</span> {dueLabel}
        </p>
        {alertLabel !== null && (
          <p>
            <span className="text-gray-500">Alert:</span> {alertLabel}
          </p>
        )}
      </div>
    </button>
  );
}
