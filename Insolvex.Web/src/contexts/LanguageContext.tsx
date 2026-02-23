import { createContext, useContext, useState, useCallback, type ReactNode } from "react";
import type { Locale, Translations } from "@/i18n/types";
import { en } from "@/i18n/en";
import { ro } from "@/i18n/ro";
import { hu } from "@/i18n/hu";

const TRANSLATIONS: Record<Locale, Translations> = { en, ro, hu };
const STORAGE_KEY = "insolvex_locale";

function getInitialLocale(): Locale {
  const stored = localStorage.getItem(STORAGE_KEY);
  if (stored === "en" || stored === "ro" || stored === "hu") return stored;
  // Auto-detect from browser
  const browserLang = navigator.language.toLowerCase();
  if (browserLang.startsWith("ro")) return "ro";
  if (browserLang.startsWith("hu")) return "hu";
  return "en";
}

interface LanguageContextValue {
  locale: Locale;
  t: Translations;
  setLocale: (locale: Locale) => void;
}

const LanguageContext = createContext<LanguageContextValue | null>(null);

export function LanguageProvider({ children }: { children: ReactNode }) {
  const [locale, setLocaleState] = useState<Locale>(getInitialLocale);

  const setLocale = useCallback((l: Locale) => {
    setLocaleState(l);
    localStorage.setItem(STORAGE_KEY, l);
  }, []);

  const t = TRANSLATIONS[locale];

  return (
    <LanguageContext.Provider value={{ locale, t, setLocale }}>
      {children}
    </LanguageContext.Provider>
  );
}

export function useTranslation() {
  const ctx = useContext(LanguageContext);
  if (!ctx) throw new Error("useTranslation must be used within LanguageProvider");
  return ctx;
}
