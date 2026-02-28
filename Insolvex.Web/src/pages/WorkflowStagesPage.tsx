/**
 * WorkflowStagesPage — Manage insolvency procedure workflow stages.
 *
 * Lists all effective stages (tenant override → global fallback), allows
 * editing stage details (name, sort order, JSON configs, linked templates),
 * creating tenant overrides, and reverting to global.
 */
import { useState, useEffect, useCallback } from "react";
import {
  workflowStagesApi,
  type WorkflowStageDto,
  type WorkflowStageDetailDto,
  type UpsertWorkflowStageCommand,
  type UpsertStageTemplateItem,
} from "@/services/api/workflowStagesApi";
import {
  documentTemplatesApi,
  type DocumentTemplateDto,
} from "@/services/api/documentTemplatesApi";
import { useTranslation } from "@/contexts/LanguageContext";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import {
  Loader2, ArrowLeft, Save, CheckCircle2, Trash2,
  Plus, ChevronRight, GripVertical, FileText, Globe,
  Building2, RotateCcw, AlertTriangle, Layers,
} from "lucide-react";

// ── Stage list card ───────────────────────────────────────────────────────────

function StageCard({
  stage,
  onClick,
}: {
  stage: WorkflowStageDto;
  onClick: () => void;
}) {
  const isGlobal = stage.tenantId === null;
  const { t } = useTranslation();

  return (
    <button
      type="button"
      onClick={onClick}
      className="flex items-center gap-3 w-full text-left rounded-lg border border-border bg-card p-4 hover:border-primary/40 transition-colors"
    >
      <div className="flex items-center gap-2 shrink-0 text-muted-foreground">
        <GripVertical className="h-4 w-4" />
        <span className="text-xs font-mono w-5 text-center">{stage.sortOrder}</span>
      </div>

      <div className="rounded-md bg-primary/10 p-2 shrink-0">
        <Layers className="h-4 w-4 text-primary" />
      </div>

      <div className="flex-1 min-w-0">
        <p className="text-sm font-medium leading-tight">{stage.name}</p>
        <div className="flex items-center gap-2 mt-1 flex-wrap">
          <span className="text-xs font-mono text-muted-foreground">{stage.stageKey}</span>
          {isGlobal ? (
            <Badge variant="outline" className="text-[10px] gap-1">
              <Globe className="h-2.5 w-2.5" />
              {t.workflowStages.globalBadge}
            </Badge>
          ) : (
            <Badge className="text-[10px] gap-1 bg-blue-100 text-blue-700 dark:bg-blue-900/30 dark:text-blue-400 border-0">
              <Building2 className="h-2.5 w-2.5" />
              {t.workflowStages.overrideTenant}
            </Badge>
          )}
          {stage.templateCount > 0 && (
            <Badge variant="secondary" className="text-[10px] gap-1">
              <FileText className="h-2.5 w-2.5" />
              {stage.templateCount} {stage.templateCount === 1 ? "template" : "templates"}
            </Badge>
          )}
          {!stage.isActive && (
            <Badge variant="secondary" className="text-[10px] stageinactive-badge">{t.workflowStages.inactive}</Badge>
          )}
        </div>
        {stage.description && (
          <p className="text-xs text-muted-foreground mt-1 truncate">{stage.description}</p>
        )}
      </div>

      <ChevronRight className="h-4 w-4 text-muted-foreground shrink-0" />
    </button>
  );
}

// ── Template link row ─────────────────────────────────────────────────────────

interface TemplateLink {
  documentTemplateId: string;
  templateName: string;
  isRequired: boolean;
  sortOrder: number;
  notes: string;
}

function TemplateLinkRow({
  link,
  onChange,
  onRemove,
}: {
  link: TemplateLink;
  onChange: (updated: TemplateLink) => void;
  onRemove: () => void;
}) {
  const { t } = useTranslation();
  return (
    <div className="flex items-center gap-2 rounded-md border border-border bg-muted/20 px-3 py-2">
      <FileText className="h-3.5 w-3.5 text-muted-foreground shrink-0" />
      <span className="text-sm flex-1 min-w-0 truncate">{link.templateName}</span>
      <label className="flex items-center gap-1.5 text-xs text-muted-foreground cursor-pointer select-none shrink-0">
        <input
          type="checkbox"
          checked={link.isRequired}
          onChange={(e) => onChange({ ...link, isRequired: e.target.checked })}
          className="rounded"
        />
        {t.workflowStages.isRequired}
      </label>
      <input
        type="number"
        value={link.sortOrder}
        onChange={(e) => onChange({ ...link, sortOrder: parseInt(e.target.value) || 0 })}
        className="w-14 rounded border border-input bg-background px-2 py-1 text-xs text-center outline-none"
        title={t.workflowStages.orderTitle}
      />
      <button
        type="button"
        onClick={onRemove}
        className="rounded p-1 hover:bg-destructive/10 text-destructive"
        title={t.workflowStages.removeTitle}
      >
        <Trash2 className="h-3.5 w-3.5" />
      </button>
    </div>
  );
}

// ── JSON field editor helper ──────────────────────────────────────────────────

function JsonFieldEditor({
  label,
  value,
  onChange,
  placeholder,
}: {
  label: string;
  value: string;
  onChange: (v: string) => void;
  placeholder?: string;
}) {
  const [error, setError] = useState<string | null>(null);

  const handleBlur = () => {
    if (!value.trim()) {
      setError(null);
      return;
    }
    try {
      JSON.parse(value);
      setError(null);
    } catch {
      setError("JSON invalid");
    }
  };

  return (
    <div className="space-y-1">
      <label className="text-xs font-medium text-muted-foreground">{label}</label>
      <textarea
        value={value}
        onChange={(e) => onChange(e.target.value)}
        onBlur={handleBlur}
        placeholder={placeholder ?? '["value1", "value2"]'}
        rows={2}
        className={`w-full rounded-md border bg-background px-3 py-2 text-xs font-mono outline-none resize-y ${
          error ? "border-destructive" : "border-input"
        } focus:ring-1 focus:ring-primary`}
      />
      {error && <p className="text-[10px] text-destructive">{error}</p>}
    </div>
  );
}

// ── Checkbox JSON editor ──────────────────────────────────────────────────────

function CheckboxJsonEditor({
  label,
  value,
  onChange,
  options,
}: {
  label: string;
  value: string;
  onChange: (v: string) => void;
  options: { value: string; label: string }[];
}) {
  const selected: string[] = (() => {
    try { return value.trim() ? JSON.parse(value) : []; } catch { return []; }
  })();

  const toggle = (opt: string) => {
    const next = selected.includes(opt)
      ? selected.filter(v => v !== opt)
      : [...selected, opt];
    onChange(next.length ? JSON.stringify(next) : "");
  };

  return (
    <div className="space-y-1">
      <label className="text-xs font-medium text-muted-foreground">{label}</label>
      <div className="flex flex-wrap gap-2">
        {options.map(o => (
          <label key={o.value} className="flex items-center gap-1.5 cursor-pointer select-none text-xs rounded-md border border-border px-2.5 py-1 hover:bg-accent transition-colors"
            style={{ background: selected.includes(o.value) ? "hsl(var(--primary)/0.1)" : undefined, borderColor: selected.includes(o.value) ? "hsl(var(--primary)/0.4)" : undefined }}>
            <input type="checkbox" checked={selected.includes(o.value)} onChange={() => toggle(o.value)} className="rounded h-3 w-3 accent-primary" />
            {o.label}
          </label>
        ))}
      </div>
    </div>
  );
}

const REQUIRED_FIELDS_OPTIONS = [
  { value: "CaseNumber", label: "CaseNumber" },
  { value: "DebtorName", label: "DebtorName" },
  { value: "CourtName", label: "CourtName" },
  { value: "CourtSection", label: "CourtSection" },
  { value: "JudgeSyndic", label: "JudgeSyndic" },
  { value: "ProcedureType", label: "ProcedureType" },
  { value: "LawReference", label: "LawReference" },
  { value: "NoticeDate", label: "NoticeDate" },
  { value: "OpeningDate", label: "OpeningDate" },
  { value: "ClaimsDeadline", label: "ClaimsDeadline" },
  { value: "ContestationsDeadline", label: "ContestationsDeadline" },
  { value: "BpiPublicationNo", label: "BpiPublicationNo" },
  { value: "OpeningDecisionNo", label: "OpeningDecisionNo" },
  { value: "PractitionerName", label: "PractitionerName" },
  { value: "PractitionerRole", label: "PractitionerRole" },
  { value: "DebtorCui", label: "DebtorCui" },
  { value: "DebtorAddress", label: "DebtorAddress" },
  { value: "DebtorTradeRegisterNo", label: "DebtorTradeRegisterNo" },
];

const PARTY_ROLE_OPTIONS = [
  { value: "Debtor", label: "Debtor" },
  { value: "InsolvencyPractitioner", label: "InsolvencyPractitioner" },
  { value: "SecuredCreditor", label: "SecuredCreditor" },
  { value: "UnsecuredCreditor", label: "UnsecuredCreditor" },
  { value: "BudgetaryCreditor", label: "BudgetaryCreditor" },
  { value: "EmployeeCreditor", label: "EmployeeCreditor" },
];

const DOC_TYPE_OPTIONS = [
  { value: "creditorNotificationBpi", label: "CreditorNotificationBpi" },
  { value: "reportArt97", label: "ReportArt97" },
  { value: "mandatoryReport", label: "MandatoryReport" },
  { value: "preliminaryClaimsTable", label: "PreliminaryClaimsTable" },
  { value: "creditorsMeetingMinutes", label: "CreditorsMeetingMinutes" },
  { value: "definitiveClaimsTable", label: "DefinitiveClaimsTable" },
  { value: "finalReportArt167", label: "FinalReportArt167" },
  { value: "creditorNotificationHtml", label: "CreditorNotificationHtml" },
];

// ── Task template editor row ──────────────────────────────────────────────────

interface OutputTaskTemplate {
  title: string;
  description?: string;
  deadlineDays?: number;
  category?: string;
}

function TaskTemplateRow({
  template,
  onChange,
  onRemove,
}: {
  template: OutputTaskTemplate;
  onChange: (t: OutputTaskTemplate) => void;
  onRemove: () => void;
}) {
  const { t } = useTranslation();
  const CATEGORIES = ["Document", "Email", "Filing", "Meeting", "Call", "Review", "Payment", "Report", "Compliance"];
  return (
    <div className="rounded-lg border border-border bg-muted/20 p-3 space-y-2">
      <div className="flex items-center gap-2">
        <input
          value={template.title}
          onChange={e => onChange({ ...template, title: e.target.value })}
          placeholder={t.workflowStages.taskTitlePlaceholder}
          className="flex-1 text-sm rounded border border-input bg-background px-2 py-1 outline-none focus:ring-1 focus:ring-primary"
        />
        <input
          type="number"
          value={template.deadlineDays ?? ""}
          onChange={e => onChange({ ...template, deadlineDays: e.target.value ? parseInt(e.target.value) : undefined })}
          placeholder={t.workflowStages.deadlineDaysPlaceholder}
          title="Deadline in days from stage start"
          className="w-16 text-xs text-center rounded border border-input bg-background px-2 py-1 outline-none focus:ring-1 focus:ring-primary"
        />
        <select
          value={template.category ?? ""}
          onChange={e => onChange({ ...template, category: e.target.value || undefined })}
          className="text-xs rounded border border-input bg-background px-2 py-1 outline-none focus:ring-1 focus:ring-primary"
        >
          <option value="">{t.workflowStages.categoryPlaceholder}</option>
          {CATEGORIES.map(c => <option key={c} value={c}>{c}</option>)}
        </select>
        <button type="button" onClick={onRemove} className="rounded p-1 hover:bg-destructive/10 text-destructive">
          <Trash2 className="h-3.5 w-3.5" />
        </button>
      </div>
      <textarea
        value={template.description ?? ""}
        onChange={e => onChange({ ...template, description: e.target.value || undefined })}
        placeholder={t.workflowStages.descriptionPlaceholder}
        rows={1}
        className="w-full text-xs rounded border border-input bg-background px-2 py-1 outline-none resize-none focus:ring-1 focus:ring-primary"
      />
    </div>
  );
}


// ── Stage detail editor ───────────────────────────────────────────────────────

function StageEditor({
  stage,
  allTemplates,
  onSave,
  onRevertToGlobal,
  onClose,
}: {
  stage: WorkflowStageDetailDto;
  allTemplates: DocumentTemplateDto[];
  onSave: (cmd: UpsertWorkflowStageCommand, asOverride: boolean) => Promise<void>;
  onRevertToGlobal: () => Promise<void>;
  onClose: () => void;
}) {
  const [name, setName] = useState(stage.name);
  const [description, setDescription] = useState(stage.description ?? "");
  const [sortOrder, setSortOrder] = useState(stage.sortOrder);
  const [applicableTypes, setApplicableTypes] = useState(stage.applicableProcedureTypes ?? "");
  const [isActive, setIsActive] = useState(stage.isActive);

  // JSON config fields
  const [requiredFieldsJson, setRequiredFieldsJson] = useState(stage.requiredFieldsJson ?? "");
  const [requiredPartyRolesJson, setRequiredPartyRolesJson] = useState(stage.requiredPartyRolesJson ?? "");
  const [requiredDocTypesJson, setRequiredDocTypesJson] = useState(stage.requiredDocTypesJson ?? "");
  const [requiredTaskTemplates, setRequiredTaskTemplates] = useState<OutputTaskTemplate[]>(() => {
    try { return stage.requiredTaskTemplatesJson ? JSON.parse(stage.requiredTaskTemplatesJson) : []; } catch { return []; }
  });
  const [validationRulesJson, setValidationRulesJson] = useState(stage.validationRulesJson ?? "");
  const [outputDocTypesJson, setOutputDocTypesJson] = useState(stage.outputDocTypesJson ?? "");
  const [allowedTransitionsJson, setAllowedTransitionsJson] = useState(stage.allowedTransitionsJson ?? "");

  // Task templates (outputTasksJson parsed)
  const [outputTaskTemplates, setOutputTaskTemplates] = useState<OutputTaskTemplate[]>(() => {
    try { return stage.outputTasksJson ? JSON.parse(stage.outputTasksJson) : []; } catch { return []; }
  });
  const outputTasksJson = outputTaskTemplates.length > 0 ? JSON.stringify(outputTaskTemplates) : "";

  // Template links
  const [templateLinks, setTemplateLinks] = useState<TemplateLink[]>(
    stage.templates.map((t) => ({
      documentTemplateId: t.documentTemplateId,
      templateName: t.templateName,
      isRequired: t.isRequired,
      sortOrder: t.sortOrder,
      notes: t.notes ?? "",
    }))
  );

  const { t } = useTranslation();
  const [saving, setSaving] = useState(false);
  const [savedOk, setSavedOk] = useState(false);
  const [reverting, setReverting] = useState(false);
  const [showConfigs, setShowConfigs] = useState(false);
  const [showAddTemplate, setShowAddTemplate] = useState(false);

  const isOverride = stage.tenantId !== null;
  const linkedTemplateIds = new Set(templateLinks.map((l) => l.documentTemplateId));
  const availableTemplates = allTemplates.filter((t) => !linkedTemplateIds.has(t.id));

  const buildCommand = (): UpsertWorkflowStageCommand => ({
    stageKey: stage.stageKey,
    name: name.trim(),
    description: description.trim() || null,
    sortOrder,
    applicableProcedureTypes: applicableTypes.trim() || null,
    requiredFieldsJson: requiredFieldsJson.trim() || null,
    requiredPartyRolesJson: requiredPartyRolesJson.trim() || null,
    requiredDocTypesJson: requiredDocTypesJson.trim() || null,
    requiredTaskTemplatesJson: requiredTaskTemplates.length > 0 ? JSON.stringify(requiredTaskTemplates) : null,
    validationRulesJson: validationRulesJson.trim() || null,
    outputDocTypesJson: outputDocTypesJson.trim() || null,
    outputTasksJson: outputTasksJson.trim() || null,
    allowedTransitionsJson: allowedTransitionsJson.trim() || null,
    isActive,
    templates: templateLinks.map<UpsertStageTemplateItem>((l) => ({
      documentTemplateId: l.documentTemplateId,
      isRequired: l.isRequired,
      sortOrder: l.sortOrder,
      notes: l.notes.trim() || null,
    })),
  });

  const handleSave = async (asOverride: boolean) => {
    setSaving(true);
    try {
      await onSave(buildCommand(), asOverride);
      setSavedOk(true);
      setTimeout(() => setSavedOk(false), 3000);
    } finally {
      setSaving(false);
    }
  };

  const handleRevert = async () => {
    if (!window.confirm(t.workflowStages.revertConfirm)) return;
    setReverting(true);
    try {
      await onRevertToGlobal();
    } finally {
      setReverting(false);
    }
  };

  const addTemplate = (t: DocumentTemplateDto) => {
    setTemplateLinks((prev) => [
      ...prev,
      {
        documentTemplateId: t.id,
        templateName: t.name,
        isRequired: false,
        sortOrder: prev.length,
        notes: "",
      },
    ]);
    setShowAddTemplate(false);
  };

  return (
    <div className="flex flex-col h-full">
      {/* Header */}
      <div className="flex items-center gap-3 border-b border-border px-4 py-3 shrink-0">
        <button
          type="button"
          onClick={onClose}
          className="rounded-md p-1 hover:bg-accent text-muted-foreground"
        >
          <ArrowLeft className="h-4 w-4" />
        </button>
        <div className="flex-1 min-w-0">
          <p className="font-semibold text-sm truncate">{stage.name}</p>
          <div className="flex items-center gap-2 mt-0.5">
            <span className="text-xs font-mono text-muted-foreground">{stage.stageKey}</span>
            {isOverride ? (
              <Badge className="text-[10px] gap-1 bg-blue-100 text-blue-700 dark:bg-blue-900/30 dark:text-blue-400 border-0">
                <Building2 className="h-2.5 w-2.5" />
              {t.workflowStages.overrideTenant}
            </Badge>
            ) : (
              <Badge variant="outline" className="text-[10px] gap-1">
                <Globe className="h-2.5 w-2.5" />
                {t.workflowStages.globalBadge}
              </Badge>
            )}
          </div>
        </div>

        <div className="flex items-center gap-2 shrink-0">
          {isOverride && (
            <Button
              variant="outline"
              size="sm"
              onClick={handleRevert}
              disabled={reverting}
            >
              {reverting ? (
                <Loader2 className="h-3.5 w-3.5 animate-spin mr-1" />
              ) : (
                <RotateCcw className="h-3.5 w-3.5 mr-1" />
              )}
              {t.workflowStages.revertToGlobal}
            </Button>
          )}

          {/* Save global */}
          <Button
            size="sm"
            variant="outline"
            onClick={() => handleSave(false)}
            disabled={saving}
            title={t.workflowStages.saveGlobal}
          >
            {saving ? (
              <Loader2 className="h-3.5 w-3.5 animate-spin mr-1" />
            ) : savedOk ? (
              <CheckCircle2 className="h-3.5 w-3.5 mr-1 text-green-500" />
            ) : (
              <Globe className="h-3.5 w-3.5 mr-1" />
            )}
            {t.workflowStages.saveGlobal}
          </Button>

          {/* Save as tenant override */}
          <Button
            size="sm"
            onClick={() => handleSave(true)}
            disabled={saving}
            title="Salvează ca override pentru acest tenant"
          >
            {saving ? (
              <Loader2 className="h-3.5 w-3.5 animate-spin mr-1" />
            ) : savedOk ? (
              <CheckCircle2 className="h-3.5 w-3.5 mr-1 text-green-500" />
            ) : (
              <Save className="h-3.5 w-3.5 mr-1" />
            )}
            {savedOk ? t.workflowStages.savedOk : t.workflowStages.saveOverride}
          </Button>
        </div>
      </div>

      {/* Content */}
      <div className="flex-1 overflow-y-auto p-4 space-y-6">
        {/* Basic fields */}
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div className="md:col-span-2 text-xs text-muted-foreground">
            {t.workflowStages.stageKeyHelp}
          </div>
          <div className="space-y-1">
            <label className="text-xs font-medium text-muted-foreground">{t.workflowStages.stageName}</label>
            <input
              value={name}
              onChange={(e) => setName(e.target.value)}
              className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm outline-none focus:ring-1 focus:ring-primary"
            />
            <p className="text-[11px] text-muted-foreground">{t.workflowStages.stageNameHelp}</p>
          </div>
          <div className="space-y-1">
            <label className="text-xs font-medium text-muted-foreground">{t.workflowStages.sortOrder}</label>
            <input
              type="number"
              value={sortOrder}
              onChange={(e) => setSortOrder(parseInt(e.target.value) || 0)}
              className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm outline-none focus:ring-1 focus:ring-primary"
            />
            <p className="text-[11px] text-muted-foreground">{t.workflowStages.sortOrderHelp}</p>
          </div>
          <div className="md:col-span-2 space-y-2">
            <label className="text-xs font-medium text-muted-foreground">{t.workflowStages.procedureTypes}</label>
            <div className="flex flex-wrap gap-1.5">
              {(["FalimentSimplificat", "Faliment", "Insolventa", "Reorganizare", "ConcordatPreventiv", "MandatAdHoc", "Other"] as const).map((type) => {
                const active = applicableTypes.split(",").map((s) => s.trim()).filter(Boolean).includes(type);
                return (
                  <button
                    key={type}
                    type="button"
                    onClick={() => {
                      const current = applicableTypes.split(",").map((s) => s.trim()).filter(Boolean);
                      const next = current.includes(type) ? current.filter((v) => v !== type) : [...current, type];
                      setApplicableTypes(next.join(","));
                    }}
                    className={`px-2.5 py-1 rounded-full text-[11px] font-medium border transition-colors ${
                      active
                        ? "bg-primary text-primary-foreground border-primary"
                        : "bg-background text-muted-foreground border-border hover:border-primary/60 hover:text-foreground"
                    }`}
                  >
                    {type}
                  </button>
                );
              })}
            </div>
            <p className="text-[11px] text-muted-foreground">{t.workflowStages.procedureTypesHelp}</p>
          </div>
          <div className="md:col-span-2 space-y-1">
            <label className="text-xs font-medium text-muted-foreground">{t.workflowStages.description}</label>
            <textarea
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              rows={2}
              className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm outline-none resize-y focus:ring-1 focus:ring-primary"
            />
            <p className="text-[11px] text-muted-foreground">{t.workflowStages.descriptionHelp}</p>
          </div>
          <div className="md:col-span-2">
            <label className="flex items-center gap-2 text-sm cursor-pointer select-none">
              <input
                type="checkbox"
                checked={isActive}
                onChange={(e) => setIsActive(e.target.checked)}
                className="rounded"
              />
              {t.workflowStages.activeStage}
            </label>
            <p className="text-[11px] text-muted-foreground mt-1">{t.workflowStages.activeStageHelp}</p>
          </div>
        </div>

        {/* Linked templates */}
        <div className="space-y-3">
          <div className="flex items-center justify-between">
            <h3 className="text-sm font-semibold">{t.workflowStages.linkedTemplates}</h3>
            <Button
              variant="outline"
              size="sm"
              onClick={() => setShowAddTemplate(!showAddTemplate)}
            >
              <Plus className="h-3.5 w-3.5 mr-1" />
              {t.workflowStages.addTemplate}
            </Button>
          </div>

          {showAddTemplate && (
            <div className="rounded-lg border border-border bg-card p-3 space-y-2 max-h-48 overflow-y-auto">
              {availableTemplates.length === 0 ? (
                <p className="text-xs text-muted-foreground text-center py-2">
                  {t.workflowStages.allTemplatesLinked}
                </p>
              ) : (
                availableTemplates.map((t) => (
                  <button
                    key={t.id}
                    type="button"
                    onClick={() => addTemplate(t)}
                    className="flex items-center gap-2 w-full text-left rounded-md px-2 py-1.5 text-sm hover:bg-accent transition-colors"
                  >
                    <FileText className="h-3.5 w-3.5 text-muted-foreground" />
                    <span className="flex-1 min-w-0 truncate">{t.name}</span>
                    <Badge variant="outline" className="text-[10px]">
                      {t.templateType}
                    </Badge>
                  </button>
                ))
              )}
            </div>
          )}

          {templateLinks.length === 0 ? (
            <div className="rounded-lg border border-dashed border-border p-6 text-center">
              <FileText className="h-6 w-6 text-muted-foreground mx-auto mb-2" />
              <p className="text-xs text-muted-foreground">
                {t.workflowStages.noTemplatesLinked}
              </p>
            </div>
          ) : (
            <div className="space-y-2">
              {templateLinks.map((link, idx) => (
                <TemplateLinkRow
                  key={link.documentTemplateId}
                  link={link}
                  onChange={(updated) =>
                    setTemplateLinks((prev) => prev.map((l, i) => (i === idx ? updated : l)))
                  }
                  onRemove={() =>
                    setTemplateLinks((prev) => prev.filter((_, i) => i !== idx))
                  }
                />
              ))}
            </div>
          )}
        </div>

        {/* Advanced: JSON config fields */}
        <div className="space-y-3">
          <button
            type="button"
            onClick={() => setShowConfigs(!showConfigs)}
            className="flex items-center gap-2 text-sm font-semibold text-muted-foreground hover:text-foreground transition-colors"
          >
            <ChevronRight
              className={`h-4 w-4 transition-transform ${showConfigs ? "rotate-90" : ""}`}
            />
            {t.workflowStages.advancedConfig}
          </button>

          {showConfigs && (
            <div className="grid grid-cols-1 gap-5 pl-6 border-l-2 border-border">
              <CheckboxJsonEditor
                label={t.workflowStages.requiredFields}
                value={requiredFieldsJson}
                onChange={setRequiredFieldsJson}
                options={REQUIRED_FIELDS_OPTIONS}
              />
              <p className="-mt-3 text-[11px] text-muted-foreground">{t.workflowStages.requiredFieldsHelp}</p>
              <CheckboxJsonEditor
                label={t.workflowStages.requiredPartyRoles}
                value={requiredPartyRolesJson}
                onChange={setRequiredPartyRolesJson}
                options={PARTY_ROLE_OPTIONS}
              />
              <p className="-mt-3 text-[11px] text-muted-foreground">{t.workflowStages.requiredPartyRolesHelp}</p>
              <CheckboxJsonEditor
                label={t.workflowStages.requiredDocTypes}
                value={requiredDocTypesJson}
                onChange={setRequiredDocTypesJson}
                options={DOC_TYPE_OPTIONS}
              />
              <p className="-mt-3 text-[11px] text-muted-foreground">{t.workflowStages.requiredDocTypesHelp}</p>
              <JsonFieldEditor
                label={t.workflowStages.validationRules}
                value={validationRulesJson}
                onChange={setValidationRulesJson}
                placeholder='{"minCreditors": 1}'
              />
              <p className="-mt-3 text-[11px] text-muted-foreground">{t.workflowStages.validationRulesHelp}</p>
              <JsonFieldEditor
                label={t.workflowStages.outputDocTypes}
                value={outputDocTypesJson}
                onChange={setOutputDocTypesJson}
                placeholder='["PreliminaryClaimsTable"]'
              />
              <p className="-mt-3 text-[11px] text-muted-foreground">{t.workflowStages.outputDocTypesHelp}</p>
              <JsonFieldEditor
                label={t.workflowStages.allowedTransitions}
                value={allowedTransitionsJson}
                onChange={setAllowedTransitionsJson}
                placeholder='["claims_collection", "creditors_meeting"]'
              />
              <p className="-mt-3 text-[11px] text-muted-foreground">{t.workflowStages.allowedTransitionsHelp}</p>
            </div>
          )}
        </div>

        {/* Required task templates */}
        <div className="space-y-3">
          <div className="flex items-center justify-between">
            <h3 className="text-sm font-semibold">{t.workflowStages.requiredTaskTemplates}</h3>
            <Button variant="outline" size="sm" onClick={() => setRequiredTaskTemplates(prev => [...prev, { title: "", deadlineDays: 7, category: "Document" }])}>
              <Plus className="h-3.5 w-3.5 mr-1" /> {t.workflowStages.addTask}
            </Button>
          </div>
          <p className="text-[11px] text-muted-foreground">{t.workflowStages.requiredTaskTemplatesHelp}</p>
          {requiredTaskTemplates.length === 0 ? (
            <div className="rounded-lg border border-dashed border-border p-6 text-center">
              <p className="text-xs text-muted-foreground">No required task templates. Add tasks that must be completed for this stage.</p>
            </div>
          ) : (
            <div className="space-y-2">
              <div className="grid grid-cols-3 gap-2 px-3 text-[10px] font-semibold text-muted-foreground uppercase tracking-wide">
                <span className="col-span-1">{t.workflowStages.taskTitlePlaceholder}</span>
                <span className="text-center">{t.workflowStages.deadlineDaysPlaceholder}</span>
                <span className="text-center">{t.workflowStages.categoryPlaceholder}</span>
              </div>
              {requiredTaskTemplates.map((tt, idx) => (
                <TaskTemplateRow
                  key={idx}
                  template={tt}
                  onChange={updated => setRequiredTaskTemplates(prev => prev.map((t, i) => i === idx ? updated : t))}
                  onRemove={() => setRequiredTaskTemplates(prev => prev.filter((_, i) => i !== idx))}
                />
              ))}
            </div>
          )}
        </div>

        {/* Task templates (outputTasksJson) */}
        <div className="space-y-3">
          <div className="flex items-center justify-between">
            <h3 className="text-sm font-semibold">{t.workflowStages.defaultTasks}</h3>
            <Button variant="outline" size="sm" onClick={() => setOutputTaskTemplates(prev => [...prev, { title: "", deadlineDays: 7, category: "Document" }])}>
              <Plus className="h-3.5 w-3.5 mr-1" /> {t.workflowStages.addTask}
            </Button>
          </div>
          <p className="text-[11px] text-muted-foreground">{t.workflowStages.defaultTasksHelp}</p>
          {outputTaskTemplates.length === 0 ? (
            <div className="rounded-lg border border-dashed border-border p-6 text-center">
              <p className="text-xs text-muted-foreground">{t.workflowStages.noDefaultTasks}</p>
            </div>
          ) : (
            <div className="space-y-2">
              <div className="grid grid-cols-3 gap-2 px-3 text-[10px] font-semibold text-muted-foreground uppercase tracking-wide">
                <span className="col-span-1">{t.workflowStages.taskTitlePlaceholder}</span>
                <span className="text-center">{t.workflowStages.deadlineDaysPlaceholder}</span>
                <span className="text-center">{t.workflowStages.categoryPlaceholder}</span>
              </div>
              {outputTaskTemplates.map((tt, idx) => (
                <TaskTemplateRow
                  key={idx}
                  template={tt}
                  onChange={updated => setOutputTaskTemplates(prev => prev.map((t, i) => i === idx ? updated : t))}
                  onRemove={() => setOutputTaskTemplates(prev => prev.filter((_, i) => i !== idx))}
                />
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

// ── New stage form ────────────────────────────────────────────────────────────

function NewStageForm({
  onCreated,
  onCancel,
}: {
  onCreated: () => void;
  onCancel: () => void;
}) {
  const [stageKey, setStageKey] = useState("");
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [sortOrder, setSortOrder] = useState(99);
  const { t } = useTranslation();
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");

  const handleCreate = async () => {
    if (!stageKey.trim() || !name.trim()) {
      setError(t.workflowStages.keyAndNameRequired);
      return;
    }
    setSaving(true);
    setError("");
    try {
      await workflowStagesApi.upsertGlobal({
        stageKey: stageKey.trim().toLowerCase().replace(/\s+/g, "_"),
        name: name.trim(),
        description: description.trim() || undefined,
        sortOrder,
        isActive: true,
      });
      onCreated();
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setError(msg ?? t.workflowStages.keyAndNameRequired);
      setSaving(false);
    }
  };

  return (
    <div className="rounded-lg border border-border bg-card p-4 space-y-3">
      <p className="text-sm font-semibold">{t.workflowStages.newStageFormTitle}</p>
      <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
        <input
          autoFocus
          value={stageKey}
          onChange={(e) => setStageKey(e.target.value)}
          placeholder={t.workflowStages.stageKeyPlaceholder}
          className="rounded-md border border-input bg-background px-3 py-2 text-sm font-mono outline-none focus:ring-1 focus:ring-primary"
        />
        <input
          value={name}
          onChange={(e) => setName(e.target.value)}
          placeholder={t.workflowStages.stageNamePlaceholder}
          className="rounded-md border border-input bg-background px-3 py-2 text-sm outline-none focus:ring-1 focus:ring-primary"
        />
        <input
          value={description}
          onChange={(e) => setDescription(e.target.value)}
          placeholder={t.workflowStages.descPlaceholder}
          className="rounded-md border border-input bg-background px-3 py-2 text-sm outline-none focus:ring-1 focus:ring-primary"
        />
        <input
          type="number"
          value={sortOrder}
          onChange={(e) => setSortOrder(parseInt(e.target.value) || 0)}
          placeholder={t.workflowStages.sortOrderPlaceholder}
          className="rounded-md border border-input bg-background px-3 py-2 text-sm outline-none focus:ring-1 focus:ring-primary"
        />
      </div>
      {error && <p className="text-xs text-destructive flex items-center gap-1"><AlertTriangle className="h-3 w-3" />{error}</p>}
      <div className="flex gap-2">
        <Button size="sm" onClick={handleCreate} disabled={saving}>
          {saving && <Loader2 className="h-3.5 w-3.5 animate-spin mr-1" />}
          {t.workflowStages.createStage}
        </Button>
        <Button size="sm" variant="ghost" onClick={onCancel}>{t.common.cancel}</Button>
      </div>
    </div>
  );
}

// ── Main page ─────────────────────────────────────────────────────────────────

export default function WorkflowStagesPage() {
  const { t } = useTranslation();
  const [stages, setStages] = useState<WorkflowStageDto[]>([]);
  const [allTemplates, setAllTemplates] = useState<DocumentTemplateDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [editingStage, setEditingStage] = useState<WorkflowStageDetailDto | null>(null);
  const [loadingDetail, setLoadingDetail] = useState(false);
  const [showNewForm, setShowNewForm] = useState(false);

  const loadStages = useCallback(async () => {
    setLoading(true);
    try {
      const [stagesRes, templatesRes] = await Promise.all([
        workflowStagesApi.getEffective(),
        documentTemplatesApi.getAll(),
      ]);
      setStages(stagesRes.data);
      setAllTemplates(templatesRes.data);
    } catch (err) {
      console.error("Failed to load workflow stages:", err);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadStages();
  }, [loadStages]);

  const openDetail = async (id: string) => {
    setLoadingDetail(true);
    try {
      const r = await workflowStagesApi.getById(id);
      setEditingStage(r.data);
    } catch (err) {
      console.error(err);
    } finally {
      setLoadingDetail(false);
    }
  };

  const handleSave = async (cmd: UpsertWorkflowStageCommand, asOverride: boolean) => {
    if (asOverride) {
      await workflowStagesApi.upsertOverride(cmd);
    } else {
      await workflowStagesApi.upsertGlobal(cmd);
    }
    await loadStages();
  };

  const handleRevertToGlobal = async () => {
    if (!editingStage) return;
    await workflowStagesApi.deleteOverride(editingStage.stageKey);
    setEditingStage(null);
    await loadStages();
  };

  const handleCreated = async () => {
    setShowNewForm(false);
    await loadStages();
  };

  // ── Render: loading detail ───────────────────────────────────────────────

  if (loadingDetail) {
    return (
      <div className="flex h-96 items-center justify-center">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  // ── Render: detail editor (full page) ────────────────────────────────────

  if (editingStage) {
    return (
      <div className="flex flex-col h-[calc(100vh-8rem)]">
        <StageEditor
          stage={editingStage}
          allTemplates={allTemplates}
          onSave={handleSave}
          onRevertToGlobal={handleRevertToGlobal}
          onClose={() => setEditingStage(null)}
        />
      </div>
    );
  }

  // ── Render: list view ─────────────────────────────────────────────────────

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-bold">{t.workflowStages.pageTitle}</h1>
          <p className="text-sm text-muted-foreground mt-1">{t.workflowStages.pageDesc}</p>
        </div>
        <Button onClick={() => setShowNewForm(true)}>
          <Plus className="h-4 w-4 mr-1.5" />
          {t.workflowStages.newStage}
        </Button>
      </div>

      {/* New stage form */}
      {showNewForm && (
        <NewStageForm
          onCreated={handleCreated}
          onCancel={() => setShowNewForm(false)}
        />
      )}

      {/* Loading */}
      {loading && (
        <div className="flex items-center gap-2 text-muted-foreground text-sm">
          <Loader2 className="h-4 w-4 animate-spin" />
          {t.workflowStages.loading}
        </div>
      )}

      {/* Stage list */}
      {!loading && stages.length === 0 ? (
        <div className="rounded-lg border border-dashed border-border p-8 text-center">
          <Layers className="h-8 w-8 text-muted-foreground mx-auto mb-2" />
          <p className="text-sm text-muted-foreground">
            {t.workflowStages.noStages}
          </p>
        </div>
      ) : (
        <div className="space-y-2">
          {stages.map((s) => (
            <StageCard key={s.id} stage={s} onClick={() => openDetail(s.id)} />
          ))}
        </div>
      )}

      {/* Summary */}
      {!loading && stages.length > 0 && (
        <div className="flex items-center gap-4 text-xs text-muted-foreground border-t border-border pt-4">
          <span>{stages.length} {t.workflowStages.stagesCount}</span>
          <span>{stages.filter((s) => s.tenantId !== null).length} {t.workflowStages.overridesCount}</span>
          <span>{stages.filter((s) => !s.isActive).length} {t.workflowStages.inactiveCount}</span>
        </div>
      )}
    </div>
  );
}
