/**
 * TemplatePreviewModal
 *
 * Full-screen modal that opens after a template is rendered against a case.
 * Provides:
 *  - TipTap rich-text editor pre-filled with the merged HTML (editable)
 *  - "Apply electronic signature" button that replaces {{ElectronicSignature}} with
 *    a rendered visual signature block at the marked position
 *  - Download PDF button (sends current HTML to the backend for conversion)
 *  - Save to case documents button (persists the PDF as an InsolvencyDocument)
 *  - Preview mode toggle (read-only rendered view)
 */
import { useState, useEffect, useCallback } from "react";
import { useEditor, EditorContent } from "@tiptap/react";
import StarterKit from "@tiptap/starter-kit";
import Underline from "@tiptap/extension-underline";
import TextAlign from "@tiptap/extension-text-align";
import { Table } from "@tiptap/extension-table";
import { TableRow } from "@tiptap/extension-table-row";
import { TableCell } from "@tiptap/extension-table-cell";
import { TableHeader } from "@tiptap/extension-table-header";
import Highlight from "@tiptap/extension-highlight";
import Link from "@tiptap/extension-link";
import { TextStyle } from "@tiptap/extension-text-style";
import Color from "@tiptap/extension-color";
import { documentTemplatesApi } from "@/services/api/documentTemplatesApi";
import { signingApi } from "@/services/api/signing";
import { Button } from "@/components/ui/button";
import {
  X, Loader2, Download, Save, CheckCircle2,
  Eye, EyeOff, PenLine, FileText,
  Bold, Italic, UnderlineIcon, List, ListOrdered,
  Heading1, Heading2, AlignLeft, AlignCenter, AlignRight,
  Undo2, Redo2, AlertTriangle, Mail, ShieldCheck,
} from "lucide-react";
import { format } from "date-fns";

// ── Constants ─────────────────────────────────────────────────────────────────

const SIGNATURE_PLACEHOLDER = "{{ElectronicSignature}}";

// ── Signature block builder ───────────────────────────────────────────────────

function buildSignatureHtml(practitionerName: string): string {
  const today = format(new Date(), "dd.MM.yyyy");
  return (
    `<div data-sig="electronic" style="display:inline-block;min-width:220px;` +
    `border-top:2px solid #334155;padding-top:10px;margin-top:24px;">` +
    `<p style="font-family:sans-serif;font-size:10px;color:#64748b;margin:0 0 2px 0;` +
    `letter-spacing:0.05em;text-transform:uppercase;">Semnat electronic</p>` +
    `<p style="font-family:Georgia,serif;font-size:20px;color:#1e293b;margin:0 0 4px 0;` +
    `font-style:italic;">${practitionerName}</p>` +
    `<p style="font-family:sans-serif;font-size:10px;color:#94a3b8;margin:0;">${today}</p>` +
    `</div>`
  );
}

// ── Toolbar helpers ───────────────────────────────────────────────────────────

function ToolbarBtn({
  active,
  title,
  onClick,
  disabled = false,
  children,
}: {
  active: boolean;
  title: string;
  onClick: () => void;
  disabled?: boolean;
  children: React.ReactNode;
}) {
  return (
    <button
      type="button"
      title={title}
      onClick={onClick}
      disabled={disabled}
      className={`rounded px-2 py-1.5 text-sm transition-colors disabled:opacity-30 disabled:cursor-not-allowed
        ${active
          ? "bg-primary text-primary-foreground"
          : "hover:bg-accent text-muted-foreground hover:text-foreground"
        }`}
    >
      {children}
    </button>
  );
}

function Sep() {
  return <div className="mx-0.5 h-5 w-px bg-border shrink-0" />;
}

// ── Main component ────────────────────────────────────────────────────────────

export interface TemplatePreviewModalProps {
  isOpen: boolean;
  templateName: string;
  /** Already-rendered HTML (Handlebars tokens replaced, {{ElectronicSignature}} preserved). */
  renderedHtml: string;
  caseId: string;
  /** Practitioner name extracted from the merge data – used for the signature block. */
  practitionerName?: string;
  onClose: () => void;
  onSaved: (documentId: string) => void;
  /** Called when the user clicks "Send via Email" after saving. */
  onSendEmail?: (subject: string, documentId: string) => void;
}

export default function TemplatePreviewModal({
  isOpen,
  templateName,
  renderedHtml,
  caseId,
  practitionerName = "Practicant insolvență",
  onClose,
  onSaved,
  onSendEmail,
}: TemplatePreviewModalProps) {
  const [showPreview, setShowPreview] = useState(false);
  const [downloading, setDownloading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [savedDocumentId, setSavedDocumentId] = useState<string | null>(null);
  const [savedDocxId, setSavedDocxId] = useState<string | null>(null);
  const [signatureApplied, setSignatureApplied] = useState(false);
  const [hasPlaceholder, setHasPlaceholder] = useState(false);

  // Sign PDF with server-side key
  const [canSign, setCanSign] = useState(false);
  const [showSignForm, setShowSignForm] = useState(false);
  const [signing, setSigning] = useState(false);
  const [signed, setSigned] = useState(false);
  const [signReason, setSignReason] = useState("");
  const [signPassword, setSignPassword] = useState("");

  // ── TipTap editor ─────────────────────────────────────────────────────────

  const editor = useEditor({
    extensions: [
      StarterKit.configure({ heading: { levels: [1, 2, 3] } }),
      Underline,
      TextAlign.configure({ types: ["heading", "paragraph"] }),
      Table.configure({ resizable: false, allowTableNodeSelection: true }),
      TableRow,
      TableCell,
      TableHeader,
      Highlight.configure({ multicolor: true }),
      Link.configure({ openOnClick: false }),
      TextStyle,
      Color,
    ],
    content: renderedHtml || "<p></p>",
    editorProps: {
      attributes: {
        class: "prose prose-sm max-w-none px-10 py-8 min-h-[600px] focus:outline-none",
      },
    },
  });

  // Reset state and load fresh content whenever the modal opens with new HTML
  useEffect(() => {
    if (!editor || !isOpen) return;
    editor.commands.setContent(renderedHtml || "<p></p>");
    setSignatureApplied(false);
    setSaved(false);
    setSavedDocumentId(null);
    setSavedDocxId(null);
    setShowSignForm(false);
    setSigned(false);
    setSignReason("");
    setSignPassword("");
    setShowPreview(false);
    setHasPlaceholder(renderedHtml.includes(SIGNATURE_PLACEHOLDER));
  }, [renderedHtml, isOpen, editor]);

  // Check whether the user has an active signing key when modal opens
  useEffect(() => {
    if (!isOpen) return;
    signingApi.getKeyStatus()
      .then(res => setCanSign(!!(res.data as { canSign?: boolean }).canSign))
      .catch(() => setCanSign(false));
  }, [isOpen]);

  // Keep hasPlaceholder reactive as content changes
  const checkPlaceholder = useCallback(() => {
    if (!editor) return;
    setHasPlaceholder(editor.getHTML().includes(SIGNATURE_PLACEHOLDER));
  }, [editor]);

  useEffect(() => {
    if (!editor) return;
    editor.on("update", checkPlaceholder);
    return () => { editor.off("update", checkPlaceholder); };
  }, [editor, checkPlaceholder]);

  // ── Handlers ──────────────────────────────────────────────────────────────

  const applySignature = useCallback(() => {
    if (!editor) return;
    const html = editor.getHTML();
    const sigHtml = buildSignatureHtml(practitionerName);
    if (html.includes(SIGNATURE_PLACEHOLDER)) {
      // Replace every occurrence of the placeholder with the signature block
      const updated = html.split(SIGNATURE_PLACEHOLDER).join(sigHtml);
      editor.commands.setContent(updated);
    } else {
      // No placeholder found in document – append signature at the end
      editor.commands.setContent(html + sigHtml);
    }
    setSignatureApplied(true);
    setHasPlaceholder(false);
  }, [editor, practitionerName]);

  const handleDownloadPdf = async () => {
    if (!editor) return;
    setDownloading(true);
    try {
      const result = await documentTemplatesApi.renderHtmlToPdfBlob({
        html: editor.getHTML(),
        caseId,
        templateName,
      });
      const url = URL.createObjectURL(result.blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = result.fileName;
      a.click();
      URL.revokeObjectURL(url);
    } catch (err) {
      console.error("Download error:", err);
    } finally {
      setDownloading(false);
    }
  };

  const handleSaveToCase = async () => {
    if (!editor) return;
    setSaving(true);
    try {
      const res = await documentTemplatesApi.saveToCaseFromHtml({
        html: editor.getHTML(),
        caseId,
        templateName,
      });
      onSaved(res.data.pdfDocumentId);
      setSavedDocumentId(res.data.pdfDocumentId);
      setSavedDocxId(res.data.docxDocumentId);
      setSaved(true);
    } catch (err) {
      console.error("Save error:", err);
    } finally {
      setSaving(false);
    }
  };

  const handleSignPdf = async () => {
    if (!savedDocumentId || !signPassword) return;
    setSigning(true);
    try {
      await signingApi.signDocument(savedDocumentId, signPassword, signReason || undefined);
      setSigned(true);
      setShowSignForm(false);
      setSignPassword("");
    } catch (err) {
      console.error("Signing error:", err);
    } finally {
      setSigning(false);
    }
  };

  // ── Render ────────────────────────────────────────────────────────────────

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 flex flex-col bg-background">

      {/* ── Header ─────────────────────────────────────────────────────────── */}
      <div className="flex items-center gap-3 border-b border-border bg-card px-4 py-3 shrink-0">
        {/* Close */}
        <button
          type="button"
          onClick={onClose}
          className="rounded-md p-1.5 hover:bg-accent text-muted-foreground transition-colors"
          title="Închide"
        >
          <X className="h-4 w-4" />
        </button>

        {/* Title */}
        <div className="flex items-center gap-2 flex-1 min-w-0">
          <FileText className="h-4 w-4 text-primary shrink-0" />
          <div className="min-w-0">
            <p className="text-sm font-semibold truncate">{templateName}</p>
            <p className="text-[11px] text-muted-foreground">
              Revizuiește și editează • aplică semnătura • descarcă sau salvează în dosar
            </p>
          </div>
        </div>

        {/* Actions */}
        <div className="flex items-center gap-2 shrink-0">
          {/* Electronic signature */}
          <Button
            variant={signatureApplied ? "default" : "outline"}
            size="sm"
            className="gap-1.5 text-xs"
            onClick={applySignature}
            disabled={signatureApplied}
            title={signatureApplied
              ? "Semnătură deja aplicată"
              : "Înlocuiește {{ElectronicSignature}} cu blocul de semnătură electronică"}
          >
            {signatureApplied
              ? <CheckCircle2 className="h-3.5 w-3.5" />
              : <PenLine className="h-3.5 w-3.5" />}
            {signatureApplied ? "Semnat" : "Semnătură electronică"}
          </Button>

          {/* Preview / edit toggle */}
          <button
            type="button"
            onClick={() => setShowPreview(p => !p)}
            title={showPreview ? "Mod editare" : "Previzualizare"}
            className="rounded-md p-1.5 hover:bg-accent text-muted-foreground transition-colors"
          >
            {showPreview ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
          </button>

          {/* Download PDF */}
          <Button
            variant="outline"
            size="sm"
            className="gap-1.5 text-xs"
            onClick={handleDownloadPdf}
            disabled={downloading}
          >
            {downloading
              ? <Loader2 className="h-3.5 w-3.5 animate-spin" />
              : <Download className="h-3.5 w-3.5" />}
            {downloading ? "Generare..." : "Descarcă PDF"}
          </Button>

          {/* Save to case */}
          <Button
            size="sm"
            className="gap-1.5 text-xs"
            onClick={handleSaveToCase}
            disabled={saving || saved}
          >
            {saving
              ? <Loader2 className="h-3.5 w-3.5 animate-spin" />
              : saved
                ? <CheckCircle2 className="h-3.5 w-3.5" />
                : <Save className="h-3.5 w-3.5" />}
            {saving ? "Se salvează..." : saved ? "Salvat (PDF + DOCX)" : "Salvează în dosar"}
          </Button>

          {/* Sign PDF — shown after saving if the user has an active signing key */}
          {saved && savedDocumentId && canSign && !signed && (
            <Button
              size="sm"
              variant="outline"
              className="gap-1.5 text-xs border-primary/30 text-primary hover:bg-primary/5"
              onClick={() => setShowSignForm(s => !s)}
            >
              <ShieldCheck className="h-3.5 w-3.5" />
              Semnează digital PDF
            </Button>
          )}
          {saved && signed && (
            <span className="flex items-center gap-1 text-xs text-emerald-600 font-medium">
              <ShieldCheck className="h-3.5 w-3.5" /> PDF semnat digital
            </span>
          )}

          {/* Send via Email — shown only after saving */}
          {saved && savedDocumentId && onSendEmail && (
            <Button
              size="sm"
              variant="outline"
              className="gap-1.5 text-xs border-primary/30 text-primary hover:bg-primary/5"
              onClick={() => onSendEmail(templateName, savedDocumentId)}
            >
              <Mail className="h-3.5 w-3.5" />
              Trimite prin Email
            </Button>
          )}
        </div>
      </div>

      {/* ── Sign PDF inline form ────────────────────────────────────────── */}
      {showSignForm && saved && !signed && (
        <div className="flex items-center gap-2 border-b border-amber-400/30 bg-amber-500/5 px-4 py-2 shrink-0">
          <ShieldCheck className="h-4 w-4 shrink-0 text-amber-600" />
          <span className="text-xs text-amber-700 font-medium shrink-0">Semnătură digitală PDF:</span>
          <input
            type="text"
            placeholder="Motiv (opțional)"
            value={signReason}
            onChange={e => setSignReason(e.target.value)}
            className="h-7 rounded-md border border-border bg-background px-2.5 text-xs w-44"
          />
          <input
            type="password"
            placeholder="Parolă certificat *"
            value={signPassword}
            onChange={e => setSignPassword(e.target.value)}
            className="h-7 rounded-md border border-border bg-background px-2.5 text-xs w-44"
          />
          <Button
            size="sm"
            className="h-7 text-xs gap-1"
            onClick={handleSignPdf}
            disabled={signing || !signPassword}
          >
            {signing ? <Loader2 className="h-3 w-3 animate-spin" /> : <ShieldCheck className="h-3 w-3" />}
            {signing ? "Se semnează..." : "Semnează"}
          </Button>
          <button
            type="button"
            onClick={() => { setShowSignForm(false); setSignPassword(""); }}
            className="text-xs text-muted-foreground hover:text-foreground"
          >
            Anulează
          </button>
        </div>
      )}

      {/* ── Editor toolbar (hidden in preview mode) ─────────────────────── */}
      {!showPreview && editor && (
        <div className="flex flex-wrap items-center gap-0.5 border-b border-border bg-muted/40 px-2 py-1 shrink-0">
          <ToolbarBtn active={false} title="Anulează (Ctrl+Z)"
            onClick={() => editor.chain().focus().undo().run()}
            disabled={!editor.can().undo()}>
            <Undo2 className="h-4 w-4" />
          </ToolbarBtn>
          <ToolbarBtn active={false} title="Refă (Ctrl+Y)"
            onClick={() => editor.chain().focus().redo().run()}
            disabled={!editor.can().redo()}>
            <Redo2 className="h-4 w-4" />
          </ToolbarBtn>
          <Sep />
          <ToolbarBtn active={editor.isActive("bold")} title="Bold (Ctrl+B)"
            onClick={() => editor.chain().focus().toggleBold().run()}>
            <Bold className="h-4 w-4" />
          </ToolbarBtn>
          <ToolbarBtn active={editor.isActive("italic")} title="Italic (Ctrl+I)"
            onClick={() => editor.chain().focus().toggleItalic().run()}>
            <Italic className="h-4 w-4" />
          </ToolbarBtn>
          <ToolbarBtn active={editor.isActive("underline")} title="Subliniat (Ctrl+U)"
            onClick={() => editor.chain().focus().toggleUnderline().run()}>
            <UnderlineIcon className="h-4 w-4" />
          </ToolbarBtn>
          <Sep />
          <ToolbarBtn active={editor.isActive("heading", { level: 1 })} title="Titlu 1"
            onClick={() => editor.chain().focus().toggleHeading({ level: 1 }).run()}>
            <Heading1 className="h-4 w-4" />
          </ToolbarBtn>
          <ToolbarBtn active={editor.isActive("heading", { level: 2 })} title="Titlu 2"
            onClick={() => editor.chain().focus().toggleHeading({ level: 2 }).run()}>
            <Heading2 className="h-4 w-4" />
          </ToolbarBtn>
          <Sep />
          <ToolbarBtn active={editor.isActive("bulletList")} title="Listă cu buline"
            onClick={() => editor.chain().focus().toggleBulletList().run()}>
            <List className="h-4 w-4" />
          </ToolbarBtn>
          <ToolbarBtn active={editor.isActive("orderedList")} title="Listă numerotată"
            onClick={() => editor.chain().focus().toggleOrderedList().run()}>
            <ListOrdered className="h-4 w-4" />
          </ToolbarBtn>
          <Sep />
          <ToolbarBtn active={editor.isActive({ textAlign: "left" })} title="Aliniere stânga"
            onClick={() => editor.chain().focus().setTextAlign("left").run()}>
            <AlignLeft className="h-4 w-4" />
          </ToolbarBtn>
          <ToolbarBtn active={editor.isActive({ textAlign: "center" })} title="Centrare"
            onClick={() => editor.chain().focus().setTextAlign("center").run()}>
            <AlignCenter className="h-4 w-4" />
          </ToolbarBtn>
          <ToolbarBtn active={editor.isActive({ textAlign: "right" })} title="Aliniere dreapta"
            onClick={() => editor.chain().focus().setTextAlign("right").run()}>
            <AlignRight className="h-4 w-4" />
          </ToolbarBtn>
          <Sep />
          {/* Insert signature placeholder at cursor */}
          <button
            type="button"
            title="Inserează placeholder semnătură la poziția cursorului"
            onClick={() => editor.chain().focus().insertContent(SIGNATURE_PLACEHOLDER).run()}
            className="rounded px-2 py-1.5 text-sm hover:bg-accent text-muted-foreground hover:text-foreground transition-colors flex items-center gap-1.5"
          >
            <PenLine className="h-4 w-4" />
            <span className="text-xs">+ Semnătură</span>
          </button>
        </div>
      )}

      {/* ── Document area ──────────────────────────────────────────────────── */}
      <div className="flex-1 overflow-auto bg-muted/20 py-8 px-4">
        <div className="max-w-4xl mx-auto">
          {showPreview ? (
            /* Read-only rendered view */
            <div
              className="prose prose-sm max-w-none bg-white dark:bg-card shadow-lg rounded-lg border border-border px-10 py-8"
              dangerouslySetInnerHTML={{ __html: editor?.getHTML() ?? renderedHtml }}
            />
          ) : (
            /* Editable TipTap view */
            <div className="bg-white dark:bg-card shadow-lg rounded-lg border border-border overflow-hidden">
              {editor && (
                <EditorContent
                  editor={editor}
                  className="[&_.ProseMirror]:outline-none [&_.ProseMirror]:min-h-[600px]"
                />
              )}
            </div>
          )}
        </div>
      </div>

      {/* ── Signature placeholder info banner ─────────────────────────────── */}
      {hasPlaceholder && !signatureApplied && (
        <div className="flex items-center gap-2 border-t border-amber-200 dark:border-amber-800 bg-amber-50 dark:bg-amber-950/20 px-4 py-2 shrink-0">
          <AlertTriangle className="h-3.5 w-3.5 text-amber-600 dark:text-amber-400 shrink-0" />
          <p className="text-xs text-amber-700 dark:text-amber-300">
            Documentul conține un loc rezervat pentru semnătură electronică (
            <code className="font-mono text-[11px]">{SIGNATURE_PLACEHOLDER}</code>
            ). Apasă <strong>"Semnătură electronică"</strong> din bara de sus pentru a aplica semnătura înainte de descărcare.
          </p>
        </div>
      )}
    </div>
  );
}
