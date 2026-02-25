import { useState, useEffect, useCallback } from "react";
import { caseEventsApi, type CaseEventDto } from "@/services/api/caseEvents";
import { Loader2, RefreshCw, FileText, CheckSquare, GitBranch, Bell, Zap, Info, AlertTriangle, AlertCircle } from "lucide-react";
import { formatDistanceToNow, parseISO } from "date-fns";
import { Button } from "@/components/ui/button";

const CATEGORY_ICONS: Record<string, React.ElementType> = {
  Document: FileText,
  Task: CheckSquare,
  Phase: GitBranch,
  Deadline: Bell,
  System: Zap,
};

const SEVERITY_STYLES: Record<string, string> = {
  Info: "bg-blue-100 text-blue-700 dark:bg-blue-900/30 dark:text-blue-400",
  Warning: "bg-amber-100 text-amber-700 dark:bg-amber-900/30 dark:text-amber-400",
  Critical: "bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-400",
  Success: "bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-400",
};

const SEVERITY_ICONS: Record<string, React.ElementType> = {
  Info: Info,
  Warning: AlertTriangle,
  Critical: AlertCircle,
  Success: CheckSquare,
};

const CATEGORY_COLORS: Record<string, string> = {
  Document: "bg-indigo-100 text-indigo-600 dark:bg-indigo-900/30 dark:text-indigo-400",
  Task: "bg-orange-100 text-orange-600 dark:bg-orange-900/30 dark:text-orange-400",
  Phase: "bg-primary/10 text-primary",
  Deadline: "bg-rose-100 text-rose-600 dark:bg-rose-900/30 dark:text-rose-400",
  System: "bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-400",
};

function EventCard({ event }: { event: CaseEventDto }) {
  const CategoryIcon = CATEGORY_ICONS[event.category] ?? Info;
  const SeverityIcon = SEVERITY_ICONS[event.severity] ?? Info;
  const catColor = CATEGORY_COLORS[event.category] ?? "bg-muted text-muted-foreground";
  const sevStyle = SEVERITY_STYLES[event.severity] ?? SEVERITY_STYLES.Info;

  return (
    <div className="flex gap-3 py-3 border-b border-border last:border-0">
      {/* Icon dot */}
      <div className={`mt-0.5 flex h-7 w-7 shrink-0 items-center justify-center rounded-full ${catColor}`}>
        <CategoryIcon className="h-3.5 w-3.5" />
      </div>

      {/* Content */}
      <div className="min-w-0 flex-1">
        <div className="flex items-start justify-between gap-2">
          <p className="text-sm text-foreground leading-snug">{event.description}</p>
          <div className={`flex items-center gap-1 shrink-0 rounded-full px-1.5 py-0.5 text-[10px] font-medium ${sevStyle}`}>
            <SeverityIcon className="h-2.5 w-2.5" />
            {event.severity}
          </div>
        </div>

        <div className="mt-1 flex flex-wrap items-center gap-x-3 gap-y-0.5">
          <span className="text-[10px] text-muted-foreground">
            {formatDistanceToNow(parseISO(event.occurredAt), { addSuffix: true })}
          </span>
          {event.actorName && (
            <span className="text-[10px] text-muted-foreground">by {event.actorName}</span>
          )}
          <span className="rounded bg-muted px-1 py-0.5 text-[10px] font-mono text-muted-foreground">
            {event.eventType}
          </span>
        </div>

        {event.documentSummary && (
          <p className="mt-1.5 text-xs text-muted-foreground line-clamp-2 italic">
            "{event.documentSummary}"
          </p>
        )}
      </div>
    </div>
  );
}

const CATEGORIES = ["All", "Document", "Task", "Phase", "Deadline", "System"];

interface Props {
  caseId: string;
}

export default function CaseEventFeed({ caseId }: Props) {
  const [events, setEvents] = useState<CaseEventDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [category, setCategory] = useState("All");
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(1);
  const PAGE_SIZE = 25;

  const load = useCallback(async (pg = 1, cat = category) => {
    setLoading(true);
    try {
      const r = await caseEventsApi.get(caseId, pg, PAGE_SIZE, cat === "All" ? undefined : cat);
      if (pg === 1) {
        setEvents(r.data.items);
      } else {
        setEvents(prev => [...prev, ...r.data.items]);
      }
      setTotalCount(r.data.totalCount);
      setPage(pg);
    } catch {
      // fail silently
    } finally {
      setLoading(false);
    }
  }, [caseId, category]);

  useEffect(() => { load(1, category); }, [caseId, category]);

  const handleCategoryChange = (cat: string) => {
    setCategory(cat);
    setPage(1);
  };

  return (
    <div className="space-y-3">
      {/* Category filter + refresh */}
      <div className="flex items-center justify-between gap-2 flex-wrap">
        <div className="flex gap-1 flex-wrap">
          {CATEGORIES.map(cat => (
            <button
              key={cat}
              onClick={() => handleCategoryChange(cat)}
              className={`rounded-full px-3 py-1 text-xs font-medium transition-colors ${
                category === cat
                  ? "bg-primary text-primary-foreground"
                  : "bg-muted text-muted-foreground hover:bg-accent hover:text-foreground"
              }`}
            >
              {cat}
            </button>
          ))}
        </div>
        <Button variant="ghost" size="sm" className="h-7 px-2 text-xs gap-1" onClick={() => load(1, category)}>
          <RefreshCw className="h-3 w-3" />
          Refresh
        </Button>
      </div>

      {/* Event list */}
      <div className="rounded-xl border border-border bg-card px-4">
        {loading && events.length === 0 ? (
          <div className="flex items-center justify-center py-10">
            <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
          </div>
        ) : events.length === 0 ? (
          <div className="py-10 text-center">
            <p className="text-sm text-muted-foreground">No activity recorded yet.</p>
          </div>
        ) : (
          <>
            {events.map(event => (
              <EventCard key={event.id} event={event} />
            ))}
            {/* Load more */}
            {events.length < totalCount && (
              <div className="py-3 text-center">
                <Button
                  variant="outline"
                  size="sm"
                  className="text-xs gap-1"
                  onClick={() => load(page + 1, category)}
                  disabled={loading}
                >
                  {loading ? <Loader2 className="h-3 w-3 animate-spin" /> : null}
                  Load more ({totalCount - events.length} remaining)
                </Button>
              </div>
            )}
          </>
        )}
      </div>
    </div>
  );
}
