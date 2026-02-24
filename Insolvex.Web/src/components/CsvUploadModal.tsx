import { useState, useRef } from "react";
import { Button } from "@/components/ui/button";
import { Upload, X, Download, FileText, AlertCircle, Check, Loader2 } from "lucide-react";

export interface CsvColumn {
  name: string;
  required?: boolean;
  description?: string;
  example?: string;
}

interface Props {
  /** Modal title, e.g. "Import Tribunals" */
  title: string;
  /** Short description shown above the format guide */
  description?: string;
  /** Column definitions for the format guide + template download */
  columns: CsvColumn[];
  /** Filename used when downloading the template, without extension */
  templateFilename?: string;
  /** Called with the chosen file when user clicks Import */
  onImport: (file: File) => Promise<{ imported: number; errors: string[] }>;
  onClose: () => void;
}

/**
 * Reusable CSV upload modal.
 * – Shows the expected CSV format (columns + examples)
 * – Lets user download an empty template CSV
 * – Handles file pick + upload + result display
 */
export default function CsvUploadModal({
  title,
  description,
  columns,
  templateFilename = "template",
  onImport,
  onClose,
}: Props) {
  const [file, setFile] = useState<File | null>(null);
  const [importing, setImporting] = useState(false);
  const [result, setResult] = useState<{ imported: number; errors: string[] } | null>(null);
  const [error, setError] = useState<string | null>(null);
  const fileRef = useRef<HTMLInputElement>(null);

  /** Build and trigger download of a header-only CSV template */
  const downloadTemplate = () => {
 const header = columns.map(c => c.name).join(",");
    const exampleRow = columns.map(c => c.example ?? "").join(",");
    const csv = `${header}\n${exampleRow}`;
    const blob = new Blob([csv], { type: "text/csv" });
 const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = `${templateFilename}_template.csv`;
a.click();
    URL.revokeObjectURL(url);
  };

  const handleImport = async () => {
    if (!file) return;
    setImporting(true);
    setError(null);
    try {
  const res = await onImport(file);
      setResult(res);
    } catch (err: unknown) {
      const axErr = err as { response?: { data?: { message?: string } } };
      setError(axErr?.response?.data?.message ?? "Import failed");
    } finally {
      setImporting(false);
    }
  };

  const done = result !== null;

  return (
  <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4"
      onClick={onClose}
    >
      <div
className="w-full max-w-xl bg-card border border-border rounded-xl shadow-2xl flex flex-col max-h-[90vh]"
    onClick={e => e.stopPropagation()}
      >
      {/* Header */}
        <div className="flex items-center justify-between px-5 py-4 border-b border-border shrink-0">
          <div className="flex items-center gap-2">
            <Upload className="h-4 w-4 text-primary" />
          <h2 className="text-sm font-semibold text-foreground">{title}</h2>
</div>
          <button
            onClick={onClose}
   className="rounded-md p-1 text-muted-foreground hover:bg-accent transition-colors"
    >
          <X className="h-4 w-4" />
      </button>
        </div>

        {/* Scrollable body */}
        <div className="overflow-y-auto flex-1 px-5 py-4 space-y-4">
          {description && (
     <p className="text-xs text-muted-foreground">{description}</p>
          )}

   {/* Format guide */}
          <div className="rounded-lg border border-border bg-muted/30 overflow-hidden">
         <div className="flex items-center justify-between px-3 py-2 bg-muted/50 border-b border-border">
              <div className="flex items-center gap-1.5">
             <FileText className="h-3.5 w-3.5 text-muted-foreground" />
    <span className="text-xs font-semibold text-foreground">CSV Format</span>
           </div>
     <Button
         variant="ghost"
    size="sm"
       className="h-7 gap-1 text-xs text-primary hover:text-primary"
           onClick={downloadTemplate}
          >
 <Download className="h-3 w-3" />
         Download Template
   </Button>
    </div>
  <div className="overflow-x-auto">
              <table className="w-full text-xs">
     <thead>
        <tr className="border-b border-border bg-muted/20">
   <th className="text-left px-3 py-2 font-semibold text-foreground w-48">Column</th>
         <th className="text-left px-3 py-2 font-semibold text-foreground">Description</th>
    <th className="text-left px-3 py-2 font-semibold text-foreground w-32">Example</th>
        </tr>
 </thead>
        <tbody>
    {columns.map((col, i) => (
         <tr key={i} className="border-b border-border/50 last:border-0">
         <td className="px-3 py-2 font-mono text-primary">
    {col.name}
   {col.required && (
    <span className="ml-1 text-destructive text-[10px]">*</span>
             )}
            </td>
    <td className="px-3 py-2 text-muted-foreground">{col.description ?? "—"}</td>
        <td className="px-3 py-2 text-muted-foreground font-mono truncate max-w-[120px]">
     {col.example ?? "—"}
     </td>
         </tr>
      ))}
         </tbody>
   </table>
            </div>
            <p className="px-3 py-2 text-[10px] text-muted-foreground border-t border-border/50">
   <span className="text-destructive">*</span> Required fields. First row must be the header row.
            </p>
        </div>

     {/* File picker */}
      {!done && (
      <div
     className="rounded-lg border-2 border-dashed border-border hover:border-primary/50 transition-colors cursor-pointer p-6 text-center"
    onClick={() => fileRef.current?.click()}
     >
              <Upload className="h-8 w-8 text-muted-foreground mx-auto mb-2" />
     {file ? (
 <div className="space-y-1">
      <p className="text-sm font-medium text-foreground">{file.name}</p>
  <p className="text-xs text-muted-foreground">
  {(file.size / 1024).toFixed(1)} KB — Click to change
   </p>
     </div>
         ) : (
           <div className="space-y-1">
         <p className="text-sm font-medium text-foreground">Click to select CSV file</p>
  <p className="text-xs text-muted-foreground">or drag and drop here</p>
       </div>
   )}
              <input
ref={fileRef}
       type="file"
    accept=".csv"
                className="hidden"
      onChange={e => setFile(e.target.files?.[0] ?? null)}
       />
    </div>
          )}

          {/* Error banner */}
    {error && (
       <div className="flex items-start gap-2 rounded-lg bg-destructive/10 border border-destructive/20 p-3">
     <AlertCircle className="h-4 w-4 text-destructive shrink-0 mt-0.5" />
       <p className="text-xs text-destructive">{error}</p>
  </div>
      )}

          {/* Result */}
       {done && (
            <div className="rounded-lg border border-emerald-200 dark:border-emerald-800 bg-emerald-50 dark:bg-emerald-950 p-4 space-y-2">
   <div className="flex items-center gap-2 text-emerald-700 dark:text-emerald-300">
    <Check className="h-4 w-4" />
           <span className="text-sm font-medium">
     Import complete — {result.imported} records imported
   </span>
         </div>
        {result.errors.length > 0 && (
 <div className="mt-2">
<p className="text-xs text-amber-600 dark:text-amber-400 mb-1 flex items-center gap-1">
     <AlertCircle className="h-3 w-3" />
    {result.errors.length} row(s) had errors:
        </p>
    <div className="max-h-32 overflow-y-auto rounded bg-black/5 dark:bg-white/5 p-2 space-y-0.5">
       {result.errors.map((e, i) => (
         <p key={i} className="text-[11px] font-mono text-destructive">{e}</p>
    ))}
      </div>
     </div>
           )}
    </div>
        )}
  </div>

        {/* Footer */}
      <div className="px-5 py-4 border-t border-border shrink-0 flex items-center justify-end gap-2">
          <Button variant="outline" size="sm" onClick={onClose}>
       {done ? "Close" : "Cancel"}
     </Button>
          {!done && (
            <Button
              size="sm"
  className="gap-1.5"
         onClick={handleImport}
              disabled={!file || importing}
          >
         {importing ? (
      <Loader2 className="h-3.5 w-3.5 animate-spin" />
    ) : (
   <Upload className="h-3.5 w-3.5" />
)}
      {importing ? "Importing..." : "Import CSV"}
            </Button>
    )}
        </div>
      </div>
 </div>
  );
}
