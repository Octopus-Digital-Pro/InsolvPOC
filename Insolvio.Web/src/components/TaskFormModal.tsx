import {useState} from "react";
import type {CompanyTask, CompanyTaskStatus, User} from "../types";
import {Button} from "@/components/ui/button";
import {DatePicker} from "@/components/ui/date-picker";
import {format} from "date-fns";
import {X, Clock} from "lucide-react";
import {formatDate} from "@/lib/dateUtils";
import UserSelect from "@/components/molecules/UserSelect";
import {Badge} from "@/components/ui/badge";

const STATUS_LABEL: Record<CompanyTaskStatus, string> = {
  open: "Open",
  blocked: "Blocked",
  done: "Done",
};

const STATUS_COLOR: Record<CompanyTaskStatus, string> = {
  open: "bg-blue-500/10 text-blue-600",
  blocked: "bg-amber-500/10 text-amber-600",
  done: "bg-emerald-500/10 text-emerald-600",
};

/** Shared label class for the auto/1fr grid layout. */
const FIELD_LABEL =
  "text-xs font-semibold uppercase tracking-wide text-muted-foreground whitespace-nowrap";

export type TaskFormPayload = {
  title: string;
  description: string;
  labels: string;
  deadline: string;
  status: CompanyTaskStatus;
  assignedTo?: string;
};

interface TaskFormModalProps {
  open: boolean;
  onClose: () => void;
  companyId: string;
  task: CompanyTask | null;
  onSubmit: (payload: TaskFormPayload, existingTask: CompanyTask | null) => void;
  /** When "view", task details are read-only with Edit/Close. Default "edit". */
  mode?: "view" | "edit";
  /** Optional company name to show in view mode. */
  companyName?: string;
  /** Users list for assignee resolution and selector. */
  users: User[];
}

const STATUS_OPTIONS: {value: CompanyTaskStatus; label: string}[] = [
  {value: "open", label: "Open"},
  {value: "blocked", label: "Blocked"},
  {value: "done", label: "Done"},
];

function parseLabels(labels: string | undefined): string[] {
  if (!labels?.trim()) return [];
  return labels
    .split(/[,;]/)
    .map((s) => s.trim())
    .filter(Boolean);
}

function getInitialFormState(task: CompanyTask | null) {
  if (!task) {
    return {
      title: "",
      description: "",
      labels: "",
      deadline: undefined as Date | undefined,
      status: "open" as CompanyTaskStatus,
      assignedTo: null as string | null,
    };
  }
  return {
    title: task.title,
    description: task.description ?? "",
    labels: task.labels ?? "",
    deadline: task.deadline
      ? new Date(
          task.deadline.includes("T")
            ? task.deadline
            : `${task.deadline}T12:00:00`,
        )
      : undefined,
    status: task.status,
    assignedTo: task.assignedTo ?? null,
  };
}

function TaskFormContent({
  task,
  users,
  onClose,
  onSubmit,
  mode,
  companyName,
}: Pick<
  TaskFormModalProps,
  "task" | "users" | "onClose" | "onSubmit" | "mode" | "companyName"
>) {
  const initial = getInitialFormState(task);
  const [title, setTitle] = useState(initial.title);
  const [description, setDescription] = useState(initial.description);
  const [labels, setLabels] = useState(initial.labels);
  const [deadline, setDeadline] = useState<Date | undefined>(initial.deadline);
  const [status, setStatus] = useState<CompanyTaskStatus>(initial.status);
  const [assignedTo, setAssignedTo] = useState<string | null>(initial.assignedTo);
  const [isEditing, setIsEditing] = useState(mode !== "view");

  // Accumulates people who were assigned but then replaced during this session.
  // Starts empty — the initial assignee only appears here after the user picks
  // someone else, keeping the list truthful and chronologically ordered.
  const [previousAssignees, setPreviousAssignees] = useState<
    {name: string; changedAt: string}[]
  >([]);

  const handleAssigneeChange = (userId: string | null) => {
    if (assignedTo) {
      const prev = users.find((u) => u.id === assignedTo);
      if (prev) {
        setPreviousAssignees((h) => [
          ...h,
          {name: prev.name, changedAt: new Date().toISOString()},
        ]);
      }
    }
    setAssignedTo(userId);
  };

  const isEdit = task != null;
  const showViewMode = mode === "view" && task != null && !isEditing;
  const assigneeUser = task?.assignedTo
    ? users.find((u) => u.id === task.assignedTo)
    : null;

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    const deadlineStr = deadline ? format(deadline, "yyyy-MM-dd") : "";
    onSubmit(
      {
        title: title.trim(),
        description: description.trim(),
        labels: labels.trim(),
        deadline: deadlineStr,
        status,
        assignedTo: assignedTo ?? undefined,
      },
      task,
    );
    onClose();
  };

  /* --- View-only mode -------------------------------------------- */
  if (showViewMode) {
    return (
      <div
        className="w-full max-w-xl rounded-xl border border-border bg-card shadow-xl"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="flex items-center justify-between border-b border-border px-6 py-4">
          <h2
            id="task-form-modal-title"
            className="text-base font-semibold text-card-foreground"
          >
            Task Details
          </h2>
          <Button
            variant="ghost"
            size="icon"
            onClick={onClose}
            className="text-muted-foreground hover:bg-accent hover:text-accent-foreground"
            aria-label="Close"
          >
            <X className="h-4 w-4" />
          </Button>
        </div>

        <div className="px-6 py-5 space-y-4">
          <div className="space-y-1.5">
            <p className={FIELD_LABEL}>Title</p>
            <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
              <span className="text-sm font-medium text-foreground">
                {task.title || "Untitled"}
              </span>
              {parseLabels(task.labels).map((label) => (
                <Badge key={label} variant="secondary" className="text-xs font-normal">
                  {label}
                </Badge>
              ))}
            </div>
          </div>

          {task.description && (
            <div className="space-y-1.5">
              <p className={FIELD_LABEL}>Description</p>
              <p className="whitespace-pre-wrap text-sm text-foreground leading-relaxed">
                {task.description}
              </p>
            </div>
          )}

          <div className="grid grid-cols-2 gap-4">
            {companyName && (
              <div className="space-y-1.5">
                <p className={FIELD_LABEL}>Company</p>
                <p className="text-sm text-foreground">{companyName}</p>
              </div>
            )}
            <div className="space-y-1.5">
              <p className={FIELD_LABEL}>Deadline</p>
              <p className="text-sm text-foreground">
                {task.deadline ? formatDate(task.deadline) : "—"}
              </p>
            </div>
            <div className="space-y-1.5">
              <p className={FIELD_LABEL}>Status</p>
              <span
                className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${STATUS_COLOR[task.status]}`}
              >
                {STATUS_LABEL[task.status]}
              </span>
            </div>
            <div className="space-y-1.5">
              <p className={FIELD_LABEL}>Assignee</p>
              {assigneeUser ? (
                <div className="flex items-center gap-1.5">
                  <img
                    src={assigneeUser.avatar}
                    alt=""
                    className="h-5 w-5 rounded-full object-cover"
                  />
                  <span className="text-sm text-foreground">{assigneeUser.name}</span>
                </div>
              ) : (
                <p className="text-sm text-muted-foreground">—</p>
              )}
            </div>
          </div>

          <div className="flex justify-end gap-2 pt-1 border-t border-border">
            <Button type="button" variant="ghost" onClick={onClose}>
              Close
            </Button>
            <Button type="button" onClick={() => setIsEditing(true)}>
              Edit
            </Button>
          </div>
        </div>
      </div>
    );
  }

  /* --- Edit / Create mode ----------------------------------------- */
  return (
    <div
      className="w-full max-w-xl rounded-xl border border-border bg-card shadow-xl"
      onClick={(e) => e.stopPropagation()}
    >
      <div className="flex items-center justify-between border-b border-border px-6 py-4">
        <div>
          <h2
            id="task-form-modal-title"
            className="text-base font-semibold text-card-foreground"
          >
            {isEdit ? "Edit Task" : "New Task"}
          </h2>
          {isEdit && (
            <p className="mt-0.5 text-[11px] text-muted-foreground">
              Update the details below and save.
            </p>
          )}
        </div>
        <Button
          variant="ghost"
          size="icon"
          onClick={onClose}
          className="text-muted-foreground hover:bg-accent hover:text-accent-foreground"
          aria-label="Close"
        >
          <X className="h-4 w-4" />
        </Button>
      </div>

      <form onSubmit={handleSubmit} className="px-6 py-5">
        {/*
          Two-column grid: left column (labels) auto-sizes to content,
          right column (inputs) stretches to fill remaining space with 1fr.
          Replaces the old stacked 100%-width layout with a clean label|field
          alignment — consistent, easy to scan, and visually balanced.
        */}
        <div
          className="grid items-center gap-x-5 gap-y-3"
          style={{gridTemplateColumns: "auto 1fr"}}
        >
          {/* Title */}
          <label htmlFor="task-title" className={FIELD_LABEL}>
            Title <span className="text-destructive">*</span>
          </label>
          <input
            id="task-title"
            type="text"
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            required
            className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm text-foreground focus:outline-none focus:ring-2 focus:ring-ring"
          />

          {/* Description */}
          <label
            htmlFor="task-description"
            className={`${FIELD_LABEL} self-start pt-2`}
          >
            Description
          </label>
          <textarea
            id="task-description"
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            rows={4}
            placeholder="Additional details about this task…"
            className="w-full resize-none rounded-md border border-input bg-background px-3 py-2 text-sm leading-relaxed text-foreground focus:outline-none focus:ring-2 focus:ring-ring"
          />

          {/* Labels */}
          <label
            htmlFor="task-labels"
            className={`${FIELD_LABEL} self-start pt-2`}
          >
            Labels
          </label>
          <div className="space-y-1">
            <input
              id="task-labels"
              type="text"
              value={labels}
              onChange={(e) => setLabels(e.target.value)}
              placeholder="e.g. urgent, review, Q1"
              className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm text-foreground focus:outline-none focus:ring-2 focus:ring-ring"
            />
            <p className="text-[11px] text-muted-foreground">
              Separate multiple labels with commas.
            </p>
          </div>

          {/* Deadline */}
          <label className={FIELD_LABEL}>Deadline</label>
          <DatePicker
            date={deadline}
            onSelect={setDeadline}
            placeholder="Pick a date"
            className="w-full"
          />

          {/* Status */}
          <label htmlFor="task-status" className={FIELD_LABEL}>
            Status
          </label>
          <select
            id="task-status"
            value={status}
            onChange={(e) => setStatus(e.target.value as CompanyTaskStatus)}
            className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm text-foreground focus:outline-none focus:ring-2 focus:ring-ring"
          >
            {STATUS_OPTIONS.map((opt) => (
              <option key={opt.value} value={opt.value}>
                {opt.label}
              </option>
            ))}
          </select>

          {/* Assignee — label in the grid left column, hideLabel on the
              component so it does not render a duplicate label element. */}
          <label className={`${FIELD_LABEL} self-start pt-2`}>
            Assignee
          </label>
          <div className="space-y-2">
            <UserSelect
              hideLabel
              users={users}
              value={
                assignedTo
                  ? users.find((u) => u.id === assignedTo) ?? null
                  : null
              }
              onChange={handleAssigneeChange}
              label="Assignee"
            />

            {/* Previous assignees — chronological dot-list, edit mode only */}
            {isEdit && previousAssignees.length > 0 && (
              <div className="rounded-md border border-border/60 bg-muted/30 px-3 py-2.5">
                <div className="mb-2 flex items-center gap-1.5">
                  <Clock className="h-3 w-3 text-muted-foreground" />
                  <span className="text-[10px] font-semibold uppercase tracking-wider text-muted-foreground">
                    Previously Assigned To
                  </span>
                </div>
                <div className="max-h-28 space-y-1.5 overflow-y-auto">
                  {previousAssignees.map((entry, i) => (
                    <div key={i} className="flex items-center gap-2">
                      <span className="h-1.5 w-1.5 shrink-0 rounded-full bg-muted-foreground/40" />
                      <span className="flex-1 text-[11px] text-muted-foreground">
                        {entry.name}
                      </span>
                      <span className="text-[10px] text-muted-foreground/50">
                        {new Date(entry.changedAt).toLocaleDateString(undefined, {
                          month: "short",
                          day: "numeric",
                        })}
                      </span>
                    </div>
                  ))}
                </div>
              </div>
            )}
          </div>
        </div>

        {/* Actions */}
        <div className="mt-5 flex justify-end gap-2 border-t border-border pt-4">
          <Button type="button" variant="ghost" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit">
            {isEdit ? "Save Changes" : "Create Task"}
          </Button>
        </div>
      </form>
    </div>
  );
}

export default function TaskFormModal({
  open,
  onClose,
  task,
  onSubmit,
  mode = "edit",
  companyName,
  users,
}: TaskFormModalProps) {
  if (!open) return null;

  return (
    <div
      className="fixed inset-0 z-10 flex items-center justify-center bg-black/50 p-4"
      onClick={onClose}
      role="dialog"
      aria-modal="true"
      aria-labelledby="task-form-modal-title"
    >
      <TaskFormContent
        key={task?.id ?? "new"}
        task={task}
        users={users}
        onClose={onClose}
        onSubmit={onSubmit}
        mode={mode}
        companyName={companyName}
      />
    </div>
  );
}
