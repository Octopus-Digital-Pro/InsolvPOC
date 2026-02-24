import { useState, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { companiesApi } from "@/services/api";
import { useTranslation } from "@/contexts/LanguageContext";
import type { CompanyDto } from "@/services/api/types";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Loader2, Search, Building2, Plus, Landmark, Scale, DollarSign, Flag, MoreHorizontal, Download } from "lucide-react";
import { downloadAuthFile } from "@/utils/downloadAuthFile";

type CompanyTab = "all" | "Debtor" | "InsolvencyPractitioner" | "Creditor" | "Court" | "GovernmentAgency" | "Other";

const TAB_CONFIG: { id: CompanyTab; icon: React.ElementType; color: string }[] = [
  { id: "all", icon: Building2, color: "" },
  { id: "Debtor", icon: Building2, color: "text-red-500" },
  { id: "InsolvencyPractitioner", icon: Scale, color: "text-blue-500" },
  { id: "Creditor", icon: DollarSign, color: "text-amber-500" },
  { id: "Court", icon: Landmark, color: "text-purple-500" },
  { id: "GovernmentAgency", icon: Flag, color: "text-green-500" },
  { id: "Other", icon: MoreHorizontal, color: "text-muted-foreground" },
];

export default function CompaniesPage() {
  const navigate = useNavigate();
  const { t } = useTranslation();
  const [companies, setCompanies] = useState<CompanyDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState("");
  const [tab, setTab] = useState<CompanyTab>("all");

  useEffect(() => {
    companiesApi.getAll()
      .then(res => setCompanies(res.data))
      .catch(err => console.error("Failed to load companies:", err))
      .finally(() => setLoading(false));
  }, []);

  const filtered = companies.filter(c => {
    if (tab !== "all" && c.companyType !== tab) return false;
    if (!search) return true;
    const q = search.toLowerCase();
    return c.name.toLowerCase().includes(q) ||
      (c.cuiRo ?? "").toLowerCase().includes(q) ||
      (c.county ?? "").toLowerCase().includes(q);
  });

  const typeCounts = companies.reduce<Record<string, number>>((acc, c) => {
    acc[c.companyType ?? "Other"] = (acc[c.companyType ?? "Other"] || 0) + 1;
    return acc;
  }, {});

  const typeLabel = (type: CompanyTab): string => {
    if (type === "all") return t.common.all;
    return (t.partyRoles as Record<string, string>)[type.charAt(0).toLowerCase() + type.slice(1)]
      ?? type.replace(/([A-Z])/g, " $1").trim();
  };

  const typeBadgeVariant = (type: string | undefined): "default" | "destructive" | "success" | "secondary" | "warning" => {
    switch (type) {
      case "Debtor": return "destructive";
      case "InsolvencyPractitioner": return "default";
      case "Creditor": return "warning";
      case "Court": return "secondary";
      default: return "secondary";
    }
  };

  if (loading) return <div className="flex h-full items-center justify-center"><Loader2 className="h-8 w-8 animate-spin text-muted-foreground" /></div>;

  return (
    <div className="mx-auto max-w-6xl space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-bold text-foreground">{t.companies.title}</h1>
        <div className="flex gap-2">
          <Button variant="outline" size="sm" className="gap-1.5" onClick={() => downloadAuthFile(companiesApi.exportCsvUrl, "companies.csv")}>
            <Download className="h-3.5 w-3.5" />{t.common.export ?? "Export CSV"}
          </Button>
          <Button size="sm" className="gap-1.5 bg-primary hover:bg-primary/90" onClick={() => navigate("/companies/new")}>
            <Plus className="h-3.5 w-3.5" />{t.companies.addCompany}
          </Button>
        </div>
      </div>

      {/* Type tabs */}
      <div className="flex gap-1 rounded-lg border border-border bg-card p-1 overflow-x-auto">
        {TAB_CONFIG.map(tc => {
          const count = tc.id === "all" ? companies.length : (typeCounts[tc.id] ?? 0);
          const Icon = tc.icon;
          return (
            <button
              key={tc.id}
              onClick={() => setTab(tc.id)}
              className={`flex items-center gap-1.5 rounded-md px-3 py-1.5 text-xs font-medium transition-colors whitespace-nowrap
       ${tab === tc.id ? "bg-primary text-primary-foreground" : "text-muted-foreground hover:bg-accent hover:text-foreground"}`}
            >
              <Icon className={`h-3.5 w-3.5 ${tab === tc.id ? "" : tc.color}`} />
              {typeLabel(tc.id)}
              <span className={`ml-0.5 text-[10px] ${tab === tc.id ? "text-primary-foreground/70" : "text-muted-foreground/60"}`}>
                {count}
              </span>
            </button>
          );
        })}
      </div>

      <div className="relative">
        <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
        <input
          type="text"
          placeholder={t.companies.searchPlaceholder}
          value={search}
          onChange={e => setSearch(e.target.value)}
          className="w-full rounded-lg border border-input bg-background py-2 pl-9 pr-3 text-sm focus:outline-none focus:ring-2 focus:ring-ring"
        />
      </div>

      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
        {filtered.length === 0 ? (
          <div className="sm:col-span-2 lg:col-span-3 flex flex-col items-center justify-center py-12 text-muted-foreground">
            <Building2 className="h-10 w-10 mb-2 opacity-30" />
            <p className="text-sm">{t.companies.noCompanies}</p>
          </div>
        ) : (
          filtered.map(c => (
            <div
              key={c.id}
              onClick={() => navigate(`/companies/${c.id}`)}
              className="rounded-xl border border-border bg-card p-4 cursor-pointer hover:border-primary/30 hover:shadow-sm transition-all"
            >
              <div className="flex items-start justify-between gap-2">
                <p className="text-sm font-semibold text-foreground truncate flex-1">{c.name}</p>
                <Badge variant={typeBadgeVariant(c.companyType)} className="text-[10px] shrink-0">
                  {c.companyType ?? "Other"}
                </Badge>
              </div>
              {c.cuiRo && <p className="mt-0.5 text-xs text-muted-foreground">{t.companies.cuiRo}: {c.cuiRo}</p>}
              {c.address && <p className="text-xs text-muted-foreground truncate">{c.address}</p>}
              <div className="mt-2 flex items-center justify-between text-xs text-muted-foreground">
                <span>{c.caseCount} {t.companies.casesCount}</span>
                {c.assignedToName && <span className="truncate ml-2">{c.assignedToName}</span>}
              </div>
            </div>
          ))
        )}
      </div>
    </div>
  );
}
