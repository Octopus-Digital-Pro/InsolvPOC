import { useState, useEffect, useCallback } from "react";
import { casesApi } from "@/services/api";
import type { CreditorClaimDto, CasePartyDto } from "@/services/api/types";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Plus, Trash2, Pencil, Loader2, Receipt, X, Check, UserPlus,
} from "lucide-react";

const CLAIM_RANKS = [
  "Secured",
  "Budgetary",
  "Employee",
  "Chirographary",
] as const;

const CLAIM_STATUSES = [
  "Received",
  "UnderReview",
  "Admitted",
  "Rejected",
  "NeedsInfo",
] as const;

const CREDITOR_ROLES = [
  "SecuredCreditor",
  "UnsecuredCreditor",
  "BudgetaryCreditor",
  "EmployeeCreditor",
] as const;

const STATUS_VARIANT: Record<string, "default" | "secondary" | "outline" | "success" | "warning" | "destructive"> = {
  Received: "secondary",
  UnderReview: "outline",
  Admitted: "success",
  Rejected: "destructive",
  NeedsInfo: "warning",
};

const STATUS_LABEL: Record<string, string> = {
  Received: "Received",
  UnderReview: "Under Review",
  Admitted: "Admitted",
  Rejected: "Rejected",
  NeedsInfo: "Needs Info",
};

const RANK_LABEL: Record<string, string> = {
  Secured: "Secured",
  Budgetary: "Budgetary",
  Employee: "Employee",
  Chirographary: "Chirographary",
};

const ROLE_LABEL: Record<string, string> = {
  SecuredCreditor: "Secured Creditor",
  UnsecuredCreditor: "Unsecured Creditor",
  BudgetaryCreditor: "Budgetary Creditor",
  EmployeeCreditor: "Employee Creditor",
  CreditorsCommittee: "Creditors Committee",
};

function formatMoney(val: number | null | undefined): string {
  if (val == null) return "—";
  return val.toLocaleString("ro-RO", { style: "currency", currency: "RON", maximumFractionDigits: 0 });
}

interface Props {
  caseId: string;
  parties: CasePartyDto[];
  readOnly?: boolean;
}

export default function CaseClaimsTab({ caseId, parties, readOnly = false }: Props) {
  const [claims, setClaims] = useState<CreditorClaimDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [showForm, setShowForm] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [addingIndividual, setAddingIndividual] = useState(false);

  // Claim form state
  const [selectedPartyId, setSelectedPartyId] = useState("");
  const [declaredAmount, setDeclaredAmount] = useState("");
  const [admittedAmount, setAdmittedAmount] = useState("");
  const [rank, setRank] = useState<string>("Chirographary");
  const [natureDescription, setNatureDescription] = useState("");
  const [status, setStatus] = useState("Received");
  const [receivedAt, setReceivedAt] = useState("");
  const [notes, setNotes] = useState("");

  // Individual party form state
  const [indivName, setIndivName] = useState("");
  const [indivIdentifier, setIndivIdentifier] = useState("");
  const [indivRole, setIndivRole] = useState("UnsecuredCreditor");
  const [indivEmail, setIndivEmail] = useState("");
  const [indivSaving, setIndivSaving] = useState(false);

  // All parties loaded for dropdown (refreshed after adding individual)
  const [allParties, setAllParties] = useState<CasePartyDto[]>(parties);

  const creditorParties = allParties.filter(p => CREDITOR_ROLES.includes(p.role as typeof CREDITOR_ROLES[number]));

  const loadClaims = useCallback(async () => {
    try {
      const { data } = await casesApi.getClaims(caseId);
      setClaims(data);
    } catch { /* ignore */ }
    finally { setLoading(false); }
  }, [caseId]);

  const refreshParties = useCallback(async () => {
    try {
      const { data } = await casesApi.getParties(caseId);
      setAllParties(data);
    } catch { /* ignore */ }
  }, [caseId]);

  useEffect(() => { loadClaims(); }, [loadClaims]);
  useEffect(() => { setAllParties(parties); }, [parties]);

  const resetForm = () => {
    setSelectedPartyId("");
    setDeclaredAmount("");
    setAdmittedAmount("");
    setRank("Chirographary");
    setNatureDescription("");
    setStatus("Received");
    setReceivedAt("");
    setNotes("");
    setEditingId(null);
    setShowForm(false);
    setAddingIndividual(false);
  };

  const openCreateForm = () => {
    resetForm();
    setShowForm(true);
  };

  const openEditForm = (c: CreditorClaimDto) => {
    setSelectedPartyId(c.creditorPartyId);
    setDeclaredAmount(c.declaredAmount.toString());
    setAdmittedAmount(c.admittedAmount?.toString() ?? "");
    setRank(c.rank);
    setNatureDescription(c.natureDescription ?? "");
    setStatus(c.status);
    setReceivedAt(c.receivedAt ? c.receivedAt.split("T")[0] : "");
    setNotes(c.notes ?? "");
    setEditingId(c.id);
    setShowForm(true);
  };

  const handleSaveClaim = async () => {
    if (!selectedPartyId || !declaredAmount) return;
    setSaving(true);
    try {
      const payload = {
        creditorPartyId: selectedPartyId,
        declaredAmount: parseFloat(declaredAmount),
        admittedAmount: admittedAmount ? parseFloat(admittedAmount) : null,
        rank,
        natureDescription: natureDescription || null,
        status,
        receivedAt: receivedAt || null,
        notes: notes || null,
      };
      if (editingId) {
        await casesApi.updateClaim(caseId, editingId, payload);
      } else {
        await casesApi.createClaim(caseId, payload);
      }
      resetForm();
      await loadClaims();
    } catch { /* ignore */ }
    finally { setSaving(false); }
  };

  const handleDelete = async (claimId: string) => {
    if (!confirm("Delete this claim?")) return;
    try {
      await casesApi.deleteClaim(caseId, claimId);
      await loadClaims();
    } catch { /* ignore */ }
  };

  const handleAddIndividual = async () => {
    if (!indivName.trim()) return;
    setIndivSaving(true);
    try {
      const { data: newParty } = await casesApi.addIndividualParty(caseId, {
        name: indivName.trim(),
        identifier: indivIdentifier || null,
        role: indivRole,
        email: indivEmail || null,
      });
      await refreshParties();
      setSelectedPartyId(newParty.id);
      // Reset individual form
      setIndivName("");
      setIndivIdentifier("");
      setIndivRole("UnsecuredCreditor");
      setIndivEmail("");
      setAddingIndividual(false);
    } catch { /* ignore */ }
    finally { setIndivSaving(false); }
  };

  // Summary by rank
  const totalDeclared = claims.reduce((s, c) => s + c.declaredAmount, 0);
  const totalAdmitted = claims.reduce((s, c) => s + (c.admittedAmount ?? 0), 0);
  const byRank = CLAIM_RANKS.reduce<Record<string, number>>((acc, r) => {
    acc[r] = claims.filter(c => c.rank === r).reduce((s, c) => s + c.declaredAmount, 0);
    return acc;
  }, {});

  const partyDisplayName = (p: CasePartyDto) =>
    p.name ?? p.companyName ?? "(Unknown)";

  if (loading) {
    return (
      <div className="flex justify-center py-12">
        <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
      </div>
    );
  }

  return (
    <div className="space-y-3">
      {/* Header */}
      <div className="flex items-center justify-between">
        <h2 className="flex items-center gap-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
          <Receipt className="h-3.5 w-3.5" /> Claims / Outlay ({claims.length})
        </h2>
        {!readOnly && (
          <Button size="sm" className="gap-1.5 text-xs h-7" onClick={openCreateForm}>
            <Plus className="h-3.5 w-3.5" /> Add Claim
          </Button>
        )}
      </div>

      {/* Summary banner */}
      {claims.length > 0 && (
        <div className="rounded-lg border border-border bg-muted/30 px-4 py-3 space-y-2">
          <div className="flex flex-wrap gap-4 text-xs">
            <div>
              <span className="text-muted-foreground">Total Declared:</span>{" "}
              <span className="font-semibold text-foreground">{formatMoney(totalDeclared)}</span>
            </div>
            <div>
              <span className="text-muted-foreground">Total Admitted:</span>{" "}
              <span className="font-semibold text-foreground">{formatMoney(totalAdmitted)}</span>
            </div>
            <div>
              <span className="text-muted-foreground">Claims:</span>{" "}
              <span className="font-semibold text-foreground">{claims.length}</span>
            </div>
          </div>
          <div className="flex flex-wrap gap-3 text-xs">
            {CLAIM_RANKS.filter(r => byRank[r] > 0).map(r => (
              <div key={r} className="flex items-center gap-1.5">
                <span className="text-muted-foreground">{RANK_LABEL[r]}:</span>
                <span className="font-medium text-foreground">{formatMoney(byRank[r])}</span>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Inline form */}
      {showForm && (
        <div className="rounded-xl border border-primary/30 bg-card p-4 space-y-3">
          <div className="flex items-center justify-between">
            <h3 className="text-sm font-semibold text-foreground">
              {editingId ? "Edit Claim" : "Add Claim"}
            </h3>
            <button onClick={resetForm} className="text-muted-foreground hover:text-foreground">
              <X className="h-4 w-4" />
            </button>
          </div>

          {/* Creditor selector */}
          <div>
            <label className="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">Creditor *</label>
            <div className="flex items-center gap-2 mt-0.5">
              <select
                value={selectedPartyId}
                onChange={e => setSelectedPartyId(e.target.value)}
                className="flex-1 rounded-md border border-input bg-background px-3 py-2 text-sm"
                disabled={!!editingId}
              >
                <option value="">— Select creditor —</option>
                {creditorParties.map(p => (
                  <option key={p.id} value={p.id}>
                    {partyDisplayName(p)}{p.identifier ? ` (${p.identifier})` : ""} · {ROLE_LABEL[p.role] ?? p.role}
                  </option>
                ))}
              </select>
              {!editingId && (
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  className="text-xs h-9 gap-1 shrink-0"
                  onClick={() => setAddingIndividual(v => !v)}
                  title="Add individual (non-company) creditor"
                >
                  <UserPlus className="h-3.5 w-3.5" />
                  Individual
                </Button>
              )}
            </div>
          </div>

          {/* Inline individual party form */}
          {addingIndividual && (
            <div className="rounded-lg border border-blue-200 bg-blue-50/50 dark:bg-blue-950/20 p-3 space-y-2">
              <p className="text-[10px] font-semibold uppercase tracking-wide text-blue-600 dark:text-blue-400">
                New Individual Creditor
              </p>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
                <div>
                  <label className="text-[10px] text-muted-foreground">Full Name *</label>
                  <input
                    type="text"
                    value={indivName}
                    onChange={e => setIndivName(e.target.value)}
                    placeholder="Ion Popescu"
                    className="w-full mt-0.5 rounded-md border border-input bg-background px-3 py-1.5 text-sm"
                  />
                </div>
                <div>
                  <label className="text-[10px] text-muted-foreground">CNP / CUI</label>
                  <input
                    type="text"
                    value={indivIdentifier}
                    onChange={e => setIndivIdentifier(e.target.value)}
                    placeholder="1234567890123"
                    className="w-full mt-0.5 rounded-md border border-input bg-background px-3 py-1.5 text-sm"
                  />
                </div>
                <div>
                  <label className="text-[10px] text-muted-foreground">Role</label>
                  <select
                    value={indivRole}
                    onChange={e => setIndivRole(e.target.value)}
                    className="w-full mt-0.5 rounded-md border border-input bg-background px-3 py-1.5 text-sm"
                  >
                    {CREDITOR_ROLES.map(r => (
                      <option key={r} value={r}>{ROLE_LABEL[r]}</option>
                    ))}
                  </select>
                </div>
                <div>
                  <label className="text-[10px] text-muted-foreground">Email</label>
                  <input
                    type="email"
                    value={indivEmail}
                    onChange={e => setIndivEmail(e.target.value)}
                    placeholder="email@example.com"
                    className="w-full mt-0.5 rounded-md border border-input bg-background px-3 py-1.5 text-sm"
                  />
                </div>
              </div>
              <div className="flex justify-end gap-2">
                <Button
                  variant="ghost"
                  size="sm"
                  className="text-xs h-7"
                  onClick={() => setAddingIndividual(false)}
                >
                  Cancel
                </Button>
                <Button
                  size="sm"
                  className="text-xs h-7 gap-1"
                  onClick={handleAddIndividual}
                  disabled={indivSaving || !indivName.trim()}
                >
                  {indivSaving ? <Loader2 className="h-3 w-3 animate-spin" /> : <Check className="h-3 w-3" />}
                  Add &amp; Select
                </Button>
              </div>
            </div>
          )}

          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
            {/* Declared Amount */}
            <div>
              <label className="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">Declared Amount (RON) *</label>
              <input
                type="number"
                value={declaredAmount}
                onChange={e => setDeclaredAmount(e.target.value)}
                placeholder="0"
                className="w-full mt-0.5 rounded-md border border-input bg-background px-3 py-2 text-sm"
              />
            </div>

            {/* Admitted Amount */}
            <div>
              <label className="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">Admitted Amount (RON)</label>
              <input
                type="number"
                value={admittedAmount}
                onChange={e => setAdmittedAmount(e.target.value)}
                placeholder="Leave blank if not reviewed"
                className="w-full mt-0.5 rounded-md border border-input bg-background px-3 py-2 text-sm"
              />
            </div>

            {/* Rank */}
            <div>
              <label className="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">Rank</label>
              <select
                value={rank}
                onChange={e => setRank(e.target.value)}
                className="w-full mt-0.5 rounded-md border border-input bg-background px-3 py-2 text-sm"
              >
                {CLAIM_RANKS.map(r => <option key={r} value={r}>{RANK_LABEL[r]}</option>)}
              </select>
            </div>

            {/* Status */}
            <div>
              <label className="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">Status</label>
              <select
                value={status}
                onChange={e => setStatus(e.target.value)}
                className="w-full mt-0.5 rounded-md border border-input bg-background px-3 py-2 text-sm"
              >
                {CLAIM_STATUSES.map(s => <option key={s} value={s}>{STATUS_LABEL[s]}</option>)}
              </select>
            </div>

            {/* Received At */}
            <div>
              <label className="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">Received On</label>
              <input
                type="date"
                value={receivedAt}
                onChange={e => setReceivedAt(e.target.value)}
                className="w-full mt-0.5 rounded-md border border-input bg-background px-3 py-2 text-sm"
              />
            </div>

            {/* Nature */}
            <div>
              <label className="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">Nature</label>
              <input
                type="text"
                value={natureDescription}
                onChange={e => setNatureDescription(e.target.value)}
                placeholder="e.g. Furnizare marfuri, TVA neplătit"
                className="w-full mt-0.5 rounded-md border border-input bg-background px-3 py-2 text-sm"
              />
            </div>
          </div>

          {/* Notes */}
          <div>
            <label className="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">Notes</label>
            <input
              type="text"
              value={notes}
              onChange={e => setNotes(e.target.value)}
              placeholder="Additional observations…"
              className="w-full mt-0.5 rounded-md border border-input bg-background px-3 py-2 text-sm"
            />
          </div>

          {/* Actions */}
          <div className="flex justify-end gap-2">
            <Button variant="outline" size="sm" className="text-xs h-7" onClick={resetForm}>Cancel</Button>
            <Button
              size="sm"
              className="text-xs h-7 gap-1"
              onClick={handleSaveClaim}
              disabled={saving || !selectedPartyId || !declaredAmount}
            >
              {saving ? <Loader2 className="h-3 w-3 animate-spin" /> : <Check className="h-3 w-3" />}
              {editingId ? "Update" : "Add"}
            </Button>
          </div>
        </div>
      )}

      {/* Claims table */}
      <div className="rounded-xl border border-border bg-card divide-y divide-border">
        {claims.length === 0 ? (
          <p className="px-4 py-6 text-center text-sm text-muted-foreground">No claims recorded yet.</p>
        ) : (
          <>
            {/* Table header */}
            <div className="hidden sm:grid grid-cols-[2rem_1fr_1fr_1fr_1fr_1fr_auto] gap-2 px-4 py-2 text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">
              <span>#</span>
              <span>Creditor</span>
              <span>Declared</span>
              <span>Admitted</span>
              <span>Rank</span>
              <span>Status</span>
              <span></span>
            </div>
            {claims.map(c => (
              <div
                key={c.id}
                className="grid grid-cols-1 sm:grid-cols-[2rem_1fr_1fr_1fr_1fr_1fr_auto] gap-2 px-4 py-3 hover:bg-muted/30 transition-colors items-center"
              >
                <span className="text-xs font-medium text-muted-foreground">{c.rowNumber}</span>
                <div className="min-w-0">
                  <p className="text-sm font-medium text-foreground truncate">{c.creditorName}</p>
                  <div className="flex items-center gap-1.5 mt-0.5 flex-wrap">
                    <Badge variant="outline" className="text-[10px]">{ROLE_LABEL[c.creditorRole] ?? c.creditorRole}</Badge>
                    {c.creditorIdentifier && (
                      <span className="text-[10px] text-muted-foreground">{c.creditorIdentifier}</span>
                    )}
                  </div>
                </div>
                <div>
                  <span className="text-sm font-medium text-foreground">{formatMoney(c.declaredAmount)}</span>
                  {c.natureDescription && (
                    <p className="text-[10px] text-muted-foreground truncate">{c.natureDescription}</p>
                  )}
                </div>
                <span className="text-sm text-foreground">
                  {c.admittedAmount != null ? formatMoney(c.admittedAmount) : <span className="text-muted-foreground text-xs">Pending</span>}
                </span>
                <span className="text-xs text-foreground">{RANK_LABEL[c.rank] ?? c.rank}</span>
                <Badge variant={STATUS_VARIANT[c.status] ?? "secondary"} className="text-[10px] w-fit">
                  {STATUS_LABEL[c.status] ?? c.status}
                </Badge>
                {!readOnly && (
                  <div className="flex gap-1">
                    <button
                      type="button"
                      onClick={() => openEditForm(c)}
                      className="rounded p-1 hover:bg-accent text-muted-foreground hover:text-foreground transition-colors"
                      title="Edit claim"
                    >
                      <Pencil className="h-3.5 w-3.5" />
                    </button>
                    <button
                      type="button"
                      onClick={() => handleDelete(c.id)}
                      className="rounded p-1 hover:bg-destructive/10 text-destructive/60 hover:text-destructive transition-colors"
                      title="Delete claim"
                    >
                      <Trash2 className="h-3.5 w-3.5" />
                    </button>
                  </div>
                )}
              </div>
            ))}
          </>
        )}
      </div>
    </div>
  );
}
