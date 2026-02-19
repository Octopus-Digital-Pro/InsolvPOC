import type {Company} from "@/types";
import {Button} from "@/components/ui/button";

interface SuggestedMatchCardProps {
  company: Company;
  onAttach: (companyId: string) => void;
}

export default function SuggestedMatchCard({
  company,
  onAttach,
}: SuggestedMatchCardProps) {
  return (
    <section className="mt-6 rounded-xl border-2 border-primary/30 bg-primary/5 p-4">
      <h3 className="mb-2 text-xs font-semibold uppercase tracking-wide text-primary">
        Suggested match
      </h3>
      <p className="mb-3 text-sm text-muted-foreground">
        This document was automatically matched to an existing company based on
        extracted data.
      </p>
      <div className="flex flex-wrap items-center gap-3">
        <span className="font-medium text-foreground">{company.name}</span>
        {company.cuiRo && (
          <span className="text-xs text-muted-foreground">{company.cuiRo}</span>
        )}
        <Button onClick={() => onAttach(company.id)}>
          Attach to {company.name}
        </Button>
      </div>
    </section>
  );
}
