/**
 * EmailComposeModal — reusable "compose a case email" modal.
 *
 * Features:
 * - Select recipient parties (fetches from case)
 * - Free-text To / Cc fields
 * - Pre-fillable subject + rich body textarea
 * - Attach documents from the case document library (checkbox list)
 * - Upload files from computer (saved to storage + audit-logged by backend)
 */

import { useState, useEffect, useRef } from "react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { caseEmailsApi } from "@/services/api/caseWorkspace";
import { casesApi } from "@/services/api/cases";
import type { CasePartyDto, DocumentDto } from "@/services/api/types";
import {
  X, Mail, Paperclip, Upload, Loader2, CheckCircle2, ChevronDown, ChevronUp,
} from "lucide-react";

interface Props {
  caseId: string;
  /** Used only for display in the header. */
  caseName?: string;
  /** Party list (may already be loaded in parent — avoids second fetch). */
  parties?: CasePartyDto[];
  /** Pre-fill subject (e.g. template name). */
  initialSubject?: string;
  /** Pre-fill body. */
  initialBody?: string;
  /** Pre-select a document from the library (e.g. the just-saved template). */
  initialAttachedDocId?: string;
  /** If composing a reply, pass the email being replied to. */
  replyToEmailId?: string;
  onSent: () => void;
  onCancel: () => void;
}

export default function EmailComposeModal({
  caseId,
  caseName,
  parties: partiesProp,
  initialSubject = "",
  initialBody = "",
  initialAttachedDocId,
  replyToEmailId,
  onSent,
  onCancel,
}: Props) {
  // ── data ──────────────────────────────────────────────────────────────────
  const [parties, setParties] = useState<CasePartyDto[]>(partiesProp ?? []);
  const [documents, setDocuments] = useState<DocumentDto[]>([]);
  const [loadingData, setLoadingData] = useState(true);

  // ── form state ────────────────────────────────────────────────────────────
  const [recipientPartyIds, setRecipientPartyIds] = useState<string[]>([]);
  const [toAddresses, setToAddresses] = useState("");
  const [cc, setCc] = useState("");
  const [subject, setSubject] = useState(initialSubject);
  const [body, setBody] = useState(initialBody);
  const [attachedDocIds, setAttachedDocIds] = useState<string[]>(
    initialAttachedDocId ? [initialAttachedDocId] : []
  );
  const [uploadedFiles, setUploadedFiles] = useState<File[]>([]);

  // ── UI state ──────────────────────────────────────────────────────────────
  const [showParties, setShowParties] = useState(true);
  const [showDocs, setShowDocs] = useState(false);
  const [sending, setSending] = useState(false);
  const [sent, setSent] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fileInputRef = useRef<HTMLInputElement>(null);

  // ── Load parties + documents ──────────────────────────────────────────────
  useEffect(() => {
    let cancelled = false;
    const load = async () => {
      try {
        const [partiesRes, docsRes] = await Promise.all([
          partiesProp ? Promise.resolve(null) : casesApi.getParties(caseId),
          casesApi.getDocuments(caseId),
        ]);
        if (cancelled) return;
        if (partiesRes) setParties(partiesRes.data);
        setDocuments(docsRes.data);
        // expand doc panel if initial doc attached
        if (initialAttachedDocId) setShowDocs(true);
      } catch (_e) {
        // silently ignore load errors
      } finally {
        if (!cancelled) setLoadingData(false);
      }
    };
    load();
    return () => { cancelled = true; };
  }, [caseId, partiesProp, initialAttachedDocId]);

  // ── Handlers ──────────────────────────────────────────────────────────────
  const toggleParty = (id: string) =>
    setRecipientPartyIds(prev =>
      prev.includes(id) ? prev.filter(x => x !== id) : [...prev, id]
    );

  const toggleDoc = (id: string) =>
    setAttachedDocIds(prev =>
      prev.includes(id) ? prev.filter(x => x !== id) : [...prev, id]
    );

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files) setUploadedFiles(prev => [...prev, ...Array.from(e.target.files!)]);
  };

  const removeUploadedFile = (idx: number) =>
    setUploadedFiles(prev => prev.filter((_, i) => i !== idx));

  const handleSend = async () => {
    if (!subject.trim()) { setError("Subject is required."); return; }
    if (recipientPartyIds.length === 0 && !toAddresses.trim()) {
      setError("Please select at least one recipient or enter an email address."); return;
    }
    setSending(true);
    setError(null);
    try {
      const fd = new FormData();
      fd.append("recipientPartyIdsJson", JSON.stringify(recipientPartyIds));
      fd.append("toAddresses", toAddresses);
      fd.append("cc", cc);
      fd.append("subject", subject);
      fd.append("body", body);
      fd.append("isHtml", "false");
      fd.append("attachedDocumentIdsJson", JSON.stringify(attachedDocIds));
      if (replyToEmailId) fd.append("replyToEmailId", replyToEmailId);
      uploadedFiles.forEach(f => fd.append("files", f));

      await caseEmailsApi.compose(caseId, fd);
      setSent(true);
      setTimeout(onSent, 1200);
    } catch (e: unknown) {
      const msg = (e as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setError(msg ?? "Failed to send email. Please try again.");
    } finally {
      setSending(false);
    }
  };

  // ── Render ────────────────────────────────────────────────────────────────
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4">
      <div className="w-full max-w-2xl rounded-2xl border border-border bg-card shadow-xl flex flex-col max-h-[90vh]">

        {/* Header */}
        <div className="flex items-center justify-between border-b border-border px-5 py-3.5 shrink-0">
          <div className="flex items-center gap-2">
            <Mail className="h-4 w-4 text-primary" />
            <h2 className="text-sm font-semibold text-foreground">Compose Email</h2>
            {caseName && (
              <Badge variant="outline" className="text-[10px]">{caseName}</Badge>
            )}
          </div>
          <button onClick={onCancel} className="rounded p-1 text-muted-foreground hover:bg-accent hover:text-foreground transition-colors">
            <X className="h-4 w-4" />
          </button>
        </div>

        {/* Body */}
        <div className="flex-1 overflow-y-auto px-5 py-4 space-y-4">
          {loadingData && (
            <div className="flex items-center justify-center py-6">
              <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
            </div>
          )}

          {!loadingData && (
            <>
              {/* Recipients section */}
              <div>
                <button
                  type="button"
                  className="flex w-full items-center justify-between text-xs font-semibold uppercase tracking-wide text-muted-foreground mb-2"
                  onClick={() => setShowParties(v => !v)}
                >
                  <span>Case Parties</span>
                  {showParties ? <ChevronUp className="h-3.5 w-3.5" /> : <ChevronDown className="h-3.5 w-3.5" />}
                </button>
                {showParties && (
                  <div className="rounded-lg border border-border divide-y divide-border max-h-[160px] overflow-y-auto">
                    {parties.length === 0 && (
                      <p className="px-3 py-2 text-xs text-muted-foreground">No parties on this case.</p>
                    )}
                    {parties.map(p => (
                      <label
                        key={p.id}
                        className="flex items-center gap-2.5 px-3 py-2 cursor-pointer hover:bg-accent/30 transition-colors"
                      >
                        <input
                          type="checkbox"
                          className="h-3.5 w-3.5 rounded border-border accent-primary"
                          checked={recipientPartyIds.includes(p.id)}
                          onChange={() => toggleParty(p.id)}
                        />
                        <div className="flex-1 min-w-0">
                          <p className="text-xs text-foreground truncate">{p.companyName ?? p.companyId}</p>
                          {p.email && <p className="text-[10px] text-muted-foreground truncate">{p.email}</p>}
                        </div>
                        <Badge variant="outline" className="text-[9px]">{p.role}</Badge>
                      </label>
                    ))}
                  </div>
                )}
              </div>

              {/* To (extra addresses) */}
              <div>
                <label className="block text-xs font-medium text-foreground mb-1">
                  To <span className="text-muted-foreground font-normal">(extra addresses not covered by parties above, comma-separated)</span>
                </label>
                <input
                  type="text"
                  value={toAddresses}
                  onChange={e => setToAddresses(e.target.value)}
                  placeholder="email@example.com, email2@example.com"
                  className="w-full rounded-lg border border-border bg-background px-3 py-1.5 text-xs text-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-1 focus:ring-primary"
                />
              </div>

              {/* Cc */}
              <div>
                <label className="block text-xs font-medium text-foreground mb-1">Cc</label>
                <input
                  type="text"
                  value={cc}
                  onChange={e => setCc(e.target.value)}
                  placeholder="cc@example.com"
                  className="w-full rounded-lg border border-border bg-background px-3 py-1.5 text-xs text-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-1 focus:ring-primary"
                />
              </div>

              {/* Subject */}
              <div>
                <label className="block text-xs font-medium text-foreground mb-1">
                  Subject <span className="text-red-500">*</span>
                </label>
                <input
                  type="text"
                  value={subject}
                  onChange={e => setSubject(e.target.value)}
                  placeholder="Email subject"
                  className="w-full rounded-lg border border-border bg-background px-3 py-1.5 text-xs text-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-1 focus:ring-primary"
                />
              </div>

              {/* Body */}
              <div>
                <label className="block text-xs font-medium text-foreground mb-1">Message</label>
                <textarea
                  value={body}
                  onChange={e => setBody(e.target.value)}
                  rows={6}
                  placeholder="Type your message here..."
                  className="w-full rounded-lg border border-border bg-background px-3 py-2 text-xs text-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-1 focus:ring-primary resize-none"
                />
              </div>

              {/* Document library attachments */}
              {documents.length > 0 && (
                <div>
                  <button
                    type="button"
                    className="flex w-full items-center justify-between text-xs font-semibold uppercase tracking-wide text-muted-foreground mb-2"
                    onClick={() => setShowDocs(v => !v)}
                  >
                    <span className="flex items-center gap-1.5">
                      <Paperclip className="h-3.5 w-3.5" />
                      Attach from Document Library
                      {attachedDocIds.length > 0 && (
                        <Badge variant="outline" className="text-[9px]">{attachedDocIds.length}</Badge>
                      )}
                    </span>
                    {showDocs ? <ChevronUp className="h-3.5 w-3.5" /> : <ChevronDown className="h-3.5 w-3.5" />}
                  </button>
                  {showDocs && (
                    <div className="rounded-lg border border-border divide-y divide-border max-h-[160px] overflow-y-auto">
                      {documents.map(d => (
                        <label
                          key={d.id}
                          className="flex items-center gap-2.5 px-3 py-2 cursor-pointer hover:bg-accent/30 transition-colors"
                        >
                          <input
                            type="checkbox"
                            className="h-3.5 w-3.5 rounded border-border accent-primary"
                            checked={attachedDocIds.includes(d.id)}
                            onChange={() => toggleDoc(d.id)}
                          />
                          <span className="flex-1 text-xs text-foreground truncate">{d.sourceFileName}</span>
                          <span className="text-[9px] text-muted-foreground shrink-0">{d.docType}</span>
                        </label>
                      ))}
                    </div>
                  )}
                </div>
              )}

              {/* Upload from computer */}
              <div>
                <div className="flex items-center justify-between mb-2">
                  <span className="text-xs font-semibold uppercase tracking-wide text-muted-foreground flex items-center gap-1.5">
                    <Upload className="h-3.5 w-3.5" />
                    Upload Files
                  </span>
                  <Button
                    variant="ghost"
                    size="sm"
                    className="h-6 text-[10px] gap-1"
                    onClick={() => fileInputRef.current?.click()}
                  >
                    + Add file
                  </Button>
                  <input
                    ref={fileInputRef}
                    type="file"
                    multiple
                    className="hidden"
                    onChange={handleFileChange}
                  />
                </div>
                {uploadedFiles.length > 0 && (
                  <div className="rounded-lg border border-dashed border-border divide-y divide-border">
                    {uploadedFiles.map((f, idx) => (
                      <div key={idx} className="flex items-center gap-2 px-3 py-1.5">
                        <Paperclip className="h-3 w-3 text-muted-foreground shrink-0" />
                        <span className="flex-1 text-xs text-foreground truncate">{f.name}</span>
                        <span className="text-[9px] text-muted-foreground shrink-0">
                          {(f.size / 1024).toFixed(0)} KB
                        </span>
                        <button
                          type="button"
                          onClick={() => removeUploadedFile(idx)}
                          className="text-muted-foreground hover:text-destructive transition-colors"
                        >
                          <X className="h-3 w-3" />
                        </button>
                      </div>
                    ))}
                  </div>
                )}
              </div>

              {/* Error */}
              {error && (
                <p className="rounded-lg border border-destructive/30 bg-destructive/10 px-3 py-2 text-xs text-destructive">
                  {error}
                </p>
              )}
            </>
          )}
        </div>

        {/* Footer */}
        <div className="flex items-center justify-end gap-2 border-t border-border px-5 py-3.5 shrink-0">
          <Button variant="ghost" size="sm" className="text-xs" onClick={onCancel} disabled={sending}>
            Cancel
          </Button>
          <Button
            size="sm"
            className="text-xs gap-1.5"
            onClick={handleSend}
            disabled={sending || sent || loadingData}
          >
            {sending
              ? <Loader2 className="h-3.5 w-3.5 animate-spin" />
              : sent
                ? <CheckCircle2 className="h-3.5 w-3.5" />
                : <Mail className="h-3.5 w-3.5" />}
            {sending ? "Sending..." : sent ? "Sent!" : "Send Email"}
          </Button>
        </div>
      </div>
    </div>
  );
}
