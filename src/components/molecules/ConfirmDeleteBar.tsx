import {Button} from "@/components/ui/button";

interface ConfirmDeleteBarProps {
  isDraft: boolean;
  showConfirm: boolean;
  onConfirmDelete: () => void;
  onCancelConfirm: () => void;
  onStartConfirm: () => void;
  onSave?: () => void;
}

export default function ConfirmDeleteBar({
  isDraft,
  showConfirm,
  onConfirmDelete,
  onCancelConfirm,
  onStartConfirm,
  onSave,
}: ConfirmDeleteBarProps) {
  return (
    <div className="mt-8 flex flex-wrap items-center gap-3 border-t border-border pt-6">
      {isDraft && onSave && (
        <Button size="sm" onClick={onSave}>
          Save
        </Button>
      )}
      {showConfirm ? (
        <>
          <span className="text-sm text-muted-foreground">
            {isDraft
              ? "Discard this draft?"
              : "Delete this case permanently?"}
          </span>
          <Button
            variant="destructive"
            size="sm"
            onClick={onConfirmDelete}
          >
            {isDraft ? "Yes, discard" : "Yes, delete"}
          </Button>
          <Button variant="secondary" size="sm" onClick={onCancelConfirm}>
            Cancel
          </Button>
        </>
      ) : (
        <Button
          variant="outline"
          size="sm"
          onClick={onStartConfirm}
          className="hover:border-destructive/50 hover:bg-destructive/10 hover:text-destructive"
        >
          {isDraft ? "Discard draft" : "Delete case"}
        </Button>
      )}
    </div>
  );
}
