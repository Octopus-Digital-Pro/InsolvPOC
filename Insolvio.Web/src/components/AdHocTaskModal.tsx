import { useState, useEffect, useRef } from "react";
import { tasksApi } from "@/services/api/tasks";
import { usersApi } from "@/services/api";
import { companiesApi } from "@/services/api/companies";
import { useTranslation } from "@/contexts/LanguageContext";
import type { UserDto, CompanyDto } from "@/services/api/types";
import { Button } from "@/components/ui/button";
import { X, Info, Loader2, ChevronDown, Check } from "lucide-react";

// ─── Company autocomplete combobox ───────────────────────────────────────────
function CompanyCombobox({
  companies,
  value,
  onChange,
}: {
  companies: CompanyDto[];
  value: string;
  onChange: (id: string) => void;
}) {
  const [search, setSearch] = useState("");
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (!open) return;
    const handler = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) {
        setOpen(false);
        setSearch("");
      }
    };
    document.addEventListener("mousedown", handler);
    return () => document.removeEventListener("mousedown", handler);
  }, [open]);

  const selected = companies.find(c => c.id === value) ?? null;

  const filtered = search.trim()
    ? companies.filter(c => c.name.toLowerCase().includes(search.toLowerCase()))
    : companies;

  const handleSelect = (id: string) => {
    onChange(id);
    setOpen(false);
    setSearch("");
  };

  return (
    <div className="space-y-1.5" ref={ref}>
      <label className="block text-xs font-semibold uppercase tracking-wide text-muted-foreground">
        Company <span className="text-destructive">*</span>
      </label>
      <div className="relative">
        {/* Trigger — shows selected company or inline search when open */}
        <div
          className="flex w-full cursor-pointer items-center gap-2 rounded-md border border-input bg-background px-3 py-2 text-sm focus-within:ring-2 focus-within:ring-ring hover:bg-accent/40 transition-colors"
          onClick={() => { setOpen(true); setTimeout(() => inputRef.current?.focus(), 0); }}
        >
          {open ? (
            <input
              ref={inputRef}
              type="text"
              value={search}
              onChange={e => setSearch(e.target.value)}
              placeholder="Type to search companies\u2026"
              autoFocus
              className="flex-1 bg-transparent text-sm outline-none placeholder:text-muted-foreground"
              aria-autocomplete="list"
              aria-expanded={open}
              aria-haspopup="listbox"
            />
          ) : (
            <span className={`flex-1 truncate ${selected ? "text-foreground" : "text-muted-foreground"}`}>
              {selected ? selected.name : "Search companies\u2026"}
            </span>
          )}
          {selected && !open && (
            <span
              role="button"
              tabIndex={0}
              onClick={e => { e.stopPropagation(); onChange(""); }}
              onKeyDown={e => { if (e.key === "Enter" || e.key === " ") { e.preventDefault(); onChange(""); } }}
              className="shrink-0 rounded p-0.5 text-muted-foreground hover:bg-destructive/10 hover:text-destructive transition-colors"
              aria-label="Clear company"
            >
              <X className="h-3.5 w-3.5" />
            </span>
          )}
          <ChevronDown
            className={`h-4 w-4 shrink-0 text-muted-foreground transition-transform duration-150 ${open ? "rotate-180" : ""}`}
          />
        </div>

        {/* Dropdown */}
        {open && (
          <div
            className="absolute left-0 right-0 top-full z-20 mt-1 max-h-52 overflow-y-auto rounded-md border border-border bg-popover shadow-lg"
            role="listbox"
          >
            {filtered.length === 0 ? (
              <p className="px-3 py-3 text-center text-sm text-muted-foreground">No companies found</p>
            ) : (
              filtered.map(c => (
                <button
                  key={c.id}
                  type="button"
                  role="option"
                  aria-selected={value === c.id}
                  onClick={() => handleSelect(c.id)}
                  className={`flex w-full items-center gap-2.5 px-3 py-2 text-left text-sm transition-colors hover:bg-accent ${
                    value === c.id ? "bg-accent/60 text-foreground" : "text-popover-foreground"
                  }`}
                >
                  <span className="flex-1 truncate">{c.name}</span>
                  {value === c.id && <Check className="h-3.5 w-3.5 shrink-0 text-primary" />}
                </button>
              ))
            )}
          </div>
        )}
      </div>
    </div>
  );
}

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
  const [assignedUserId, setAssignedUserId] = useState<string | null>(null);
  const [userSearch, setUserSearch] = useState("");
  const [userDropdownOpen, setUserDropdownOpen] = useState(false);

  const [users, setUsers] = useState<UserDto[]>([]);
  const [companies, setCompanies] = useState<CompanyDto[]>([]);
  const [selectedCompanyId, setSelectedCompanyId] = useState(companyId ?? "");

  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const userDropdownRef = useRef<HTMLDivElement>(null);

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
      setAssignedUserId(null);
      setUserSearch("");
      setUserDropdownOpen(false);
      setSelectedCompanyId(companyId ?? "");
      setError(null);
    }
  }, [isOpen, companyId]);

  // Close user dropdown on outside click
  useEffect(() => {
    if (!userDropdownOpen) return;
    const handleClickOutside = (e: MouseEvent) => {
      if (userDropdownRef.current && !userDropdownRef.current.contains(e.target as Node)) {
        setUserDropdownOpen(false);
        setUserSearch("");
      }
    };
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, [userDropdownOpen]);

  if (!isOpen) return null;

  const effectiveCompanyId = companyId ?? selectedCompanyId;

  const filteredUsers = userSearch.trim()
    ? users.filter(u => u.fullName.toLowerCase().includes(userSearch.toLowerCase()))
    : users;

  const selectedUser = assignedUserId ? users.find(u => u.id === assignedUserId) ?? null : null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!title.trim()) return;
    if (!effectiveCompanyId) {
      setError("Please select a company.");
      return;
    }
    if (!assignedUserId) {
      setError("Please assign the task to a user.");
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
        additionalAssigneeIds: assignedUserId ? [assignedUserId] : undefined,
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
        <div className="flex items-center justify-between border-b border-border px-6 py-4">
          <div>
            <h2 className="text-base font-semibold text-foreground">{t.tasks.newAdHocTask}</h2>
            <p className="flex items-center gap-1.5 text-[11px] text-muted-foreground mt-0.5">
              <Info className="h-3 w-3 shrink-0" />
              {t.tasks.notLinkedToStage}
            </p>
          </div>
          <button
            onClick={onClose}
            className="rounded-md p-1.5 text-muted-foreground hover:bg-accent hover:text-foreground transition-colors"
            aria-label="Close"
          >
            <X className="h-4 w-4" />
          </button>
        </div>

        {/* Body */}
        <form onSubmit={handleSubmit} className="px-6 py-5 space-y-4">

          {/* Company autocomplete combobox — only shown when no companyId is pre-set */}
          {!companyId && (
            <CompanyCombobox
              companies={companies}
              value={selectedCompanyId}
              onChange={setSelectedCompanyId}
            />
          )}

          {/* Title */}
          <div className="space-y-1.5">
            <label htmlFor="adhoc-title" className="block text-xs font-semibold uppercase tracking-wide text-muted-foreground">
              {t.tasks.taskTitle} <span className="text-destructive">*</span>
            </label>
            <input
              id="adhoc-title"
              type="text"
              value={title}
              onChange={e => setTitle(e.target.value)}
              required
              maxLength={500}
              placeholder={t.tasks.taskTitlePlaceholder}
              className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring"
            />
          </div>

          {/* Description */}
          <div className="space-y-1.5">
            <label htmlFor="adhoc-description" className="block text-xs font-semibold uppercase tracking-wide text-muted-foreground">
              {t.tasks.description}
            </label>
            <textarea
              id="adhoc-description"
              value={description}
              onChange={e => setDescription(e.target.value)}
              rows={3}
              placeholder="Optional details about this task…"
              className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring resize-none"
            />
          </div>

          {/* Deadline */}
          <div className="space-y-1.5">
            <label htmlFor="adhoc-deadline" className="block text-xs font-semibold uppercase tracking-wide text-muted-foreground">
              {t.tasks.deadline}
            </label>
            <input
              id="adhoc-deadline"
              type="date"
              value={deadline}
              onChange={e => setDeadline(e.target.value)}
              className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring"
            />
          </div>

          {/* Assigned To — single-select combobox with search */}
          {users.length > 0 && (
            <div className="space-y-1.5">
              <label className="block text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                {t.tasks.assignedTo} <span className="text-destructive">*</span>
              </label>
              <div className="relative" ref={userDropdownRef}>
                {/* Trigger button */}
                <button
                  type="button"
                  onClick={() => { setUserDropdownOpen(o => !o); if (!userDropdownOpen) setUserSearch(""); }}
                  className="flex w-full items-center justify-between rounded-md border border-input bg-background px-3 py-2 text-sm text-left focus:outline-none focus:ring-2 focus:ring-ring hover:bg-accent/40 transition-colors"
                  aria-expanded={userDropdownOpen}
                  aria-haspopup="listbox"
                >
                  {selectedUser ? (
                    <div className="flex items-center gap-2 min-w-0">
                      <span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-primary/15 text-[11px] font-semibold text-primary">
                        {selectedUser.fullName.charAt(0).toUpperCase()}
                      </span>
                      <span className="font-medium text-foreground truncate">{selectedUser.fullName}</span>
                    </div>
                  ) : (
                    <span className="text-muted-foreground">Select a user…</span>
                  )}
                  <div className="flex items-center gap-1 shrink-0 ml-2">
                    {selectedUser && (
                      <span
                        role="button"
                        tabIndex={0}
                        onClick={e => { e.stopPropagation(); setAssignedUserId(null); }}
                        onKeyDown={e => { if (e.key === "Enter" || e.key === " ") { e.preventDefault(); setAssignedUserId(null); } }}
                        className="rounded p-0.5 text-muted-foreground hover:bg-destructive/10 hover:text-destructive cursor-pointer transition-colors"
                        aria-label="Clear assignment"
                      >
                        <X className="h-3.5 w-3.5" />
                      </span>
                    )}
                    <ChevronDown className={`h-4 w-4 text-muted-foreground transition-transform duration-150 ${userDropdownOpen ? "rotate-180" : ""}`} />
                  </div>
                </button>

                {/* Dropdown panel */}
                {userDropdownOpen && (
                  <div
                    className="absolute top-full left-0 right-0 z-20 mt-1 rounded-md border border-border bg-popover shadow-lg overflow-hidden"
                    role="listbox"
                  >
                    {/* Inline search */}
                    <div className="border-b border-border p-2">
                      <input
                        type="text"
                        value={userSearch}
                        onChange={e => setUserSearch(e.target.value)}
                        placeholder="Search users…"
                        autoFocus
                        className="w-full rounded border border-input bg-background px-2.5 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
                      />
                    </div>
                    <div className="max-h-48 overflow-y-auto py-1">
                      {filteredUsers.length === 0 ? (
                        <p className="px-3 py-3 text-sm text-center text-muted-foreground">No users found</p>
                      ) : (
                        filteredUsers.map(u => (
                          <button
                            key={u.id}
                            type="button"
                            role="option"
                            aria-selected={assignedUserId === u.id}
                            onClick={() => { setAssignedUserId(u.id); setUserDropdownOpen(false); setUserSearch(""); }}
                            className={`flex w-full items-center gap-2.5 px-3 py-2 text-left text-sm transition-colors hover:bg-accent ${assignedUserId === u.id ? "bg-accent/60 text-foreground" : "text-popover-foreground"}`}
                          >
                            <span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-primary/15 text-[11px] font-semibold text-primary">
                              {u.fullName.charAt(0).toUpperCase()}
                            </span>
                            <span className="flex-1 truncate">{u.fullName}</span>
                            {assignedUserId === u.id && <Check className="h-3.5 w-3.5 text-primary shrink-0" />}
                          </button>
                        ))
                      )}
                    </div>
                  </div>
                )}
              </div>
            </div>
          )}

          {/* Error */}
          {error && (
            <p className="rounded-md bg-destructive/10 px-3 py-2 text-xs text-destructive">
              {error}
            </p>
          )}

          {/* Actions */}
          <div className="flex justify-end gap-2 pt-2 border-t border-border">
            <Button type="button" variant="ghost" size="sm" onClick={onClose} disabled={saving}>
              {t.common.cancel}
            </Button>
            <Button type="submit" size="sm" disabled={saving || !title.trim() || !effectiveCompanyId || !assignedUserId}>
              {saving && <Loader2 className="mr-1.5 h-3.5 w-3.5 animate-spin" />}
              {t.tasks.createTask}
            </Button>
          </div>
        </form>
      </div>
    </div>
  );
}
