/**
 * PdfAnnotatorModal
 *
 * Full-page overlay for annotating a reference PDF with Handlebars field regions.
 * The user selects a field type from the palette, then click-drags on the rendered
 * PDF page to mark the region where that field typically appears.
 *
 * Coordinates are stored as relative values (0–1) so they scale to any PDF render size.
 */
import { useEffect, useRef, useState, useCallback } from "react";
import * as pdfjsLib from "pdfjs-dist";
import {
  documentTemplatesApi,
  type IncomingDocumentType,
  type IncomingAnnotationItem,
  type IncomingAnnotationsPayload,
  type IncomingDocumentProfile,
  INCOMING_DOCUMENT_LABELS,
} from "@/services/api/documentTemplatesApi";
import { Button } from "@/components/ui/button";
import { Loader2, Save, X, Trash2, MousePointer2, Info, CheckCircle2, Brain, Sparkles } from "lucide-react";

pdfjsLib.GlobalWorkerOptions.workerSrc = new URL(
  "pdfjs-dist/build/pdf.worker.min.mjs",
  import.meta.url,
).toString();

// ── Annotatable field catalogue ───────────────────────────────────────────────

export interface AnnotatableField {
  field: string;
  label: string;
  group: string;
  color: string;
  textColor: string;
}

export const ANNOTATABLE_FIELDS: AnnotatableField[] = [
  // Case
  { field: "CaseNumber",             label: "Case Number",            group: "Case",        color: "#f97316", textColor: "#7c2d12" },
  { field: "ProcedureType",          label: "Procedure Type",          group: "Case",        color: "#fb923c", textColor: "#7c2d12" },
  { field: "OpeningDecisionNo",      label: "Opening Decision No.",    group: "Case",        color: "#fdba74", textColor: "#7c2d12" },
  // Debtor
  { field: "DebtorName",             label: "Debtor Name",             group: "Debtor",      color: "#3b82f6", textColor: "#1e3a8a" },
  { field: "DebtorCui",              label: "Debtor CUI / Tax ID",     group: "Debtor",      color: "#60a5fa", textColor: "#1e3a8a" },
  { field: "DebtorAddress",          label: "Debtor Address",          group: "Debtor",      color: "#93c5fd", textColor: "#1e3a8a" },
  // Court
  { field: "CourtName",              label: "Court / Tribunal",        group: "Court",       color: "#8b5cf6", textColor: "#3b0764" },
  { field: "CourtSection",           label: "Court Section",           group: "Court",       color: "#a78bfa", textColor: "#3b0764" },
  { field: "JudgeSyndic",            label: "Judge / Syndic",          group: "Court",       color: "#c4b5fd", textColor: "#3b0764" },
  // Dates
  { field: "OpeningDate",            label: "Opening Date",            group: "Dates",       color: "#22c55e", textColor: "#14532d" },
  { field: "ClaimsDeadline",         label: "Claims Deadline",         group: "Dates",       color: "#4ade80", textColor: "#14532d" },
  { field: "ContestationsDeadline",  label: "Contestations Deadline",  group: "Dates",       color: "#86efac", textColor: "#14532d" },
  { field: "NextHearingDate",        label: "Next Hearing Date",       group: "Dates",       color: "#d9f99d", textColor: "#14532d" },
  // Practitioner
  { field: "PractitionerName",       label: "Practitioner Name",       group: "Practitioner", color: "#06b6d4", textColor: "#164e63" },
  { field: "PractitionerRole",       label: "Practitioner Role",       group: "Practitioner", color: "#67e8f9", textColor: "#164e63" },
];

const fieldByKey = Object.fromEntries(ANNOTATABLE_FIELDS.map((f) => [f.field, f]));
const groupedFields = ANNOTATABLE_FIELDS.reduce<Record<string, AnnotatableField[]>>((acc, f) => {
  (acc[f.group] ??= []).push(f);
  return acc;
}, {});

// ── Example illustration ──────────────────────────────────────────────────────

/** Static SVG showing an example annotated court document page. */
function AnnotationExample() {
  const examples = [
    { field: "CourtName",   x: 6,  y: 5,  w: 38, h: 5,  label: "Court / Tribunal" },
    { field: "CaseNumber",  x: 6,  y: 13, w: 32, h: 4.5, label: "Case Number" },
    { field: "DebtorName",  x: 6,  y: 22, w: 42, h: 4,  label: "Debtor Name" },
    { field: "DebtorCui",   x: 6,  y: 28, w: 30, h: 4,  label: "Debtor CUI" },
    { field: "OpeningDate", x: 6,  y: 37, w: 30, h: 4,  label: "Opening Date" },
    { field: "ClaimsDeadline", x: 6, y: 44, w: 40, h: 4, label: "Claims Deadline" },
    { field: "JudgeSyndic", x: 6,  y: 53, w: 35, h: 4,  label: "Judge / Syndic" },
  ];

  return (
    <div className="flex flex-col items-center gap-4">
      <div className="max-w-md text-center space-y-1">
        <p className="text-sm font-semibold text-foreground">How to annotate</p>
        <p className="text-xs text-muted-foreground">
          Select a field type from the left palette, then click and drag on the PDF to mark where that data typically appears.
          The example below shows what a fully annotated document looks like.
        </p>
      </div>

      {/* Mock document with example annotations */}
      <div className="relative bg-white border border-border shadow-md rounded" style={{ width: 340, height: 480 }}>
        {/* Document lines */}
        <svg viewBox="0 0 340 480" width="340" height="480" className="absolute inset-0">
          {/* Header area */}
          <rect x="0" y="0" width="340" height="480" fill="white" />
          {/* Simulated text lines */}
          {[12, 20, 35, 42, 56, 64, 80, 88, 100, 108, 120, 128, 140, 148, 160, 168, 180, 188, 200, 208].map((y) => (
            <rect key={y} x="20" y={y} width={200 + Math.sin(y) * 60} height="3" fill="#e5e7eb" rx="1" />
          ))}

          {/* Example annotation rectangles */}
          {examples.map((ex) => {
            const f = fieldByKey[ex.field];
            if (!f) return null;
            const x = (ex.x / 100) * 340;
            const y = (ex.y / 100) * 480;
            const w = (ex.w / 100) * 340;
            const h = (ex.h / 100) * 480;
            return (
              <g key={ex.field}>
                <rect x={x} y={y} width={w} height={h} fill={f.color} fillOpacity="0.25" stroke={f.color} strokeWidth="1.5" rx="2" />
                <rect x={x} y={y - 10} width={Math.min(w, ex.label.length * 5.5 + 8)} height="11" fill={f.color} rx="2" />
                <text x={x + 4} y={y - 2} fontSize="7" fill={f.textColor} fontWeight="600">{ex.label}</text>
              </g>
            );
          })}
        </svg>
      </div>

      <div className="flex flex-wrap gap-1.5 justify-center max-w-sm">
        {examples.map((ex) => {
          const f = fieldByKey[ex.field];
          if (!f) return null;
          return (
            <span
              key={ex.field}
              className="flex items-center gap-1 rounded-full px-2 py-0.5 text-[10px] font-medium border"
              style={{ background: f.color + "30", borderColor: f.color, color: f.textColor }}
            >
              <span className="w-2 h-2 rounded-full shrink-0" style={{ background: f.color }} />
              {ex.label}
            </span>
          );
        })}
      </div>
    </div>
  );
}

// ── Main component ────────────────────────────────────────────────────────────

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
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const overlayRef = useRef<SVGSVGElement>(null);
  const containerRef = useRef<HTMLDivElement>(null);

  // PDF state
  const [pdfLoading, setPdfLoading] = useState(true);
  const [pdfError, setPdfError] = useState<string | null>(null);
  const [canvasSize, setCanvasSize] = useState({ w: 0, h: 0 });

  // Annotation state
  const [annotations, setAnnotations] = useState<IncomingAnnotationItem[]>([]);
  const [notes, setNotes] = useState<string>("");
  const [selectedField, setSelectedField] = useState<string>("CaseNumber");
  const [saving, setSaving] = useState(false);
  const [savedOk, setSavedOk] = useState(false);

  // AI state
  const [profile, setProfile] = useState<IncomingDocumentProfile | null>(null);
  const [analysing, setAnalysing] = useState(false);
  const [analyseError, setAnalyseError] = useState<string | null>(null);
  const [aiTab, setAiTab] = useState<"en" | "ro" | "hu">("en");
  const [aiDone, setAiDone] = useState(false);

  // Drawing state (refs to avoid stale closures in event handlers)
  const drawingRef = useRef<{ startX: number; startY: number } | null>(null);
  const [dragRect, setDragRect] = useState<{ x: number; y: number; w: number; h: number } | null>(null);

  // ── Load PDF ────────────────────────────────────────────────────────────────

  useEffect(() => {
    let cancelled = false;

    const renderPdf = async (arrayBuffer: ArrayBuffer) => {
      try {
        const pdf = await pdfjsLib.getDocument({ data: arrayBuffer }).promise;
        const page = await pdf.getPage(1);
        const viewport = page.getViewport({ scale: 1.6 });

        const canvas = canvasRef.current;
        if (!canvas || cancelled) return;
        canvas.width = viewport.width;
        canvas.height = viewport.height;
        setCanvasSize({ w: viewport.width, h: viewport.height });

        await page.render({ canvas, viewport }).promise;
      } catch (e) {
        if (!cancelled) setPdfError("Failed to render PDF. Make sure the file is a valid PDF document.");
      } finally {
        if (!cancelled) setPdfLoading(false);
      }
    };

    const load = async () => {
      setPdfLoading(true);
      setPdfError(null);

      try {
        if (uploadedFile) {
          const ab = await uploadedFile.arrayBuffer();
          if (!cancelled) await renderPdf(ab);
        } else {
          // Fetch from server with auth headers
          const token = localStorage.getItem("authToken");
          const tenantId = localStorage.getItem("selectedTenantId");
          const res = await fetch(documentTemplatesApi.getIncomingReferenceFileUrl(type), {
            headers: {
              ...(token ? { Authorization: `Bearer ${token}` } : {}),
              ...(tenantId ? { "X-Tenant-Id": tenantId } : {}),
            },
          });
          if (!res.ok) throw new Error(`HTTP ${res.status}`);
          const ab = await res.arrayBuffer();
          if (!cancelled) await renderPdf(ab);
        }
      } catch {
        if (!cancelled) {
          setPdfError("Could not load PDF.");
          setPdfLoading(false);
        }
      }
    };

    load();
    return () => { cancelled = true; };
  }, [type, uploadedFile]);

  // ── Load existing annotations ───────────────────────────────────────────────

  useEffect(() => {
    documentTemplatesApi
      .getIncomingAnnotations(type)
      .then((r) => {
        setAnnotations(r.data.annotations ?? []);
        setNotes(r.data.notes ?? "");
      })
      .catch(() => {/* no saved annotations yet */});
  }, [type]);

  // ── Load existing DB profile (AI summaries etc.) ──────────────────────────

  useEffect(() => {
    documentTemplatesApi
      .getIncomingDocumentProfile(type)
      .then((r) => { if (r.data.exists) setProfile(r.data); })
      .catch(() => {});
  }, [type]);

  // ── Drawing (SVG overlay mouse events) ─────────────────────────────────────

  const getSvgCoords = useCallback(
    (e: React.MouseEvent<SVGSVGElement>): { px: number; py: number } | null => {
      const svg = overlayRef.current;
      if (!svg) return null;
      const rect = svg.getBoundingClientRect();
      return {
        px: e.clientX - rect.left,
        py: e.clientY - rect.top,
      };
    },
    [],
  );

  const handleMouseDown = useCallback(
    (e: React.MouseEvent<SVGSVGElement>) => {
      e.preventDefault();
      const coords = getSvgCoords(e);
      if (!coords) return;
      drawingRef.current = { startX: coords.px, startY: coords.py };
    },
    [getSvgCoords],
  );

  const handleMouseMove = useCallback(
    (e: React.MouseEvent<SVGSVGElement>) => {
      if (!drawingRef.current) return;
      const coords = getSvgCoords(e);
      if (!coords || !canvasSize.w) return;
      const { startX, startY } = drawingRef.current;
      const svgEl = overlayRef.current!;
      const svgRect = svgEl.getBoundingClientRect();
      // Convert CSS px → canvas (viewBox) coords
      const scale = canvasSize.w / svgRect.width;
      const x = Math.min(startX, coords.px) * scale;
      const y = Math.min(startY, coords.py) * scale;
      const w = Math.abs(coords.px - startX) * scale;
      const h = Math.abs(coords.py - startY) * scale;
      setDragRect({ x, y, w, h });
    },
    [getSvgCoords, canvasSize],
  );

  const handleMouseUp = useCallback(
    (e: React.MouseEvent<SVGSVGElement>) => {
      if (!drawingRef.current || !canvasSize.w || !canvasSize.h) {
        drawingRef.current = null;
        setDragRect(null);
        return;
      }
      const coords = getSvgCoords(e);
      drawingRef.current = null;

      if (!coords || !dragRect || dragRect.w < 8 || dragRect.h < 8) {
        setDragRect(null);
        return;
      }

      const field = ANNOTATABLE_FIELDS.find((f) => f.field === selectedField);
      if (!field) { setDragRect(null); return; }

      const newAnnotation: IncomingAnnotationItem = {
        id: crypto.randomUUID(),
        field: field.field,
        label: field.label,
        // Store as relative coords (0-1)
        x: dragRect.x / canvasSize.w,
        y: dragRect.y / canvasSize.h,
        w: dragRect.w / canvasSize.w,
        h: dragRect.h / canvasSize.h,
      };

      setAnnotations((prev) => [...prev, newAnnotation]);
      setDragRect(null);
    },
    [getSvgCoords, canvasSize, selectedField, dragRect],
  );

  const handleMouseLeave = useCallback(() => {
    if (drawingRef.current) {
      drawingRef.current = null;
      setDragRect(null);
    }
  }, []);

  // ── Save ────────────────────────────────────────────────────────────────────

  const handleSave = async () => {
    setSaving(true);
    try {
      const payload: IncomingAnnotationsPayload = { annotations, notes: notes || null };
      await documentTemplatesApi.saveIncomingAnnotations(type, payload);
      setSavedOk(true);
      setTimeout(() => setSavedOk(false), 3000);
      onSaved();
      // Clear stale AI summaries so user knows re-analysis is needed
      setProfile((prev) => prev ? { ...prev, aiSummaryEn: null, aiSummaryRo: null, aiSummaryHu: null } : prev);
    } catch {
      // leave saving state
    } finally {
      setSaving(false);
    }
  };

  // ── AI Analysis ─────────────────────────────────────────────────────────────

  const handleAnalyse = async () => {
    setAnalysing(true);
    setAnalyseError(null);
    setAiDone(false);
    try {
      const r = await documentTemplatesApi.analyseIncomingDocument(type);
      setProfile((prev) => ({
        ...(prev ?? { type, exists: true }),
        aiSummaryEn: r.data.aiSummaryEn,
        aiSummaryRo: r.data.aiSummaryRo,
        aiSummaryHu: r.data.aiSummaryHu,
        aiParametersJson: r.data.aiParametersJson,
        aiModel: r.data.aiModel,
        aiConfidence: r.data.aiConfidence,
        aiAnalysedOn: r.data.aiAnalysedOn,
      }));
      setAiDone(true);
      setTimeout(() => setAiDone(false), 4000);
    } catch {
      setAnalyseError("AI analysis failed. Make sure AI is enabled in Settings › AI Config.");
    } finally {
      setAnalysing(false);
    }
  };

  // ── Render annotation SVG rects ─────────────────────────────────────────────
  // Annotations are stored as relative (0-1); rendered in canvas-pixel viewBox coords.

  const renderAnnotationRects = () =>
    annotations.map((a) => {
      const f = fieldByKey[a.field];
      if (!f) return null;
      const ax = a.x * canvasSize.w;
      const ay = a.y * canvasSize.h;
      const aw = a.w * canvasSize.w;
      const ah = a.h * canvasSize.h;
      const labelW = Math.min(aw, a.label.length * 6.5 + 10);
      return (
        <g key={a.id}>
          <rect x={ax} y={ay} width={aw} height={ah}
            fill={f.color} fillOpacity="0.22"
            stroke={f.color} strokeWidth="1.5" rx="3" />
          {/* Label badge above the rect */}
          <rect x={ax} y={ay - 15} width={labelW} height="14" fill={f.color} rx="3" />
          <text x={ax + 4} y={ay - 4} fontSize="9" fill={f.textColor} fontWeight="700">{a.label}</text>
        </g>
      );
    });

  const activeField = ANNOTATABLE_FIELDS.find((f) => f.field === selectedField);

  return (
    <div className="fixed inset-0 z-50 flex flex-col bg-background">
      {/* ── Header ── */}
      <div className="flex items-center gap-3 border-b border-border bg-card px-4 py-3 shrink-0">
        <div className="flex-1 min-w-0">
          <p className="text-sm font-bold leading-tight">Annotate reference document</p>
          <p className="text-xs text-muted-foreground truncate">{INCOMING_DOCUMENT_LABELS[type]}</p>
        </div>
        <div className="flex items-center gap-2">
          <Button
            size="sm"
            variant="outline"
            onClick={handleAnalyse}
            disabled={analysing || saving}
            title={annotations.length === 0 ? "Save annotations first, then run AI analysis" : "Generate AI summaries in EN / RO / HU and extract field parameters"}
          >
            {analysing ? (
              <Loader2 className="h-3.5 w-3.5 animate-spin mr-1" />
            ) : aiDone ? (
              <Sparkles className="h-3.5 w-3.5 mr-1 text-purple-500" />
            ) : (
              <Brain className="h-3.5 w-3.5 mr-1" />
            )}
            {aiDone ? "Analysis done" : analysing ? "Analysing…" : "Analyse with AI"}
          </Button>
          <Button size="sm" onClick={handleSave} disabled={saving || annotations.length === 0}>
            {saving ? (
              <Loader2 className="h-3.5 w-3.5 animate-spin mr-1" />
            ) : savedOk ? (
              <CheckCircle2 className="h-3.5 w-3.5 mr-1 text-green-500" />
            ) : (
              <Save className="h-3.5 w-3.5 mr-1" />
            )}
            {savedOk ? "Saved" : `Save${annotations.length > 0 ? ` (${annotations.length})` : ""}`}
          </Button>
          <button
            type="button"
            onClick={onClose}
            className="rounded-md p-1.5 hover:bg-accent text-muted-foreground"
          >
            <X className="h-4 w-4" />
          </button>
        </div>
      </div>

      {/* ── Instruction bar ── */}
      <div className="flex items-center gap-2 border-b border-border bg-muted/30 px-4 py-1.5 shrink-0">
        <MousePointer2 className="h-3.5 w-3.5 text-muted-foreground shrink-0" />
        <p className="text-xs text-muted-foreground">
          <span className="font-medium">Select a field type</span> from the left, then{" "}
          <span className="font-medium">click and drag</span> on the document to mark the region.
        </p>
        {activeField && (
          <span
            className="ml-auto flex items-center gap-1.5 rounded-full px-2.5 py-0.5 text-xs font-semibold border text-white shrink-0"
            style={{ background: activeField.color, borderColor: activeField.color, color: activeField.textColor }}
          >
            Currently marking: {activeField.label}
          </span>
        )}
      </div>

      {/* ── Main body ── */}
      <div className="flex flex-1 min-h-0 overflow-hidden">
        {/* Field palette */}
        <div className="w-52 shrink-0 border-r border-border overflow-y-auto bg-card">
          <div className="px-3 py-2 border-b border-border">
            <p className="text-xs font-semibold text-muted-foreground uppercase tracking-wide">Field types</p>
          </div>
          {Object.entries(groupedFields).map(([group, fields]) => (
            <div key={group}>
              <p className="px-3 pt-2 pb-0.5 text-[10px] font-bold text-muted-foreground uppercase tracking-wider">{group}</p>
              {fields.map((f) => (
                <button
                  key={f.field}
                  type="button"
                  onClick={() => setSelectedField(f.field)}
                  className={`w-full flex items-center gap-2 px-3 py-1.5 text-xs text-left transition-colors hover:bg-accent ${
                    selectedField === f.field ? "bg-accent font-semibold" : ""
                  }`}
                >
                  <span className="w-2.5 h-2.5 rounded-full shrink-0" style={{ background: f.color }} />
                  {f.label}
                </button>
              ))}
            </div>
          ))}
        </div>

        {/* PDF canvas area */}
        <div className="flex-1 overflow-auto bg-gray-100 dark:bg-gray-900 flex items-start justify-center p-6">
          {pdfLoading && (
            <div className="flex flex-col items-center gap-3 mt-20 text-muted-foreground">
              <Loader2 className="h-6 w-6 animate-spin" />
              <p className="text-sm">Loading PDF…</p>
            </div>
          )}

          {pdfError && (
            <div className="mt-20 max-w-sm text-center space-y-3">
              <p className="text-sm text-destructive">{pdfError}</p>
              <AnnotationExample />
            </div>
          )}

          {!pdfLoading && !pdfError && (
            <div ref={containerRef} className="relative shadow-lg" style={{ display: "inline-block" }}>
              <canvas ref={canvasRef} className="block" />
              {/* SVG annotation overlay — viewBox matches canvas pixel dimensions */}
              <svg
                ref={overlayRef}
                className="absolute inset-0"
                viewBox={canvasSize.w ? `0 0 ${canvasSize.w} ${canvasSize.h}` : undefined}
                preserveAspectRatio="none"
                style={{ width: "100%", height: "100%", cursor: "crosshair", userSelect: "none" }}
                onMouseDown={handleMouseDown}
                onMouseMove={handleMouseMove}
                onMouseUp={handleMouseUp}
                onMouseLeave={handleMouseLeave}
              >
                {/* Saved annotations */}
                {renderAnnotationRects()}
                {/* In-progress drag rectangle (already in canvas-px viewBox coords) */}
                {dragRect && activeField && (
                  <rect
                    x={dragRect.x} y={dragRect.y} width={dragRect.w} height={dragRect.h}
                    fill={activeField.color} fillOpacity="0.20"
                    stroke={activeField.color} strokeWidth="2"
                    strokeDasharray="6 3" rx="3"
                  />
                )}
              </svg>
            </div>
          )}

          {/* Show example below PDF if no annotations yet */}
          {!pdfLoading && !pdfError && annotations.length === 0 && (
            <div className="ml-6 flex-shrink-0 flex flex-col gap-2">
              <div className="flex items-center gap-1.5 text-xs text-muted-foreground rounded-lg bg-blue-50 dark:bg-blue-950/30 border border-blue-200 dark:border-blue-800 px-3 py-2 max-w-xs">
                <Info className="h-3.5 w-3.5 text-blue-500 shrink-0" />
                No annotations yet. See the example on the right for guidance.
              </div>
              <AnnotationExample />
            </div>
          )}
        </div>

        {/* Right sidebar: annotation list + notes */}
        <div className="w-64 shrink-0 border-l border-border flex flex-col bg-card">
          <div className="px-3 py-2 border-b border-border">
            <p className="text-xs font-semibold text-muted-foreground uppercase tracking-wide">
              Annotations{annotations.length > 0 && ` (${annotations.length})`}
            </p>
          </div>

          <div className="flex-1 overflow-y-auto divide-y divide-border">
            {annotations.length === 0 ? (
              <p className="px-3 py-4 text-xs text-muted-foreground text-center">
                Draw rectangles on the PDF to add annotations.
              </p>
            ) : (
              annotations.map((a) => {
                const f = fieldByKey[a.field];
                return (
                  <div key={a.id} className="flex items-center gap-2 px-3 py-2">
                    <span
                      className="w-2 h-2 rounded-full shrink-0"
                      style={{ background: f?.color ?? "#aaa" }}
                    />
                    <span className="flex-1 text-xs truncate">{a.label}</span>
                    <button
                      type="button"
                      onClick={() => setAnnotations((prev) => prev.filter((x) => x.id !== a.id))}
                      className="text-muted-foreground hover:text-destructive transition-colors shrink-0"
                    >
                      <Trash2 className="h-3 w-3" />
                    </button>
                  </div>
                );
              })
            )}
          </div>

          {/* Notes */}
          <div className="border-t border-border p-3 space-y-1">
            <p className="text-[10px] font-semibold text-muted-foreground uppercase tracking-wider">Notes (optional)</p>
            <textarea
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              placeholder="Describe this document type, layout variations, etc."
              rows={3}
              className="w-full rounded-md border border-input bg-background px-2.5 py-1.5 text-xs outline-none resize-none focus:ring-1 focus:ring-primary leading-relaxed"
            />
          </div>

          {/* ── AI Analysis results ── */}
          <div className="border-t border-border p-3 space-y-2">
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
                Save annotations, then click “Analyse with AI” to generate multilingual summaries and field parameters.
              </p>
            )}

            {(profile?.aiSummaryEn || profile?.aiSummaryRo || profile?.aiSummaryHu) && (
              <>
                {/* Language tabs */}
                <div className="flex gap-0.5 bg-muted rounded-md p-0.5">
                  {(["en", "ro", "hu"] as const).map((lang) => (
                    <button
                      key={lang}
                      type="button"
                      onClick={() => setAiTab(lang)}
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

                {/* Summary text */}
                <p className="text-[10px] text-foreground leading-relaxed">
                  {aiTab === "en" && (profile.aiSummaryEn ?? <span className="text-muted-foreground italic">Not available</span>)}
                  {aiTab === "ro" && (profile.aiSummaryRo ?? <span className="text-muted-foreground italic">Not available</span>)}
                  {aiTab === "hu" && (profile.aiSummaryHu ?? <span className="text-muted-foreground italic">Not available</span>)}
                </p>

                {/* Confidence + timestamp */}
                <div className="flex items-center gap-1.5 flex-wrap">
                  {profile.aiConfidence != null && (
                    <span
                      className={`inline-flex items-center gap-1 rounded-full px-1.5 py-0.5 text-[9px] font-semibold ${
                        profile.aiConfidence >= 0.8
                          ? "bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-300"
                          : profile.aiConfidence >= 0.5
                          ? "bg-yellow-100 text-yellow-800 dark:bg-yellow-900/30 dark:text-yellow-300"
                          : "bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-300"
                      }`}
                    >
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

          {/* Show example button if PDF is shown */}
          {!pdfLoading && !pdfError && (
            <div className="border-t border-border p-3">
              <details className="text-xs">
                <summary className="cursor-pointer text-muted-foreground hover:text-foreground select-none">
                  Show annotation example
                </summary>
                <div className="mt-3">
                  <AnnotationExample />
                </div>
              </details>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
