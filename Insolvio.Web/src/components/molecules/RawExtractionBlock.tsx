interface RawExtractionBlockProps {
  rawJson: string;
}

export default function RawExtractionBlock({rawJson}: RawExtractionBlockProps) {
  return (
    <details className="mt-6 rounded-lg border border-border bg-muted/30">
      <summary className="cursor-pointer px-4 py-3 text-xs font-medium text-muted-foreground hover:text-foreground">
        Raw AI Extraction (JSON)
      </summary>
      <pre className="overflow-x-auto whitespace-pre-wrap px-4 pb-4 font-mono text-xs text-muted-foreground">
        {rawJson}
      </pre>
    </details>
  );
}
