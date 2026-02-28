import { useState, useEffect, useRef } from "react";
import { useNavigate } from "react-router-dom";
import { tasksApi } from "@/services/api";
import { useTranslation } from "@/contexts/LanguageContext";
import type { TaskDto } from "@/services/api/types";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import TaskDetailModal from "@/components/TaskDetailModal";
import {
  Loader2, Search, ListChecks, LayoutGrid, Calendar as CalendarIcon,
  CheckCircle2, Clock, AlertTriangle, Ban, X,
} from "lucide-react";
import { format, isPast, isToday, addDays, startOfWeek, endOfWeek } from "date-fns";

type ViewMode = "list" | "kanban" | "calendar";

const STATUS_VARIANT: Record<string, "default" | "warning" | "success" | "destructive"> = {
  open: "default",
  inProgress: "warning",
  blocked: "destructive",
  done: "success",
  overdue: "destructive",
};

const KANBAN_COLS = [
  { key: "open", label: "Open", icon: Clock, color: "text-blue-500" },
  { key: "inProgress", label: "In Progress", icon: AlertTriangle, color: "text-amber-500" },
  { key: "blocked", label: "Blocked", icon: Ban, color: "text-red-500" },
  { key: "done", label: "Done", icon: CheckCircle2, color: "text-green-500" },
] as const;

function BlockReasonModal({
  open, onConfirm, onCancel,
}: { open: boolean; onConfirm: (reason: string) => void; onCancel: () => void }) {
  const [reason, setReason] = useState("");
  const { t } = useTranslation();
  const inputRef = useRef<HTMLTextAreaElement>(null);
  useEffect(() => { if (open) { setReason(""); setTimeout(() => inputRef.current?.focus(), 50); } }, [open]);
  if (!open) return null;
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
      <div className="w-full max-w-sm rounded-xl border border-border bg-card p-5 shadow-xl space-y-3">
        <div className="flex items-center justify-between">
          <h3 className="flex items-center gap-2 text-sm font-semibold text-foreground">
            <Ban className="h-4 w-4 text-destructive" /> {t.tasks.blockReasonLabel}
          </h3>
          <button onClick={onCancel} className="text-muted-foreground hover:text-foreground"><X className="h-4 w-4" /></button>
        </div>
        <textarea ref={inputRef} value={reason} onChange={e => setReason(e.target.value)}
          placeholder={t.tasks.blockReasonPlaceholder} rows={3}
          className="w-full rounded-lg border border-input bg-background px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring resize-none" />
        <div className="flex justify-end gap-2">
          <Button variant="outline" size="sm" className="text-xs" onClick={onCancel}>{t.common.cancel}</Button>
          <Button size="sm" className="text-xs bg-destructive hover:bg-destructive/90" disabled={!reason.trim()}
            onClick={() => onConfirm(reason.trim())}>
            <Ban className="h-3 w-3 mr-1" /> Block Task
          </Button>
        </div>
      </div>
    </div>
  );
}

export default function TasksPage() {
  const navigate = useNavigate();
  const { t } = useTranslation();
  const [tasks, setTasks] = useState<TaskDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState("");
  const [filter, setFilter] = useState<"all" | "mine">("mine");
  const [view, setView] = useState<ViewMode>("list");
  const [updatingId, setUpdatingId] = useState<string | null>(null);
  const [draggedId, setDraggedId] = useState<string | null>(null);
  const [blockModal, setBlockModal] = useState<{ taskId: string; open: boolean }>({ taskId: "", open: false });
  const [selectedTaskId, setSelectedTaskId] = useState<string | null>(null);

  const reload = () => {
    setLoading(true);
    tasksApi.getAll({ myTasks: filter === "mine" })
      .then(res => setTasks(res.data))
      .catch(console.error)
      .finally(() => setLoading(false));
  };

  useEffect(() => { reload(); }, [filter]);

  const applyStatusChange = async (taskId: string, newStatus: string, blockReason?: string) => {
    setUpdatingId(taskId);
    try {
      await tasksApi.update(taskId, { status: newStatus, ...(blockReason ? { blockReason } : {}) });
      reload();
    } catch (e) { console.error(e); }
    finally { setUpdatingId(null); }
  };

  const handleStatusChange = (taskId: string, newStatus: string) => {
    if (newStatus === "blocked") { setBlockModal({ taskId, open: true }); return; }
    applyStatusChange(taskId, newStatus);
  };

  const handleBlockConfirm = (reason: string) => {
    const { taskId } = blockModal;
    setBlockModal({ taskId: "", open: false });
    applyStatusChange(taskId, "blocked", reason);
  };

  const handleDrop = (colKey: string) => {
    if (!draggedId) return;
    handleStatusChange(draggedId, colKey);
    setDraggedId(null);
  };

  const filtered = tasks.filter(tk =>
    !search ||
    tk.title.toLowerCase().includes(search.toLowerCase()) ||
    (tk.companyName ?? "").toLowerCase().includes(search.toLowerCase())
  );

  const overdue = filtered.filter(t => t.deadline && isPast(new Date(t.deadline)) && t.status !== "done");
  const blockedTasks = filtered.filter(t => t.status === "blocked");
  const inProgressTasks = filtered.filter(t => t.status === "inProgress");

  if (loading) return <div className="flex h-full items-center justify-center"><Loader2 className="h-8 w-8 animate-spin text-muted-foreground" /></div>;

  const statusLabel = (s: string) => {
    if (s === "open") return t.tasks.open;
    if (s === "inProgress") return t.tasks.inProgress;
    if (s === "blocked") return t.tasks.blocked;
    if (s === "done") return t.tasks.done;
    return s;
  };

  return (
    <div className="mx-auto max-w-6xl space-y-4">
      <BlockReasonModal open={blockModal.open} onConfirm={handleBlockConfirm} onCancel={() => setBlockModal({ taskId: "", open: false })} />
      <TaskDetailModal taskId={selectedTaskId} onClose={() => setSelectedTaskId(null)} onStatusChanged={() => reload()} />

      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <h1 className="text-xl font-bold text-foreground">{t.tasks.title}</h1>
          {overdue.length > 0 && <Badge variant="destructive" className="text-[10px]">{overdue.length} overdue</Badge>}
        </div>
        <div className="flex items-center gap-1 rounded-lg border border-border p-0.5">
          {([
            { mode: "list" as ViewMode, icon: ListChecks, label: "List" },
            { mode: "kanban" as ViewMode, icon: LayoutGrid, label: "Board" },
            { mode: "calendar" as ViewMode, icon: CalendarIcon, label: "Calendar" },
          ]).map(v => (
            <button key={v.mode} onClick={() => setView(v.mode)}
              className={`rounded-md p-1.5 transition-colors ${
                view === v.mode ? "bg-primary text-primary-foreground" : "text-muted-foreground hover:bg-accent"
              }`} title={v.label}><v.icon className="h-4 w-4" /></button>
          ))}
        </div>
      </div>

      {/* Blocked / In-Progress summary widgets */}
      {(blockedTasks.length > 0 || inProgressTasks.length > 0) && (
        <div className="grid grid-cols-2 gap-3">
          {blockedTasks.length > 0 && (
            <div className="flex items-center gap-3 rounded-xl border border-destructive/30 bg-destructive/5 px-4 py-3">
              <Ban className="h-4 w-4 text-destructive shrink-0" />
              <div><p className="text-lg font-bold leading-none text-destructive">{blockedTasks.length}</p>
                <p className="mt-0.5 text-xs text-muted-foreground">{t.tasks.blockedTasks}</p></div>
            </div>
          )}
          {inProgressTasks.length > 0 && (
            <div className="flex items-center gap-3 rounded-xl border border-amber-500/30 bg-amber-500/5 px-4 py-3">
              <AlertTriangle className="h-4 w-4 text-amber-500 shrink-0" />
              <div><p className="text-lg font-bold leading-none text-amber-600">{inProgressTasks.length}</p>
                <p className="mt-0.5 text-xs text-muted-foreground">{t.tasks.inProgressTasks}</p></div>
            </div>
          )}
        </div>
      )}

      {/* Filters */}
      <div className="flex gap-2 items-center">
        <div className="relative flex-1">
          <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
          <input
            type="text"
            placeholder={t.tasks.searchPlaceholder}
            value={search}
            onChange={e => setSearch(e.target.value)}
            className="w-full rounded-lg border border-input bg-background py-2 pl-9 pr-3 text-sm focus:outline-none focus:ring-2 focus:ring-ring"
          />
        </div>
        <div className="flex rounded-lg border border-input overflow-hidden">
          <button
            className={`px-3 py-2 text-xs font-medium transition-colors ${filter === "mine" ? "bg-primary text-primary-foreground" : "bg-background text-foreground hover:bg-accent"}`}
            onClick={() => setFilter("mine")}
          >
            {t.tasks.myTasks}
          </button>
          <button
            className={`px-3 py-2 text-xs font-medium transition-colors ${filter === "all" ? "bg-primary text-primary-foreground" : "bg-background text-foreground hover:bg-accent"}`}
            onClick={() => setFilter("all")}
          >
            {t.tasks.allTasks}
          </button>
        </div>
      </div>


      {/* List View (grouped by status) */}
      {view === "list" && (
        <GroupedListView
          tasks={filtered}
          statusLabel={statusLabel}
          updatingId={updatingId}
          onStatusChange={handleStatusChange}
          navigate={navigate}
          onTaskClick={setSelectedTaskId}
        />
      )}

      {/* Kanban View (drag-and-drop) */}
      {view === "kanban" && (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3">
          {KANBAN_COLS.map(col => {
            const colTasks = filtered.filter(t => t.status === col.key);
            return (
              <div
                key={col.key}
                className="rounded-xl border border-border bg-card/50"
                onDragOver={e => e.preventDefault()}
                onDrop={() => handleDrop(col.key)}
              >
                <div className="flex items-center gap-2 border-b border-border px-3 py-2">
                  <col.icon className={`h-3.5 w-3.5 ${col.color}`} />
                  <span className="text-xs font-semibold">{col.label}</span>
                  <Badge variant="secondary" className="text-[9px] ml-auto">{colTasks.length}</Badge>
                </div>
                <div className="p-2 space-y-2 max-h-[500px] overflow-y-auto">
                  {colTasks.length === 0 ? (
                    <p className="text-[10px] text-muted-foreground text-center py-6">No tasks</p>
                  ) : (
                    colTasks.map(task => {
                      const isOverdue = task.deadline && isPast(new Date(task.deadline)) && task.status !== "done";
                      return (
                        <div
                          key={task.id}
                          draggable
                          onDragStart={() => setDraggedId(task.id)}
                          onDragEnd={() => setDraggedId(null)}
                          onClick={() => setSelectedTaskId(task.id)}
                          className={`rounded-lg border border-border bg-card p-2.5 cursor-pointer hover:shadow-sm hover:bg-accent/30 transition-shadow ${isOverdue ? "border-destructive/50" : ""} ${draggedId === task.id ? "opacity-50" : ""}`}
                        >
                          <p className="text-xs font-medium text-foreground line-clamp-2">{task.title}</p>
                          <div className="mt-1.5 flex items-center gap-2 flex-wrap">
                            {task.companyName && <span className="text-[9px] text-muted-foreground truncate">{task.companyName}</span>}
                            {task.deadline && (
                              <span className={`text-[9px] ${isOverdue ? "text-destructive font-medium" : "text-muted-foreground"}`}>
                                {format(new Date(task.deadline), "dd MMM")}
                              </span>
                            )}
                          </div>
                          {task.status === "blocked" && task.blockReason && (
                            <p className="mt-1 text-[9px] text-destructive italic line-clamp-1">{task.blockReason}</p>
                          )}
                        </div>
                      );
                    })
                  )}
                </div>
              </div>
            );
          })}
        </div>
      )}

      {/* Calendar View */}
      {view === "calendar" && <TaskCalendarView tasks={filtered} />}
    </div>
  );
}

/* â”€â”€â”€ Grouped List View â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€ */
const GROUP_ORDER: Array<TaskDto["status"]> = ["blocked", "inProgress", "open", "done"];
const GROUP_META: Record<string, { label: string; colorClass: string }> = {
  blocked:    { label: "Blocked",     colorClass: "text-destructive" },
  inProgress: { label: "In Progress", colorClass: "text-amber-500" },
  open:       { label: "Open",        colorClass: "text-blue-500" },
  done:       { label: "Done",        colorClass: "text-green-500" },
};

function GroupedListView({
  tasks,
  statusLabel,
  updatingId,
  onStatusChange,
  navigate: _navigate,
  onTaskClick,
}: {
  tasks: TaskDto[];
  statusLabel: (s: string) => string;
  updatingId: string | null;
  onStatusChange: (id: string, status: string) => void;
  navigate: (path: string) => void;
  onTaskClick: (id: string) => void;
}) {
  const groups = GROUP_ORDER
    .map(status => ({ status, items: tasks.filter(t => t.status === status) }))
    .filter(g => g.items.length > 0);

  if (groups.length === 0) {
    return (
      <div className="rounded-xl border border-border bg-card flex flex-col items-center justify-center py-12 text-muted-foreground">
        <ListChecks className="h-10 w-10 mb-2 opacity-30" />
        <p className="text-sm">No tasks</p>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      {groups.map(({ status, items }) => {
        const meta = GROUP_META[status] ?? { label: status, colorClass: "text-muted-foreground" };
        return (
          <div key={status} className="rounded-xl border border-border bg-card overflow-hidden">
            <div className="flex items-center gap-2 border-b border-border bg-muted/30 px-4 py-2">
              <span className={`text-xs font-semibold ${meta.colorClass}`}>{meta.label}</span>
              <Badge variant="secondary" className="text-[9px]">{items.length}</Badge>
            </div>
            <div className="divide-y divide-border">
              {items.map(tk => {
                const isOverdue = tk.deadline && isPast(new Date(tk.deadline)) && tk.status !== "done";
                const isDueToday = tk.deadline && isToday(new Date(tk.deadline));
                return (
                  <div key={tk.id} className={`flex items-center gap-3 px-4 py-2.5 hover:bg-accent/50 transition-colors ${isOverdue ? "bg-destructive/5" : ""}`}>
                    <button
                      onClick={() => onStatusChange(tk.id, tk.status === "done" ? "open" : "done")}
                      disabled={updatingId === tk.id}
                      className={`h-5 w-5 rounded-full border-2 flex items-center justify-center shrink-0 transition-colors ${
                        tk.status === "done"
                          ? "border-green-500 bg-green-500 text-white"
                          : isOverdue ? "border-red-400 hover:border-red-500"
                          : "border-border hover:border-primary"
                      }`}
                    >
                      {tk.status === "done" && <CheckCircle2 className="h-3 w-3" />}
                    </button>
                    <div className="min-w-0 flex-1 cursor-pointer" onClick={() => onTaskClick(tk.id)}>                      <div className="flex items-center gap-2">
                        <p className={`text-sm font-medium truncate ${tk.status === "done" ? "line-through text-muted-foreground" : "text-foreground"}`}>{tk.title}</p>
                        {tk.labels && tk.labels.split(",").slice(0, 2).map(l => (
                          <Badge key={l.trim()} variant="secondary" className="text-[10px] font-normal shrink-0 hidden sm:inline-flex">{l.trim()}</Badge>
                        ))}
                      </div>
                      <p className="text-xs text-muted-foreground">
                        {tk.companyName ?? ""}
                        {tk.assignedToName ? ` \u00b7 ${tk.assignedToName}` : ""}
                        {tk.status === "blocked" && tk.blockReason ? ` \u00b7 ${tk.blockReason}` : ""}
                      </p>
                    </div>
                    <Badge variant={STATUS_VARIANT[tk.status] ?? "default"} className="shrink-0 text-[10px]">{statusLabel(tk.status)}</Badge>
                    {tk.deadline && (
                      <span className={`text-xs shrink-0 ${isOverdue ? "text-destructive font-medium" : isDueToday ? "text-amber-500" : "text-muted-foreground"}`}>
                        {isOverdue && "! "}{format(new Date(tk.deadline), "dd MMM")}
                      </span>
                    )}
                  </div>
                );
              })}
            </div>
          </div>
        );
      })}
    </div>
  );
}

/* â”€â”€â”€ Calendar View (week grid) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€ */
function TaskCalendarView({ tasks }: { tasks: TaskDto[] }) {
  const [weekOffset, setWeekOffset] = useState(0);

  const now = new Date();
  const weekStart = startOfWeek(addDays(now, weekOffset * 7), { weekStartsOn: 1 });
  const weekEnd = endOfWeek(addDays(now, weekOffset * 7), { weekStartsOn: 1 });
  const days = Array.from({ length: 7 }, (_, i) => addDays(weekStart, i));

  return (
    <div className="space-y-2">
      <div className="flex items-center justify-between">
        <Button variant="ghost" size="sm" className="text-xs" onClick={() => setWeekOffset(w => w - 1)}>
          &laquo; Prev
        </Button>
        <span className="text-xs font-medium text-muted-foreground">
          {format(weekStart, "dd MMM")} &ndash; {format(weekEnd, "dd MMM yyyy")}
        </span>
        <div className="flex gap-1">
          <Button variant="ghost" size="sm" className="text-xs" onClick={() => setWeekOffset(0)}>Today</Button>
          <Button variant="ghost" size="sm" className="text-xs" onClick={() => setWeekOffset(w => w + 1)}>
            Next &raquo;
          </Button>
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
              className={`rounded-lg border p-2 min-h-[120px] ${
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
                    title={`${t.title} - ${t.companyName ?? ""}`}
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

