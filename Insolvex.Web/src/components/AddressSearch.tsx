import { useState, useEffect, useRef, useCallback } from "react";
import { MapPin, Loader2, X } from "lucide-react";
import { addressApi, type AddressResult } from "@/services/api";

export interface AddressSelection {
  address: string;   // street + number
  locality: string;
  county: string;
  postalCode: string;
  country: string;
}

interface AddressSearchProps {
  /** Label shown above the input */
  label?: string;
  /** Placeholder inside the input */
  placeholder?: string;
  /** Current value shown in the input (controlled) */
  value?: string;
  /** Called every time the user types */
  onInputChange?: (value: string) => void;
  /** Called when the user picks a result from the dropdown */
  onSelect: (selection: AddressSelection) => void;
  className?: string;
  inputClassName?: string;
  disabled?: boolean;
}

/** Debounced Romanian address search backed by OSM Nominatim via the backend proxy. */
export default function AddressSearch({
  label,
  placeholder = "Caută adresă...",
  value = "",
  onInputChange,
  onSelect,
  className = "",
  inputClassName = "",
  disabled = false,
}: AddressSearchProps) {
  const [query, setQuery] = useState(value);
  const [results, setResults] = useState<AddressResult[]>([]);
  const [loading, setLoading] = useState(false);
  const [open, setOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  // Keep internal query in sync if parent changes value programmatically
  useEffect(() => {
    setQuery(value);
  }, [value]);

  // Close dropdown on outside click
  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setOpen(false);
      }
    };
    document.addEventListener("mousedown", handler);
    return () => document.removeEventListener("mousedown", handler);
  }, []);

  const search = useCallback(async (q: string) => {
    if (q.length < 3) {
      setResults([]);
      setOpen(false);
      return;
    }
    setLoading(true);
    try {
      const res = await addressApi.search(q);
      setResults(res.data);
      setOpen(res.data.length > 0);
    } catch {
      setResults([]);
      setOpen(false);
    } finally {
      setLoading(false);
    }
  }, []);

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const val = e.target.value;
    setQuery(val);
    onInputChange?.(val);

    if (debounceRef.current) clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(() => search(val), 350);
  };

  const handleSelect = (result: AddressResult) => {
    const streetAddress = [result.road, result.houseNumber].filter(Boolean).join(" ");
    const displayValue = streetAddress || result.city || result.displayName;

    setQuery(displayValue);
    setOpen(false);
    setResults([]);

    onSelect({
      address:    streetAddress,
      locality:   result.city,
      county:     result.county,
      postalCode: result.postcode,
      country:    result.country || "Romania",
    });
  };

  const handleClear = () => {
    setQuery("");
    setResults([]);
    setOpen(false);
    onInputChange?.("");
  };

  return (
    <div ref={containerRef} className={`relative ${className}`}>
      {label && (
        <label className="block text-xs font-medium text-muted-foreground mb-1">
          {label}
        </label>
      )}
      <div className="relative">
        <MapPin className="absolute left-2.5 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground pointer-events-none" />
        <input
          type="text"
          value={query}
          onChange={handleChange}
          onFocus={() => results.length > 0 && setOpen(true)}
          placeholder={placeholder}
          disabled={disabled}
          className={`w-full pl-8 pr-8 py-2 text-sm border border-input rounded-md bg-background
                      focus:outline-none focus:ring-2 focus:ring-ring disabled:opacity-50 ${inputClassName}`}
        />
        {loading ? (
          <Loader2 className="absolute right-2.5 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground animate-spin" />
        ) : query ? (
          <button
            type="button"
            onClick={handleClear}
            className="absolute right-2.5 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground hover:text-foreground"
          >
            <X className="h-4 w-4" />
          </button>
        ) : null}
      </div>

      {open && results.length > 0 && (
        <ul className="absolute z-50 mt-1 w-full bg-popover border border-border rounded-md shadow-lg max-h-64 overflow-y-auto text-sm">
          {results.map((result, idx) => {
            const streetAddress = [result.road, result.houseNumber].filter(Boolean).join(" ");
            const location = [result.city, result.county].filter(Boolean).join(", ");
            return (
              <li key={idx}>
                <button
                  type="button"
                  className="w-full text-left px-3 py-2 hover:bg-accent hover:text-accent-foreground transition-colors"
                  onClick={() => handleSelect(result)}
                >
                  <div className="font-medium truncate">
                    {streetAddress || result.city || result.displayName}
                  </div>
                  {location && (
                    <div className="text-xs text-muted-foreground truncate">{location}</div>
                  )}
                  {result.postcode && (
                    <div className="text-xs text-muted-foreground">{result.postcode}</div>
                  )}
                </button>
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
}
