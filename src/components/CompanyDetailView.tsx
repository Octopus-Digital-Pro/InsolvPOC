import {useState} from "react";
import type {Company, CompanyTask, InsolvencyCase} from "../types";
import {USERS, type User} from "../types";
import type {CaseWithDocuments} from "../hooks/useCases";
import {aggregateDeadlines} from "../domain/insolvencyCase";
import {toTitleCase} from "@/lib/dateUtils";
import BackButton from "@/components/ui/BackButton";
import AssigneeDropdown from "@/components/molecules/AssigneeDropdown";
import UserSelect from "@/components/molecules/UserSelect";
import Section from "@/components/molecules/Section";
import DocumentCard from "./DocumentCard";
import {Button} from "@/components/ui/button";
import {Upload, Plus} from "lucide-react";
import TaskTable from "./TaskTable";
import TaskFormModal from "./TaskFormModal";

interface CompanyDetailViewProps {
  company: Company | null;
  cases: InsolvencyCase[];
  casesWithDocs: CaseWithDocuments[];
  companyTasks: CompanyTask[];
  activeCaseId: string | null;
  onSelectCase: (id: string) => void;
  onSelectDocument?: (caseId: string, documentId: string) => void;
  onBack: () => void;
  onUpdateCompany?: (id: string, updates: Partial<Company>) => void;
  onUpdateCase?: (id: string, updates: Partial<InsolvencyCase>) => void;
  onUploadClick?: () => void;
  onAddTask?: (task: CompanyTask) => void;
  onUpdateTask?: (id: string, updates: Partial<CompanyTask>) => void;
  onDeleteTask?: (id: string) => void;
}

export default function CompanyDetailView({
  company,
  casesWithDocs,
  companyTasks,
  onSelectCase,
  onSelectDocument,
  onBack,
  onUpdateCompany,
  onUploadClick,
  onAddTask,
  onUpdateTask,
  onDeleteTask,
}: CompanyDetailViewProps) {
  const [taskFormOpen, setTaskFormOpen] = useState(false);
  const [taskFormEditing, setTaskFormEditing] = useState<CompanyTask | null>(
    null,
  );

  const allDocuments = casesWithDocs.flatMap(({case: c, documents: docs}) =>
    docs.map((doc) => ({
      caseId: c.id,
      caseNumber: c.caseNumber,
      document: doc,
    })),
  );
  const deadlines = aggregateDeadlines(
    casesWithDocs.flatMap(({documents}) => documents),
  );

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
              <p className="mt-0.5 text-sm text-muted-foreground">
                {company.address}
              </p>
            )}
          </>
        )}
        <div className="mt-4 flex flex-row flex-wrap gap-x-6 gap-y-2 justify-between items-center">
          <div className="flex flex-row items-center gap-6">
            <UserSelect
              users={USERS}
              value={selectedUser}
              onChange={handleSelectAssignee}
            />
            <AssigneeDropdown dueDateDisplay={undefined} />
          </div>
        </div>
      </div>

      <Section title="Deadlines">
        {deadlines.length === 0 ? (
          <p className="text-sm text-muted-foreground">
            No deadlines extracted yet.
          </p>
        ) : (
          <ul className="space-y-2 text-sm">
            {deadlines.slice(0, 10).map((d, i) => (
              <li key={i} className="flex flex-wrap gap-x-2 gap-y-1">
                <span className="font-medium">
                  {toTitleCase(d.type.replace(/_/g, " "))}:
                </span>
                <span className="text-muted-foreground">
                  {d.date?.iso ?? d.date?.text ?? "—"}{" "}
                  {d.time ? ` ${d.time}` : ""}
                </span>
                {d.notes && (
                  <span className="text-muted-foreground">({d.notes})</span>
                )}
              </li>
            ))}
          </ul>
        )}
      </Section>

      <Section title="Documents">
        {allDocuments.length === 0 ? (
          <p className="text-sm text-muted-foreground">No documents yet.</p>
        ) : (
          <div className="space-y-2">
            {allDocuments.map(({caseId, document: doc}) => {
              const key = `${caseId}-${doc.id}`;
              return (
                <DocumentCard
                  key={key}
                  document={doc}
                  isActive={false}
                  onClick={() => {
                    if (onSelectDocument) {
                      onSelectDocument(caseId, doc.id);
                    } else {
                      onSelectCase(caseId);
                    }
                  }}
                />
              );
            })}
          </div>
        )}
        <div className="mb-3 flex items-center justify-between">
          {onUploadClick && (
            <div className="mt-2">
              <Button
                variant="ghost"
                size="sm"
                onClick={onUploadClick}
                title="Upload insolvency document"
                className="gap-1.5 text-muted-foreground hover:text-primary hover:bg-accent"
              >
                <Upload className="h-4 w-4 shrink-0" />
                <span>+ Upload document</span>
              </Button>
            </div>
          )}
        </div>
      </Section>

      <div className="mb-6">
        <div className="mb-3 flex items-center justify-between">
          <h2 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
            Tasks ({companyTasks.length})
          </h2>
          {company && onAddTask && (
            <Button
              variant="ghost"
              size="sm"
              onClick={() => {
                setTaskFormEditing(null);
                setTaskFormOpen(true);
              }}
              className="gap-1.5 text-muted-foreground hover:text-primary hover:bg-accent"
            >
              <Plus className="h-4 w-4 shrink-0" />
              <span>Create task</span>
            </Button>
          )}
        </div>
        <div className="rounded-xl border border-border bg-card overflow-hidden">
          <TaskTable
            tasks={companyTasks}
            onEdit={
              onUpdateTask
                ? (task) => {
                    setTaskFormEditing(task);
                    setTaskFormOpen(true);
                  }
                : undefined
            }
            onDelete={
              onDeleteTask
                ? (task) => {
                    if (window.confirm("Delete this task?")) {
                      onDeleteTask(task.id);
                    }
                  }
                : undefined
            }
          />
        </div>
      </div>

      {company && onAddTask && onUpdateTask && (
        <TaskFormModal
          open={taskFormOpen}
          onClose={() => {
            setTaskFormOpen(false);
            setTaskFormEditing(null);
          }}
          companyId={company.id}
          task={taskFormEditing}
          onSubmit={(payload, existingTask) => {
            if (existingTask) {
              onUpdateTask(existingTask.id, payload);
            } else {
              onAddTask({
                id: crypto.randomUUID(),
                companyId: company.id,
                ...payload,
              });
            }
          }}
        />
      )}

      {/* {sortedCases.length === 0 ? (
        <p className="py-6 text-sm text-muted-foreground">
          No insolvency cases yet.
        </p>
      ) : (
        <div className="space-y-2">
          {sortedCases.map((c) => (
            <CaseCard
              key={c.id}
              insolvencyCase={c}
              company={company}
              isActive={c.id === activeCaseId}
              onClick={() => onSelectCase(c.id)}
            />
          ))}
        </div>
      )} */}
    </div>
  );
}
