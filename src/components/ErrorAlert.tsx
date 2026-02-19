import {AlertCircle, X} from "lucide-react";
import {Button} from "@/components/ui/button";

interface ErrorAlertProps {
  title?: string;
  message: string;
  onDismiss?: () => void;
}

export default function ErrorAlert({
  title = "Processing Error",
  message,
  onDismiss,
}: ErrorAlertProps) {
  return (
    <div className="rounded-lg border border-destructive/30 bg-destructive/10 p-4">
      <div className="flex items-start justify-between gap-3">
        <div className="flex items-start gap-3">
          <AlertCircle className="mt-0.5 h-5 w-5 shrink-0 text-destructive" />
          <div>
            <h4 className="text-sm font-medium text-destructive">{title}</h4>
            <p className="mt-1 text-sm text-destructive/90">{message}</p>
          </div>
        </div>
        {onDismiss && (
          <Button
            variant="ghost"
            size="icon"
            onClick={onDismiss}
            className="shrink-0 h-8 w-8 text-destructive hover:bg-destructive/10 hover:text-destructive"
            aria-label="Dismiss"
          >
            <X className="h-5 w-5" />
          </Button>
        )}
      </div>
    </div>
  );
}
