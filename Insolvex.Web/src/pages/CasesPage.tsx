import { useState, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { casesApi } from "@/services/api";
import { useTranslation } from "@/contexts/LanguageContext";
import type { CaseDto } from "@/services/api/types";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Loader2, Search, Briefcase, Plus } from "lucide-react";
import { format } from "date-fns";

const STAGE_VARIANT: Record<string, "default" | "secondary" | "success" | "warning" | "destructive" | "outline"> = {
  opened: "default",
  claimsWindow: "warning",
  preliminaryTable: "secondary",
  definitiveTable: "secondary",
  liquidation: "destructive",
  closed: "success",
  unknown: "outline",
};

export default function CasesPage() {
  const navigate = useNavigate();
  const { t } = useTranslation();
  const [cases, setCases] = useState<CaseDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState("");

  useEffect(() => {
    casesApi.getAll()
      .then(res => setCases(res.data))
      .catch(err => console.error("Failed to load cases:", err))
      .finally(() => setLoading(false));
  }, []);

  const stageLabel = (s: string): string => {
 return (t.stages as Record<string, string>)[s] ?? s.replace(/([A-Z])/g, " $1").trim();
  };

  const filtered = cases.filter(c =>
    !search ||
    c.caseNumber.toLowerCase().includes(search.toLowerCase()) ||
    c.debtorName.toLowerCase().includes(search.toLowerCase()) ||
 (c.companyName ?? "").toLowerCase().includes(search.toLowerCase())
  );

  if (loading) return <div className="flex h-full items-center justify-center"><Loader2 className="h-8 w-8 animate-spin text-muted-foreground" /></div>;

return (
 <div className="mx-auto max-w-6xl space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-bold text-foreground">{t.cases.title}</h1>
        <Button size="sm" className="gap-1.5 bg-primary hover:bg-primary/90" onClick={() => navigate("/cases/new")}>
          <Plus className="h-3.5 w-3.5" />
          {t.cases.newCase.replace("+ ", "")}
 </Button>
  </div>

      {/* Search */}
      <div className="relative">
     <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
    <input
      type="text"
       placeholder={t.cases.searchPlaceholder}
     value={search}
          onChange={e => setSearch(e.target.value)}
        className="w-full rounded-lg border border-input bg-background py-2 pl-9 pr-3 text-sm focus:outline-none focus:ring-2 focus:ring-ring"
        />
      </div>

      {/* Cases list */}
      <div className="rounded-xl border border-border bg-card divide-y divide-border">
        {filtered.length === 0 ? (
     <div className="flex flex-col items-center justify-center py-12 text-muted-foreground">
  <Briefcase className="h-10 w-10 mb-2 opacity-30" />
            <p className="text-sm">{t.cases.noCases}</p>
    </div>
        ) : (
          filtered.map(c => (
            <div
     key={c.id}
      onClick={() => navigate(`/cases/${c.id}`)}
        className="flex items-center gap-4 px-4 py-3 cursor-pointer hover:bg-accent/50 transition-colors"
    >
   <div className="min-w-0 flex-1">
     <div className="flex items-center gap-2">
         <p className="text-sm font-semibold text-foreground truncate">{c.caseNumber}</p>
         <Badge variant={STAGE_VARIANT[c.stage] ?? "outline"} className="text-[10px] shrink-0">
    {stageLabel(c.stage)}
      </Badge>
       </div>
    <p className="text-xs text-muted-foreground truncate">
 {c.debtorName}{c.companyName ? ` · ${c.companyName}` : ""}
     </p>
       </div>
      <div className="hidden sm:flex flex-col items-end gap-0.5 shrink-0 text-right">
           {c.nextHearingDate && (
      <span className="text-[10px] text-muted-foreground">
      {t.cases.hearing}: {format(new Date(c.nextHearingDate), "dd MMM yyyy")}
            </span>
  )}
      <span className="text-[10px] text-muted-foreground">{c.documentCount} {t.cases.docs}</span>
  </div>
            </div>
       ))
        )}
      </div>
    </div>
  );
}
