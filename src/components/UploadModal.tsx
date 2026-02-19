import FileDropZone from "./FileDropZone";
import ErrorAlert from "./ErrorAlert";
import {Button} from "@/components/ui/button";
import {X} from "lucide-react";

interface UploadModalProps {
  open: boolean;
  onClose: () => void;
  onFileAccepted: (file: File) => void;
  isProcessing: boolean;
  error: string | null;
}

export default function UploadModal({
  open,
  onClose,
  onFileAccepted,
  isProcessing,
  error,
}: UploadModalProps) {
  if (!open) return null;
  return (
    <div
      className="fixed inset-0 z-10 flex items-center justify-center bg-black/50 p-4"
      onClick={onClose}
      role="dialog"
      aria-modal="true"
      aria-labelledby="upload-modal-title"
    >
      <div
        className="w-full max-w-xl rounded-xl bg-card border border-border p-6 shadow-xl"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="mb-4 flex items-start justify-between">
          <div>
            <h2
              id="upload-modal-title"
              className="text-xl font-semibold text-card-foreground"
            >
              Upload a Contract
            </h2>
            <p className="mt-1 text-sm text-muted-foreground">
              Upload a contract document to automatically extract key information
            </p>
          </div>
          <Button
            variant="ghost"
            size="icon"
            onClick={onClose}
            className="text-muted-foreground hover:bg-accent hover:text-accent-foreground"
            aria-label="Close"
          >
            <X className="h-5 w-5" />
          </Button>
        </div>
        <FileDropZone
          onFileAccepted={onFileAccepted}
          isProcessing={isProcessing}
        />
        {error && (
          <div className="mt-4">
            <ErrorAlert message={error} title="Processing Error" />
          </div>
        )}
      </div>
    </div>
  );
}
