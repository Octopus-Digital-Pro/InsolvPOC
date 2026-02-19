import {useCallback} from "react";
import {useDropzone} from "react-dropzone";
import {Upload} from "lucide-react";
import {ACCEPTED_FILE_TYPES} from "../services/fileProcessor";

interface FileDropZoneProps {
  onFileAccepted: (file: File) => void;
  isProcessing: boolean;
}

export default function FileDropZone({
  onFileAccepted,
  isProcessing,
}: FileDropZoneProps) {
  const onDrop = useCallback(
    (acceptedFiles: File[]) => {
      if (acceptedFiles.length > 0 && !isProcessing) {
        onFileAccepted(acceptedFiles[0]);
      }
    },
    [onFileAccepted, isProcessing]
  );

  const {getRootProps, getInputProps, isDragActive} = useDropzone({
    onDrop,
    accept: ACCEPTED_FILE_TYPES,
    multiple: false,
    disabled: isProcessing,
  });

  return (
    <div
      {...getRootProps()}
      className={`
        flex flex-col items-center justify-center
        rounded-2xl border-2 border-dashed p-12
        transition-all duration-200 cursor-pointer
        ${isDragActive
          ? "border-primary bg-primary/10 scale-[1.01]"
          : "border-border bg-muted/50 hover:border-input hover:bg-muted"}
        ${isProcessing ? "opacity-50 pointer-events-none" : ""}
      `}
    >
      <input {...getInputProps()} />

      <div className="mb-4 rounded-full bg-card p-4 shadow-sm border border-border">
        <Upload
          className={`h-10 w-10 ${isDragActive ? "text-primary" : "text-muted-foreground"}`}
        />
      </div>

      {isDragActive ? (
        <p className="text-lg font-medium text-primary">Drop the file here</p>
      ) : (
        <>
          <p className="text-lg font-medium text-foreground">
            Drag & drop a document here
          </p>
          <p className="mt-1 text-sm text-muted-foreground">
            or <span className="text-primary underline">browse files</span>
          </p>
          <p className="mt-3 text-xs text-muted-foreground">
            Supports PDF, PNG, JPG, WEBP
          </p>
        </>
      )}
    </div>
  );
}
