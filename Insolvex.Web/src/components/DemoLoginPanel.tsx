import { useState } from "react";
import { Badge } from "@/components/ui/badge";
import { useTranslation } from "@/contexts/LanguageContext";
import { Loader2, UserCircle, Shield, Briefcase, FileText } from "lucide-react";

interface DemoUser {
  email: string;
  password: string;
  firstName: string;
  lastName: string;
  role: "globalAdmin" | "tenantAdmin" | "practitioner" | "secretary";
  icon: typeof Shield;
  color: string;
}

const DEMO_USERS: DemoUser[] = [
  {
    email: "admin@insolvex.local",
    password: "Admin123!",
    firstName: "Admin",
    lastName: "User",
    role: "globalAdmin",
    icon: Shield,
    color: "text-purple-600 dark:text-purple-400",
  },
  {
    email: "practitioner@insolvex.local",
    password: "Pract123!",
    firstName: "Jon",
    lastName: "Doe",
    role: "practitioner",
    icon: Briefcase,
    color: "text-blue-600 dark:text-blue-400",
  },
  {
    email: "secretary@insolvex.local",
    password: "Secr123!",
    firstName: "Gipsz",
    lastName: "Jakab",
    role: "secretary",
    icon: FileText,
    color: "text-green-600 dark:text-green-400",
  },
];

export default function DemoLoginPanel({ onLogin }: { onLogin: (email: string, password: string) => Promise<void> }) {
  const { t } = useTranslation();
  const [loading, setLoading] = useState<string | null>(null);

  const handleDemoLogin = async (user: DemoUser) => {
    setLoading(user.email);
    try {
   await onLogin(user.email, user.password);
    } catch (err) {
      console.error("Demo login failed:", err);
    } finally {
      setLoading(null);
    }
  };

  const roleLabel = (role: string): string => {
    const map: Record<string, string> = {
      globalAdmin: t.login.demoRoleAdmin || "Admin",
practitioner: t.login.demoRolePractitioner || "Practician",
      secretary: t.login.demoRoleSecretary || "Secretar",
    };
 return map[role] || role;
  };

  return (
    <div className="rounded-xl border-2 border-dashed border-primary/30 bg-primary/5 p-5">
   <div className="mb-3 flex items-center gap-2">
        <UserCircle className="h-5 w-5 text-primary" />
        <h3 className="text-sm font-semibold text-foreground">{t.login.demoTitle || "Demo Users"}</h3>
        <Badge variant="secondary" className="ml-auto text-[10px]">DEV</Badge>
      </div>
      <p className="mb-4 text-xs text-muted-foreground">
        {t.login.demoDesc || "Click any user below to log in instantly (development mode only)."}
      </p>

 <div className="grid gap-2 sm:grid-cols-3">
   {DEMO_USERS.map((user) => {
     const Icon = user.icon;
        const isLoading = loading === user.email;

   return (
    <button
           key={user.email}
      onClick={() => handleDemoLogin(user)}
            disabled={!!loading}
        className="flex flex-col items-start gap-2 rounded-lg border border-border bg-card p-3 text-left transition-all hover:border-primary/50 hover:bg-accent/30 disabled:opacity-50 disabled:cursor-not-allowed"
            >
 <div className="flex items-center gap-2 w-full">
                <Icon className={`h-4 w-4 ${user.color}`} />
     <span className="text-sm font-medium text-foreground truncate flex-1">
          {user.firstName} {user.lastName}
         </span>
          </div>
              <div className="flex items-center justify-between w-full">
    <Badge variant="outline" className="text-[10px]">
        {roleLabel(user.role)}
        </Badge>
   {isLoading && <Loader2 className="h-3 w-3 animate-spin text-primary" />}
      </div>
   </button>
   );
        })}
      </div>

    <p className="mt-3 text-[10px] text-muted-foreground text-center">
      {t.login.demoNote || "All demo accounts have pre-seeded data."}
    </p>
    </div>
  );
}
