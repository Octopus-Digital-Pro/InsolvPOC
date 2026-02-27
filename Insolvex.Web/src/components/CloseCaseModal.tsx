import { useState, useEffect } from "react";
import { AlertTriangle, CheckCircle2, XCircle, Loader2, X, Lock } from "lucide-react";
import { Button } from "@/components/ui/button";
import { caseWorkflowApi, type CaseCloseabilityDto } from "@/services/api/caseWorkflowApi";

interface CloseCaseModalProps {
  caseId: string;
  caseName: string;
  onClosed: () => void;
  onCancel: () => void;
}

const STATUS_COLOR: Record<string, string> = {
  NotStarted: "text-muted-foreground",
  InProgress:  "text-blue-600",
  Completed:   "text-emerald-600",
  Skipped:     "text-amber-500",
};

export function CloseCaseModal({ caseId, caseName, onClosed, onCancel }: CloseCaseModalProps) {
  const [loading, setLoading]           = useState(true);
  const [closeability, setCloseability] = useState<CaseCloseabilityDto | null>(null);
  const [explanation, setExplanation]   = useState("");
  const [override, setOverride]         = useState(false);
  const [submitting, setSubmitting]     = useState(false);
  const [error, setError]               = useState<string | null>(null);

  useEffect(() => {
    caseWorkflowApi
      .getCloseability(caseId)
      .then((r) => setCloseability(r.data))
      .catch(() => setError("Failed to load stage readiness. Please try again."))
      .finally(() => setLoading(false));
  }, [caseId]);

  const requiresExplanation = !closeability?.canClose;
  const explanationValid = explanation.trim().length >= 20;
  const canSubmit =
    !submitting &&
    closeability != null &&
    (closeability.canClose || (override && explanationValid));

  async function handleSubmit() {
    if (!canSubmit) return;
    setError(null);
    setSubmitting(true);
    try {
      await caseWorkflowApi.closeCase(caseId, {
        explanation: explanation.trim() || undefined,
        overridePendingStages: override,
      });
      onClosed();
    } catch (e: unknown) {
      const msg = (e as { response?: { data?: { message?: string } } })
        ?.response?.data?.message ?? "Failed to close the case.";
      setError(msg);
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4"
      onClick={(e) => { if (e.target === e.currentTarget) onCancel(); }}
    >
      <div
        className="w-full max-w-lg rounded-xl border border-border bg-card p-6 shadow-xl"
        onClick={(e) => e.stopPropagation()}
      >
        {/* Header */}
        <div className="mb-5 flex items-start justify-between gap-4">
          <div className="flex items-center gap-2">
            <Lock className="h-5 w-5 text-destructive" />
            <h2 className="text-lg font-semibold text-card-foreground">Close Case</h2>
          </div>
          <Button variant="ghost" size="icon" onClick={onCancel} aria-label="Cancel">
            <X className="h-4 w-4" />
          </Button>
        </div>

        <p className="mb-4 text-sm text-muted-foreground">
          You are about to close <span className="font-medium text-foreground">{caseName}</span>.
          This action cannot be undone.
        </p>

        {/* Loading */}
        {loading && (
          <div className="flex items-center justify-center py-8">
            <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
          </div>
        )}

        {/* Error */}
        {error && (
          <div className="mb-4 flex items-start gap-2 rounded-lg border border-destructive/30 bg-destructive/10 p-3 text-sm text-destructive">
            <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
            {error}
          </div>
        )}

        {/* Stage readiness */}
        {closeability && !loading && (
          <>
            {closeability.canClose ? (
              <div className="mb-4 flex items-center gap-2 rounded-lg border border-emerald-200 bg-emerald-50 p-3 text-sm text-emerald-700 dark:border-emerald-800 dark:bg-emerald-950 dark:text-emerald-300">
                <CheckCircle2 className="h-4 w-4 shrink-0" />
                All workflow stages are completed or skipped. The case is ready to close.
              </div>
            ) : (
              <div className="mb-4">
                <div className="mb-2 flex items-center gap-2 rounded-lg border border-amber-200 bg-amber-50 p-3 text-sm text-amber-700 dark:border-amber-800 dark:bg-amber-950 dark:text-amber-300">
                  <AlertTriangle className="h-4 w-4 shrink-0" />
                  <span>
                    {closeability.pendingStages.length} stage
                    {closeability.pendingStages.length !== 1 && "s"} still pending.
                  </span>
                </div>
                <ul className="mb-3 space-y-1 rounded-lg border border-border bg-muted/30 p-3">
                  {closeability.pendingStages.map((s) => (
                    <li key={s.stageKey} className="flex items-center gap-2 text-sm">
                      <XCircle className="h-3.5 w-3.5 shrink-0 text-destructive" />
                      <span className="flex-1 text-foreground">{s.name}</span>
                      <span className={`text-xs font-medium ${STATUS_COLOR[s.status] ?? "text-muted-foreground"}`}>
                        {s.status}
                      </span>
                    </li>
                  ))}
                </ul>

                {/* Override toggle */}
                <label className="flex cursor-pointer items-start gap-3">
                  <input
                    type="checkbox"
                    className="mt-0.5 h-4 w-4 rounded border-border accent-destructive"
                    checked={override}
                    onChange={(e) => setOverride(e.target.checked)}
                  />
                  <span className="text-sm text-foreground">
                    Override pending stages and force-close this case.{" "}
                    <span className="text-muted-foreground">(All pending stages will be skipped.)</span>
                  </span>
                </label>
              </div>
            )}

            {/* Explanation */}
            {(requiresExplanation || closeability.canClose) && (
              <div className="mb-5">
                <label
                  htmlFor="close-explanation"
                  className="mb-1 block text-xs font-semibold uppercase tracking-wide text-muted-foreground"
                >
                  {requiresExplanation ? (
                    <>
                      Explanation{" "}
                      <span className="text-destructive">*</span>{" "}
                      <span className="normal-case font-normal">(required when overriding, min 20 chars)</span>
                    </>
                  ) : (
                    "Closure notes (optional)"
                  )}
                </label>
                <textarea
                  id="close-explanation"
                  rows={3}
                  className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm text-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-ring"
                  placeholder={
                    requiresExplanation
                      ? "Describe why you are force-closing this case…"
                      : "Add any final notes for this case…"
                  }
                  value={explanation}
                  onChange={(e) => setExplanation(e.target.value)}
                />
                {requiresExplanation && override && !explanationValid && explanation.length > 0 && (
                  <p className="mt-1 text-xs text-destructive">
                    Explanation must be at least 20 characters.
                  </p>
                )}
              </div>
            )}

            {/* Actions */}
            <div className="flex justify-end gap-2">
              <Button variant="outline" onClick={onCancel} disabled={submitting}>
                Cancel
              </Button>
              <Button
                variant="destructive"
                onClick={handleSubmit}
                disabled={!canSubmit}
              >
                {submitting ? (
                  <><Loader2 className="mr-2 h-4 w-4 animate-spin" /> Closing…</>
                ) : (
                  <><Lock className="mr-2 h-4 w-4" /> Close Case</>
                )}
              </Button>
            </div>
          </>
        )}
      </div>
    </div>
  );
}
