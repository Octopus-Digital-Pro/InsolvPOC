import { useState, useCallback } from "react";
import { useNavigate } from "react-router-dom";
import { Loader2, Upload, FileUp, FileText, Sparkles } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import type { Translations } from "@/i18n/types";

interface QuickUploadZoneProps {
  t: Translations;
  /** Called with the new document id immediately after a successful upload. */
  onUploadSuccess?: (id: string) => void;
}

export default function QuickUploadZone({ t, onUploadSuccess }: QuickUploadZoneProps) {
  const navigate = useNavigate();
  const [dragging, setDragging] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [recentUploads, setRecentUploads] = useState<Array<{ name: string; id: string; status: string }>>([]);

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
          if (onUploadSuccess) {
            onUploadSuccess(data.id);
          }
        } else {
          setRecentUploads(prev => [{ name: file.name, id: "", status: "error" }, ...prev.slice(0, 4)]);
        }
      } catch {
        setRecentUploads(prev => [{ name: file.name, id: "", status: "error" }, ...prev.slice(0, 4)]);
      }
    }
    setUploading(false);
  };

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
            <Button disabled variant="outline" size="sm" className="mt-2 text-xs gap-1.5 border-primary/30 text-primary hover:bg-primary/5" type="button">
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
