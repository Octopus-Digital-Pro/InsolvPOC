import {useState} from "react";
import type {FieldEdit} from "@/types";
import {formatEditDate} from "@/lib/dateUtils";
import {Button} from "@/components/ui/button";
import {Pencil} from "lucide-react";

interface EditableFieldProps {
  label: string;
  value: string;
  fieldKey: string;
  multiline?: boolean;
  editInfo?: FieldEdit;
  onSave: (key: string, value: string) => void;
}

export default function EditableField({
  label,
  value,
  fieldKey,
  multiline,
  editInfo,
  onSave,
}: EditableFieldProps) {
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState(value);

  const handleSave = () => {
    onSave(fieldKey, draft);
    setEditing(false);
  };

  const handleCancel = () => {
    setDraft(value);
    setEditing(false);
  };

  return (
    <div className="group rounded-lg border border-border bg-card p-4 transition-colors hover:border-border">
      <div className="mb-1.5 flex items-center justify-between">
        <label className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
          {label}
        </label>
        {!editing && (
          <Button
            variant="ghost"
            size="sm"
            onClick={() => setEditing(true)}
            className="h-auto px-0 py-0 text-xs opacity-0 transition-opacity group-hover:opacity-100 hover:text-primary"
          >
            <Pencil className="mr-1 h-3 w-3" />
            Edit
          </Button>
        )}
      </div>

      {editing ? (
        <div>
          {multiline ? (
            <textarea
              value={draft}
              onChange={(e) => setDraft(e.target.value)}
              rows={4}
              className="w-full rounded-md border border-input bg-background p-2 text-sm text-foreground focus:border-ring focus:outline-none focus:ring-1 focus:ring-ring"
              autoFocus
            />
          ) : (
            <input
              type="text"
              value={draft}
              onChange={(e) => setDraft(e.target.value)}
              className="w-full rounded-md border border-input bg-background p-2 text-sm text-foreground focus:border-ring focus:outline-none focus:ring-1 focus:ring-ring"
              autoFocus
            />
          )}
          <div className="mt-2 flex gap-2">
            <Button size="sm" onClick={handleSave}>
              Save
            </Button>
            <Button variant="secondary" size="sm" onClick={handleCancel}>
              Cancel
            </Button>
          </div>
        </div>
      ) : (
        <>
          <p className="whitespace-pre-wrap text-sm text-card-foreground">
            {value || <span className="italic text-muted-foreground">Empty</span>}
          </p>
          {editInfo && (
            <p className="mt-1.5 text-[11px] italic text-muted-foreground">
              Edited by {editInfo.editedBy} on {formatEditDate(editInfo.editedAt)}
            </p>
          )}
        </>
      )}
    </div>
  );
}
