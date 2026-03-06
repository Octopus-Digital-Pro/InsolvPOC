interface SectionProps {
  title: string;
  children: React.ReactNode;
}

export default function Section({title, children}: SectionProps) {
  return (
    <div className="mt-8">
      <h3 className="mb-3 border-b border-border pb-2 text-xs font-bold uppercase tracking-wider text-muted-foreground">
        {title}
      </h3>
      <div className="space-y-2">{children}</div>
    </div>
  );
}
