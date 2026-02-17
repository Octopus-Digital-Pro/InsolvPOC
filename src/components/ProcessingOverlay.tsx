interface ProcessingOverlayProps {
  fileName: string;
}

export default function ProcessingOverlay({ fileName }: ProcessingOverlayProps) {
  return (
    <div className="flex flex-col items-center justify-center py-20">
      <div className="relative mb-6">
        <div className="h-14 w-14 rounded-full border-4 border-gray-200" />
        <div className="absolute inset-0 h-14 w-14 animate-spin rounded-full border-4 border-transparent border-t-blue-500" />
      </div>
      <h3 className="text-lg font-semibold text-gray-800">Analyzing document...</h3>
      <p className="mt-1 text-sm text-gray-500">{fileName}</p>
      <p className="mt-4 max-w-xs text-center text-xs text-gray-400">
        Extracting company name, addressee, dates, deadlines and court information using AI
      </p>
    </div>
  );
}
