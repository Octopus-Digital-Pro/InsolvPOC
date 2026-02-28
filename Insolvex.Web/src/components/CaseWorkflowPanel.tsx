import { useState, useEffect, useCallback } from "react";
import { caseWorkflowApi } from "@/services/api/caseWorkflowApi";
import type { CaseWorkflowStageDto, ValidationResultDto } from "@/services/api/caseWorkflowApi";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { useTranslation } from "@/contexts/LanguageContext";
import { useAuth } from "@/contexts/AuthContext";
import {
  CheckCircle2, Circle, Play, SkipForward, RotateCcw,
  Loader2, ChevronDown, ChevronUp, Lock,
  AlertTriangle, X, Calendar, Pencil,
} from "lucide-react";

interface Props {
  caseId: string;
  readOnly?: boolean;
}

export default function CaseWorkflowPanel({ caseId, readOnly = false }: Props) {
  const { t } = useTranslation();
  const { isTenantAdmin } = useAuth();

  const STATUS_CONFIG: Record<string, { label: string; variant: "default" | "success" | "warning" | "outline" | "secondary"; icon: typeof Circle }> = {
    NotStarted: { label: t.workflow.notStarted, variant: "outline", icon: Circle },
    InProgress:  { label: t.workflow.inProgress, variant: "warning", icon: Play },
    Completed:   { label: t.workflow.completed,  variant: "success", icon: CheckCircle2 },
    Skipped:     { label: t.workflow.skipped,    variant: "secondary", icon: SkipForward },
  };

  const [stages, setStages] = useState<CaseWorkflowStageDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [actionLoading, setActionLoading] = useState<string | null>(null);
  const [expandedStage, setExpandedStage] = useState<string | null>(null);
  const [skipReason, setSkipReason] = useState("");
  const [skipConfirm, setSkipConfirm] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  // Deadline override (tenant admin only)
  const [deadlineEdit, setDeadlineEdit] = useState<string | null>(null);
  const [deadlineDate, setDeadlineDate] = useState("");
  const [deadlineNote, setDeadlineNote] = useState("");
  const [deadlineLoading, setDeadlineLoading] = useState(false);
  const [deadlineError, setDeadlineError] = useState<string | null>(null);

  const loadStages = useCallback(async () => {
    try {
      const res = await caseWorkflowApi.getStages(caseId);
      setStages(res.data);
    } catch (e: unknown) {
      const msg = e instanceof Error ? e.message : "Failed to load workflow stages";
      setError(msg);
    } finally {
      setLoading(false);
    }
  }, [caseId]);

  useEffect(() => { loadStages(); }, [loadStages]);

  const handleAction = async (
    action: "start" | "complete" | "skip" | "reopen",
    stageKey: string,
    reason?: string,
  ) => {
    setActionLoading(stageKey);
    setError(null);
    try {
      switch (action) {
        case "start":    await caseWorkflowApi.start(caseId, stageKey); break;
        case "complete": await caseWorkflowApi.complete(caseId, stageKey); break;
        case "skip":     await caseWorkflowApi.skip(caseId, stageKey, reason); break;
        case "reopen":   await caseWorkflowApi.reopen(caseId, stageKey); break;
      }
      setSkipConfirm(null);
      setSkipReason("");
      await loadStages();
    } catch (e: unknown) {
      const errMsg = (e as { response?: { data?: { message?: string } } })?.response?.data?.message
        || (e instanceof Error ? e.message : "Action failed");
      setError(errMsg);
    } finally {
      setActionLoading(null);
    }
  };

  const handleSetDeadline = async (stageKey: string) => {
    if (!deadlineDate || !deadlineNote.trim()) return;
    setDeadlineLoading(true);
    setDeadlineError(null);
    try {
      await caseWorkflowApi.setDeadline(caseId, stageKey, {
        newDate: new Date(deadlineDate).toISOString(),
        note: deadlineNote.trim(),
      });
      setDeadlineEdit(null);
      setDeadlineDate("");
      setDeadlineNote("");
      await loadStages();
    } catch (e: unknown) {
      const msg = (e as { response?: { data?: { message?: string } } })?.response?.data?.message
        ?? "Failed to update deadline.";
      setDeadlineError(msg);
    } finally {
      setDeadlineLoading(false);
    }
  };

  /** Determine if a stage is gated (prior stages not all complete/skipped). */
  const isGated = (stage: CaseWorkflowStageDto): boolean => {
    return stages
      .filter(s => s.sortOrder < stage.sortOrder)
      .some(s => s.status !== "Completed" && s.status !== "Skipped");
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center py-8">
        <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (stages.length === 0) {
    return (
      <p className="py-6 text-center text-sm text-muted-foreground">
        {t.workflow.noStages}
      </p>
    );
  }

  return (
    <div className="space-y-3">
      {/* Progress bar */}
      <ProgressBar stages={stages} />

      {/* Error banner */}
      {error && (
        <div className="flex items-center gap-2 rounded-lg border border-destructive/30 bg-destructive/5 px-3 py-2 text-sm text-destructive">
          <AlertTriangle className="h-4 w-4 shrink-0" />
          <span className="flex-1">{error}</span>
          <button onClick={() => setError(null)}><X className="h-3.5 w-3.5" /></button>
        </div>
      )}

      {/* Stage list */}
      <div className="space-y-2">
        {stages.map((stage) => {
          const config = STATUS_CONFIG[stage.status] || STATUS_CONFIG.NotStarted;
          const StatusIcon = config.icon;
          const gated = isGated(stage);
          const expanded = expandedStage === stage.stageKey;
          const isActioning = actionLoading === stage.stageKey;

          return (
            <div
              key={stage.id}
              className={`rounded-lg border bg-card transition-colors ${
                stage.status === "InProgress"
                  ? "border-amber-400/50 ring-1 ring-amber-400/20"
                  : stage.status === "Completed"
                  ? "border-emerald-400/30"
                  : "border-border"
              }`}
            >
              {/* Stage header */}
              <button
                className="flex w-full items-center gap-3 px-4 py-3 text-left"
                onClick={() => setExpandedStage(expanded ? null : stage.stageKey)}
              >
                {/* Step number + icon */}
                <div className={`flex h-8 w-8 items-center justify-center rounded-full text-xs font-bold shrink-0 ${
                  stage.status === "Completed"
                    ? "bg-emerald-500/15 text-emerald-600"
                    : stage.status === "InProgress"
                    ? "bg-amber-500/15 text-amber-600"
                    : stage.status === "Skipped"
                    ? "bg-muted text-muted-foreground line-through"
                    : gated
                    ? "bg-muted text-muted-foreground"
                    : "bg-primary/10 text-primary"
                }`}>
                  {stage.status === "Completed"
                    ? <CheckCircle2 className="h-4 w-4" />
                    : gated && stage.status === "NotStarted"
                    ? <Lock className="h-3.5 w-3.5" />
                    : <StatusIcon className="h-4 w-4" />}
                </div>

                {/* Name + description */}
                <div className="min-w-0 flex-1">
                  <p className={`text-sm font-medium ${
                    stage.status === "Skipped" ? "text-muted-foreground line-through" : "text-foreground"
                  }`}>
                    {stage.sortOrder + 1}. {stage.name}
                  </p>
                  {stage.description && (
                    <p className="text-xs text-muted-foreground truncate">{stage.description}</p>
                  )}
                </div>

                {/* Badge */}
                <Badge variant={config.variant} className="text-[10px] shrink-0">
                  {config.label}
                </Badge>

                {/* Expand chevron */}
                {expanded
                  ? <ChevronUp className="h-4 w-4 text-muted-foreground shrink-0" />
                  : <ChevronDown className="h-4 w-4 text-muted-foreground shrink-0" />}
              </button>

              {/* Expanded details */}
              {expanded && (
                <div className="border-t border-border px-4 py-3 space-y-3">
                  {/* Deadline row */}
                  <div className="flex items-start gap-2">
                    <Calendar className="mt-0.5 h-3.5 w-3.5 shrink-0 text-muted-foreground" />
                    <div className="flex-1">
                      <div className="flex items-center gap-2">
                        <span className="text-xs text-muted-foreground">
                          Deadline:{" "}
                          <span className={stage.deadlineDate ? "font-medium text-foreground" : "italic"}>
                            {stage.deadlineDate
                              ? new Date(stage.deadlineDate).toLocaleDateString("en-GB")
                              : "Not set"}
                          </span>
                        </span>
                        {stage.deadlineOverriddenAt && (
                          <span className="rounded-full bg-amber-100 px-1.5 py-0.5 text-[10px] font-medium text-amber-700 dark:bg-amber-900/40 dark:text-amber-300">
                            Overridden
                          </span>
                        )}
                        {isTenantAdmin && !readOnly && deadlineEdit !== stage.stageKey && (
                          <button
                            onClick={() => {
                              setDeadlineEdit(stage.stageKey);
                              setDeadlineDate(
                                stage.deadlineDate
                                  ? new Date(stage.deadlineDate).toISOString().substring(0, 10)
                                  : ""
                              );
                              setDeadlineNote("");
                              setDeadlineError(null);
                            }}
                            className="flex items-center gap-1 rounded px-1.5 py-0.5 text-[10px] text-muted-foreground hover:bg-muted hover:text-foreground transition-colors"
                          >
                            <Pencil className="h-2.5 w-2.5" /> Override
                          </button>
                        )}
                      </div>
                      {stage.deadlineOverriddenAt && stage.deadlineOverriddenBy && (
                        <p className="mt-0.5 text-[10px] text-muted-foreground">
                          Set by {stage.deadlineOverriddenBy} on{" "}
                          {new Date(stage.deadlineOverriddenAt).toLocaleDateString("en-GB")}
                          {stage.deadlineOverrideNote && (
                            <span> · "{stage.deadlineOverrideNote}"</span>
                          )}
                        </p>
                      )}

                      {/* Inline deadline override form */}
                      {isTenantAdmin && !readOnly && deadlineEdit === stage.stageKey && (
                        <div className="mt-2 rounded-md border border-border bg-muted/30 p-3 space-y-2">
                          <p className="text-xs font-medium text-foreground">Override Deadline</p>
                          <div className="grid grid-cols-2 gap-2">
                            <div>
                              <label className="mb-1 block text-[10px] uppercase tracking-wide text-muted-foreground">New date *</label>
                              <input
                                type="date"
                                className="w-full rounded-md border border-input bg-background px-2.5 py-1 text-xs focus:outline-none focus:ring-1 focus:ring-ring"
                                value={deadlineDate}
                                onChange={(e) => setDeadlineDate(e.target.value)}
                              />
                            </div>
                            <div>
                              <label className="mb-1 block text-[10px] uppercase tracking-wide text-muted-foreground">Note *</label>
                              <input
                                type="text"
                                placeholder="Reason for change…"
                                className="w-full rounded-md border border-input bg-background px-2.5 py-1 text-xs focus:outline-none focus:ring-1 focus:ring-ring"
                                value={deadlineNote}
                                onChange={(e) => setDeadlineNote(e.target.value)}
                              />
                            </div>
                          </div>
                          {deadlineError && (
                            <p className="text-xs text-destructive">{deadlineError}</p>
                          )}
                          <div className="flex gap-2">
                            <Button
                              size="sm"
                              className="h-7 text-xs"
                              onClick={() => handleSetDeadline(stage.stageKey)}
                              disabled={deadlineLoading || !deadlineDate || !deadlineNote.trim()}
                            >
                              {deadlineLoading
                                ? <Loader2 className="mr-1 h-3 w-3 animate-spin" />
                                : <Calendar className="mr-1 h-3 w-3" />}
                              Save
                            </Button>
                            <Button
                              size="sm"
                              variant="ghost"
                              className="h-7 text-xs"
                              onClick={() => { setDeadlineEdit(null); setDeadlineError(null); }}
                            >
                              Cancel
                            </Button>
                          </div>
                        </div>
                      )}
                    </div>
                  </div>

                  {/* Timestamps */}
                  <div className="flex flex-wrap gap-4 text-xs text-muted-foreground">
                    {stage.startedAt && (
                      <span>{t.workflow.startedLabel}: {new Date(stage.startedAt).toLocaleDateString("ro-RO")}</span>
                    )}
                    {stage.completedAt && (
                      <span>
                        {stage.status === "Skipped" ? t.workflow.skippedLabel : t.workflow.completedLabel}:{" "}
                        {new Date(stage.completedAt).toLocaleDateString("ro-RO")}
                      </span>
                    )}
                    {stage.completedBy && <span>{t.workflow.byLabel}: {stage.completedBy}</span>}
                  </div>

                  {/* Validation details */}
                  {stage.validation && !stage.validation.canComplete && (
                    <ValidationPanel validation={stage.validation} />
                  )}
                  {stage.validation?.canComplete && stage.status === "InProgress" && (
                    <div className="flex items-center gap-1.5 text-xs text-emerald-600">
                      <CheckCircle2 className="h-3.5 w-3.5" />
                      {t.workflow.requirementsMet}
                    </div>
                  )}

                  {/* Gating info */}
                  {gated && stage.status === "NotStarted" && (
                    <div className="flex items-center gap-1.5 text-xs text-muted-foreground">
                      <Lock className="h-3.5 w-3.5" />
                      {t.workflow.gatedMessage}
                    </div>
                  )}

                  {/* Skip confirmation */}
                  {skipConfirm === stage.stageKey && !readOnly && (
                    <div className="rounded-md border border-amber-400/30 bg-amber-500/5 p-3 space-y-2">
                      <p className="text-xs font-medium text-amber-700">{t.workflow.skipStagePrompt}</p>
                      <input
                        type="text"
                        placeholder={t.workflow.skipReason}
                        value={skipReason}
                        onChange={(e) => setSkipReason(e.target.value)}
                        className="w-full rounded-md border border-border bg-background px-2.5 py-1.5 text-xs"
                      />
                      <div className="flex gap-2">
                        <Button
                          size="sm"
                          variant="outline"
                          className="h-7 text-xs"
                          onClick={() => handleAction("skip", stage.stageKey, skipReason)}
                          disabled={isActioning}
                        >
                          {isActioning ? <Loader2 className="h-3 w-3 animate-spin mr-1" /> : null}
                          {t.workflow.confirmSkip}
                        </Button>
                        <Button
                          size="sm"
                          variant="ghost"
                          className="h-7 text-xs"
                          onClick={() => { setSkipConfirm(null); setSkipReason(""); }}
                        >
                          {t.common.cancel}
                        </Button>
                      </div>
                    </div>
                  )}

                  {/* Action buttons */}
                  {skipConfirm !== stage.stageKey && !readOnly && (
                    <div className="flex gap-2">
                      {/* Start */}
                      {stage.status === "NotStarted" && !gated && (
                        <Button
                          size="sm"
                          className="h-7 text-xs gap-1"
                          onClick={() => handleAction("start", stage.stageKey)}
                          disabled={isActioning}
                        >
                          {isActioning ? <Loader2 className="h-3 w-3 animate-spin" /> : <Play className="h-3 w-3" />}
                          {t.workflow.startStage}
                        </Button>
                      )}

                      {/* Complete */}
                      {stage.status === "InProgress" && (
                        <Button
                          size="sm"
                          className="h-7 text-xs gap-1"
                          onClick={() => handleAction("complete", stage.stageKey)}
                          disabled={isActioning || (stage.validation != null && !stage.validation.canComplete)}
                        >
                          {isActioning ? <Loader2 className="h-3 w-3 animate-spin" /> : <CheckCircle2 className="h-3 w-3" />}
                          {t.workflow.complete}
                        </Button>
                      )}

                      {/* Skip */}
                      {(stage.status === "NotStarted" || stage.status === "InProgress") && (
                        <Button
                          size="sm"
                          variant="outline"
                          className="h-7 text-xs gap-1"
                          onClick={() => setSkipConfirm(stage.stageKey)}
                          disabled={isActioning}
                        >
                          <SkipForward className="h-3 w-3" />
                          {t.workflow.skip}
                        </Button>
                      )}

                      {/* Reopen */}
                      {(stage.status === "Completed" || stage.status === "Skipped") && (
                        <Button
                          size="sm"
                          variant="outline"
                          className="h-7 text-xs gap-1"
                          onClick={() => handleAction("reopen", stage.stageKey)}
                          disabled={isActioning}
                        >
                          {isActioning ? <Loader2 className="h-3 w-3 animate-spin" /> : <RotateCcw className="h-3 w-3" />}
                          {t.workflow.reopen}
                        </Button>
                      )}
                    </div>
                  )}
                </div>
              )}
            </div>
          );
        })}
      </div>
    </div>
  );
}

/* ── Sub-components ────────────────────────────────────────────────────── */

function ProgressBar({ stages }: { stages: CaseWorkflowStageDto[] }) {
  const { t } = useTranslation();
  const total = stages.length;
  const completed = stages.filter(s => s.status === "Completed" || s.status === "Skipped").length;
  const pct = total > 0 ? Math.round((completed / total) * 100) : 0;

  return (
    <div className="space-y-1">
      <div className="flex items-center justify-between text-xs">
        <span className="font-medium text-foreground">{t.workflow.progressTitle}</span>
        <span className="text-muted-foreground">{completed}/{total} {t.workflow.stages} · {pct}%</span>
      </div>
      <div className="h-2 w-full rounded-full bg-muted overflow-hidden">
        <div
          className="h-full rounded-full bg-emerald-500 transition-all duration-500"
          style={{ width: `${pct}%` }}
        />
      </div>
    </div>
  );
}

function ValidationPanel({ validation }: { validation: ValidationResultDto }) {
  const { t } = useTranslation();
  const sections: { label: string; items: string[] }[] = [];
  if (validation.missingFields.length > 0)
    sections.push({ label: t.workflow.missingFields, items: validation.missingFields });
  if (validation.missingPartyRoles.length > 0)
    sections.push({ label: t.workflow.missingPartyRoles, items: validation.missingPartyRoles });
  if (validation.missingDocTypes.length > 0)
    sections.push({ label: t.workflow.missingDocuments, items: validation.missingDocTypes });
  if (validation.missingTasks.length > 0)
    sections.push({ label: t.workflow.incompleteTasks, items: validation.missingTasks });

  if (sections.length === 0) return null;

  return (
    <div className="rounded-md border border-amber-400/30 bg-amber-500/5 p-3 space-y-2">
      <p className="flex items-center gap-1.5 text-xs font-medium text-amber-700">
        <AlertTriangle className="h-3.5 w-3.5" />
        {t.workflow.requirementsNotMet}
      </p>
      {sections.map((sec) => (
        <div key={sec.label}>
          <p className="text-[10px] font-semibold uppercase tracking-wide text-amber-600/80">{sec.label}</p>
          <ul className="mt-0.5 space-y-0.5">
            {sec.items.map((item) => (
              <li key={item} className="flex items-center gap-1.5 text-xs text-amber-700">
                <span className="h-1 w-1 rounded-full bg-amber-500 shrink-0" />
                {item}
              </li>
            ))}
          </ul>
        </div>
      ))}
    </div>
  );
}
