import { useState, useEffect, useRef } from "react";
import { useNavigate } from "react-router-dom";
import { tasksApi } from "@/services/api";
import { useTranslation } from "@/contexts/LanguageContext";
import type { TaskDto, TaskNoteDto } from "@/services/api/types";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  X, Building2, FolderOpen, AlertCircle, CheckCircle2, Clock,
  AlertTriangle, Ban, Edit2, Trash2, Plus, Save, Loader2,
} from "lucide-react";
import { format } from "date-fns";

interface Props {
  taskId: string | null;
  onClose: () => void;
  onStatusChanged?: (taskId: string, newStatus: string) => void;
  readOnly?: boolean;
}

const STATUS_OPTIONS = [
  { value: "open", icon: Clock, color: "text-blue-500" },
  { value: "inProgress", icon: AlertTriangle, color: "text-amber-500" },
  { value: "blocked", icon: Ban, color: "text-red-500" },
  { value: "done", icon: CheckCircle2, color: "text-green-500" },
] as const;

const STATUS_BADGE: Record<string, "default" | "warning" | "destructive" | "success"> = {
  open: "default",
  inProgress: "warning",
  blocked: "destructive",
  done: "success",
};

function Note({
  note,
  onEdit,
  onDelete,
  readOnly = false,
}: { note: TaskNoteDto; onEdit: (n: TaskNoteDto) => void; onDelete: (id: string) => void; readOnly?: boolean }) {
  return (
    <div className="group flex gap-3 rounded-lg border border-border bg-muted/30 p-3 text-sm">
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-2 mb-1 text-xs text-muted-foreground">
          <span className="font-medium text-foreground">{note.createdByName}</span>
          <span>·</span>
          <span>{format(new Date(note.createdOn), "dd MMM yyyy HH:mm")}</span>
          {note.updatedOn && <span className="italic">(edited)</span>}
        </div>
        <p className="whitespace-pre-wrap break-words text-foreground">{note.content}</p>
      </div>
      {!readOnly && (
      <div className="flex flex-col gap-1 opacity-0 group-hover:opacity-100 transition-opacity">
        <button onClick={() => onEdit(note)}
          className="p-1 rounded hover:bg-accent text-muted-foreground hover:text-foreground">
          <Edit2 className="h-3.5 w-3.5" />
        </button>
        <button onClick={() => onDelete(note.id)}
          className="p-1 rounded hover:bg-destructive/10 text-muted-foreground hover:text-destructive">
          <Trash2 className="h-3.5 w-3.5" />
        </button>
      </div>
      )}
    </div>
  );
}

export default function TaskDetailModal({ taskId, onClose, onStatusChanged, readOnly = false }: Props) {
  const navigate = useNavigate();
  const { t } = useTranslation();
  const [task, setTask] = useState<TaskDto | null>(null);
  const [notes, setNotes] = useState<TaskNoteDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [noteText, setNoteText] = useState("");
  const [editingNote, setEditingNote] = useState<TaskNoteDto | null>(null);
  const [editText, setEditText] = useState("");
  const [savingNote, setSavingNote] = useState(false);
  const [showBlockModal, setShowBlockModal] = useState(false);
  const [blockReason, setBlockReason] = useState("");
  const [reportSummaryText, setReportSummaryText] = useState("");
  const [savingReportSummary, setSavingReportSummary] = useState(false);
  const textareaRef = useRef<HTMLTextAreaElement>(null);

  useEffect(() => {
    if (!taskId) return;
    setLoading(true);
    Promise.all([
      tasksApi.getById(taskId),
      tasksApi.getNotes(taskId),
    ]).then(([taskRes, notesRes]) => {
      setTask(taskRes.data);
      setReportSummaryText(taskRes.data.reportSummary ?? "");
      setNotes(notesRes.data);
    }).finally(() => setLoading(false));
  }, [taskId]);

  if (!taskId) return null;

  const handleStatusChange = async (newStatus: string) => {
    if (!task) return;
    if (newStatus === "blocked") {
      setShowBlockModal(true);
      return;
    }
    try {
      const res = await tasksApi.update(task.id, { status: newStatus });
      setTask(res.data);
      onStatusChanged?.(task.id, newStatus);
    } catch (_) { /* ignored */ }
  };

  const handleBlockConfirm = async () => {
    if (!task || !blockReason.trim()) return;
    try {
      const res = await tasksApi.update(task.id, { status: "blocked", blockReason: blockReason.trim() });
      setTask(res.data);
      onStatusChanged?.(task.id, "blocked");
      setShowBlockModal(false);
      setBlockReason("");
    } catch (_) { /* ignored */ }
  };

  const handleAddNote = async () => {
    if (!taskId || !noteText.trim()) return;
    setSavingNote(true);
    try {
      const res = await tasksApi.addNote(taskId, noteText.trim());
      setNotes(prev => [...prev, res.data]);
      setNoteText("");
    } finally { setSavingNote(false); }
  };

  const handleUpdateNote = async () => {
    if (!editingNote || !editText.trim()) return;
    setSavingNote(true);
    try {
      const res = await tasksApi.updateNote(editingNote.taskId, editingNote.id, editText.trim());
      setNotes(prev => prev.map(n => n.id === editingNote.id ? res.data : n));
      setEditingNote(null);
      setEditText("");
    } finally { setSavingNote(false); }
  };

  const handleSaveReportSummary = async () => {
    if (!task) return;
    setSavingReportSummary(true);
    try {
      const res = await tasksApi.update(task.id, { reportSummary: reportSummaryText.trim() || null });
      setTask(res.data);
    } finally { setSavingReportSummary(false); }
  };

  const handleDeleteNote = async (noteId: string) => {    if (!taskId) return;
    const note = notes.find(n => n.id === noteId);
    if (!note) return;
    await tasksApi.deleteNote(taskId, noteId);
    setNotes(prev => prev.filter(n => n.id !== noteId));
  };

  const startEdit = (note: TaskNoteDto) => {
    setEditingNote(note);
    setEditText(note.content);
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4" onClick={onClose}>
      <div
        className="relative w-full max-w-2xl max-h-[90vh] overflow-y-auto rounded-xl border border-border bg-card shadow-2xl flex flex-col"
        onClick={e => e.stopPropagation()}
      >
        {/* Block Reason overlay */}
        {showBlockModal && (
          <div className="absolute inset-0 z-10 flex items-center justify-center bg-black/50 rounded-xl p-4">
            <div className="w-full max-w-sm bg-card border border-border rounded-xl p-5 space-y-3 shadow-xl">
              <h3 className="flex items-center gap-2 text-sm font-semibold">
                <Ban className="h-4 w-4 text-destructive" /> {t.tasks.blockReasonLabel}
              </h3>
              <textarea
                value={blockReason}
                onChange={e => setBlockReason(e.target.value)}
                placeholder={t.tasks.blockReasonPlaceholder}
                rows={3}
                className="w-full rounded-lg border border-input bg-background px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring resize-none"
              />
              <div className="flex justify-end gap-2">
                <Button variant="outline" size="sm" onClick={() => setShowBlockModal(false)}>{t.common.cancel}</Button>
                <Button size="sm" className="bg-destructive hover:bg-destructive/90" disabled={!blockReason.trim()} onClick={handleBlockConfirm}>
                  <Ban className="h-3 w-3 mr-1" /> Block
                </Button>
              </div>
            </div>
          </div>
        )}

        {/* Header */}
        <div className="flex items-start justify-between gap-3 p-5 border-b border-border">
          <div className="flex-1 min-w-0">
            {loading ? (
              <div className="h-6 w-48 bg-muted animate-pulse rounded" />
            ) : (
              <h2 className="text-base font-semibold text-foreground leading-snug">{task?.title}</h2>
            )}
            <div className="flex items-center gap-2 mt-1.5 flex-wrap">
              {task && (
                <Badge variant={STATUS_BADGE[task.status] ?? "default"} className="text-xs capitalize">
                  {t.tasks[task.status as keyof typeof t.tasks] ?? task.status}
                </Badge>
              )}
              {task?.category && (
                <span className="text-xs text-muted-foreground bg-muted px-2 py-0.5 rounded">{task.category}</span>
              )}
              {task?.deadline && (
                <span className="text-xs text-muted-foreground">{format(new Date(task.deadline), "dd MMM yyyy")}</span>
              )}
            </div>
          </div>
          <button onClick={onClose} className="p-1.5 rounded-lg hover:bg-accent text-muted-foreground hover:text-foreground flex-shrink-0">
            <X className="h-4 w-4" />
          </button>
        </div>

        {/* Action buttons */}
        <div className="flex items-center gap-2 px-5 py-3 border-b border-border flex-wrap">
          {task?.caseId && (
            <Button variant="outline" size="sm" className="text-xs gap-1.5"
              onClick={() => { onClose(); navigate(`/cases/${task.caseId}`); }}>
              <FolderOpen className="h-3.5 w-3.5" /> {t.tasks.goToCase}
            </Button>
          )}
          {task?.companyId && (
            <Button variant="outline" size="sm" className="text-xs gap-1.5"
              onClick={() => { onClose(); navigate(`/companies/${task.companyId}`); }}>
              <Building2 className="h-3.5 w-3.5" /> {t.tasks.goToCompany}
            </Button>
          )}
          <div className="flex items-center gap-1 ml-auto">
            <span className="text-xs text-muted-foreground mr-1">{t.tasks.setStatus}:</span>
            {STATUS_OPTIONS.map(({ value, icon: Icon, color }) => (
              <button
                key={value}
                title={t.tasks[value as keyof typeof t.tasks] as string}
                onClick={() => !readOnly && handleStatusChange(value)}
                disabled={readOnly}
                className={`p-1.5 rounded hover:bg-accent transition-colors ${task?.status === value ? color : "text-muted-foreground hover:text-foreground"} ${readOnly ? "opacity-40 cursor-not-allowed" : ""}`}
              >
                <Icon className="h-4 w-4" />
              </button>
            ))}
          </div>
        </div>

        {/* Body */}
        <div className="flex-1 p-5 space-y-5 overflow-y-auto">
          {/* Task details */}
          {task && (
            <dl className="grid grid-cols-2 gap-x-6 gap-y-2 text-sm">
              {task.assignedToName && (
                <>
                  <dt className="text-muted-foreground">{t.tasks.assignedTo}</dt>
                  <dd className="font-medium text-foreground">{task.assignedToName}</dd>
                </>
              )}
              {task.labels && (
                <>
                  <dt className="text-muted-foreground">{t.tasks.labels}</dt>
                  <dd>
                    {task.labels.split(",").map(l => (
                      <span key={l.trim()} className="inline-block mr-1 mb-0.5 px-2 py-0.5 text-xs bg-primary/10 text-primary rounded-full">{l.trim()}</span>
                    ))}
                  </dd>
                </>
              )}
              {task.caseNumber && (
                <>
                  <dt className="text-muted-foreground">Case #</dt>
                  <dd className="font-medium text-foreground">{task.caseNumber}</dd>
                </>
              )}
              {task.description && (
                <>
                  <dt className="text-muted-foreground col-span-2">{t.tasks.description}</dt>
                  <dd className="col-span-2 text-foreground bg-muted/30 rounded-lg p-3 text-xs whitespace-pre-wrap">{task.description}</dd>
                </>
              )}
              {task.blockReason && (
                <>
                  <dt className="text-muted-foreground col-span-2 flex items-center gap-1">
                    <AlertCircle className="h-3.5 w-3.5 text-destructive" /> {t.tasks.blockReasonLabel}
                  </dt>
                  <dd className="col-span-2 text-destructive bg-destructive/5 rounded-lg p-3 text-xs">{task.blockReason}</dd>
                </>
              )}
            </dl>
          )}

          {/* Report Summary section — visible for done/inProgress tasks */}
          {task && (task.status === "done" || task.status === "inProgress") && (
            <div className="space-y-2">
              <h3 className="text-sm font-semibold text-foreground">{t.tasks.reportSummaryLabel}</h3>
              <p className="text-xs text-muted-foreground">{t.tasks.reportSummaryHelp}</p>
              <textarea
                value={reportSummaryText}
                onChange={e => !readOnly && setReportSummaryText(e.target.value)}
                readOnly={readOnly}
                rows={3}
                placeholder={t.tasks.reportSummaryPlaceholder}
                className={`w-full rounded-lg border border-input bg-background px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring resize-y ${readOnly ? "opacity-60 cursor-not-allowed" : ""}`}
              />
              {!readOnly && (
              <div className="flex justify-end">
                <Button size="sm" disabled={savingReportSummary} onClick={handleSaveReportSummary}>
                  {savingReportSummary ? <Loader2 className="h-3 w-3 animate-spin mr-1" /> : null}
                  {t.tasks.saveChanges}
                </Button>
              </div>
              )}
            </div>
          )}

          {/* Notes section */}
          <div>
            <h3 className="text-sm font-semibold text-foreground mb-3">{t.tasks.notes}</h3>
            {loading ? (
              <div className="space-y-2">
                {[1, 2].map(i => <div key={i} className="h-16 bg-muted animate-pulse rounded-lg" />)}
              </div>
            ) : notes.length === 0 ? (
              <p className="text-xs text-muted-foreground italic py-2">{t.tasks.noNotes}</p>
            ) : (
              <div className="space-y-2">
                {notes.map(note =>
                  editingNote?.id === note.id ? (
                    <div key={note.id} className="rounded-lg border border-primary/40 bg-muted/30 p-3 space-y-2">
                      <textarea
                        value={editText}
                        onChange={e => setEditText(e.target.value)}
                        rows={3}
                        className="w-full text-sm rounded border border-input bg-background px-3 py-2 focus:outline-none focus:ring-2 focus:ring-ring resize-none"
                      />
                      <div className="flex gap-2 justify-end">
                        <Button variant="outline" size="sm" className="text-xs h-7"
                          onClick={() => { setEditingNote(null); setEditText(""); }}>
                          {t.common.cancel}
                        </Button>
                        <Button size="sm" className="text-xs h-7 gap-1" disabled={savingNote || !editText.trim()}
                          onClick={handleUpdateNote}>
                          {savingNote ? <Loader2 className="h-3 w-3 animate-spin" /> : <Save className="h-3 w-3" />}
                          {t.tasks.saveChanges}
                        </Button>
                      </div>
                    </div>
                  ) : (
                    <Note key={note.id} note={note} onEdit={readOnly ? () => {} : startEdit} onDelete={readOnly ? () => {} : handleDeleteNote} readOnly={readOnly} />
                  )
                )}
              </div>
            )}

            {/* Add note */}
            {!editingNote && !readOnly && (
              <div className="mt-3 space-y-2">
                <textarea
                  ref={textareaRef}
                  value={noteText}
                  onChange={e => setNoteText(e.target.value)}
                  placeholder={t.tasks.notePlaceholder}
                  rows={3}
                  className="w-full text-sm rounded-lg border border-input bg-background px-3 py-2 focus:outline-none focus:ring-2 focus:ring-ring resize-none"
                />
                <div className="flex justify-end">
                  <Button size="sm" className="text-xs gap-1.5" disabled={savingNote || !noteText.trim()}
                    onClick={handleAddNote}>
                    {savingNote ? <Loader2 className="h-3 w-3 animate-spin" /> : <Plus className="h-3 w-3" />}
                    {t.tasks.addNote}
                  </Button>
                </div>
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
