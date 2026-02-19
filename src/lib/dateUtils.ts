import { format } from "date-fns";

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

/** Format an ISO date string for date + time display. */
export function formatDateTime(iso: string): string {
  const d = new Date(iso);
  return (
    d.toLocaleDateString("en-GB", {
      day: "numeric",
      month: "short",
      year: "numeric",
    }) +
    ", " +
    d.toLocaleTimeString("en-GB", { hour: "2-digit", minute: "2-digit" })
  );
}
