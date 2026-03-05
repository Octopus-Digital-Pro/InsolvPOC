import { useState, useEffect } from "react";
import { deadlineSettingsApi } from "@/services/api/deadlineSettings";
import type { TenantDeadlineSettingsDto, DeadlinePreviewDto } from "@/services/api/deadlineSettings";
import { useTranslation } from "@/contexts/LanguageContext";
import { Button } from "@/components/ui/button";
import BackButton from "@/components/ui/BackButton";
import { useNavigate } from "react-router-dom";
import {
  Loader2, Save, Clock, CalendarDays, RefreshCw,
  AlertTriangle, CheckCircle2, Settings2,
} from "lucide-react";
import { format } from "date-fns";

interface FieldProps {
  label: string;
  description: string;
  children: React.ReactNode;
}

function SettingField({ label, description, children }: FieldProps) {
  return (
    <div className="flex flex-col sm:flex-row sm:items-center gap-2 sm:gap-4 py-3 border-b border-border last:border-0">
      <div className="flex-1 min-w-0">
        <p className="text-sm font-medium text-foreground">{label}</p>
        <p className="text-[11px] text-muted-foreground">{description}</p>
  </div>
      <div className="shrink-0">{children}</div>
    </div>
  );
}

function NumberInput({ value, onChange, min = 0, max = 365, unit }: {
  value: number;
  onChange: (v: number) => void;
  min?: number;
  max?: number;
  unit?: string;
}) {
  return (
    <div className="flex items-center gap-1.5">
      <input
        type="number"
        value={value}
        onChange={e => onChange(Math.max(min, Math.min(max, parseInt(e.target.value) || 0)))}
className="w-20 rounded-md border border-input bg-background px-2.5 py-1.5 text-sm text-right focus:outline-none focus:ring-2 focus:ring-ring"
      min={min}
    max={max}
      />
      {unit && <span className="text-xs text-muted-foreground">{unit}</span>}
    </div>
  );
}

function Toggle({ checked, onChange }: { checked: boolean; onChange: (v: boolean) => void }) {
  return (
  <button
      onClick={() => onChange(!checked)}
      className={`relative h-6 w-11 rounded-full transition-colors ${
checked ? "bg-primary" : "bg-muted"
      }`}
    >
      <span className={`absolute top-0.5 left-0.5 h-5 w-5 rounded-full bg-white shadow-sm transition-transform ${
   checked ? "translate-x-5" : ""
      }`} />
    </button>
  );
}

export default function DeadlineSettingsPage() {
  const navigate = useNavigate();
  const { t } = useTranslation();
  const [settings, setSettings] = useState<TenantDeadlineSettingsDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [preview, setPreview] = useState<DeadlinePreviewDto | null>(null);
  const [previewDate, setPreviewDate] = useState(format(new Date(), "yyyy-MM-dd"));

  useEffect(() => {
    deadlineSettingsApi.getTenantSettings()
      .then(r => setSettings(r.data))
      .catch(console.error)
      .finally(() => setLoading(false));
  }, []);

  const handleSave = async () => {
    if (!settings) return;
    setSaving(true);
    setSaved(false);
    try {
      const r = await deadlineSettingsApi.updateTenantSettings(settings);
      setSettings(r.data);
      setSaved(true);
   setTimeout(() => setSaved(false), 3000);
  } catch (e) {
console.error(e);
    } finally {
      setSaving(false);
    }
  };

  const handlePreview = async () => {
    try {
      const r = await deadlineSettingsApi.preview(previewDate);
    setPreview(r.data);
    } catch (e) {
      console.error(e);
    }
  };

  const update = <K extends keyof TenantDeadlineSettingsDto>(key: K, value: TenantDeadlineSettingsDto[K]) => {
    if (!settings) return;
  setSettings({ ...settings, [key]: value });
  };

  if (loading) return <div className="flex h-full items-center justify-center"><Loader2 className="h-8 w-8 animate-spin text-muted-foreground" /></div>;
  if (!settings) return <p className="p-8 text-muted-foreground">{t.deadlines.failedToLoad}</p>;

  return (
    <div className="mx-auto max-w-3xl space-y-6">
      <div className="flex items-center justify-between">
        <div>
      <BackButton className="cursor-pointer flex items-center gap-2 mb-2 text-xs" onClick={() => navigate("/settings")}>
      {t.deadlines.backToSettings}
          </BackButton>
      <h1 className="text-xl font-bold text-foreground flex items-center gap-2">
            <Settings2 className="h-5 w-5" /> {t.deadlines.title}
          </h1>
          <p className="text-sm text-muted-foreground mt-1">
         {t.deadlines.description}
</p>
        </div>
        <Button
     onClick={handleSave}
  disabled={saving}
          className="gap-1.5"
      >
          {saving ? <Loader2 className="h-4 w-4 animate-spin" /> : saved ? <CheckCircle2 className="h-4 w-4" /> : <Save className="h-4 w-4" />}
          {saved ? t.deadlines.saved : t.common.save}
        </Button>
      </div>

      {/* Deadline Periods */}
      <div className="rounded-xl border border-border bg-card">
   <div className="border-b border-border px-4 py-3">
        <h2 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground flex items-center gap-1.5">
 <Clock className="h-3.5 w-3.5" /> {t.deadlines.deadlinePeriods}
          </h2>
     </div>
<div className="px-4">
    <SettingField label={t.deadlines.claimDeadline} description={t.deadlines.claimDeadlineDesc}>
            <NumberInput value={settings.claimDeadlineDaysFromNotice} onChange={v => update("claimDeadlineDaysFromNotice", v)} unit={t.deadlines.days} />
       </SettingField>
          <SettingField label={t.deadlines.objectionDeadline} description={t.deadlines.objectionDeadlineDesc}>
         <NumberInput value={settings.objectionDeadlineDaysFromNotice} onChange={v => update("objectionDeadlineDaysFromNotice", v)} unit={t.deadlines.days} />
   </SettingField>
          <SettingField label={t.deadlines.initialNotice} description={t.deadlines.initialNoticeDesc}>
     <NumberInput value={settings.sendInitialNoticeWithinDays} onChange={v => update("sendInitialNoticeWithinDays", v)} unit={t.deadlines.days} />
          </SettingField>
      <SettingField label={t.deadlines.meetingNotice} description={t.deadlines.meetingNoticeDesc}>
         <NumberInput value={settings.meetingNoticeMinimumDays} onChange={v => update("meetingNoticeMinimumDays", v)} unit={t.deadlines.days} />
 </SettingField>
          <SettingField label={t.deadlines.reportFrequency} description={t.deadlines.reportFrequencyDesc}>
 <NumberInput value={settings.reportEveryNDays} onChange={v => update("reportEveryNDays", v)} unit={t.deadlines.days} />
          </SettingField>
        </div>
  </div>

   {/* Calculation Options */}
      <div className="rounded-xl border border-border bg-card">
        <div className="border-b border-border px-4 py-3">
   <h2 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground flex items-center gap-1.5">
        <CalendarDays className="h-3.5 w-3.5" /> {t.deadlines.calculationOptions}
          </h2>
        </div>
    <div className="px-4">
      <SettingField label={t.deadlines.useBusinessDays} description={t.deadlines.useBusinessDaysDesc}>
 <Toggle checked={settings.useBusinessDays} onChange={v => update("useBusinessDays", v)} />
</SettingField>
      <SettingField label={t.deadlines.adjustToWorkingDay} description={t.deadlines.adjustToWorkingDayDesc}>
       <Toggle checked={settings.adjustToNextWorkingDay} onChange={v => update("adjustToNextWorkingDay", v)} />
          </SettingField>
          <SettingField label={t.deadlines.reminderSchedule} description={t.deadlines.reminderScheduleDesc}>
 <input
          type="text"
              value={settings.reminderDaysBeforeDeadline}
     onChange={e => update("reminderDaysBeforeDeadline", e.target.value)}
   placeholder="7,3,1,0"
    className="w-32 rounded-md border border-input bg-background px-2.5 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-ring"
            />
          </SettingField>
        </div>
      </div>

      {/* Escalation */}
      <div className="rounded-xl border border-border bg-card">
        <div className="border-b border-border px-4 py-3">
    <h2 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground flex items-center gap-1.5">
   <AlertTriangle className="h-3.5 w-3.5" /> {t.deadlines.escalationRules}
    </h2>
      </div>
        <div className="px-4">
          <SettingField label={t.deadlines.urgentThreshold} description={t.deadlines.urgentThresholdDesc}>
  <NumberInput value={settings.urgentQueueHoursBeforeDeadline} onChange={v => update("urgentQueueHoursBeforeDeadline", v)} unit={t.deadlines.hours} />
          </SettingField>
     <SettingField label={t.deadlines.autoAssignBackup} description={t.deadlines.autoAssignBackupDesc}>
         <Toggle checked={settings.autoAssignBackupOnCriticalOverdue} onChange={v => update("autoAssignBackupOnCriticalOverdue", v)} />
          </SettingField>
        </div>
      </div>

      {/* Preview */}
      <div className="rounded-xl border border-primary/20 bg-primary/5">
<div className="border-b border-primary/10 px-4 py-3">
     <h2 className="text-xs font-semibold uppercase tracking-wide text-primary flex items-center gap-1.5">
            <RefreshCw className="h-3.5 w-3.5" /> {t.deadlines.preview}
    </h2>
          <p className="text-[11px] text-muted-foreground mt-0.5">{t.deadlines.previewDescription}</p>
      </div>
  <div className="px-4 py-3 space-y-3">
          <div className="flex items-center gap-2">
            <label className="text-xs text-muted-foreground">{t.deadlines.noticeDate}:</label>
            <input
              type="date"
          value={previewDate}
              onChange={e => setPreviewDate(e.target.value)}
              className="rounded-md border border-input bg-background px-2.5 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-ring"
            />
 <Button variant="outline" size="sm" className="text-xs gap-1" onClick={handlePreview}>
           <RefreshCw className="h-3 w-3" /> {t.deadlines.compute}
            </Button>
    </div>
    {preview && (
            <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
   {[
            { label: t.deadlines.claimDeadlineResult, value: preview.claimDeadline },
{ label: t.deadlines.objectionDeadlineResult, value: preview.objectionDeadline },
      { label: t.deadlines.noticeSendBy, value: preview.initialNoticeSendBy },
       { label: t.deadlines.firstReport, value: preview.firstReportDue },
              ].map(d => (
                <div key={d.label} className="rounded-lg border border-border bg-card p-2.5 text-center">
         <p className="text-[10px] text-muted-foreground">{d.label}</p>
               <p className="text-sm font-semibold text-foreground">
 {format(new Date(d.value), "dd MMM yyyy")}
       </p>
        </div>
    ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
