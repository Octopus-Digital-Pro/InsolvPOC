import {Button} from "@/components/ui/button";

type PrefillSource = "beneficiary" | "contractor";

interface CreateCompanyFormProps {
  name: string;
  cuiRo: string;
  address: string;
  prefillSource: PrefillSource;
  error: string | null;
  saving: boolean;
  onPrefillBeneficiary: () => void;
  onPrefillContractor: () => void;
  onNameChange: (value: string) => void;
  onCuiRoChange: (value: string) => void;
  onAddressChange: (value: string) => void;
  onSave: () => void;
  onBack: () => void;
}

export default function CreateCompanyForm({
  name,
  cuiRo,
  address,
  prefillSource,
  error,
  saving,
  onPrefillBeneficiary,
  onPrefillContractor,
  onNameChange,
  onCuiRoChange,
  onAddressChange,
  onSave,
  onBack,
}: CreateCompanyFormProps) {
  return (
    <div className="mx-auto max-w-xl pt-12">
      <h2 className="text-xl font-semibold text-foreground">
        Create new company
      </h2>
      <p className="mt-1 text-sm text-muted-foreground">
        Pre-filled from scan. You can switch to contractor data or edit.
      </p>
      <div className="mt-2 flex gap-2 text-xs">
        <button
          type="button"
          onClick={onPrefillBeneficiary}
          className={
            prefillSource === "beneficiary"
              ? "font-medium text-primary"
              : "text-muted-foreground hover:text-foreground"
          }
        >
          Use beneficiary
        </button>
        <span className="text-border">|</span>
        <button
          type="button"
          onClick={onPrefillContractor}
          className={
            prefillSource === "contractor"
              ? "font-medium text-primary"
              : "text-muted-foreground hover:text-foreground"
          }
        >
          Use contractor
        </button>
      </div>
      <div className="mt-6 space-y-4">
        <div>
          <label className="mb-1 block text-xs font-semibold uppercase tracking-wide text-muted-foreground">
            Name
          </label>
          <input
            type="text"
            value={name}
            onChange={(e) => onNameChange(e.target.value)}
            className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm text-foreground placeholder:text-muted-foreground focus:border-ring focus:outline-none focus:ring-1 focus:ring-ring"
            placeholder="Company name"
          />
        </div>
        <div>
          <label className="mb-1 block text-xs font-semibold uppercase tracking-wide text-muted-foreground">
            CUI / RO
          </label>
          <input
            type="text"
            value={cuiRo}
            onChange={(e) => onCuiRoChange(e.target.value)}
            className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm text-foreground placeholder:text-muted-foreground focus:border-ring focus:outline-none focus:ring-1 focus:ring-ring"
            placeholder="Tax ID"
          />
        </div>
        <div>
          <label className="mb-1 block text-xs font-semibold uppercase tracking-wide text-muted-foreground">
            Address
          </label>
          <textarea
            value={address}
            onChange={(e) => onAddressChange(e.target.value)}
            rows={2}
            className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm text-foreground placeholder:text-muted-foreground focus:border-ring focus:outline-none focus:ring-1 focus:ring-ring"
            placeholder="Address"
          />
        </div>
        {error && <p className="text-sm text-destructive">{error}</p>}
        <div className="flex gap-3">
          <Button onClick={onSave} disabled={saving || !name.trim()}>
            {saving ? "Saving…" : "Create company & attach case"}
          </Button>
          <Button variant="outline" onClick={onBack}>
            Back
          </Button>
        </div>
      </div>
    </div>
  );
}
