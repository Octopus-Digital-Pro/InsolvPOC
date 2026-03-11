import {useState, useRef, useEffect} from "react";
import type {User} from "@/types";
import {ChevronDown, Check, X} from "lucide-react";

interface UserSelectProps {
  users: User[];
  value: User | null;
  onChange: (userId: string | null) => void;
  label?: string;
  /** When true the label element is not rendered. Use when the parent grid handles the label. */
  hideLabel?: boolean;
}

export default function UserSelect({
  users,
  value: selectedUser,
  onChange,
  label = "Assigned to",
  hideLabel = false,
}: UserSelectProps) {
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState("");
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    const handleClickOutside = (e: MouseEvent) => {
      if (
        containerRef.current &&
        !containerRef.current.contains(e.target as Node)
      ) {
        setOpen(false);
        setSearch("");
      }
    };
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, [open]);

  const handleSelect = (userId: string | null) => {
    onChange(userId);
    setOpen(false);
    setSearch("");
  };

  const filteredUsers = search.trim()
    ? users.filter(u => u.name.toLowerCase().includes(search.toLowerCase()))
    : users;

  return (
    <div ref={containerRef} className={hideLabel ? undefined : "space-y-1.5"}>
      {!hideLabel && (
        <label className="block text-xs font-semibold uppercase tracking-wide text-muted-foreground">
          {label}
        </label>
      )}
      <div className="relative">
        {/* Trigger */}
        <button
          type="button"
          onClick={() => { setOpen((o) => !o); if (!open) setSearch(""); }}
          className="flex w-full cursor-pointer items-center justify-between gap-2 rounded-md border border-input bg-background px-3 py-2 text-left text-sm transition-colors hover:bg-accent/40 focus:outline-none focus:ring-2 focus:ring-ring"
          aria-expanded={open}
          aria-haspopup="listbox"
        >
          {selectedUser ? (
            <div className="flex items-center gap-2 min-w-0">
              <img
                src={selectedUser.avatar}
                alt=""
                className="h-6 w-6 shrink-0 rounded-full object-cover"
              />
              <span className="font-medium text-foreground truncate">
                {selectedUser.name}
              </span>
            </div>
          ) : (
            <span className="text-muted-foreground">Select a user…</span>
          )}
          <div className="flex items-center gap-1 shrink-0 ml-2">
            {selectedUser && (
              <span
                role="button"
                tabIndex={0}
                onClick={e => { e.stopPropagation(); handleSelect(null); }}
                onKeyDown={e => { if (e.key === "Enter" || e.key === " ") { e.preventDefault(); handleSelect(null); } }}
                className="rounded p-0.5 text-muted-foreground hover:bg-destructive/10 hover:text-destructive cursor-pointer transition-colors"
                aria-label="Clear assignment"
              >
                <X className="h-3.5 w-3.5" />
              </span>
            )}
            <ChevronDown
              className={`h-4 w-4 shrink-0 text-muted-foreground transition-transform duration-150 ${open ? "rotate-180" : ""}`}
            />
          </div>
        </button>

        {/* Dropdown */}
        {open && (
          <div
            className="absolute left-0 right-0 top-full z-10 mt-1 rounded-md border border-border bg-popover shadow-lg overflow-hidden"
            role="listbox"
          >
            {/* Inline search */}
            <div className="border-b border-border p-2">
              <input
                type="text"
                value={search}
                onChange={e => setSearch(e.target.value)}
                placeholder="Search users…"
                autoFocus
                className="w-full rounded border border-input bg-background px-2.5 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
              />
            </div>
            <div className="max-h-52 overflow-y-auto py-1">
              {filteredUsers.length === 0 ? (
                <p className="px-3 py-3 text-sm text-center text-muted-foreground">No users found</p>
              ) : (
                filteredUsers.map((u) => (
                  <button
                    key={u.id}
                    type="button"
                    role="option"
                    aria-selected={selectedUser?.id === u.id}
                    onClick={() => handleSelect(u.id)}
                    className={`flex w-full cursor-pointer items-center gap-3 px-3 py-2 text-left text-sm transition-colors hover:bg-accent ${
                      selectedUser?.id === u.id
                        ? "bg-accent/60 text-accent-foreground"
                        : "text-popover-foreground"
                    }`}
                  >
                    <img
                      src={u.avatar}
                      alt=""
                      className="h-7 w-7 shrink-0 rounded-full object-cover"
                    />
                    <div className="min-w-0 flex-1">
                      <p
                        className={`truncate font-medium ${selectedUser?.id === u.id ? "text-accent-foreground" : "text-foreground"}`}
                      >
                        {u.name}
                      </p>
                      <p className="text-xs text-muted-foreground truncate">{u.role}</p>
                    </div>
                    {selectedUser?.id === u.id && (
                      <Check className="h-3.5 w-3.5 text-primary shrink-0" />
                    )}
                  </button>
                ))
              )}
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
