import type { Company, InsolvencyCase } from "../types";
import { USERS } from "../types";

interface CaseCardProps {
  insolvencyCase: InsolvencyCase;
  company?: Company | null;
  isActive: boolean;
  onClick: () => void;
}

export default function CaseCard({
  insolvencyCase,
  company,
  isActive,
  onClick,
}: CaseCardProps) {
  const date = new Date(insolvencyCase.createdAt);
  const formattedDate = date.toLocaleDateString("en-GB", {
    day: "numeric",
    month: "short",
    year: "numeric",
  });
  const formattedTime = date.toLocaleTimeString("en-GB", {
    hour: "2-digit",
    minute: "2-digit",
  });

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
        {insolvencyCase.caseNumber || "No number"} – {insolvencyCase.debtorName || "Unknown"}
      </h3>
      <p className="mt-0.5 truncate text-xs text-muted-foreground">
        {insolvencyCase.courtName || "—"}
      </p>
      <div className="mt-1.5 flex flex-wrap items-center gap-x-2 gap-y-0.5 text-xs text-muted-foreground">
        <span>{formattedDate}, {formattedTime}</span>
        {insolvencyCase.createdBy && (
          <>
            <span className="text-border">|</span>
            <span className="truncate">{insolvencyCase.createdBy}</span>
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
