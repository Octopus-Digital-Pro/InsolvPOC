import { useState, useEffect, useCallback } from "react";
import { useNavigate } from "react-router-dom";
import { dashboardApi } from "@/services/api";
import { useTranslation } from "@/contexts/LanguageContext";
import type { DashboardDto, CalendarEventDto } from "@/services/api/types";
import type { Translations } from "@/i18n/types";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Briefcase, Building2, CheckCircle2, Clock, AlertTriangle,
  ChevronLeft, ChevronRight, CalendarDays, Loader2,
  Upload, FileUp, FileText, Sparkles, Ban,
} from "lucide-react";
import {
  format, startOfMonth, endOfMonth, eachDayOfInterval,
  isSameMonth, isToday, addMonths, subMonths, startOfWeek, endOfWeek,
} from "date-fns";

/* ?? Stat Card ??????????????????????????????????????????? */
function StatCard({ icon: Icon, label, value, accent }: {
  icon: React.ElementType; label: string; value: number; accent?: string;
}) {
  return (
    <div className="flex items-center gap-3 rounded-xl border border-border bg-card px-4 py-3">
      <div className={`flex h-9 w-9 shrink-0 items-center justify-center rounded-lg ${accent ?? "bg-primary/10 text-primary"}`}>
        <Icon className="h-4.5 w-4.5" />
      </div>
      <div className="min-w-0">
    <p className="text-2xl font-bold leading-none text-foreground">{value}</p>
 <p className="mt-0.5 text-xs text-muted-foreground truncate">{label}</p>
      </div>
    </div>
  );
}

/* ?? Full Calendar ??????????????????????????????????????? */
function FullCalendar({ events, selectedDate, onDateClick, t }: {
  events: CalendarEventDto[];
  selectedDate: Date | null;
  onDateClick: (date: Date) => void;
  t: Translations;
}) {
  const [month, setMonth] = useState(new Date());
  const start = startOfWeek(startOfMonth(month), { weekStartsOn: 1 });
  const end = endOfWeek(endOfMonth(month), { weekStartsOn: 1 });
  const days = eachDayOfInterval({ start, end });

  const eventsByDate = new Map<string, CalendarEventDto[]>();
  for (const ev of events) {
    const key = format(new Date(ev.start), "yyyy-MM-dd");
    const arr = eventsByDate.get(key) ?? [];
    arr.push(ev);
    eventsByDate.set(key, arr);
  }

const selectedKey = selectedDate ? format(selectedDate, "yyyy-MM-dd") : null;

  return (
    <div className="rounded-xl border border-border bg-card p-5 h-full flex flex-col">
    {/* Header */}
      <div className="mb-4 flex items-center justify-between">
    <div className="flex items-center gap-2">
          <CalendarDays className="h-5 w-5 text-primary" />
  <h3 className="text-base font-semibold text-foreground">{format(month, "MMMM yyyy")}</h3>
      </div>
        <div className="flex gap-1">
      <Button variant="outline" size="icon" className="h-8 w-8" onClick={() => setMonth(subMonths(month, 1))}><ChevronLeft className="h-4 w-4" /></Button>
          <Button variant="outline" size="sm" className="h-8 px-3 text-xs" onClick={() => setMonth(new Date())}>{t.dashboard.today}</Button>
   <Button variant="outline" size="icon" className="h-8 w-8" onClick={() => setMonth(addMonths(month, 1))}><ChevronRight className="h-4 w-4" /></Button>
        </div>
    </div>

      {/* Day headers */}
      <div className="grid grid-cols-7 text-center text-xs font-semibold uppercase text-muted-foreground mb-2">
        {["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"].map(d => <div key={d}>{d}</div>)}
      </div>

    {/* Days grid � flex-1 so it fills remaining space */}
      <div className="grid grid-cols-7 gap-1 flex-1">
        {days.map((day) => {
          const key = format(day, "yyyy-MM-dd");
          const dayEvents = eventsByDate.get(key) ?? [];
   const inMonth = isSameMonth(day, month);
          const today = isToday(day);
        const isSelected = key === selectedKey;

          return (
      <button
         key={key}
 onClick={() => onDateClick(day)}
   className={`
       relative flex flex-col items-start rounded-lg p-1.5 text-left transition-all min-h-[4.5rem]
    ${!inMonth ? "text-muted-foreground/30" : "text-foreground"}
         ${today ? "ring-2 ring-primary ring-inset" : ""}
     ${isSelected ? "bg-primary/10" : "hover:bg-accent"}
              `}
            >
              <span className={`text-xs font-medium ${today ? "text-primary font-bold" : ""}`}>
          {format(day, "d")}
 </span>
              {dayEvents.length > 0 && (
        <div className="mt-auto flex flex-col gap-0.5 w-full">
        {dayEvents.slice(0, 2).map((ev, i) => (
         <div key={i} className={`truncate rounded px-1 py-px text-[9px] font-medium leading-tight ${
       ev.type === "hearing"
            ? "bg-chart-1/15 text-chart-1"
         : "bg-chart-2/15 text-chart-2"
      }`}>
  {ev.title.length > 18 ? ev.title.slice(0, 18) + "�" : ev.title}
          </div>
     ))}
      {dayEvents.length > 2 && (
     <span className="text-[9px] text-muted-foreground">+{dayEvents.length - 2} more</span>
    )}
       </div>
          )}
          </button>
 );
   })}
      </div>

      {/* Legend */}
      <div className="mt-3 flex gap-4 text-xs text-muted-foreground border-t border-border pt-3">
        <span className="flex items-center gap-1.5"><span className="h-2 w-2 rounded-full bg-chart-1" /> {t.dashboard.hearings}</span>
        <span className="flex items-center gap-1.5"><span className="h-2 w-2 rounded-full bg-chart-2" /> {t.dashboard.taskDeadlines}</span>
      </div>
    </div>
  );
}

/* ?? Upload Drop Zone ???????????????????????????????????? */
function QuickUploadZone({ t }: { t: Translations }) {
  const navigate = useNavigate();
  const [dragging, setDragging] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [recentUploads, setRecentUploads] = useState<Array<{ name: string; id: string; status: string }>>([]);

  const handleDrop = useCallback(async (e: React.DragEvent) => {
    e.preventDefault();
    setDragging(false);
    const files = Array.from(e.dataTransfer.files).filter(f =>
      f.type === "application/pdf" || f.name.endsWith(".pdf") || f.name.endsWith(".docx") || f.name.endsWith(".doc") || f.type.startsWith("image/")
    );
    if (files.length === 0) return;
    await uploadFiles(files);
  }, []);

  const handleFileSelect = useCallback(async (e: React.ChangeEvent<HTMLInputElement>) => {
    const files = Array.from(e.target.files ?? []);
    if (files.length === 0) return;
    await uploadFiles(files);
    e.target.value = "";
}, []);

  const uploadFiles = async (files: File[]) => {
    setUploading(true);
    for (const file of files) {
      const formData = new FormData();
      formData.append("file", file);
      try {
        const token = localStorage.getItem("authToken");
        const res = await fetch("/api/documents/upload", {
        method: "POST",
 headers: token ? { Authorization: `Bearer ${token}` } : {},
          body: formData,
        });
   if (res.ok) {
          const data = await res.json();
    setRecentUploads(prev => [{ name: file.name, id: data.id, status: data.recommendedAction ?? "processing" }, ...prev.slice(0, 4)]);
        } else {
          setRecentUploads(prev => [{ name: file.name, id: "", status: "error" }, ...prev.slice(0, 4)]);
        }
      } catch {
     setRecentUploads(prev => [{ name: file.name, id: "", status: "error" }, ...prev.slice(0, 4)]);
      }
    }
    setUploading(false);
  };

  return (
    <div className="rounded-xl border border-border bg-card p-5 h-full flex flex-col">
      <div className="flex items-center gap-2 mb-3">
        <Sparkles className="h-4 w-4 text-primary" />
        <h3 className="text-sm font-semibold text-foreground">{t.dashboard.quickUpload}</h3>
 </div>
      <p className="text-xs text-muted-foreground mb-3">
        {t.dashboard.quickUploadDesc}
      </p>

      {/* Drop zone */}
 <label
        onDragOver={e => { e.preventDefault(); setDragging(true); }}
        onDragLeave={() => setDragging(false)}
     onDrop={handleDrop}
    className={`
          flex-1 flex flex-col items-center justify-center rounded-lg border-2 border-dashed cursor-pointer transition-all min-h-[10rem]
        ${dragging
        ? "border-primary bg-primary/5 scale-[1.01]"
            : "border-border hover:border-primary/40 hover:bg-accent/30"}
        `}
      >
    <input type="file" className="sr-only" multiple accept=".pdf,.doc,.docx,image/*" onChange={handleFileSelect} />
        {uploading ? (
          <div className="flex flex-col items-center gap-2 text-primary">
 <Loader2 className="h-8 w-8 animate-spin" />
            <span className="text-xs font-medium">{t.dashboard.processingAi}</span>
   </div>
        ) : (
        <div className="flex flex-col items-center gap-2 text-muted-foreground">
            <FileUp className="h-10 w-10 opacity-40" />
            <span className="text-xs font-medium">{t.dashboard.dragDrop}</span>
            <span className="text-[10px]">{t.dashboard.fileTypes}</span>
         <Button variant="outline" size="sm" className="mt-2 text-xs gap-1.5 border-primary/30 text-primary hover:bg-primary/5" type="button">
              <Upload className="h-3.5 w-3.5" />
  {t.dashboard.browseFiles}
    </Button>
   </div>
        )}
      </label>

      {/* Recent uploads */}
      {recentUploads.length > 0 && (
     <div className="mt-3 space-y-1.5">
          <p className="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">{t.dashboard.recent}</p>
        {recentUploads.map((u, i) => (
            <div
    key={i}
   className="flex items-center gap-2 rounded-md border border-border px-2.5 py-1.5 text-xs cursor-pointer hover:bg-accent/50 transition-colors"
     onClick={() => u.id && navigate(`/documents/${u.id}/review`)}
   >
     <FileText className="h-3.5 w-3.5 shrink-0 text-muted-foreground" />
              <span className="truncate flex-1 text-foreground">{u.name}</span>
      <Badge
                variant={u.status === "error" ? "destructive" : u.status === "processing" ? "secondary" : "success"}
   className="text-[9px] shrink-0"
      >
          {u.status === "newCase" ? t.dashboard.newCase : u.status === "filing" ? t.dashboard.fileToCase : u.status}
        </Badge>
 </div>
          ))}
  </div>
      )}
    </div>
  );
}

/* ?? Dashboard Page ?????????????????????????????????????? */
export default function DashboardPage() {
  const navigate = useNavigate();
  const { t } = useTranslation();
  const [data, setData] = useState<DashboardDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [selectedDate, setSelectedDate] = useState<Date | null>(null);

  useEffect(() => {
    dashboardApi.get()
      .then(res => setData(res.data))
      .catch(err => console.error("Dashboard load failed:", err))
      .finally(() => setLoading(false));
  }, []);

  if (loading) {
    return (
  <div className="flex h-full items-center justify-center">
        <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (!data) return <p className="p-8 text-muted-foreground">{t.common.loading}</p>;

  const selectedDateStr = selectedDate ? format(selectedDate, "yyyy-MM-dd") : null;
  const selectedEvents = selectedDateStr
    ? data.calendarEvents.filter(e => format(new Date(e.start), "yyyy-MM-dd") === selectedDateStr)
    : [];

  return (
    <div className="mx-auto max-w-7xl space-y-5">
   {/* Stats row */}
 <div className="grid grid-cols-2 gap-3 lg:grid-cols-5">
  <StatCard icon={Briefcase} label={t.dashboard.totalCases} value={data.totalCases} />
 <StatCard icon={CheckCircle2} label={t.dashboard.openCases} value={data.openCases} accent="bg-chart-2/15 text-chart-2" />
        <StatCard icon={Building2} label={t.dashboard.companies} value={data.totalCompanies} accent="bg-chart-3/15 text-chart-3" />
        <StatCard icon={Clock} label={t.dashboard.pendingTasks} value={data.pendingTasks} accent="bg-chart-4/15 text-chart-4" />
        <StatCard icon={AlertTriangle} label={t.dashboard.overdue} value={data.overdueTasks} accent="bg-destructive/10 text-destructive" />
      </div>

      {/* Calendar (8 cols) + Upload Zone (4 cols) */}
    <div className="grid gap-4 lg:grid-cols-12">
      <div className="lg:col-span-8">
          <FullCalendar events={data.calendarEvents} selectedDate={selectedDate} onDateClick={setSelectedDate} t={t} />
        </div>
        <div className="lg:col-span-4">
          <QuickUploadZone t={t} />
        </div>
      </div>

      {/* Selected date detail */}
      {selectedDate && selectedEvents.length > 0 && (
        <div className="rounded-xl border border-border bg-card p-4">
      <p className="mb-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
            {t.dashboard.eventsOn} {format(selectedDate, "d MMMM yyyy")}
          </p>
          <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-3">
         {selectedEvents.map(ev => (
          <div
           key={ev.id}
     className="flex items-start gap-2 rounded-lg border border-border px-3 py-2 text-xs cursor-pointer hover:bg-accent/50 transition-colors"
            onClick={() => ev.caseId && navigate(`/cases/${ev.caseId}`)}
        >
<span className={`mt-0.5 h-2 w-2 shrink-0 rounded-full ${ev.type === "hearing" ? "bg-chart-1" : "bg-chart-2"}`} />
 <div className="min-w-0">
      <p className="font-medium text-foreground truncate">{ev.title}</p>
     {ev.metadata && <p className="text-muted-foreground truncate">{ev.metadata}</p>}
          </div>
    </div>
            ))}
          </div>
        </div>
  )}

      {/* Deadlines + My Tasks side-by-side */}
   <div className="grid gap-4 lg:grid-cols-2">
        {/* Upcoming Deadlines */}
        <div className="rounded-xl border border-border bg-card">
  <div className="flex items-center gap-2 border-b border-border px-4 py-3">
            <CalendarDays className="h-4 w-4 text-chart-1" />
      <h3 className="text-sm font-semibold text-foreground">{t.dashboard.upcomingDeadlines}</h3>
          </div>
          <div className="divide-y divide-border">
            {data.upcomingDeadlines.length === 0 ? (
      <p className="px-4 py-6 text-center text-sm text-muted-foreground">{t.dashboard.noDeadlines}</p>
   ) : (
         data.upcomingDeadlines.slice(0, 6).map((dl, i) => {
      const d = new Date(dl.deadlineDate);
    const isPast = d < new Date();
            return (
            <div
     key={i}
     className="flex items-center gap-3 px-4 py-2.5 hover:bg-accent/50 cursor-pointer transition-colors"
         onClick={() => navigate(`/cases/${dl.caseId}`)}
       >
        <div className={`text-center leading-none ${isPast ? "text-destructive" : "text-foreground"}`}>
      <div className="text-lg font-bold">{format(d, "dd")}</div>
          <div className="text-[10px] uppercase">{format(d, "MMM")}</div>
      </div>
          <div className="min-w-0 flex-1">
      <p className="text-sm font-medium text-foreground truncate">{dl.caseNumber}</p>
   <p className="text-xs text-muted-foreground truncate">{dl.debtorName}</p>
            </div>
      <Badge variant={isPast ? "destructive" : "secondary"} className="shrink-0 text-[10px]">{dl.deadlineType}</Badge>
         </div>
    );
        })
  )}
          </div>
        </div>

        {/* My Tasks */}
        <div className="rounded-xl border border-border bg-card">
          <div className="flex items-center justify-between border-b border-border px-4 py-3">
            <h3 className="text-sm font-semibold text-foreground">{t.dashboard.myTasks}</h3>
            <Button variant="ghost" size="sm" className="text-xs text-primary hover:text-primary" onClick={() => navigate("/tasks")}>{t.common.viewAll}</Button>
          </div>
          <div className="divide-y divide-border">
            {data.recentTasks.filter(t => t.status !== "blocked" && t.status !== "inProgress").length === 0 ? (
              <p className="px-4 py-6 text-center text-sm text-muted-foreground">{t.dashboard.noTasks}</p>
            ) : (
              data.recentTasks.filter(t => t.status !== "blocked" && t.status !== "inProgress").slice(0, 6).map(task => (
                <div
                  key={task.id}
                  className="flex items-center gap-3 px-4 py-2.5 hover:bg-accent/50 cursor-pointer transition-colors"
                  onClick={() => navigate(`/companies/${task.companyId}`)}
                >
                  <div className="min-w-0 flex-1">
                    <p className="text-sm font-medium text-foreground truncate">{task.title}</p>
                    <p className="text-xs text-muted-foreground">{task.companyName ?? ""}</p>
                  </div>
                  <Badge variant={task.status === "done" ? "success" : "default"} className="shrink-0 text-[10px]">{task.status}</Badge>
                  {task.deadline && <span className="text-xs text-muted-foreground shrink-0">{format(new Date(task.deadline), "dd MMM")}</span>}
                </div>
              ))
            )}
          </div>
        </div>
      </div>

      {/* Blocked + In-Progress task widgets */}
      {(() => {
        const blocked = data.recentTasks.filter(t => t.status === "blocked");
        const inProg = data.recentTasks.filter(t => t.status === "inProgress");
        if (blocked.length === 0 && inProg.length === 0) return null;
        return (
          <div className="grid gap-4 lg:grid-cols-2">
            {blocked.length > 0 && (
              <div className="rounded-xl border border-destructive/20 bg-card">
                <div className="flex items-center gap-2 border-b border-destructive/20 px-4 py-3">
                  <Ban className="h-4 w-4 text-destructive" />
                  <h3 className="text-sm font-semibold text-foreground">{t.tasks.blockedTasks}</h3>
                  <span className="ml-auto text-xs font-bold text-destructive">{blocked.length}</span>
                </div>
                <div className="divide-y divide-border">
                  {blocked.map(task => (
                    <div
                      key={task.id}
                      className="flex items-center gap-3 px-4 py-2.5 hover:bg-accent/50 cursor-pointer transition-colors"
                      onClick={() => navigate(`/companies/${task.companyId}`)}
                    >
                      <div className="min-w-0 flex-1">
                        <p className="text-sm font-medium text-foreground truncate">{task.title}</p>
                        <p className="text-xs text-muted-foreground">
                          {task.companyName ?? ""}
                          {(task as unknown as Record<string, string>).blockReason ? ` \u00b7 ${(task as unknown as Record<string, string>).blockReason}` : ""}
                        </p>
                      </div>
                      {task.deadline && <span className="text-xs text-destructive shrink-0">{format(new Date(task.deadline), "dd MMM")}</span>}
                    </div>
                  ))}
                </div>
              </div>
            )}
            {inProg.length > 0 && (
              <div className="rounded-xl border border-amber-500/20 bg-card">
                <div className="flex items-center gap-2 border-b border-amber-500/20 px-4 py-3">
                  <AlertTriangle className="h-4 w-4 text-amber-500" />
                  <h3 className="text-sm font-semibold text-foreground">{t.tasks.inProgressTasks}</h3>
                  <span className="ml-auto text-xs font-bold text-amber-500">{inProg.length}</span>
                </div>
                <div className="divide-y divide-border">
                  {inProg.map(task => (
                    <div
                      key={task.id}
                      className="flex items-center gap-3 px-4 py-2.5 hover:bg-accent/50 cursor-pointer transition-colors"
                      onClick={() => navigate(`/companies/${task.companyId}`)}
                    >
                      <div className="min-w-0 flex-1">
                        <p className="text-sm font-medium text-foreground truncate">{task.title}</p>
                        <p className="text-xs text-muted-foreground">{task.companyName ?? ""}</p>
                      </div>
                      {task.deadline && <span className="text-xs text-amber-500 shrink-0">{format(new Date(task.deadline), "dd MMM")}</span>}
                    </div>
                  ))}
                </div>
              </div>
            )}
          </div>
        );
      })()}
    </div>
  );
}

