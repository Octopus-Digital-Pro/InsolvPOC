import {useState, useRef, useEffect} from "react";
import {format} from "date-fns";
import {DatePicker} from "@/components/ui/date-picker";
import type {Company, ContractCase} from "../types";
import {USERS, type User} from "../types";
import DocumentCard from "./DocumentCard";

/** Format a date-only string (YYYY-MM-DD) or ISO string for display as DD.MM.YYYY. */
function formatAlertDate(dateString: string): string {
  const d = new Date(
    dateString.includes("T") ? dateString : `${dateString}T12:00:00`,
  );
  return format(d, "dd.MM.yyyy");
}

interface CompanyDetailViewProps {
  company: Company | null;
  cases: ContractCase[];
  activeCaseId: string | null;
  onSelectCase: (id: string) => void;
  onBack: () => void;
  onUpdateCompany?: (id: string, updates: Partial<Company>) => void;
  onUpdateCase?: (id: string, updates: Partial<ContractCase>) => void;
}

export default function CompanyDetailView({
  company,
  cases,
  activeCaseId,
  onSelectCase,
  onBack,
  onUpdateCompany,
  onUpdateCase,
}: CompanyDetailViewProps) {
  const [open, setOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);

  const sortedCases = [...cases].sort(
    (a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime(),
  );
  /** Case used for due date / notification (active when selected, else first). */
  const focusedCase =
    activeCaseId != null
      ? sortedCases.find((c) => c.id === activeCaseId)
      : sortedCases[0] ?? null;
  const dueDateDisplay = focusedCase?.contractDate;
  const alertAt = focusedCase?.alertAt;
  const handleSetAlert = (iso: string | undefined) => {
    if (focusedCase && onUpdateCase) {
      onUpdateCase(focusedCase.id, {alertAt: iso ?? undefined});
    }
  };

  const selectedUser: User | null =
    company?.assignedTo != null
      ? USERS.find((u) => u.id === company.assignedTo) ?? null
      : null;

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

  const handleSelectAssignee = (userId: string | null) => {
    if (company && onUpdateCompany) {
      onUpdateCompany(company.id, {assignedTo: userId ?? undefined});
    }
    setOpen(false);
  };


  return (
    <div className="mx-auto max-w-3xl pb-12">
      <button
        onClick={onBack}
        type="button"
        className="mb-4 flex items-center gap-1.5 text-sm text-gray-400 hover:text-blue-500 transition-colors"
      >
        <svg
          className="h-4 w-4"
          fill="none"
          viewBox="0 0 24 24"
          stroke="currentColor"
          strokeWidth={2}
        >
          <path
            strokeLinecap="round"
            strokeLinejoin="round"
            d="M15 19l-7-7 7-7"
          />
        </svg>
        Back to companies
      </button>

      <div className="mb-6 rounded-xl border border-gray-100 bg-white p-4">
        <h1 className="text-xl font-bold text-gray-900">
          {company ? company.name : "No company"}
        </h1>
        {company && (
          <>
            {company.cuiRo && (
              <p className="mt-1 text-sm text-gray-600">
                CUI/RO: {company.cuiRo}
              </p>
            )}
            {company.address && (
              <p className="mt-0.5 text-sm text-gray-600">{company.address}</p>
            )}
          </>
        )}
        <div
          className="my-8 flex flex-row  gap-x-6 gap-y-2 justify-between"
          ref={containerRef}
        >
          <div className="flex flex-row items-center gap-6">
            {/* Assigner */}
            <div className="flex flex-row align-center items-center gap-x-2">
              <label className="text-xs  font-semibold uppercase tracking-wide text-gray-400">
                Assigned to
              </label>
              <div className="relative">
                <button
                  type="button"
                  onClick={() => setOpen((o: boolean) => !o)}
                  className="flex items-center gap-2 rounded-lg border cursor-pointer border-gray-200 bg-white px-2.5 py-1.5 text-left text-sm transition-colors hover:border-gray-300 hover:bg-gray-50 focus:border-blue-300 focus:outline-none focus:ring-1 focus:ring-blue-300"
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
                      <span className="font-medium text-gray-800">
                        {selectedUser.name}
                      </span>
                    </>
                  ) : (
                    <span className="text-gray-400">— Unassigned —</span>
                  )}
                  <svg
                    className={`h-4 w-4 shrink-0 text-gray-400 transition-transform ${open === true ? "rotate-180" : ""}`}
                    fill="none"
                    viewBox="0 0 24 24"
                    stroke="currentColor"
                    strokeWidth={2}
                  >
                    <path
                      strokeLinecap="round"
                      strokeLinejoin="round"
                      d="M19 9l-7 7-7-7"
                    />
                  </svg>
                </button>
                {open ? (
                  <div
                    className="absolute left-0 top-full z-10 mt-1 w-max max-w-48 rounded-xl border border-gray-200 bg-white py-1 shadow-lg"
                    role="listbox"
                  >
                    <button
                      type="button"
                      role="option"
                      onClick={() => handleSelectAssignee(null)}
                      className={`flex w-full items-center gap-3 px-4 py-2.5 text-left text-sm transition-colors hover:bg-gray-50 ${
                        !selectedUser
                          ? "bg-blue-50 text-blue-800"
                          : "text-gray-700"
                      }`}
                    >
                      <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-gray-100 text-xs text-gray-400">
                        👨🏻‍💼
                      </span>
                      <span className={!selectedUser ? "font-medium" : ""}>
                        Unassigned
                      </span>
                    </button>
                    {USERS.map((u) => (
                      <button
                        key={u.id}
                        type="button"
                        role="option"
                        onClick={() => handleSelectAssignee(u.id)}
                        className={`flex w-full items-center cursor-pointer gap-3 px-4 py-2.5 text-left text-sm transition-colors hover:bg-gray-50 ${
                          selectedUser?.id === u.id
                            ? "bg-blue-50 text-blue-800"
                            : "text-gray-700"
                        }`}
                      >
                        <img
                          src={u.avatar}
                          alt=""
                          className="h-7 w-7 shrink-0 rounded-full object-cover"
                        />
                        <div>
                          <p
                            className={`font-medium ${selectedUser?.id === u.id ? "text-blue-800" : "text-gray-800"}`}
                          >
                            {u.name}
                          </p>
                          <p className="text-xs text-gray-400">{u.role}</p>
                        </div>
                      </button>
                    ))}
                  </div>
                ) : null}
              </div>
            </div>
            {/* Due date (from focused case, same as CaseDetail) */}
            <div className="flex items-center gap-2">
              <label className="text-xs font-semibold uppercase tracking-wide text-gray-400">
                Due date
              </label>
              <span className="text-sm text-gray-800">
                {dueDateDisplay && dueDateDisplay !== "Not found"
                  ? dueDateDisplay
                  : "—"}
              </span>
            </div>
          </div>
          {/* Notification (from focused case, same as CaseDetail) */}
          <div className="flex items-center gap-2">
            <label className="text-xs font-semibold uppercase tracking-wide text-gray-400">
              Notification
            </label>
            {alertAt ? (
              <div className="flex items-center gap-2">
                <span className="text-sm text-gray-800">
                  {formatAlertDate(alertAt)}
                </span>
                <button
                  type="button"
                  onClick={() => handleSetAlert(undefined)}
                  className="text-xs text-gray-400 hover:text-red-600 cursor-pointer underline"
                >
                  Clear
                </button>
              </div>
            ) : (
              <DatePicker
                date={undefined}
                onSelect={(d: Date | undefined) => {
                  if (d) handleSetAlert(format(d, "yyyy-MM-dd"));
                }}
                placeholder="Pick a date"
                className="min-w-40"
              />
            )}
          </div>
        </div>
      </div>

      <h2 className="mb-3 text-xs font-semibold uppercase tracking-wide text-gray-500">
        Documents ({sortedCases.length})
      </h2>
      {sortedCases.length === 0 ? (
        <p className="py-6 text-sm text-gray-400">No documents attached.</p>
      ) : (
        <div className="space-y-2">
          {sortedCases.map((c) => (
            <DocumentCard
              key={c.id}
              contractCase={c}
              isActive={c.id === activeCaseId}
              onClick={() => onSelectCase(c.id)}
            />
          ))}
        </div>
      )}
    </div>
  );
}
