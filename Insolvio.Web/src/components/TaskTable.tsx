import type {CompanyTask} from "../types";
import {formatDate} from "@/lib/dateUtils";
import {Badge} from "@/components/ui/badge";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import {Button} from "@/components/ui/button";
import {Pencil, Trash2} from "lucide-react";

const STATUS_VARIANT: Record<
  CompanyTask["status"],
  "default" | "secondary" | "success" | "warning" | "destructive" | "outline"
> = {
  open: "default",
  blocked: "warning",
  done: "success",
};

const STATUS_LABEL: Record<CompanyTask["status"], string> = {
  open: "Open",
  blocked: "Blocked",
  done: "Done",
};

interface TaskTableProps {
  tasks: CompanyTask[];
  companyNameById?: (id: string) => string;
  assigneeNameById?: (id: string) => string;
  onEdit?: (task: CompanyTask) => void;
  onDelete?: (task: CompanyTask) => void;
  onOpenCompany?: (companyId: string) => void;
  onTaskClick?: (task: CompanyTask) => void;
}

// const DESC_MAX = 80;

/** Split free-text labels into trimmed non-empty parts for pill display. */
function parseLabels(labels: string | undefined): string[] {
  if (!labels?.trim()) return [];
  return labels
    .split(/[,;]/)
    .map((s) => s.trim())
    .filter(Boolean);
}

export default function TaskTable({
  tasks,
  companyNameById,
  assigneeNameById,
  onEdit,
  onDelete,
  onOpenCompany,
  onTaskClick,
}: TaskTableProps) {
  const showCompany = Boolean(companyNameById ?? onOpenCompany);
  const showAssignee = Boolean(assigneeNameById);
  const showActions = Boolean(onEdit ?? onDelete);
  const colCount =
    3 + (showCompany ? 1 : 0) + (showAssignee ? 1 : 0) + (showActions ? 1 : 0);

  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead className="ps-4 max-w-0 w-2/5">Task</TableHead>
          {showCompany && <TableHead className="ps-4">Company</TableHead>}
          {showAssignee && <TableHead className="ps-4">Assignee</TableHead>}
          <TableHead className="ps-4">Status</TableHead>
          <TableHead className="ps-4">Deadline</TableHead>
          {showActions && (
            <TableHead className="ps-4 w-[100px]">Actions</TableHead>
          )}
        </TableRow>
      </TableHeader>
      <TableBody>
        {tasks.length === 0 ? (
          <TableRow>
            <TableCell
              colSpan={colCount}
              className="h-24 text-center text-muted-foreground"
            >
              No tasks.
            </TableCell>
          </TableRow>
        ) : (
          tasks.map((task) => (
            <TableRow key={task.id}>
              <TableCell
                className={
                  onTaskClick
                    ? "ps-4 max-w-0 w-2/5 cursor-pointer hover:bg-muted/50 focus:outline-none focus:ring-2 focus:ring-ring focus:ring-inset"
                    : "ps-4 max-w-0 w-2/5"
                }
                onClick={onTaskClick ? () => onTaskClick(task) : undefined}
                role={onTaskClick ? "button" : undefined}
                tabIndex={onTaskClick ? 0 : undefined}
                onKeyDown={
                  onTaskClick
                    ? (e) => {
                        if (e.key === "Enter" || e.key === " ") {
                          e.preventDefault();
                          onTaskClick(task);
                        }
                      }
                    : undefined
                }
              >
                <div>
                  <div className="flex flex-wrap items-center gap-x-4 gap-y-1">
                    <span className="font-medium truncate">
                      {task.title || "Untitled"}
                    </span>
                    {parseLabels(task.labels).map((label) => (
                      <Badge
                        key={label}
                        variant="secondary"
                        className="shrink-0 text-xs font-normal"
                      >
                        {label}
                      </Badge>
                    ))}
                  </div>
                  {/* {task.description && (
                    <p className="text-xs text-muted-foreground truncate max-w-md mt-0.5">
                      {task.description.length > DESC_MAX
                        ? `${task.description.slice(0, DESC_MAX)}…`
                        : task.description}
                    </p>
                  )} */}
                </div>
              </TableCell>
              {showCompany && (
                <TableCell className="ps-4">
                  {companyNameById ? (
                    <span className="text-sm text-muted-foreground">
                      {companyNameById(task.companyId)}
                    </span>
                  ) : onOpenCompany ? (
                    <Button
                      variant="link"
                      size="sm"
                      className="h-auto p-0 text-xs"
                      onClick={() => onOpenCompany(task.companyId)}
                    >
                      Open company
                    </Button>
                  ) : null}
                </TableCell>
              )}
              {showAssignee && (
                <TableCell className="ps-4 text-sm text-muted-foreground">
                  {task.assignedTo && assigneeNameById
                    ? assigneeNameById(task.assignedTo)
                    : "—"}
                </TableCell>
              )}
              <TableCell className="ps-4">
                <Badge variant={STATUS_VARIANT[task.status]}>
                  {STATUS_LABEL[task.status]}
                </Badge>
              </TableCell>
              <TableCell className="ps-4 text-sm text-muted-foreground">
                {task.deadline ? formatDate(task.deadline) : "—"}
              </TableCell>
              {showActions && (
                <TableCell className="ps-4">
                  <div className="flex items-center gap-1">
                    {onEdit && (
                      <Button
                        variant="ghost"
                        size="icon"
                        className="h-8 w-8"
                        onClick={() => onEdit(task)}
                        title="Edit"
                      >
                        <Pencil className="h-4 w-4" />
                      </Button>
                    )}
                    {onDelete && (
                      <Button
                        variant="ghost"
                        size="icon"
                        className="h-8 w-8 text-destructive hover:text-destructive"
                        onClick={() => onDelete(task)}
                        title="Delete"
                      >
                        <Trash2 className="h-4 w-4" />
                      </Button>
                    )}
                  </div>
                </TableCell>
              )}
            </TableRow>
          ))
        )}
      </TableBody>
    </Table>
  );
}
