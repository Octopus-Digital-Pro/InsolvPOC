import {useCallback} from "react";
import {useDropzone} from "react-dropzone";
import {Upload} from "lucide-react";
import {ACCEPTED_FILE_TYPES} from "../services/fileProcessor";

interface FloatingUploadCTAProps {
  onUploadClick: () => void;
  onFileAccepted: (file: File) => void;
  isProcessing: boolean;
}

export default function FloatingUploadCTA({
  onUploadClick,
  onFileAccepted,
  isProcessing,
}: FloatingUploadCTAProps) {
  const onDrop = useCallback(
    (acceptedFiles: File[]) => {
      if (acceptedFiles.length > 0 && !isProcessing) {
        onFileAccepted(acceptedFiles[0]);
      }
    },
    [onFileAccepted, isProcessing],
  );

  const {getRootProps, getInputProps, isDragActive} = useDropzone({
    onDrop,
    accept: ACCEPTED_FILE_TYPES,
    multiple: false,
    disabled: isProcessing,
    noClick: true,
  });

  const handleClick = () => {
    if (!isProcessing) onUploadClick();
  };

  return (
    <div
      {...getRootProps()}
      onClick={handleClick}
      role="button"
      tabIndex={0}
      onKeyDown={(e) => {
        if (e.key === "Enter" || e.key === " ") {
          e.preventDefault();
          handleClick();
        }
      }}
      aria-label="Upload a document"
      className={`
        fixed bottom-6 right-6 z-20 flex cursor-pointer items-center gap-3 rounded-xl border-2  px-4 py-3 shadow-lg transition-all duration-200
        ${
          isDragActive
            ? "border-primary bg-primary/10"
            : "border-border bg-card hover:border-input hover:bg-accent"
        }
        ${isProcessing ? "pointer-events-none opacity-50" : ""}
      `}
      title="Upload a document"
    >
      <input {...getInputProps()} />
      <div className="rounded-full bg-muted p-2">
        <Upload
          className={`h-5 w-5 ${isDragActive ? "text-primary" : "text-muted-foreground"}`}
        />
      </div>
      <span className="text-sm font-medium text-foreground">Drag & drop</span>
    </div>
  );
}
