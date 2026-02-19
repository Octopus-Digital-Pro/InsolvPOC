import type {Company} from "../types";
import {Button} from "@/components/ui/button";
import {Building2, Upload} from "lucide-react";

interface CompaniesSidebarProps {
  open: boolean;
  companies: Company[];
  onUploadClick: () => void;
  showAddCompany: boolean;
  onToggleAddCompany: (show: boolean) => void;
  newCompanyName: string;
  newCompanyCuiRo: string;
  newCompanyAddress: string;
  addCompanyError: string | null;
  onNewCompanyNameChange: (value: string) => void;
  onNewCompanyCuiRoChange: (value: string) => void;
  onNewCompanyAddressChange: (value: string) => void;
  onSaveNewCompany: () => void;
}

export default function CompaniesSidebar({
  open,
  companies,
  onUploadClick,
  showAddCompany,
  onToggleAddCompany,
  newCompanyName,
  newCompanyCuiRo,
  newCompanyAddress,
  addCompanyError,
  onNewCompanyNameChange,
  onNewCompanyCuiRoChange,
  onNewCompanyAddressChange,
  onSaveNewCompany,
}: CompaniesSidebarProps) {
  if (!open) return null;
  return (
    <>
      <div className="flex items-center justify-between border-b border-sidebar-border px-4 py-3">
        <h2 className="text-sm font-semibold text-sidebar-foreground">
          Companies
        </h2>
        <span className="rounded-full bg-sidebar-accent px-2 py-0.5 text-xs text-sidebar-accent-foreground">
          {companies.length}
        </span>
      </div>

      {showAddCompany ? (
        <div className="space-y-2 border-b border-sidebar-border bg-sidebar-accent/50 p-3">
          <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
            New company
          </h3>
          <input
            type="text"
            value={newCompanyName}
            onChange={(e) => onNewCompanyNameChange(e.target.value)}
            placeholder="Company name"
            className="w-full rounded-md border border-input bg-background px-2 py-1.5 text-sm text-foreground placeholder:text-muted-foreground"
          />
          <input
            type="text"
            value={newCompanyCuiRo}
            onChange={(e) => onNewCompanyCuiRoChange(e.target.value)}
            placeholder="CUI / RO"
            className="w-full rounded-md border border-input bg-background px-2 py-1.5 text-sm text-foreground placeholder:text-muted-foreground"
          />
          <textarea
            value={newCompanyAddress}
            onChange={(e) => onNewCompanyAddressChange(e.target.value)}
            placeholder="Address"
            rows={2}
            className="w-full rounded-md border border-input bg-background px-2 py-1.5 text-sm text-foreground placeholder:text-muted-foreground"
          />
          {addCompanyError && (
            <p className="text-xs text-destructive">{addCompanyError}</p>
          )}
          <div className="flex gap-2">
            <Button size="sm" onClick={onSaveNewCompany}>
              Save
            </Button>
            <Button
              variant="outline"
              size="sm"
              onClick={() => onToggleAddCompany(false)}
            >
              Cancel
            </Button>
          </div>
        </div>
      ) : (
        <div>
          <div className="border-b border-sidebar-border px-4 py-2">
            <Button
              variant="ghost"
              size="sm"
              onClick={onUploadClick}
              title="Upload contract"
              className="gap-1.5 text-muted-foreground hover:text-primary hover:bg-sidebar-accent"
            >
              <Upload className="h-4 w-4 shrink-0" />
              <span>+ Upload contract</span>
            </Button>
          </div>
          <div className="border-b border-sidebar-border px-4 py-2">
            <Button
              variant="ghost"
              size="sm"
              onClick={() => onToggleAddCompany(true)}
              title="Register new company"
              className="gap-1.5 text-muted-foreground hover:text-primary hover:bg-sidebar-accent"
            >
              <Building2 className="h-4 w-4 shrink-0" />
              <span>Register new company</span>
            </Button>
          </div>
        </div>
      )}

      <div className="flex-1 overflow-y-auto p-3">
        {companies.length === 0 ? (
          <p className="px-2 py-8 text-center text-xs text-muted-foreground">
            No companies yet. Add a company or upload a document.
          </p>
        ) : null}
      </div>
    </>
  );
}
