import {useState} from "react";
import type {CompanyTask, CompanyTaskStatus} from "../types";
import {Button} from "@/components/ui/button";
import {DatePicker} from "@/components/ui/date-picker";
import {format} from "date-fns";
import {X} from "lucide-react";
import {formatDate} from "@/lib/dateUtils";

const STATUS_LABEL: Record<CompanyTaskStatus, string> = {
  open: "Open",
  blocked: "Blocked",
  done: "Done",
};

interface TaskFormModalProps {
  open: boolean;
  onClose: () => void;
  companyId: string;
  task: CompanyTask | null;
  onSubmit: (
    payload: {
      title: string;
      description: string;
      deadline: string;
      status: CompanyTaskStatus;
    },
    existingTask: CompanyTask | null,
  ) => void;
  /** When "view", task details are read-only with Edit/Close. Default "edit". */
  mode?: "view" | "edit";
  /** Optional company name to show in view mode. */
  companyName?: string;
}

const STATUS_OPTIONS: {value: CompanyTaskStatus; label: string}[] = [
  {value: "open", label: "Open"},
  {value: "blocked", label: "Blocked"},
  {value: "done", label: "Done"},
];

function getInitialFormState(task: CompanyTask | null) {
  if (!task) {
    return {
      title: "",
      description: "",
      deadline: undefined as Date | undefined,
      status: "open" as CompanyTaskStatus,
    };
  }
  return {
    title: task.title,
    description: task.description ?? "",
    deadline: task.deadline
      ? new Date(
          task.deadline.includes("T")
            ? task.deadline
            : `${task.deadline}T12:00:00`,
        )
      : undefined,
    status: task.status,
  };
}

function TaskFormContent({
  task,
  onClose,
  onSubmit,
  mode,
  companyName,
}: Pick<
  TaskFormModalProps,
  "task" | "onClose" | "onSubmit" | "mode" | "companyName"
>) {
  const initial = getInitialFormState(task);
  const [title, setTitle] = useState(initial.title);
  const [description, setDescription] = useState(initial.description);
  const [deadline, setDeadline] = useState<Date | undefined>(initial.deadline);
  const [status, setStatus] = useState<CompanyTaskStatus>(initial.status);
  const [isEditing, setIsEditing] = useState(mode !== "view");

  const isEdit = task != null;
  const showViewMode = mode === "view" && task != null && !isEditing;

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    const deadlineStr = deadline ? format(deadline, "yyyy-MM-dd") : "";
    onSubmit(
      {
        title: title.trim(),
        description: description.trim(),
        deadline: deadlineStr,
        status,
      },
      task,
    );
    onClose();
  };

  if (showViewMode) {
    return (
      <div
        className="w-2xl rounded-xl border border-border bg-card p-6 shadow-xl"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="mb-4 flex items-start justify-between">
          <h2
            id="task-form-modal-title"
            className="text-xl font-semibold text-card-foreground"
          >
            Task details
          </h2>
          <Button
            variant="ghost"
            size="icon"
            onClick={onClose}
            className="text-muted-foreground hover:bg-accent hover:text-accent-foreground"
            aria-label="Close"
          >
            <X className="h-5 w-5" />
          </Button>
        </div>
        <div className="space-y-4">
          <div>
            <p className="mb-1 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
              Title
            </p>
            <p className="text-sm text-foreground">
              {task.title || "Untitled"}
            </p>
          </div>
          {task.description && (
            <div>
              <p className="mb-1 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                Description
              </p>
              <p className="whitespace-pre-wrap text-sm text-foreground">
                {task.description}
              </p>
            </div>
          )}
          {companyName && (
            <div>
              <p className="mb-1 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                Company
              </p>
              <p className="text-sm text-foreground">{companyName}</p>
            </div>
          )}
          <div>
            <p className="mb-1 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
              Deadline
            </p>
            <p className="text-sm text-foreground">
              {task.deadline ? formatDate(task.deadline) : "—"}
            </p>
          </div>
          <div>
            <p className="mb-1 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
              Status
            </p>
            <p className="text-sm text-foreground">
              {STATUS_LABEL[task.status]}
            </p>
          </div>
          <div className="flex justify-end gap-2 pt-2">
            <Button type="button" variant="outline" onClick={onClose}>
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

  return (
    <div
      className="w-xl rounded-xl border border-border bg-card p-6 shadow-xl"
      onClick={(e) => e.stopPropagation()}
    >
      <div className="mb-4 flex items-start justify-between">
        <h2
          id="task-form-modal-title"
          className="text-xl font-semibold text-card-foreground"
        >
          {isEdit ? "Edit task" : "Create task"}
        </h2>
        <Button
          variant="ghost"
          size="icon"
          onClick={onClose}
          className="text-muted-foreground hover:bg-accent hover:text-accent-foreground"
          aria-label="Close"
        >
          <X className="h-5 w-5" />
        </Button>
      </div>

      <form onSubmit={handleSubmit} className="space-y-4">
        <div>
          <label
            htmlFor="task-title"
            className="mb-1 block text-xs font-semibold uppercase tracking-wide text-muted-foreground"
          >
            Title
          </label>
          <input
            id="task-title"
            type="text"
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm text-foreground"
            required
          />
        </div>
        <div>
          <label
            htmlFor="task-description"
            className="mb-1 block text-xs font-semibold uppercase tracking-wide text-muted-foreground"
          >
            Description
          </label>
          <textarea
            id="task-description"
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            rows={3}
            className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm text-foreground resize-none"
          />
        </div>
        <div>
          <label className="mb-1 block text-xs font-semibold uppercase tracking-wide text-muted-foreground">
            Deadline
          </label>
          <DatePicker
            date={deadline}
            onSelect={setDeadline}
            placeholder="Pick a date"
            className="min-w-0 w-full"
          />
        </div>
        <div>
          <label
            htmlFor="task-status"
            className="mb-1 block text-xs font-semibold uppercase tracking-wide text-muted-foreground"
          >
            Status
          </label>
          <select
            id="task-status"
            value={status}
            onChange={(e) => setStatus(e.target.value as CompanyTaskStatus)}
            className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm text-foreground"
          >
            {STATUS_OPTIONS.map((opt) => (
              <option key={opt.value} value={opt.value}>
                {opt.label}
              </option>
            ))}
          </select>
        </div>
        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="outline" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit">{isEdit ? "Save" : "Create"}</Button>
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
        onClose={onClose}
        onSubmit={onSubmit}
        mode={mode}
        companyName={companyName}
      />
    </div>
  );
}
