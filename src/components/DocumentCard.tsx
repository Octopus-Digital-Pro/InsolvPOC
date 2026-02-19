import type {ContractCase} from "../types";
import {formatDateTime} from "@/lib/dateUtils";

interface DocumentCardProps {
  contractCase: ContractCase;
  isActive: boolean;
  onClick: () => void;
}

export default function DocumentCard({
  contractCase,
  isActive,
  onClick,
}: DocumentCardProps) {
  const creationDate = formatDateTime(contractCase.createdAt);
  const dueLabel =
    contractCase.contractDate && contractCase.contractDate !== "Not found"
      ? contractCase.contractDate
      : "—";

  return (
    <button
      onClick={onClick}
      type="button"
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
      <div className="mt-2 space-y-1 text-xs text-muted-foreground">
        <p>
          <span className="text-muted-foreground">Created:</span> {creationDate}
        </p>
        <p>
          <span className="text-muted-foreground">Contract date:</span> {dueLabel}
        </p>
      </div>
    </button>
  );
}
