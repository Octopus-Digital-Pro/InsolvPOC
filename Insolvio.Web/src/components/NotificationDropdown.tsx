import { useState, useEffect, useRef, useCallback } from "react";
import { useNavigate } from "react-router-dom";
import { notificationsApi, type NotificationDto } from "@/services/api/notifications";

const POLL_INTERVAL = 30_000; // 30 seconds

/** Inline bell icon (Heroicons outline, 20x20) */
function BellIcon({ className }: { className?: string }) {
  return (
    <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor" className={className ?? "h-5 w-5"}>
      <path strokeLinecap="round" strokeLinejoin="round" d="M14.857 17.082a23.848 23.848 0 0 0 5.454-1.31A8.967 8.967 0 0 1 18 9.75V9A6 6 0 0 0 6 9v.75a8.967 8.967 0 0 1-2.312 6.022c1.733.64 3.56 1.085 5.455 1.31m5.714 0a24.255 24.255 0 0 1-5.714 0m5.714 0a3 3 0 1 1-5.714 0" />
    </svg>
  );
}

const categoryIcons: Record<string, string> = {
  Email: "✉️",
  Task: "📋",
  Deadline: "⏰",
  System: "⚙️",
};

function timeAgo(dateStr: string): string {
  const diff = Date.now() - new Date(dateStr).getTime();
  const mins = Math.floor(diff / 60000);
  if (mins < 1) return "just now";
  if (mins < 60) return `${mins}m ago`;
  const hrs = Math.floor(mins / 60);
  if (hrs < 24) return `${hrs}h ago`;
  const days = Math.floor(hrs / 24);
  return `${days}d ago`;
}

export default function NotificationDropdown() {
  const [open, setOpen] = useState(false);
  const [unreadCount, setUnreadCount] = useState(0);
  const [notifications, setNotifications] = useState<NotificationDto[]>([]);
  const [loading, setLoading] = useState(false);
  const dropdownRef = useRef<HTMLDivElement>(null);
  const navigate = useNavigate();

  const fetchUnreadCount = useCallback(async () => {
    try {
      const { data } = await notificationsApi.getUnreadCount();
      setUnreadCount(data.count);
    } catch { /* silent */ }
  }, []);

  // Poll unread count
  useEffect(() => {
    fetchUnreadCount();
    const id = setInterval(fetchUnreadCount, POLL_INTERVAL);
    return () => clearInterval(id);
  }, [fetchUnreadCount]);

  // Close dropdown on outside click
  useEffect(() => {
    function handleClickOutside(e: MouseEvent) {
      if (dropdownRef.current && !dropdownRef.current.contains(e.target as Node)) {
        setOpen(false);
      }
    }
    if (open) document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, [open]);

  const toggle = async () => {
    const opening = !open;
    setOpen(opening);
    if (opening) {
      setLoading(true);
      try {
        const { data } = await notificationsApi.getRecent(1, 15);
        setNotifications(data);
      } catch { /* silent */ }
      setLoading(false);
    }
  };

  const handleClick = async (n: NotificationDto) => {
    if (!n.isRead) {
      await notificationsApi.markRead(n.id);
      setUnreadCount((c) => Math.max(0, c - 1));
      setNotifications((prev) => prev.map((x) => (x.id === n.id ? { ...x, isRead: true } : x)));
    }
    setOpen(false);
    if (n.actionUrl) {
      navigate(n.actionUrl);
    } else if (n.relatedCaseId) {
      navigate(`/cases/${n.relatedCaseId}`);
    }
  };

  const handleMarkAllRead = async () => {
    await notificationsApi.markAllRead();
    setUnreadCount(0);
    setNotifications((prev) => prev.map((n) => ({ ...n, isRead: true })));
  };

  return (
    <div ref={dropdownRef} className="relative">
      <button
        type="button"
        onClick={toggle}
        className="relative rounded-md p-1.5 text-muted-foreground hover:bg-accent hover:text-foreground transition-colors"
        aria-label="Notifications"
      >
        <BellIcon />
        {unreadCount > 0 && (
          <span className="absolute -top-0.5 -right-0.5 flex h-4 min-w-4 items-center justify-center rounded-full bg-destructive px-1 text-[10px] font-bold text-destructive-foreground">
            {unreadCount > 99 ? "99+" : unreadCount}
          </span>
        )}
      </button>

      {open && (
        <div className="absolute right-0 top-full z-50 mt-2 w-80 rounded-lg border border-border bg-background shadow-lg">
          <div className="flex items-center justify-between border-b border-border px-4 py-2.5">
            <span className="text-sm font-semibold text-foreground">Notifications</span>
            {unreadCount > 0 && (
              <button
                type="button"
                onClick={handleMarkAllRead}
                className="text-xs text-primary hover:underline"
              >
                Mark all read
              </button>
            )}
          </div>

          <div className="max-h-80 overflow-y-auto">
            {loading ? (
              <div className="p-4 text-center text-sm text-muted-foreground">Loading…</div>
            ) : notifications.length === 0 ? (
              <div className="p-4 text-center text-sm text-muted-foreground">No notifications</div>
            ) : (
              notifications.map((n) => (
                <button
                  key={n.id}
                  type="button"
                  onClick={() => handleClick(n)}
                  className={`w-full text-left px-4 py-2.5 border-b border-border last:border-b-0 hover:bg-accent transition-colors ${!n.isRead ? "bg-accent/40" : ""}`}
                >
                  <div className="flex items-start gap-2">
                    <span className="mt-0.5 text-sm">{categoryIcons[n.category] ?? "📌"}</span>
                    <div className="min-w-0 flex-1">
                      <p className={`text-sm truncate ${!n.isRead ? "font-semibold text-foreground" : "text-foreground"}`}>
                        {n.title}
                      </p>
                      {n.message && (
                        <p className="text-xs text-muted-foreground truncate">{n.message}</p>
                      )}
                      <p className="text-[10px] text-muted-foreground mt-0.5">{timeAgo(n.createdAt)}</p>
                    </div>
                    {!n.isRead && (
                      <span className="mt-1.5 h-2 w-2 flex-shrink-0 rounded-full bg-primary" />
                    )}
                  </div>
                </button>
              ))
            )}
          </div>
        </div>
      )}
    </div>
  );
}
