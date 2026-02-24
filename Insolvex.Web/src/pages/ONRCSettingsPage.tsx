import { useState, useEffect, useCallback } from "react";
import { useTranslation } from "@/contexts/LanguageContext";
import { onrcApi } from "@/services/api/onrc";
import type { ONRCFirmResult, ONRCDatabaseStats, ONRCImportResult } from "@/services/api/onrc";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Database, Upload, Loader2, Search, Building2,
  RefreshCw, AlertCircle, Check, MapPin,
} from "lucide-react";
import { format } from "date-fns";

const REGIONS = ["Romania", "Hungary"] as const;

export default function ONRCSettingsPage() {
  const { t } = useTranslation();
  const [region, setRegion] = useState<string>("Romania");
  const [stats, setStats] = useState<ONRCDatabaseStats | null>(null);
  const [loading, setLoading] = useState(true);

  // Import state
  const [csvFile, setCsvFile] = useState<File | null>(null);
  const [importing, setImporting] = useState(false);
  const [importResult, setImportResult] = useState<ONRCImportResult | null>(null);

  // Search state
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

  const handleImport = async () => {
    if (!csvFile) return;
    setImporting(true);
    setImportResult(null);
    try {
      const r = await onrcApi.importCsv(csvFile, region);
      setImportResult(r.data);
      setCsvFile(null);
      loadStats();
    } catch (err) { console.error(err); }
    finally { setImporting(false); }
  };

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
   <h1 className="text-lg font-bold text-foreground">{t.settings.onrcDatabase}</h1>
          <p className="text-xs text-muted-foreground mt-0.5">{t.settings.onrcDatabaseDesc}</p>
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
     key={r}
       onClick={() => setRegion(r)}
         className={`flex items-center gap-2 rounded-lg border px-4 py-2.5 text-sm font-medium transition-all ${
   region === r
           ? "border-primary bg-primary/5 text-primary ring-1 ring-primary/20"
      : "border-border text-foreground hover:border-primary/30 hover:bg-accent/30"
}`}
    >
        <span className="text-lg">{r === "Romania" ? "????" : "????"}</span>
   {r === "Romania" ? t.settings.regionRomania : t.settings.regionHungary}
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

      {/* CSV Import */}
      <div className="rounded-xl border border-border bg-card p-4 space-y-3">
  <div className="flex items-center gap-2">
        <Upload className="h-4 w-4 text-primary" />
          <h2 className="text-sm font-semibold text-foreground">{t.settings.onrcUpload}</h2>
        </div>
        <p className="text-xs text-muted-foreground">
          Upload a CSV file exported from the ONRC registry. Columns: CUI, Denumire, Nr. Reg. Com., CAEN, Adresa, Localitate, Judet, Stare, etc.
        </p>
        <div className="flex items-center gap-2">
   <input
        type="file"
 accept=".csv"
            onChange={e => setCsvFile(e.target.files?.[0] ?? null)}
        className="flex-1 rounded-md border border-input bg-background px-3 py-2 text-sm file:mr-2 file:rounded file:border-0 file:bg-primary/10 file:px-2 file:py-1 file:text-xs file:font-medium file:text-primary"
          />
 <Button size="sm" className="gap-1" onClick={handleImport} disabled={importing || !csvFile}>
            {importing ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Upload className="h-3.5 w-3.5" />}
     {t.common.import}
     </Button>
  </div>
     {importResult && (
       <div className="rounded bg-emerald-50 dark:bg-emerald-950 border border-emerald-200 dark:border-emerald-800 p-3 space-y-1">
            <div className="flex items-center gap-2 text-xs text-emerald-700 dark:text-emerald-300">
   <Check className="h-3.5 w-3.5" />
   {t.settings.onrcImportSuccess}
            </div>
      <p className="text-xs text-emerald-600 dark:text-emerald-400">
  {t.common.imported}: {importResult.imported} new, {importResult.updated} updated
     ({importResult.totalRows} rows, {importResult.skipped} skipped)
  </p>
      {importResult.errors.length > 0 && (
              <div className="mt-2 space-y-1">
    <p className="text-xs text-destructive flex items-center gap-1">
           <AlertCircle className="h-3 w-3" />
   {importResult.errors.length} {t.common.errorsOccurred}
       </p>
              <div className="max-h-32 overflow-y-auto text-[10px] text-destructive/80 font-mono">
   {importResult.errors.slice(0, 20).map((e, i) => <div key={i}>{e}</div>)}
            {importResult.errors.length > 20 && <div>... and {importResult.errors.length - 20} more</div>}
       </div>
              </div>
            )}
       </div>
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
         {firm.tradeRegisterNo && ` · ${firm.tradeRegisterNo}`}
          {firm.county && ` · ${firm.county}`}
    {firm.locality && `, ${firm.locality}`}
     </p>
                </div>
           {firm.status && (
         <Badge
     variant={firm.status.toUpperCase() === "ACTIV" ? "success" : "secondary"}
  className="text-[10px] shrink-0"
       >
           {firm.status}
        </Badge>
      )}
             {firm.caen && (
    <span className="text-[10px] text-muted-foreground shrink-0">CAEN: {firm.caen}</span>
                )}
      </div>
 ))}
          </div>
 )}
      </div>
    </div>
  );
}
