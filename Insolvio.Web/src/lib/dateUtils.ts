import { format } from "date-fns";

/** Capitalize first letter of each word (e.g. "next hearing" → "Next Hearing"). */
export function toTitleCase(s: string): string {
  return (s || "")
    .split(/\s+/)
    .map((w) => w.charAt(0).toUpperCase() + w.slice(1).toLowerCase())
    .join(" ");
}

/** Format a date-only string (YYYY-MM-DD) or ISO string for display as DD.MM.YYYY. */
export function formatAlertDate(dateString: string): string {
  const d = new Date(
    dateString.includes("T") ? dateString : `${dateString}T12:00:00`,
  );
  return format(d, "dd.MM.yyyy");
}

/** Format an ISO date string for edit history (date + time). */
export function formatEditDate(iso: string): string {
  const d = new Date(iso);
  return (
    d.toLocaleDateString("en-GB", {
      day: "numeric",
      month: "short",
      year: "numeric",
    }) +
    " at " +
    d.toLocaleTimeString("en-GB", { hour: "2-digit", minute: "2-digit" })
  );
}

/** Format an ISO date string for date-only display. */
export function formatDate(iso: string): string {
  const d = new Date(iso);
  return d.toLocaleDateString("en-GB", {
    day: "numeric",
    month: "short",
    year: "numeric",
  });
}

/** Format an ISO date string for date + time display (consistent capitalization). */
export function formatDateTime(iso: string): string {
  const d = new Date(iso);
  return format(d, "d MMM yyyy, HH:mm");
}

/** Format an ISO date string as YYYY-MM-DD (date only, no time). */
export function formatDateOnly(iso: string): string {
  const d = new Date(iso.includes("T") ? iso : `${iso}T12:00:00`);
  return format(d, "yyyy-MM-dd");
}
