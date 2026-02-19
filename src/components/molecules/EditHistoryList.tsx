import type {EditHistoryEntry} from "@/types";
import {formatEditDate} from "@/lib/dateUtils";

interface EditHistoryListProps {
  editHistory: EditHistoryEntry[] | undefined;
}

export default function EditHistoryList({editHistory}: EditHistoryListProps) {
  const entries = editHistory ?? [];
  return (
    <details className="mt-4 rounded-lg border border-border bg-muted/30" open={false}>
      <summary className="cursor-pointer px-4 py-3 text-xs font-medium text-muted-foreground hover:text-foreground">
        Edit history
        {entries.length > 0 && (
          <span className="ml-2 text-muted-foreground">({entries.length})</span>
        )}
      </summary>
      <div className="border-t border-border px-4 pb-4 pt-2">
        {entries.length === 0 ? (
          <p className="text-xs italic text-muted-foreground">No edits yet.</p>
        ) : (
          <ul className="space-y-3">
            {entries.map((entry, i) => (
              <li
                key={`${entry.at}-${entry.field}-${i}`}
                className="flex gap-3 text-sm"
              >
                <span className="shrink-0 text-xs tabular-nums text-muted-foreground">
                  {formatEditDate(entry.at)}
                </span>
                <span className="text-muted-foreground">
                  <span className="font-medium text-foreground">{entry.by}</span>
                  {" changed "}
                  <span className="font-medium text-foreground">{entry.field}</span>
                  {entry.oldValue !== undefined && entry.newValue !== undefined ? (
                    <>
                      {" from "}
                      <span
                        className="max-w-48 line-clamp-1 align-middle text-muted-foreground"
                        title={entry.oldValue}
                      >
                        {entry.oldValue || "—"}
                      </span>
                      {" to "}
                      <span
                        className="max-w-48 line-clamp-1 align-middle text-foreground"
                        title={entry.newValue}
                      >
                        {entry.newValue || "—"}
                      </span>
                    </>
                  ) : entry.newValue ? (
                    <>
                      {" "}
                      to{" "}
                      {entry.newValue.length > 80
                        ? entry.newValue.slice(0, 80) + "…"
                        : entry.newValue}
                    </>
                  ) : (
                    " (cleared)"
                  )}
                </span>
              </li>
            ))}
          </ul>
        )}
      </div>
    </details>
  );
}
