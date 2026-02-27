import { useState, useEffect } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { companiesApi, casesApi, tasksApi, documentsApi } from "@/services/api";
import { useTranslation } from "@/contexts/LanguageContext";
import type { CompanyDto, CaseDto, TaskDto, DocumentDto, CompanyCasePartyDto } from "@/services/api/types";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import BackButton from "@/components/ui/BackButton";
import TaskDetailModal from "@/components/TaskDetailModal";
import { Loader2, Plus, Briefcase, ListChecks, Pencil, Phone, FileText, X, Users } from "lucide-react";
import { format } from "date-fns";

function InfoRow({ label, value }: { label: string; value: string | null | undefined }) {
  if (!value) return null;
  return (
    <div>
      <p className="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">{label}</p>
      <p className="text-sm text-foreground">{value}</p>
    </div>
  );
}

export default function CompanyDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { t } = useTranslation();
  const [company, setCompany] = useState<CompanyDto | null>(null);
  const [cases, setCases] = useState<CaseDto[]>([]);
  const [tasks, setTasks] = useState<TaskDto[]>([]);
  const [docs, setDocs] = useState<DocumentDto[]>([]);
  const [parties, setParties] = useState<CompanyCasePartyDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [showDocPanel, setShowDocPanel] = useState(false);
  const [showCreateTask, setShowCreateTask] = useState(false);
  const [newTaskTitle, setNewTaskTitle] = useState("");
  const [newTaskDeadline, setNewTaskDeadline] = useState("");
  const [savingTask, setSavingTask] = useState(false);
  const [selectedTaskId, setSelectedTaskId] = useState<string | null>(null);

  useEffect(() => {
    if (!id) return;
    Promise.all([
      companiesApi.getById(id),
      casesApi.getAll(id),
      tasksApi.getAll({ companyId: id }),
      documentsApi.getByCompany(id),
      companiesApi.getPartiesByCompany(id),
    ]).then(([compRes, casesRes, tasksRes, docsRes, partiesRes]) => {
      setCompany(compRes.data);
      setCases(casesRes.data);
      setTasks(tasksRes.data);
      setDocs(docsRes.data);
      setParties(partiesRes.data);
    }).catch(console.error)
    .finally(() => setLoading(false));
  }, [id]);

  const reloadTasks = () => {
    if (!id) return;
    tasksApi.getAll({ companyId: id }).then(r => setTasks(r.data)).catch(console.error);
  };

  const handleCreateTask = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!id || !newTaskTitle.trim()) return;
    setSavingTask(true);
    try {
      await tasksApi.create({ companyId: id, title: newTaskTitle.trim(), deadline: newTaskDeadline || undefined });
      setNewTaskTitle("");
      setNewTaskDeadline("");
      setShowCreateTask(false);
      reloadTasks();
    } catch (err) { console.error(err); }
    finally { setSavingTask(false); }
  };

  const statusLabel = (s: string): string =>
    (t.statuses as Record<string, string>)?.[s] ?? s;

  if (loading) return <div className="flex h-full items-center justify-center"><Loader2 className="h-8 w-8 animate-spin text-muted-foreground" /></div>;
  if (!company) return <p className="p-8 text-muted-foreground">{t.companies.noCompanies}</p>;

  const hasContact = company.phone || company.email || company.contactPerson || company.iban;

  return (
    <div className="flex gap-5">
      {/* Main content */}
 <div className="flex-1 min-w-0 max-w-5xl mx-auto space-y-6">
      <TaskDetailModal taskId={selectedTaskId} onClose={() => setSelectedTaskId(null)} onStatusChanged={() => { if (id) tasksApi.getAll({ companyId: id }).then(r => setTasks(r.data)); }} />
     <BackButton className="cursor-pointer flex items-center gap-2 mb-2" onClick={() => navigate("/companies")}>{t.companies.backToCompanies}</BackButton>

        {/* Company header */}
    <div className="rounded-xl border border-border bg-card p-4">
          <div className="flex items-start justify-between">
       <div>
    <div className="flex items-center gap-2">
            <h1 className="text-xl font-bold text-card-foreground">{company.name}</h1>
              </div>
    {company.cuiRo && <p className="mt-1 text-sm text-muted-foreground">{t.companies.cuiRo}: {company.cuiRo}</p>}
  {company.address && <p className="text-sm text-muted-foreground">{company.address}{company.locality ? `, ${company.locality}` : ""}{company.county ? `, ${company.county}` : ""}</p>}
            </div>
            <div className="flex gap-2">
       <Button variant="outline" size="sm" className="gap-1.5 text-xs" onClick={() => setShowDocPanel(!showDocPanel)}>
     <FileText className="h-3.5 w-3.5" /> Documents ({docs.length})
       </Button>
      <Button variant="outline" size="sm" className="gap-1.5 text-xs border-primary/30 text-primary hover:bg-primary/5" onClick={() => navigate(`/companies/${id}/edit`)}>
 <Pencil className="h-3.5 w-3.5" /> {t.common.edit}
      </Button>
       </div>
   </div>
     <div className="mt-3 grid grid-cols-2 gap-x-8 gap-y-3 sm:grid-cols-4">
    <InfoRow label={t.companies.tradeRegisterNo} value={company.tradeRegisterNo} />
   <InfoRow label={t.companies.vatNumber || "VAT"} value={company.vatNumber} />
      <InfoRow label={t.companies.caen} value={company.caen} />
            <InfoRow label={t.companies.incorporationYear} value={company.incorporationYear} />
          </div>
          <div className="mt-3 flex flex-wrap gap-4 text-xs text-muted-foreground">
   {company.assignedToName && <span>{t.companies.assignedTo}: <span className="font-medium text-foreground">{company.assignedToName}</span></span>}
         <span>{company.caseCount} {t.companies.casesCount}</span>
          </div>
 </div>

      {/* Contact & Banking */}
        {hasContact && (
          <div className="rounded-xl border border-border bg-card p-4">
  <div className="flex items-center gap-2 mb-3">
        <Phone className="h-4 w-4 text-primary" />
   <h2 className="text-sm font-semibold text-foreground">{t.companies.contactSection || "Contact & Banking"}</h2>
</div>
        <div className="grid grid-cols-2 gap-x-8 gap-y-3 sm:grid-cols-3">
      <InfoRow label={t.companies.contactPerson || "Contact"} value={company.contactPerson} />
          <InfoRow label={t.companies.phone || "Phone"} value={company.phone} />
    <InfoRow label={t.companies.email || "Email"} value={company.email} />
           <InfoRow label={t.companies.iban || "IBAN"} value={company.iban} />
  <InfoRow label={t.companies.bankName || "Bank"} value={company.bankName} />
            </div>
        </div>
    )}

      {/* Cases */}
      <div>
          <div className="mb-2 flex items-center justify-between">
            <h2 className="flex items-center gap-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
              <Briefcase className="h-3.5 w-3.5" /> {t.cases.title} ({cases.length})
            </h2>
          </div>
          <div className="rounded-xl border border-border bg-card divide-y divide-border">
            {cases.length === 0 ? (
     <p className="px-4 py-6 text-center text-sm text-muted-foreground">{t.cases.noCases}</p>
       ) : (
       cases.map(c => (
  <div key={c.id} className="flex items-center gap-3 px-4 py-2.5 cursor-pointer hover:bg-accent/50 transition-colors" onClick={() => navigate(`/cases/${c.id}`)}>
    <div className="min-w-0 flex-1">
             <p className="text-sm font-medium text-foreground truncate">{c.caseNumber}</p>
     <p className="text-xs text-muted-foreground">{c.debtorName}</p>
  </div>
        <Badge variant="secondary" className="text-[10px]">{statusLabel(c.status)}</Badge>
     {c.nextHearingDate && <span className="text-[10px] text-muted-foreground shrink-0">{format(new Date(c.nextHearingDate), "dd MMM")}</span>}
           </div>
 ))
     )}
    </div>
        </div>

        {/* Parties */}
        {parties.length > 0 && (
          <div>
            <div className="mb-2 flex items-center justify-between">
              <h2 className="flex items-center gap-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                <Users className="h-3.5 w-3.5" /> {t.companies.partiesSection} ({parties.length})
              </h2>
            </div>
            <div className="rounded-xl border border-border bg-card divide-y divide-border">
              {parties.map(p => (
                <div
                  key={p.id}
                  className="flex items-center gap-3 px-4 py-2.5 cursor-pointer hover:bg-accent/50 transition-colors"
                  onClick={() => navigate(`/cases/${p.caseId}`)}
                >
                  <div className="min-w-0 flex-1">
                    <p className="text-sm font-medium text-foreground truncate">
                      {p.caseNumber ?? p.caseId}
                      {p.debtorName && <span className="ml-2 text-xs text-muted-foreground">— {p.debtorName}</span>}
                    </p>
                    {p.roleDescription && (
                      <p className="text-xs text-muted-foreground truncate">{p.roleDescription}</p>
                    )}
                  </div>
                  <Badge variant="secondary" className="text-[10px] shrink-0">{p.role}</Badge>
                  {p.claimAmountRon != null && (
                    <span className="text-[10px] text-muted-foreground shrink-0">
                      {p.claimAmountRon.toLocaleString("ro-RO")} RON
                      {p.claimAccepted === true && <span className="ml-1 text-green-600">✓</span>}
                      {p.claimAccepted === false && <span className="ml-1 text-destructive">✗</span>}
                    </span>
                  )}
                </div>
              ))}
            </div>
          </div>
        )}

        {/* Tasks */}
        <div>
      <div className="mb-2 flex items-center justify-between">
            <h2 className="flex items-center gap-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
              <ListChecks className="h-3.5 w-3.5" /> {t.tasks.title} ({tasks.length})
            </h2>
            <Button
              variant="ghost"
              size="sm"
              className="text-xs gap-1 text-primary"
              onClick={() => setShowCreateTask(v => !v)}
            >
              {showCreateTask ? <X className="h-3.5 w-3.5" /> : <Plus className="h-3.5 w-3.5" />}
              {showCreateTask ? t.common.cancel : t.tasks.createTask}
            </Button>
          </div>

          {/* Inline create form */}
          {showCreateTask && (
            <form onSubmit={handleCreateTask} className="mb-3 rounded-xl border border-primary/30 bg-primary/5 p-3 flex flex-col sm:flex-row gap-2">
              <input
                autoFocus
                value={newTaskTitle}
                onChange={e => setNewTaskTitle(e.target.value)}
                placeholder={t.tasks.taskTitlePlaceholder}
                required
                className="flex-1 rounded-lg border border-input bg-background px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-ring"
              />
              <input
                type="date"
                value={newTaskDeadline}
                onChange={e => setNewTaskDeadline(e.target.value)}
                className="rounded-lg border border-input bg-background px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-ring"
              />
              <Button type="submit" size="sm" disabled={savingTask || !newTaskTitle.trim()} className="text-xs shrink-0">
                {savingTask ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Plus className="h-3.5 w-3.5" />}
                {t.tasks.createTask}
              </Button>
            </form>
          )}

          <div className="rounded-xl border border-border bg-card divide-y divide-border">
            {tasks.length === 0 ? (
       <p className="px-4 py-6 text-center text-sm text-muted-foreground">{t.tasks.noTasks}</p>
   ) : (
     tasks.map(tk => (
     <div key={tk.id} className="flex items-center gap-3 px-4 py-2.5 cursor-pointer hover:bg-accent/50 transition-colors" onClick={() => setSelectedTaskId(tk.id)}>
          <div className="min-w-0 flex-1">
    <p className="text-sm font-medium text-foreground truncate">{tk.title}</p>
        {tk.description && <p className="text-xs text-muted-foreground truncate">{tk.description}</p>}
            </div>
     <Badge variant={tk.status === "done" ? "success" : tk.status === "blocked" ? "destructive" : tk.status === "inProgress" ? "warning" : "default"} className="shrink-0 text-[10px]">{tk.status}</Badge>
              {tk.deadline && <span className="text-[10px] text-muted-foreground shrink-0">{format(new Date(tk.deadline), "dd MMM")}</span>}
        </div>
 ))
 )}
  </div>
        </div>
      </div>

      {/* Documents side panel */}
      {showDocPanel && (
        <div className="w-80 shrink-0 rounded-xl border border-border bg-card p-4 h-fit sticky top-6 space-y-3 hidden lg:block">
        <div className="flex items-center justify-between">
          <h2 className="flex items-center gap-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
    <FileText className="h-3.5 w-3.5" /> Documents ({docs.length})
            </h2>
            <button onClick={() => setShowDocPanel(false)} className="text-xs text-muted-foreground hover:text-foreground">?</button>
          </div>
     {docs.length === 0 ? (
   <p className="text-sm text-muted-foreground text-center py-4">No documents linked to this company.</p>
          ) : (
     <div className="space-y-2 max-h-[calc(100vh-200px)] overflow-y-auto">
    {docs.map(d => (
        <div key={d.id} className="rounded-lg border border-border p-2.5 hover:border-primary/30 transition-colors cursor-pointer" onClick={() => navigate(`/cases/${d.caseId}`)}>
         <p className="text-xs font-medium text-foreground truncate">{d.sourceFileName}</p>
         <div className="mt-1 flex items-center gap-2">
    <Badge variant="secondary" className="text-[9px]">{d.docType}</Badge>
            <span className="text-[10px] text-muted-foreground">{format(new Date(d.uploadedAt), "dd MMM yyyy")}</span>
     </div>
         <p className="mt-0.5 text-[10px] text-muted-foreground">by {d.uploadedBy}</p>
                </div>
    ))}
 </div>
          )}
        </div>
      )}
    </div>
  );
}
