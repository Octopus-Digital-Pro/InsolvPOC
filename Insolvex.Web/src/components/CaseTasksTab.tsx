import { useState, useEffect } from "react";
import { tasksApi } from "@/services/api/tasks";
import { usersApi } from "@/services/api";
import type { TaskDto, UserDto } from "@/services/api/types";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  ListChecks, Calendar as CalendarIcon, LayoutGrid,
  Clock, CheckCircle2, AlertTriangle, Ban,
} from "lucide-react";
import { format, isPast, isToday, addDays, startOfWeek, endOfWeek } from "date-fns";

type ViewMode = "list" | "kanban" | "calendar";

interface Props {
  caseId: string;
  tasks: TaskDto[];
  onRefresh: () => void;
}

const STATUS_COLUMNS = [
  { key: "open", label: "Open", icon: Clock, color: "text-blue-500" },
  { key: "inProgress", label: "In Progress", icon: AlertTriangle, color: "text-amber-500" },
  { key: "blocked", label: "Blocked", icon: Ban, color: "text-red-500" },
  { key: "done", label: "Done", icon: CheckCircle2, color: "text-green-500" },
] as const;

export default function CaseTasksTab({ caseId: _caseId, tasks, onRefresh }: Props) {
  const [view, setView] = useState<ViewMode>("list");
  const [updatingId, setUpdatingId] = useState<string | null>(null);
  const [users, setUsers] = useState<UserDto[]>([]);

  useEffect(() => {
    usersApi.getAll().then(r => setUsers(r.data)).catch(console.error);
  }, []);

  const handleStatusChange = async (taskId: string, newStatus: string) => {
    setUpdatingId(taskId);
    try {
      await tasksApi.update(taskId, { status: newStatus });
      onRefresh();
    } catch (e) {
      console.error(e);
    } finally {
      setUpdatingId(null);
    }
  };

  const handleAssign = async (taskId: string, userId: string | null) => {
    setUpdatingId(taskId);
    try {
      await tasksApi.update(taskId, { assignedToUserId: userId });
      onRefresh();
    } catch (e) {
      console.error(e);
    } finally {
      setUpdatingId(null);
    }
  };

  const overdue = tasks.filter(t => t.deadline && isPast(new Date(t.deadline)) && t.status !== "done");

  return (
    <div className="space-y-3">
  {/* Header + view toggle */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <h2 className="flex items-center gap-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
       <ListChecks className="h-3.5 w-3.5" /> Tasks ({tasks.length})
          </h2>
          {overdue.length > 0 && (
 <Badge variant="destructive" className="text-[10px]">
              {overdue.length} overdue
   </Badge>
      )}
        </div>
        <div className="flex items-center gap-1">
     {([
  { mode: "list" as const, icon: ListChecks, label: "List" },
        { mode: "kanban" as const, icon: LayoutGrid, label: "Board" },
   { mode: "calendar" as const, icon: CalendarIcon, label: "Calendar" },
       ]).map(v => (
      <button
     key={v.mode}
   onClick={() => setView(v.mode)}
    className={`rounded-md p-1.5 text-xs transition-colors ${
      view === v.mode
  ? "bg-primary text-primary-foreground"
        : "text-muted-foreground hover:bg-accent"
  }`}
            title={v.label}
        >
   <v.icon className="h-3.5 w-3.5" />
            </button>
      ))}
        </div>
      </div>

      {/* List View */}
      {view === "list" && <ListView tasks={tasks} onStatusChange={handleStatusChange} onAssign={handleAssign} updatingId={updatingId} users={users} />}

      {/* Kanban View */}
    {view === "kanban" && <KanbanView tasks={tasks} onStatusChange={handleStatusChange} updatingId={updatingId} />}

 {/* Calendar View */}
      {view === "calendar" && <CalendarView tasks={tasks} />}
    </div>
  );
}

/* ?? List View ???????????????????????????????????????? */
function ListView({ tasks, onStatusChange, onAssign, updatingId, users }: {
  tasks: TaskDto[];
  onStatusChange: (id: string, status: string) => void;
  onAssign: (id: string, userId: string | null) => void;
  updatingId: string | null;
  users: UserDto[];
}) {
  const sorted = [...tasks].sort((a, b) => {
    if (a.status === "done" && b.status !== "done") return 1;
    if (a.status !== "done" && b.status === "done") return -1;
    const da = a.deadline ? new Date(a.deadline).getTime() : Infinity;
    const db = b.deadline ? new Date(b.deadline).getTime() : Infinity;
    return da - db;
  });

  if (sorted.length === 0) {
    return (
      <div className="rounded-xl border border-dashed border-border bg-card/50 p-8 text-center">
        <p className="text-sm text-muted-foreground">No tasks for this case yet.</p>
      </div>
    );
  }

  return (
    <div className="rounded-xl border border-border bg-card divide-y divide-border">
      {sorted.map(task => (
        <TaskRow key={task.id} task={task} onStatusChange={onStatusChange} onAssign={onAssign} updating={updatingId === task.id} users={users} />
      ))}
    </div>
  );
}

function TaskRow({ task, onStatusChange, onAssign, updating, users }: {
  task: TaskDto;
  onStatusChange: (id: string, status: string) => void;
  onAssign: (id: string, userId: string | null) => void;
  updating: boolean;
  users: UserDto[];
}) {
  const isOverdue = task.deadline && isPast(new Date(task.deadline)) && task.status !== "done";
  const isDueToday = task.deadline && isToday(new Date(task.deadline));

  return (
    <div className={`flex items-center gap-3 px-4 py-2.5 ${isOverdue ? "bg-destructive/5" : ""} ${task.status === "done" ? "opacity-60" : ""}`}>
      {/* Status toggle */}
      <button
        onClick={() => onStatusChange(task.id, task.status === "done" ? "open" : "done")}
        disabled={updating}
      className={`h-5 w-5 rounded-full border-2 flex items-center justify-center shrink-0 transition-colors ${
          task.status === "done"
  ? "border-green-500 bg-green-500 text-white"
       : isOverdue
      ? "border-red-400 hover:border-red-500"
     : "border-border hover:border-primary"
   }`}
      >
        {task.status === "done" && <CheckCircle2 className="h-3 w-3" />}
      </button>

      {/* Content */}
      <div className="min-w-0 flex-1">
      <p className={`text-sm font-medium truncate ${task.status === "done" ? "line-through text-muted-foreground" : "text-foreground"}`}>
 {task.title}
        </p>
        {task.description && (
          <p className="text-[10px] text-muted-foreground truncate">{task.description}</p>
        )}
      </div>

      {/* Labels */}
      {task.labels && (
        <div className="hidden sm:flex gap-1">
          {task.labels.split(",").slice(0, 2).map(l => (
       <Badge key={l.trim()} variant="outline" className="text-[9px] px-1.5">{l.trim()}</Badge>
    ))}
        </div>
      )}

    {/* Assignee */}
      <select
        value={task.assignedToUserId ?? ""}
        onChange={e => onAssign(task.id, e.target.value || null)}
        disabled={updating}
        onClick={e => e.stopPropagation()}
        className="hidden md:block text-[10px] text-muted-foreground bg-transparent border-b border-dashed border-border/40 hover:border-primary/50 focus:outline-none focus:border-primary cursor-pointer transition-colors max-w-[110px] truncate"
      >
        <option value="">— unassigned —</option>
        {users.map(u => <option key={u.id} value={u.id}>{u.fullName}</option>)}
      </select>

      {/* Deadline */}
      {task.deadline && (
        <span className={`text-[10px] shrink-0 font-medium ${
isOverdue ? "text-destructive" : isDueToday ? "text-amber-500" : "text-muted-foreground"
  }`}>
       {isOverdue && "? "}
          {format(new Date(task.deadline), "dd MMM")}
        </span>
      )}

      {/* Status badge */}
      <Badge
        variant={task.status === "done" ? "success" : task.status === "blocked" ? "destructive" : "secondary"}
        className="text-[10px] shrink-0"
   >
        {task.status}
    </Badge>
    </div>
  );
}

/* ?? Kanban View ?????????????????????????????????????? */
function KanbanView({ tasks, onStatusChange, updatingId: _updatingId }: {
  tasks: TaskDto[];
  onStatusChange: (id: string, status: string) => void;
  updatingId: string | null;
}) {
  return (
    <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3">
      {STATUS_COLUMNS.map(col => {
        const colTasks = tasks.filter(t => t.status === col.key);
        return (
          <div key={col.key} className="rounded-xl border border-border bg-card/50">
            <div className="flex items-center gap-2 border-b border-border px-3 py-2">
           <col.icon className={`h-3.5 w-3.5 ${col.color}`} />
      <span className="text-xs font-semibold">{col.label}</span>
        <Badge variant="secondary" className="text-[9px] ml-auto">{colTasks.length}</Badge>
    </div>
     <div className="p-2 space-y-2 max-h-[400px] overflow-y-auto">
   {colTasks.length === 0 ? (
  <p className="text-[10px] text-muted-foreground text-center py-4">No tasks</p>
      ) : (
                colTasks.map(task => (
          <KanbanCard
         key={task.id}
         task={task}
     onStatusChange={onStatusChange}
                    nextStatus={col.key === "open" ? "done" : col.key === "done" ? "open" : "done"}
     />
           ))
    )}
     </div>
          </div>
   );
      })}
    </div>
  );
}

function KanbanCard({ task, onStatusChange, nextStatus }: {
  task: TaskDto;
  onStatusChange: (id: string, status: string) => void;
  nextStatus: string;
}) {
  const isOverdue = task.deadline && isPast(new Date(task.deadline)) && task.status !== "done";

  return (
    <div
      className={`rounded-lg border border-border bg-card p-2.5 cursor-pointer hover:shadow-sm transition-shadow ${isOverdue ? "border-destructive/50" : ""}`}
      onClick={() => onStatusChange(task.id, nextStatus)}
    >
   <p className="text-xs font-medium text-foreground line-clamp-2">{task.title}</p>
      <div className="mt-1.5 flex items-center gap-2">
        {task.deadline && (
    <span className={`text-[9px] ${isOverdue ? "text-destructive font-medium" : "text-muted-foreground"}`}>
            {format(new Date(task.deadline), "dd MMM")}
  </span>
 )}
    {task.assignedToName && (
       <span className="text-[9px] text-muted-foreground truncate">{task.assignedToName}</span>
        )}
      </div>
    </div>
  );
}

/* ?? Calendar View ???????????????????????????????????? */
function CalendarView({ tasks }: { tasks: TaskDto[] }) {
  const [weekOffset, setWeekOffset] = useState(0);

  const now = new Date();
  const weekStart = startOfWeek(addDays(now, weekOffset * 7), { weekStartsOn: 1 });
  const weekEnd = endOfWeek(addDays(now, weekOffset * 7), { weekStartsOn: 1 });

  const days = Array.from({ length: 7 }, (_, i) => addDays(weekStart, i));

  return (
    <div className="space-y-2">
      <div className="flex items-center justify-between">
        <Button variant="ghost" size="sm" className="text-xs" onClick={() => setWeekOffset(w => w - 1)}>← Prev</Button>
        <span className="text-xs font-medium text-muted-foreground">
       {format(weekStart, "dd MMM")} — {format(weekEnd, "dd MMM yyyy")}
        </span>
        <div className="flex gap-1">
          <Button variant="ghost" size="sm" className="text-xs" onClick={() => setWeekOffset(0)}>Today</Button>
        <Button variant="ghost" size="sm" className="text-xs" onClick={() => setWeekOffset(w => w + 1)}>Next →</Button>
      </div>
      </div>

      <div className="grid grid-cols-7 gap-1">
        {days.map(day => {
          const dayStr = format(day, "yyyy-MM-dd");
          const dayTasks = tasks.filter(t =>
t.deadline && format(new Date(t.deadline), "yyyy-MM-dd") === dayStr
          );
        const isWeekend = day.getDay() === 0 || day.getDay() === 6;
          const isTodayDate = isToday(day);

          return (
            <div
      key={dayStr}
       className={`rounded-lg border p-2 min-h-[100px] ${
                isTodayDate ? "border-primary bg-primary/5" : isWeekend ? "border-border bg-muted/30" : "border-border bg-card"
          }`}
            >
           <p className={`text-[10px] font-medium mb-1 ${isTodayDate ? "text-primary" : "text-muted-foreground"}`}>
      {format(day, "EEE d")}
    </p>
    <div className="space-y-1">
    {dayTasks.map(t => (
      <div
      key={t.id}
            className={`rounded px-1.5 py-0.5 text-[9px] truncate ${
      t.status === "done"
         ? "bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-400"
             : "bg-primary/10 text-primary"
              }`}
          title={t.title}
 >
        {t.title}
   </div>
            ))}
       </div>
       </div>
          );
        })}
      </div>
  </div>
  );
}
