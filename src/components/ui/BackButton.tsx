import {ChevronLeft} from "lucide-react";

interface BackButtonProps {
  onClick: () => void;
  children: React.ReactNode;
  className?: string;
}

export default function BackButton({
  onClick,
  children,
  className = "",
}: BackButtonProps) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={
        className ||
        "mb-4 flex items-center gap-1.5 text-sm text-muted-foreground hover:text-primary transition-colors"
      }
    >
      <ChevronLeft className="h-4 w-4" />
      {children}
    </button>
  );
}
