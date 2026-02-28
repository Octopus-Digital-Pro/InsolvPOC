import { useState, useEffect, useCallback } from "react";
import { casesApi } from "@/services/api";
import type { AssetDto } from "@/services/api/types";
import type { CasePartyDto } from "@/services/api/types";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Plus, Trash2, Pencil, Loader2, Package, X, Check,
} from "lucide-react";

const ASSET_TYPES = [
  "Vehicle", "RealEstate", "Receivable", "Inventory",
  "Equipment", "IP", "Cash", "Other",
] as const;

const ASSET_STATUSES = [
  "Identified", "Valued", "ForSale", "Sold", "Unrecoverable",
] as const;

const STATUS_VARIANT: Record<string, "default" | "secondary" | "outline" | "success" | "warning" | "destructive"> = {
  Identified: "secondary",
  Valued: "outline",
  ForSale: "warning",
  Sold: "success",
  Unrecoverable: "destructive",
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

export default function CaseAssetsTab({ caseId, parties, readOnly = false }: Props) {
  const [assets, setAssets] = useState<AssetDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [showForm, setShowForm] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  // Form state
  const [assetType, setAssetType] = useState("Other");
  const [description, setDescription] = useState("");
  const [estimatedValue, setEstimatedValue] = useState("");
  const [encumbranceDetails, setEncumbranceDetails] = useState("");
  const [securedCreditorPartyId, setSecuredCreditorPartyId] = useState<string>("");
  const [status, setStatus] = useState("Identified");
  const [saleProceeds, setSaleProceeds] = useState("");
  const [notes, setNotes] = useState("");

  const loadAssets = useCallback(async () => {
    try {
      const { data } = await casesApi.getAssets(caseId);
      setAssets(data);
    } catch { /* ignore */ }
    finally { setLoading(false); }
  }, [caseId]);

  useEffect(() => { loadAssets(); }, [loadAssets]);

  const resetForm = () => {
    setAssetType("Other");
    setDescription("");
    setEstimatedValue("");
    setEncumbranceDetails("");
    setSecuredCreditorPartyId("");
    setStatus("Identified");
    setSaleProceeds("");
    setNotes("");
    setEditingId(null);
    setShowForm(false);
  };

  const openCreateForm = () => {
    resetForm();
    setShowForm(true);
  };

  const openEditForm = (a: AssetDto) => {
    setAssetType(a.assetType);
    setDescription(a.description);
    setEstimatedValue(a.estimatedValue?.toString() ?? "");
    setEncumbranceDetails(a.encumbranceDetails ?? "");
    setSecuredCreditorPartyId(a.securedCreditorPartyId ?? "");
    setStatus(a.status);
    setSaleProceeds(a.saleProceeds?.toString() ?? "");
    setNotes(a.notes ?? "");
    setEditingId(a.id);
    setShowForm(true);
  };

  const handleSave = async () => {
    if (!description.trim()) return;
    setSaving(true);
    try {
      const payload = {
        assetType,
        description: description.trim(),
        estimatedValue: estimatedValue ? parseFloat(estimatedValue) : null,
        encumbranceDetails: encumbranceDetails || null,
        securedCreditorPartyId: securedCreditorPartyId || null,
        status,
        saleProceeds: saleProceeds ? parseFloat(saleProceeds) : null,
        notes: notes || null,
      };

      if (editingId) {
        await casesApi.updateAsset(caseId, editingId, payload);
      } else {
        await casesApi.createAsset(caseId, payload);
      }
      resetForm();
      await loadAssets();
    } catch { /* ignore */ }
    finally { setSaving(false); }
  };

  const handleDelete = async (assetId: string) => {
    if (!confirm("Delete this asset?")) return;
    try {
      await casesApi.deleteAsset(caseId, assetId);
      await loadAssets();
    } catch { /* ignore */ }
  };

  // Summary stats
  const totalEstimated = assets.reduce((sum, a) => sum + (a.estimatedValue ?? 0), 0);
  const totalProceeds = assets.reduce((sum, a) => sum + (a.saleProceeds ?? 0), 0);

  const creditorParties = parties.filter(p =>
    ["SecuredCreditor", "UnsecuredCreditor", "BudgetaryCreditor"].includes(p.role)
  );

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
          <Package className="h-3.5 w-3.5" /> Assets ({assets.length})
        </h2>
        {!readOnly && (
          <Button size="sm" className="gap-1.5 text-xs h-7" onClick={openCreateForm}>
            <Plus className="h-3.5 w-3.5" /> Add Asset
          </Button>
        )}
      </div>

      {/* Summary banner */}
      {assets.length > 0 && (
        <div className="flex gap-4 rounded-lg border border-border bg-muted/30 px-4 py-2.5 text-xs">
          <div>
            <span className="text-muted-foreground">Estimated Total:</span>{" "}
            <span className="font-semibold text-foreground">{formatMoney(totalEstimated)}</span>
          </div>
          <div>
            <span className="text-muted-foreground">Sale Proceeds:</span>{" "}
            <span className="font-semibold text-foreground">{formatMoney(totalProceeds)}</span>
          </div>
          <div>
            <span className="text-muted-foreground">Items:</span>{" "}
            <span className="font-semibold text-foreground">{assets.length}</span>
          </div>
        </div>
      )}

      {/* Inline form */}
      {showForm && (
        <div className="rounded-xl border border-primary/30 bg-card p-4 space-y-3">
          <div className="flex items-center justify-between">
            <h3 className="text-sm font-semibold text-foreground">
              {editingId ? "Edit Asset" : "Add Asset"}
            </h3>
            <button onClick={resetForm} className="text-muted-foreground hover:text-foreground">
              <X className="h-4 w-4" />
            </button>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
            {/* Asset Type */}
            <div>
              <label className="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">Type</label>
              <select value={assetType} onChange={e => setAssetType(e.target.value)}
                className="w-full mt-0.5 rounded-md border border-input bg-background px-3 py-2 text-sm">
                {ASSET_TYPES.map(t => <option key={t} value={t}>{t}</option>)}
              </select>
            </div>

            {/* Status */}
            <div>
              <label className="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">Status</label>
              <select value={status} onChange={e => setStatus(e.target.value)}
                className="w-full mt-0.5 rounded-md border border-input bg-background px-3 py-2 text-sm">
                {ASSET_STATUSES.map(s => <option key={s} value={s}>{s}</option>)}
              </select>
            </div>

            {/* Estimated Value */}
            <div>
              <label className="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">Estimated Value (RON)</label>
              <input type="number" value={estimatedValue} onChange={e => setEstimatedValue(e.target.value)}
                placeholder="0"
                className="w-full mt-0.5 rounded-md border border-input bg-background px-3 py-2 text-sm" />
            </div>

            {/* Sale Proceeds */}
            {(status === "Sold" || saleProceeds) && (
              <div>
                <label className="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">Sale Proceeds (RON)</label>
                <input type="number" value={saleProceeds} onChange={e => setSaleProceeds(e.target.value)}
                  placeholder="0"
                  className="w-full mt-0.5 rounded-md border border-input bg-background px-3 py-2 text-sm" />
              </div>
            )}

            {/* Secured Creditor */}
            <div>
              <label className="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">Secured Creditor</label>
              <select value={securedCreditorPartyId} onChange={e => setSecuredCreditorPartyId(e.target.value)}
                className="w-full mt-0.5 rounded-md border border-input bg-background px-3 py-2 text-sm">
                <option value="">— None —</option>
                {creditorParties.map(p => (
                  <option key={p.id} value={p.id}>{p.companyName} ({p.role})</option>
                ))}
              </select>
            </div>

            {/* Encumbrance Details */}
            <div>
              <label className="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">Encumbrance Details</label>
              <input type="text" value={encumbranceDetails} onChange={e => setEncumbranceDetails(e.target.value)}
                placeholder="Mortgage, lien, etc."
                className="w-full mt-0.5 rounded-md border border-input bg-background px-3 py-2 text-sm" />
            </div>
          </div>

          {/* Description */}
          <div>
            <label className="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">Description *</label>
            <textarea value={description} onChange={e => setDescription(e.target.value)}
              placeholder="Describe the asset…"
              rows={2}
              className="w-full mt-0.5 rounded-md border border-input bg-background px-3 py-2 text-sm resize-none" />
          </div>

          {/* Notes */}
          <div>
            <label className="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">Notes</label>
            <input type="text" value={notes} onChange={e => setNotes(e.target.value)}
              placeholder="Location, condition, etc."
              className="w-full mt-0.5 rounded-md border border-input bg-background px-3 py-2 text-sm" />
          </div>

          {/* Actions */}
          <div className="flex justify-end gap-2">
            <Button variant="outline" size="sm" className="text-xs h-7" onClick={resetForm}>Cancel</Button>
            <Button size="sm" className="text-xs h-7 gap-1" onClick={handleSave} disabled={saving || !description.trim()}>
              {saving ? <Loader2 className="h-3 w-3 animate-spin" /> : <Check className="h-3 w-3" />}
              {editingId ? "Update" : "Add"}
            </Button>
          </div>
        </div>
      )}

      {/* Asset list */}
      <div className="rounded-xl border border-border bg-card divide-y divide-border">
        {assets.length === 0 ? (
          <p className="px-4 py-6 text-center text-sm text-muted-foreground">No assets recorded yet.</p>
        ) : (
          assets.map(a => (
            <div key={a.id} className="px-4 py-3 flex items-start gap-3 group">
              <Package className="h-4 w-4 text-muted-foreground shrink-0 mt-0.5" />
              <div className="min-w-0 flex-1 space-y-0.5">
                <div className="flex items-center gap-2">
                  <p className="text-sm font-medium text-foreground truncate">{a.description}</p>
                  <Badge variant={STATUS_VARIANT[a.status] ?? "outline"} className="text-[10px] shrink-0">
                    {a.status}
                  </Badge>
                  <Badge variant="secondary" className="text-[10px] shrink-0">{a.assetType}</Badge>
                </div>
                <div className="flex flex-wrap gap-x-4 gap-y-0.5 text-[11px] text-muted-foreground">
                  {a.estimatedValue != null && (
                    <span>Est: <span className="text-foreground font-medium">{formatMoney(a.estimatedValue)}</span></span>
                  )}
                  {a.saleProceeds != null && (
                    <span>Sold: <span className="text-foreground font-medium">{formatMoney(a.saleProceeds)}</span></span>
                  )}
                  {a.securedCreditorName && (
                    <span>Creditor: <span className="text-foreground">{a.securedCreditorName}</span></span>
                  )}
                  {a.encumbranceDetails && (
                    <span>Enc: <span className="text-foreground">{a.encumbranceDetails}</span></span>
                  )}
                  {a.notes && (
                    <span className="italic">{a.notes}</span>
                  )}
                </div>
              </div>
              <div className="flex gap-1 shrink-0 opacity-0 group-hover:opacity-100 transition-opacity">
                {!readOnly && (
                  <>
                <button onClick={() => openEditForm(a)} className="p-1 rounded-md hover:bg-accent text-muted-foreground hover:text-foreground">
                  <Pencil className="h-3.5 w-3.5" />
                </button>
                <button onClick={() => handleDelete(a.id)} className="p-1 rounded-md hover:bg-destructive/10 text-muted-foreground hover:text-destructive">
                  <Trash2 className="h-3.5 w-3.5" />
                </button>
                  </>
                )}
              </div>
            </div>
          ))
        )}
      </div>
    </div>
  );
}
