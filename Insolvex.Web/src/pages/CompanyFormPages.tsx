import { useState, useEffect } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { companiesApi, usersApi } from "@/services/api";
import { useTranslation } from "@/contexts/LanguageContext";
import type { CompanyDto, UserDto } from "@/services/api/types";
import { Button } from "@/components/ui/button";
import BackButton from "@/components/ui/BackButton";
import { Loader2, Building2, Phone, Landmark, Scale, DollarSign, Flag, MoreHorizontal, ArrowRight } from "lucide-react";

const COMPANY_TYPES = [
  { value: "Debtor", icon: Building2, color: "border-red-200 bg-red-50 hover:border-red-400", textColor: "text-red-700", desc: "Company undergoing insolvency procedure" },
  { value: "InsolvencyPractitioner", icon: Scale, color: "border-blue-200 bg-blue-50 hover:border-blue-400", textColor: "text-blue-700", desc: "Judicial administrator / liquidator firm" },
  { value: "Creditor", icon: DollarSign, color: "border-amber-200 bg-amber-50 hover:border-amber-400", textColor: "text-amber-700", desc: "Secured, unsecured, budgetary, or employee creditor" },
  { value: "Court", icon: Landmark, color: "border-purple-200 bg-purple-50 hover:border-purple-400", textColor: "text-purple-700", desc: "Tribunal, court of appeal, or other judicial body" },
  { value: "GovernmentAgency", icon: Flag, color: "border-green-200 bg-green-50 hover:border-green-400", textColor: "text-green-700", desc: "ANAF, ONRC, or other government institution" },
  { value: "Other", icon: MoreHorizontal, color: "border-gray-200 bg-gray-50 hover:border-gray-400", textColor: "text-gray-700", desc: "Guarantor, expert, third party, or other" },
] as const;

function CompanyForm({ initial, onSubmit, saving, onCancel, title }: {
  initial: Partial<CompanyDto>;
  onSubmit: (data: Partial<CompanyDto>) => void;
  saving: boolean;
  onCancel: () => void;
  title: string;
}) {
  const { t } = useTranslation();
  const [name, setName] = useState(initial.name ?? "");
  const [companyType, setCompanyType] = useState(initial.companyType ?? "Debtor");
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

  useEffect(() => {
    usersApi.getAll().then(r => setUsers(r.data)).catch(console.error);
  }, []);

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    onSubmit({
      name, companyType,
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
            <label className={labelCls}>{t.companies.companyType || "Type"}</label>
<select value={companyType} onChange={e => setCompanyType(e.target.value)} className={inputCls}>
    {COMPANY_TYPES.map(ct => <option key={ct.value} value={ct.value}>{ct.value}</option>)}
     </select>
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
         <option value="">{t.common.unassigned}</option>
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
  const [selectedType, setSelectedType] = useState<string | null>(null);

  const handleCreate = async (data: Partial<CompanyDto>) => {
    setSaving(true);
    try {
      const res = await companiesApi.create(data);
      navigate(`/companies/${res.data.id}`);
    } catch (err) { console.error("Create company failed:", err); }
    finally { setSaving(false); }
  };

  // Step 1: Choose company type
  if (!selectedType) {
    return (
      <div className="mx-auto max-w-3xl">
     <BackButton onClick={() => navigate("/companies")}>{t.companies.backToCompanies}</BackButton>
        <div className="mt-4 text-center">
       <h1 className="text-xl font-bold text-foreground">{t.companies.newCompany}</h1>
          <p className="mt-1 text-sm text-muted-foreground">What type of company are you creating?</p>
        </div>
     <div className="mt-6 grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
        {COMPANY_TYPES.map(ct => {
            const Icon = ct.icon;
         return (
    <button
         key={ct.value}
      type="button"
  onClick={() => setSelectedType(ct.value)}
                className={`flex flex-col items-start gap-2 rounded-xl border-2 p-4 text-left transition-all cursor-pointer ${ct.color}`}
        >
            <div className="flex items-center gap-2">
      <Icon className={`h-5 w-5 ${ct.textColor}`} />
          <span className={`text-sm font-bold ${ct.textColor}`}>{ct.value.replace(/([A-Z])/g, " $1").trim()}</span>
      </div>
   <p className="text-xs text-muted-foreground">{ct.desc}</p>
  <ArrowRight className={`h-4 w-4 mt-1 ${ct.textColor} opacity-50`} />
              </button>
 );
   })}
  </div>
      </div>
    );
  }

  // Step 2: Company form with pre-selected type
  return (
    <div className="mx-auto max-w-2xl">
      <BackButton onClick={() => setSelectedType(null)}>? Change type</BackButton>
   <div className="mt-2">
        <CompanyForm
        initial={{ companyType: selectedType }}
          onSubmit={handleCreate}
          saving={saving}
 onCancel={() => navigate("/companies")}
          title={`${t.companies.newCompany}: ${selectedType.replace(/([A-Z])/g, " $1").trim()}`}
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
