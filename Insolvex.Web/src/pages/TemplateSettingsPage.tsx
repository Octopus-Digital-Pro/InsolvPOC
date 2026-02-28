/**
 * TemplateSettingsPage — Rich document template manager.
 *
 * Three views:
 *  1. "Required Templates" — system/mandatory templates tied to insolvency stages.
 *     Users can write and save the HTML body for each.
 *  2. "Custom Templates"   — user-created templates for any purpose.
 *  3. "Incoming Documents" — document types received from courts / external parties.
 *     Admins upload a sample PDF so AI can recognise and auto-classify these documents.
 *
 * The editor uses Tiptap (ProseMirror-based) with a placeholder sidebar so users
 * can click {{PlaceholderName}} tokens that get inserted at the cursor.
 */
import { useState, useEffect, useCallback, useRef } from "react";
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
import Superscript from "@tiptap/extension-superscript";
import Subscript from "@tiptap/extension-subscript";
import { TextStyle } from "@tiptap/extension-text-style";
import Color from "@tiptap/extension-color";
import Image from "@tiptap/extension-image";
import Placeholder from "@tiptap/extension-placeholder";
import {
  documentTemplatesApi,
  type DocumentTemplateDto,
  type DocumentTemplateDetailDto,
  type PlaceholderGroup,
  type IncomingDocumentType,
  type IncomingDocumentReferenceStatus,
  type ImportWordDocumentResult,
  SYSTEM_TEMPLATE_LABELS,
  SYSTEM_TEMPLATE_STAGE,
  getIncomingDocumentLabel,
  getIncomingDocumentDescription,
} from "@/services/api/documentTemplatesApi";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import {
  FileText, Plus, Pencil, Trash2, Loader2, ChevronRight,
  Bold, Italic, UnderlineIcon, List, ListOrdered,
  Heading1, Heading2, Heading3, AlignLeft, AlignCenter, AlignRight, AlignJustify,
  Eye, EyeOff, Save, ArrowLeft, CheckCircle2, AlertTriangle,
  Upload, Info, CheckCircle, FileUp, X,
  Table as TableIcon, Undo2, Redo2, Strikethrough, Code,
  Link as LinkIcon, Unlink, Highlighter,
  Superscript as SuperscriptIcon, Subscript as SubscriptIcon,
  Minus, Quote, RemoveFormatting, Columns, RowsIcon,
  ImageIcon, Palette,
} from "lucide-react";
import { useTranslation } from "@/contexts/LanguageContext";
import { PdfAnnotatorModal } from "@/components/PdfAnnotatorModal";

// ── Editor toolbar ────────────────────────────────────────────────────────────

function EditorToolbar({ editor }: { editor: ReturnType<typeof useEditor> }) {
  if (!editor) return null;

  const btn = (active: boolean, title: string, icon: React.ReactNode, onClick: () => void, disabled = false) => (
    <button
      key={title}
      type="button"
      title={title}
      onClick={onClick}
      disabled={disabled}
      className={`
        rounded px-2 py-1.5 text-sm transition-colors disabled:opacity-30 disabled:cursor-not-allowed
        ${active
          ? "bg-primary text-primary-foreground"
          : "hover:bg-accent text-muted-foreground hover:text-foreground"
        }
      `}
    >
      {icon}
    </button>
  );

  const sep = () => <div className="mx-0.5 h-5 w-px bg-border shrink-0" />;

  const insertTable = () => {
    editor.chain().focus().insertTable({ rows: 3, cols: 3, withHeaderRow: true }).run();
  };

  const setLink = () => {
    const prev = editor.getAttributes("link").href;
    const url = window.prompt("URL:", prev ?? "https://");
    if (url === null) return;
    if (url === "") { editor.chain().focus().unsetLink().run(); return; }
    editor.chain().focus().extendMarkRange("link").setLink({ href: url }).run();
  };

  const insertImage = () => {
    const url = window.prompt("URL imagine:");
    if (url) editor.chain().focus().setImage({ src: url }).run();
  };

  const setFontColor = () => {
    const color = window.prompt("Culoare (hex):", "#000000");
    if (color) editor.chain().focus().setColor(color).run();
  };

  return (
    <div className="flex flex-wrap items-center gap-0.5 rounded-t-lg border border-border bg-muted/40 p-1 border-b-0">
      {/* ── Undo / Redo ── */}
      {btn(false, "Undo (Ctrl+Z)", <Undo2 className="h-4 w-4" />,
        () => editor.chain().focus().undo().run(), !editor.can().undo())}
      {btn(false, "Redo (Ctrl+Y)", <Redo2 className="h-4 w-4" />,
        () => editor.chain().focus().redo().run(), !editor.can().redo())}

      {sep()}

      {/* ── Inline formatting ── */}
      {btn(editor.isActive("bold"), "Bold (Ctrl+B)", <Bold className="h-4 w-4" />,
        () => editor.chain().focus().toggleBold().run())}
      {btn(editor.isActive("italic"), "Italic (Ctrl+I)", <Italic className="h-4 w-4" />,
        () => editor.chain().focus().toggleItalic().run())}
      {btn(editor.isActive("underline"), "Underline (Ctrl+U)", <UnderlineIcon className="h-4 w-4" />,
        () => editor.chain().focus().toggleUnderline().run())}
      {btn(editor.isActive("strike"), "Strikethrough", <Strikethrough className="h-4 w-4" />,
        () => editor.chain().focus().toggleStrike().run())}
      {btn(editor.isActive("code"), "Inline code", <Code className="h-4 w-4" />,
        () => editor.chain().focus().toggleCode().run())}
      {btn(editor.isActive("superscript"), "Superscript", <SuperscriptIcon className="h-4 w-4" />,
        () => editor.chain().focus().toggleSuperscript().run())}
      {btn(editor.isActive("subscript"), "Subscript", <SubscriptIcon className="h-4 w-4" />,
        () => editor.chain().focus().toggleSubscript().run())}
      {btn(editor.isActive("highlight"), "Highlight", <Highlighter className="h-4 w-4" />,
        () => editor.chain().focus().toggleHighlight().run())}

      {sep()}

      {/* ── Font color ── */}
      {btn(false, "Font color", <Palette className="h-4 w-4" />, setFontColor)}

      {sep()}

      {/* ── Headings ── */}
      {btn(editor.isActive("heading", { level: 1 }), "Heading 1", <Heading1 className="h-4 w-4" />,
        () => editor.chain().focus().toggleHeading({ level: 1 }).run())}
      {btn(editor.isActive("heading", { level: 2 }), "Heading 2", <Heading2 className="h-4 w-4" />,
        () => editor.chain().focus().toggleHeading({ level: 2 }).run())}
      {btn(editor.isActive("heading", { level: 3 }), "Heading 3", <Heading3 className="h-4 w-4" />,
        () => editor.chain().focus().toggleHeading({ level: 3 }).run())}

      {sep()}

      {/* ── Lists ── */}
      {btn(editor.isActive("bulletList"), "Bullet list", <List className="h-4 w-4" />,
        () => editor.chain().focus().toggleBulletList().run())}
      {btn(editor.isActive("orderedList"), "Numbered list", <ListOrdered className="h-4 w-4" />,
        () => editor.chain().focus().toggleOrderedList().run())}

      {sep()}

      {/* ── Alignment ── */}
      {btn(editor.isActive({ textAlign: "left" }), "Align left", <AlignLeft className="h-4 w-4" />,
        () => editor.chain().focus().setTextAlign("left").run())}
      {btn(editor.isActive({ textAlign: "center" }), "Align center", <AlignCenter className="h-4 w-4" />,
        () => editor.chain().focus().setTextAlign("center").run())}
      {btn(editor.isActive({ textAlign: "right" }), "Align right", <AlignRight className="h-4 w-4" />,
        () => editor.chain().focus().setTextAlign("right").run())}
      {btn(editor.isActive({ textAlign: "justify" }), "Justify", <AlignJustify className="h-4 w-4" />,
        () => editor.chain().focus().setTextAlign("justify").run())}

      {sep()}

      {/* ── Block elements ── */}
      {btn(editor.isActive("blockquote"), "Blockquote", <Quote className="h-4 w-4" />,
        () => editor.chain().focus().toggleBlockquote().run())}
      {btn(false, "Horizontal rule", <Minus className="h-4 w-4" />,
        () => editor.chain().focus().setHorizontalRule().run())}

      {sep()}

      {/* ── Links & Images ── */}
      {btn(editor.isActive("link"), "Insert / edit link", <LinkIcon className="h-4 w-4" />, setLink)}
      {btn(false, "Remove link", <Unlink className="h-4 w-4" />,
        () => editor.chain().focus().unsetLink().run(), !editor.isActive("link"))}
      {btn(false, "Insert image (URL)", <ImageIcon className="h-4 w-4" />, insertImage)}

      {sep()}

      {/* ── Table ── */}
      {btn(false, "Insert table 3×3", <TableIcon className="h-4 w-4" />, insertTable)}
      {btn(false, "Add column after", <Columns className="h-4 w-4" />,
        () => editor.chain().focus().addColumnAfter().run(), !editor.can().addColumnAfter())}
      {btn(false, "Add row after", <RowsIcon className="h-4 w-4" />,
        () => editor.chain().focus().addRowAfter().run(), !editor.can().addRowAfter())}
      {btn(false, "Delete column", <span className="text-[10px] font-bold leading-none">−Col</span>,
        () => editor.chain().focus().deleteColumn().run(), !editor.can().deleteColumn())}
      {btn(false, "Delete row", <span className="text-[10px] font-bold leading-none">−Row</span>,
        () => editor.chain().focus().deleteRow().run(), !editor.can().deleteRow())}
      {btn(false, "Delete table", <Trash2 className="h-4 w-4" />,
        () => editor.chain().focus().deleteTable().run(), !editor.can().deleteTable())}
      {btn(false, "Merge / split cells", <span className="text-[10px] font-bold leading-none">M/S</span>,
        () => editor.chain().focus().mergeOrSplit().run(), !editor.can().mergeOrSplit())}
      {btn(false, "Toggle header row", <span className="text-[10px] font-bold leading-none">TH</span>,
        () => editor.chain().focus().toggleHeaderRow().run(), !editor.can().toggleHeaderRow())}

      {sep()}

      {/* ── Clear formatting ── */}
      {btn(false, "Clear formatting", <RemoveFormatting className="h-4 w-4" />,
        () => editor.chain().focus().unsetAllMarks().clearNodes().run())}

      {sep()}

      {/* ── Electronic signature placeholder ── */}
      {btn(false, "Insert electronic signature placeholder ({{ElectronicSignature}})",
        <span className="flex items-center gap-1 text-[11px] font-semibold">✍ Semn.</span>,
        () => editor.chain().focus().insertContent("{{ElectronicSignature}}").run())}
    </div>
  );
}

// ── Placeholder sidebar ───────────────────────────────────────────────────────

/**
 * Detects group type from the group name convention returned by the API:
 * - "🔁 Creditori ({{#each Creditors}})" → repeater block
 * - "❓ Condiții ({{#if}})"              → conditional block
 * - anything else                         → scalar placeholder
 */
function parseGroupType(groupName: string): {
  type: "scalar" | "each" | "if";
  collectionName?: string;
} {
  const eachMatch = groupName.match(/\{\{#each\s+(\w+)\}\}/);
  if (eachMatch) return { type: "each", collectionName: eachMatch[1] };
  if (groupName.includes("{{#if}}")) return { type: "if" };
  return { type: "scalar" };
}

function PlaceholderSidebar({
  groups,
  onInsert,
}: {
  groups: PlaceholderGroup[];
  onInsert: (placeholder: string) => void;
}) {
  const [openGroup, setOpenGroup] = useState<string | null>(null);
  const { t } = useTranslation();

  /** Insert a full {{#each Collection}} … {{/each}} block with all fields as a table row. */
  const insertEachBlock = (collectionName: string, fields: { key: string; label: string }[]) => {
    const cols = fields.map((f) => `<td>{{${f.key}}}</td>`).join("");
    const headerCols = fields.map((f) => `<th>${f.label}</th>`).join("");
    const block =
      `<table border="1" cellpadding="4" cellspacing="0" style="width:100%; border-collapse:collapse;">\n` +
      `<thead><tr>${headerCols}</tr></thead>\n` +
      `<tbody>\n{{#each ${collectionName}}}\n<tr>${cols}</tr>\n{{/each}}\n</tbody>\n</table>`;
    onInsert(block);
  };

  /** Insert a single field inside a repeater context (no wrapper). */
  const insertRepeaterField = (key: string) => {
    onInsert(`{{${key}}}`);
  };

  /** Insert a {{#if key}} … {{/if}} conditional block. */
  const insertIfBlock = (key: string) => {
    onInsert(`{{#if ${key}}}\n<p>…${t.templateSettings.contentShown.replace('{{key}}', key)}…</p>\n{{/if}}`);  
  };

  return (
    <div className="flex flex-col gap-0 divide-y divide-border rounded-lg border border-border overflow-hidden">
      <div className="bg-muted/60 px-3 py-2">
        <p className="text-xs font-semibold text-muted-foreground uppercase tracking-wide">
          {t.templateSettings.availableFields}
        </p>
        <p className="text-xs text-muted-foreground mt-0.5">
          {t.templateSettings.clickToInsert}
        </p>
      </div>
      {groups.map((g) => {
        const { type, collectionName } = parseGroupType(g.group);

        return (
          <div key={g.group}>
            <button
              type="button"
              onClick={() => setOpenGroup(openGroup === g.group ? null : g.group)}
              className="flex w-full items-center justify-between px-3 py-2 text-sm font-medium hover:bg-accent/50 transition-colors"
            >
              <span>{g.group}</span>
              <ChevronRight
                className={`h-4 w-4 text-muted-foreground transition-transform ${
                  openGroup === g.group ? "rotate-90" : ""
                }`}
              />
            </button>
            {openGroup === g.group && (
              <div className="bg-muted/20 px-2 pb-2 flex flex-col gap-0.5">
                {/* Repeater groups get a "Insert full table" button */}
                {type === "each" && collectionName && (
                  <button
                    type="button"
                    onClick={() => insertEachBlock(collectionName, g.fields)}
                    className="text-left rounded px-2 py-2 text-xs font-medium bg-primary/5 hover:bg-primary/15 text-primary transition-colors mb-1 border border-primary/20"
                  >
                    ⚡ {t.templateSettings.insertFullTable} {`{{#each ${collectionName}}}`}
                  </button>
                )}

                {g.fields.map((f) => (
                  <button
                    key={f.key}
                    type="button"
                    title={
                      type === "if"
                        ? `{{#if ${f.key}}} … {{/if}}`
                        : `{{${f.key}}}`
                    }
                    onClick={() =>
                      type === "if"
                        ? insertIfBlock(f.key)
                        : type === "each"
                          ? insertRepeaterField(f.key)
                          : onInsert(`{{${f.key}}}`)
                    }
                    className="text-left rounded px-2 py-1.5 text-xs hover:bg-primary/10 hover:text-primary transition-colors"
                  >
                    <span className="font-mono text-primary mr-1.5 text-[11px]">
                      {type === "if"
                        ? `{{#if ${f.key}}}`
                        : `{{${f.key}}}`}
                    </span>
                    <span className="text-muted-foreground">{f.label}</span>
                  </button>
                ))}
              </div>
            )}
          </div>
        );
      })}
    </div>
  );
}

// ── Template editor panel ─────────────────────────────────────────────────────

interface TemplateEditorPanelProps {
  template: DocumentTemplateDetailDto;
  placeholders: PlaceholderGroup[];
  onSave: (id: string, bodyHtml: string, meta: { name: string; description: string; category: string; isActive: boolean }) => Promise<void>;
  onClose: () => void;
}

function TemplateEditorPanel({
  template,
  placeholders,
  onSave,
  onClose,
}: TemplateEditorPanelProps) {
  const [name, setName] = useState(template.name);
  const [description, setDescription] = useState(template.description ?? "");
  const [category, setCategory] = useState(template.category ?? "");
  const [isActive, setIsActive] = useState(template.isActive);
  const [saving, setSaving] = useState(false);
  const [showPreview, setShowPreview] = useState(false);
  const [showHtml, setShowHtml] = useState(false);
  const [htmlContent, setHtmlContent] = useState(template.bodyHtml ?? "");
  const [savedOk, setSavedOk] = useState(false);
  const [importingWord, setImportingWord] = useState(false);
  const [importBanner, setImportBanner] = useState<{ count: number; fileName: string } | null>(null);
  const [importError, setImportError] = useState<string | null>(null);
  const htmlRef = useRef<HTMLTextAreaElement>(null);
  const wordFileRef = useRef<HTMLInputElement>(null);
  const { t } = useTranslation();

  const editor = useEditor({
    extensions: [
      StarterKit.configure({
        heading: { levels: [1, 2, 3] },
      }),
      Underline,
      TextAlign.configure({ types: ["heading", "paragraph"] }),
      Table.configure({ resizable: true, allowTableNodeSelection: true }),
      TableRow,
      TableCell,
      TableHeader,
      Highlight.configure({ multicolor: true }),
      Link.configure({ openOnClick: false, autolink: true }),
      Superscript,
      Subscript,
      TextStyle,
      Color,
      Image.configure({ inline: false, allowBase64: true }),
      Placeholder.configure({ placeholder: "Începe să scrii documentul aici…" }),
    ],
    content: template.bodyHtml ?? "",
  });

  const insertPlaceholder = useCallback(
    (token: string) => {
      if (!editor) return;

      const isBlockSyntax = token.includes("{{#each") || token.includes("{{#if");

      // Block syntax ({{#each}}/{{#if}}) must go through the HTML textarea
      //  because ProseMirror strips Handlebars nodes between table elements.
      if (isBlockSyntax && !showHtml) {
        // Snapshot current rich-text content, switch to HTML view,
        // then insert the block after a short tick.
        const currentHtml = editor.getHTML();
        setHtmlContent(currentHtml);
        setShowHtml(true);
        setTimeout(() => {
          const ta = htmlRef.current;
          if (!ta) return;
          const newValue = ta.value + "\n" + token;
          setHtmlContent(newValue);
          ta.focus();
          setTimeout(() => ta.setSelectionRange(newValue.length, newValue.length), 0);
        }, 60);
        return;
      }

      if (showHtml) {
        const ta = htmlRef.current;
        if (!ta) return;
        const pos = ta.selectionStart ?? ta.value.length;
        const v = ta.value;
        const newVal = v.slice(0, pos) + token + v.slice(pos);
        setHtmlContent(newVal);
        ta.focus();
        setTimeout(() => ta.setSelectionRange(pos + token.length, pos + token.length), 0);
      } else {
        editor.chain().focus().insertContent(token).run();
      }
    },
    [editor, showHtml]
  );

  const toggleHtmlView = useCallback(() => {
    if (!showHtml) {
      // Switch TO html view: snapshot editor content
      setHtmlContent(editor?.getHTML() ?? "");
      setShowHtml(true);
    } else {
      // Switch back: push textarea content into editor
      editor?.commands.setContent(htmlContent);
      setShowHtml(false);
    }
  }, [showHtml, editor, htmlContent]);

  const handleWordImport = async (file: File) => {
    setImportingWord(true);
    setImportError(null);
    setImportBanner(null);
    try {
      const res = await documentTemplatesApi.importWordDocument(file);
      const { html, detectedPlaceholders, fileName } = res.data as ImportWordDocumentResult;
      // Load into editor (prefer rich-text view)
      editor?.commands.setContent(html);
      setHtmlContent(html);
      setShowHtml(false);
      setImportBanner({ count: detectedPlaceholders.length, fileName });
      setTimeout(() => setImportBanner(null), 10_000);
    } catch (err: any) {
      setImportError(
        err?.response?.data?.message ?? "Failed to import Word document. Ensure the file is a valid .docx."
      );
      setTimeout(() => setImportError(null), 6000);
    } finally {
      setImportingWord(false);
      if (wordFileRef.current) wordFileRef.current.value = "";
    }
  };

  const handleSave = async () => {
    if (!editor) return;
    setSaving(true);
    try {
      const bodyHtml = showHtml ? htmlContent : editor.getHTML();
      await onSave(template.id, bodyHtml, { name, description, category, isActive });
      setSavedOk(true);
      setTimeout(() => setSavedOk(false), 3000);
    } finally {
      setSaving(false);
    }
  };

  const preview = showHtml ? htmlContent : (editor?.getHTML() ?? "");

  return (
    <div className="flex flex-col h-full">
      {/* Header */}
      <div className="flex items-center gap-3 border-b border-border px-4 py-3">
        <button
          type="button"
          onClick={onClose}
          className="rounded-md p-1 hover:bg-accent text-muted-foreground"
        >
          <ArrowLeft className="h-4 w-4" />
        </button>
        <div className="flex-1 min-w-0">
          {template.isSystem ? (
            <>
              <p className="font-semibold text-sm truncate">{template.name}</p>
              <p className="text-xs text-muted-foreground">
                Template de sistem •{" "}
                {SYSTEM_TEMPLATE_STAGE[template.templateType] ?? template.stage ?? ""}
              </p>
            </>
          ) : (
            <input
              value={name}
              onChange={(e) => setName(e.target.value)}
              className="bg-transparent text-sm font-semibold outline-none w-full"
              placeholder={t.templateSettings.templateNamePlaceholder}
            />
          )}
        </div>

        <div className="flex items-center gap-2 shrink-0">
          {/* Hidden file input for Word import */}
          <input
            ref={wordFileRef}
            type="file"
            accept=".docx,application/vnd.openxmlformats-officedocument.wordprocessingml.document"
            className="hidden"
            onChange={(e) => {
              const file = e.target.files?.[0];
              if (file) handleWordImport(file);
            }}
          />
          {/* Import Word button */}
          <button
            type="button"
            onClick={() => wordFileRef.current?.click()}
            disabled={importingWord}
            title="Import Word document (.docx) — AI will detect and insert placeholders"
            className="flex items-center gap-1.5 rounded-md px-2.5 py-1.5 text-xs font-medium border border-border bg-background hover:bg-accent text-muted-foreground hover:text-foreground transition-colors disabled:opacity-50 disabled:cursor-not-allowed shrink-0"
          >
            {importingWord ? (
              <Loader2 className="h-3.5 w-3.5 animate-spin" />
            ) : (
              <FileUp className="h-3.5 w-3.5" />
            )}
            {importingWord ? "Analyzing…" : "Import Word"}
          </button>

          <button
            type="button"
            onClick={toggleHtmlView}
            title={showHtml ? "Switch to rich-text editor" : "Edit HTML source"}
            className={`rounded-md p-1.5 transition-colors ${
              showHtml
                ? "bg-primary text-primary-foreground"
                : "hover:bg-accent text-muted-foreground"
            }`}
          >
            <Code className="h-4 w-4" />
          </button>
          <button
            type="button"
            onClick={() => setShowPreview(!showPreview)}
            title={showPreview ? t.templateSettings.prevTitle : t.templateSettings.prevTitle}
            className="rounded-md p-1.5 hover:bg-accent text-muted-foreground"
          >
            {showPreview ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
          </button>
          <Button size="sm" onClick={handleSave} disabled={saving}>
            {saving ? (
              <Loader2 className="h-3.5 w-3.5 animate-spin mr-1" />
            ) : savedOk ? (
              <CheckCircle2 className="h-3.5 w-3.5 mr-1 text-green-500" />
            ) : (
              <Save className="h-3.5 w-3.5 mr-1" />
            )}
            {savedOk ? t.templateSettings.saved : t.templateSettings.save}
          </Button>
        </div>
      </div>

      {/* Word import success banner */}
      {importBanner && (
        <div className="flex items-center gap-2 border-b border-green-200 bg-green-50 dark:bg-green-950/30 dark:border-green-900 px-4 py-2 text-sm">
          <CheckCircle2 className="h-4 w-4 text-green-600 dark:text-green-400 shrink-0" />
          <span className="text-green-800 dark:text-green-300 flex-1">
            <span className="font-medium">{importBanner.fileName}</span> imported successfully.
            {importBanner.count > 0 ? (
              <> AI detected <span className="font-semibold">{importBanner.count}</span> placeholder{importBanner.count !== 1 ? "s" : ""} and inserted them automatically.</>
            ) : (
              <> The document was converted to HTML — review the placeholder sidebar to add tokens manually.</>
            )}
          </span>
          <button type="button" onClick={() => setImportBanner(null)} className="text-green-600 hover:text-green-800 dark:text-green-400">
            <X className="h-3.5 w-3.5" />
          </button>
        </div>
      )}

      {/* Word import error banner */}
      {importError && (
        <div className="flex items-center gap-2 border-b border-destructive/30 bg-destructive/10 px-4 py-2 text-sm">
          <AlertTriangle className="h-4 w-4 text-destructive shrink-0" />
          <span className="text-destructive flex-1">{importError}</span>
          <button type="button" onClick={() => setImportError(null)} className="text-destructive hover:opacity-70">
            <X className="h-3.5 w-3.5" />
          </button>
        </div>
      )}

      {/* Description + Category for custom templates */}
      {!template.isSystem && (
        <div className="flex gap-3 border-b border-border px-4 py-2 bg-muted/30">
          <input
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            className="flex-1 bg-transparent text-xs outline-none text-muted-foreground placeholder:text-muted-foreground/50"
            placeholder={t.templateSettings.descOptional}
          />
          <input
            value={category}
            onChange={(e) => setCategory(e.target.value)}
            className="w-40 bg-transparent text-xs outline-none text-muted-foreground placeholder:text-muted-foreground/50 border-l border-border pl-3"
            placeholder={t.templateSettings.categoryPlaceholder}
          />
          <label className="flex items-center gap-1.5 text-xs text-muted-foreground border-l border-border pl-3 cursor-pointer select-none">
            <input
              type="checkbox"
              checked={isActive}
              onChange={(e) => setIsActive(e.target.checked)}
              className="rounded"
            />
            {t.templateSettings.active}
          </label>
        </div>
      )}

      {/* Main content: editor + sidebar */}
      <div className="flex flex-1 min-h-0 overflow-hidden gap-3 p-4">
        {showPreview ? (
          /* Preview pane */
          <div className="flex-1 overflow-auto">
            <div
              className="prose prose-sm max-w-none bg-white dark:bg-card border rounded-lg p-6 shadow-sm"
              dangerouslySetInnerHTML={{ __html: preview }}
            />
          </div>
        ) : showHtml ? (
          /* HTML source editor */
          <div className="flex-1 overflow-auto flex flex-col min-w-0">
            <div className="flex items-center gap-2 rounded-t-lg border border-border bg-muted/40 px-3 py-1.5 border-b-0">
              <Code className="h-3.5 w-3.5 text-muted-foreground" />
              <span className="text-xs font-medium text-muted-foreground">HTML source</span>
              <span className="ml-auto text-[11px] text-muted-foreground/60">{"Handlebars {{#each}} / {{#if}} blocks preserved"}</span>
            </div>
            <textarea
              ref={htmlRef}
              value={htmlContent}
              onChange={e => setHtmlContent(e.target.value)}
              className="flex-1 rounded-b-lg border border-border border-t-0 bg-background font-mono text-xs p-4 resize-none outline-none leading-relaxed text-foreground min-h-[400px]"
              spellCheck={false}
              autoCorrect="off"
              autoCapitalize="off"
            />
          </div>
        ) : (
          /* Editor pane */
          <div className="flex-1 overflow-auto flex flex-col min-w-0">
            <EditorToolbar editor={editor} />
            <div className="flex-1 overflow-auto rounded-b-lg border border-border border-t-0 bg-background">
              <EditorContent editor={editor} />
            </div>
          </div>
        )}

        {/* Placeholder sidebar (always visible) */}
        <div className="w-64 shrink-0 overflow-y-auto">
          <PlaceholderSidebar groups={placeholders} onInsert={insertPlaceholder} />
        </div>
      </div>
    </div>
  );
}

// ── System template card ──────────────────────────────────────────────────────

function SystemTemplateCard({
  template,
  onEdit,
}: {
  template: DocumentTemplateDto;
  onEdit: (id: string) => void;
}) {
  const { t } = useTranslation();
  const label = SYSTEM_TEMPLATE_LABELS[template.templateType] ?? template.name;
  const stage = SYSTEM_TEMPLATE_STAGE[template.templateType];

  return (
    <div className="flex items-start gap-3 rounded-lg border border-border bg-card p-4 hover:border-primary/40 transition-colors">
      <div className="mt-0.5 rounded-md bg-primary/10 p-2 shrink-0">
        <FileText className="h-4 w-4 text-primary" />
      </div>

      <div className="flex-1 min-w-0">
        <p className="text-sm font-medium leading-tight">{label}</p>
        {stage && (
          <p className="text-xs text-muted-foreground mt-0.5">{stage}</p>
        )}
        <div className="flex items-center gap-2 mt-2 flex-wrap">
          {template.hasContent ? (
            <Badge className="text-xs bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-400 border-0">
              <CheckCircle2 className="h-3 w-3 mr-1" />
              {t.templateSettings.contentDefined}
            </Badge>
          ) : (
            <Badge variant="secondary" className="text-xs border-0 bg-amber-100 text-amber-700 dark:bg-amber-900/30 dark:text-amber-400">
              <AlertTriangle className="h-3 w-3 mr-1" />
              {t.templateSettings.noContent}
            </Badge>
          )}
          {!template.isActive && (
            <Badge variant="secondary" className="text-xs">{t.workflowStages.inactive}</Badge>
          )}
        </div>
      </div>

      <Button
        variant="ghost"
        size="sm"
        onClick={() => onEdit(template.id)}
        className="shrink-0"
      >
        <Pencil className="h-3.5 w-3.5 mr-1" />
        {t.templateSettings.edit}
      </Button>
    </div>
  );
}

// ── Custom template card ──────────────────────────────────────────────────────

function CustomTemplateCard({
  template,
  onEdit,
  onDelete,
  deleting,
}: {
  template: DocumentTemplateDto;
  onEdit: (id: string) => void;
  onDelete: (id: string) => void;
  deleting: boolean;
}) {
  const { t } = useTranslation();
  return (
    <div className="flex items-start gap-3 rounded-lg border border-border bg-card p-4 hover:border-primary/40 transition-colors">
      <div className="mt-0.5 rounded-md bg-muted p-2 shrink-0">
        <FileText className="h-4 w-4 text-muted-foreground" />
      </div>

      <div className="flex-1 min-w-0">
        <p className="text-sm font-medium leading-tight">{template.name}</p>
        {template.description && (
          <p className="text-xs text-muted-foreground mt-0.5 truncate">{template.description}</p>
        )}
        <div className="flex items-center gap-2 mt-2 flex-wrap">
          {template.category && (
            <Badge variant="outline" className="text-xs">{template.category}</Badge>
          )}
          {template.hasContent ? (
            <Badge className="text-xs bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-400 border-0">
              <CheckCircle2 className="h-3 w-3 mr-1" />
              {t.templateSettings.hasContent}
            </Badge>
          ) : (
            <Badge variant="secondary" className="text-xs">{t.templateSettings.empty}</Badge>
          )}
          {!template.isActive && (
            <Badge variant="secondary" className="text-xs">{t.workflowStages.inactive}</Badge>
          )}
        </div>
      </div>

      <div className="flex gap-1 shrink-0">
        <Button variant="ghost" size="sm" onClick={() => onEdit(template.id)}>
          <Pencil className="h-3.5 w-3.5 mr-1" />
          {t.templateSettings.edit}
        </Button>
        <Button
          variant="ghost"
          size="sm"
          onClick={() => onDelete(template.id)}
          disabled={deleting}
          className="text-destructive hover:text-destructive"
        >
          {deleting ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Trash2 className="h-3.5 w-3.5" />}
        </Button>
      </div>
    </div>
  );
}

// ── Incoming document card (sample PDF upload for AI recognition) ─────────────

function IncomingDocumentCard({
  type,
  status,
  onUploaded,
}: {
  type: IncomingDocumentType;
  status: IncomingDocumentReferenceStatus | null;
  onUploaded: () => void;
}) {
  const [uploading, setUploading] = useState(false);
  const [uploadPct, setUploadPct] = useState<number | null>(null);
  const [dragOver, setDragOver] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [annotatorOpen, setAnnotatorOpen] = useState(false);
  const [lastUploadedFile, setLastUploadedFile] = useState<File | null>(null);
  const [profile, setProfile] = useState<import("@/services/api/documentTemplatesApi").IncomingDocumentProfile | null>(null);
  const fileRef = useRef<HTMLInputElement>(null);
  const { t, locale } = useTranslation();

  const reloadProfile = () => {
    documentTemplatesApi
      .getIncomingDocumentProfile(type)
      .then((r) => { if (r.data.exists) setProfile(r.data); })
      .catch(() => {});
  };

  // Load profile when reference exists
  useEffect(() => {
    if (status?.exists) reloadProfile();
  }, [type, status?.exists]);

  const handleFile = async (file: File) => {
    if (!file.name.toLowerCase().endsWith(".pdf")) {
      setError(t.templateSettings.onlyPdfError);
      return;
    }
    setUploading(true);
    setUploadPct(0);
    setError(null);
    try {
      await documentTemplatesApi.uploadIncomingReference(type, file, (pct) => setUploadPct(pct));
      onUploaded();
      // Auto-open annotator with the freshly uploaded file
      setLastUploadedFile(file);
      setAnnotatorOpen(true);
    } catch {
      setError(t.templateSettings.uploadError);
    } finally {
      setUploading(false);
      setUploadPct(null);
    }
  };

  const annotationCount = profile?.annotationCount ?? null;
  const hasAiSummary = !!(profile?.aiSummaryEn || profile?.aiSummaryRo || profile?.aiSummaryHu);

  return (
    <div className="rounded-xl border border-border bg-card p-5 space-y-4">
      {/* Annotator modal */}
      {annotatorOpen && (
        <PdfAnnotatorModal
          type={type}
          uploadedFile={lastUploadedFile}
          onClose={() => { setAnnotatorOpen(false); setLastUploadedFile(null); }}
          onSaved={() => { reloadProfile(); }}
        />
      )}

      {/* Header */}
      <div className="flex items-start justify-between gap-3">
        <div className="flex items-center gap-2.5">
          <div className="rounded-lg bg-primary/10 p-2">
            <FileText className="h-5 w-5 text-primary" />
          </div>
          <div>
            <p className="text-sm font-semibold text-foreground">{getIncomingDocumentLabel(type, locale)}</p>
            <div className="flex items-center gap-1.5 mt-0.5 flex-wrap">
              <Badge variant="outline" className="text-[10px] rounded-md px-1.5 py-0.5 border-amber-400 text-amber-600 dark:text-amber-400">
                {t.templateSettings.incomingBadge}
              </Badge>
              {status?.exists && (
                <Badge variant="outline" className="text-[10px] rounded-md px-1.5 py-0.5 border-emerald-400 text-emerald-600 dark:text-emerald-400 gap-1">
                  <CheckCircle className="h-2.5 w-2.5" />
                  {t.templateSettings.referenceUploaded}
                </Badge>
              )}
              {annotationCount !== null && annotationCount > 0 && (
                <Badge variant="outline" className="text-[10px] rounded-md px-1.5 py-0.5 border-blue-400 text-blue-600 dark:text-blue-400 gap-1">
                  <CheckCircle className="h-2.5 w-2.5" />
                  {annotationCount} {annotationCount !== 1 ? t.templateSettings.fields : t.templateSettings.field} {t.templateSettings.annotated}
                </Badge>
              )}
              {hasAiSummary && (
                <Badge variant="outline" className="text-[10px] rounded-md px-1.5 py-0.5 border-purple-400 text-purple-600 dark:text-purple-400 gap-1">
                  <CheckCircle className="h-2.5 w-2.5" />
                  {t.templateSettings.aiProfileBadge}
                </Badge>
              )}
            </div>
          </div>
        </div>
        {/* Annotate button — visible once a reference PDF exists */}
        {status?.exists && (
          <Button
            size="sm"
            variant="outline"
            onClick={() => { setLastUploadedFile(null); setAnnotatorOpen(true); }}
            className="shrink-0"
          >
            <Pencil className="h-3.5 w-3.5 mr-1.5" />
            {hasAiSummary ? t.templateSettings.viewEdit : t.templateSettings.annotate}
          </Button>
        )}
      </div>

      {/* Description */}
      <div className="flex gap-2 rounded-lg bg-blue-50 dark:bg-blue-950/30 border border-blue-200 dark:border-blue-800 p-3">
        <Info className="h-4 w-4 text-blue-500 shrink-0 mt-0.5" />
        <p className="text-xs text-blue-700 dark:text-blue-300">
          {getIncomingDocumentDescription(type, locale)}
        </p>
      </div>

      {/* Upload area */}
      {!uploading ? (
        <div
          className={`rounded-lg border-2 border-dashed transition-colors cursor-pointer p-5 text-center ${
            dragOver ? "border-primary bg-primary/5" : "border-border hover:border-primary/40"
          }`}
          onClick={() => fileRef.current?.click()}
          onDragOver={(e) => { e.preventDefault(); setDragOver(true); }}
          onDragLeave={() => setDragOver(false)}
          onDrop={(e) => {
            e.preventDefault();
            setDragOver(false);
            const f = e.dataTransfer.files[0];
            if (f) handleFile(f);
          }}
        >
          <Upload className="h-6 w-6 text-muted-foreground mx-auto mb-2" />
          {status?.exists ? (
            <div>
              <p className="text-sm font-medium text-foreground">{t.templateSettings.replaceReference}</p>
              <p className="text-xs text-muted-foreground mt-0.5">
                {t.templateSettings.currentFile} <span className="font-mono">{profile?.originalFileName ?? status.fileName ?? "reference"}</span>
                {profile?.fileSizeBytes ? ` (${(profile.fileSizeBytes / 1024).toFixed(0)} KB)` : ""}
              </p>
            </div>
          ) : (
            <div>
              <p className="text-sm font-medium text-foreground">{t.templateSettings.uploadPdfReference}</p>
              <p className="text-xs text-muted-foreground mt-0.5">{t.templateSettings.clickOrDrag}</p>
            </div>
          )}
          <input
            ref={fileRef}
            type="file"
            accept=".pdf,application/pdf"
            className="hidden"
            onChange={(e) => { const f = e.target.files?.[0]; e.target.value = ""; if (f) handleFile(f); }}
          />
        </div>
      ) : (
        <div className="space-y-2 py-2">
          <div className="flex items-center justify-between text-xs text-muted-foreground">
            <span>{uploadPct !== null && uploadPct < 100 ? `${t.templateSettings.uploading} ${uploadPct}%` : t.templateSettings.processing}</span>
          </div>
          <div className="h-1.5 w-full rounded-full bg-muted overflow-hidden">
            <div
              className={`h-full rounded-full transition-all duration-300 ${uploadPct !== null && uploadPct < 100 ? "bg-primary" : "bg-amber-500 animate-pulse"}`}
              style={{ width: `${uploadPct !== null && uploadPct < 100 ? uploadPct : 100}%` }}
            />
          </div>
        </div>
      )}

      {error && (
        <p className="text-xs text-destructive flex items-center gap-1">
          <AlertTriangle className="h-3.5 w-3.5" />
          {error}
        </p>
      )}

      {/* AI summary snippet */}
      {hasAiSummary && profile && (
        <div className="rounded-lg border border-purple-200 dark:border-purple-800 bg-purple-50 dark:bg-purple-950/30 p-3 space-y-1.5">
          <div className="flex items-center gap-1.5">
            <span className="text-[10px] font-semibold text-purple-700 dark:text-purple-300 uppercase tracking-wider">{t.templateSettings.aiProfileLabel}</span>
            {profile.aiConfidence != null && (
              <span className={`text-[9px] font-semibold rounded-full px-1.5 py-0.5 ${
                profile.aiConfidence >= 0.8
                  ? "bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-300"
                  : "bg-yellow-100 text-yellow-800 dark:bg-yellow-900/30 dark:text-yellow-300"
              }`}>
                {Math.round(profile.aiConfidence * 100)}% {t.templateSettings.confLabel}
              </span>
            )}
            {profile.aiAnalysedOn && (
              <span className="text-[9px] text-muted-foreground ml-auto">{new Date(profile.aiAnalysedOn).toLocaleDateString()}</span>
            )}
          </div>
          <p className="text-xs text-purple-800 dark:text-purple-200 leading-relaxed line-clamp-3">
            {profile.aiSummaryEn}
          </p>
          <p className="text-[10px] text-purple-600 dark:text-purple-400">
            {t.templateSettings.summariesAvailable}
          </p>
        </div>
      )}

      {/* AI recognition status */}
      {status?.exists && (
        <div className="flex items-center gap-2 rounded-lg bg-emerald-50 dark:bg-emerald-950/30 border border-emerald-200 dark:border-emerald-800 p-3">
          <CheckCircle2 className="h-4 w-4 text-emerald-600 dark:text-emerald-400 shrink-0" />
          <p className="text-xs text-emerald-700 dark:text-emerald-300">
            {t.templateSettings.aiRecognitionActive} <strong>{getIncomingDocumentLabel(type, locale)}</strong>.
          </p>
        </div>
      )}
    </div>
  );
}

// ── New template form ─────────────────────────────────────────────────────────

function NewTemplateForm({
  onCreated,
  onCancel,
}: {
  onCreated: (id: string) => void;
  onCancel: () => void;
}) {
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [category, setCategory] = useState("");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const { t } = useTranslation();

  const handleCreate = async () => {
    if (!name.trim()) { setError(t.templateSettings.templateNamePlaceholder + " is required."); return; }
    setSaving(true);
    setError("");
    try {
      const r = await documentTemplatesApi.create({ name: name.trim(), description, category });
      onCreated(r.data.id);
    } catch {
      setError("Creation error. Please try again.");
      setSaving(false);
    }
  };

  return (
    <div className="rounded-lg border border-border bg-card p-4 space-y-3">
      <p className="text-sm font-semibold">{t.templateSettings.newTemplate}</p>
      <input
        autoFocus
        value={name}
        onChange={(e) => setName(e.target.value)}
        onKeyDown={(e) => e.key === "Enter" && handleCreate()}
        placeholder="Denumire șablon *"
        className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm outline-none focus:ring-1 focus:ring-primary"
      />
      <input
        value={description}
        onChange={(e) => setDescription(e.target.value)}
        placeholder={t.templateSettings.descOptional}
        className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm outline-none focus:ring-1 focus:ring-primary"
      />
      <input
        value={category}
        onChange={(e) => setCategory(e.target.value)}
        placeholder={t.templateSettings.categoryOptional}
        className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm outline-none focus:ring-1 focus:ring-primary"
      />
      {error && <p className="text-xs text-destructive">{error}</p>}
      <div className="flex gap-2">
        <Button size="sm" onClick={handleCreate} disabled={saving}>
          {saving && <Loader2 className="h-3.5 w-3.5 animate-spin mr-1" />}
          {t.templateSettings.createAndOpen}
        </Button>
        <Button size="sm" variant="ghost" onClick={onCancel}>{t.common.cancel}</Button>
      </div>
    </div>
  );
}

// ── Main page ─────────────────────────────────────────────────────────────────

export default function TemplateSettingsPage() {
  const { locale, t } = useTranslation();
  const [tab, setTab] = useState<"system" | "custom" | "incoming">("system");
  const [templates, setTemplates] = useState<DocumentTemplateDto[]>([]);
  const [placeholders, setPlaceholders] = useState<PlaceholderGroup[]>([]);
  const [loadingList, setLoadingList] = useState(true);
  const [editingTemplate, setEditingTemplate] = useState<DocumentTemplateDetailDto | null>(null);
  const [loadingEditor, setLoadingEditor] = useState(false);
  const [showNewForm, setShowNewForm] = useState(false);
  const [deletingId, setDeletingId] = useState<string | null>(null);
  const [incomingStatuses, setIncomingStatuses] = useState<Record<string, IncomingDocumentReferenceStatus>>({});

  // System templates: exclude CourtOpeningDecision (it's an incoming document, not generated)
  const systemTemplates = templates.filter((t) => t.isSystem && t.templateType !== "courtOpeningDecision");
  const customTemplates = templates.filter((t) => !t.isSystem);

  const INCOMING_TYPES: IncomingDocumentType[] = ["CourtOpeningDecision"];

  const incomingText = {
    en: {
      tab: "Incoming documents",
      description:
        "Incoming documents are issued by courts or third parties and received by the practitioner. Upload one sample PDF for each type so AI can auto-recognize and classify similar documents later.",
    },
    ro: {
      tab: "Documente primite",
      description:
        "Documentele primite sunt emise de instanță sau de terți și primite de practician. Încarcă un exemplu PDF pentru fiecare tip, iar AI-ul va recunoaște și clasifica automat documentele similare încărcate ulterior.",
    },
    hu: {
      tab: "Beérkező dokumentumok",
      description:
        "A beérkező dokumentumokat a bíróság vagy harmadik felek állítják ki, és a felszámoló kapja meg. Töltsön fel típusonként egy mint PDF-et, így az AI később automatikusan felismeri és osztályozza a hasonló dokumentumokat.",
    },
  }[locale];

  const loadTemplates = useCallback(async () => {
    setLoadingList(true);
    try {
      const [tmpl, ph] = await Promise.all([
        documentTemplatesApi.getAll(),
        documentTemplatesApi.getPlaceholders(),
      ]);
      setTemplates(tmpl.data);
      setPlaceholders(ph.data);
    } catch (err) {
      console.error(err);
    } finally {
      setLoadingList(false);
    }
  }, []);

  const loadIncomingStatuses = useCallback(async () => {
    const results = await Promise.allSettled(
      INCOMING_TYPES.map((t) => documentTemplatesApi.getIncomingReference(t))
    );
    const next: Record<string, IncomingDocumentReferenceStatus> = {};
    results.forEach((r, i) => {
      if (r.status === "fulfilled") next[INCOMING_TYPES[i]] = r.value.data;
      else next[INCOMING_TYPES[i]] = { type: INCOMING_TYPES[i], exists: false };
    });
    setIncomingStatuses(next);
  }, []);

  useEffect(() => {
    loadTemplates();
    loadIncomingStatuses();
  }, [loadTemplates, loadIncomingStatuses]);

  const openEditor = async (id: string) => {
    setLoadingEditor(true);
    try {
      const r = await documentTemplatesApi.getById(id);
      setEditingTemplate(r.data);
    } catch (err) {
      console.error(err);
    } finally {
      setLoadingEditor(false);
    }
  };

  const handleSave = async (
    id: string,
    bodyHtml: string,
    meta: { name: string; description: string; category: string; isActive: boolean }
  ) => {
    if (!editingTemplate) return;
    await documentTemplatesApi.update(id, {
      name: meta.name || editingTemplate.name,
      description: meta.description || undefined,
      category: meta.category || undefined,
      bodyHtml,
      isActive: meta.isActive,
    });
    // Refresh list (hasContent flag may change)
    setTemplates((prev) =>
      prev.map((t) => (t.id === id ? { ...t, hasContent: bodyHtml.length > 50 } : t))
    );
  };

  const handleDelete = async (id: string) => {
    if (!window.confirm(t.templateSettings.deleteConfirm)) return;
    setDeletingId(id);
    try {
      await documentTemplatesApi.delete(id);
      setTemplates((prev) => prev.filter((t) => t.id !== id));
      if (editingTemplate?.id === id) setEditingTemplate(null);
    } catch (err) {
      console.error(err);
    } finally {
      setDeletingId(null);
    }
  };

  const handleCreated = async (newId: string) => {
    setShowNewForm(false);
    await loadTemplates();
    await openEditor(newId);
    setTab("custom");
  };

  // ── Render: loading editor ───────────────────────────────────────────────

  if (loadingEditor) {
    return (
      <div className="flex h-96 items-center justify-center">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  // ── Render: editor panel (full page) ─────────────────────────────────────

  if (editingTemplate) {
    return (
      <div className="flex flex-col h-[calc(100vh-8rem)]">
        <TemplateEditorPanel
          template={editingTemplate}
          placeholders={placeholders}
          onSave={handleSave}
          onClose={() => setEditingTemplate(null)}
        />
      </div>
    );
  }

  // ── Render: list view ─────────────────────────────────────────────────────

  return (
    <div className="space-y-6">
      {/* Page header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-bold">{t.templateSettings.pageTitle}</h1>
          <p className="text-sm text-muted-foreground mt-1">
{t.templateSettings.pageDesc}</p>
        </div>
        <Button onClick={() => { setShowNewForm(true); setTab("custom"); }}>
          <Plus className="h-4 w-4 mr-1.5" />
          {t.templateSettings.newTemplate}
        </Button>
      </div>

      {/* Tabs */}
      <div className="flex border-b border-border">
        <button
          type="button"
          onClick={() => setTab("system")}
          className={`
            px-4 py-2 text-sm font-medium border-b-2 transition-colors
            ${tab === "system"
              ? "border-primary text-foreground"
              : "border-transparent text-muted-foreground hover:text-foreground"
            }
          `}
        >
          {t.templateSettings.tabRequired}{" "}
          <span className="ml-1 text-xs text-muted-foreground">({systemTemplates.length})</span>
        </button>
        <button
          type="button"
          onClick={() => setTab("custom")}
          className={`
            px-4 py-2 text-sm font-medium border-b-2 transition-colors
            ${tab === "custom"
              ? "border-primary text-foreground"
              : "border-transparent text-muted-foreground hover:text-foreground"
            }
          `}
        >
          Custom{" "}
          <span className="ml-1 text-xs text-muted-foreground">({customTemplates.length})</span>
        </button>
        <button
          type="button"
          onClick={() => setTab("incoming")}
          className={`
            px-4 py-2 text-sm font-medium border-b-2 transition-colors
            ${tab === "incoming"
              ? "border-primary text-foreground"
              : "border-transparent text-muted-foreground hover:text-foreground"
            }
          `}
        >
          {incomingText.tab}{" "}
          <span className="ml-1 text-xs text-muted-foreground">({INCOMING_TYPES.length})</span>
        </button>
      </div>

      {/* Loading */}
      {loadingList && (
        <div className="flex items-center gap-2 text-muted-foreground text-sm">
          <Loader2 className="h-4 w-4 animate-spin" />
          {t.templateSettings.loading}
        </div>
      )}

      {/* System templates tab */}
      {!loadingList && tab === "system" && (
        <div className="space-y-3">
          <p className="text-xs text-muted-foreground">
            Aceste șabloane sunt obligatorii conform procedurii de insolvență. Definește conținutul HTML
            cu câmpuri dinamice din dosar — vor fi completate automat la generare.
          </p>
          {systemTemplates.length === 0 ? (
            <div className="rounded-lg border border-dashed border-border p-8 text-center">
              <FileText className="h-8 w-8 text-muted-foreground mx-auto mb-2" />
              <p className="text-sm text-muted-foreground">
                {t.templateSettings.noSystemTemplates}
              </p>
            </div>
          ) : (
            <div className="grid gap-3 sm:grid-cols-2">
              {systemTemplates.map((t) => (
                <SystemTemplateCard key={t.id} template={t} onEdit={openEditor} />
              ))}
            </div>
          )}
        </div>
      )}

      {/* Custom templates tab */}
      {!loadingList && tab === "custom" && (
        <div className="space-y-3">
          <p className="text-xs text-muted-foreground">
            Șabloane create de tine pentru orice scop — notificări, adrese, rapoarte interne.
            Inserează câmpuri dinamice din dosar, debitor, creditori și alte persoane implicate.
          </p>

          {/* New template form */}
          {showNewForm && (
            <NewTemplateForm
              onCreated={handleCreated}
              onCancel={() => setShowNewForm(false)}
            />
          )}

          {customTemplates.length === 0 && !showNewForm ? (
            <div className="rounded-lg border border-dashed border-border p-8 text-center">
              <FileText className="h-8 w-8 text-muted-foreground mx-auto mb-2" />
              <p className="text-sm text-muted-foreground mb-3">
                {t.templateSettings.noCustomTemplates}
              </p>
              <Button variant="outline" size="sm" onClick={() => setShowNewForm(true)}>
                <Plus className="h-3.5 w-3.5 mr-1" />
                {t.templateSettings.createFirstTemplate}
              </Button>
            </div>
          ) : (
            <div className="grid gap-3 sm:grid-cols-2">
              {customTemplates.map((t) => (
                <CustomTemplateCard
                  key={t.id}
                  template={t}
                  onEdit={openEditor}
                  onDelete={handleDelete}
                  deleting={deletingId === t.id}
                />
              ))}
            </div>
          )}
        </div>
      )}

      {/* Incoming documents tab */}
      {!loadingList && tab === "incoming" && (
        <div className="space-y-4">
          <p className="text-xs text-muted-foreground">
            {incomingText.description}
          </p>
          {INCOMING_TYPES.map((type) => (
            <IncomingDocumentCard
              key={type}
              type={type}
              status={incomingStatuses[type] ?? null}
              onUploaded={loadIncomingStatuses}
            />
          ))}
        </div>
      )}
    </div>
  );
}
