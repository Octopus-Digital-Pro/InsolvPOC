import { useState, useEffect, useCallback } from "react";
import { useTranslation } from "@/contexts/LanguageContext";
import { onrcApi } from "@/services/api/onrc";
import type { ONRCFirmResult, ONRCDatabaseStats, ONRCImportResult } from "@/services/api/onrc";
import CsvUploadModal from "@/components/CsvUploadModal";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Database, Upload, Loader2, Search, Building2,
  RefreshCw, MapPin,
} from "lucide-react";
import { format } from "date-fns";

const REGIONS = [
  { value: "Romania", label: "Rom\u00e2nia", flag: "\u{1F1F7}\u{1F1F4}" },
  { value: "Hungary", label: "Magyarorsz\u00e1g", flag: "\u{1F1ED}\u{1F1FA}" },
] as const;

const ONRC_CSV_COLUMNS = [
  { name: "CUI", required: true, description: "Codul unic de inregistrare", example: "RO12345678" },
  { name: "Denumire", required: true, description: "Numele firmei", example: "SC ACME SRL" },
  { name: "Nr. Reg. Com.", description: "Numarul de inregistrare la Registrul Comertului", example: "J40/1234/2005" },
  { name: "CAEN", description: "Codul CAEN", example: "6201" },
  { name: "Adresa", description: "Adresa firmei", example: "Str. Unirii 1" },
  { name: "Localitate", description: "Localitatea", example: "Bucuresti" },
  { name: "Judet", description: "Judetul", example: "Ilfov" },
  { name: "Cod Postal", description: "Codul postal", example: "040042" },
  { name: "Telefon", description: "Numarul de telefon", example: "+40 21 300 0000" },
  { name: "Stare", description: "Starea (ACTIV/INACTIV)", example: "ACTIV" },
  { name: "An Infiintare", description: "Anul infiintarii", example: "2005" },
  { name: "Capital Social", description: "Capitalul social (RON)", example: "200.00" },
];

export default function ONRCSettingsPage() {
  const { t } = useTranslation();
  const [region, setRegion] = useState<string>("Romania");
  const [stats, setStats] = useState<ONRCDatabaseStats | null>(null);
  const [loading, setLoading] = useState(true);
  const [showImportModal, setShowImportModal] = useState(false);

  const [query, setQuery] = useState("");
  const [results, setResults] = useState<ONRCFirmResult[]>([]);
  const [searching, setSearching] = useState(false);

  const loadStats = useCallback(async () => {
    setLoading(true);
    try {
      const r = await onrcApi.getStats(region);
  setStats(r.data);
    } catch (err) { console.error(err); }
    finally { setLoading(false); }
  }, [region]);

  useEffect(() => { loadStats(); }, [loadStats]);

  const handleSearch = async () => {
    if (!query.trim()) return;
    setSearching(true);
    try {
      const r = await onrcApi.search(query, region, 20);
      setResults(r.data);
    } catch (err) { console.error(err); }
    finally { setSearching(false); }
  };

  return (
    <div className="mx-auto max-w-4xl space-y-5">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-lg font-bold text-foreground">{t.settings.firmsDatabase}</h1>
          <p className="text-xs text-muted-foreground mt-0.5">{t.settings.firmsDatabaseDesc}</p>
        </div>
 <Button variant="ghost" size="icon" className="h-8 w-8" onClick={loadStats}>
          <RefreshCw className="h-4 w-4" />
        </Button>
      </div>

      {/* Region selector */}
      <div className="rounded-xl border border-border bg-card p-4">
  <div className="flex items-center gap-2 mb-3">
          <MapPin className="h-4 w-4 text-primary" />
   <h2 className="text-sm font-semibold text-foreground">{t.settings.region}</h2>
        </div>
    <div className="flex gap-2">
        {REGIONS.map(r => (
   <button
          key={r.value}
          onClick={() => setRegion(r.value)}
    className={`flex items-center gap-2 rounded-lg border px-4 py-2.5 text-sm font-medium transition-all ${
     region === r.value
           ? "border-primary bg-primary/5 text-primary ring-1 ring-primary/20"
    : "border-border text-foreground hover:border-primary/30 hover:bg-accent/30"
         }`}
            >
   <span className="text-lg">{r.flag}</span>
              {r.value === "Romania" ? t.settings.regionRomania : t.settings.regionHungary}
      </button>
          ))}
        </div>
      </div>

      {/* Stats */}
  <div className="rounded-xl border border-border bg-card p-4">
        <div className="flex items-center gap-2 mb-3">
    <Database className="h-4 w-4 text-primary" />
    <h2 className="text-sm font-semibold text-foreground">{t.settings.onrcStats}</h2>
      </div>
        {loading ? (
          <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
        ) : stats && stats.totalRecords > 0 ? (
        <div className="grid grid-cols-2 gap-4 text-sm">
            <div>
         <span className="text-muted-foreground">{t.settings.onrcTotalRecords}</span>
              <p className="font-semibold text-foreground text-lg">{stats.totalRecords.toLocaleString()}</p>
         </div>
            <div>
          <span className="text-muted-foreground">{t.settings.onrcLastImport}</span>
           <p className="font-medium text-foreground">
    {stats.lastImportedAt ? format(new Date(stats.lastImportedAt), "dd MMM yyyy HH:mm") : "—"}
       </p>
     </div>
     </div>
        ) : (
   <p className="text-sm text-muted-foreground">{t.settings.onrcNoData}</p>
        )}
      </div>

      {/* CSV Import — now triggers modal */}
      <div className="rounded-xl border border-border bg-card p-4">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
   <Upload className="h-4 w-4 text-primary" />
     <h2 className="text-sm font-semibold text-foreground">{t.settings.onrcUpload}</h2>
     </div>
          <Button size="sm" className="gap-1.5" onClick={() => setShowImportModal(true)} disabled={region !== "Romania"}>
            <Upload className="h-3.5 w-3.5" />
            Upload CSV
    </Button>
        </div>
        {region === "Romania" ? (
          <p className="text-xs text-muted-foreground mt-2">
            Fișier CSV exportat din portalul ONRC open-data. Delimitator: <code className="font-mono bg-muted px-1 rounded">^</code> (caret). Fișiere până la 700 MB.
          </p>
        ) : (
          <p className="text-xs text-amber-600 dark:text-amber-400 mt-2">
            Import not yet available for this region. Only the Romanian ONRC format is currently supported.
          </p>
        )}
      </div>

      {/* Search */}
      <div className="rounded-xl border border-border bg-card p-4 space-y-3">
        <div className="flex items-center gap-2">
        <Search className="h-4 w-4 text-primary" />
      <h2 className="text-sm font-semibold text-foreground">{t.common.search}</h2>
        </div>
      <div className="flex gap-2">
 <input
 value={query}
      onChange={e => setQuery(e.target.value)}
          onKeyDown={e => e.key === "Enter" && handleSearch()}
        placeholder={t.settings.onrcSearchPlaceholder}
    className="flex-1 rounded-md border border-input bg-background px-3 py-2 text-sm"
          />
          <Button size="sm" onClick={handleSearch} disabled={searching || !query.trim()}>
 {searching ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Search className="h-3.5 w-3.5" />}
     </Button>
        </div>
        {results.length > 0 && (
       <div className="rounded-lg border border-border divide-y divide-border max-h-96 overflow-y-auto">
        {results.map(firm => (
    <div key={firm.id} className="flex items-center gap-3 px-4 py-2.5 hover:bg-muted/30 transition-colors">
        <Building2 className="h-4 w-4 text-muted-foreground shrink-0" />
       <div className="min-w-0 flex-1">
      <p className="text-sm font-medium text-foreground truncate">{firm.name}</p>
        <p className="text-xs text-muted-foreground truncate">
                  CUI: {firm.cui}
    {firm.tradeRegisterNo && ` — ${firm.tradeRegisterNo}`}
         {firm.county && ` — ${firm.county}`}
            {firm.locality && `, ${firm.locality}`}
      </p>
       </div>
                {firm.status && (
  <Badge variant={firm.status.toUpperCase() === "ACTIV" ? "success" : "secondary"} className="text-[10px] shrink-0">
    {firm.status}
    </Badge>
 )}
      {firm.caen && <span className="text-[10px] text-muted-foreground shrink-0">CAEN: {firm.caen}</span>}
    </div>
         ))}
     </div>
   )}
      </div>

      {/* CSV Import Modal */}
 {showImportModal && (
        <CsvUploadModal
          title="Import ONRC Firms Database (România)"
          description="Fișier CSV oficial ONRC open-data. Delimitator: '^' (caret), encoding UTF-8. Fișiere până la 700 MB sunt suportate. Un import de ~600 MB (≈ 1–3 milioane rânduri) poate dura câteva minute."
          delimiter="^"
        columns={ONRC_CSV_COLUMNS}
    templateFilename="onrc_firms"
          onImport={async (file, onProgress) => {
         const r = await onrcApi.importCsv(file, region, onProgress);
            loadStats();
   const d = r.data as ONRCImportResult;
            return { imported: (d.imported ?? 0) + (d.updated ?? 0), errors: d.errors ?? [] };
          }}
          onClose={() => setShowImportModal(false)}
/>
      )}
    </div>
  );
}
