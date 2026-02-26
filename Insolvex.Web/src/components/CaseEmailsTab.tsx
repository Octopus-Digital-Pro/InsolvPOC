import { useState } from "react";
import { caseEmailsApi } from "@/services/api/caseWorkspace";
import type { CaseEmailDto, BulkEmailPreview } from "@/services/api/caseWorkspace";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
Mail, Send, Clock, CheckCircle2, XCircle, Users,
  ChevronDown, ChevronUp, Eye,
} from "lucide-react";
import { format } from "date-fns";

interface Props {
  caseId: string;
  emails: CaseEmailDto[];
onRefresh: () => void;
}

export default function CaseEmailsTab({ caseId, emails, onRefresh }: Props) {
  const [expandedId, setExpandedId] = useState<string | null>(null);
  const [showBulk, setShowBulk] = useState(false);
  const [bulkPreview, setBulkPreview] = useState<BulkEmailPreview | null>(null);
  const [bulkLoading, setBulkLoading] = useState(false);

  const scheduled = emails.filter(e => e.status === "Scheduled");
  const sent = emails.filter(e => e.status === "Sent" || e.sentAt);
  const failed = emails.filter(e => e.status === "Failed");

  const handlePreviewCohort = async () => {
    setBulkLoading(true);
    try {
      const r = await caseEmailsApi.previewCohort(caseId);
      setBulkPreview(r.data);
      setShowBulk(true);
    } catch (e) {
      console.error(e);
    } finally {
      setBulkLoading(false);
    }
  };

  const statusIcon = (status: string) => {
    switch (status) {
      case "Sent": return <CheckCircle2 className="h-3.5 w-3.5 text-green-500" />;
      case "Failed": return <XCircle className="h-3.5 w-3.5 text-red-500" />;
      case "Scheduled": return <Clock className="h-3.5 w-3.5 text-amber-500" />;
      default: return <Mail className="h-3.5 w-3.5 text-muted-foreground" />;
    }
  };

  const statusVariant = (status: string): "success" | "destructive" | "warning" | "secondary" => {
    switch (status) {
      case "Sent": return "success";
      case "Failed": return "destructive";
      case "Scheduled": return "warning";
      default: return "secondary";
    }
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
          <Button
            variant="outline" size="sm"
 className="text-xs gap-1 border-primary/30 text-primary hover:bg-primary/5"
    onClick={handlePreviewCohort}
   disabled={bulkLoading}
          >
       <Users className="h-3.5 w-3.5" />
      <span className="hidden sm:inline">Bulk Email</span>
          </Button>
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

      {/* Email list */}
      {emails.length === 0 ? (
        <div className="rounded-xl border border-dashed border-border bg-card/50 p-8 text-center">
          <Mail className="h-8 w-8 mx-auto text-muted-foreground/30 mb-2" />
          <p className="text-sm text-muted-foreground">No emails scheduled or sent for this case yet.</p>
        </div>
    ) : (
        <div className="rounded-xl border border-border bg-card divide-y divide-border">
          {emails.map(email => (
     <div key={email.id}>
         <div
           className="flex items-center gap-3 px-4 py-2.5 cursor-pointer hover:bg-accent/30 transition-colors"
         onClick={() => setExpandedId(expandedId === email.id ? null : email.id)}
              >
      {statusIcon(email.status)}
             <div className="min-w-0 flex-1">
     <p className="text-sm font-medium text-foreground truncate">{email.subject}</p>
       <p className="text-[10px] text-muted-foreground truncate">To: {email.to}</p>
        </div>
      <Badge variant={statusVariant(email.status)} className="text-[10px] shrink-0">{email.status}</Badge>
           <span className="text-[10px] text-muted-foreground shrink-0">
                  {email.sentAt
        ? format(new Date(email.sentAt), "dd MMM HH:mm")
  : format(new Date(email.scheduledFor), "dd MMM HH:mm")
    }
    </span>
   {expandedId === email.id ? <ChevronUp className="h-3.5 w-3.5 text-muted-foreground" /> : <ChevronDown className="h-3.5 w-3.5 text-muted-foreground" />}
     </div>

         {/* Expanded body */}
      {expandedId === email.id && (
       <div className="px-4 pb-3 pt-1 border-t border-border/50 bg-muted/20">
           <div className="grid grid-cols-2 gap-2 text-[10px] text-muted-foreground mb-2">
   <div><strong>To:</strong> {email.to}</div>
          {email.cc && <div><strong>Cc:</strong> {email.cc}</div>}
        <div><strong>Scheduled:</strong> {format(new Date(email.scheduledFor), "dd MMM yyyy HH:mm")}</div>
             {email.sentAt && <div><strong>Sent:</strong> {format(new Date(email.sentAt), "dd MMM yyyy HH:mm")}</div>}
        </div>
     <div className="rounded-lg border border-border bg-card p-3 text-xs text-foreground whitespace-pre-wrap max-h-[200px] overflow-y-auto">
              {email.isHtml ? (
  <div dangerouslySetInnerHTML={{ __html: email.body }} />
   ) : (
      email.body
      )}
          </div>
   </div>
     )}
  </div>
  ))}
        </div>
      )}
    </div>
  );
}
