import type { InsolvencyDocument } from "../types";
import { formatDateTime } from "@/lib/dateUtils";

interface DocumentCardProps {
  document: InsolvencyDocument;
  isActive: boolean;
  onClick: () => void;
}

function formatDocType(docType: string): string {
  return (docType || "other").replace(/_/g, " ");
}

export default function DocumentCard({
  document,
  isActive,
  onClick,
}: DocumentCardProps) {
  const uploadedAt = formatDateTime(document.uploadedAt);
  const docDate =
    typeof document.documentDate === "string"
      ? document.documentDate
      : (document.documentDate as { iso?: string; text?: string })?.iso ??
        (document.documentDate as { text?: string })?.text ??
        "—";

  return (
    <button
      onClick={onClick}
      type="button"
      className={`
        w-full text-left rounded-xl border px-4 py-3 transition-all duration-150
        ${isActive
          ? "bg-sidebar-accent border-sidebar-border shadow-sm text-sidebar-accent-foreground"
          : "bg-card border-border hover:bg-accent hover:border-border text-card-foreground"}
      `}
    >
      <h3
        className={`truncate text-sm font-semibold ${isActive ? "text-sidebar-primary" : "text-foreground"}`}
      >
        {formatDocType(document.docType)}
      </h3>
      <div className="mt-2 space-y-1 text-xs text-muted-foreground">
        <p>
          <span className="text-muted-foreground">Uploaded:</span> {uploadedAt}
        </p>
        <p>
          <span className="text-muted-foreground">Document date:</span> {docDate}
        </p>
        <p className="truncate" title={document.sourceFileName}>
          {document.sourceFileName}
        </p>
      </div>
    </button>
  );
}
