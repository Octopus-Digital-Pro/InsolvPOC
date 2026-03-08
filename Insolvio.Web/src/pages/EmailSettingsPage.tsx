import { useState, useEffect } from "react";
import { settingsApi } from "@/services/api/settingsApi";
import type { SystemConfigItem } from "@/services/api/settingsApi";
import { useTranslation } from "@/contexts/LanguageContext";
import { Button } from "@/components/ui/button";
import { Loader2, Mail, Save, CheckCircle2 } from "lucide-react";

function Toggle({
  checked,
  onChange,
  label,
  description,
}: {
  checked: boolean;
  onChange: (v: boolean) => void;
  label: string;
  description?: string;
}) {
  return (
    <div className="flex items-start justify-between gap-4 py-4 border-b border-border last:border-b-0">
      <div className="min-w-0">
        <p className="text-sm font-medium text-foreground">{label}</p>
        {description && <p className="mt-0.5 text-xs text-muted-foreground">{description}</p>}
      </div>
      <button
        type="button"
        role="switch"
        aria-checked={checked}
        onClick={() => onChange(!checked)}
        className={`relative inline-flex h-6 w-11 shrink-0 cursor-pointer items-center rounded-full border-2 border-transparent transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring ${
          checked ? "bg-primary" : "bg-muted"
        }`}
      >
        <span
          className={`pointer-events-none block h-5 w-5 rounded-full bg-white shadow-lg ring-0 transition-transform ${
            checked ? "translate-x-5" : "translate-x-0"
          }`}
        />
      </button>
    </div>
  );
}

export default function EmailSettingsPage() {
  const { t } = useTranslation();
  const [configs, setConfigs] = useState<SystemConfigItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);

  // Local state for each preference
  const [autoCcUser, setAutoCcUser] = useState(false);
  const [inboundEnabled, setInboundEnabled] = useState(true);
  const [adminCcAddress, setAdminCcAddress] = useState("");

  useEffect(() => {
    settingsApi.emailPreferences
      .get()
      .then((res) => {
        const items: SystemConfigItem[] = res.data;
        setConfigs(items);
        const get = (key: string) => items.find((i) => i.key === key)?.value ?? "";
        setAutoCcUser(get("Email:AutoCcUser") === "true");
        setInboundEnabled(get("Email:InboundEnabled") !== "false"); // default true if not set
        setAdminCcAddress(get("Email:AdminCcAddress"));
      })
      .catch(console.error)
      .finally(() => setLoading(false));
  }, []);

  const handleSave = async () => {
    setSaving(true);
    setSaved(false);
    try {
      await settingsApi.emailPreferences.update([
        { key: "Email:AutoCcUser", value: autoCcUser ? "true" : "false", group: "Email", description: "Auto-CC current user on outbound emails" },
        { key: "Email:InboundEnabled", value: inboundEnabled ? "true" : "false", group: "Email", description: "Poll S3 for inbound .eml files" },
        { key: "Email:AdminCcAddress", value: adminCcAddress, group: "Email", description: "Always CC this address on outbound emails" },
      ]);
      setSaved(true);
      setTimeout(() => setSaved(false), 3000);
    } catch (err) {
      console.error("Failed to save email preferences", err);
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
        <div className="flex h-9 w-9 items-center justify-center rounded-lg bg-primary/10 text-primary">
          <Mail className="h-5 w-5" />
        </div>
        <div>
          <h1 className="text-xl font-semibold text-foreground">{t.emailSettings.pageTitle}</h1>
          <p className="text-sm text-muted-foreground">{t.emailSettings.pageDesc}</p>
        </div>
      </div>

      {/* Toggles card */}
      <div className="rounded-xl border border-border bg-card px-5">
        <Toggle
          checked={autoCcUser}
          onChange={setAutoCcUser}
          label={t.emailSettings.autoCcUser}
          description={t.emailSettings.autoCcUserDesc}
        />
        <Toggle
          checked={inboundEnabled}
          onChange={setInboundEnabled}
          label={t.emailSettings.inboundEnabled}
          description={t.emailSettings.inboundEnabledDesc}
        />
      </div>

      {/* Admin CC address */}
      <div className="rounded-xl border border-border bg-card p-5 space-y-2">
        <label className="block text-sm font-medium text-foreground">
          {t.emailSettings.adminCcAddress}
        </label>
        <p className="text-xs text-muted-foreground">{t.emailSettings.adminCcAddressDesc}</p>
        <input
          type="email"
          value={adminCcAddress}
          onChange={(e) => setAdminCcAddress(e.target.value)}
          placeholder={t.emailSettings.adminCcAddressPlaceholder}
          className="w-full rounded-lg border border-input bg-background px-3 py-2 text-sm text-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-ring"
        />
      </div>

      {/* Save button */}
      <div className="flex items-center gap-3">
        <Button onClick={handleSave} disabled={saving} className="gap-2">
          {saving ? (
            <Loader2 className="h-4 w-4 animate-spin" />
          ) : (
            <Save className="h-4 w-4" />
          )}
          {t.emailSettings.save}
        </Button>
        {saved && (
          <span className="flex items-center gap-1.5 text-sm text-chart-2">
            <CheckCircle2 className="h-4 w-4" />
            {t.emailSettings.saved}
          </span>
        )}
      </div>
    </div>
  );
}
