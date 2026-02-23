import { useState, useEffect, useCallback } from "react";
import { useAuth } from "@/contexts/AuthContext";
import { useTranslation } from "@/contexts/LanguageContext";
import type { Locale } from "@/i18n/types";
import type { InsolvencyFirmDto } from "@/services/api/types";
import { tribunalsApi, financeApi, localGovApi } from "@/services/api/authorities";
import type { AuthorityRecord } from "@/services/api/authorities";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import client from "@/services/api/client";
import { signingApi } from "@/services/api/signing";
import {
    Loader2, Building2, Mail, AlertCircle, Users,
    Check, Trash2, RefreshCw, Shield, Globe, Landmark,
    KeyRound, Upload, ShieldCheck,
  ChevronDown, ChevronRight, UserPlus, Copy,
    Pencil, X, Gavel, Receipt, MapPin, Download, AlertTriangle, RotateCcw,
} from "lucide-react";
import { format } from "date-fns";

type Tab = "tenant" | "firm" | "users" | "signing" | "emails" | "errors" | "permissions"
 | "tribunals" | "finance" | "localgov" | "demo";

/* ?? Tenant Settings ????????????????????????????????????? */
function TenantTab() {
    const { t, locale, setLocale } = useTranslation();
    const [data, setData] = useState<Record<string, unknown> | null>(null);
    const [name, setName] = useState("");
    const [domain, setDomain] = useState("");
    const [saving, setSaving] = useState(false);
    const [saved, setSaved] = useState(false);

    useEffect(() => {
        client.get("/settings/tenant").then(r => {
            setData(r.data);
            setName(r.data.name ?? "");
            setDomain(r.data.domain ?? "");
        }).catch(console.error);
    }, []);

    const handleSave = async () => {
        setSaving(true);
        try {
            await client.put("/settings/tenant", { name, domain });
            setSaved(true);
            setTimeout(() => setSaved(false), 2000);
        } catch (err) { console.error(err); }
        finally { setSaving(false); }
    };

    if (!data) return <Loader2 className="h-6 w-6 animate-spin text-muted-foreground mx-auto mt-8" />;

    const LANGUAGES: { code: Locale; label: string; flag: string }[] = [
        { code: "en", label: "English", flag: "🇬🇧" },
        { code: "ro", label: "Română", flag: "🇷🇴" },
        { code: "hu", label: "Magyar", flag: "🇭🇺" },
    ];

    return (
        <div className="space-y-5">
            {/* Language switcher */}
            <div className="rounded-xl border border-border bg-card p-5">
                <div className="flex items-center gap-2 mb-3">
                    <Globe className="h-4 w-4 text-primary" />
                    <h2 className="text-sm font-semibold text-foreground">{t.settings.language}</h2>
                </div>
                <p className="text-xs text-muted-foreground mb-3">{t.settings.languageDesc}</p>
                <div className="flex gap-2">
                    {LANGUAGES.map(lang => (
                        <button
                            key={lang.code}
                            onClick={() => setLocale(lang.code)}
                            className={`flex items-center gap-2 rounded-lg border px-4 py-2.5 text-sm font-medium transition-all ${locale === lang.code
                                ? "border-primary bg-primary/5 text-primary ring-1 ring-primary/20"
                                : "border-border text-foreground hover:border-primary/30 hover:bg-accent/30"
                                }`}
                        >
                            <span className="text-lg">{lang.flag}</span>
                            {lang.label}
                        </button>
                    ))}
                </div>
            </div>

            {/* Organization Settings */}
            <div className="rounded-xl border border-border bg-card p-5 space-y-4">
                <h2 className="text-sm font-semibold text-foreground">{t.settings.orgSettings}</h2>
                <div className="grid gap-4 sm:grid-cols-2">
                    <div>
                        <label className="mb-1 block text-xs font-semibold uppercase tracking-wide text-muted-foreground">{t.settings.orgName}</label>
                        <input value={name} onChange={e => setName(e.target.value)} className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm" />
                    </div>
                    <div>
                        <label className="mb-1 block text-xs font-semibold uppercase tracking-wide text-muted-foreground">{t.settings.domain}</label>
                        <input value={domain} onChange={e => setDomain(e.target.value)} className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm" placeholder={t.settings.domainPlaceholder} />
                    </div>
                </div>
                <div className="flex items-center gap-2">
                    <Button size="sm" className="bg-primary hover:bg-primary/90" onClick={handleSave} disabled={saving}>
                        {saving ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : saved ? <Check className="h-3.5 w-3.5" /> : null}
                        {saved ? t.settings.saved : t.settings.saveSettings}
                    </Button>
                </div>
            </div>

            <div className="rounded-xl border border-border bg-card p-5">
                <h2 className="text-sm font-semibold text-foreground mb-3">{t.settings.planUsage}</h2>
                <div className="grid gap-4 sm:grid-cols-3 text-sm">
                    <div><span className="text-muted-foreground">{t.settings.plan}</span><p className="font-medium">{(data.planName as string) ?? "Free"}</p></div>
                    <div><span className="text-muted-foreground">{t.settings.expires}</span><p className="font-medium">{data.subscriptionExpiry ? format(new Date(data.subscriptionExpiry as string), "dd MMM yyyy") : t.settings.never}</p></div>
                    <div><span className="text-muted-foreground">{t.common.status}</span><Badge variant={(data.isActive as boolean) ? "success" : "destructive"}>{(data.isActive as boolean) ? t.common.active : t.common.inactive}</Badge></div>
                    <div><span className="text-muted-foreground">{t.settings.users}</span><p className="font-medium">{data.userCount as number}</p></div>
                    <div><span className="text-muted-foreground">{t.dashboard.companies}</span><p className="font-medium">{data.companyCount as number}</p></div>
                    <div><span className="text-muted-foreground">{t.cases.title}</span><p className="font-medium">{data.caseCount as number}</p></div>
                </div>
            </div>
        </div>
    );
}

/* ── Users Tab ──────────────────────────────────────────── */
function UsersTab() {
    const { t } = useTranslation();
    const { isGlobalAdmin } = useAuth();
    const [users, setUsers] = useState<Array<Record<string, unknown>>>([]);
    const [invitations, setInvitations] = useState<Array<Record<string, unknown>>>([]);
    const [roles, setRoles] = useState<Array<{ value: string; label: string }>>([]);
    const [loading, setLoading] = useState(true);
    // Invite form
    const [showInviteForm, setShowInviteForm] = useState(false);
    const [inviteEmail, setInviteEmail] = useState("");
    const [inviteFirst, setInviteFirst] = useState("");
    const [inviteLast, setInviteLast] = useState("");
    const [inviteRole, setInviteRole] = useState("Practitioner");
    const [inviting, setInviting] = useState(false);
    const [inviteError, setInviteError] = useState("");
    const [inviteSuccess, setInviteSuccess] = useState("");
    const [copiedToken, setCopiedToken] = useState("");
    // Edit modal
    const [editUser, setEditUser] = useState<Record<string, unknown> | null>(null);
    const [editFirst, setEditFirst] = useState("");
    const [editLast, setEditLast] = useState("");
    const [editRole, setEditRole] = useState("");
    const [editActive, setEditActive] = useState(true);
    const [editSaving, setEditSaving] = useState(false);
    const [editError, setEditError] = useState("");
    // Password reset
    const [resetUserId, setResetUserId] = useState<string | null>(null);
    const [newPassword, setNewPassword] = useState("");
  const [resetSaving, setResetSaving] = useState(false);
    const [resetMsg, setResetMsg] = useState("");

    const load = useCallback(async () => {
        setLoading(true);
   try {
     const [usersRes, invitesRes, rolesRes] = await Promise.all([
         client.get("/users"),
        client.get("/users/invitations"),
    client.get("/users/roles"),
     ]);
      setUsers(usersRes.data);
    setInvitations(invitesRes.data);
     setRoles(rolesRes.data);
      } catch (err) { console.error(err); }
        finally { setLoading(false); }
    }, []);

    useEffect(() => { load(); }, [load]);

    const roleLabels: Record<string, string> = {
        GlobalAdmin: t.settings.roleGlobalAdmin ?? "Global Admin",
        TenantAdmin: t.settings.roleTenantAdmin ?? "Tenant Admin",
        Practitioner: t.settings.rolePractitioner ?? "Practitioner",
        Secretary: t.settings.roleSecretary ?? "Secretary",
    };

    const handleInvite = async () => {
        setInviting(true); setInviteError(""); setInviteSuccess("");
   try {
        const r = await client.post("/users/invite", { email: inviteEmail, firstName: inviteFirst, lastName: inviteLast, role: inviteRole });
         setInviteSuccess(r.data.message); setCopiedToken(r.data.token);
            setInviteEmail(""); setInviteFirst(""); setInviteLast("");
   load();
        } catch (err: any) {
  setInviteError(err?.response?.data?.message || "Failed to send invitation");
        } finally { setInviting(false); }
    };

    const openEdit = (u: Record<string, unknown>) => {
        setEditUser(u);
        setEditFirst(u.firstName as string);
        setEditLast(u.lastName as string);
        setEditRole(u.role as string);
        setEditActive(u.isActive as boolean);
        setEditError("");
    };

    const handleEditSave = async () => {
        if (!editUser) return;
     setEditSaving(true); setEditError("");
    try {
     await client.put(`/users/${editUser.id}`, {
  firstName: editFirst,
       lastName: editLast,
  role: editRole,
        isActive: editActive,
            });
    setEditUser(null);
            load();
      } catch (err: any) {
 setEditError(err?.response?.data?.message || "Failed to update user");
   } finally { setEditSaving(false); }
    };

    const handleAdminReset = async () => {
        if (!resetUserId || !newPassword) return;
        setResetSaving(true); setResetMsg("");
        try {
            await client.post(`/users/${resetUserId}/reset-password`, { newPassword });
setResetMsg(t.settings.passwordResetSuccess ?? "Password reset successfully");
  setNewPassword("");
    } catch (err: any) {
      setResetMsg(err?.response?.data?.message || "Failed to reset password");
     } finally { setResetSaving(false); }
    };

 const copyToken = (token: string) => {
     navigator.clipboard.writeText(token);
        setCopiedToken(token);
     setTimeout(() => setCopiedToken(""), 2000);
    };

    if (loading) return <Loader2 className="h-6 w-6 animate-spin text-muted-foreground mx-auto mt-8" />;

    return (
        <div className="space-y-4">
    {/* Invite Form */}
            <div className="rounded-xl border border-border bg-card">
              <div className="flex items-center justify-between px-4 py-3 border-b border-border">
        <h2 className="text-sm font-semibold text-foreground">{t.settings.inviteUser}</h2>
            <Button size="sm" variant="outline" className="text-xs gap-1" onClick={() => setShowInviteForm(!showInviteForm)}>
            <UserPlus className="h-3.5 w-3.5" />
{showInviteForm ? t.common.cancel : (t.settings.inviteUser ?? "Invite User")}
         </Button>
      </div>
         {showInviteForm && (
     <div className="px-4 py-3 space-y-3">
       <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <input placeholder={t.settings.firstName ?? "First Name"} value={inviteFirst} onChange={e => setInviteFirst(e.target.value)} className="rounded-md border border-input bg-background px-3 py-2 text-sm" />
 <input placeholder={t.settings.lastName ?? "Last Name"} value={inviteLast} onChange={e => setInviteLast(e.target.value)} className="rounded-md border border-input bg-background px-3 py-2 text-sm" />
  </div>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
        <input type="email" placeholder={t.settings.emailPlaceholder ?? "email@firm.com"} value={inviteEmail} onChange={e => setInviteEmail(e.target.value)} className="rounded-md border border-input bg-background px-3 py-2 text-sm" />
          <select value={inviteRole} onChange={e => setInviteRole(e.target.value)} className="rounded-md border border-input bg-background px-3 py-2 text-sm">
    {roles.map(r => <option key={r.value} value={r.value}>{roleLabels[r.value] ?? r.label}</option>)}
    </select>
        </div>
      {inviteError && <p className="text-xs text-destructive">{inviteError}</p>}
     {inviteSuccess && (
       <div className="rounded bg-emerald-50 dark:bg-emerald-950 border border-emerald-200 dark:border-emerald-800 p-3 space-y-2">
       <p className="text-xs text-emerald-700 dark:text-emerald-300">{inviteSuccess}</p>
       {copiedToken && (
          <div className="flex items-center gap-2">
     <code className="text-[10px] font-mono bg-muted rounded px-2 py-1 flex-1 truncate">{copiedToken}</code>
           <Button variant="ghost" size="icon" className="h-7 w-7" onClick={() => copyToken(copiedToken)}><Copy className="h-3.5 w-3.5" /></Button>
           </div>
)}
     </div>
         )}
           <Button size="sm" className="text-xs" onClick={handleInvite} disabled={inviting || !inviteEmail || !inviteFirst || !inviteLast}>
       {inviting && <Loader2 className="h-3.5 w-3.5 animate-spin mr-1" />}
          {t.settings.sendInvitation ?? "Send Invitation"}
             </Button>
     </div>
                )}
</div>

            {/* Active Users */}
            <div className="rounded-xl border border-border bg-card divide-y divide-border">
     <div className="px-4 py-3">
<h2 className="text-sm font-semibold text-foreground">{t.settings.teamMembers} ({users.length})</h2>
            </div>
     {users.map((u) => (
    <div key={u.id as string} className="flex items-center gap-3 px-4 py-2.5 hover:bg-muted/30 transition-colors cursor-pointer" onClick={() => openEdit(u)}>
 <div className="flex h-8 w-8 items-center justify-center rounded-full bg-primary/10 text-xs font-bold text-primary">
      {(u.firstName as string)?.[0]}{(u.lastName as string)?.[0]}
            </div>
    <div className="min-w-0 flex-1">
   <p className="text-sm font-medium text-foreground truncate">{u.fullName as string}</p>
       <p className="text-xs text-muted-foreground">{u.email as string}</p>
        </div>
    <Badge variant="secondary" className="text-[10px]">{roleLabels[u.role as string] ?? (u.role as string)}</Badge>
         <Badge variant={(u.isActive as boolean) ? "success" : "destructive"} className="text-[10px]">
  {(u.isActive as boolean) ? (t.common.active ?? "Active") : (t.common.inactive ?? "Inactive")}
  </Badge>
         <Pencil className="h-3.5 w-3.5 text-muted-foreground shrink-0" />
         </div>
  ))}
    </div>

            {/* Pending Invitations */}
        {invitations.length > 0 && (
    <div className="rounded-xl border border-border bg-card divide-y divide-border">
         <div className="px-4 py-3">
       <h2 className="text-sm font-semibold text-foreground">{t.settings.pendingInvitations ?? "Pending Invitations"} ({invitations.length})</h2>
    </div>
           {invitations.map(inv => (
<div key={inv.id as string} className="flex items-center gap-3 px-4 py-2.5">
        <Mail className="h-4 w-4 text-muted-foreground shrink-0" />
       <div className="min-w-0 flex-1">
   <p className="text-sm font-medium text-foreground truncate">{inv.firstName as string} {inv.lastName as string}</p>
 <p className="text-xs text-muted-foreground">{inv.email as string}</p>
       </div>
 <Badge variant="secondary" className="text-[10px]">{roleLabels[inv.role as string] ?? (inv.role as string)}</Badge>
           {(inv.isAccepted as boolean) ? (
    <Badge variant="success" className="text-[10px]">{t.common.accepted ?? "Accepted"}</Badge>
    ) : (inv.isExpired as boolean) ? (
      <Badge variant="destructive" className="text-[10px]">{t.common.expired ?? "Expired"}</Badge>
     ) : (
            <Badge variant="outline" className="text-[10px]">{t.common.pending}</Badge>
        )}
                 <span className="text-[10px] text-muted-foreground shrink-0">{format(new Date(inv.createdOn as string), "dd MMM HH:mm")}</span>
   </div>
         ))}
           </div>
          )}

        {/* Edit User Modal */}
         {editUser && (
     <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50" onClick={() => setEditUser(null)}>
             <div className="bg-card border border-border rounded-xl shadow-xl w-full max-w-md mx-4 p-5 space-y-4" onClick={e => e.stopPropagation()}>
             <div className="flex items-center justify-between">
             <h2 className="text-sm font-semibold text-foreground">{t.settings.editUser ?? "Edit User"}</h2>
       <Button variant="ghost" size="icon" className="h-7 w-7" onClick={() => setEditUser(null)}><X className="h-4 w-4" /></Button>
          </div>
    <p className="text-xs text-muted-foreground">{editUser.email as string}</p>

       <div className="space-y-3">
     <div className="grid grid-cols-2 gap-3">
         <div>
            <label className="mb-1 block text-[10px] font-semibold uppercase text-muted-foreground">{t.settings.firstName ?? "First Name"}</label>
       <input value={editFirst} onChange={e => setEditFirst(e.target.value)} className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm" />
             </div>
             <div>
  <label className="mb-1 block text-[10px] font-semibold uppercase text-muted-foreground">{t.settings.lastName ?? "Last Name"}</label>
           <input value={editLast} onChange={e => setEditLast(e.target.value)} className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm" />
    </div>
        </div>
   <div>
       <label className="mb-1 block text-[10px] font-semibold uppercase text-muted-foreground">{t.settings.role ?? "Role"}</label>
     <select value={editRole} onChange={e => setEditRole(e.target.value)} className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm">
  {roles.map(r => <option key={r.value} value={r.value}>{roleLabels[r.value] ?? r.label}</option>)}
             </select>
   </div>
      <label className="flex items-center gap-2 text-sm text-foreground cursor-pointer">
          <input type="checkbox" checked={editActive} onChange={e => setEditActive(e.target.checked)} />
    {t.common.active}
   </label>
    </div>
             {editError && <p className="text-xs text-destructive">{editError}</p>}
           <div className="flex justify-end gap-2">
        <Button variant="outline" size="sm" onClick={() => setEditUser(null)}>{t.common.cancel}</Button>
        <Button size="sm" onClick={handleEditSave} disabled={editSaving}>
        {editSaving && <Loader2 className="h-3.5 w-3.5 animate-spin mr-1" />}
         {t.common.save}
</Button>
           </div>

            {/* Admin Password Reset */}
      <div className="border-t border-border pt-3 mt-3">
          <h3 className="text-xs font-semibold text-foreground mb-2">{t.settings.adminResetPassword ?? "Reset User Password"}</h3>
      <div className="flex gap-2">
    <input
           type="password"
   placeholder={t.settings.newPassword ?? "New password"}
      value={resetUserId === (editUser.id as string) ? newPassword : ""}
           onChange={e => { setResetUserId(editUser.id as string); setNewPassword(e.target.value); }}
           className="flex-1 rounded-md border border-input bg-background px-3 py-2 text-sm"
       />
     <Button size="sm" variant="destructive" onClick={handleAdminReset} disabled={resetSaving || !newPassword || resetUserId !== (editUser.id as string)}>
             {resetSaving && <Loader2 className="h-3.5 w-3.5 animate-spin mr-1" />}
        {t.settings.resetPassword ?? "Reset"}
        </Button>
        </div>
   {resetMsg && <p className="text-xs text-muted-foreground mt-1">{resetMsg}</p>}
    </div>
     </div>
   </div>
            )}
        </div>
    );
}

/* ── Scheduled Emails Tab ───────────────────────────────── */
function EmailsTab() {
    const { t } = useTranslation();
    const [emails, setEmails] = useState<Array<Record<string, unknown>>>([]);
 const [total, setTotal] = useState(0);
    const [loading, setLoading] = useState(true);
    const [filter, setFilter] = useState<"all" | "pending" | "sent">("all");
    const [expandedId, setExpandedId] = useState<string | null>(null);

    const load = useCallback(async () => {
        setLoading(true);
  const params: Record<string, unknown> = { pageSize: 30 };
        if (filter === "pending") params.sent = false;
        if (filter === "sent") params.sent = true;
        try {
       const r = await client.get("/settings/emails", { params });
    setEmails(r.data.items);
            setTotal(r.data.total);
        } catch (err) { console.error(err); }
     finally { setLoading(false); }
    }, [filter]);

    useEffect(() => { load(); }, [load]);

    const handleDelete = async (id: string) => {
        await client.delete(`/settings/emails/${id}`);
     load();
    };

    return (
        <div className="space-y-3">
            <div className="flex items-center justify-between">
 <div className="flex rounded-lg border border-input overflow-hidden">
   {(["all", "pending", "sent"] as const).map(f => (
               <button key={f} onClick={() => setFilter(f)}
      className={`px-3 py-1.5 text-xs font-medium ${filter === f ? "bg-primary text-primary-foreground" : "bg-background text-foreground hover:bg-accent"}`}
  >{f === "all" ? t.common.all : f === "pending" ? t.common.pending : t.common.sent}</button>
     ))}
           </div>
             <div className="flex items-center gap-2">
        <span className="text-xs text-muted-foreground">{total} {t.common.total ?? "total"}</span>
      <Button variant="ghost" size="icon" className="h-7 w-7" onClick={load}>
       <RefreshCw className="h-3.5 w-3.5" />
          </Button>
       </div>
            </div>

     {loading ? <Loader2 className="h-6 w-6 animate-spin text-muted-foreground mx-auto mt-8" /> : (
      <div className="rounded-xl border border-border bg-card divide-y divide-border">
   {emails.length === 0 ? (
       <p className="px-4 py-6 text-center text-sm text-muted-foreground">{t.settings.noEmails}</p>
       ) : emails.map(e => {
      const id = e.id as string;
      const isExpanded = expandedId === id;
         return (
     <div key={id}>
           <div className="flex items-center gap-3 px-4 py-2.5 cursor-pointer hover:bg-muted/30" onClick={() => setExpandedId(isExpanded ? null : id)}>
            <div className="shrink-0">
            {isExpanded ? <ChevronDown className="h-3.5 w-3.5 text-muted-foreground" /> : <ChevronRight className="h-3.5 w-3.5 text-muted-foreground" />}
              </div>
              <Mail className="h-4 w-4 text-muted-foreground shrink-0" />
           <div className="min-w-0 flex-1">
   <p className="text-sm font-medium text-foreground truncate">{e.subject as string}</p>
                  <p className="text-xs text-muted-foreground truncate">{t.settings.emailTo ?? "To"}: {e.to as string}{e.cc ? `, CC: ${e.cc}` : ""}</p>
 </div>
     <Badge variant={(e.isSent as boolean) ? "success" : "secondary"} className="text-[10px] shrink-0">
      {(e.isSent as boolean) ? t.common.sent : t.common.pending}
 </Badge>
   <span className="text-[10px] text-muted-foreground shrink-0">
     {format(new Date((e.scheduledFor ?? e.sentAt) as string), "dd MMM HH:mm")}
      </span>
               {!(e.isSent as boolean) && (
            <Button variant="ghost" size="icon" className="h-7 w-7" onClick={(ev) => { ev.stopPropagation(); handleDelete(id); }}>
        <Trash2 className="h-3.5 w-3.5 text-destructive" />
       </Button>
      )}
        </div>
  {isExpanded && (
 <div className="px-4 pb-3 ml-10 space-y-2">
        {e.errorMessage && (
    <div className="rounded bg-destructive/10 px-3 py-1.5 text-xs text-destructive">
        {t.settings.emailError ?? "Error"}: {e.errorMessage as string} {e.retryCount ? `(${e.retryCount} ${t.common.retries ?? "retries"})` : ""}
       </div>
   )}
      <div className="rounded bg-muted/50 p-3 text-xs text-foreground whitespace-pre-wrap max-h-48 overflow-y-auto">
 {(e.body as string) || (t.settings.noBody ?? "No body")}
         </div>
    {e.sentAt && <p className="text-[10px] text-muted-foreground">{t.common.sent}: {format(new Date(e.sentAt as string), "dd MMM yyyy HH:mm")}</p>}
       </div>
           )}
            </div>
 );
            })}
           </div>
            )}
        </div>
    );
}

/* ?? Error Logs Tab ?????????????????????????????????????? */
function ErrorsTab() {
    const { t } = useTranslation();
    const [errors, setErrors] = useState<Array<Record<string, unknown>>>([]);
    const [total, setTotal] = useState(0);
    const [loading, setLoading] = useState(true);
    const [showResolved, setShowResolved] = useState(false);
    const [expandedId, setExpandedId] = useState<string | null>(null);

const load = useCallback(async () => {
        setLoading(true);
        try {
   const r = await client.get("/settings/errors", { params: { resolved: showResolved, pageSize: 30 } });
      setErrors(r.data.items);
     setTotal(r.data.total);
 } catch (err) { console.error(err); }
  finally { setLoading(false); }
    }, [showResolved]);

  useEffect(() => { load(); }, [load]);

    const handleResolve = async (id: string) => {
        await client.put(`/settings/errors/${id}/resolve`);
      load();
    };

    return (
  <div className="space-y-3">
            <div className="flex items-center justify-between">
 <div className="flex items-center gap-2">
      <label className="flex items-center gap-1.5 text-xs text-muted-foreground cursor-pointer">
<input type="checkbox" checked={showResolved} onChange={e => setShowResolved(e.target.checked)} />
    {t.settings.showResolved}
         </label>
  </div>
        <div className="flex items-center gap-2">
       <span className="text-xs text-muted-foreground">{total} {t.common.total ?? "total"}</span>
    <Button variant="ghost" size="icon" className="h-7 w-7" onClick={load}>
             <RefreshCw className="h-3.5 w-3.5" />
          </Button>
  </div>
        </div>

    {loading ? <Loader2 className="h-6 w-6 animate-spin text-muted-foreground mx-auto mt-8" /> : (
      <div className="rounded-xl border border-border bg-card divide-y divide-border">
        {errors.length === 0 ? (
    <p className="px-4 py-6 text-center text-sm text-muted-foreground">{t.settings.noErrors}</p>
           ) : errors.map(err => {
   const id = err.id as string;
     const isExpanded = expandedId === id;
   return (
          <div key={id}>
            <div className="flex items-start gap-3 px-4 py-2.5 cursor-pointer hover:bg-muted/30" onClick={() => setExpandedId(isExpanded ? null : id)}>
  <div className="mt-0.5 shrink-0">
      {isExpanded ? <ChevronDown className="h-3.5 w-3.5 text-muted-foreground" /> : <ChevronRight className="h-3.5 w-3.5 text-muted-foreground" />}
            </div>
  <AlertCircle className="h-4 w-4 text-destructive shrink-0 mt-0.5" />
          <div className="min-w-0 flex-1">
   <p className="text-sm font-medium text-foreground truncate">{err.message as string}</p>
 <p className="text-xs text-muted-foreground truncate">
       {err.source as string}
   {err.requestMethod ? ` \u00B7 ${err.requestMethod}` : ""}
         {err.requestPath ? ` ${err.requestPath}` : ""}
        </p>
    </div>
        <span className="text-[10px] text-muted-foreground shrink-0">
   {format(new Date(err.timestamp as string), "dd MMM HH:mm")}
     </span>
         {!(err.isResolved as boolean) && (
           <Button variant="ghost" size="sm" className="text-xs shrink-0 text-primary" onClick={(ev) => { ev.stopPropagation(); handleResolve(id); }}>
   {t.settings.resolve}
       </Button>
    )}
                  {(err.isResolved as boolean) && (
              <Badge variant="success" className="text-[10px] shrink-0">{t.common.resolved ?? "Resolved"}</Badge>
    )}
  </div>
          {isExpanded && (
          <div className="px-4 pb-3 ml-10 space-y-2">
     {err.stackTrace && (
             <pre className="rounded bg-muted/50 p-2 text-[10px] font-mono overflow-x-auto max-h-40 text-foreground">
            {err.stackTrace as string}
         </pre>
  )}
      {err.userEmail && <p className="text-[11px] text-muted-foreground">{t.common.user ?? "User"}: {err.userEmail as string}</p>}
          </div>
          )}
       </div>
   );
            })}
     </div>
 )}
        </div>
    );
}

/* ── Insolvency Firm Tab ──────────────────────────────── */
function FirmTab() {
    const { t } = useTranslation();
    const [_firm, setFirm] = useState<InsolvencyFirmDto | null>(null);
    const [loading, setLoading] = useState(true);
    const [saving, setSaving] = useState(false);
const [saved, setSaved] = useState(false);

    // Form fields
    const [firmName, setFirmName] = useState("");
    const [cuiRo, setCuiRo] = useState("");
    const [tradeRegisterNo, setTradeRegisterNo] = useState("");
    const [vatNumber, setVatNumber] = useState("");
    const [unpirRegistrationNo, setUnpirRegistrationNo] = useState("");
    const [unpirRfo, setUnpirRfo] = useState("");
    const [address, setAddress] = useState("");
    const [locality, setLocality] = useState("");
    const [county, setCounty] = useState("");
    const [country, setCountry] = useState("Romania");
    const [postalCode, setPostalCode] = useState("");
    const [phone, setPhone] = useState("");
    const [fax, setFax] = useState("");
    const [email, setEmail] = useState("");
    const [website, setWebsite] = useState("");
    const [contactPerson, setContactPerson] = useState("");
    const [iban, setIban] = useState("");
    const [bankName, setBankName] = useState("");
    const [secondaryIban, setSecondaryIban] = useState("");
  const [secondaryBankName, setSecondaryBankName] = useState("");

    useEffect(() => {
        client.get("/settings/firm").then(r => {
     const f = r.data as InsolvencyFirmDto | null;
            if (f) {
      setFirm(f);
  setFirmName(f.firmName ?? "");
      setCuiRo(f.cuiRo ?? "");
             setTradeRegisterNo(f.tradeRegisterNo ?? "");
                setVatNumber(f.vatNumber ?? "");
     setUnpirRegistrationNo(f.unpirRegistrationNo ?? "");
      setUnpirRfo(f.unpirRfo ?? "");
                setAddress(f.address ?? "");
    setLocality(f.locality ?? "");
                setCounty(f.county ?? "");
                setCountry(f.country ?? "Romania");
        setPostalCode(f.postalCode ?? "");
     setPhone(f.phone ?? "");
       setFax(f.fax ?? "");
             setEmail(f.email ?? "");
        setWebsite(f.website ?? "");
       setContactPerson(f.contactPerson ?? "");
     setIban(f.iban ?? "");
       setBankName(f.bankName ?? "");
       setSecondaryIban(f.secondaryIban ?? "");
           setSecondaryBankName(f.secondaryBankName ?? "");
         }
     }).catch(console.error).finally(() => setLoading(false));
    }, []);

    const handleSave = async () => {
        setSaving(true);
        try {
            const res = await client.put("/settings/firm", {
       firmName, cuiRo: cuiRo || null, tradeRegisterNo: tradeRegisterNo || null,
           vatNumber: vatNumber || null, unpirRegistrationNo: unpirRegistrationNo || null,
 unpirRfo: unpirRfo || null, address: address || null, locality: locality || null,
       county: county || null, country: country || null, postalCode: postalCode || null,
        phone: phone || null, fax: fax || null, email: email || null, website: website || null,
      contactPerson: contactPerson || null, iban: iban || null, bankName: bankName || null,
          secondaryIban: secondaryIban || null, secondaryBankName: secondaryBankName || null,
            });
 setFirm(res.data);
            setSaved(true);
            setTimeout(() => setSaved(false), 2000);
        } catch (err) { console.error(err); }
        finally { setSaving(false); }
    };

    if (loading) return <Loader2 className="h-6 w-6 animate-spin text-muted-foreground mx-auto mt-8" />;

    const inputCls = "w-full rounded-md border border-input bg-background px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring";
    const labelCls = "mb-1 block text-xs font-semibold uppercase tracking-wide text-muted-foreground";

    return (
        <div className="space-y-5">
    <div className="rounded-xl border border-border bg-card p-5">
   <div className="flex items-center gap-2 mb-1">
        <Landmark className="h-4 w-4 text-primary" />
    <h2 className="text-sm font-semibold text-foreground">{t.firm.title}</h2>
                </div>
          <p className="text-xs text-muted-foreground mb-4">{t.firm.description}</p>

                <div className="grid gap-4 sm:grid-cols-2">
            <div className="sm:col-span-2">
             <label className={labelCls}>{t.firm.firmName} *</label>
         <input value={firmName} onChange={e => setFirmName(e.target.value)} required className={inputCls} />
             </div>
              <div>
    <label className={labelCls}>{t.companies.cuiRo}</label>
            <input value={cuiRo} onChange={e => setCuiRo(e.target.value)} className={inputCls} placeholder="RO12345678" />
        </div>
         <div>
    <label className={labelCls}>{t.companies.tradeRegisterNo}</label>
          <input value={tradeRegisterNo} onChange={e => setTradeRegisterNo(e.target.value)} className={inputCls} />
     </div>
    <div>
            <label className={labelCls}>{t.companies.vatNumber}</label>
            <input value={vatNumber} onChange={e => setVatNumber(e.target.value)} className={inputCls} />
   </div>
         <div>
           <label className={labelCls}>{t.firm.unpirRegistrationNo}</label>
            <input value={unpirRegistrationNo} onChange={e => setUnpirRegistrationNo(e.target.value)} className={inputCls} placeholder="RFO II-0999" />
     </div>
         <div>
        <label className={labelCls}>{t.firm.unpirRfo}</label>
   <input value={unpirRfo} onChange={e => setUnpirRfo(e.target.value)} className={inputCls} />
      </div>
   <div className="sm:col-span-2">
    <label className={labelCls}>{t.companies.address}</label>
   <input value={address} onChange={e => setAddress(e.target.value)} className={inputCls} />
      </div>
               <div>
             <label className={labelCls}>{t.companies.locality}</label>
      <input value={locality} onChange={e => setLocality(e.target.value)} className={inputCls} />
      </div>
         <div>
            <label className={labelCls}>{t.companies.county}</label>
      <input value={county} onChange={e => setCounty(e.target.value)} className={inputCls} />
    </div>
    <div>
    <label className={labelCls}>{t.companies.country}</label>
               <input value={country} onChange={e => setCountry(e.target.value)} className={inputCls} />
       </div>
     <div>
  <label className={labelCls}>{t.companies.postalCode}</label>
  <input value={postalCode} onChange={e => setPostalCode(e.target.value)} className={inputCls} />
        </div>
       </div>
            </div>

            <div className="rounded-xl border border-border bg-card p-5">
   <h2 className="text-sm font-semibold text-foreground mb-3">{t.companies.contactSection}</h2>
         <div className="grid gap-4 sm:grid-cols-2">
           <div>
               <label className={labelCls}>{t.companies.phone}</label>
       <input value={phone} onChange={e => setPhone(e.target.value)} className={inputCls} placeholder="+40 xxx xxx xxx" />
    </div>
    <div>
            <label className={labelCls}>{t.firm.fax}</label>
  <input value={fax} onChange={e => setFax(e.target.value)} className={inputCls} />
</div>
      <div>
     <label className={labelCls}>{t.companies.email}</label>
              <input value={email} onChange={e => setEmail(e.target.value)} className={inputCls} type="email" />
  </div>
<div>
        <label className={labelCls}>{t.firm.website}</label>
         <input value={website} onChange={e => setWebsite(e.target.value)} className={inputCls} placeholder="https://" />
          </div>
   <div>
  <label className={labelCls}>{t.companies.contactPerson}</label>
      <input value={contactPerson} onChange={e => setContactPerson(e.target.value)} className={inputCls} />
       </div>
 <div>
                 <label className={labelCls}>{t.companies.iban}</label>
            <input value={iban} onChange={e => setIban(e.target.value)} className={inputCls} placeholder="RO49AAAA1B31007593840000" />
         </div>
         <div>
               <label className={labelCls}>{t.companies.bankName}</label>
        <input value={bankName} onChange={e => setBankName(e.target.value)} className={inputCls} />
           </div>
       <div>
          <label className={labelCls}>{t.firm.secondaryIban}</label>
             <input value={secondaryIban} onChange={e => setSecondaryIban(e.target.value)} className={inputCls} />
 </div>
   <div>
        <label className={labelCls}>{t.firm.secondaryBankName}</label>
    <input value={secondaryBankName} onChange={e => setSecondaryBankName(e.target.value)} className={inputCls} />
      </div>
 </div>
  </div>

     <div className="flex items-center gap-2">
            <Button className="bg-primary hover:bg-primary/90" onClick={handleSave} disabled={saving || !firmName}>
       {saving ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : saved ? <Check className="h-3.5 w-3.5" /> : null}
      {saved ? t.settings.saved : t.firm.saveFirm}
       </Button>
   </div>
        </div>
    );
}

/* ── Signing Tab ─────────────────────────────────────── */
function SigningTab() {
  const { t } = useTranslation();
  const [keys, setKeys] = useState<Array<Record<string, unknown>>>([]);
    const [signatures, setSignatures] = useState<Array<Record<string, unknown>>>([]);
    const [loading, setLoading] = useState(true);
    const [uploading, setUploading] = useState(false);
    const [password, setPassword] = useState("");
    const [keyName, setKeyName] = useState("");
    const [file, setFile] = useState<File | null>(null);
    const [error, setError] = useState("");
    const [success, setSuccess] = useState("");

    const loadKeys = useCallback(async () => {
        setLoading(true);
        try {
  const [keysRes, sigsRes] = await Promise.all([
      signingApi.getMyKeys(),
              signingApi.getMySignatures(),
    ]);
setKeys(keysRes.data);
            setSignatures(sigsRes.data);
        } catch (err) { console.error(err); }
        finally { setLoading(false); }
    }, []);

    useEffect(() => { loadKeys(); }, [loadKeys]);

    const handleUpload = async () => {
        if (!file || !password) return;
 setUploading(true); setError(""); setSuccess("");
        try {
    await signingApi.uploadKey(file, password, keyName || undefined);
      setSuccess(t.settings.keyUploaded ?? "Signing key uploaded successfully");
      setFile(null); setPassword(""); setKeyName("");
            loadKeys();
        } catch (err: any) {
            setError(err?.response?.data?.message || "Failed to upload signing key");
        } finally { setUploading(false); }
};

    const handleDeactivate = async (id: string) => {
    try {
          await signingApi.deactivateKey(id);
        loadKeys();
  } catch (err) { console.error(err); }
    };

    return (
        <div className="space-y-4">
        {/* Upload form */}
        <div className="rounded-xl border border-border bg-card p-4 space-y-3">
       <h2 className="text-sm font-semibold text-foreground">{t.settings.uploadKey}</h2>
         <p className="text-xs text-muted-foreground">{t.settings.signingDesc}</p>

      {error && <p className="text-xs text-destructive">{error}</p>}
                {success && <p className="text-xs text-emerald-600 dark:text-emerald-400">{success}</p>}

       <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
        <div>
       <label className="mb-1 block text-xs font-semibold uppercase tracking-wide text-muted-foreground">{t.settings.pfxFile ?? "PFX Certificate"}</label>
   <input
     type="file"
     accept=".pfx,.p12"
  onChange={e => setFile(e.target.files?.[0] ?? null)}
          className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm file:mr-2 file:rounded file:border-0 file:bg-primary/10 file:px-2 file:py-1 file:text-xs file:font-medium file:text-primary"
    />
        </div>
        <div>
         <label className="mb-1 block text-xs font-semibold uppercase tracking-wide text-muted-foreground">{t.settings.certificatePassword ?? "Certificate Password"}</label>
        <input
    type="password"
          value={password}
   onChange={e => setPassword(e.target.value)}
      className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
    placeholder={t.settings.pfxPasswordPlaceholder ?? "PFX password"}
    />
              </div>
              <div className="sm:col-span-2">
     <label className="mb-1 block text-xs font-semibold uppercase tracking-wide text-muted-foreground">{t.settings.keyNameLabel ?? "Key Name (optional)"}</label>
         <input
  value={keyName}
   onChange={e => setKeyName(e.target.value)}
         className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
        placeholder={t.settings.keyNamePlaceholder ?? "e.g. My Company Signing Certificate 2025"}
     />
                </div>
                </div>

        <Button size="sm" className="bg-primary hover:bg-primary/90 gap-1" onClick={handleUpload} disabled={uploading || !file || !password}>
         {uploading ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Upload className="h-3.5 w-3.5" />}
{t.settings.uploadKey}
     </Button>
    </div>

        {/* Existing keys */}
   <div className="rounded-xl border border-border bg-card divide-y divide-border">
         <div className="flex items-center justify-between px-4 py-3">
 <h2 className="text-sm font-semibold text-foreground">{t.settings.yourSigningKeys ?? "Your Signing Keys"}</h2>
        <Button variant="ghost" size="icon" className="h-7 w-7" onClick={loadKeys}><RefreshCw className="h-3.5 w-3.5" /></Button>
       </div>
         {loading ? (
    <div className="p-6 text-center"><Loader2 className="h-5 w-5 animate-spin mx-auto text-muted-foreground" /></div>
            ) : keys.length === 0 ? (
       <p className="px-4 py-6 text-center text-sm text-muted-foreground">{t.settings.noKeys}</p>
             ) : keys.map(k => (
        <div key={k.id as string} className="flex items-center gap-3 px-4 py-3">
        <KeyRound className={`h-4 w-4 shrink-0 ${(k.isActive as boolean) ? "text-green-500" : "text-muted-foreground"}`} />
         <div className="min-w-0 flex-1">
         <p className="text-sm font-medium text-foreground truncate">{k.name as string}</p>
           <p className="text-xs text-muted-foreground truncate">
            {k.subjectName as string} \u00B7 Thumbprint: {(k.thumbprint as string)?.slice(0, 16)}\u2026
        </p>
         <p className="text-[10px] text-muted-foreground">
   {t.settings.validRange ?? "Valid"}: {format(new Date(k.validFrom as string), "dd MMM yyyy")} \u2192 {format(new Date(k.validTo as string), "dd MMM yyyy")}
 {k.lastUsedAt && ` \u00B7 ${t.settings.lastUsed ?? "Last used"}: ${format(new Date(k.lastUsedAt as string), "dd MMM yyyy HH:mm")}`}
      </p>
     </div>
     <div className="flex items-center gap-2 shrink-0">
         {(k.isExpired as boolean) && <Badge variant="destructive" className="text-[10px]">{t.common.expired}</Badge>}
      {(k.isActive as boolean) ? (
    <Badge variant="success" className="text-[10px]">{t.common.active}</Badge>
             ) : (
  <Badge variant="secondary" className="text-[10px]">{t.common.inactive}</Badge>
       )}
   {(k.isActive as boolean) && (
             <Button variant="ghost" size="icon" className="h-7 w-7" onClick={() => handleDeactivate(k.id as string)} title={t.settings.deactivateKey ?? "Deactivate"}>
    <Trash2 className="h-3.5 w-3.5 text-destructive" />
         </Button>
           )}
    </div>
      </div>
 ))}
     </div>

         {/* My Recent Signatures */}
            {signatures.length > 0 && (
    <div className="rounded-xl border border-border bg-card divide-y divide-border">
        <div className="px-4 py-3">
 <h2 className="text-sm font-semibold text-foreground">{t.settings.recentSignatures ?? "My Recent Signatures"}</h2>
     </div>
         {signatures.map(sig => (
    <div key={sig.id as string} className="flex items-center gap-3 px-4 py-2.5">
     <ShieldCheck className="h-4 w-4 text-green-500 shrink-0" />
   <div className="min-w-0 flex-1">
      <p className="text-sm font-medium text-foreground truncate">{sig.documentName as string || sig.documentId as string}</p>
          <p className="text-xs text-muted-foreground truncate">
           {sig.reason as string || (t.settings.noReason ?? "No reason")}
  </p>
         </div>
       <Badge variant={sig.isValid ? "success" : "destructive"} className="text-[10px]">
           {sig.isValid ? (t.settings.validSignature ?? "Valid") : (t.settings.invalidSignature ?? "Invalid")}
     </Badge>
          <span className="text-[10px] text-muted-foreground shrink-0">
   {format(new Date(sig.signedAt as string), "dd MMM yyyy HH:mm")}
     </span>
        </div>
             ))}
         </div>
      )}
        </div>
    );
}

/* ── Authority Management Tab (reusable) ──────────────── */
function AuthorityTab({ api, entityLabel, fields }: {
    api: typeof tribunalsApi;
    entityLabel: string;
    fields: { key: string; label: string; wide?: boolean }[];
}) {
    const { t } = useTranslation();
    const { isGlobalAdmin } = useAuth();
    const [items, setItems] = useState<AuthorityRecord[]>([]);
    const [loading, setLoading] = useState(true);
    const [csvFile, setCsvFile] = useState<File | null>(null);
    const [importing, setImporting] = useState(false);
    const [importResult, setImportResult] = useState<{ imported: number; errors: string[] } | null>(null);
    // Edit modal
 const [editing, setEditing] = useState<AuthorityRecord | null | undefined>(undefined); // undefined=closed, null=create
    const [form, setForm] = useState<Record<string, string>>({});
    const [saving, setSaving] = useState(false);
    const [formError, setFormError] = useState("");

    const load = useCallback(async () => {
      setLoading(true);
        try { const r = await api.getAll(); setItems(r.data); }
     catch (err) { console.error(err); }
finally { setLoading(false); }
    }, [api]);

    useEffect(() => { load(); }, [load]);

    const openEdit = (item: AuthorityRecord | null) => {
        setEditing(item);
        const f: Record<string, string> = {};
        fields.forEach(fd => { f[fd.key] = (item as Record<string, unknown>)?.[fd.key] as string || ""; });
        setForm(f);
    setFormError("");
    };

    const handleSave = async () => {
   setSaving(true); setFormError("");
        try {
            if (editing) {
      await api.update(editing.id, form);
 } else {
          await api.create(form);
      }
   setEditing(undefined);
            load();
  } catch (err: unknown) {
     const msg = (err as Record<string, Record<string, string>>)?.response?.data?.message;
 setFormError(msg || "Failed to save");
      } finally { setSaving(false); }
    };

    const handleDelete = async (id: string) => {
        try { await api.delete(id); load(); }
      catch (err) { console.error(err); }
    };

    const handleImport = async () => {
        if (!csvFile) return;
   setImporting(true); setImportResult(null);
        try {
      const r = await api.importCsv(csvFile);
   setImportResult(r.data);
   setCsvFile(null);
        load();
        } catch (err) { console.error(err); }
  finally { setImporting(false); }
    };

  const handleExport = () => {
        const token = localStorage.getItem("authToken");
        const tenantId = localStorage.getItem("selectedTenantId");
        const baseUrl = (import.meta.env.VITE_API_URL || "/api");
        const url = `${baseUrl}${api.exportCsvUrl}`;
        const a = document.createElement("a");
     a.href = url;
 // For auth headers we need fetch
  fetch(url, {
            headers: {
  ...(token ? { Authorization: `Bearer ${token}` } : {}),
       ...(tenantId ? { "X-Tenant-Id": tenantId } : {}),
      },
        }).then(r => r.blob()).then(blob => {
     const u = URL.createObjectURL(blob);
    a.href = u;
            a.download = `${entityLabel}_export.csv`;
   a.click();
 URL.revokeObjectURL(u);
 });
    };

    return (
        <div className="space-y-4">
   {/* CSV Import */}
            <div className="rounded-xl border border-border bg-card p-4 space-y-3">
         <div className="flex items-center justify-between">
   <h2 className="text-sm font-semibold text-foreground">{entityLabel}</h2>
      <div className="flex gap-2">
        <Button variant="outline" size="sm" className="gap-1 text-xs" onClick={handleExport}>
      <Download className="h-3.5 w-3.5" />{t.common.export ?? "Export CSV"}
  </Button>
       <Button size="sm" className="gap-1 text-xs" onClick={() => openEdit(null)}>
  <Pencil className="h-3.5 w-3.5" />{t.common.create}
  </Button>
       </div>
  </div>

           <div className="flex items-center gap-2">
  <input type="file" accept=".csv" onChange={e => setCsvFile(e.target.files?.[0] ?? null)}
  className="flex-1 rounded-md border border-input bg-background px-3 py-2 text-sm file:mr-2 file:rounded file:border-0 file:bg-primary/10 file:px-2 file:py-1 file:text-xs file:font-medium file:text-primary" />
    <Button size="sm" className="gap-1" onClick={handleImport} disabled={importing || !csvFile}>
   {importing ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Upload className="h-3.5 w-3.5" />}
    {t.common.import ?? "Import CSV"}
   </Button>
       </div>

   {importResult && (
 <div className="rounded bg-emerald-50 dark:bg-emerald-950 border border-emerald-200 dark:border-emerald-800 p-2 text-xs">
          <p className="text-emerald-700 dark:text-emerald-300">{t.common.imported ?? "Imported"}: {importResult.imported}</p>
          {importResult.errors.length > 0 && (
            <p className="text-destructive mt-1">{importResult.errors.length} {t.common.errorsOccurred ?? "errors"}</p>
   )}
  </div>
  )}
         </div>

   {/* Records Table */}
            <div className="rounded-xl border border-border bg-card divide-y divide-border">
          <div className="flex items-center justify-between px-4 py-3">
 <h2 className="text-sm font-semibold text-foreground">{items.length} {t.common.records ?? "records"}</h2>
   <Button variant="ghost" size="icon" className="h-7 w-7" onClick={load}><RefreshCw className="h-3.5 w-3.5" /></Button>
 </div>
    {loading ? (
          <div className="p-6 text-center"><Loader2 className="h-5 w-5 animate-spin mx-auto text-muted-foreground" /></div>
            ) : items.length === 0 ? (
 <p className="px-4 py-6 text-center text-sm text-muted-foreground">{t.common.noData ?? "No data yet."}</p>
     ) : items.map(item => (
      <div key={item.id} className="flex items-center gap-3 px-4 py-2.5 hover:bg-muted/30 transition-colors">
          <div className="min-w-0 flex-1">
 <p className="text-sm font-medium text-foreground truncate">{item.name}</p>
      <p className="text-xs text-muted-foreground truncate">
   {item.county && `${item.county}`}{item.locality && ` · ${item.locality}`}
          {item.phone && ` · ${item.phone}`}{item.email && ` · ${item.email}`}
           {(item as Record<string, unknown>).registryPhone && ` · ${(item as Record<string, unknown>).registryPhone}`}
               {(item as Record<string, unknown>).registryEmail && ` · ${(item as Record<string, unknown>).registryEmail}`}
  </p>
  </div>
            <Badge variant={item.isGlobal ? "outline" : "secondary"} className="text-[10px] shrink-0">
 {item.isGlobal ? (t.authorities?.global ?? "Global") : (t.authorities?.override ?? "Custom")}
 </Badge>
        <Button variant="ghost" size="icon" className="h-7 w-7 shrink-0" onClick={() => openEdit(item)}>
     <Pencil className="h-3.5 w-3.5" />
  </Button>
 {(isGlobalAdmin || item.isTenantOverride) && (
 <Button variant="ghost" size="icon" className="h-7 w-7 shrink-0" onClick={() => handleDelete(item.id)}>
       <Trash2 className="h-3.5 w-3.5 text-destructive" />
          </Button>
   )}
      </div>
 ))}
  </div>

     {/* Edit/Create Modal */}
 {editing !== undefined && (
      <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50" onClick={() => setEditing(undefined)}>
    <div className="bg-card border border-border rounded-xl shadow-xl w-full max-w-lg mx-4 p-5 space-y-4 max-h-[80vh] overflow-y-auto" onClick={e => e.stopPropagation()}>
   <div className="flex items-center justify-between">
       <h2 className="text-sm font-semibold text-foreground">
       {editing ? (t.common.edit ?? "Edit") : (t.common.create ?? "Create")} {entityLabel}
    </h2>
     <Button variant="ghost" size="icon" className="h-7 w-7" onClick={() => setEditing(undefined)}><X className="h-4 w-4" /></Button>
       </div>
     <div className="grid gap-3 sm:grid-cols-2">
         {fields.map(f => (
         <div key={f.key} className={f.wide ? "sm:col-span-2" : ""}>
     <label className="mb-1 block text-[10px] font-semibold uppercase text-muted-foreground">{f.label}</label>
   <input value={form[f.key] || ""} onChange={e => setForm({ ...form, [f.key]: e.target.value })}
   className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm" />
 </div>
          ))}
     </div>
          {formError && <p className="text-xs text-destructive">{formError}</p>}
     <div className="flex justify-end gap-2">
       <Button variant="outline" size="sm" onClick={() => setEditing(undefined)}>{t.common.cancel}</Button>
         <Button size="sm" onClick={handleSave} disabled={saving || !form.name?.trim()}>
  {saving && <Loader2 className="h-3.5 w-3.5 animate-spin mr-1" />}
          {t.common.save}
 </Button>
       </div>
 </div>
         </div>
            )}
     </div>
    );
}

/* ── Demo Tab ──────────────────────────────────────────── */
function DemoTab() {
    const { t } = useTranslation();
    const [resetting, setResetting] = useState(false);
    const [confirmed, setConfirmed] = useState(false);
    const [result, setResult] = useState("");

    const handleReset = async () => {
     if (!confirmed) return;
        setResetting(true); setResult("");
        try {
   const r = await client.post("/settings/demo/reset");
     setResult(r.data.message || (t.settings?.demoResetSuccess ?? "Demo data reset successfully"));
         setConfirmed(false);
        } catch (err: unknown) {
            const msg = (err as Record<string, Record<string, string>>)?.response?.data?.message;
            setResult(msg || "Failed to reset demo data");
     } finally { setResetting(false); }
    };

    return (
        <div className="space-y-4">
   <div className="rounded-xl border border-destructive/50 bg-destructive/5 p-5 space-y-4">
      <div className="flex items-center gap-2">
    <AlertTriangle className="h-5 w-5 text-destructive" />
           <h2 className="text-sm font-semibold text-destructive">{t.settings?.demoReset ?? "Reset Demo Data"}</h2>
        </div>
  <p className="text-xs text-muted-foreground">
           {t.settings?.demoResetDesc ?? "This will permanently delete ALL cases, companies, documents, tasks, parties, and phases for the current tenant. Users and tenant settings will be preserved. This action cannot be undone."}
     </p>
      <label className="flex items-center gap-2 text-sm text-foreground cursor-pointer">
                <input type="checkbox" checked={confirmed} onChange={e => setConfirmed(e.target.checked)} />
     {t.settings?.demoResetConfirm ?? "I understand this will delete all data permanently"}
   </label>
          <Button variant="destructive" size="sm" className="gap-1.5" onClick={handleReset} disabled={resetting || !confirmed}>
       {resetting ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <RotateCcw className="h-3.5 w-3.5" />}
       {t.settings?.demoResetButton ?? "Reset All Demo Data"}
      </Button>
  {result && <p className="text-xs text-muted-foreground">{result}</p>}
    </div>
        </div>
    );
}

/* ── Permissions Tab ────────────────────────────────────── */
function PermissionsTab() {
    const { t } = useTranslation();
    const { user } = useAuth();
    const [perms, setPerms] = useState<{ role: string; permissions: string[]; permissionCount: number } | null>(null);
    const [loading, setLoading] = useState(true);

    useEffect(() => {
 client.get("/users/my-permissions")
            .then(r => setPerms(r.data))
            .catch(console.error)
    .finally(() => setLoading(false));
    }, []);

    if (loading) return <Loader2 className="h-6 w-6 animate-spin text-muted-foreground mx-auto mt-8" />;
    if (!perms) return <p className="text-sm text-muted-foreground">{t.settings.noAccess ?? "Unable to load permissions"}</p>;

    const roleLabels: Record<string, string> = {
        GlobalAdmin: t.settings.roleGlobalAdmin ?? "Global Admin",
      TenantAdmin: t.settings.roleTenantAdmin ?? "Tenant Admin",
        Practitioner: t.settings.rolePractitioner ?? "Practitioner",
        Secretary: t.settings.roleSecretary ?? "Secretary",
    };

    // Group permissions by prefix (e.g. "Case", "Document", "Task")
    const groups: Record<string, string[]> = {};
    for (const p of perms.permissions) {
        const prefix = p.replace(/View|Create|Edit|Delete|Upload|Download|Manage|Generate|Advance|Initialize|Verify|Sign|Invite|Deactivate|Resolve|Reset$/, "");
        const key = prefix || "Other";
      if (!groups[key]) groups[key] = [];
     groups[key].push(p);
    }

 return (
        <div className="space-y-4">
            <div className="rounded-xl border border-border bg-card p-4">
  <div className="flex items-center gap-3 mb-3">
      <div className="flex h-10 w-10 items-center justify-center rounded-full bg-primary/10">
      <ShieldCheck className="h-5 w-5 text-primary" />
        </div>
   <div>
         <p className="text-sm font-semibold text-foreground">{user?.email}</p>
              <p className="text-xs text-muted-foreground">
          {t.settings.role ?? "Role"}: <Badge variant="secondary" className="text-[10px] ml-1">{roleLabels[perms.role] ?? perms.role}</Badge>
            </p>
        </div>
   </div>
     <p className="text-xs text-muted-foreground">
   {perms.permissionCount} {t.settings.permissionsGranted ?? "permissions granted"}
      </p>
  </div>

            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
    {Object.entries(groups).sort((a, b) => a[0].localeCompare(b[0])).map(([group, permList]) => (
          <div key={group} className="rounded-lg border border-border bg-card p-3">
       <h3 className="text-xs font-semibold text-foreground mb-2">{group}</h3>
     <div className="flex flex-wrap gap-1">
           {permList.map(p => (
  <Badge key={p} variant="outline" className="text-[9px] font-mono">
      {p.replace(group, "")}
      </Badge>
    ))}

             </div>
   </div>
  ))}

    </div>
        </div>
    );
}

/* ── Settings Page ─────────────────────────────────────── */
export default function SettingsPage() {
    const { isGlobalAdmin, isTenantAdmin } = useAuth();
    const { t } = useTranslation();
    const [tab, setTab] = useState<Tab>("tenant");

    const TABS: { id: Tab; label: string; icon: React.ElementType }[] = [
        { id: "tenant", label: t.settings.organization, icon: Building2 },
        { id: "firm", label: t.firm.title, icon: Landmark },
        { id: "users", label: t.settings.users, icon: Users },
        { id: "signing", label: t.settings.signing, icon: KeyRound },
        { id: "tribunals", label: t.authorities?.tribunals ?? "Tribunals", icon: Gavel },
        { id: "finance", label: t.authorities?.finance ?? "ANAF", icon: Receipt },
      { id: "localgov", label: t.authorities?.localGov ?? "Local Gov", icon: MapPin },
        { id: "emails", label: t.settings.scheduledEmails, icon: Mail },
        { id: "errors", label: t.settings.errorLogs, icon: AlertCircle },
  { id: "permissions", label: t.settings.permissions ?? "Permissions", icon: ShieldCheck },
  ...(isGlobalAdmin ? [{ id: "demo" as Tab, label: t.settings?.demoReset ?? "Demo", icon: RotateCcw }] : []),
  ];

    if (!isGlobalAdmin && !isTenantAdmin) {
        return (
            <div className="flex flex-col items-center justify-center h-full text-muted-foreground">
                <Shield className="h-12 w-12 mb-3 opacity-30" />
                <p className="text-sm">{t.settings.noAccess}</p>
            </div>
        );
    }

    return (
        <div className="mx-auto max-w-5xl">
            <h1 className="text-xl font-bold text-foreground mb-5">{t.settings.title}</h1>

            {/* Tab bar */}
            <div className="mb-5 flex gap-1 rounded-lg border border-border bg-card p-1 overflow-x-auto">
                {TABS.map(tb => (
                    <button
                        key={tb.id}
                        onClick={() => setTab(tb.id)}
                        className={`flex items-center gap-1.5 rounded-md px-3 py-1.5 text-xs font-medium transition-colors whitespace-nowrap
      ${tab === tb.id ? "bg-primary text-primary-foreground" : "text-muted-foreground hover:bg-accent hover:text-foreground"}`}
          >
                        <tb.icon className="h-3.5 w-3.5" />
                        {tb.label}
                    </button>
                ))}
            </div>

            {tab === "tenant" && <TenantTab />}
            {tab === "firm" && <FirmTab />}
            {tab === "users" && <UsersTab />}
            {tab === "signing" && <SigningTab />}
            {tab === "tribunals" && (
         <AuthorityTab api={tribunalsApi} entityLabel={t.authorities?.tribunals ?? "Tribunals"} fields={[
       { key: "name", label: t.common.name ?? "Name", wide: true },
          { key: "section", label: t.authorities?.section ?? "Section" },
   { key: "county", label: t.companies?.county ?? "County" },
 { key: "locality", label: t.companies?.locality ?? "Locality" },
       { key: "address", label: t.companies?.address ?? "Address", wide: true },
                 { key: "postalCode", label: t.companies?.postalCode ?? "Postal Code" },
    { key: "registryPhone", label: t.authorities?.registryPhone ?? "Registry Phone" },
                  { key: "registryFax", label: t.authorities?.registryFax ?? "Registry Fax" },
               { key: "registryEmail", label: t.authorities?.registryEmail ?? "Registry Email" },
    { key: "registryHours", label: t.authorities?.registryHours ?? "Registry Hours" },
   { key: "website", label: t.firm?.website ?? "Website" },
    { key: "contactPerson", label: t.companies?.contactPerson ?? "Contact Person" },
            { key: "notes", label: t.common?.notes ?? "Notes", wide: true },
    ]} />
     )}
            {tab === "finance" && (
   <AuthorityTab api={financeApi} entityLabel={t.authorities?.finance ?? "ANAF"} fields={[
         { key: "name", label: t.common.name ?? "Name", wide: true },
        { key: "county", label: t.companies?.county ?? "County" },
   { key: "locality", label: t.companies?.locality ?? "Locality" },
      { key: "address", label: t.companies?.address ?? "Address", wide: true },
         { key: "postalCode", label: t.companies?.postalCode ?? "Postal Code" },
     { key: "phone", label: t.companies?.phone ?? "Phone" },
                { key: "fax", label: t.firm?.fax ?? "Fax" },
          { key: "email", label: t.companies?.email ?? "Email" },
  { key: "website", label: t.firm?.website ?? "Website" },
    { key: "contactPerson", label: t.companies?.contactPerson ?? "Contact Person" },
      { key: "scheduleHours", label: t.authorities?.scheduleHours ?? "Schedule Hours" },
           { key: "notes", label: t.common?.notes ?? "Notes", wide: true },
  ]} />
      )}
  {tab === "localgov" && (
 <AuthorityTab api={localGovApi} entityLabel={t.authorities?.localGov ?? "Local Government"} fields={[
    { key: "name", label: t.common.name ?? "Name", wide: true },
     { key: "county", label: t.companies?.county ?? "County" },
            { key: "locality", label: t.companies?.locality ?? "Locality" },
       { key: "address", label: t.companies?.address ?? "Address", wide: true },
             { key: "postalCode", label: t.companies?.postalCode ?? "Postal Code" },
       { key: "phone", label: t.companies?.phone ?? "Phone" },
       { key: "fax", label: t.firm?.fax ?? "Fax" },
    { key: "email", label: t.companies?.email ?? "Email" },
     { key: "website", label: t.firm?.website ?? "Website" },
      { key: "contactPerson", label: t.companies?.contactPerson ?? "Contact Person" },
        { key: "scheduleHours", label: t.authorities?.scheduleHours ?? "Schedule Hours" },
       { key: "notes", label: t.common?.notes ?? "Notes", wide: true },
     ]} />
      )}
            {tab === "emails" && <EmailsTab />}
          {tab === "errors" && <ErrorsTab />}
            {tab === "permissions" && <PermissionsTab />}
    {tab === "demo" && isGlobalAdmin && <DemoTab />}
     </div>
    );
}
