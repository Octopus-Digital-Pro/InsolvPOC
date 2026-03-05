import { useNavigate } from "react-router-dom";
import { useTranslation } from "@/contexts/LanguageContext";
import { Button } from "@/components/ui/button";
import { BarChart3, FileText } from "lucide-react";

export default function ReportsPage() {
  const navigate = useNavigate();
  const { locale } = useTranslation();

  const text = {
    en: {
      title: "Reports",
      subtitle: "Open operational reports and logs.",
      auditTrail: "Audit Trail",
      errorLogs: "Error Logs",
    },
    ro: {
      title: "Rapoarte",
      subtitle: "Deschide rapoarte operaționale și jurnale.",
      auditTrail: "Jurnal Audit",
      errorLogs: "Jurnal Erori",
    },
    hu: {
      title: "Jelentések",
      subtitle: "Működési jelentések és naplók megnyitása.",
      auditTrail: "Audit napló",
      errorLogs: "Hibanapló",
    },
  }[locale];

  return (
    <div className="mx-auto max-w-4xl space-y-4">
      <div>
        <h1 className="text-xl font-bold text-foreground">{text.title}</h1>
        <p className="text-sm text-muted-foreground">{text.subtitle}</p>
      </div>

      <div className="grid gap-3 sm:grid-cols-2">
        <div className="rounded-xl border border-border bg-card p-4 space-y-3">
          <div className="flex items-center gap-2 text-sm font-medium text-foreground">
            <FileText className="h-4 w-4 text-primary" />
            {text.auditTrail}
          </div>
          <Button variant="outline" className="w-full" onClick={() => navigate("/audit-trail")}>
            {text.auditTrail}
          </Button>
        </div>

        <div className="rounded-xl border border-border bg-card p-4 space-y-3">
          <div className="flex items-center gap-2 text-sm font-medium text-foreground">
            <BarChart3 className="h-4 w-4 text-primary" />
            {text.errorLogs}
          </div>
          <Button variant="outline" className="w-full" onClick={() => navigate("/settings/errors")}>
            {text.errorLogs}
          </Button>
        </div>
      </div>
    </div>
  );
}
