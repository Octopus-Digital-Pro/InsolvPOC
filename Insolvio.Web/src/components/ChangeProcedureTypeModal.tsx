import { useState } from "react";
import { AlertTriangle, Loader2, X } from "lucide-react";
import { Button } from "@/components/ui/button";
import { casesApi } from "@/services/api";
import { useTranslation } from "@/contexts/LanguageContext";

interface Props {
  caseId: string;
  currentProcedureType: string;
  onChanged: () => void;
  onCancel: () => void;
}

export function ChangeProcedureTypeModal({ caseId, currentProcedureType, onChanged, onCancel }: Props) {
  const { t } = useTranslation();

  const procedureTypeOptions = [
    { value: "FalimentSimplificat", label: t.procedures.simplifiedBankruptcy },
    { value: "Faliment", label: t.procedures.faliment },
    { value: "Insolventa", label: t.procedures.generalInsolvency },
    { value: "Reorganizare", label: t.procedures.reorganization },
    { value: "ConcordatPreventiv", label: t.procedures.preventiveConcordat },
    { value: "MandatAdHoc", label: t.procedures.adHocMandate },
    { value: "Other", label: t.procedures.other },
  ];

  const [newType, setNewType] = useState(
    procedureTypeOptions.find(o => o.value !== currentProcedureType)?.value ?? procedureTypeOptions[0].value
  );
  const [reason, setReason] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<{ removedStages: string[]; addedStages: string[]; preservedTasks: number } | null>(null);

  const currentLabel = procedureTypeOptions.find(o => o.value === currentProcedureType)?.label ?? currentProcedureType;
  const newLabel = procedureTypeOptions.find(o => o.value === newType)?.label ?? newType;

  const canSubmit = !submitting && newType !== currentProcedureType && reason.trim().length >= 5;

  async function handleSubmit() {
    if (!canSubmit) return;
    setError(null);
    setSubmitting(true);
    try {
      const res = await casesApi.changeProcedureType(caseId, { newProcedureType: newType, reason: reason.trim() });
      setResult(res.data);
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setError(msg ?? t.changeProcedureType.errorFallback);
      setSubmitting(false);
    }
  }

  // ── Result view (after successful change) ──────────────────────────────────
  if (result) {
    return (
      <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm">
        <div className="bg-card rounded-xl shadow-xl w-full max-w-md mx-4 p-6 space-y-4">
          <div className="flex items-center gap-3">
            <div className="rounded-full bg-green-100 dark:bg-green-900/30 p-2">
              <AlertTriangle className="h-5 w-5 text-green-600 dark:text-green-400" />
            </div>
            <h2 className="text-base font-semibold">{t.changeProcedureType.resultTitle}</h2>
          </div>
          <p className="text-sm text-muted-foreground">
            {t.changeProcedureType.resultTextPrefix} <strong>{currentLabel}</strong>{" "}
            {t.changeProcedureType.resultTextMid} <strong>{newLabel}</strong>.
          </p>
          {result.removedStages.length > 0 && (
            <div className="rounded-md bg-amber-50 dark:bg-amber-900/20 border border-amber-200 dark:border-amber-800 p-3 text-xs text-amber-800 dark:text-amber-300">
              <p className="font-semibold mb-1">{t.changeProcedureType.removedStagesTitle} ({result.removedStages.length}):</p>
              <ul className="list-disc list-inside space-y-0.5">
                {result.removedStages.map(s => <li key={s}>{s}</li>)}
              </ul>
            </div>
          )}
          {result.addedStages.length > 0 && (
            <div className="rounded-md bg-blue-50 dark:bg-blue-900/20 border border-blue-200 dark:border-blue-800 p-3 text-xs text-blue-800 dark:text-blue-300">
              <p className="font-semibold mb-1">{t.changeProcedureType.addedStagesTitle} ({result.addedStages.length}):</p>
              <ul className="list-disc list-inside space-y-0.5">
                {result.addedStages.map(s => <li key={s}>{s}</li>)}
              </ul>
            </div>
          )}
          <Button className="w-full" onClick={onChanged}>{t.changeProcedureType.closeButton}</Button>
        </div>
      </div>
    );
  }

  // ── Change form ────────────────────────────────────────────────────────────
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm">
      <div className="bg-card rounded-xl shadow-xl w-full max-w-md mx-4">
        {/* Header */}
        <div className="flex items-center justify-between px-5 py-4 border-b border-border">
          <h2 className="text-base font-semibold">{t.changeProcedureType.modalTitle}</h2>
          <button type="button" onClick={onCancel} className="rounded-md p-1 hover:bg-accent text-muted-foreground">
            <X className="h-4 w-4" />
          </button>
        </div>

        {/* Body */}
        <div className="px-5 py-4 space-y-4">
          {/* Current */}
          <div className="rounded-md bg-muted/50 px-3 py-2 text-sm">
            <span className="text-muted-foreground text-xs">{t.changeProcedureType.currentTypeLabel}</span>{" "}
            <span className="font-medium">{currentLabel}</span>
          </div>

          {/* New type */}
          <div className="space-y-1">
            <label className="text-xs font-medium text-muted-foreground">{t.changeProcedureType.newTypeLabel}</label>
            <select
              value={newType}
              onChange={e => setNewType(e.target.value)}
              className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm outline-none focus:ring-1 focus:ring-primary"
            >
              {procedureTypeOptions.filter(o => o.value !== currentProcedureType).map(o => (
                <option key={o.value} value={o.value}>{o.label}</option>
              ))}
            </select>
          </div>

          {/* Reason */}
          <div className="space-y-1">
            <label className="text-xs font-medium text-muted-foreground">
              {t.changeProcedureType.reasonLabel} <span className="text-destructive">*</span>
            </label>
            <textarea
              value={reason}
              onChange={e => setReason(e.target.value)}
              placeholder={t.changeProcedureType.reasonPlaceholder}
              rows={3}
              className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm outline-none resize-none focus:ring-1 focus:ring-primary"
            />
            <p className="text-[11px] text-muted-foreground">{t.changeProcedureType.reasonHint}</p>
          </div>

          {/* Warning */}
          <div className="flex gap-2 rounded-md bg-amber-50 dark:bg-amber-900/20 border border-amber-200 dark:border-amber-800 p-3">
            <AlertTriangle className="h-4 w-4 text-amber-600 dark:text-amber-400 shrink-0 mt-0.5" />
            <p className="text-xs text-amber-800 dark:text-amber-300">
              {t.changeProcedureType.warningText}
            </p>
          </div>

          {error && (
            <p className="text-xs text-destructive flex items-center gap-1">
              <AlertTriangle className="h-3.5 w-3.5" /> {error}
            </p>
          )}
        </div>

        {/* Footer */}
        <div className="flex gap-2 px-5 py-4 border-t border-border">
          <Button variant="outline" className="flex-1" onClick={onCancel} disabled={submitting}>{t.changeProcedureType.cancelButton}</Button>
          <Button className="flex-1" onClick={handleSubmit} disabled={!canSubmit}>
            {submitting && <Loader2 className="h-3.5 w-3.5 animate-spin mr-1.5" />}
            {t.changeProcedureType.confirmButton}
          </Button>
        </div>
      </div>
    </div>
  );
}
