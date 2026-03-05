/**
 * OrganisationSettingsPage
 * Combines:
 *  - Language switcher
 *  - Tenant (workspace) name / domain
 *  - Insolvency firm details — with ONRC autocomplete to pre-fill from national registry
 *  - Plan / usage stats
 */
import { useState, useEffect, useRef } from "react";
import { useAuth } from "@/contexts/AuthContext";
import { useTranslation } from "@/contexts/LanguageContext";
import type { Locale } from "@/i18n/types";
import type { InsolvencyFirmDto } from "@/services/api/types";
import { onrcApi } from "@/services/api/onrc";
import type { ONRCFirmResult } from "@/services/api/onrc";
import client from "@/services/api/client";
import AddressSearch from "@/components/AddressSearch";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import {
  Loader2, Globe, Building2, Landmark, Check,
  Search, X, CreditCard, Users, Briefcase,
} from "lucide-react";
import { format } from "date-fns";

// ── ONRC Firm Search ─────────────────────────────────────────────────────────

interface ONRCFirmSearchProps {
  onSelect: (r: ONRCFirmResult) => void;
}

function ONRCFirmSearch({ onSelect }: ONRCFirmSearchProps) {
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<ONRCFirmResult[]>([]);
  const [loading, setLoading] = useState(false);
  const [open, setOpen] = useState(false);
  const [expanded, setExpanded] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (!containerRef.current?.contains(e.target as Node)) setOpen(false);
    };
    document.addEventListener("mousedown", handler);
    return () => document.removeEventListener("mousedown", handler);
  }, []);

  const doSearch = async () => {
    if (query.trim().length < 2) return;
    setLoading(true);
    onrcApi.search(query.trim(), "Romania", 10)
      .then(r => { setResults(r.data); setOpen(r.data.length > 0); })
      .catch(console.error)
      .finally(() => setLoading(false));
  };

  const select = (r: ONRCFirmResult) => {
    onSelect(r);
    setQuery("");
    setResults([]);
    setOpen(false);
    setExpanded(false);
  };

  if (!expanded) {
    return (
      <button
        type="button"
        onClick={() => setExpanded(true)}
        className="flex items-center gap-2 text-sm text-primary hover:underline"
      >
        <Search className="h-3.5 w-3.5" />
        Search ONRC to auto-fill firm details…
      </button>
    );
  }

  return (
    <div ref={containerRef} className="relative">
      <div className="rounded-lg border border-dashed border-primary/50 bg-primary/5 p-3 space-y-2">
        <div className="flex items-center justify-between">
          <p className="text-xs font-medium text-primary flex items-center gap-1.5">
            <Building2 className="h-3.5 w-3.5" />
            Search national registry (ONRC) to pre-fill firm details
          </p>
          <button type="button" onClick={() => { setExpanded(false); setQuery(""); setResults([]); setOpen(false); }}
            className="text-muted-foreground hover:text-foreground">
            <X className="h-3.5 w-3.5" />
          </button>
        </div>

        <div className="relative">
          <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-muted-foreground pointer-events-none" />
          <input
            autoFocus
            value={query}
            onChange={e => { setQuery(e.target.value); setResults([]); setOpen(false); }}
            onKeyDown={e => e.key === "Enter" && (e.preventDefault(), doSearch())}
            onFocus={() => results.length > 0 && setOpen(true)}
            placeholder="Type firm name or CUI (RO12345678)…"
            className="w-full rounded-md border border-input bg-background pl-8 pr-24 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring"
          />
          <Button
            type="button"
            size="sm"
            variant="outline"
            onClick={doSearch}
            disabled={loading || query.trim().length < 2}
            className="absolute right-1 top-1/2 -translate-y-1/2 h-7 gap-1 text-xs px-2"
          >
            {loading ? <Loader2 className="h-3 w-3 animate-spin" /> : <Search className="h-3 w-3" />}
            Search
          </Button>
        </div>

        {open && results.length > 0 && (
          <div className="absolute z-50 left-0 right-0 mt-1 max-h-64 overflow-y-auto rounded-md border border-border bg-popover shadow-lg">
            {results.map(r => (
              <button
                key={r.id}
                type="button"
                onClick={() => select(r)}
                className="w-full text-left px-3 py-2.5 hover:bg-accent flex items-start gap-2.5 border-b border-border/40 last:border-0"
              >
                <Building2 className="h-4 w-4 mt-0.5 shrink-0 text-muted-foreground" />
                <div className="min-w-0">
                  <p className="text-sm font-medium truncate">{r.name}</p>
                  <p className="text-[11px] text-muted-foreground">
                    CUI: {r.cui}
                    {r.tradeRegisterNo ? ` · ${r.tradeRegisterNo}` : ""}
                    {r.locality ? ` · ${r.locality}` : ""}
                    {r.county ? `, ${r.county}` : ""}
                    {r.address ? ` · ${r.address}` : ""}
                  </p>
                </div>
              </button>
            ))}
          </div>
        )}

        {results.length === 0 && query.length >= 2 && !loading && open === false && (
          <p className="text-xs text-muted-foreground">
            Click <strong>Search</strong> or press Enter to query the ONRC registry. If no results appear, ensure the database is populated in{" "}
            <a href="/settings/firms-database" className="text-primary hover:underline">Settings → Firms Database</a>.
          </p>
        )}
      </div>
    </div>
  );
}

// ── Main Page ────────────────────────────────────────────────────────────────

export default function OrganisationSettingsPage() {
  const { isGlobalAdmin, isTenantAdmin } = useAuth();
  const { t, locale, setLocale } = useTranslation();

  // Tenant section
  const [tenantData, setTenantData] = useState<Record<string, unknown> | null>(null);
  const [tenantName, setTenantName] = useState("");
  const [tenantDomain, setTenantDomain] = useState("");
  const [savingTenant, setSavingTenant] = useState(false);
  const [savedTenant, setSavedTenant] = useState(false);

  // Firm section
  const [firmLoading, setFirmLoading] = useState(true);
  const [savingFirm, setSavingFirm] = useState(false);
  const [savedFirm, setSavedFirm] = useState(false);

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
    client.get("/settings/tenant").then(r => {
      setTenantData(r.data);
      setTenantName(r.data.name ?? "");
      setTenantDomain(r.data.domain ?? "");
    }).catch(console.error);

    client.get("/settings/firm").then(r => {
      const f = r.data as InsolvencyFirmDto | null;
      if (f) {
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
    }).catch(console.error).finally(() => setFirmLoading(false));
  }, []);

  // Pre-fill firm from ONRC result
  const handleONRCSelect = (r: ONRCFirmResult) => {
    if (r.name) setFirmName(r.name);
    if (r.cui) setCuiRo(r.cui);
    if (r.tradeRegisterNo) setTradeRegisterNo(r.tradeRegisterNo);
    if (r.address) setAddress(r.address);
    if (r.locality) setLocality(r.locality);
    if (r.county) setCounty(r.county);
    if (r.postalCode) setPostalCode(r.postalCode);
    if (r.phone) setPhone(r.phone);
  };

  const handleSaveTenant = async () => {
    setSavingTenant(true);
    try {
      await client.put("/settings/tenant", { name: tenantName, domain: tenantDomain, language: locale });
      setSavedTenant(true);
      setTimeout(() => setSavedTenant(false), 2000);
    } catch (err) { console.error(err); }
    finally { setSavingTenant(false); }
  };

  const handleSaveFirm = async () => {
    setSavingFirm(true);
    try {
      await client.put("/settings/firm", {
        firmName, cuiRo: cuiRo || null, tradeRegisterNo: tradeRegisterNo || null,
        vatNumber: vatNumber || null, unpirRegistrationNo: unpirRegistrationNo || null,
        unpirRfo: unpirRfo || null, address: address || null, locality: locality || null,
        county: county || null, country: country || null, postalCode: postalCode || null,
        phone: phone || null, fax: fax || null, email: email || null, website: website || null,
        contactPerson: contactPerson || null, iban: iban || null, bankName: bankName || null,
        secondaryIban: secondaryIban || null, secondaryBankName: secondaryBankName || null,
      });
      setSavedFirm(true);
      setTimeout(() => setSavedFirm(false), 2000);
    } catch (err) { console.error(err); }
    finally { setSavingFirm(false); }
  };

  if (!isGlobalAdmin && !isTenantAdmin) {
    return (
      <div className="flex flex-col items-center justify-center h-full text-muted-foreground py-20">
        <Briefcase className="h-12 w-12 mb-3 opacity-30" />
        <p className="text-sm">You don't have access to organisation settings.</p>
      </div>
    );
  }

  const inputCls = "w-full rounded-md border border-input bg-background px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring";
  const labelCls = "mb-1 block text-xs font-semibold uppercase tracking-wide text-muted-foreground";

  const LANGUAGES: { code: Locale; label: string; flag: string }[] = [
    { code: "en", label: "English", flag: "🇬🇧" },
    { code: "ro", label: "Română", flag: "🇷🇴" },
    { code: "hu", label: "Magyar", flag: "🇭🇺" },
  ];

  return (
    <div className="mx-auto max-w-3xl space-y-6">
      <div>
        <h1 className="text-lg font-semibold text-foreground">Organisation</h1>
        <p className="text-sm text-muted-foreground mt-0.5">Workspace settings and insolvency firm details used in all generated documents.</p>
      </div>

      {/* ── Language ── */}
      <div className="rounded-xl border border-border bg-card p-5 space-y-3">
        <div className="flex items-center gap-2">
          <Globe className="h-4 w-4 text-primary" />
          <h2 className="text-sm font-semibold text-foreground">{t.settings.language}</h2>
        </div>
        <p className="text-xs text-muted-foreground">{t.settings.languageDesc}</p>
        <div className="flex gap-2 flex-wrap">
          {LANGUAGES.map(lang => (
            <button
              key={lang.code}
              type="button"
              onClick={() => setLocale(lang.code)}
              className={`flex items-center gap-2 rounded-lg border px-4 py-2.5 text-sm font-medium transition-all ${
                locale === lang.code
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

      {/* ── Workspace settings ── */}
      <div className="rounded-xl border border-border bg-card p-5 space-y-4">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <Building2 className="h-4 w-4 text-primary" />
            <h2 className="text-sm font-semibold text-foreground">{t.settings.orgSettings}</h2>
          </div>
          <Button size="sm" onClick={handleSaveTenant} disabled={savingTenant || !tenantData}
            className="bg-primary hover:bg-primary/90 h-8 px-3 text-xs">
            {savingTenant ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : savedTenant ? <Check className="h-3.5 w-3.5" /> : null}
            <span className="ml-1">{savedTenant ? "Saved" : "Save"}</span>
          </Button>
        </div>
        <div className="grid gap-4 sm:grid-cols-2">
          <div>
            <label className={labelCls}>{t.settings.orgName}</label>
            <input value={tenantName} onChange={e => setTenantName(e.target.value)} className={inputCls} />
          </div>
          <div>
            <label className={labelCls}>{t.settings.domain}</label>
            <input value={tenantDomain} onChange={e => setTenantDomain(e.target.value)} className={inputCls} placeholder={t.settings.domainPlaceholder} />
          </div>
        </div>

        {/* Plan / usage */}
        {tenantData && (
          <div className="mt-1 rounded-lg bg-muted/40 border border-border/60 p-3 grid gap-3 sm:grid-cols-3 text-sm">
            <div className="flex items-center gap-2">
              <CreditCard className="h-3.5 w-3.5 text-muted-foreground shrink-0" />
              <div>
                <p className="text-[10px] text-muted-foreground uppercase tracking-wide">Plan</p>
                <p className="font-medium text-xs">{(tenantData.planName as string) ?? "Free"}</p>
              </div>
            </div>
            <div className="flex items-center gap-2">
              <Users className="h-3.5 w-3.5 text-muted-foreground shrink-0" />
              <div>
                <p className="text-[10px] text-muted-foreground uppercase tracking-wide">Users / Cases / Companies</p>
                <p className="font-medium text-xs">
                  {tenantData.userCount as number} · {tenantData.caseCount as number} · {tenantData.companyCount as number}
                </p>
              </div>
            </div>
            <div className="flex items-center gap-2">
              <Badge variant={(tenantData.isActive as boolean) ? "success" : "destructive"} className="text-xs">
                {(tenantData.isActive as boolean) ? "Active" : "Inactive"}
              </Badge>
              {!!(tenantData.subscriptionExpiry as string | null) && (
                <p className="text-[10px] text-muted-foreground">
                  Expires {format(new Date(tenantData.subscriptionExpiry as string), "dd MMM yyyy")}
                </p>
              )}
            </div>
          </div>
        )}
      </div>

      {/* ── Insolvency Firm Details ── */}
      <div className="rounded-xl border border-border bg-card p-5 space-y-5">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <Landmark className="h-4 w-4 text-primary" />
            <div>
              <h2 className="text-sm font-semibold text-foreground">{t.firm?.title ?? "Insolvency Firm"}</h2>
              <p className="text-xs text-muted-foreground">{t.firm?.description ?? "These details are merged into all generated documents and correspondence."}</p>
            </div>
          </div>
          <Button size="sm" onClick={handleSaveFirm} disabled={savingFirm || firmLoading || !firmName}
            className="bg-primary hover:bg-primary/90 h-8 px-3 text-xs shrink-0">
            {savingFirm ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : savedFirm ? <Check className="h-3.5 w-3.5" /> : null}
            <span className="ml-1">{savedFirm ? "Saved" : t.firm?.saveFirm ?? "Save Firm"}</span>
          </Button>
        </div>

        {firmLoading ? (
          <div className="flex justify-center py-8">
            <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
          </div>
        ) : (
          <div className="space-y-5">
            {/* ONRC search to pre-fill */}
            <ONRCFirmSearch onSelect={handleONRCSelect} />

            {/* Identity */}
            <div>
              <p className="text-[10px] font-semibold uppercase tracking-widest text-muted-foreground/60 mb-3">Identity &amp; Registration</p>
              <div className="grid gap-4 sm:grid-cols-2">
                <div className="sm:col-span-2">
                  <label className={labelCls}>{t.firm?.firmName ?? "Firm Name"} *</label>
                  <input value={firmName} onChange={e => setFirmName(e.target.value)} required className={inputCls} />
                </div>
                <div>
                  <label className={labelCls}>{t.companies?.cuiRo ?? "CUI"}</label>
                  <input value={cuiRo} onChange={e => setCuiRo(e.target.value)} className={inputCls} placeholder="RO12345678" />
                </div>
                <div>
                  <label className={labelCls}>{t.companies?.tradeRegisterNo ?? "Trade Register No."}</label>
                  <input value={tradeRegisterNo} onChange={e => setTradeRegisterNo(e.target.value)} className={inputCls} placeholder="J12/999/2010" />
                </div>
                <div>
                  <label className={labelCls}>{t.companies?.vatNumber ?? "VAT Number"}</label>
                  <input value={vatNumber} onChange={e => setVatNumber(e.target.value)} className={inputCls} />
                </div>
                <div>
                  <label className={labelCls}>{t.firm?.unpirRegistrationNo ?? "UNPIR Registration No."}</label>
                  <input value={unpirRegistrationNo} onChange={e => setUnpirRegistrationNo(e.target.value)} className={inputCls} placeholder="RFO II-0999" />
                </div>
                <div>
                  <label className={labelCls}>{t.firm?.unpirRfo ?? "UNPIR RFO Code"}</label>
                  <input value={unpirRfo} onChange={e => setUnpirRfo(e.target.value)} className={inputCls} />
                </div>
              </div>
            </div>

            {/* Address */}
            <div>
              <p className="text-[10px] font-semibold uppercase tracking-widest text-muted-foreground/60 mb-3">Address</p>
              <div className="grid gap-4 sm:grid-cols-2">
                <div className="sm:col-span-2">
                  <AddressSearch
                    label={t.companies?.address ?? "Street Address"}
                    placeholder="Caută stradă, localitate..."
                    value={address}
                    onInputChange={setAddress}
                    onSelect={(sel) => {
                      setAddress(sel.address);
                      setLocality(sel.locality);
                      setCounty(sel.county);
                      setPostalCode(sel.postalCode);
                      setCountry(sel.country);
                    }}
                  />
                </div>
                <div>
                  <label className={labelCls}>{t.companies?.locality ?? "City / Locality"}</label>
                  <input value={locality} onChange={e => setLocality(e.target.value)} className={inputCls} />
                </div>
                <div>
                  <label className={labelCls}>{t.companies?.county ?? "County"}</label>
                  <input value={county} onChange={e => setCounty(e.target.value)} className={inputCls} />
                </div>
                <div>
                  <label className={labelCls}>{t.companies?.country ?? "Country"}</label>
                  <input value={country} onChange={e => setCountry(e.target.value)} className={inputCls} />
                </div>
                <div>
                  <label className={labelCls}>{t.companies?.postalCode ?? "Postal Code"}</label>
                  <input value={postalCode} onChange={e => setPostalCode(e.target.value)} className={inputCls} />
                </div>
              </div>
            </div>

            {/* Contact */}
            <div>
              <p className="text-[10px] font-semibold uppercase tracking-widest text-muted-foreground/60 mb-3">Contact</p>
              <div className="grid gap-4 sm:grid-cols-2">
                <div>
                  <label className={labelCls}>{t.companies?.phone ?? "Phone"}</label>
                  <input value={phone} onChange={e => setPhone(e.target.value)} className={inputCls} placeholder="+40 xxx xxx xxx" />
                </div>
                <div>
                  <label className={labelCls}>{t.firm?.fax ?? "Fax"}</label>
                  <input value={fax} onChange={e => setFax(e.target.value)} className={inputCls} />
                </div>
                <div>
                  <label className={labelCls}>{t.companies?.email ?? "Email"}</label>
                  <input value={email} onChange={e => setEmail(e.target.value)} className={inputCls} type="email" />
                </div>
                <div>
                  <label className={labelCls}>{t.firm?.website ?? "Website"}</label>
                  <input value={website} onChange={e => setWebsite(e.target.value)} className={inputCls} placeholder="https://" />
                </div>
                <div>
                  <label className={labelCls}>{t.companies?.contactPerson ?? "Contact Person"}</label>
                  <input value={contactPerson} onChange={e => setContactPerson(e.target.value)} className={inputCls} />
                </div>
              </div>
            </div>

            {/* Banking */}
            <div>
              <p className="text-[10px] font-semibold uppercase tracking-widest text-muted-foreground/60 mb-3">Banking</p>
              <div className="grid gap-4 sm:grid-cols-2">
                <div>
                  <label className={labelCls}>{t.companies?.iban ?? "IBAN"}</label>
                  <input value={iban} onChange={e => setIban(e.target.value)} className={inputCls} placeholder="RO49AAAA1B31007593840000" />
                </div>
                <div>
                  <label className={labelCls}>{t.companies?.bankName ?? "Bank Name"}</label>
                  <input value={bankName} onChange={e => setBankName(e.target.value)} className={inputCls} />
                </div>
                <div>
                  <label className={labelCls}>{t.firm?.secondaryIban ?? "Secondary IBAN"}</label>
                  <input value={secondaryIban} onChange={e => setSecondaryIban(e.target.value)} className={inputCls} placeholder="Cont lichidare / client funds" />
                </div>
                <div>
                  <label className={labelCls}>{t.firm?.secondaryBankName ?? "Secondary Bank"}</label>
                  <input value={secondaryBankName} onChange={e => setSecondaryBankName(e.target.value)} className={inputCls} />
                </div>
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
