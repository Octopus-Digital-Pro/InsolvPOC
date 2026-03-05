import type {Company} from "../types";
import {USERS} from "../types";

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
  const title = company ? company.name : "No company";
  const subtitle = company
    ? [company.cuiRo, company.address].filter(Boolean).join(" · ") || undefined
    : "Cases not linked to a company";

  const assigneeName = company?.assignedTo
    ? USERS.find((u) => u.id === company.assignedTo)?.name
    : null;

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
        {title}
      </h3>
      {subtitle && (
        <p className="mt-0.5 truncate text-xs text-muted-foreground" title={subtitle}>
          {subtitle}
        </p>
      )}
      <div className="mt-1.5 flex flex-wrap items-center gap-x-2 gap-y-0.5 text-xs text-muted-foreground">
        <span>{documentCount} document{documentCount !== 1 ? "s" : ""}</span>
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
