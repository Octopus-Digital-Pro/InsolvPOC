import { useState, useEffect } from "react";
import { settingsApi } from "@/services/api/settingsApi";
import { useTranslation } from "@/contexts/LanguageContext";
import { Button } from "@/components/ui/button";
import { Loader2, Globe, Save, CheckCircle2, ExternalLink } from "lucide-react";

export default function IntegrationsSettingsPage() {
  const { t } = useTranslation();
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [bpiPortalUrl, setBpiPortalUrl] = useState("https://portal.onrc.ro");

  useEffect(() => {
    settingsApi.integrations
      .get()
      .then((res) => {
        const item = res.data.find((i) => i.key === "Integrations:BpiPortalUrl");
        if (item?.value) setBpiPortalUrl(item.value);
      })
      .catch(console.error)
      .finally(() => setLoading(false));
  }, []);

  const handleSave = async () => {
    setSaving(true);
    setSaved(false);
    try {
      await settingsApi.integrations.update([
        {
          key: "Integrations:BpiPortalUrl",
          value: bpiPortalUrl,
          group: "Integrations",
          description: "BPI insolvency portal URL",
        },
      ]);
      setSaved(true);
      setTimeout(() => setSaved(false), 3000);
    } catch (err) {
      console.error("Failed to save integration settings", err);
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <div className="flex h-48 items-center justify-center">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-2xl space-y-6">
      {/* Header */}
      <div className="flex items-center gap-3">
        <div className="flex h-9 w-9 items-center justify-center rounded-lg bg-chart-3/15 text-chart-3">
          <Globe className="h-5 w-5" />
        </div>
        <div>
          <h1 className="text-xl font-semibold text-foreground">{t.integrations.pageTitle}</h1>
          <p className="text-sm text-muted-foreground">{t.integrations.pageDesc}</p>
        </div>
      </div>

      {/* BPI Portal section */}
      <div className="rounded-xl border border-border bg-card p-5 space-y-4">
        <div className="flex items-center gap-2">
          <Globe className="h-4 w-4 text-chart-3" />
          <h2 className="text-sm font-semibold text-foreground">BPI Portal</h2>
          <a
            href={bpiPortalUrl}
            target="_blank"
            rel="noopener noreferrer"
            className="ml-auto flex items-center gap-1 text-xs text-primary hover:underline"
          >
            Open portal <ExternalLink className="h-3 w-3" />
          </a>
        </div>

        <div className="space-y-1.5">
          <label className="block text-sm font-medium text-foreground">
            {t.integrations.bpiPortalUrl}
          </label>
          <p className="text-xs text-muted-foreground">{t.integrations.bpiPortalUrlDesc}</p>
          <input
            type="url"
            value={bpiPortalUrl}
            onChange={(e) => setBpiPortalUrl(e.target.value)}
            placeholder={t.integrations.bpiPortalUrlPlaceholder}
            className="w-full rounded-lg border border-input bg-background px-3 py-2 text-sm text-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-ring font-mono"
          />
        </div>
      </div>

      {/* Save */}
      <div className="flex items-center gap-3">
        <Button onClick={handleSave} disabled={saving} className="gap-2">
          {saving ? (
            <Loader2 className="h-4 w-4 animate-spin" />
          ) : (
            <Save className="h-4 w-4" />
          )}
          {t.integrations.save}
        </Button>
        {saved && (
          <span className="flex items-center gap-1.5 text-sm text-chart-2">
            <CheckCircle2 className="h-4 w-4" />
            {t.integrations.saved}
          </span>
        )}
      </div>
    </div>
  );
}
