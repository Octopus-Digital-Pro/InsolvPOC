import { useState } from 'react';
import type { InsolvencyNote } from '../types';

interface NoteDetailProps {
  note: InsolvencyNote;
  onUpdate: (id: string, updates: Partial<InsolvencyNote>) => void;
  onDelete: (id: string) => void;
  onBack: () => void;
}

function EditableField({
  label,
  value,
  fieldKey,
  multiline,
  onSave,
}: {
  label: string;
  value: string;
  fieldKey: string;
  multiline?: boolean;
  onSave: (key: string, value: string) => void;
}) {
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState(value);

  const handleSave = () => {
    onSave(fieldKey, draft);
    setEditing(false);
  };

  const handleCancel = () => {
    setDraft(value);
    setEditing(false);
  };

  return (
    <div className="group rounded-lg border border-gray-100 bg-white p-4 transition-colors hover:border-gray-200">
      <div className="mb-1.5 flex items-center justify-between">
        <label className="text-xs font-semibold uppercase tracking-wide text-gray-400">
          {label}
        </label>
        {!editing && (
          <button
            onClick={() => setEditing(true)}
            className="text-xs text-gray-400 opacity-0 transition-opacity group-hover:opacity-100 hover:text-blue-500"
          >
            Edit
          </button>
        )}
      </div>

      {editing ? (
        <div>
          {multiline ? (
            <textarea
              value={draft}
              onChange={(e) => setDraft(e.target.value)}
              rows={4}
              className="w-full rounded-md border border-gray-200 p-2 text-sm text-gray-800 focus:border-blue-300 focus:outline-none focus:ring-1 focus:ring-blue-300"
              autoFocus
            />
          ) : (
            <input
              type="text"
              value={draft}
              onChange={(e) => setDraft(e.target.value)}
              className="w-full rounded-md border border-gray-200 p-2 text-sm text-gray-800 focus:border-blue-300 focus:outline-none focus:ring-1 focus:ring-blue-300"
              autoFocus
            />
          )}
          <div className="mt-2 flex gap-2">
            <button
              onClick={handleSave}
              className="rounded-md bg-blue-500 px-3 py-1 text-xs font-medium text-white hover:bg-blue-600"
            >
              Save
            </button>
            <button
              onClick={handleCancel}
              className="rounded-md bg-gray-100 px-3 py-1 text-xs font-medium text-gray-600 hover:bg-gray-200"
            >
              Cancel
            </button>
          </div>
        </div>
      ) : (
        <p className="whitespace-pre-wrap text-sm text-gray-700">
          {value || <span className="italic text-gray-300">Empty</span>}
        </p>
      )}
    </div>
  );
}

export default function NoteDetail({ note, onUpdate, onDelete, onBack }: NoteDetailProps) {
  const createdDate = new Date(note.createdAt);
  const formattedDate = createdDate.toLocaleDateString('en-GB', {
    weekday: 'long',
    day: 'numeric',
    month: 'long',
    year: 'numeric',
  });
  const formattedTime = createdDate.toLocaleTimeString('en-GB', {
    hour: '2-digit',
    minute: '2-digit',
  });

  const handleFieldSave = (key: string, value: string) => {
    const updates: Partial<InsolvencyNote> = { [key]: value };
    if (key === 'companyName') {
      updates.title = value;
    }
    onUpdate(note.id, updates);
  };

  const [showConfirmDelete, setShowConfirmDelete] = useState(false);

  return (
    <div className="mx-auto max-w-2xl">
      {/* Back / New upload */}
      <button
        onClick={onBack}
        className="mb-4 flex items-center gap-1.5 text-sm text-gray-400 hover:text-blue-500 transition-colors"
      >
        <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
          <path strokeLinecap="round" strokeLinejoin="round" d="M15 19l-7-7 7-7" />
        </svg>
        Upload new document
      </button>

      {/* Header */}
      <div className="mb-6">
        <h1 className="text-2xl font-bold text-gray-900">{note.title || 'Untitled'}</h1>
        <div className="mt-2 flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-gray-400">
          <span>Created {formattedDate} at {formattedTime}</span>
          {note.createdBy && (
            <>
              <span className="text-gray-300">|</span>
              <span>by {note.createdBy}</span>
            </>
          )}
          <span className="text-gray-300">|</span>
          <span className="font-mono">{note.sourceFileName}</span>
        </div>
      </div>

      {/* Fields */}
      <div className="space-y-3">
        <EditableField
          label="Company Name"
          value={note.companyName}
          fieldKey="companyName"
          onSave={handleFieldSave}
        />
        <EditableField
          label="Addressee"
          value={note.addressee}
          fieldKey="addressee"
          onSave={handleFieldSave}
        />
        <EditableField
          label="Dates & Deadlines"
          value={note.dateAndDeadlines}
          fieldKey="dateAndDeadlines"
          multiline
          onSave={handleFieldSave}
        />
        <EditableField
          label="Court"
          value={note.court}
          fieldKey="court"
          onSave={handleFieldSave}
        />
      </div>

      {/* Raw extraction (collapsible) */}
      <details className="mt-6 rounded-lg border border-gray-100 bg-gray-50">
        <summary className="cursor-pointer px-4 py-3 text-xs font-medium text-gray-400 hover:text-gray-600">
          Raw AI Extraction
        </summary>
        <pre className="overflow-x-auto whitespace-pre-wrap px-4 pb-4 font-mono text-xs text-gray-500">
          {note.rawExtractedText}
        </pre>
      </details>

      {/* Delete */}
      <div className="mt-8 border-t border-gray-100 pt-6">
        {showConfirmDelete ? (
          <div className="flex items-center gap-3">
            <span className="text-sm text-gray-600">Delete this note permanently?</span>
            <button
              onClick={() => onDelete(note.id)}
              className="rounded-md bg-red-500 px-3 py-1.5 text-xs font-medium text-white hover:bg-red-600"
            >
              Yes, delete
            </button>
            <button
              onClick={() => setShowConfirmDelete(false)}
              className="rounded-md bg-gray-100 px-3 py-1.5 text-xs font-medium text-gray-600 hover:bg-gray-200"
            >
              Cancel
            </button>
          </div>
        ) : (
          <button
            onClick={() => setShowConfirmDelete(true)}
            className="text-xs text-gray-400 hover:text-red-500"
          >
            Delete note
          </button>
        )}
      </div>
    </div>
  );
}
