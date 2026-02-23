import { useState } from "react";
import { useAuth } from "@/contexts/AuthContext";
import { useTranslation } from "@/contexts/LanguageContext";
import { Button } from "@/components/ui/button";
import { AlertCircle, Loader2 } from "lucide-react";
import logo from "@/assets/logo.png";
import DemoLoginPanel from "@/components/DemoLoginPanel";

const isDevelopment = import.meta.env.DEV;

export default function LoginPage() {
  const { login } = useAuth();
  const { t } = useTranslation();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    await handleLogin(email, password);
  };

  const handleLogin = async (loginEmail: string, loginPassword: string) => {
    setError(null);
    setLoading(true);
    try {
      await login(loginEmail, loginPassword);
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message || t.login.loginFailed;
      setError(msg);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="flex min-h-screen items-center justify-center bg-muted/30 p-4">
      <div className="w-full max-w-xl space-y-6">
        <div className="text-center">
          <img src={logo} alt="Insolvex" className="mx-auto mb-4 h-14 w-auto rounded-2xl object-contain shadow-lg" />
          <h1 className="text-2xl font-bold text-foreground">{t.login.title}</h1>
          <p className="mt-1 text-sm text-muted-foreground">{t.login.subtitle}</p>
        </div>

        {/* Demo Panel (dev only) */}
        {isDevelopment && <DemoLoginPanel onLogin={handleLogin} />}

        {/* Standard Login Form */}
        <form onSubmit={handleSubmit} className="space-y-4 rounded-xl border border-border bg-card p-6 shadow-sm">
          {error && (
            <div className="flex items-center gap-2 rounded-lg border border-destructive/30 bg-destructive/5 px-3 py-2 text-sm text-destructive">
              <AlertCircle className="h-4 w-4 shrink-0" />
              {error}
            </div>
          )}

          <div>
            <label htmlFor="email" className="mb-1 block text-xs font-semibold uppercase tracking-wide text-muted-foreground">
              {t.common.email}
            </label>
            <input
              id="email"
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm text-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-ring"
              placeholder={t.login.emailPlaceholder}
              required
              autoFocus={!isDevelopment}
            />
          </div>

          <div>
            <label htmlFor="password" className="mb-1 block text-xs font-semibold uppercase tracking-wide text-muted-foreground">
              {t.common.password}
            </label>
            <input
              id="password"
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm text-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-ring"
              placeholder={t.login.passwordPlaceholder}
              required
            />
          </div>

          <Button type="submit" className="w-full bg-primary hover:bg-primary/90" disabled={loading}>
            {loading && <Loader2 className="h-4 w-4 animate-spin" />}
            {t.common.signIn}
          </Button>
        </form>

        {!isDevelopment && (
          <p className="text-center text-xs text-muted-foreground">
            {t.login.demoHint}
          </p>
        )}
      </div>
    </div>
  );
}
