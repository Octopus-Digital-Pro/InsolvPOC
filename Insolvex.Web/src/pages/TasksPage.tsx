import { useState, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { tasksApi } from "@/services/api";
import { useTranslation } from "@/contexts/LanguageContext";
import type { TaskDto } from "@/services/api/types";
import { Badge } from "@/components/ui/badge";
import { Loader2, Search, ListChecks } from "lucide-react";
import { format } from "date-fns";

const STATUS_VARIANT: Record<string, "default" | "warning" | "success"> = {
  open: "default",
  blocked: "warning",
  done: "success",
};

export default function TasksPage() {
  const navigate = useNavigate();
  const { t } = useTranslation();
  const [tasks, setTasks] = useState<TaskDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState("");
  const [filter, setFilter] = useState<"all" | "mine">("mine");

  useEffect(() => {
    tasksApi.getAll({ myTasks: filter === "mine" })
      .then(res => setTasks(res.data))
      .catch(console.error)
.finally(() => setLoading(false));
  }, [filter]);

  const filtered = tasks.filter(t =>
    !search ||
 t.title.toLowerCase().includes(search.toLowerCase()) ||
    (t.companyName ?? "").toLowerCase().includes(search.toLowerCase())
  );

  if (loading) return <div className="flex h-full items-center justify-center"><Loader2 className="h-8 w-8 animate-spin text-muted-foreground" /></div>;

  const statusLabel = (s: string) =>
    s === "open" ? t.tasks.open : s === "blocked" ? t.tasks.blocked : s === "done" ? t.tasks.done : s;

  return (
    <div className="mx-auto max-w-6xl space-y-4">
 <div className="flex items-center justify-between">
        <h1 className="text-xl font-bold text-foreground">{t.tasks.title}</h1>
      </div>

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
    onClick={() => { setFilter("mine"); setLoading(true); }}
   >
     {t.tasks.myTasks}
   </button>
     <button
         className={`px-3 py-2 text-xs font-medium transition-colors ${filter === "all" ? "bg-primary text-primary-foreground" : "bg-background text-foreground hover:bg-accent"}`}
         onClick={() => { setFilter("all"); setLoading(true); }}
          >
  {t.tasks.allTasks}
  </button>
 </div>
   </div>

      <div className="rounded-xl border border-border bg-card divide-y divide-border">
        {filtered.length === 0 ? (
  <div className="flex flex-col items-center justify-center py-12 text-muted-foreground">
            <ListChecks className="h-10 w-10 mb-2 opacity-30" />
  <p className="text-sm">{t.tasks.noTasks}</p>
 </div>
        ) : (
   filtered.map(tk => (
    <div key={tk.id} className="flex items-center gap-3 px-4 py-2.5 hover:bg-accent/50 cursor-pointer transition-colors" onClick={() => tk.companyId && navigate(`/companies/${tk.companyId}`)}>
    <div className="min-w-0 flex-1">
 <div className="flex items-center gap-2">
 <p className="text-sm font-medium text-foreground truncate">{tk.title}</p>
   {tk.labels && tk.labels.split(",").slice(0, 2).map(l => (
  <Badge key={l.trim()} variant="secondary" className="text-[10px] font-normal shrink-0">{l.trim()}</Badge>
  ))}
  </div>
  <p className="text-xs text-muted-foreground">{tk.companyName ?? "—"}{tk.assignedToName ? ` · ${tk.assignedToName}` : ""}</p>
          </div>
              <Badge variant={STATUS_VARIANT[tk.status] ?? "default"} className="shrink-0">{statusLabel(tk.status)}</Badge>
          {tk.deadline && (
    <span className={`text-xs shrink-0 ${new Date(tk.deadline) < new Date() && tk.status !== "done" ? "text-destructive font-medium" : "text-muted-foreground"}`}>
  {format(new Date(tk.deadline), "dd MMM")}
   </span>
  )}
 </div>
 ))
        )}
      </div>
    </div>
  );
}
