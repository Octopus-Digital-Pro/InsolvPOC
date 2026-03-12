import { useState, useEffect, useCallback, useRef } from "react";
import { useAuth } from "@/contexts/AuthContext";
import { useTranslation } from "@/contexts/LanguageContext";
import { Button } from "@/components/ui/button";
import { regionsApi } from "@/services/api";
import type { RegionDto } from "@/services/api/types";
import { Shield, Globe, Loader2, Trash2, Plus, X, Check } from "lucide-react";

// ─── Flag image component ────────────────────────────────────────────────────────
// Uses flagcdn.com to serve real country flag PNG images by ISO alpha-2 code.
function FlagImg({ isoCode, name, className }: { isoCode: string; name?: string; className?: string }) {
  const code = isoCode?.slice(0, 2).toLowerCase();
  if (!code || code.length !== 2) return <span className={className}>🌍</span>;
  return (
    <img
      src={`https://flagcdn.com/32x24/${code}.png`}
      srcSet={`https://flagcdn.com/64x48/${code}.png 2x`}
      width={32}
      height={24}
      alt={name ?? isoCode}
      className={`rounded-sm object-cover inline-block ${className ?? ""}`}
      onError={(e) => { (e.currentTarget as HTMLImageElement).style.display = "none"; }}
    />
  );
}

// ─── Static world country dataset ────────────────────────────────────────────
const COUNTRIES: { name: string; isoCode: string; flag: string }[] = [
  { name: "Afghanistan", isoCode: "AF", flag: "🇦🇫" },
  { name: "Albania", isoCode: "AL", flag: "🇦🇱" },
  { name: "Algeria", isoCode: "DZ", flag: "🇩🇿" },
  { name: "Andorra", isoCode: "AD", flag: "🇦🇩" },
  { name: "Angola", isoCode: "AO", flag: "🇦🇴" },
  { name: "Argentina", isoCode: "AR", flag: "🇦🇷" },
  { name: "Armenia", isoCode: "AM", flag: "🇦🇲" },
  { name: "Australia", isoCode: "AU", flag: "🇦🇺" },
  { name: "Austria", isoCode: "AT", flag: "🇦🇹" },
  { name: "Azerbaijan", isoCode: "AZ", flag: "🇦🇿" },
  { name: "Bahrain", isoCode: "BH", flag: "🇧🇭" },
  { name: "Bangladesh", isoCode: "BD", flag: "🇧🇩" },
  { name: "Belarus", isoCode: "BY", flag: "🇧🇾" },
  { name: "Belgium", isoCode: "BE", flag: "🇧🇪" },
  { name: "Bolivia", isoCode: "BO", flag: "🇧🇴" },
  { name: "Bosnia and Herzegovina", isoCode: "BA", flag: "🇧🇦" },
  { name: "Brazil", isoCode: "BR", flag: "🇧🇷" },
  { name: "Bulgaria", isoCode: "BG", flag: "🇧🇬" },
  { name: "Cambodia", isoCode: "KH", flag: "🇰🇭" },
  { name: "Canada", isoCode: "CA", flag: "🇨🇦" },
  { name: "Chile", isoCode: "CL", flag: "🇨🇱" },
  { name: "China", isoCode: "CN", flag: "🇨🇳" },
  { name: "Colombia", isoCode: "CO", flag: "🇨🇴" },
  { name: "Croatia", isoCode: "HR", flag: "🇭🇷" },
  { name: "Cyprus", isoCode: "CY", flag: "🇨🇾" },
  { name: "Czech Republic", isoCode: "CZ", flag: "🇨🇿" },
  { name: "Denmark", isoCode: "DK", flag: "🇩🇰" },
  { name: "Ecuador", isoCode: "EC", flag: "🇪🇨" },
  { name: "Egypt", isoCode: "EG", flag: "🇪🇬" },
  { name: "Estonia", isoCode: "EE", flag: "🇪🇪" },
  { name: "Ethiopia", isoCode: "ET", flag: "🇪🇹" },
  { name: "Finland", isoCode: "FI", flag: "🇫🇮" },
  { name: "France", isoCode: "FR", flag: "🇫🇷" },
  { name: "Georgia", isoCode: "GE", flag: "🇬🇪" },
  { name: "Germany", isoCode: "DE", flag: "🇩🇪" },
  { name: "Ghana", isoCode: "GH", flag: "🇬🇭" },
  { name: "Greece", isoCode: "GR", flag: "🇬🇷" },
  { name: "Hungary", isoCode: "HU", flag: "🇭🇺" },
  { name: "Iceland", isoCode: "IS", flag: "🇮🇸" },
  { name: "India", isoCode: "IN", flag: "🇮🇳" },
  { name: "Indonesia", isoCode: "ID", flag: "🇮🇩" },
  { name: "Iran", isoCode: "IR", flag: "🇮🇷" },
  { name: "Iraq", isoCode: "IQ", flag: "🇮🇶" },
  { name: "Ireland", isoCode: "IE", flag: "🇮🇪" },
  { name: "Israel", isoCode: "IL", flag: "🇮🇱" },
  { name: "Italy", isoCode: "IT", flag: "🇮🇹" },
  { name: "Japan", isoCode: "JP", flag: "🇯🇵" },
  { name: "Jordan", isoCode: "JO", flag: "🇯🇴" },
  { name: "Kazakhstan", isoCode: "KZ", flag: "🇰🇿" },
  { name: "Kenya", isoCode: "KE", flag: "🇰🇪" },
  { name: "Kosovo", isoCode: "XK", flag: "🇽🇰" },
  { name: "Kuwait", isoCode: "KW", flag: "🇰🇼" },
  { name: "Latvia", isoCode: "LV", flag: "🇱🇻" },
  { name: "Lebanon", isoCode: "LB", flag: "🇱🇧" },
  { name: "Libya", isoCode: "LY", flag: "🇱🇾" },
  { name: "Lithuania", isoCode: "LT", flag: "🇱🇹" },
  { name: "Luxembourg", isoCode: "LU", flag: "🇱🇺" },
  { name: "Macedonia", isoCode: "MK", flag: "🇲🇰" },
  { name: "Malaysia", isoCode: "MY", flag: "🇲🇾" },
  { name: "Malta", isoCode: "MT", flag: "🇲🇹" },
  { name: "Mexico", isoCode: "MX", flag: "🇲🇽" },
  { name: "Moldova", isoCode: "MD", flag: "🇲🇩" },
  { name: "Monaco", isoCode: "MC", flag: "🇲🇨" },
  { name: "Montenegro", isoCode: "ME", flag: "🇲🇪" },
  { name: "Morocco", isoCode: "MA", flag: "🇲🇦" },
  { name: "Netherlands", isoCode: "NL", flag: "🇳🇱" },
  { name: "New Zealand", isoCode: "NZ", flag: "🇳🇿" },
  { name: "Nigeria", isoCode: "NG", flag: "🇳🇬" },
  { name: "Norway", isoCode: "NO", flag: "🇳🇴" },
  { name: "Pakistan", isoCode: "PK", flag: "🇵🇰" },
  { name: "Peru", isoCode: "PE", flag: "🇵🇪" },
  { name: "Philippines", isoCode: "PH", flag: "🇵🇭" },
  { name: "Poland", isoCode: "PL", flag: "🇵🇱" },
  { name: "Portugal", isoCode: "PT", flag: "🇵🇹" },
  { name: "Qatar", isoCode: "QA", flag: "🇶🇦" },
  { name: "Romania", isoCode: "RO", flag: "🇷🇴" },
  { name: "Russia", isoCode: "RU", flag: "🇷🇺" },
  { name: "Saudi Arabia", isoCode: "SA", flag: "🇸🇦" },
  { name: "Serbia", isoCode: "RS", flag: "🇷🇸" },
  { name: "Singapore", isoCode: "SG", flag: "🇸🇬" },
  { name: "Slovakia", isoCode: "SK", flag: "🇸🇰" },
  { name: "Slovenia", isoCode: "SI", flag: "🇸🇮" },
  { name: "South Africa", isoCode: "ZA", flag: "🇿🇦" },
  { name: "South Korea", isoCode: "KR", flag: "🇰🇷" },
  { name: "Spain", isoCode: "ES", flag: "🇪🇸" },
  { name: "Sweden", isoCode: "SE", flag: "🇸🇪" },
  { name: "Switzerland", isoCode: "CH", flag: "🇨🇭" },
  { name: "Taiwan", isoCode: "TW", flag: "🇹🇼" },
  { name: "Thailand", isoCode: "TH", flag: "🇹🇭" },
  { name: "Tunisia", isoCode: "TN", flag: "🇹🇳" },
  { name: "Turkey", isoCode: "TR", flag: "🇹🇷" },
  { name: "Ukraine", isoCode: "UA", flag: "🇺🇦" },
  { name: "United Arab Emirates", isoCode: "AE", flag: "🇦🇪" },
  { name: "United Kingdom", isoCode: "GB", flag: "🇬🇧" },
  { name: "United States", isoCode: "US", flag: "🇺🇸" },
  { name: "Uruguay", isoCode: "UY", flag: "🇺🇾" },
  { name: "Uzbekistan", isoCode: "UZ", flag: "🇺🇿" },
  { name: "Venezuela", isoCode: "VE", flag: "🇻🇪" },
  { name: "Vietnam", isoCode: "VN", flag: "🇻🇳" },
];

// ─── Country Autocomplete ────────────────────────────────────────────────────
interface SelectedCountry {
  name: string;
  isoCode: string;
  flag: string;
}

function CountryAutocomplete({
  value,
  onChange,
  placeholder,
}: {
  value: SelectedCountry | null;
  onChange: (country: SelectedCountry | null) => void;
  placeholder: string;
}) {
  const [inputValue, setInputValue] = useState(value ? `${value.flag} ${value.name}` : "");
  const [open, setOpen] = useState(false);
  const [filtered, setFiltered] = useState<typeof COUNTRIES>([]);
  const containerRef = useRef<HTMLDivElement>(null);

  const handleInput = (e: React.ChangeEvent<HTMLInputElement>) => {
    const val = e.target.value;
    setInputValue(val);
    onChange(null); // Clear selection when user types

    if (val.trim().length === 0) {
      setFiltered([]);
      setOpen(false);
      return;
    }

    const lower = val.toLowerCase();
    const matches = COUNTRIES.filter(
      (c) =>
        c.name.toLowerCase().includes(lower) ||
        c.isoCode.toLowerCase().includes(lower)
    ).slice(0, 10);
    setFiltered(matches);
    setOpen(matches.length > 0);
  };

  const handleSelect = (country: (typeof COUNTRIES)[0]) => {
    onChange(country);
    setInputValue(`${country.flag} ${country.name}`);
    setOpen(false);
  };

  const handleClear = () => {
    onChange(null);
    setInputValue("");
    setOpen(false);
  };

  // Close on outside click
  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setOpen(false);
      }
    };
    document.addEventListener("mousedown", handler);
    return () => document.removeEventListener("mousedown", handler);
  }, []);

  // Sync when value changes externally
  useEffect(() => {
    if (!value) {
      setInputValue("");
    }
  }, [value]);

  return (
    <div ref={containerRef} className="relative flex-1">
      <div className="relative">
        <input
          type="text"
          value={inputValue}
          onChange={handleInput}
          onFocus={() => {
            if (filtered.length > 0) setOpen(true);
          }}
          placeholder={placeholder}
          className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm pr-8 focus:outline-none focus:ring-2 focus:ring-ring"
          autoComplete="off"
        />
        {inputValue && (
          <button
            type="button"
            onClick={handleClear}
            className="absolute right-2 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground"
          >
            <X className="h-3.5 w-3.5" />
          </button>
        )}
      </div>
      {open && (
        <div className="absolute z-50 mt-1 w-full rounded-md border border-border bg-popover shadow-lg max-h-56 overflow-y-auto">
          {filtered.map((country) => (
            <button
              key={country.isoCode}
              type="button"
              onClick={() => handleSelect(country)}
              className="flex w-full items-center gap-2 px-3 py-2 text-sm text-popover-foreground hover:bg-accent transition-colors text-left"
            >
              <span className="text-base">{country.flag}</span>
              <span className="flex-1">{country.name}</span>
              <span className="text-xs text-muted-foreground">{country.isoCode}</span>
            </button>
          ))}
        </div>
      )}
    </div>
  );
}

// ─── Delete Confirmation Modal ────────────────────────────────────────────────
function DeleteConfirmModal({
  regionName,
  onConfirm,
  onCancel,
}: {
  regionName: string;
  onConfirm: () => void;
  onCancel: () => void;
}) {
  const { t } = useTranslation();
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50" onClick={onCancel}>
      <div
        className="bg-card border border-border rounded-xl shadow-xl w-full max-w-sm mx-4 p-5 space-y-4"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="flex items-center justify-between">
          <h2 className="text-sm font-semibold text-foreground">{t.regions.deleteConfirmTitle}</h2>
          <Button variant="ghost" size="icon" className="h-7 w-7" onClick={onCancel}>
            <X className="h-4 w-4" />
          </Button>
        </div>
        <p className="text-sm text-muted-foreground">
          {t.regions.deleteConfirmMessage}{" "}
          <span className="font-medium text-foreground">{regionName}</span>?
        </p>
        <div className="flex justify-end gap-2">
          <Button variant="outline" size="sm" onClick={onCancel}>
            {t.common.cancel}
          </Button>
          <Button variant="destructive" size="sm" onClick={onConfirm}>
            {t.common.delete}
          </Button>
        </div>
      </div>
    </div>
  );
}

// ─── Toast notification ───────────────────────────────────────────────────────
function Toast({ message, onClose }: { message: string; onClose: () => void }) {
  useEffect(() => {
    const timer = setTimeout(onClose, 3500);
    return () => clearTimeout(timer);
  }, [onClose]);

  return (
    <div className="fixed bottom-4 right-4 z-50 flex items-center gap-2 rounded-lg border border-border bg-card px-4 py-3 shadow-lg text-sm text-foreground animate-in slide-in-from-bottom-2">
      <Check className="h-4 w-4 text-green-500 shrink-0" />
      <span>{message}</span>
      <button onClick={onClose} className="ml-2 text-muted-foreground hover:text-foreground">
        <X className="h-3.5 w-3.5" />
      </button>
    </div>
  );
}

// ─── Main Page ────────────────────────────────────────────────────────────────
export default function RegionsPage() {
  const { isGlobalAdmin } = useAuth();
  const { t } = useTranslation();

  const [regions, setRegions] = useState<RegionDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [selectedCountry, setSelectedCountry] = useState<SelectedCountry | null>(null);
  const [adding, setAdding] = useState(false);
  const [addError, setAddError] = useState("");
  const [toast, setToast] = useState<string | null>(null);
  const [confirmDelete, setConfirmDelete] = useState<RegionDto | null>(null);
  const [deleting, setDeleting] = useState<string | null>(null);
  const [settingDefault, setSettingDefault] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const res = await regionsApi.getAll();
      setRegions(res.data);
    } catch {
      // silently fail
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  if (!isGlobalAdmin) {
    return (
      <div className="flex flex-col items-center justify-center h-full text-muted-foreground">
        <Shield className="h-12 w-12 mb-3 opacity-30" />
        <p className="text-sm">Only Global Admins can manage regions.</p>
      </div>
    );
  }

  const handleAdd = async () => {
    setAddError("");
    if (!selectedCountry) {
      setAddError(t.regions.selectCountryFirst);
      return;
    }

    // Local duplicate check
    const exists = regions.some(
      (r) =>
        r.isoCode.toUpperCase() === selectedCountry.isoCode.toUpperCase() ||
        r.name.toLowerCase() === selectedCountry.name.toLowerCase()
    );
    if (exists) {
      setAddError(t.regions.alreadyExists);
      return;
    }

    setAdding(true);
    try {
      await regionsApi.create({
        name: selectedCountry.name,
        isoCode: selectedCountry.isoCode,
        flag: selectedCountry.flag,
      });
      setSelectedCountry(null);
      setToast(t.regions.addSuccess);
      await load();
    } catch (err: unknown) {
      const axErr = err as { response?: { data?: { message?: string } } };
      setAddError(axErr?.response?.data?.message ?? t.regions.duplicateError);
    } finally {
      setAdding(false);
    }
  };

  const handleDelete = async (region: RegionDto) => {
    setConfirmDelete(null);
    setDeleting(region.id);
    try {
      await regionsApi.delete(region.id);
      setToast(t.regions.deleteSuccess);
      await load();
    } catch (err: unknown) {
      const axErr = err as { response?: { data?: { message?: string } } };
      setAddError(axErr?.response?.data?.message ?? "Failed to delete region.");
    } finally {
      setDeleting(null);
    }
  };

  const handleSetDefault = async (region: RegionDto) => {
    if (region.isDefault) return; // already default — nothing to do
    setSettingDefault(region.id);
    try {
      await regionsApi.setDefault(region.id);
      setToast(`${region.name} is now the default region.`);
      await load();
    } catch (err: unknown) {
      const axErr = err as { response?: { data?: { message?: string } } };
      setAddError(axErr?.response?.data?.message ?? "Failed to update default region.");
    } finally {
      setSettingDefault(null);
    }
  };

  return (
    <div className="mx-auto max-w-3xl space-y-6">
      {/* Page header */}
      <div className="flex items-center gap-3">
        <Globe className="h-6 w-6 text-primary shrink-0" />
        <h1 className="text-xl font-bold text-foreground">{t.regions.title}</h1>
      </div>

      {/* Add Region section */}
      <div className="rounded-xl border border-border bg-card p-4 space-y-3">
        <h2 className="text-sm font-semibold text-foreground">{t.regions.addRegion}</h2>
        <div className="flex items-start gap-2">
          <CountryAutocomplete
            value={selectedCountry}
            onChange={(c) => {
              setSelectedCountry(c);
              setAddError("");
            }}
            placeholder={t.regions.countryPlaceholder}
          />
          <Button
            size="sm"
            onClick={handleAdd}
            disabled={adding || !selectedCountry}
            className="gap-1 shrink-0"
          >
            {adding ? (
              <Loader2 className="h-3.5 w-3.5 animate-spin" />
            ) : (
              <Plus className="h-3.5 w-3.5" />
            )}
            {t.regions.addRegion}
          </Button>
        </div>
        {addError && <p className="text-xs text-destructive">{addError}</p>}
      </div>

      {/* Regions table */}
      <div className="rounded-xl border border-border bg-card overflow-hidden">
        {loading ? (
          <div className="flex justify-center py-12">
            <Loader2 className="h-7 w-7 animate-spin text-muted-foreground" />
          </div>
        ) : regions.length === 0 ? (
          <p className="px-4 py-8 text-center text-sm text-muted-foreground">
            {t.regions.noRegions}
          </p>
        ) : (
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-border bg-muted/40">
                <th className="px-4 py-2.5 text-center text-[10px] font-semibold uppercase tracking-widest text-muted-foreground w-14">
                  Default
                </th>
                <th className="px-4 py-2.5 text-left text-[10px] font-semibold uppercase tracking-widest text-muted-foreground w-16">
                  {t.regions.flag}
                </th>
                <th className="px-4 py-2.5 text-left text-[10px] font-semibold uppercase tracking-widest text-muted-foreground">
                  {t.regions.regionName}
                </th>
                <th className="px-4 py-2.5 text-left text-[10px] font-semibold uppercase tracking-widest text-muted-foreground w-24">
                  {t.regions.isoCode}
                </th>
                <th className="px-4 py-2.5 text-center text-[10px] font-semibold uppercase tracking-widest text-muted-foreground w-24">
                  {t.regions.usageCount}
                </th>
                <th className="px-4 py-2.5 text-right text-[10px] font-semibold uppercase tracking-widest text-muted-foreground w-24">
                  {t.regions.actions}
                </th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {regions.map((region) => (
                <tr
                  key={region.id}
                  className={`transition-colors ${
                    region.isDefault
                      ? "bg-primary/5 hover:bg-primary/10"
                      : "hover:bg-muted/30 opacity-80"
                  }`}
                >
                  {/* Default checkbox */}
                  <td className="px-4 py-3 text-center" onClick={(e) => e.stopPropagation()}>
                    {settingDefault === region.id ? (
                      <Loader2 className="h-4 w-4 animate-spin text-primary mx-auto" />
                    ) : (
                      <input
                        type="radio"
                        name="defaultRegion"
                        checked={region.isDefault}
                        onChange={() => handleSetDefault(region)}
                        className="h-4 w-4 accent-primary cursor-pointer"
                        title={region.isDefault ? "Current default region" : `Set ${region.name} as default`}
                      />
                    )}
                  </td>

                  {/* Flag */}
                  <td className="px-4 py-3">
                    <span
                      className={`transition-all duration-200 ${
                        region.isDefault ? "" : " opacity-60"
                      }`}
                    >
                      <FlagImg isoCode={region.isoCode} name={region.name} />
                    </span>
                  </td>

                  {/* Region name */}
                  <td className="px-4 py-3">
                    <span
                      className={`text-sm transition-all duration-200 ${
                        region.isDefault
                          ? "font-bold text-foreground"
                          : "font-normal text-muted-foreground"
                      }`}
                    >
                      {region.name}
                      {region.isDefault && (
                        <span className="ml-2 text-[10px] font-semibold text-primary uppercase tracking-wider">
                          Default
                        </span>
                      )}
                    </span>
                  </td>

                  {/* ISO Code */}
                  <td className="px-4 py-3 text-xs text-muted-foreground font-mono">
                    {region.isoCode}
                  </td>

                  {/* Usage count */}
                  <td className="px-4 py-3 text-center text-xs text-muted-foreground">
                    {region.usageCount}
                  </td>

                  {/* Actions */}
                  <td className="px-4 py-3 text-right" onClick={(e) => e.stopPropagation()}>
                    {region.isDefault ? (
                      <div className="relative group inline-block">
                        <Button
                          variant="ghost"
                          size="icon"
                          className="h-7 w-7 text-muted-foreground cursor-not-allowed opacity-40"
                          disabled
                          aria-label="Cannot delete the default region"
                        >
                          <Trash2 className="h-3.5 w-3.5" />
                        </Button>
                        <div className="pointer-events-none absolute bottom-full right-0 mb-1 hidden group-hover:block">
                          <div className="rounded-md bg-popover border border-border px-2.5 py-1.5 text-xs text-popover-foreground shadow-lg whitespace-nowrap max-w-xs">
                            Cannot delete the default region
                          </div>
                        </div>
                      </div>
                    ) : region.usageCount > 0 ? (
                      <div className="relative group inline-block">
                        <Button
                          variant="ghost"
                          size="icon"
                          className="h-7 w-7 text-muted-foreground cursor-not-allowed opacity-40"
                          disabled
                          aria-label={t.regions.inUseTooltip}
                        >
                          <Trash2 className="h-3.5 w-3.5" />
                        </Button>
                        <div className="pointer-events-none absolute bottom-full right-0 mb-1 hidden group-hover:block">
                          <div className="rounded-md bg-popover border border-border px-2.5 py-1.5 text-xs text-popover-foreground shadow-lg whitespace-nowrap max-w-xs">
                            {t.regions.inUseTooltip}
                          </div>
                        </div>
                      </div>
                    ) : (
                      <Button
                        variant="ghost"
                        size="icon"
                        className="h-7 w-7 text-destructive hover:text-destructive hover:bg-destructive/10"
                        disabled={deleting === region.id}
                        onClick={() => setConfirmDelete(region)}
                        aria-label={t.common.delete}
                      >
                        {deleting === region.id ? (
                          <Loader2 className="h-3.5 w-3.5 animate-spin" />
                        ) : (
                          <Trash2 className="h-3.5 w-3.5" />
                        )}
                      </Button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {/* Delete confirmation modal */}
      {confirmDelete && (
        <DeleteConfirmModal
          regionName={confirmDelete.name}
          onConfirm={() => handleDelete(confirmDelete)}
          onCancel={() => setConfirmDelete(null)}
        />
      )}

      {/* Success toast */}
      {toast && <Toast message={toast} onClose={() => setToast(null)} />}
    </div>
  );
}
