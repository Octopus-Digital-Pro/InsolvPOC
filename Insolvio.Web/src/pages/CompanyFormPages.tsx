import { useState, useEffect } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { companiesApi, usersApi, onrcApi } from "@/services/api";
import { useTranslation } from "@/contexts/LanguageContext";
import type { CompanyDto, UserDto } from "@/services/api/types";
import type { ONRCFirmResult } from "@/services/api/onrc";
import { Button } from "@/components/ui/button";
import BackButton from "@/components/ui/BackButton";
import AddressSearch from "@/components/AddressSearch";
import { Loader2, Building2, Phone, Database, Search } from "lucide-react";

function CompanyForm({ initial, onSubmit, saving, onCancel, title }: {
  initial: Partial<CompanyDto>;
  onSubmit: (data: Partial<CompanyDto>) => void;
  saving: boolean;
  onCancel: () => void;
  title: string;
}) {
  const { t } = useTranslation();
  const [name, setName] = useState(initial.name ?? "");
  const [cuiRo, setCuiRo] = useState(initial.cuiRo ?? "");
  const [tradeRegisterNo, setTradeRegisterNo] = useState(initial.tradeRegisterNo ?? "");
  const [vatNumber, setVatNumber] = useState(initial.vatNumber ?? "");
  const [address, setAddress] = useState(initial.address ?? "");
  const [locality, setLocality] = useState(initial.locality ?? "");
  const [county, setCounty] = useState(initial.county ?? "");
  const [country, setCountry] = useState(initial.country ?? "Romania");
  const [postalCode, setPostalCode] = useState(initial.postalCode ?? "");
  const [caen, setCaen] = useState(initial.caen ?? "");
  const [incorporationYear, setIncorporationYear] = useState(initial.incorporationYear ?? "");
  const [phone, setPhone] = useState(initial.phone ?? "");
  const [email, setEmail] = useState(initial.email ?? "");
  const [contactPerson, setContactPerson] = useState(initial.contactPerson ?? "");
  const [iban, setIban] = useState(initial.iban ?? "");
  const [bankName, setBankName] = useState(initial.bankName ?? "");
  const [assignedToUserId, setAssignedToUserId] = useState(initial.assignedToUserId ?? "");
  const [users, setUsers] = useState<UserDto[]>([]);

  // ONRC lookup state
  const [onrcQuery, setOnrcQuery] = useState("");
  const [onrcResults, setOnrcResults] = useState<ONRCFirmResult[]>([]);
  const [onrcSearching, setOnrcSearching] = useState(false);
  const [, setShowOnrcDropdown] = useState(false);
  const [localResults, setLocalResults] = useState<CompanyDto[]>([]);
  const [localSearching, setLocalSearching] = useState(false);
  const [showDropdown, setShowDropdown] = useState(false);

  useEffect(() => {
    usersApi.getAll().then(r => setUsers(r.data)).catch(console.error);
  }, []);

  // Search local DB as user types
  useEffect(() => {
    if (!onrcQuery || onrcQuery.trim().length < 2) {
      setLocalResults([]);
      setShowDropdown(false);
      return;
    }
    const timer = setTimeout(() => {
      setLocalSearching(true);
      companiesApi.search(onrcQuery.trim(), 8)
        .then(r => { setLocalResults(r.data); setShowDropdown(true); })
        .catch(console.error)
        .finally(() => setLocalSearching(false));
    }, 300);
    return () => clearTimeout(timer);
  }, [onrcQuery]);

  // Check for exact match — skip ONRC if found
  const hasExactMatch = localResults.some(c => {
    const q = onrcQuery.trim().toLowerCase();
    return (c.cuiRo && c.cuiRo.toLowerCase() === q) || c.name.toLowerCase() === q;
  });

  const triggerOnrcSearch = async () => {
    if (!onrcQuery || onrcQuery.trim().length < 2 || hasExactMatch) return;
    setOnrcSearching(true);
    try {
      const r = await onrcApi.search(onrcQuery.trim(), "Romania", 8);
      setOnrcResults(r.data);
      setShowDropdown(true);
    } catch { /* ignore */ }
    finally { setOnrcSearching(false); }
  };

  const fillFromLocal = (company: CompanyDto) => {
    setName(company.name);
    setCuiRo(company.cuiRo ?? "");
    if (company.tradeRegisterNo) setTradeRegisterNo(company.tradeRegisterNo);
    if (company.caen) setCaen(company.caen);
    if (company.address) setAddress(company.address);
    if (company.locality) setLocality(company.locality);
    if (company.county) setCounty(company.county);
    if (company.country) setCountry(company.country);
    if (company.postalCode) setPostalCode(company.postalCode);
    if (company.phone) setPhone(company.phone);
    if (company.email) setEmail(company.email);
    if (company.contactPerson) setContactPerson(company.contactPerson);
    if (company.iban) setIban(company.iban);
    if (company.bankName) setBankName(company.bankName);
    if (company.incorporationYear) setIncorporationYear(company.incorporationYear);
    setShowDropdown(false);
    setOnrcQuery("");
  };

const fillFromOnrc = (firm: ONRCFirmResult) => {
    setName(firm.name);
    setCuiRo(firm.cui);
    if (firm.tradeRegisterNo) setTradeRegisterNo(firm.tradeRegisterNo);
    if (firm.caen) setCaen(firm.caen);
    if (firm.address) setAddress(firm.address);
    if (firm.locality) setLocality(firm.locality);
    if (firm.county) setCounty(firm.county);
    if (firm.postalCode) setPostalCode(firm.postalCode);
    if (firm.phone) setPhone(firm.phone);
    if (firm.incorporationYear) setIncorporationYear(firm.incorporationYear);
    setShowDropdown(false);
    setShowOnrcDropdown(false);
    setOnrcQuery("");
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    onSubmit({
      name,
    cuiRo: cuiRo || undefined,
      tradeRegisterNo: tradeRegisterNo || undefined,
      vatNumber: vatNumber || undefined,
      address: address || undefined,
      locality: locality || undefined,
county: county || undefined,
      country: country || undefined,
      postalCode: postalCode || undefined,
      caen: caen || undefined,
  incorporationYear: incorporationYear || undefined,
    phone: phone || undefined,
  email: email || undefined,
 contactPerson: contactPerson || undefined,
 iban: iban || undefined,
    bankName: bankName || undefined,
   assignedToUserId: assignedToUserId || undefined,
    } as Partial<CompanyDto>);
  };

  const inputCls = "w-full rounded-md border border-input bg-background px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring";
  const labelCls = "mb-1 block text-xs font-semibold uppercase tracking-wide text-muted-foreground";

  return (
    <form onSubmit={handleSubmit} className="space-y-6">
      {/* Company Lookup — Local DB first, then ONRC */}
<div className="rounded-xl border border-primary/20 bg-primary/5 p-4 space-y-3">
        <div className="flex items-center gap-2">
     <Database className="h-4 w-4 text-primary" />
  <h2 className="text-sm font-semibold text-foreground">{t.settings.onrcDatabase ?? "Company Lookup"}</h2>
        <span className="text-[10px] text-muted-foreground ml-auto">Search local DB first, then ONRC registry</span>
        </div>
        <div className="relative">
          <div className="flex gap-2">
            <div className="relative flex-1">
              <input
                value={onrcQuery}
                onChange={e => { setOnrcQuery(e.target.value); setOnrcResults([]); setShowOnrcDropdown(false); }}
                onKeyDown={e => e.key === "Enter" && (e.preventDefault(), triggerOnrcSearch())}
                placeholder={t.settings.onrcSearchPlaceholder ?? "Search by CUI or company name..."}
                className={inputCls}
                onFocus={() => (localResults.length > 0 || onrcResults.length > 0) && setShowDropdown(true)}
              />
              {(onrcSearching || localSearching) && (
                <Loader2 className="absolute right-3 top-1/2 -translate-y-1/2 h-3.5 w-3.5 animate-spin text-muted-foreground" />
              )}
            </div>
            <Button
              type="button"
              variant="outline"
              onClick={triggerOnrcSearch}
              disabled={onrcSearching || !onrcQuery || onrcQuery.trim().length < 2 || hasExactMatch}
              className="shrink-0 gap-1.5 text-xs px-3"
              title={hasExactMatch ? "Exact match found locally — ONRC search skipped" : "Search ONRC registry"}
            >
              {onrcSearching ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Search className="h-3.5 w-3.5" />}
              ONRC
            </Button>
          </div>
          {showDropdown && (localResults.length > 0 || onrcResults.length > 0) && (
   <div className="absolute z-20 mt-1 w-full rounded-lg border border-border bg-card shadow-lg max-h-64 overflow-y-auto">
  {/* Local DB results */}
  {localResults.length > 0 && (
    <>
      <p className="px-3 py-1 text-[10px] font-semibold uppercase tracking-wide text-muted-foreground bg-muted/50 sticky top-0 z-10">
        Companii locale
      </p>
      {localResults.map(company => (
        <button
          key={company.id}
          type="button"
          onClick={() => fillFromLocal(company)}
          className="flex items-center gap-3 w-full px-4 py-2.5 text-left hover:bg-muted/50 transition-colors border-b border-border last:border-0"
        >
          <Building2 className="h-4 w-4 text-emerald-500 shrink-0" />
          <div className="min-w-0 flex-1">
            <p className="text-sm font-medium text-foreground truncate">{company.name}</p>
            <p className="text-xs text-muted-foreground truncate">
              {company.cuiRo ? `CUI: ${company.cuiRo}` : "No CUI"}
              {company.tradeRegisterNo && ` · ${company.tradeRegisterNo}`}
              {company.county && ` · ${company.county}`}
            </p>
          </div>
          <span className="text-[10px] px-1.5 py-0.5 rounded bg-emerald-100 text-emerald-700 dark:bg-emerald-900/40 dark:text-emerald-400">
            local
          </span>
        </button>
      ))}
    </>
  )}
  {/* ONRC results */}
  {onrcResults.length > 0 && (
    <>
      <p className="px-3 py-1 text-[10px] font-semibold uppercase tracking-wide text-muted-foreground bg-muted/50 sticky top-0 z-10">
        Registrul ONRC
      </p>
      {onrcResults.map(firm => (
         <button
         key={firm.id}
       type="button"
      onClick={() => fillFromOnrc(firm)}
                  className="flex items-center gap-3 w-full px-4 py-2.5 text-left hover:bg-muted/50 transition-colors border-b border-border last:border-0"
         >
    <Building2 className="h-4 w-4 text-primary shrink-0" />
          <div className="min-w-0 flex-1">
   <p className="text-sm font-medium text-foreground truncate">{firm.name}</p>
           <p className="text-xs text-muted-foreground truncate">
      CUI: {firm.cui}
         {firm.tradeRegisterNo && ` · ${firm.tradeRegisterNo}`}
       {firm.county && ` · ${firm.county}`}
                  </p>
   </div>
         {firm.status && (
      <span className={`text-[10px] px-1.5 py-0.5 rounded ${firm.status.toUpperCase() === "ACTIV" ? "bg-emerald-100 text-emerald-700" : "bg-gray-100 text-gray-600"}`}>
        {firm.status}
    </span>
    )}
   </button>
              ))}
    </>
  )}
            </div>
     )}
     {onrcQuery.trim().length >= 2 && !localSearching && localResults.length === 0 && onrcResults.length === 0 && !onrcSearching && (
       <p className="mt-1 text-xs text-muted-foreground">
         No local companies found. Click <strong>ONRC</strong> to search the national registry.
       </p>
     )}
        </div>
  </div>

      {/* Company Details */}
 <div className="rounded-xl border border-border bg-card p-5 space-y-4">
        <div className="flex items-center gap-2">
          <Building2 className="h-4 w-4 text-primary" />
     <h2 className="text-lg font-bold text-foreground">{title}</h2>
        </div>

        <div className="grid gap-4 sm:grid-cols-2">
        <div className="sm:col-span-2">
         <label className={labelCls}>{t.companies.companyName} *</label>
    <input value={name} onChange={e => setName(e.target.value)} required className={inputCls} />
          </div>
          <div>
            <label className={labelCls}>{t.companies.cuiRo}</label>
    <input value={cuiRo} onChange={e => setCuiRo(e.target.value)} className={inputCls} placeholder="RO12345678" />
      </div>
      <div>
        <label className={labelCls}>{t.companies.tradeRegisterNo}</label>
            <input value={tradeRegisterNo} onChange={e => setTradeRegisterNo(e.target.value)} className={inputCls} placeholder="J12/345/2020" />
          </div>
          <div>
       <label className={labelCls}>{t.companies.vatNumber || "VAT"}</label>
      <input value={vatNumber} onChange={e => setVatNumber(e.target.value)} className={inputCls} />
          </div>
        <div className="sm:col-span-2">
            <AddressSearch
              label={t.companies.address}
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
              inputClassName={inputCls.replace('w-full ', '')}
            />
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
     <label className={labelCls}>{t.companies.country || "Country"}</label>
            <input value={country} onChange={e => setCountry(e.target.value)} className={inputCls} />
   </div>
          <div>
<label className={labelCls}>{t.companies.postalCode || "Postal Code"}</label>
            <input value={postalCode} onChange={e => setPostalCode(e.target.value)} className={inputCls} />
          </div>
      <div>
            <label className={labelCls}>{t.companies.caen}</label>
          <input value={caen} onChange={e => setCaen(e.target.value)} className={inputCls} />
          </div>
          <div>
            <label className={labelCls}>{t.companies.incorporationYear}</label>
         <input value={incorporationYear} onChange={e => setIncorporationYear(e.target.value)} className={inputCls} />
  </div>
        </div>
      </div>

      {/* Contact & Banking */}
      <div className="rounded-xl border border-border bg-card p-5 space-y-4">
        <div className="flex items-center gap-2">
   <Phone className="h-4 w-4 text-primary" />
          <h2 className="text-sm font-bold text-foreground">{t.companies.contactSection || "Contact & Banking"}</h2>
 </div>
      <div className="grid gap-4 sm:grid-cols-2">
          <div>
     <label className={labelCls}>{t.companies.phone || "Phone"}</label>
 <input value={phone} onChange={e => setPhone(e.target.value)} className={inputCls} placeholder="+40 xxx xxx xxx" />
       </div>
       <div>
            <label className={labelCls}>{t.companies.email || "Email"}</label>
       <input value={email} onChange={e => setEmail(e.target.value)} className={inputCls} type="email" />
          </div>
   <div>
            <label className={labelCls}>{t.companies.contactPerson || "Contact Person"}</label>
         <input value={contactPerson} onChange={e => setContactPerson(e.target.value)} className={inputCls} />
          </div>
     <div>
            <label className={labelCls}>{t.companies.iban || "IBAN"}</label>
            <input value={iban} onChange={e => setIban(e.target.value)} className={inputCls} placeholder="RO49AAAA1B31007593840000" />
          </div>
  <div>
  <label className={labelCls}>{t.companies.bankName || "Bank"}</label>
            <input value={bankName} onChange={e => setBankName(e.target.value)} className={inputCls} />
          </div>
    <div>
            <label className={labelCls}>{t.companies.assignedTo}</label>
     <select value={assignedToUserId} onChange={e => setAssignedToUserId(e.target.value)} className={inputCls}>
         <option value=""></option>
           {users.map(u => <option key={u.id} value={u.id}>{u.fullName} ({u.role})</option>)}
        </select>
          </div>
     </div>
      </div>

      {/* Actions */}
   <div className="flex justify-end gap-2">
        <Button type="button" variant="outline" onClick={onCancel}>{t.common.cancel}</Button>
 <Button type="submit" disabled={saving} className="bg-primary hover:bg-primary/90">
{saving && <Loader2 className="h-4 w-4 animate-spin mr-1" />}
          {initial.id ? t.companies.saveChanges : t.companies.createCompany}
    </Button>
      </div>
    </form>
  );
}

export function NewCompanyPage() {
  const navigate = useNavigate();
  const { t } = useTranslation();
  const [saving, setSaving] = useState(false);

  const handleCreate = async (data: Partial<CompanyDto>) => {
    setSaving(true);
    try {
      const res = await companiesApi.create(data);
      navigate(`/companies/${res.data.id}`);
    } catch (err) { console.error("Create company failed:", err); }
    finally { setSaving(false); }
  };

  return (
    <div className="mx-auto max-w-2xl">
      <BackButton onClick={() => navigate("/companies")}>{t.companies.backToCompanies}</BackButton>
      <div className="mt-2">
        <CompanyForm
          initial={{}}
          onSubmit={handleCreate}
          saving={saving}
          onCancel={() => navigate("/companies")}
          title={t.companies.newCompany}
        />
      </div>
    </div>
  );
}

export function EditCompanyPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { t } = useTranslation();
  const [company, setCompany] = useState<CompanyDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
 if (!id) return;
    companiesApi.getById(id).then(r => setCompany(r.data)).catch(console.error).finally(() => setLoading(false));
  }, [id]);

  const handleUpdate = async (data: Partial<CompanyDto>) => {
    if (!id) return;
    setSaving(true);
    try {
      await companiesApi.update(id, data);
    navigate(`/companies/${id}`);
 } catch (err) { console.error("Update company failed:", err); }
    finally { setSaving(false); }
  };

  if (loading) return <div className="flex h-full items-center justify-center"><Loader2 className="h-8 w-8 animate-spin text-muted-foreground" /></div>;
  if (!company) return <p className="p-8 text-muted-foreground">{t.companies.noCompanies}</p>;

  return (
    <div className="mx-auto max-w-2xl">
      <BackButton onClick={() => navigate(`/companies/${id}`)}>{t.companies.backToCompany}</BackButton>
      <div className="mt-2">
        <CompanyForm initial={company} onSubmit={handleUpdate} saving={saving} onCancel={() => navigate(`/companies/${id}`)} title={`${t.companies.editCompany}: ${company.name}`} />
      </div>
    </div>
  );
}
