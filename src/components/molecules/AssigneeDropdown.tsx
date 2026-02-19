import {format} from "date-fns";
import {DatePicker} from "@/components/ui/date-picker";
import {formatAlertDate} from "@/lib/dateUtils";

interface AssigneeDropdownProps {
  dueDateDisplay?: string;
  alertAt?: string;
  onSetAlert?: (iso: string | undefined) => void;
}

export default function AssigneeDropdown({
  dueDateDisplay,
  alertAt,
  onSetAlert,
}: AssigneeDropdownProps) {
  return (
    <>
      <div className="flex items-center gap-2">
        <label className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
          Due date
        </label>
        <span className="text-sm text-foreground">
          {dueDateDisplay && dueDateDisplay !== "Not found"
            ? dueDateDisplay
            : "—"}
        </span>
      </div>
      <div className="flex items-center gap-2">
        <label className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
          Notification
        </label>
        {alertAt ? (
          <div className="flex items-center gap-2">
            <span className="text-sm text-foreground">
              {formatAlertDate(alertAt)}
            </span>
            <button
              type="button"
              onClick={() => onSetAlert?.(undefined)}
              className="text-xs text-muted-foreground hover:text-destructive underline"
            >
              Clear
            </button>
          </div>
        ) : (
          <DatePicker
            date={undefined}
            onSelect={(d) => {
              if (d) onSetAlert?.(format(d, "yyyy-MM-dd"));
            }}
            placeholder="Pick a date"
            className="min-w-40"
          />
        )}
      </div>
    </>
  );
}
