/**
 * CaseEmailsTab — Emails tab for a case detail page.
 *
 * Features:
 * - Threaded view: emails grouped by threadId
 * - Direction indicator (Outbound / Inbound)
 * - Attachment links from attachmentsJson
 * - "New Email" button → EmailComposeModal
 * - Bulk e-mail cohort preview
 */

import { useState, useMemo } from "react";
import { caseEmailsApi } from "@/services/api/caseWorkspace";
import type { CaseEmailDto, BulkEmailPreview } from "@/services/api/caseWorkspace";
import type { CasePartyDto } from "@/services/api/types";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import EmailComposeModal from "@/components/EmailComposeModal";
import {
  Mail, Clock, CheckCircle2, XCircle, Users,
  ChevronDown, ChevronUp, MessageSquare, Paperclip, ArrowUpRight, ArrowDownLeft,
} from "lucide-react";
import { format } from "date-fns";

interface Props {
  caseId: string;
  caseName?: string;
  parties?: CasePartyDto[];
  emails: CaseEmailDto[];
  onRefresh: () => void;
  readOnly?: boolean;
}

// ── Helpers ───────────────────────────────────────────────────────────────────

function statusIcon(status: string) {
  switch (status) {
    case "Sent":      return <CheckCircle2 className="h-3.5 w-3.5 text-green-500" />;
    case "Failed":    return <XCircle className="h-3.5 w-3.5 text-red-500" />;
    case "Scheduled": return <Clock className="h-3.5 w-3.5 text-amber-500" />;
    default:          return <Mail className="h-3.5 w-3.5 text-muted-foreground" />;
  }
}

function statusVariant(status: string): "success" | "destructive" | "warning" | "secondary" {
  switch (status) {
    case "Sent":      return "success";
    case "Failed":    return "destructive";
    case "Scheduled": return "warning";
    default:          return "secondary";
  }
}

interface Attachment { fileName: string; storageKey?: string; contentType?: string }

function parseAttachments(json: string | null): Attachment[] {
  if (!json) return [];
  try { return JSON.parse(json) as Attachment[]; } catch { return []; }
}

function EmailCard({ email, onReply }: {
  email: CaseEmailDto;
  onReply?: (emailId: string) => void;
}) {
  const [expanded, setExpanded] = useState(false);
  const attachments = parseAttachments(email.attachmentsJson);
  const isInbound = email.direction === "Inbound";

  return (
    <div className={`border-l-2 ${isInbound ? "border-blue-400" : "border-primary/40"}`}>
      {/* Row */}
      <div
        className="flex items-center gap-3 px-4 py-2.5 cursor-pointer hover:bg-accent/30 transition-colors"
        onClick={() => setExpanded(v => !v)}
      >
        {isInbound
          ? <ArrowDownLeft className="h-3.5 w-3.5 text-blue-500 shrink-0" />
          : <ArrowUpRight className="h-3.5 w-3.5 text-primary/70 shrink-0" />}
        {statusIcon(email.status)}
        <div className="min-w-0 flex-1">
          <p className="text-sm font-medium text-foreground truncate">{email.subject}</p>
          <p className="text-[10px] text-muted-foreground truncate">
            {isInbound
              ? `From: ${email.fromName ?? email.to}`
              : `To: ${email.to}`}
            {email.cc && <span className="ml-2">Cc: {email.cc}</span>}
          </p>
        </div>
        {attachments.length > 0 && (
          <Paperclip className="h-3 w-3 text-muted-foreground shrink-0" aria-label={`${attachments.length} attachment(s)`} />
        )}
        <Badge variant={statusVariant(email.status)} className="text-[10px] shrink-0">{email.status}</Badge>
        <span className="text-[10px] text-muted-foreground shrink-0">
          {email.sentAt
            ? format(new Date(email.sentAt), "dd MMM HH:mm")
            : format(new Date(email.scheduledFor), "dd MMM HH:mm")}
        </span>
        {expanded ? <ChevronUp className="h-3.5 w-3.5 text-muted-foreground" /> : <ChevronDown className="h-3.5 w-3.5 text-muted-foreground" />}
      </div>

      {/* Expanded body */}
      {expanded && (
        <div className="px-4 pb-3 pt-1 border-t border-border/50 bg-muted/20 space-y-2">
          <div className="grid grid-cols-2 gap-2 text-[10px] text-muted-foreground">
            <div><strong>To:</strong> {email.to}</div>
            {email.cc && <div><strong>Cc:</strong> {email.cc}</div>}
            <div><strong>Scheduled:</strong> {format(new Date(email.scheduledFor), "dd MMM yyyy HH:mm")}</div>
            {email.sentAt && <div><strong>Sent:</strong> {format(new Date(email.sentAt), "dd MMM yyyy HH:mm")}</div>}
            {email.fromName && <div><strong>From:</strong> {email.fromName}</div>}
            {email.caseEmailAddress && <div><strong>Case inbox:</strong> {email.caseEmailAddress}</div>}
          </div>

          <div className="rounded-lg border border-border bg-card p-3 text-xs text-foreground whitespace-pre-wrap max-h-[200px] overflow-y-auto">
            <div dangerouslySetInnerHTML={{ __html: email.body }} />
          </div>

          {/* Attachments */}
          {attachments.length > 0 && (
            <div className="space-y-1">
              <p className="text-[10px] font-semibold text-muted-foreground uppercase tracking-wide">Attachments</p>
              <div className="flex flex-wrap gap-1.5">
                {attachments.map((a, i) => (
                  <span
                    key={i}
                    className="inline-flex items-center gap-1 rounded-md border border-border bg-muted/50 px-2 py-0.5 text-[10px] text-foreground"
                  >
                    <Paperclip className="h-2.5 w-2.5 text-muted-foreground" />
                    {a.fileName}
                  </span>
                ))}
              </div>
            </div>
          )}

          {/* Reply button */}
          {onReply && (
            <div className="flex justify-end">
              <Button
                variant="ghost"
                size="sm"
                className="h-6 text-[10px] gap-1"
                onClick={(e) => { e.stopPropagation(); onReply(email.id); }}
              >
                <MessageSquare className="h-3 w-3" /> Reply
              </Button>
            </div>
          )}
        </div>
      )}
    </div>
  );
}

// ── Main component ────────────────────────────────────────────────────────────

export default function CaseEmailsTab({ caseId, caseName, parties, emails, onRefresh, readOnly = false }: Props) {
  const [expandedThreadIds, setExpandedThreadIds] = useState<Set<string>>(new Set());
  const [showBulk, setShowBulk] = useState(false);
  const [bulkPreview, setBulkPreview] = useState<BulkEmailPreview | null>(null);
  const [bulkLoading, setBulkLoading] = useState(false);
  const [composeOpen, setComposeOpen] = useState(false);
  const [replyToEmailId, setReplyToEmailId] = useState<string | undefined>();

  const scheduled = emails.filter(e => e.status === "Scheduled");
  const failed = emails.filter(e => e.status === "Failed");

  // ── Build threads ──────────────────────────────────────────────────────────
  const threads = useMemo(() => {
    const map = new Map<string, CaseEmailDto[]>();
    const standalone: CaseEmailDto[] = [];

    for (const e of [...emails].sort(
      (a, b) => new Date(b.scheduledFor).getTime() - new Date(a.scheduledFor).getTime()
    )) {
      if (e.threadId) {
        const list = map.get(e.threadId) ?? [];
        list.push(e);
        map.set(e.threadId, list);
      } else {
        standalone.push(e);
      }
    }

    const result: Array<{ key: string; emails: CaseEmailDto[]; isThread: boolean }> = [];
    map.forEach((list, threadId) => result.push({ key: threadId, emails: list, isThread: true }));
    standalone.forEach(e => result.push({ key: e.id, emails: [e], isThread: false }));
    result.sort((a, b) =>
      new Date(b.emails[0].scheduledFor).getTime() - new Date(a.emails[0].scheduledFor).getTime()
    );
    return result;
  }, [emails]);

  const toggleThread = (key: string) =>
    setExpandedThreadIds(prev => {
      const next = new Set(prev);
      next.has(key) ? next.delete(key) : next.add(key);
      return next;
    });

  const handlePreviewCohort = async () => {
    setBulkLoading(true);
    try {
      const r = await caseEmailsApi.previewCohort(caseId);
      setBulkPreview(r.data);
      setShowBulk(true);
    } catch (e) { console.error(e); }
    finally { setBulkLoading(false); }
  };

  const openReply = (emailId: string) => {
    setReplyToEmailId(emailId);
    setComposeOpen(true);
  };

  const openNewEmail = () => {
    setReplyToEmailId(undefined);
    setComposeOpen(true);
  };

  return (
    <div className="space-y-3">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <h2 className="flex items-center gap-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
            <Mail className="h-3.5 w-3.5" /> Emails ({emails.length})
          </h2>
          {scheduled.length > 0 && (
            <Badge variant="warning" className="text-[10px]">{scheduled.length} scheduled</Badge>
          )}
          {failed.length > 0 && (
            <Badge variant="destructive" className="text-[10px]">{failed.length} failed</Badge>
          )}
        </div>
        <div className="flex gap-1.5">
          {!readOnly && (
            <>
          <Button
            variant="outline" size="sm"
            className="text-xs gap-1 border-border"
            onClick={handlePreviewCohort}
            disabled={bulkLoading}
          >
            <Users className="h-3.5 w-3.5" />
            <span className="hidden sm:inline">Bulk Email</span>
          </Button>
          <Button
            variant="outline" size="sm"
            className="text-xs gap-1 border-primary/30 text-primary hover:bg-primary/5"
            onClick={openNewEmail}
          >
            <Mail className="h-3.5 w-3.5" />
            New Email
          </Button>
            </>
          )}
        </div>
      </div>

      {/* Bulk email preview panel */}
      {showBulk && bulkPreview && (
        <div className="rounded-xl border border-primary/20 bg-primary/5 p-4 space-y-3">
          <div className="flex items-center justify-between">
            <h3 className="text-xs font-semibold text-primary">Creditor Cohort Preview</h3>
            <Button variant="ghost" size="sm" className="h-6 text-[10px]" onClick={() => setShowBulk(false)}>Close</Button>
          </div>
          <div className="grid grid-cols-3 gap-3 text-center">
            <div>
              <p className="text-lg font-bold text-foreground">{bulkPreview.total}</p>
              <p className="text-[10px] text-muted-foreground">Total Creditors</p>
            </div>
            <div>
              <p className="text-lg font-bold text-green-600">{bulkPreview.withEmail}</p>
              <p className="text-[10px] text-muted-foreground">With Email</p>
            </div>
            <div>
              <p className="text-lg font-bold text-red-500">{bulkPreview.withoutEmail}</p>
              <p className="text-[10px] text-muted-foreground">No Email</p>
            </div>
          </div>
          {bulkPreview.recipients.length > 0 && (
            <div className="max-h-[200px] overflow-y-auto divide-y divide-border rounded-lg border border-border bg-card">
              {bulkPreview.recipients.map(r => (
                <div key={r.partyId} className="flex items-center gap-2 px-3 py-1.5 text-[11px]">
                  <span className={`h-1.5 w-1.5 rounded-full shrink-0 ${r.hasEmail ? "bg-green-500" : "bg-red-400"}`} />
                  <span className="font-medium truncate flex-1">{r.name ?? "—"}</span>
                  <Badge variant="outline" className="text-[9px]">{r.role}</Badge>
                  <span className="text-muted-foreground truncate max-w-[150px]">{r.email ?? "no email"}</span>
                </div>
              ))}
            </div>
          )}
        </div>
      )}

      {/* Email threads */}
      {threads.length === 0 ? (
        <div className="rounded-xl border border-dashed border-border bg-card/50 p-8 text-center">
          <Mail className="h-8 w-8 mx-auto text-muted-foreground/30 mb-2" />
          <p className="text-sm text-muted-foreground">No emails scheduled or sent for this case yet.</p>
          {!readOnly && (
            <Button variant="outline" size="sm" className="mt-3 text-xs gap-1" onClick={openNewEmail}>
              <Mail className="h-3.5 w-3.5" /> New Email
            </Button>
          )}
        </div>
      ) : (
        <div className="space-y-2">
          {threads.map(thread => {
            const isExpanded = expandedThreadIds.has(thread.key);
            const head = thread.emails[0];
            const rest = thread.emails.slice(1);

            return (
              <div key={thread.key} className="rounded-xl border border-border bg-card overflow-hidden">
                <EmailCard email={head} onReply={openReply} />
                {thread.isThread && rest.length > 0 && (
                  <>
                    <div
                      className="flex items-center gap-2 px-4 py-1 text-[10px] text-muted-foreground cursor-pointer hover:bg-accent/20 border-t border-border/40 bg-muted/10"
                      onClick={() => toggleThread(thread.key)}
                    >
                      <MessageSquare className="h-3 w-3" />
                      {isExpanded
                        ? `Hide ${rest.length} earlier message${rest.length > 1 ? "s" : ""}`
                        : `Show ${rest.length} more message${rest.length > 1 ? "s" : ""} in thread`}
                      {isExpanded ? <ChevronUp className="h-3 w-3 ml-auto" /> : <ChevronDown className="h-3 w-3 ml-auto" />}
                    </div>
                    {isExpanded && (
                      <div className="divide-y divide-border/50">
                        {rest.map(e => (
                          <EmailCard key={e.id} email={e} onReply={openReply} />
                        ))}
                      </div>
                    )}
                  </>
                )}
              </div>
            );
          })}
        </div>
      )}

      {/* Compose modal */}
      {composeOpen && (
        <EmailComposeModal
          caseId={caseId}
          caseName={caseName}
          parties={parties}
          replyToEmailId={replyToEmailId}
          onSent={() => { setComposeOpen(false); onRefresh(); }}
          onCancel={() => setComposeOpen(false)}
        />
      )}
    </div>
  );
}
