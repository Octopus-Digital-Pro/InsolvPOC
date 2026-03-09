import { useState, useEffect } from "react";
import { useAuth } from "@/contexts/AuthContext";
import { useTranslation } from "@/contexts/LanguageContext";
import { tenantAiConfigApi } from "@/services/api/caseAiApi";
import type { TenantAiConfigDto, UpdateTenantAiConfigRequest } from "@/services/api/caseAiApi";
import { tenantsApi } from "@/services/api";
import { Button } from "@/components/ui/button";
import {
  Brain,
  Loader2,
  Save,
  CheckCircle,
  AlertCircle,
  BarChart3,
  MessageSquare,
  FileText,
  ToggleLeft,
  ToggleRight,
  Key,
  Eye,
  EyeOff,
  Server,
  Cpu,
} from "lucide-react";

interface TenantOption {
  id: string;
  name: string;
}

export default function TenantAiConfigPage() {
  const { isGlobalAdmin } = useAuth();
  const { t } = useTranslation();

  const [tenants, setTenants] = useState<TenantOption[]>([]);
  const [tenantsLoading, setTenantsLoading] = useState(true);
  const [selectedTenantId, setSelectedTenantId] = useState<string>("");

  const [config, setConfig] = useState<TenantAiConfigDto | null>(null);
  const [configLoading, setConfigLoading] = useState(false);

  const [saving, setSaving] = useState(false);
  const [savedOk, setSavedOk] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);

  // Key override (never pre-filled from server)
  const [apiKey, setApiKey] = useState("");
  const [showKey, setShowKey] = useState(false);
  const [clearKey, setClearKey] = useState(false);

  // ── Redirect non-GlobalAdmin ───────────────────────────────────────────────
  if (!isGlobalAdmin) {
    return (
      <div className="flex flex-col items-center justify-center h-56 gap-4 text-center p-8">
        <AlertCircle className="h-10 w-10 text-muted-foreground" />
        <p className="text-sm text-muted-foreground">Access restricted to Global Administrators.</p>
      </div>
    );
  }

  // ── Load tenant list ───────────────────────────────────────────────────────
  // eslint-disable-next-line react-hooks/rules-of-hooks
  useEffect(() => {
    tenantsApi
      .getAll()
      .then((r) => {
        const items = r.data.map(x => ({ id: x.id, name: x.name }));
        setTenants(items);
        if (items.length > 0) setSelectedTenantId(items[0].id);
      })
      .catch(console.error)
      .finally(() => setTenantsLoading(false));
  }, []);

  // ── Load config when tenant selection changes ──────────────────────────────
  // eslint-disable-next-line react-hooks/rules-of-hooks
  useEffect(() => {
    if (!selectedTenantId) return;
    setConfigLoading(true);
    setConfig(null);
    setSavedOk(false);
    setSaveError(null);
    setApiKey("");
    setShowKey(false);
    setClearKey(false);
    tenantAiConfigApi
      .getForTenant(selectedTenantId)
      .then(r => setConfig(r.data))
      .catch(console.error)
      .finally(() => setConfigLoading(false));
  }, [selectedTenantId]);

  function setField<K extends keyof UpdateTenantAiConfigRequest>(
    key: K,
    value: UpdateTenantAiConfigRequest[K]
  ) {
    setConfig((prev) =>
      prev ? ({ ...prev, [key]: value } as TenantAiConfigDto) : prev
    );
  }

  async function handleSave() {
    if (!config || !selectedTenantId) return;
    setSaving(true);
    setSavedOk(false);
    setSaveError(null);
    try {
      const req: UpdateTenantAiConfigRequest = {
        aiEnabled: config.aiEnabled,
        monthlyTokenLimit: config.monthlyTokenLimit,
        summaryEnabled: config.summaryEnabled,
        chatEnabled: config.chatEnabled,
        summaryActivityDays: config.summaryActivityDays,
        notes: config.notes,
        // Key: clearKey = clear it; apiKey != "" = new key; else = no change
        apiKey: clearKey ? "" : apiKey !== "" ? apiKey : undefined,
        provider: config.provider,
        apiEndpoint: config.apiEndpoint,
        modelName: config.modelName,
      };
      const updated = await tenantAiConfigApi.update(selectedTenantId, req);
      setConfig(updated.data);
      setApiKey("");
      setClearKey(false); // clear after save
      setSavedOk(true);
      setTimeout(() => setSavedOk(false), 3000);
    } catch {
      setSaveError(t.ai.saveError);
    } finally {
      setSaving(false);
    }
  }

  const usagePct =
    config && config.monthlyTokenLimit > 0
      ? Math.min(
          100,
          Math.round((config.currentMonthTokensUsed / config.monthlyTokenLimit) * 100)
        )
      : 0;

  return (
    <div className="max-w-2xl mx-auto space-y-6">
      {/* Page header */}
      <div className="flex items-center gap-3">
        <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-primary/10">
          <Brain className="h-5 w-5 text-primary" />
        </div>
        <div>
          <h1 className="text-lg font-bold text-foreground">{t.ai.tenantConfigTitle}</h1>
          <p className="text-sm text-muted-foreground">{t.ai.tenantConfigDesc}</p>
        </div>
      </div>

      {/* Tenant selector */}
      <div className="rounded-xl border border-border bg-card p-5 space-y-3">
        <label className="text-sm font-medium text-foreground">{t.ai.selectTenant}</label>
        {tenantsLoading ? (
          <div className="flex items-center gap-2 text-sm">
            <Loader2 className="h-4 w-4 animate-spin" />
            {t.common.loading}
          </div>
        ) : (
          <select
            value={selectedTenantId}
            onChange={(e) => setSelectedTenantId(e.target.value)}
            className="w-full rounded-lg border border-input bg-background px-3 py-2 text-sm text-foreground focus:outline-none focus:ring-2 focus:ring-ring"
          >
            {tenants.map((tn) => (
              <option key={tn.id} value={tn.id}>
                {tn.name}
              </option>
            ))}
          </select>
        )}
      </div>

      {/* Config form */}
      {configLoading ? (
        <div className="flex items-center justify-center h-40 gap-2 text-muted-foreground">
          <Loader2 className="h-5 w-5 animate-spin" />
          <span className="text-sm">{t.common.loading}</span>
        </div>
      ) : config ? (
        <>
          {/* Current usage */}
          {config.monthlyTokenLimit > 0 && (
            <div className="rounded-xl border border-border bg-card p-5 space-y-2">
              <div className="flex items-center gap-2 text-sm font-medium text-foreground">
                <BarChart3 className="h-4 w-4 text-muted-foreground" />
                {t.ai.currentUsage}
              </div>
              <div className="h-2 w-full rounded-full bg-muted overflow-hidden">
                <div
                  className={`h-full rounded-full transition-all ${
                    usagePct >= 90
                      ? "bg-destructive"
                      : usagePct >= 70
                      ? "bg-yellow-500"
                      : "bg-primary"
                  }`}
                  style={{ width: `${usagePct}%` }}
                />
              </div>
              <div className="flex justify-between text-xs text-muted-foreground">
                <span>{config.currentMonthTokensUsed.toLocaleString()} tokens used</span>
                <span>
                  {usagePct}% of {config.monthlyTokenLimit.toLocaleString()}
                </span>
              </div>
            </div>
          )}

          {/* Main toggles */}
          <div className="rounded-xl border border-border bg-card divide-y divide-border">
            {/* Enable AI */}
            <ToggleRow
              icon={<Brain className="h-4 w-4 text-primary" />}
              label={t.ai.enableAi}
              description={t.ai.enableAiDesc}
              value={config.aiEnabled}
              onChange={(v) => setField("aiEnabled", v)}
            />
            {/* Summary */}
            <ToggleRow
              icon={<FileText className="h-4 w-4 text-blue-500" />}
              label={t.ai.enableSummary}
              description={t.ai.enableSummaryDesc}
              value={config.summaryEnabled}
              onChange={(v) => setField("summaryEnabled", v)}
              disabled={!config.aiEnabled}
            />
            {/* Chat */}
            <ToggleRow
              icon={<MessageSquare className="h-4 w-4 text-green-500" />}
              label={t.ai.enableChat}
              description={t.ai.enableChatDesc}
              value={config.chatEnabled}
              onChange={(v) => setField("chatEnabled", v)}
              disabled={!config.aiEnabled}
            />
          </div>

          {/* Numeric fields */}
          <div className="rounded-xl border border-border bg-card p-5 space-y-5">
            {/* Monthly token limit */}
            <div className="space-y-1.5">
              <label className="text-sm font-medium text-foreground">
                {t.ai.monthlyTokenLimit}
              </label>
              <input
                type="number"
                min={0}
                step={10000}
                value={config.monthlyTokenLimit}
                onChange={(e) =>
                  setField("monthlyTokenLimit", parseInt(e.target.value, 10) || 0)
                }
                className="w-full rounded-lg border border-input bg-background px-3 py-2 text-sm text-foreground focus:outline-none focus:ring-2 focus:ring-ring"
              />
              <p className="text-xs text-muted-foreground">{t.ai.monthlyTokenLimitDesc}</p>
            </div>

            {/* Activity days */}
            <div className="space-y-1.5">
              <label className="text-sm font-medium text-foreground">
                {t.ai.activityDays}
              </label>
              <input
                type="number"
                min={7}
                max={90}
                value={config.summaryActivityDays}
                onChange={(e) =>
                  setField(
                    "summaryActivityDays",
                    Math.min(90, Math.max(7, parseInt(e.target.value, 10) || 30))
                  )
                }
                className="w-full rounded-lg border border-input bg-background px-3 py-2 text-sm text-foreground focus:outline-none focus:ring-2 focus:ring-ring"
              />
              <p className="text-xs text-muted-foreground">{t.ai.activityDaysDesc}</p>
            </div>

            {/* Notes */}
            <div className="space-y-1.5">
              <label className="text-sm font-medium text-foreground">{t.ai.notes}</label>
              <textarea
                rows={3}
                value={config.notes ?? ""}
                onChange={(e) => setField("notes", e.target.value || null)}
                className="w-full resize-none rounded-lg border border-input bg-background px-3 py-2 text-sm text-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-ring"
              />
            </div>
          </div>

          {/* Provider & API Key Override */}
          <div className="rounded-xl border border-border bg-card p-5 space-y-5">
            <div className="flex items-center gap-2 mb-1">
              <Key className="h-4 w-4 text-muted-foreground" />
              <p className="text-sm font-semibold text-foreground">API Key Override</p>
              {config.hasApiKey && (
                <span className="ml-auto text-[10px] bg-green-500/10 text-green-600 border border-green-500/20 rounded-full px-2 py-0.5">Custom key active</span>
              )}
            </div>
            <p className="text-xs text-muted-foreground -mt-2">
              Tenant-specific key overrides the system key. Leave blank to keep existing setting.
            </p>

            {/* Provider */}
            <div className="space-y-1.5">
              <label className="text-sm font-medium text-foreground flex items-center gap-1.5">
                <Server className="h-3.5 w-3.5 text-muted-foreground" /> Provider
              </label>
              <select
                value={config.provider ?? ""}
                onChange={(e) => setConfig(prev => prev ? { ...prev, provider: e.target.value || null } : prev)}
                className="w-full rounded-lg border border-input bg-background px-3 py-2 text-sm text-foreground focus:outline-none focus:ring-2 focus:ring-ring"
              >
                <option value="">— Use system default —</option>
                <option value="OpenAI">OpenAI</option>
                <option value="AzureOpenAI">Azure OpenAI</option>
                <option value="Anthropic">Anthropic (Claude)</option>
                <option value="Google">Google Gemini</option>
                <option value="OpenRouter">OpenRouter (300+ models)</option>
                <option value="Custom">Custom / Self-hosted</option>
              </select>
            </div>

            {/* API Key */}
            <div className="space-y-1.5">
              <label className="text-sm font-medium text-foreground flex items-center gap-1.5">
                <Key className="h-3.5 w-3.5 text-muted-foreground" /> API Key
              </label>
              <div className="relative">
                <input
                  type={showKey ? "text" : "password"}
                  placeholder={config.hasApiKey ? "●●●●●●●●●●●● (leave blank to keep)" : "Enter API key to set…"}
                  value={apiKey}
                  onChange={(e) => setApiKey(e.target.value)}
                  className="w-full rounded-lg border border-input bg-background px-3 py-2 pr-10 text-sm text-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-ring"
                />
                <button
                  type="button"
                  onClick={() => setShowKey(v => !v)}
                  className="absolute right-3 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground"
                >
                  {showKey ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                </button>
              </div>
              {config.hasApiKey && (
                <button
                  type="button"
                  onClick={() => { setClearKey(true); setApiKey(""); setConfig(prev => prev ? { ...prev, hasApiKey: false } : prev); }}
                  className="text-xs text-destructive hover:underline"
                >
                  Clear existing key
                </button>
              )}
            </div>

            {/* Model */}
            <div className="space-y-1.5">
              <label className="text-sm font-medium text-foreground flex items-center gap-1.5">
                <Cpu className="h-3.5 w-3.5 text-muted-foreground" /> Model
              </label>
              <input
                type="text"
                placeholder="e.g. gpt-4o, claude-3-5-sonnet-20241022"
                value={config.modelName ?? ""}
                onChange={(e) => setConfig(prev => prev ? { ...prev, modelName: e.target.value || null } : prev)}
                className="w-full rounded-lg border border-input bg-background px-3 py-2 text-sm text-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-ring"
              />
            </div>

            {/* Endpoint */}
            {(config.provider === "AzureOpenAI" || config.provider === "OpenRouter" || config.provider === "Custom") && (
              <div className="space-y-1.5">
                <label className="text-sm font-medium text-foreground">API Endpoint</label>
                <input
                  type="text"
                  placeholder={
                    config.provider === "AzureOpenAI"
                      ? "https://your-resource.openai.azure.com/…"
                      : config.provider === "OpenRouter"
                      ? "https://openrouter.ai/api/v1"
                      : "https://your-endpoint/v1"
                  }
                  value={config.apiEndpoint ?? ""}
                  onChange={(e) => setConfig(prev => prev ? { ...prev, apiEndpoint: e.target.value || null } : prev)}
                  className="w-full rounded-lg border border-input bg-background px-3 py-2 text-sm text-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-ring"
                />
              </div>
            )}
          </div>

          {/* Save bar */}
          <div className="flex items-center justify-between">
            <div>
              {savedOk && (
                <div className="flex items-center gap-2 text-sm text-green-600">
                  <CheckCircle className="h-4 w-4" />
                  {t.ai.saved}
                </div>
              )}
              {saveError && (
                <div className="flex items-center gap-2 text-sm text-destructive">
                  <AlertCircle className="h-4 w-4" />
                  {saveError}
                </div>
              )}
            </div>
            <Button onClick={handleSave} disabled={saving}>
              {saving ? (
                <>
                  <Loader2 className="h-4 w-4 mr-2 animate-spin" />
                  {t.common.save}...
                </>
              ) : (
                <>
                  <Save className="h-4 w-4 mr-2" />
                  {t.ai.saveConfig}
                </>
              )}
            </Button>
          </div>
        </>
      ) : null}
    </div>
  );
}

// ── Toggle row ─────────────────────────────────────────────────────────────────
function ToggleRow({
  icon,
  label,
  description,
  value,
  onChange,
  disabled = false,
}: {
  icon: React.ReactNode;
  label: string;
  description: string;
  value: boolean;
  onChange: (v: boolean) => void;
  disabled?: boolean;
}) {
  return (
    <div
      className={`flex items-center justify-between px-5 py-4 ${
        disabled ? "opacity-50" : ""
      }`}
    >
      <div className="flex items-start gap-3">
        <div className="mt-0.5">{icon}</div>
        <div>
          <p className="text-sm font-medium text-foreground">{label}</p>
          <p className="text-xs text-muted-foreground">{description}</p>
        </div>
      </div>
      <button
        type="button"
        onClick={() => !disabled && onChange(!value)}
        disabled={disabled}
        className="shrink-0 focus:outline-none"
        aria-pressed={value}
      >
        {value ? (
          <ToggleRight className="h-7 w-7 text-primary" />
        ) : (
          <ToggleLeft className="h-7 w-7 text-muted-foreground" />
        )}
      </button>
    </div>
  );
}
