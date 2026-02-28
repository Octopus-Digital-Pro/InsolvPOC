import { useState, useEffect } from "react";
import { useAuth } from "@/contexts/AuthContext";
import { useTranslation } from "@/contexts/LanguageContext";
import { tenantAiConfigApi } from "@/services/api/caseAiApi";
import type { TenantAiConfigDto, UpdateTenantAiKeyRequest } from "@/services/api/caseAiApi";
import { Button } from "@/components/ui/button";
import {
  Brain,
  Loader2,
  Save,
  CheckCircle2,
  AlertCircle,
  Key,
  Eye,
  EyeOff,
  Server,
  Cpu,
  Info,
  Trash2,
} from "lucide-react";

type AiProvider = "OpenAI" | "AzureOpenAI" | "Anthropic" | "Google" | "Custom";

const PROVIDERS: { value: AiProvider; label: string; description: string }[] = [
  { value: "OpenAI", label: "OpenAI", description: "GPT-4o, GPT-4 Turbo, etc." },
  { value: "AzureOpenAI", label: "Azure OpenAI", description: "OpenAI models hosted on Azure" },
  { value: "Anthropic", label: "Anthropic", description: "Claude 3.5, Claude 3 Opus, etc." },
  { value: "Google", label: "Google Gemini", description: "Gemini 1.5 Pro, Flash, etc." },
  { value: "Custom", label: "Custom / Self-hosted", description: "OpenAI-compatible endpoint" },
];

const DEFAULT_MODELS: Record<AiProvider, string> = {
  OpenAI: "gpt-4o",
  AzureOpenAI: "gpt-4o",
  Anthropic: "claude-3-5-sonnet-20241022",
  Google: "gemini-1.5-pro",
  Custom: "",
};

function SettingField({
  label,
  description,
  children,
}: {
  label: string;
  description?: string;
  children: React.ReactNode;
}) {
  return (
    <div className="flex flex-col sm:flex-row sm:items-start gap-2 sm:gap-6 py-4 border-b border-border last:border-0">
      <div className="sm:w-52 shrink-0">
        <p className="text-sm font-medium text-foreground">{label}</p>
        {description && (
          <p className="text-xs text-muted-foreground mt-0.5">{description}</p>
        )}
      </div>
      <div className="flex-1">{children}</div>
    </div>
  );
}

export default function MyAiSettingsPage() {
  const { isTenantAdmin } = useAuth();
  const { t } = useTranslation();

  const [config, setConfig] = useState<TenantAiConfigDto | null>(null);
  const [loading, setLoading] = useState(true);

  const [provider, setProvider] = useState<AiProvider | "">("");
  const [apiKey, setApiKey] = useState("");
  const [showKey, setShowKey] = useState(false);
  const [apiEndpoint, setApiEndpoint] = useState("");
  const [modelName, setModelName] = useState("");
  const [clearKey, setClearKey] = useState(false);

  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);

  if (!isTenantAdmin) {
    return (
      <div className="flex flex-col items-center justify-center h-56 gap-4 text-center p-8">
        <AlertCircle className="h-10 w-10 text-muted-foreground" />
        <p className="text-sm text-muted-foreground">Access restricted to Tenant Administrators.</p>
      </div>
    );
  }

  // eslint-disable-next-line react-hooks/rules-of-hooks
  useEffect(() => {
    tenantAiConfigApi
      .getOwn()
      .then((r) => {
        const d = r.data;
        setConfig(d);
        setProvider((d.provider as AiProvider | null) ?? "");
        setModelName(d.modelName ?? "");
        setApiEndpoint(d.apiEndpoint ?? "");
      })
      .catch(console.error)
      .finally(() => setLoading(false));
  }, []);

  function handleProviderChange(p: AiProvider | "") {
    setProvider(p);
    if (p && !modelName) setModelName(DEFAULT_MODELS[p as AiProvider] ?? "");
  }

  async function handleSave() {
    setSaving(true);
    setSaved(false);
    setSaveError(null);
    try {
      const req: UpdateTenantAiKeyRequest = {
        provider: provider || null,
        apiKey: clearKey ? "" : apiKey !== "" ? apiKey : undefined,
        apiEndpoint: apiEndpoint.trim() || null,
        modelName: modelName.trim() || null,
      };
      const updated = await tenantAiConfigApi.updateOwnKey(req);
      setConfig(updated.data);
      setApiKey("");
      setClearKey(false);
      setSaved(true);
      setTimeout(() => setSaved(false), 3000);
    } catch {
      setSaveError(t.ai.saveError);
    } finally {
      setSaving(false);
    }
  }

  if (loading) {
    return (
      <div className="flex items-center justify-center h-40 gap-2 text-muted-foreground">
        <Loader2 className="h-5 w-5 animate-spin" />
        <span className="text-sm">{t.common.loading}</span>
      </div>
    );
  }

  return (
    <div className="max-w-2xl mx-auto space-y-6">
      {/* Header */}
      <div className="flex items-center gap-3">
        <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-primary/10">
          <Brain className="h-5 w-5 text-primary" />
        </div>
        <div>
          <h1 className="text-lg font-bold text-foreground">My AI Settings</h1>
          <p className="text-sm text-muted-foreground">
            Configure a custom AI provider key for your organisation. Leave blank to use the system default.
          </p>
        </div>
      </div>

      {/* AI status banner */}
      {config && !config.aiEnabled && (
        <div className="flex items-start gap-3 rounded-xl border border-yellow-500/30 bg-yellow-500/5 p-4">
          <Info className="h-4 w-4 text-yellow-600 shrink-0 mt-0.5" />
          <p className="text-sm text-yellow-700 dark:text-yellow-400">
            AI features are currently disabled for your organisation by a Global Administrator. You can configure your key now so it's ready when AI is enabled.
          </p>
        </div>
      )}

      {/* Current key status */}
      <div className="rounded-xl border border-border bg-card p-5 space-y-0 divide-y divide-border">
        {/* Provider */}
        <SettingField
          label="Provider"
          description="Which AI service should handle your requests?"
        >
          <select
            value={provider}
            onChange={(e) => handleProviderChange(e.target.value as AiProvider | "")}
            className="w-full rounded-lg border border-input bg-background px-3 py-2 text-sm text-foreground focus:outline-none focus:ring-2 focus:ring-ring"
          >
            <option value="">— Use system default —</option>
            {PROVIDERS.map((p) => (
              <option key={p.value} value={p.value}>
                {p.label} — {p.description}
              </option>
            ))}
          </select>
        </SettingField>

        {/* API Key */}
        <SettingField
          label="API Key"
          description={
            config?.hasApiKey && !clearKey
              ? "A custom key is active. Enter a new value to replace it."
              : "Enter your API key from your provider's dashboard."
          }
        >
          <div className="space-y-2">
            {config?.hasApiKey && !clearKey && (
              <div className="flex items-center gap-2 rounded-lg bg-green-500/10 border border-green-500/20 px-3 py-2">
                <Key className="h-3.5 w-3.5 text-green-600" />
                <span className="text-xs text-green-700 dark:text-green-400 flex-1">Custom key is active</span>
                <button
                  type="button"
                  onClick={() => { setClearKey(true); setApiKey(""); }}
                  className="flex items-center gap-1 text-xs text-destructive hover:underline"
                >
                  <Trash2 className="h-3 w-3" /> Remove
                </button>
              </div>
            )}
            {clearKey && (
              <div className="flex items-center gap-2 rounded-lg bg-destructive/10 border border-destructive/20 px-3 py-2">
                <AlertCircle className="h-3.5 w-3.5 text-destructive" />
                <span className="text-xs text-destructive flex-1">Key will be cleared on save</span>
                <button
                  type="button"
                  onClick={() => setClearKey(false)}
                  className="text-xs underline text-muted-foreground"
                >
                  Undo
                </button>
              </div>
            )}
            {!clearKey && (
              <div className="relative">
                <input
                  type={showKey ? "text" : "password"}
                  placeholder={config?.hasApiKey ? "Enter new key to replace…" : "sk-… or your key"}
                  value={apiKey}
                  onChange={(e) => setApiKey(e.target.value)}
                  className="w-full rounded-lg border border-input bg-background px-3 py-2 pr-10 text-sm text-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-ring"
                />
                <button
                  type="button"
                  onClick={() => setShowKey((v) => !v)}
                  className="absolute right-3 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground"
                  tabIndex={-1}
                >
                  {showKey ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                </button>
              </div>
            )}
          </div>
        </SettingField>

        {/* Model */}
        <SettingField
          label="Model"
          description="The model name to use. Defaults shown for each provider."
        >
          <div className="space-y-1.5">
            <div className="relative">
              <Cpu className="absolute left-3 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-muted-foreground" />
              <input
                type="text"
                placeholder={
                  provider ? DEFAULT_MODELS[provider as AiProvider] || "model name…" : "e.g. gpt-4o"
                }
                value={modelName}
                onChange={(e) => setModelName(e.target.value)}
                className="w-full rounded-lg border border-input bg-background pl-9 pr-3 py-2 text-sm text-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-ring"
              />
            </div>
            {provider && (
              <button
                type="button"
                onClick={() => setModelName(DEFAULT_MODELS[provider as AiProvider] ?? "")}
                className="text-xs text-primary hover:underline"
              >
                Use default: {DEFAULT_MODELS[provider as AiProvider]}
              </button>
            )}
          </div>
        </SettingField>

        {/* Endpoint (Azure / Custom only) */}
        {(provider === "AzureOpenAI" || provider === "Custom") && (
          <SettingField
            label="API Endpoint"
            description={
              provider === "AzureOpenAI"
                ? "Your Azure OpenAI resource endpoint URL."
                : "Your custom OpenAI-compatible endpoint URL."
            }
          >
            <div className="relative">
              <Server className="absolute left-3 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-muted-foreground" />
              <input
                type="text"
                placeholder="https://your-resource.openai.azure.com/…"
                value={apiEndpoint}
                onChange={(e) => setApiEndpoint(e.target.value)}
                className="w-full rounded-lg border border-input bg-background pl-9 pr-3 py-2 text-sm text-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-ring"
              />
            </div>
          </SettingField>
        )}
      </div>

      {/* Save bar */}
      <div className="flex items-center justify-between">
        <div>
          {saved && (
            <div className="flex items-center gap-2 text-sm text-green-600">
              <CheckCircle2 className="h-4 w-4" />
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
              {t.common.save}…
            </>
          ) : (
            <>
              <Save className="h-4 w-4 mr-2" />
              Save AI Settings
            </>
          )}
        </Button>
      </div>
    </div>
  );
}
