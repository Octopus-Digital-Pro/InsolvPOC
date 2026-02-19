interface ProcessingOverlayProps {
  fileName: string;
}

export default function ProcessingOverlay({fileName}: ProcessingOverlayProps) {
  return (
    <div className="flex flex-col items-center justify-center py-20">
      <div className="relative mb-6">
        <div className="h-14 w-14 rounded-full border-4 border-border" />
        <div className="absolute inset-0 h-14 w-14 animate-spin rounded-full border-4 border-transparent border-t-primary" />
      </div>
      <h3 className="text-lg font-semibold text-foreground">Analyzing document...</h3>
      <p className="mt-1 text-sm text-muted-foreground">{fileName}</p>
      <p className="mt-4 max-w-xs text-center text-xs text-muted-foreground">
        Extracting case number, debtor, deadlines, tables and reports using AI
      </p>
    </div>
  );
}
