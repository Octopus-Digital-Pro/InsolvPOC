import { useState, useEffect, useCallback } from "react";
import { aiConfigApi, type AiConfigDto, type AiProvider, type UpdateAiConfigRequest } from "@/services/api/aiConfig";
import BackButton from "@/components/ui/BackButton";
import { Button } from "@/components/ui/button";
import { useNavigate } from "react-router-dom";
import {
  Loader2, Save, CheckCircle2, Brain, Key, Server,
  Cpu, AlertTriangle, Eye, EyeOff, ToggleLeft, ToggleRight,
} from "lucide-react";
import { format, parseISO } from "date-fns";

const PROVIDERS: { value: AiProvider; label: string; description: string }[] = [
  { value: "OpenAI", label: "OpenAI", description: "GPT-4o, GPT-4 Turbo, etc." },
  { value: "AzureOpenAI", label: "Azure OpenAI", description: "OpenAI models hosted on Azure" },
  { value: "Anthropic", label: "Anthropic", description: "Claude 3.5, Claude 3 Opus, etc." },
  { value: "Google", label: "Google Gemini", description: "Gemini 1.5 Pro, Flash, etc." },
  { value: "OpenRouter", label: "OpenRouter", description: "300+ models via openrouter.ai" },
  { value: "Custom", label: "Custom / Self-hosted", description: "OpenAI-compatible endpoint" },
];

const DEFAULT_MODELS: Record<AiProvider, string> = {
  OpenAI: "gpt-4o",
  AzureOpenAI: "gpt-4o",
  Anthropic: "claude-3-5-sonnet-20241022",
  Google: "gemini-1.5-pro",
  OpenRouter: "openai/gpt-4o",
  Custom: "",
};

interface FieldProps {
  label: string;
  description?: string;
  children: React.ReactNode;
}

function SettingField({ label, description, children }: FieldProps) {
  return (
    <div className="flex flex-col sm:flex-row sm:items-start gap-2 sm:gap-6 py-4 border-b border-border last:border-0">
      <div className="sm:w-56 shrink-0">
        <p className="text-sm font-medium text-foreground">{label}</p>
        {description && <p className="text-xs text-muted-foreground mt-0.5">{description}</p>}
      </div>
      <div className="flex-1">{children}</div>
    </div>
  );
}

export default function AiSettingsPage() {
  const navigate = useNavigate();
  const [config, setConfig] = useState<AiConfigDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Form state
  const [provider, setProvider] = useState<AiProvider>("OpenAI");
  const [apiKey, setApiKey] = useState("");
  const [showKey, setShowKey] = useState(false);
  const [apiEndpoint, setApiEndpoint] = useState("");
  const [modelName, setModelName] = useState("");
  const [deploymentName, setDeploymentName] = useState("");
  const [isEnabled, setIsEnabled] = useState(false);
  const [notes, setNotes] = useState("");

  const loadConfig = useCallback(async () => {
    setLoading(true);
    try {
      const r = await aiConfigApi.get();
      const d = r.data;
      setConfig(d);
      setProvider(d.provider);
      setApiEndpoint(d.apiEndpoint ?? "");
      setModelName(d.modelName ?? DEFAULT_MODELS[d.provider]);
      setDeploymentName(d.deploymentName ?? "");
      setIsEnabled(d.isEnabled);
      setNotes(d.notes ?? "");
      setApiKey(""); // Never pre-fill the key
    } catch {
      setError("Failed to load AI configuration.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { loadConfig(); }, [loadConfig]);

  const handleProviderChange = (p: AiProvider) => {
    setProvider(p);
    // Always reset to provider default when switching to OpenRouter (model IDs are incompatible)
    if (p === "OpenRouter" || !modelName || modelName === DEFAULT_MODELS[provider]) {
      setModelName(DEFAULT_MODELS[p]);
    }
    // Clear Azure-specific fields when not using Azure
    if (p !== "AzureOpenAI") setDeploymentName("");
    // Clear endpoint for providers that don't need it; pre-fill for OpenRouter
    if (p === "OpenAI" || p === "Anthropic" || p === "Google") setApiEndpoint("");
    if (p === "OpenRouter" && !apiEndpoint) setApiEndpoint("https://openrouter.ai/api/v1");
  };

  const handleSave = async () => {
    setSaving(true);
    setError(null);
    try {
      const req: UpdateAiConfigRequest = {
        provider,
        apiKey: apiKey === "" ? null : apiKey, // null = unchanged
        apiEndpoint: apiEndpoint || null,
        modelName: modelName || null,
        deploymentName: deploymentName || null,
        isEnabled,
        notes: notes || null,
      };
      const r = await aiConfigApi.update(req);
      setConfig(r.data);
      setApiKey("");
      setSaved(true);
      setTimeout(() => setSaved(false), 3000);
    } catch {
      setError("Failed to save AI configuration.");
    } finally {
      setSaving(false);
    }
  };

  const showEndpoint = provider === "AzureOpenAI" || provider === "OpenRouter" || provider === "Custom";
  const showDeployment = provider === "AzureOpenAI";

  if (loading) {
    return (
      <div className="flex items-center justify-center h-48">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  return (
    <div className="max-w-2xl space-y-6">
      {/* Header */}
      <div className="flex items-center gap-3">
        <div className="flex h-10 w-10 items-center justify-center rounded-full bg-primary/10">
          <Brain className="h-5 w-5 text-primary" />
        </div>
        <div>
          <h1 className="text-xl font-semibold text-foreground">AI Configuration</h1>
          <p className="text-sm text-muted-foreground">
            Configure the AI provider used for case summaries and intelligence features.
          </p>
        </div>
      </div>

      {/* Status banner */}
      <div className={`flex items-center gap-3 rounded-lg border px-4 py-3 text-sm ${
        isEnabled
          ? "border-green-200 bg-green-50 text-green-800 dark:border-green-800 dark:bg-green-950 dark:text-green-300"
          : "border-amber-200 bg-amber-50 text-amber-800 dark:border-amber-800 dark:bg-amber-950 dark:text-amber-300"
      }`}>
        {isEnabled
          ? <CheckCircle2 className="h-4 w-4 shrink-0" />
          : <AlertTriangle className="h-4 w-4 shrink-0" />}
        <span>
          AI features are currently <strong>{isEnabled ? "enabled" : "disabled"}</strong>.
          {config?.updatedAt && (
            <span className="text-xs ml-2 opacity-70">
              Last updated {format(parseISO(config.updatedAt), "d MMM yyyy, HH:mm")}
            </span>
          )}
        </span>
      </div>

      {/* Form card */}
      <div className="rounded-xl border border-border bg-card p-5 space-y-0">
        {/* Enable / Disable */}
        <SettingField
          label="Enable AI Features"
          description="Activate or deactivate all AI-powered functionality across the system."
        >
          <button
            type="button"
            onClick={() => setIsEnabled(v => !v)}
            className={`flex items-center gap-2 rounded-lg px-3 py-1.5 text-sm font-medium transition-colors ${
              isEnabled
                ? "bg-green-100 text-green-700 hover:bg-green-200 dark:bg-green-900/30 dark:text-green-400"
                : "bg-muted text-muted-foreground hover:bg-accent hover:text-foreground"
            }`}
          >
            {isEnabled ? <ToggleRight className="h-4 w-4" /> : <ToggleLeft className="h-4 w-4" />}
            {isEnabled ? "Enabled" : "Disabled"}
          </button>
        </SettingField>

        {/* Provider */}
        <SettingField
          label="AI Provider"
          description="Select the AI service provider."
        >
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
            {PROVIDERS.map(p => (
              <button
                key={p.value}
                type="button"
                onClick={() => handleProviderChange(p.value)}
                className={`flex flex-col items-start rounded-lg border px-3 py-2.5 text-left transition-colors ${
                  provider === p.value
                    ? "border-primary bg-primary/5 text-foreground"
                    : "border-border text-muted-foreground hover:border-primary/40 hover:bg-accent"
                }`}
              >
                <span className="text-sm font-medium">{p.label}</span>
                <span className="text-xs opacity-70">{p.description}</span>
              </button>
            ))}
          </div>
        </SettingField>

        {/* API Key */}
        <SettingField
          label="API Key"
          description={config?.hasApiKey ? "A key is stored. Enter a new value to replace it, or leave blank to keep the current key." : "Enter your API key. It will be encrypted at rest."}
        >
          <div className="flex gap-2">
            <div className="relative flex-1">
              <Key className="absolute left-3 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-muted-foreground" />
              <input
                type={showKey ? "text" : "password"}
                value={apiKey}
                onChange={e => setApiKey(e.target.value)}
                placeholder={config?.hasApiKey ? "●●●●●●●●●●●● (key stored — leave blank to keep)" : "sk-..."}
                className="w-full rounded-md border border-input bg-background pl-8 pr-10 py-2 text-sm placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-ring"
              />
              <button
                type="button"
                onClick={() => setShowKey(v => !v)}
                className="absolute right-3 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground"
              >
                {showKey ? <EyeOff className="h-3.5 w-3.5" /> : <Eye className="h-3.5 w-3.5" />}
              </button>
            </div>
            {config?.hasApiKey && (
              <Button
                variant="outline"
                size="sm"
                className="text-xs text-destructive hover:text-destructive border-destructive/30"
                onClick={() => setApiKey("")}
              >
                Clear
              </Button>
            )}
          </div>
          {config?.hasApiKey && !apiKey && (
            <p className="mt-1.5 text-xs text-green-600 dark:text-green-400 flex items-center gap-1">
              <CheckCircle2 className="h-3 w-3" /> Encrypted key stored
            </p>
          )}
        </SettingField>

        {/* API Endpoint (Azure / OpenRouter / Custom only) */}
        {showEndpoint && (
          <SettingField
            label={provider === "AzureOpenAI" ? "Azure Endpoint" : "API Endpoint"}
            description={
              provider === "AzureOpenAI"
                ? "e.g. https://my-resource.openai.azure.com/"
                : provider === "OpenRouter"
                ? "OpenRouter base URL (default: https://openrouter.ai/api/v1)."
                : "Base URL for your OpenAI-compatible endpoint."
            }
          >
            <div className="relative">
              <Server className="absolute left-3 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-muted-foreground" />
              <input
                type="url"
                value={apiEndpoint}
                onChange={e => setApiEndpoint(e.target.value)}
                placeholder={
                  provider === "AzureOpenAI"
                    ? "https://my-resource.openai.azure.com/"
                    : provider === "OpenRouter"
                    ? "https://openrouter.ai/api/v1"
                    : "https://"
                }
                className="w-full rounded-md border border-input bg-background pl-8 pr-3 py-2 text-sm placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-ring"
              />
            </div>
          </SettingField>
        )}

        {/* Model Name */}
        <SettingField
          label="Model Name"
          description={
            provider === "OpenRouter"
              ? "Use provider/model-name format, e.g. openai/gpt-4o or anthropic/claude-3-5-sonnet."
              : "The specific model to use for AI completions."
          }
        >
          <div className="relative">
            <Cpu className="absolute left-3 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-muted-foreground" />
            <input
              type="text"
              value={modelName}
              onChange={e => setModelName(e.target.value)}
              placeholder={DEFAULT_MODELS[provider] || "model-name"}
              className="w-full rounded-md border border-input bg-background pl-8 pr-3 py-2 text-sm placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-ring"
            />
          </div>
        </SettingField>

        {/* Deployment Name (Azure only) */}
        {showDeployment && (
          <SettingField
            label="Deployment Name"
            description="The Azure OpenAI deployment name (may differ from model name)."
          >
            <input
              type="text"
              value={deploymentName}
              onChange={e => setDeploymentName(e.target.value)}
              placeholder="my-gpt4o-deployment"
              className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-ring"
            />
          </SettingField>
        )}

        {/* Notes */}
        <SettingField
          label="Notes"
          description="Internal notes about this configuration (optional)."
        >
          <textarea
            value={notes}
            onChange={e => setNotes(e.target.value)}
            rows={2}
            placeholder="e.g. Production key — rate limit: 100k TPM"
            className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-ring resize-none"
          />
        </SettingField>
      </div>

      {/* Error */}
      {error && (
        <div className="flex items-center gap-2 rounded-lg border border-destructive/30 bg-destructive/5 px-4 py-3 text-sm text-destructive">
          <AlertTriangle className="h-4 w-4 shrink-0" />
          {error}
        </div>
      )}

      {/* Save */}
      <div className="flex items-center gap-3">
        <Button onClick={handleSave} disabled={saving} className="gap-2">
          {saving ? <Loader2 className="h-4 w-4 animate-spin" /> : <Save className="h-4 w-4" />}
          Save Configuration
        </Button>
        {saved && (
          <span className="flex items-center gap-1.5 text-sm text-green-600 dark:text-green-400">
            <CheckCircle2 className="h-4 w-4" /> Saved successfully
          </span>
        )}
      </div>

      <BackButton onClick={() => navigate("/settings")}>← Back</BackButton>
    </div>
  );
}
