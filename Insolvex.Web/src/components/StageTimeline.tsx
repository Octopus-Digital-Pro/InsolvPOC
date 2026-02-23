import { useState, useEffect } from "react";
import { workflowApi } from "@/services/api/workflow";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import {
    CheckCircle2, Circle, PlayCircle, ChevronRight,
    Loader2, AlertTriangle,
} from "lucide-react";

interface StageInfo {
    stage: string;
    order: number;
    name: string;
    goal: string;
    status: "completed" | "current" | "pending";
}

interface ValidationResult {
    canAdvance: boolean;
  rules: Array<{ description: string; passed: boolean }>;
}

export default function StageTimeline({
    caseId,
    onAdvanced,
}: {
 caseId: string;
  onAdvanced?: () => void;
}) {
    const [stages, setStages] = useState<StageInfo[]>([]);
    const [validation, setValidation] = useState<ValidationResult | null>(null);
    const [loading, setLoading] = useState(true);
    const [advancing, setAdvancing] = useState(false);
    const [showValidation, setShowValidation] = useState(false);

    const loadTimeline = async () => {
        try {
 const r = await workflowApi.getTimeline(caseId);
        setStages(r.data);
      } catch (e) {
         console.error(e);
        } finally {
    setLoading(false);
    }
    };

    useEffect(() => {
        loadTimeline();
    }, [caseId]);

    const handleValidate = async () => {
        try {
         const r = await workflowApi.validate(caseId);
            setValidation(r.data);
            setShowValidation(true);
  } catch (e) {
            console.error(e);
        }
    };

    const handleAdvance = async () => {
        setAdvancing(true);
        try {
 await workflowApi.advance(caseId);
            setShowValidation(false);
     setValidation(null);
         await loadTimeline();
            onAdvanced?.();
   } catch (e) {
   console.error(e);
        } finally {
            setAdvancing(false);
  }
    };

if (loading) {
        return (
      <div className="p-4 text-center">
    <Loader2 className="h-5 w-5 animate-spin mx-auto text-muted-foreground" />
            </div>
     );
    }

    const currentStage = stages.find((s) => s.status === "current");

    return (
        <div className="space-y-1">
         <h3 className="text-xs font-semibold uppercase tracking-wider text-muted-foreground px-2 mb-2">
     Workflow Stages
            </h3>

            {stages.map((s) => (
     <div
 key={s.stage}
 className={`flex items-center gap-2 rounded-lg px-2 py-1.5 text-xs transition-colors ${
             s.status === "current"
          ? "bg-primary/10 text-primary font-medium"
     : s.status === "completed"
         ? "text-muted-foreground"
    : "text-muted-foreground/60"
     }`}
         title={s.goal}
        >
        {s.status === "completed" ? (
             <CheckCircle2 className="h-3.5 w-3.5 text-green-500 shrink-0" />
      ) : s.status === "current" ? (
               <PlayCircle className="h-3.5 w-3.5 text-primary shrink-0" />
      ) : (
      <Circle className="h-3.5 w-3.5 shrink-0" />
           )}
             <span className="truncate flex-1">{s.name}</span>
              {s.status === "current" && (
      <ChevronRight className="h-3 w-3 shrink-0" />
             )}
    </div>
  ))}

       {/* Advance button */}
      {currentStage && (
 <div className="pt-2 px-2 space-y-2">
      <Button
            size="sm"
     variant="outline"
     className="w-full text-xs gap-1 border-primary/30 text-primary hover:bg-primary/5"
              onClick={handleValidate}
    >
     Check Gate Rules
     </Button>

 {showValidation && validation && (
        <div className="rounded-lg border border-border bg-card p-2 space-y-1.5">
          {validation.rules.map((r, i) => (
   <div
             key={i}
         className="flex items-start gap-1.5 text-[10px]"
       >
             {r.passed ? (
            <CheckCircle2 className="h-3 w-3 text-green-500 shrink-0 mt-0.5" />
  ) : (
      <AlertTriangle className="h-3 w-3 text-amber-500 shrink-0 mt-0.5" />
      )}
        <span
  className={
 r.passed
       ? "text-muted-foreground"
       : "text-foreground font-medium"
       }
      >
  {r.description}
       </span>
            </div>
  ))}

               {validation.canAdvance && (
      <Button
    size="sm"
className="w-full mt-2 text-xs bg-green-600 hover:bg-green-700"
  onClick={handleAdvance}
                disabled={advancing}
     >
         {advancing ? (
         <Loader2 className="h-3.5 w-3.5 animate-spin" />
         ) : null}
  Advance to Next Stage
  </Button>
            )}

             {!validation.canAdvance && (
               <p className="text-[10px] text-amber-600 font-medium mt-1">
          Cannot advance — resolve the items above
           first.
            </p>
    )}
    </div>
            )}
         </div>
            )}
        </div>
    );
}
