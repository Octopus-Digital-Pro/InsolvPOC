import type {Company, ContractCase} from "../types";
import {USERS} from "../types";

interface CaseCardProps {
  contractCase: ContractCase;
  company?: Company | null;
  isActive: boolean;
  onClick: () => void;
}

export default function CaseCard({
  contractCase,
  company,
  isActive,
  onClick,
}: CaseCardProps) {
  const date = new Date(contractCase.createdAt);
  const formattedDate = date.toLocaleDateString("en-GB", {
    day: "numeric",
    month: "short",
    year: "numeric",
  });
  const formattedTime = date.toLocaleTimeString("en-GB", {
    hour: "2-digit",
    minute: "2-digit",
  });

  const subtitle = [contractCase.beneficiary, contractCase.contractor]
    .filter((v) => v && v !== "Not found")
    .join(" / ");

  const assigneeName = company?.assignedTo
    ? USERS.find((u) => u.id === company.assignedTo)?.name
    : null;

  return (
    <button
      type="button"
      onClick={onClick}
      className={`
        w-full text-left rounded-xl border px-4 py-3 transition-all duration-150
        ${isActive
          ? "bg-sidebar-accent border-sidebar-border shadow-sm text-sidebar-accent-foreground"
          : "bg-card border-border hover:bg-accent hover:border-border text-card-foreground"}
      `}
    >
      <h3
        className={`truncate text-sm font-semibold ${isActive ? "text-sidebar-primary" : "text-foreground"}`}
      >
        {contractCase.title || "Untitled"}
      </h3>
      {subtitle && (
        <p className="mt-0.5 truncate text-xs text-muted-foreground">{subtitle}</p>
      )}
      <div className="mt-1.5 flex flex-wrap items-center gap-x-2 gap-y-0.5 text-xs text-muted-foreground">
        <span>{formattedDate}, {formattedTime}</span>
        {contractCase.createdBy && (
          <>
            <span className="text-border">|</span>
            <span className="truncate">{contractCase.createdBy}</span>
          </>
        )}
        {assigneeName && (
          <>
            <span className="text-border">|</span>
            <span className="truncate">Assigned to {assigneeName}</span>
          </>
        )}
      </div>
    </button>
  );
}
