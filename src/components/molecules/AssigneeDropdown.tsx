interface AssigneeDropdownProps {
  dueDateDisplay?: string;
}

export default function AssigneeDropdown({
  dueDateDisplay,
}: AssigneeDropdownProps) {
  return (
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
  );
}
