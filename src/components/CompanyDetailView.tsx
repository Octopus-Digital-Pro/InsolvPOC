import type {Company, ContractCase} from "../types";
import {USERS, type User} from "../types";
import DocumentCard from "./DocumentCard";
import BackButton from "@/components/ui/BackButton";
import AssigneeDropdown from "@/components/molecules/AssigneeDropdown";
import UserSelect from "@/components/molecules/UserSelect";
import {Button} from "@/components/ui/button";
import {Upload} from "lucide-react";

interface CompanyDetailViewProps {
  company: Company | null;
  cases: ContractCase[];
  activeCaseId: string | null;
  onSelectCase: (id: string) => void;
  onBack: () => void;
  onUpdateCompany?: (id: string, updates: Partial<Company>) => void;
  onUpdateCase?: (id: string, updates: Partial<ContractCase>) => void;
  onUploadClick?: () => void;
}

export default function CompanyDetailView({
  company,
  cases,
  activeCaseId,
  onSelectCase,
  onBack,
  onUpdateCompany,
  onUpdateCase,
  onUploadClick,
}: CompanyDetailViewProps) {
  const sortedCases = [...cases].sort(
    (a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime(),
  );
  /** Case used for due date / notification (active when selected, else first). */
  const focusedCase =
    activeCaseId != null
      ? sortedCases.find((c) => c.id === activeCaseId)
      : (sortedCases[0] ?? null);
  const dueDateDisplay = focusedCase?.contractDate;
  const alertAt = focusedCase?.alertAt;
  const handleSetAlert = (iso: string | undefined) => {
    if (focusedCase && onUpdateCase) {
      onUpdateCase(focusedCase.id, {alertAt: iso ?? undefined});
    }
  };

  const selectedUser: User | null =
    company?.assignedTo != null
      ? (USERS.find((u) => u.id === company.assignedTo) ?? null)
      : null;

  const handleSelectAssignee = (userId: string | null) => {
    if (company && onUpdateCompany) {
      onUpdateCompany(company.id, {assignedTo: userId ?? undefined});
    }
  };

  return (
    <div className="mx-auto max-w-3xl pb-12 ">
      <BackButton
        className="cursor-pointer flex flex-row items-center gap-2 mb-2"
        onClick={onBack}
      >
        Back to home
      </BackButton>

      <div className="mb-6 rounded-xl border border-border bg-card p-4">
        <h1 className="text-xl font-bold text-card-foreground">
          {company ? company.name : "No company"}
        </h1>
        {company && (
          <>
            {company.cuiRo && (
              <p className="mt-1 text-sm text-muted-foreground">
                CUI/RO: {company.cuiRo}
              </p>
            )}
            {company.address && (
              <p className="mt-0.5 text-sm text-muted-foreground">{company.address}</p>
            )}
          </>
        )}
        <div className="my-8 flex flex-row flex-wrap gap-x-6 gap-y-2 justify-between items-center">
          <div className="flex flex-row items-center gap-6">
            <UserSelect
              users={USERS}
              value={selectedUser}
              onChange={handleSelectAssignee}
            />
            <AssigneeDropdown
              dueDateDisplay={dueDateDisplay}
              alertAt={alertAt}
              onSetAlert={handleSetAlert}
            />
          </div>
        </div>
      </div>

      <div className="mb-3 flex items-center justify-between">
        <h2 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
          Documents ({sortedCases.length})
        </h2>
        {onUploadClick && (
          <div className="border-b border-border px-4 py-2">
            <Button
              variant="ghost"
              size="sm"
              onClick={onUploadClick}
              title="Upload contract"
              className="gap-1.5 text-muted-foreground hover:text-primary hover:bg-accent"
            >
              <Upload className="h-4 w-4 shrink-0" />
              <span>+ Upload contract</span>
            </Button>
          </div>
        )}
      </div>
      {sortedCases.length === 0 ? (
        <p className="py-6 text-sm text-muted-foreground">No documents attached.</p>
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
