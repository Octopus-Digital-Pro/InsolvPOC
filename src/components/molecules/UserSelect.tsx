import {useState, useRef, useEffect} from "react";
import type {User} from "@/types";
import {ChevronDown} from "lucide-react";

interface UserSelectProps {
  users: User[];
  value: User | null;
  onChange: (userId: string | null) => void;
  label?: string;
}

export default function UserSelect({
  users,
  value: selectedUser,
  onChange,
  label = "Assigned to",
}: UserSelectProps) {
  const [open, setOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    const handleClickOutside = (e: MouseEvent) => {
      if (
        containerRef.current &&
        !containerRef.current.contains(e.target as Node)
      ) {
        setOpen(false);
      }
    };
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, [open]);

  const handleSelect = (userId: string | null) => {
    onChange(userId);
    setOpen(false);
  };

  return (
    <div className="relative flex flex-row items-center gap-x-2" ref={containerRef}>
      <label className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
        {label}
      </label>
      <div className="relative">
        <button
          type="button"
          onClick={() => setOpen((o) => !o)}
          className="flex cursor-pointer items-center gap-2 rounded-lg border border-input bg-background px-2.5 py-1.5 text-left text-sm transition-colors hover:bg-accent hover:border-input focus:border-ring focus:outline-none focus:ring-1 focus:ring-ring"
          aria-expanded={open}
          aria-haspopup="listbox"
        >
          {selectedUser ? (
            <>
              <img
                src={selectedUser.avatar}
                alt=""
                className="h-6 w-6 shrink-0 rounded-full object-cover"
              />
              <span className="font-medium text-foreground">
                {selectedUser.name}
              </span>
            </>
          ) : (
            <span className="text-muted-foreground">— Unassigned —</span>
          )}
          <ChevronDown
            className={`h-4 w-4 shrink-0 text-muted-foreground transition-transform ${open ? "rotate-180" : ""}`}
          />
        </button>
        {open && (
          <div
            className="absolute left-0 top-full z-10 mt-1 w-max max-w-48 rounded-xl border border-border bg-popover py-1 shadow-lg"
            role="listbox"
          >
            <button
              type="button"
              role="option"
              onClick={() => handleSelect(null)}
              className={`flex w-full items-center gap-3 px-4 py-2.5 text-left text-sm transition-colors hover:bg-accent ${
                !selectedUser ? "bg-accent text-accent-foreground" : "text-popover-foreground"
              }`}
            >
              <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-muted text-xs text-muted-foreground">
                👨🏻‍💼
              </span>
              <span className={!selectedUser ? "font-medium" : ""}>
                Unassigned
              </span>
            </button>
            {users.map((u) => (
              <button
                key={u.id}
                type="button"
                role="option"
                onClick={() => handleSelect(u.id)}
                className={`flex w-full cursor-pointer items-center gap-3 px-4 py-2.5 text-left text-sm transition-colors hover:bg-accent ${
                  selectedUser?.id === u.id
                    ? "bg-accent text-accent-foreground"
                    : "text-popover-foreground"
                }`}
              >
                <img
                  src={u.avatar}
                  alt=""
                  className="h-7 w-7 shrink-0 rounded-full object-cover"
                />
                <div>
                  <p
                    className={`font-medium ${selectedUser?.id === u.id ? "text-accent-foreground" : "text-foreground"}`}
                  >
                    {u.name}
                  </p>
                  <p className="text-xs text-muted-foreground">{u.role}</p>
                </div>
              </button>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
