/**
 * PdfAnnotatorModal â€” Text-selection annotation (v2)
 *
 * Replaces the old click-drag bounding-box system with native browser text
 * selection.  Workflow:
 *   1. The PDF is rendered on the LEFT as a read-only visual canvas.
 *   2. Extracted text is shown on the RIGHT in a scrollable, selectable panel.
 *   3. User highlights any text in the right panel â†’ an assignment bar
 *      appears at the bottom.
 *   4. The bar shows the selected text with adjustable surrounding context
 *      (Â±N chars) and a field selector. Click "Add" to confirm.
 *   5. Confirmed annotations are listed below the text panel, with annotated
 *      spans highlighted in-place in the text.
 *   6. "Analyse with AI" and "Save" work the same as before.
 */
import { useEffect, useRef, useState, useCallback, useMemo } from "react";
import * as pdfjsLib from "pdfjs-dist";
import {
  documentTemplatesApi,
  type IncomingDocumentType,
  type IncomingAnnotationItem,
  type IncomingAnnotationsPayload,
  type IncomingDocumentProfile,
  INCOMING_DOCUMENT_LABELS,
} from "@/services/api/documentTemplatesApi";
import { postCorrectionFeedback, computeCorrectionDiff } from "@/services/api/aiFeedbackApi";
import { Button } from "@/components/ui/button";
import {
  Loader2, Save, X, Trash2, CheckCircle2, Brain, Sparkles, RefreshCw,
  ChevronLeft, ChevronRight, Type, Plus,
} from "lucide-react";

pdfjsLib.GlobalWorkerOptions.workerSrc = new URL(
  "pdfjs-dist/build/pdf.worker.min.mjs",
  import.meta.url,
).toString();

// â”€â”€ Annotatable field catalogue â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

export interface AnnotatableField {
  field: string;
  label: string;
  group: string;
  color: string;
  textColor: string;
}

export const ANNOTATABLE_FIELDS: AnnotatableField[] = [
  // Case
  { field: "CaseNumber",            label: "Case Number",            group: "Case",         color: "#f97316", textColor: "#7c2d12" },
  { field: "ProcedureType",         label: "Procedure Type",         group: "Case",         color: "#fb923c", textColor: "#7c2d12" },
  { field: "OpeningDecisionNo",     label: "Opening Decision No.",   group: "Case",         color: "#fdba74", textColor: "#7c2d12" },
  // Debtor
  { field: "DebtorName",            label: "Debtor Name",            group: "Debtor",       color: "#3b82f6", textColor: "#1e3a8a" },
  { field: "DebtorCui",             label: "Debtor CUI / Tax ID",    group: "Debtor",       color: "#60a5fa", textColor: "#1e3a8a" },
  { field: "DebtorAddress",         label: "Debtor Address",         group: "Debtor",       color: "#93c5fd", textColor: "#1e3a8a" },
  // Court
  { field: "CourtName",             label: "Court / Tribunal",       group: "Court",        color: "#8b5cf6", textColor: "#3b0764" },
  { field: "CourtSection",          label: "Court Section",          group: "Court",        color: "#a78bfa", textColor: "#3b0764" },
  { field: "JudgeSyndic",           label: "Judge / Syndic",         group: "Court",        color: "#c4b5fd", textColor: "#3b0764" },
  { field: "Registrar",             label: "Registrar (Grefier)",    group: "Court",        color: "#ddd6fe", textColor: "#3b0764" },
  // Dates
  { field: "OpeningDate",           label: "Opening Date",           group: "Dates",        color: "#22c55e", textColor: "#14532d" },
  { field: "ClaimsDeadline",        label: "Claims Deadline",        group: "Dates",        color: "#4ade80", textColor: "#14532d" },
  { field: "ContestationsDeadline", label: "Contestations Deadline", group: "Dates",        color: "#86efac", textColor: "#14532d" },
  { field: "NextHearingDate",       label: "Next Hearing Date",      group: "Dates",        color: "#d9f99d", textColor: "#14532d" },
  // Practitioner
  // (removed — the insolvency practitioner is the system user/tenant, not a document field to annotate)
];

const fieldByKey = Object.fromEntries(ANNOTATABLE_FIELDS.map((f) => [f.field, f]));

// â”€â”€ Helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

/** Return the character offset of the selection start within rootEl's text content. */
function getSelectionStartOffset(rootEl: Element, range: Range): number {
  const before = document.createRange();
  before.selectNodeContents(rootEl);
  before.setEnd(range.startContainer, range.startOffset);
  return before.toString().length;
}

/** Extract all text from every page of a PDF document. */
/**
 * Locate `needle` inside `haystack`. Tries exact match first, then falls back
 * to a case-insensitive comparison to handle AI normalisation differences.
 * Returns the character offset of the match, or -1 if not found.
 */
function findTextIndex(haystack: string, needle: string): number {
  const exact = haystack.indexOf(needle);
  if (exact !== -1) return exact;
  return haystack.toLowerCase().indexOf(needle.toLowerCase());
}

async function extractDocumentText(doc: pdfjsLib.PDFDocumentProxy): Promise<string> {
  const parts: string[] = [];
  for (let p = 1; p <= doc.numPages; p++) {
    const page = await doc.getPage(p);
    const tc = await page.getTextContent();
    let pageText = "";
    for (const item of tc.items as Array<{ str: string; hasEOL?: boolean }>) {
      pageText += item.str;
      if (item.hasEOL) pageText += "\n";
    }
    if (doc.numPages > 1) {
      parts.push(`-- Page ${p} ${"-".repeat(40)}\n${pageText.trim()}`);
    } else {
      parts.push(pageText);
    }
  }
  return parts.join("\n\n");
}

// â”€â”€ Main component â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

interface PdfAnnotatorModalProps {
  type: IncomingDocumentType;
  /** Freshly uploaded File object (takes priority over server fetch). */
  uploadedFile?: File | null;
  onClose: () => void;
  onSaved: () => void;
}

export function PdfAnnotatorModal({
  type,
  uploadedFile,
  onClose,
  onSaved,
}: PdfAnnotatorModalProps) {
  const canvasRef    = useRef<HTMLCanvasElement>(null);
  const textPanelRef = useRef<HTMLDivElement>(null);
  const pdfDocRef    = useRef<pdfjsLib.PDFDocumentProxy | null>(null);

  // PDF visual
  const [currentPage, setCurrentPage] = useState(1);
  const [totalPages,  setTotalPages]  = useState(0);
  const [pdfLoading,  setPdfLoading]  = useState(true);
  const [pdfError,    setPdfError]    = useState<string | null>(null);

  // Text extraction
  const [fullText,         setFullText]         = useState<string>("");
  const [textLoading,      setTextLoading]      = useState(false);
  const [isManualText,     setIsManualText]     = useState(false);
  const [manualTextDraft,  setManualTextDraft]  = useState("");

  // Pending selection â€” waiting for field assignment
  const [pendingSelection, setPendingSelection] = useState<{
    text: string;
    startIndex: number;
  } | null>(null);
  const [contextChars, setContextChars] = useState(60);
  const [assignField,  setAssignField]  = useState<string>("CaseNumber");

  // Saved annotations
  const [annotations,       setAnnotations]       = useState<IncomingAnnotationItem[]>([]);
  const [annotationsLoaded, setAnnotationsLoaded] = useState(false);
  const [notes,             setNotes]             = useState<string>("");
  const [saving,            setSaving]            = useState(false);
  const [savedOk,           setSavedOk]           = useState(false);

  // AI
  const [profile,      setProfile]      = useState<IncomingDocumentProfile | null>(null);
  const [analysing,    setAnalysing]    = useState(false);
  const [analyseError, setAnalyseError] = useState<string | null>(null);
  const [aiTab,        setAiTab]        = useState<"en" | "ro" | "hu">("en");
  const [aiDone,       setAiDone]       = useState(false);
  const [suggesting,           setSuggesting]           = useState(false);
  const [suggestError,          setSuggestError]          = useState<string | null>(null);
  const [suggestAiNotConfigured, setSuggestAiNotConfigured] = useState(false);
  const [suggestNoFields,        setSuggestNoFields]        = useState(false);
  // Incrementing this counter forces the auto-suggest effect to re-run (retry)
  const [suggestTrigger,         setSuggestTrigger]         = useState(0);

  // Stores the initial AI-suggested annotation values for correction diffing
  const aiSuggestionsRef = useRef<Record<string, string>>({});

  // â”€â”€ Render one page to the canvas â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

  const renderPage = useCallback(async (
    doc: pdfjsLib.PDFDocumentProxy,
    pageNum: number,
  ) => {
    const page     = await doc.getPage(pageNum);
    const viewport = page.getViewport({ scale: 1.5 });
    const canvas   = canvasRef.current;
    if (!canvas) return;
    canvas.width  = viewport.width;
    canvas.height = viewport.height;
    const ctx = canvas.getContext("2d");
    if (!ctx) return;
    await page.render({ canvasContext: ctx, viewport }).promise;
  }, []);

  // â”€â”€ Load PDF â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

  useEffect(() => {
    let cancelled = false;

    const init = async (arrayBuffer: ArrayBuffer) => {
      // Step 1: Render PDF visually
      try {
        const doc = await pdfjsLib.getDocument({ data: arrayBuffer }).promise;
        if (cancelled) return;
        pdfDocRef.current = doc;
        setTotalPages(doc.numPages);
        await renderPage(doc, 1);
        if (!cancelled) setPdfLoading(false);
      } catch {
        if (!cancelled) {
          setPdfError("Failed to render PDF. Make sure the file is a valid PDF document.");
          setPdfLoading(false);
        }
        return;
      }

      // Step 2: Extract text separately — errors here must NOT affect PDF display state
      if (!pdfDocRef.current || cancelled) return;
      setTextLoading(true);
      try {
        const text = await extractDocumentText(pdfDocRef.current);
        if (!cancelled) { setFullText(text); setTextLoading(false); }
      } catch {
        // Text extraction failed (e.g. pdfjs worker error on scanned PDFs)
        // Leave PDF visible; user can enter text manually
        if (!cancelled) setTextLoading(false);
      }
    };

    const load = async () => {
      setPdfLoading(true);
      setPdfError(null);
      setFullText("");
      setCurrentPage(1);
      try {
        if (uploadedFile) {
          await init(await uploadedFile.arrayBuffer());
        } else {
          const token    = localStorage.getItem("authToken");
          const tenantId = localStorage.getItem("selectedTenantId");
          const res = await fetch(
            documentTemplatesApi.getIncomingReferenceFileUrl(type),
            {
              headers: {
                ...(token    ? { Authorization: `Bearer ${token}` }  : {}),
                ...(tenantId ? { "X-Tenant-Id": tenantId }           : {}),
              },
            },
          );
          if (!res.ok) throw new Error(`HTTP ${res.status}`);
          await init(await res.arrayBuffer());
        }
      } catch {
        if (!cancelled) { setPdfError("Could not load PDF."); setPdfLoading(false); }
      }
    };

    load();
    return () => { cancelled = true; };
  }, [type, uploadedFile, renderPage]);

  // -- Load saved annotations (skipped when a fresh file was just uploaded) --

  useEffect(() => {
    if (uploadedFile) {
      // New file uploaded — start with a clean slate so AI auto-suggest runs
      setAnnotations([]);
      setNotes("");
      setAnnotationsLoaded(true);
      return;
    }
    documentTemplatesApi
      .getIncomingAnnotations(type)
      .then((r) => {
        setAnnotations((r.data.annotations ?? []) as IncomingAnnotationItem[]);
        setNotes(r.data.notes ?? "");
      })
      .catch(() => {})
      .finally(() => setAnnotationsLoaded(true));
  }, [type, uploadedFile]);

  // -- Auto-suggest annotations when text is ready and no saved annotations exist --

  useEffect(() => {
    if (!fullText || !annotationsLoaded || annotations.length > 0 || suggesting) return;
    let cancelled = false;
    setSuggesting(true);
    setSuggestError(null);
    setSuggestAiNotConfigured(false);
    setSuggestNoFields(false);
    documentTemplatesApi
      .suggestAnnotations(type, fullText)
      .then((r) => {
        if (cancelled) return;
        const { suggestions: sugg = {}, aiConfigured } = r.data;
        if (!aiConfigured) {
          setSuggestAiNotConfigured(true);
          return;
        }
        const items: IncomingAnnotationItem[] = [];
        for (const [fieldName, verbatim] of Object.entries(sugg)) {
          if (!verbatim) continue;
          const fieldDef = ANNOTATABLE_FIELDS.find((f) => f.field === fieldName);
          if (!fieldDef) continue;
          // Try exact match, then case-insensitive fallback
          const idx = findTextIndex(fullText, verbatim);
          const actualText = idx !== -1 ? fullText.substring(idx, idx + verbatim.length) : verbatim;
          const before = idx > 0
            ? fullText.substring(Math.max(0, idx - 60), idx).trimStart()
            : "";
          const after = idx >= 0
            ? fullText.substring(idx + verbatim.length, idx + verbatim.length + 60).trimEnd()
            : "";
          items.push({
            id: crypto.randomUUID(),
            field: fieldDef.field,
            label: fieldDef.label,
            selectedText: actualText,
            contextBefore: before,
            contextAfter: after,
            aiSuggested: true,
          });
        }
        if (items.length > 0) {
          setAnnotations(items);
          // Snapshot AI suggestions for later correction diffing
          const snapshot: Record<string, string> = {};
          for (const item of items) snapshot[item.field] = item.selectedText;
          aiSuggestionsRef.current = snapshot;
        } else {
          setSuggestNoFields(true);
        }
      })
      .catch(() => {
        if (!cancelled) setSuggestError("AI annotation suggestion failed.");
      })
      .finally(() => { if (!cancelled) setSuggesting(false); });
    return () => { cancelled = true; };
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [fullText, annotationsLoaded, suggestTrigger]);

  // â”€â”€ Load AI profile â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

  useEffect(() => {
    documentTemplatesApi
      .getIncomingDocumentProfile(type)
      .then((r) => { if (r.data.exists) setProfile(r.data); })
      .catch(() => {});
  }, [type]);

  // â”€â”€ Page navigation â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

  const changePage = useCallback(async (delta: number) => {
    const doc  = pdfDocRef.current;
    if (!doc) return;
    const next = Math.max(1, Math.min(totalPages, currentPage + delta));
    if (next === currentPage) return;
    setCurrentPage(next);
    setPdfLoading(true);
    try { await renderPage(doc, next); } finally { setPdfLoading(false); }
  }, [currentPage, totalPages, renderPage]);

  // â”€â”€ Text selection capture â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

  const handleTextMouseUp = useCallback(() => {
    const sel = window.getSelection();
    if (!sel || sel.isCollapsed) return;

    const selectedText = sel.toString();
    if (selectedText.trim().length < 2) return;

    const textEl = textPanelRef.current;
    if (!textEl || sel.rangeCount === 0) return;

    const range = sel.getRangeAt(0);
    if (!textEl.contains(range.commonAncestorContainer)) return;

    const startIndex = getSelectionStartOffset(textEl, range);

    sel.removeAllRanges(); // replace browser selection with our own context bar
    setPendingSelection({ text: selectedText, startIndex });
  }, []);

  // â”€â”€ Confirm annotation â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

  const handleAddAnnotation = useCallback(() => {
    if (!pendingSelection) return;
    const { text, startIndex } = pendingSelection;

    const before = fullText
      .substring(Math.max(0, startIndex - contextChars), startIndex)
      .trimStart();
    const after = fullText
      .substring(startIndex + text.length, startIndex + text.length + contextChars)
      .trimEnd();

    const field = ANNOTATABLE_FIELDS.find((f) => f.field === assignField);
    if (!field) return;

    setAnnotations((prev) => [
      ...prev.filter((a) => a.field !== field.field), // one annotation per field
      { id: crypto.randomUUID(), field: field.field, label: field.label,
        selectedText: text, contextBefore: before, contextAfter: after },
    ]);
    setPendingSelection(null);
  }, [pendingSelection, fullText, contextChars, assignField]);

  // â”€â”€ Highlighted text â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

  const highlightedText = useMemo((): React.ReactNode => {
    if (!fullText) return null;

    type Span = { start: number; end: number; field: string; label: string };
    const spans: Span[] = [];
    for (const ann of annotations) {
      const idx = findTextIndex(fullText, ann.selectedText);
      if (idx !== -1)
        spans.push({ start: idx, end: idx + ann.selectedText.length, field: ann.field, label: ann.label });
    }
    spans.sort((a, b) => a.start - b.start);

    const nodes: React.ReactNode[] = [];
    let pos = 0;
    for (const span of spans) {
      if (span.start < pos) continue;
      if (span.start > pos) nodes.push(<span key={`t${pos}`}>{fullText.substring(pos, span.start)}</span>);
      const f = fieldByKey[span.field];
      nodes.push(
        <mark
          key={`m${span.start}`}
          title={span.label}
          style={{
            background:   (f?.color ?? "#3b82f6") + "44",
            outline:      `1px solid ${f?.color ?? "#3b82f6"}`,
            borderRadius: "2px",
            padding:      "0 1px",
          }}
        >
          {fullText.substring(span.start, span.end)}
        </mark>,
      );
      pos = span.end;
    }
    if (pos < fullText.length) nodes.push(<span key="tend">{fullText.substring(pos)}</span>);
    return nodes;
  }, [fullText, annotations]);

  // â”€â”€ Save â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

  const handleSave = async () => {
    setSaving(true);
    try {
      const payload: IncomingAnnotationsPayload = { annotations, notes: notes || null };
      await documentTemplatesApi.saveIncomingAnnotations(type, payload);

      // Post correction feedback if AI suggestions were captured
      if (Object.keys(aiSuggestionsRef.current).length > 0) {
        const userValues: Record<string, string> = {};
        for (const a of annotations) userValues[a.field] = a.selectedText;
        const diffs = computeCorrectionDiff(
          aiSuggestionsRef.current,
          userValues,
          type,
          "annotation_modal",
        );
        if (diffs.length > 0) {
          postCorrectionFeedback(diffs).catch(() => {/* non-critical */});
        }
      }

      setSavedOk(true);
      setTimeout(() => setSavedOk(false), 3000);
      onSaved();
      setProfile((prev) =>
        prev ? { ...prev, aiSummaryEn: null, aiSummaryRo: null, aiSummaryHu: null } : prev,
      );
    } catch {
      // keep saving=false so user can retry
    } finally {
      setSaving(false);
    }
  };

  // â”€â”€ AI analysis â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

  const handleAnalyse = async () => {
    setAnalysing(true);
    setAnalyseError(null);
    setAiDone(false);
    try {
      const r = await documentTemplatesApi.analyseIncomingDocument(type);
      setProfile((prev) => ({
        ...(prev ?? { type, exists: true }),
        aiSummaryEn: r.data.aiSummaryEn, aiSummaryRo: r.data.aiSummaryRo,
        aiSummaryHu: r.data.aiSummaryHu, aiParametersJson: r.data.aiParametersJson,
        aiModel: r.data.aiModel, aiConfidence: r.data.aiConfidence, aiAnalysedOn: r.data.aiAnalysedOn,
      }));
      setAiDone(true);
      setTimeout(() => setAiDone(false), 4000);
    } catch {
      setAnalyseError("AI analysis failed. Make sure AI is enabled in Settings > AI Config.");
    } finally {
      setAnalysing(false);
    }
  };

  // â”€â”€ Derived values used in the assignment bar â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

  const activeFieldDef = ANNOTATABLE_FIELDS.find((f) => f.field === assignField);

  const pendingContextBefore = pendingSelection
    ? fullText.substring(Math.max(0, pendingSelection.startIndex - contextChars), pendingSelection.startIndex)
    : "";
  const pendingContextAfter = pendingSelection
    ? fullText.substring(
        pendingSelection.startIndex + pendingSelection.text.length,
        pendingSelection.startIndex + pendingSelection.text.length + contextChars,
      )
    : "";

  // â”€â”€ Render â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

  return (
    <div className="fixed inset-0 z-50 flex flex-col bg-background">

      {/* â”€â”€ Header â”€â”€ */}
      <div className="flex items-center gap-3 border-b border-border bg-card px-4 py-3 shrink-0">
        <div className="flex-1 min-w-0">
          <p className="text-sm font-bold leading-tight">Annotate reference document</p>
          <p className="text-xs text-muted-foreground truncate">{INCOMING_DOCUMENT_LABELS[type]}</p>
        </div>
        <div className="flex items-center gap-2">
          <Button
            size="sm" variant="outline"
            onClick={handleAnalyse}
            disabled={analysing || saving}
            title="Generate AI summaries in EN / RO / HU and extract field parameters"
          >
            {analysing  ? <Loader2       className="h-3.5 w-3.5 animate-spin mr-1" />
             : aiDone   ? <Sparkles      className="h-3.5 w-3.5 mr-1 text-purple-500" />
             :             <Brain        className="h-3.5 w-3.5 mr-1" />}
            {aiDone ? "Analysis done" : analysing ? "Analysing..." : "Analyse with AI"}
          </Button>
          <Button size="sm" onClick={handleSave} disabled={saving || annotations.length === 0}>
            {saving   ? <Loader2     className="h-3.5 w-3.5 animate-spin mr-1" />
             : savedOk ? <CheckCircle2 className="h-3.5 w-3.5 mr-1 text-green-500" />
             :            <Save        className="h-3.5 w-3.5 mr-1" />}
            {savedOk
              ? "Saved"
              : `Save${annotations.length > 0 ? ` (${annotations.length})` : ""}`}
          </Button>
          <button
            type="button" onClick={onClose}
            className="rounded-md p-1.5 hover:bg-accent text-muted-foreground"
          >
            <X className="h-4 w-4" />
          </button>
        </div>
      </div>

      {/* â”€â”€ Instruction bar â”€â”€ */}
      <div className="flex items-center gap-2 border-b border-border bg-muted/30 px-4 py-1.5 shrink-0">
        <Type className="h-3.5 w-3.5 text-muted-foreground shrink-0" />
        <p className="text-xs text-muted-foreground">
          The PDF is shown on the left for reference.{" "}
          <span className="font-medium">Select any text in the right panel</span>{" "}
          and an assignment bar will appear â€” pick the field, adjust context, then{" "}
          <span className="font-medium">Add annotation</span>.
        </p>
      </div>

      {/* â”€â”€ Body â”€â”€ */}
      <div className="flex flex-1 min-h-0 overflow-hidden">

        {/* Left: PDF visual (read-only reference) */}
        <div className="flex-1 flex flex-col min-h-0 border-r border-border overflow-hidden">
          {totalPages > 1 && (
            <div className="flex items-center justify-center gap-3 py-2 bg-card border-b border-border shrink-0">
              <Button variant="ghost" size="sm" onClick={() => changePage(-1)} disabled={pdfLoading || currentPage === 1}>
                <ChevronLeft className="h-4 w-4" />
              </Button>
              <span className="text-xs text-muted-foreground">Page {currentPage} / {totalPages}</span>
              <Button variant="ghost" size="sm" onClick={() => changePage(1)} disabled={pdfLoading || currentPage === totalPages}>
                <ChevronRight className="h-4 w-4" />
              </Button>
            </div>
          )}
          <div className="flex-1 overflow-auto bg-gray-100 dark:bg-gray-900 flex items-start justify-center p-6">
            {pdfLoading && !pdfError && (
              <div className="flex flex-col items-center gap-3 mt-24 text-muted-foreground">
                <Loader2 className="h-6 w-6 animate-spin" />
                <p className="text-sm">Loading PDF...</p>
              </div>
            )}
            {pdfError && (
              <div className="mt-24 max-w-xs text-center space-y-2">
                <p className="text-sm text-destructive">{pdfError}</p>
                <p className="text-xs text-muted-foreground">
                  Visual preview unavailable. You can still annotate using the extracted text on the right.
                </p>
              </div>
            )}
            {/* Canvas always in DOM so renderPage can reach it */}
            <canvas
              ref={canvasRef}
              className="block shadow-lg"
              style={{ display: pdfLoading || pdfError ? "none" : "block" }}
            />
          </div>
        </div>

        {/* Right panel: text + annotations + notes + AI */}
        <div
          className="flex flex-col min-h-0 bg-card"
          style={{ width: "45%", minWidth: 360 }}
        >

          {/* Extracted text -- selectable */}
          <div className="flex flex-col min-h-0 border-b border-border" style={{ flex: "1 1 0" }}>
            <div className="px-3 py-2 border-b border-border flex items-center gap-2 shrink-0">
              <Type className="h-3.5 w-3.5 text-muted-foreground" />
              <p className="text-xs font-semibold text-muted-foreground uppercase tracking-wide flex-1">
                {isManualText ? "Enter document text" : "Extracted text -- select to annotate"}
              </p>
              {textLoading && <Loader2 className="h-3 w-3 animate-spin text-muted-foreground" />}
              {!textLoading && fullText && (
                <button
                  type="button"
                  onClick={() => { setFullText(""); setManualTextDraft(""); setIsManualText(false); }}
                  className="text-[10px] text-muted-foreground hover:text-foreground underline shrink-0"
                  title="Clear text and re-enter"
                >
                  clear
                </button>
              )}
            </div>

            {!textLoading && isManualText && !fullText ? (
              <div className="flex flex-col flex-1 min-h-0 p-3 gap-2">
                <p className="text-[10px] text-muted-foreground shrink-0">
                  Paste the document text below, then click <strong>Use this text</strong> to begin annotating.
                </p>
                <textarea
                  className="flex-1 w-full rounded-md border border-input bg-background px-2.5 py-1.5 text-xs font-mono outline-none resize-none focus:ring-1 focus:ring-primary leading-relaxed"
                  placeholder="Paste or type document text here..."
                  value={manualTextDraft}
                  onChange={(e) => setManualTextDraft(e.target.value)}
                  style={{ minHeight: 160 }}
                />
                <div className="flex justify-end gap-2 shrink-0">
                  <Button size="sm" variant="ghost" onClick={() => setIsManualText(false)}>Cancel</Button>
                  <Button
                    size="sm"
                    onClick={() => { setFullText(manualTextDraft); setIsManualText(false); }}
                    disabled={!manualTextDraft.trim()}
                  >
                    <Plus className="h-3.5 w-3.5 mr-1" />
                    Use this text
                  </Button>
                </div>
              </div>
            ) : (
              <div
                ref={textPanelRef}
                className="flex-1 overflow-y-auto p-3 text-xs font-mono text-foreground leading-relaxed whitespace-pre-wrap"
                style={{ cursor: "text", userSelect: "text" }}
                onMouseUp={handleTextMouseUp}
              >
                {textLoading ? (
                  <p className="text-center text-muted-foreground mt-6">Extracting text...</p>
                ) : fullText ? (
                  highlightedText
                ) : (
                  <div className="flex flex-col items-center gap-3 mt-8 px-3 text-center">
                    <p className="text-xs text-muted-foreground">
                      {pdfError
                        ? "PDF could not be loaded -- no text to display."
                        : "No extractable text was found in this PDF (it may be a scanned image)."}
                    </p>
                    {!pdfError && (
                      <Button size="sm" variant="outline" onClick={() => setIsManualText(true)}>
                        <Type className="h-3.5 w-3.5 mr-1" />
                        Enter text manually
                      </Button>
                    )}
                  </div>
                )}
              </div>
            )}
          </div>
          {/* Annotations list */}
          <div className="border-b border-border flex flex-col shrink-0" style={{ maxHeight: 200 }}>
            <div className="px-3 py-2 border-b border-border shrink-0 flex items-center gap-2">
              <p className="text-xs font-semibold text-muted-foreground uppercase tracking-wide flex-1">
                Annotations{annotations.length > 0 && ` (${annotations.length})`}
              </p>
              {suggesting && (
                <span className="flex items-center gap-1 text-[10px] text-purple-500">
                  <Loader2 className="h-3 w-3 animate-spin" />
                  AI suggesting...
                </span>
              )}
              {suggestError && (
                <span className="text-[10px] text-destructive">{suggestError}</span>
              )}
              {suggestAiNotConfigured && (
                <span className="text-[10px] text-amber-600 dark:text-amber-400">
                  AI not configured
                </span>
              )}
              {!suggesting && !suggestAiNotConfigured && annotationsLoaded && !!fullText && annotations.length === 0 && (
                <button
                  type="button"
                  title="Ask AI to identify fields in the extracted text"
                  onClick={() => {
                    setSuggestNoFields(false);
                    setSuggestError(null);
                    setSuggestTrigger((t) => t + 1);
                  }}
                  className="flex items-center gap-0.5 text-[10px] text-muted-foreground hover:text-purple-500 transition-colors"
                >
                  <RefreshCw className="h-3 w-3" />
                  {suggestNoFields ? "Retry AI" : "Ask AI"}
                </button>
              )}
            </div>
            <div className="overflow-y-auto flex-1 divide-y divide-border">
              {annotations.length === 0 ? (
                <p className="px-3 py-3 text-xs text-muted-foreground text-center">
                  {suggesting
                    ? "AI is identifying fields..."
                    : suggestAiNotConfigured
                    ? "Configure AI in Settings to enable automatic field detection."
                    : suggestNoFields
                    ? "AI could not identify fields in this document."
                    : "Select text above to add annotations."}
                </p>
              ) : (
                annotations.map((a) => {
                  const f = fieldByKey[a.field];
                  return (
                    <div key={a.id} className="flex items-start gap-2 px-3 py-2">
                      <span className="w-2 h-2 rounded-full shrink-0 mt-1" style={{ background: f?.color ?? "#aaa" }} />
                      <div className="flex-1 min-w-0">
                        <div className="flex items-center gap-1.5">
                          <p className="text-xs font-semibold truncate">{a.label}</p>
                          {a.aiSuggested && (
                            <span className="inline-flex items-center gap-0.5 rounded px-1 py-0 text-[9px] font-medium bg-purple-100 text-purple-700 dark:bg-purple-900/40 dark:text-purple-300 shrink-0">
                              <Sparkles className="h-2 w-2" />AI
                            </span>
                          )}
                        </div>
                        <p className="text-[10px] text-muted-foreground truncate" title={a.selectedText}>
                          "{a.selectedText}"
                        </p>
                        {(a.contextBefore || a.contextAfter) && (
                          <p
                            className="text-[10px] text-muted-foreground/60 truncate font-mono"
                            title={`...${a.contextBefore}[${a.selectedText}]${a.contextAfter}...`}
                          >
                            ...{a.contextBefore.slice(-20)}
                            <span className="font-bold not-italic" style={{ color: f?.color }}>
                              [{a.selectedText}]
                            </span>
                            {a.contextAfter.slice(0, 20)}...
                          </p>
                        )}
                      </div>
                      <button
                        type="button"
                        onClick={() => setAnnotations((prev) => prev.filter((x) => x.id !== a.id))}
                        className="text-muted-foreground hover:text-destructive transition-colors shrink-0 mt-0.5"
                      >
                        <Trash2 className="h-3 w-3" />
                      </button>
                    </div>
                  );
                })
              )}
            </div>
          </div>

          {/* Notes */}
          <div className="border-b border-border p-3 space-y-1 shrink-0">
            <p className="text-[10px] font-semibold text-muted-foreground uppercase tracking-wider">
              Notes (optional)
            </p>
            <textarea
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              placeholder="Describe this document type, layout variations, etc."
              rows={2}
              className="w-full rounded-md border border-input bg-background px-2.5 py-1.5 text-xs outline-none resize-none focus:ring-1 focus:ring-primary leading-relaxed"
            />
          </div>

          {/* AI summary */}
          <div className="p-3 space-y-2 overflow-y-auto shrink-0" style={{ maxHeight: 220 }}>
            <div className="flex items-center gap-1.5">
              <Brain className="h-3.5 w-3.5 text-purple-500 shrink-0" />
              <p className="text-[10px] font-semibold text-muted-foreground uppercase tracking-wider flex-1">
                AI Document Summary
              </p>
              {profile?.aiModel && (
                <span className="text-[9px] text-muted-foreground truncate" title={profile.aiModel}>
                  {profile.aiModel.split("/").pop()}
                </span>
              )}
            </div>

            {analyseError && (
              <p className="text-[10px] text-destructive leading-snug">{analyseError}</p>
            )}

            {!profile?.aiSummaryEn && !profile?.aiSummaryRo && !profile?.aiSummaryHu && !analyseError && (
              <p className="text-[10px] text-muted-foreground">
                Save annotations, then click "Analyse with AI" to generate multilingual summaries.
              </p>
            )}

            {(profile?.aiSummaryEn || profile?.aiSummaryRo || profile?.aiSummaryHu) && (
              <>
                <div className="flex gap-0.5 bg-muted rounded-md p-0.5">
                  {(["en", "ro", "hu"] as const).map((lang) => (
                    <button
                      key={lang} type="button" onClick={() => setAiTab(lang)}
                      className={`flex-1 rounded px-2 py-0.5 text-[10px] font-semibold transition-colors ${
                        aiTab === lang
                          ? "bg-background text-foreground shadow-sm"
                          : "text-muted-foreground hover:text-foreground"
                      }`}
                    >
                      {lang.toUpperCase()}
                    </button>
                  ))}
                </div>
                <p className="text-[10px] text-foreground leading-relaxed">
                  {aiTab === "en" && (profile.aiSummaryEn ?? <span className="italic text-muted-foreground">Not available</span>)}
                  {aiTab === "ro" && (profile.aiSummaryRo ?? <span className="italic text-muted-foreground">Not available</span>)}
                  {aiTab === "hu" && (profile.aiSummaryHu ?? <span className="italic text-muted-foreground">Not available</span>)}
                </p>
                <div className="flex items-center gap-1.5 flex-wrap">
                  {profile.aiConfidence != null && (
                    <span className={`inline-flex items-center rounded-full px-1.5 py-0.5 text-[9px] font-semibold ${
                      profile.aiConfidence >= 0.8
                        ? "bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-300"
                        : profile.aiConfidence >= 0.5
                        ? "bg-yellow-100 text-yellow-800 dark:bg-yellow-900/30 dark:text-yellow-300"
                        : "bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-300"
                    }`}>
                      {Math.round(profile.aiConfidence * 100)}% confidence
                    </span>
                  )}
                  {profile.aiAnalysedOn && (
                    <span className="text-[9px] text-muted-foreground">
                      {new Date(profile.aiAnalysedOn).toLocaleDateString()}
                    </span>
                  )}
                </div>
              </>
            )}
          </div>
        </div>
      </div>

      {/* â”€â”€ Assignment bar â€” appears when text is selected â”€â”€ */}
      {pendingSelection && (
        <div className="fixed bottom-0 inset-x-0 z-[60] border-t-2 border-primary/40 bg-card/95 backdrop-blur-sm shadow-2xl">
          <div className="max-w-5xl mx-auto px-4 py-3 space-y-2">

            {/* Context preview */}
            <div>
              <p className="text-[10px] font-semibold text-muted-foreground uppercase tracking-wide mb-1.5">
                Selection with context
              </p>
              <p className="text-xs font-mono leading-relaxed rounded-lg bg-muted/60 border border-border px-3 py-2 break-words">
                <span className="text-muted-foreground opacity-70">{pendingContextBefore}</span>
                <mark
                  className="px-0.5 rounded mx-px font-semibold"
                  style={{
                    background:  (activeFieldDef?.color ?? "#3b82f6") + "44",
                    outline:     `1.5px solid ${activeFieldDef?.color ?? "#3b82f6"}`,
                    color:       activeFieldDef?.textColor ?? "inherit",
                  }}
                >
                  {pendingSelection.text}
                </mark>
                <span className="text-muted-foreground opacity-70">{pendingContextAfter}</span>
              </p>
            </div>

            <div className="flex items-center gap-4 flex-wrap">
              {/* Context amount */}
              <label className="flex items-center gap-2 text-xs text-muted-foreground">
                Context:
                <button
                  type="button"
                  onClick={() => setContextChars((c) => Math.max(10, c - 20))}
                  className="h-6 w-6 rounded border border-border text-sm font-bold flex items-center justify-center hover:bg-accent transition-colors"
                >
                  −
                </button>
                <span className="w-10 text-center text-foreground">±{contextChars}</span>
                <button
                  type="button"
                  onClick={() => setContextChars((c) => Math.min(150, c + 20))}
                  className="h-6 w-6 rounded border border-border text-sm font-bold flex items-center justify-center hover:bg-accent transition-colors"
                >
                  +
                </button>
              </label>

              {/* Field selector */}
              <label className="flex items-center gap-2 text-xs text-muted-foreground">
                Assign to:
                <select
                  value={assignField}
                  onChange={(e) => setAssignField(e.target.value)}
                  className="text-xs rounded-md border border-input bg-background px-2 py-1.5 outline-none focus:ring-1 focus:ring-primary font-medium"
                  style={{ borderLeft: `3px solid ${activeFieldDef?.color ?? "#3b82f6"}` }}
                >
                  {ANNOTATABLE_FIELDS.map((f) => (
                    <option key={f.field} value={f.field}>{f.label}</option>
                  ))}
                </select>
              </label>

              <div className="flex items-center gap-2 ml-auto">
                <Button
                  size="sm"
                  onClick={handleAddAnnotation}
                  className="gap-1 text-white"
                  style={{ background: activeFieldDef?.color, borderColor: activeFieldDef?.color }}
                >
                  <Plus className="h-3.5 w-3.5" />
                  Add annotation
                </Button>
                <Button
                  size="sm" variant="ghost"
                  onClick={() => setPendingSelection(null)}
                  className="text-muted-foreground"
                >
                  Cancel
                </Button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
