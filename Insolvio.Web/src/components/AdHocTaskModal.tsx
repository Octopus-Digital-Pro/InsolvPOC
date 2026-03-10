import { useState, useEffect } from "react";
import { tasksApi } from "@/services/api/tasks";
import { usersApi } from "@/services/api";
import { companiesApi } from "@/services/api/companies";
import { useTranslation } from "@/contexts/LanguageContext";
import type { UserDto, CompanyDto } from "@/services/api/types";
import { Button } from "@/components/ui/button";
import { X, Info, Loader2 } from "lucide-react";

interface Props {
  isOpen: boolean;
  onClose: () => void;
  onCreated: () => void;
  /** Pre-set company (e.g. when opened from CaseDetailPage). If absent, a company picker is shown. */
  companyId?: string;
  /** Pre-set case (optional). */
  caseId?: string;
}

export default function AdHocTaskModal({ isOpen, onClose, onCreated, companyId, caseId }: Props) {
  const { t } = useTranslation();

  const [title, setTitle] = useState("");
  const [description, setDescription] = useState("");
  const [deadline, setDeadline] = useState("");
  const [additionalAssigneeIds, setAdditionalAssigneeIds] = useState<string[]>([]);

  const [users, setUsers] = useState<UserDto[]>([]);
  const [companies, setCompanies] = useState<CompanyDto[]>([]);
  const [selectedCompanyId, setSelectedCompanyId] = useState(companyId ?? "");
  const [companySearch, setCompanySearch] = useState("");

  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!isOpen) return;
    usersApi.getAll().then(r => setUsers(r.data)).catch(console.error);
    if (!companyId) {
      companiesApi.getAll().then(r => setCompanies(r.data)).catch(console.error);
    }
  }, [isOpen, companyId]);

  // Reset form when (re-)opened
  useEffect(() => {
    if (isOpen) {
      setTitle("");
      setDescription("");
      setDeadline("");
      setAdditionalAssigneeIds([]);
      setSelectedCompanyId(companyId ?? "");
      setCompanySearch("");
      setError(null);
    }
  }, [isOpen, companyId]);

  if (!isOpen) return null;

  const effectiveCompanyId = companyId ?? selectedCompanyId;

  const filteredCompanies = companySearch.trim()
    ? companies.filter(c => c.name.toLowerCase().includes(companySearch.toLowerCase()))
    : companies;

  const toggleAssignee = (userId: string) => {
    setAdditionalAssigneeIds(prev =>
      prev.includes(userId) ? prev.filter(id => id !== userId) : [...prev, userId]
    );
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!title.trim()) return;
    if (!effectiveCompanyId) {
      setError("Please select a company.");
      return;
    }
    setSaving(true);
    setError(null);
    try {
      await tasksApi.createAdHoc({
        companyId: effectiveCompanyId,
        caseId: caseId || undefined,
        title: title.trim(),
        description: description.trim() || undefined,
        deadline: deadline || undefined,
        additionalAssigneeIds: additionalAssigneeIds.length > 0 ? additionalAssigneeIds : undefined,
      });
      onCreated();
      onClose();
    } catch (err: unknown) {
      console.error(err);
      setError(t.common.errorsOccurred);
    } finally {
      setSaving(false);
    }
  };

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4"
      onClick={onClose}
    >
      <div
        className="w-full max-w-md rounded-xl bg-card shadow-2xl border border-border"
        onClick={e => e.stopPropagation()}
      >
        {/* Header */}
        <div className="flex items-center justify-between border-b border-border px-5 py-4">
          <div>
            <h2 className="text-base font-semibold text-foreground">{t.tasks.newAdHocTask}</h2>
            <p className="flex items-center gap-1 text-[11px] text-muted-foreground mt-0.5">
              <Info className="h-3 w-3" />
              {t.tasks.notLinkedToStage}
            </p>
          </div>
          <button onClick={onClose} className="rounded-md p-1 hover:bg-accent">
            <X className="h-4 w-4 text-muted-foreground" />
          </button>
        </div>

        {/* Body */}
        <form onSubmit={handleSubmit} className="px-5 py-4 space-y-4">
          {/* Company picker — only shown when no companyId is pre-set */}
          {!companyId && (
            <div>
              <label className="block text-xs font-medium text-foreground mb-1">Company *</label>
              <input
                type="text"
                placeholder="Search companies..."
                value={companySearch}
                onChange={e => setCompanySearch(e.target.value)}
                className="w-full rounded-md border border-input bg-background px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-ring mb-1"
              />
              <select
                value={selectedCompanyId}
                onChange={e => setSelectedCompanyId(e.target.value)}
                className="w-full rounded-md border border-input bg-background px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-ring"
                size={4}
              >
                <option value="">— select —</option>
                {filteredCompanies.map(c => (
                  <option key={c.id} value={c.id}>{c.name}</option>
                ))}
              </select>
            </div>
          )}

          {/* Title */}
          <div>
            <label className="block text-xs font-medium text-foreground mb-1">{t.tasks.taskTitle} *</label>
            <input
              type="text"
              value={title}
              onChange={e => setTitle(e.target.value)}
              required
              maxLength={500}
              placeholder={t.tasks.taskTitlePlaceholder}
              className="w-full rounded-md border border-input bg-background px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-ring"
            />
          </div>

          {/* Description */}
          <div>
            <label className="block text-xs font-medium text-foreground mb-1">{t.tasks.description}</label>
            <textarea
              value={description}
              onChange={e => setDescription(e.target.value)}
              rows={3}
              className="w-full rounded-md border border-input bg-background px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-ring resize-none"
            />
          </div>

          {/* Deadline */}
          <div>
            <label className="block text-xs font-medium text-foreground mb-1">{t.tasks.deadline}</label>
            <input
              type="date"
              value={deadline}
              onChange={e => setDeadline(e.target.value)}
              className="w-full rounded-md border border-input bg-background px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-ring"
            />
          </div>

          {/* Additional assignees */}
          {users.length > 0 && (
            <div>
              <label className="block text-xs font-medium text-foreground mb-1">{t.tasks.assignedTo}</label>
              <div className="max-h-36 overflow-y-auto rounded-md border border-input divide-y divide-border">
                {users.map(u => (
                  <label key={u.id} className="flex items-center gap-2 px-3 py-1.5 hover:bg-accent cursor-pointer">
                    <input
                      type="checkbox"
                      checked={additionalAssigneeIds.includes(u.id)}
                      onChange={() => toggleAssignee(u.id)}
                      className="h-3.5 w-3.5 rounded border-input text-primary"
                    />
                    <span className="text-sm text-foreground">{u.fullName}</span>
                  </label>
                ))}
              </div>
            </div>
          )}

          {/* Error */}
          {error && <p className="text-xs text-destructive">{error}</p>}

          {/* Actions */}
          <div className="flex justify-end gap-2 pt-1">
            <Button type="button" variant="ghost" size="sm" onClick={onClose} disabled={saving}>
              {t.common.cancel}
            </Button>
            <Button type="submit" size="sm" disabled={saving || !title.trim() || !effectiveCompanyId}>
              {saving && <Loader2 className="mr-1.5 h-3.5 w-3.5 animate-spin" />}
              {t.tasks.createTask}
            </Button>
          </div>
        </form>
      </div>
    </div>
  );
}
