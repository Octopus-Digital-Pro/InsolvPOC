import { useState, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { casesApi, companiesApi } from "@/services/api";
import type { CompanyDto } from "@/services/api/types";
import { Button } from "@/components/ui/button";
import BackButton from "@/components/ui/BackButton";
import { Loader2 } from "lucide-react";

export default function NewCasePage() {
  const navigate = useNavigate();
  const [companies, setCompanies] = useState<CompanyDto[]>([]);
  const [saving, setSaving] = useState(false);

  const [caseNumber, setCaseNumber] = useState("");
const [courtName, setCourtName] = useState("");
  const [courtSection, setCourtSection] = useState("");
  const [debtorName, setDebtorName] = useState("");
  const [debtorCui, setDebtorCui] = useState("");
  const [companyId, setCompanyId] = useState("");
  const [procedureType, setProcedureType] = useState("generalInsolvency");
  const [lawReference, setLawReference] = useState("Legea 85/2014");

  useEffect(() => {
    companiesApi.getAll().then(r => setCompanies(r.data)).catch(console.error);
  }, []);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);
    try {
      const res = await casesApi.create({
        caseNumber,
  courtName: courtName || undefined,
        debtorName,
   debtorCui: debtorCui || undefined,
        companyId: companyId || undefined,
      });
      navigate(`/cases/${res.data.id}`);
    } catch (err) {
   console.error("Create case failed:", err);
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="mx-auto max-w-2xl">
      <BackButton onClick={() => navigate("/cases")}>Back to cases</BackButton>
      <h1 className="mt-2 text-xl font-bold text-foreground mb-5">New Insolvency Case</h1>

      <form onSubmit={handleSubmit} className="space-y-4 rounded-xl border border-border bg-card p-5">
        <div className="grid gap-4 sm:grid-cols-2">
          <div>
       <label className="mb-1 block text-xs font-semibold uppercase tracking-wide text-muted-foreground">Case Number *</label>
  <input value={caseNumber} onChange={e => setCaseNumber(e.target.value)} required className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm" placeholder="e.g. 1234/1285/2025" />
          </div>
        <div>
    <label className="mb-1 block text-xs font-semibold uppercase tracking-wide text-muted-foreground">Court</label>
            <input value={courtName} onChange={e => setCourtName(e.target.value)} className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm" placeholder="e.g. Tribunalul Cluj" />
          </div>
        </div>

        <div>
          <label className="mb-1 block text-xs font-semibold uppercase tracking-wide text-muted-foreground">Court Section</label>
          <input value={courtSection} onChange={e => setCourtSection(e.target.value)} className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm" placeholder="e.g. Sec?ia a II-a Civil?" />
     </div>

      <div className="grid gap-4 sm:grid-cols-2">
 <div>
  <label className="mb-1 block text-xs font-semibold uppercase tracking-wide text-muted-foreground">Debtor Name *</label>
    <input value={debtorName} onChange={e => setDebtorName(e.target.value)} required className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm" placeholder="e.g. SC Example SRL" />
    </div>
   <div>
          <label className="mb-1 block text-xs font-semibold uppercase tracking-wide text-muted-foreground">Debtor CUI</label>
      <input value={debtorCui} onChange={e => setDebtorCui(e.target.value)} className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm" placeholder="e.g. RO12345678" />
   </div>
        </div>

      <div className="grid gap-4 sm:grid-cols-2">
       <div>
            <label className="mb-1 block text-xs font-semibold uppercase tracking-wide text-muted-foreground">Procedure Type</label>
 <select value={procedureType} onChange={e => setProcedureType(e.target.value)} className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm">
     <option value="generalInsolvency">General Insolvency</option>
          <option value="simplifiedBankruptcy">Simplified Bankruptcy</option>
              <option value="reorganization">Reorganization</option>
    <option value="preventiveConcordat">Preventive Concordat</option>
     <option value="adHocMandate">Ad-Hoc Mandate</option>
   </select>
          </div>
   <div>
            <label className="mb-1 block text-xs font-semibold uppercase tracking-wide text-muted-foreground">Law Reference</label>
  <input value={lawReference} onChange={e => setLawReference(e.target.value)} className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm" />
          </div>
        </div>

        <div>
      <label className="mb-1 block text-xs font-semibold uppercase tracking-wide text-muted-foreground">Attach to Company</label>
          <select value={companyId} onChange={e => setCompanyId(e.target.value)} className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm">
         <option value="">— None (create standalone) —</option>
            {companies.map(c => <option key={c.id} value={c.id}>{c.name}{c.cuiRo ? ` (${c.cuiRo})` : ""}</option>)}
  </select>
 </div>

        <div className="flex justify-end gap-2 pt-2">
      <Button type="button" variant="outline" onClick={() => navigate("/cases")}>Cancel</Button>
      <Button type="submit" disabled={saving}>
            {saving && <Loader2 className="h-4 w-4 animate-spin mr-1" />}
            Create Case
          </Button>
        </div>
      </form>
 </div>
  );
}
