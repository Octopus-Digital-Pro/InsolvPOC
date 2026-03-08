# Document Analysis ML Training Plan

## Overview

This document outlines the plan to replace the current prompt-only AI extraction pipeline with a fine-tuned model trained on annotated insolvency documents, fed by a continuous global feedback loop. The goal is higher accuracy, lower cost, offline capability for sensitive case data, and a model that improves autonomously from every user correction across the platform.

---

## 1. Current State

The current pipeline (`DocumentAiService`) works as follows:

1. PDF/DOCX is uploaded → text extracted
2. Text is sent to a third-party LLM (OpenAI / tenant-provided key) with a structured extraction prompt
3. The LLM returns JSON: case number, parties, dates, procedure type, court, etc.
4. Low-confidence results trigger a second AI pass focused on courts and deadlines

**Weaknesses:**
- Relies entirely on prompt engineering — no domain fine-tuning
- Each document incurs an API call (cost, latency, data-privacy risk)
- Inconsistent on Romanian-specific legal text patterns
- **No feedback loop:** user corrections are silently discarded — the model never learns from them
- **Siloed per-tenant:** each tenant's annotation work is invisible to the global model
- **No correction tracking:** the system cannot distinguish between AI-suggested values and user-entered values — every save looks identical

---

## 2. Target Capability

Users can navigate to **Settings → Incoming Documents** to:

1. Upload sample documents (PDF, DOCX, DOC)
2. View the AI's pre-filled annotation suggestions for each field
3. Accept, reject, or correct each suggestion inline
4. Every correction is silently logged as a feedback signal
5. Approved annotations are eligible for training

Globally across all tenants:

6. Anonymised correction signals pool into a **global feedback store**
7. A scheduled export job assembles correction pairs into a training dataset
8. The fine-tuned model improves for all tenants from each correction, not just the tenant who made it
9. Tenants with high correction volumes can optionally receive tenant-specific model fine-tuning on top of the global model

The trained model will be used as the **primary extractor**, with the LLM as a fallback for low-confidence cases.

---

## 3. Architecture

```
Upload → Text Extraction → [Fine-Tuned Model] → JSON result + confidence per field
                               ↑ (primary)
                          If confidence < threshold:
                               ↓
                         [LLM fallback] → JSON result
                               ↓
                    User sees pre-filled form (AI-suggested values shown visually)
                               ↓
                    User corrects/accepts → AiCorrectionFeedback row written:
                               { fieldName, aiSuggestedValue, userCorrectedValue,
                                 documentType, tenantId (hashed), correctedAt }
                               ↓
                    Global Feedback DB → Weekly export → Monthly retrain
                               ↑
                    Applies also to:
                    - Annotation modal (reference documents)
                    - Case creation upload flow (real documents)
                    - Document review page (field corrections)
```

### Components

| Component | Technology | Location |
|---|---|---|
| Text extraction | Apache PDFBox (PDF) / OpenXML SDK (DOCX) | `DocumentAiService.ExtractTextAsync` |
| Tokenisation / feature extraction | spaCy (Romanian language model) or ML.NET | New: `Insolvio.ML` project |
| NER / field extraction model | SpanBERT or DistilBERT fine-tuned on Romanian legal corpus | Hosted model endpoint or embedded ONNX |
| Annotation store | `IncomingDocumentProfile` entity + `AnnotationsJson` column (already exists) | `Insolvio.Domain` |
| **Correction feedback store** | **New: `AiCorrectionFeedback` entity + `AiCorrectionFeedbacks` table** | **`Insolvio.Domain` / `Insolvio.Data`** |
| Training data pipeline | Python script + EF Core data export | `tools/training/` |
| Model serving | FastAPI endpoint or ML.NET ONNX runtime | `Insolvio.Integrations` |
| Fine-tuning job | Hugging Face `Trainer` API / Azure ML | External job, triggered from admin UI |

---

## 4. Annotation Schema

Each annotated document will be stored as a JSON document in `IncomingDocumentProfile.AnnotationsJson`.

```json
{
  "documentId": "guid",
  "annotatedAt": "2025-03-01T12:00:00Z",
  "annotatedByUserId": "guid",
  "fields": {
    "caseNumber": { "value": "1234/F/2023", "spanStart": 102, "spanEnd": 115, "confidence": 1.0 },
    "debtorName": { "value": "SC ALFA SRL", "spanStart": 210, "spanEnd": 221, "confidence": 1.0 },
    "debtorCui": { "value": "RO12345678", "spanStart": 224, "spanEnd": 234, "confidence": 1.0 },
    "courtName": { "value": "Tribunalul Constanța", "spanStart": 55, "spanEnd": 75, "confidence": 1.0 },
    "judgeSyndic": { "value": "Ionescu Maria", "spanStart": 310, "spanEnd": 323, "confidence": 1.0 },
    "procedureType": { "value": "GeneralInsolvency", "spanStart": null, "spanEnd": null, "confidence": 1.0 },
    "openingDate": { "value": "2023-09-15", "spanStart": 88, "spanEnd": 98, "confidence": 1.0 },
    "claimsDeadline": { "value": "2023-10-15", "spanStart": 400, "spanEnd": 410, "confidence": 1.0 },
    "contestationsDeadline": { "value": "2023-10-30", "spanStart": 412, "spanEnd": 422, "confidence": 1.0 },
    "nextHearingDate": { "value": "2023-11-05", "spanStart": 440, "spanEnd": 450, "confidence": 1.0 }
  },
  "documentText": "full extracted text stored here for span alignment",
  "reviewStatus": "approved"
}
```

**Fields to annotate:**
- `caseNumber` — unique case identifier (e.g. `1234/F/2023`)
- `debtorName` — company name of the debtor
- `debtorCui` — Romanian fiscal identifier (CUI/CIF)
- `courtName` — full court name
- `courtSection` — court section number/name
- `judgeSyndic` — name of judge-syndic
- `registrar` — court registrar name
- `procedureType` — enum: `GeneralInsolvency | SimplifiedBankruptcy | Reorganization | ...`
- `openingDate` — date of insolvency opening
- `claimsDeadline` — deadline for claims submission
- `contestationsDeadline` — deadline for contesting the claims table
- `nextHearingDate` — first court hearing date
- `parties` — array: `{ role, name, fiscalId }` for each party

---

## 5. Feedback Loop — Code Requirements

This is the core mechanism by which the system learns from every user interaction. It requires new code at every layer of the stack.

### 5.1 New DB Entity: `AiCorrectionFeedback`

```csharp
// Insolvio.Domain/Entities/AiCorrectionFeedback.cs
public class AiCorrectionFeedback
{
    public Guid   Id                  { get; set; }
    public string DocumentType        { get; set; } = "";  // e.g. "CourtOpeningDecision"
    public string FieldName           { get; set; } = "";  // e.g. "CaseNumber"
    public string AiSuggestedValue    { get; set; } = "";  // What the model returned
    public string UserCorrectedValue  { get; set; } = "";  // What the user actually saved
    public bool   WasAccepted         { get; set; }        // true = user kept AI value unchanged
    public float? AiConfidence        { get; set; }        // confidence the model reported
    public string DocumentTextSnippet { get; set; } = "";  // ~200 chars around the field span (no PII beyond the field itself)
    public string TenantIdHash        { get; set; } = "";  // SHA256 of tenantId — links corrections per tenant without exposing identity
    public DateTime CorrectedAt       { get; set; }
    public string Source              { get; set; } = "";  // "annotation_modal" | "case_creation" | "document_review"
}
```

**Migration required**: `dotnet ef migrations add AddAiCorrectionFeedback`

### 5.2 Where Corrections Must Be Captured (Code Changes)

The feedback loop only works if corrections are captured at every point where AI suggestions become user-confirmed values. There are three distinct touch points in the current codebase:

#### Touch point A — PdfAnnotatorModal (reference document annotation)

**File:** `Insolvio.Web/src/components/PdfAnnotatorModal.tsx`

Current: AI calls `POST /api/document-templates/incoming-reference/{type}/suggest-annotations` → pre-fills annotation list. User edits and saves.

**Required change:** When `handleSave` is called, diff the initial AI-suggested annotations against the final saved set and POST each changed/added/removed field as a correction record to `POST /api/ai-feedback/corrections`.

```typescript
// On save, compute diff between aiSuggestions (state at load time) and annotations (state at save)
const diffs = computeAnnotationDiff(aiSuggestions, annotations);
await aiApi.postCorrectionFeedback(diffs, type, "annotation_modal");
```

#### Touch point B — Case creation upload flow

**File:** `Insolvio.Core/Services/CaseCreationService.cs`

Current: `DocumentAiService.AnalyzeTextAsync()` returns structured fields → directly written to `Case.*` properties. User later edits the case fields via `PUT /api/cases/{id}`.

**Required change:** 
1. Persist the AI-suggested values alongside the case at creation time in a new `AiExtractionSnapshot` column (JSON) or a separate table.
2. When `PUT /api/cases/{id}` is called, compare incoming values against the stored AI snapshot → write `AiCorrectionFeedback` rows for any field that changed.

New optional column on `Case`:
```csharp
public string? AiExtractionSnapshotJson { get; set; } // JSON of what AI originally extracted
```

#### Touch point C — Document review page

**File:** `Insolvio.Web/src/pages/DocumentReviewPage.tsx` + corresponding API controller

Current: Users correct AI-extracted fields on the review page. Changes are saved but no comparison is made.

**Required change:** The review page already receives the full AI extraction result. When fields are submitted, POST the diff to the feedback endpoint.

### 5.3 New API Endpoint

```
POST /api/ai-feedback/corrections
```

```csharp
// Insolvio.API/Controllers/AiFeedbackController.cs
[HttpPost("corrections")]
[RequirePermission(Permission.CaseEdit)]
public async Task<IActionResult> PostCorrections(
    [FromBody] IReadOnlyList<AiCorrectionFeedbackDto> corrections, CancellationToken ct)
```

Each `AiCorrectionFeedbackDto`:
```csharp
public record AiCorrectionFeedbackDto(
    string DocumentType,
    string FieldName,
    string AiSuggestedValue,
    string UserCorrectedValue,
    bool WasAccepted,
    float? AiConfidence,
    string? DocumentTextSnippet,   // optional — only sent from annotation modal
    string Source);
```

The controller:
- Hashes `tenantId` before writing (no raw tenant identity in the feedback store)
- Discards `DocumentTextSnippet` for cases where the snippet itself would contain PII (configurable)
- Writes in bulk — one HTTP call per save action, never per keystroke

### 5.4 Global Feedback Store vs Tenant-Scoped Annotations

| | Tenant annotation store | Global feedback store |
|---|---|---|
| **Table** | `IncomingDocumentProfiles` | `AiCorrectionFeedbacks` |
| **Scope** | Per-tenant reference documents | All tenants, hashed |
| **Contains PII?** | Yes (raw document text + spans) | No (only field value pairs + context snippet) |
| **Used for** | Tenant-specific annotation UI | Global model retraining |
| **Exported for training?** | Yes, with consent gate | Yes, always (anonymised) |
| **Retention** | Forever (user-owned) | Configurable (default: 2 years) |

This separation means: **retraining improves the global model** from anonymised correction signals, while **tenant document text never crosses tenant boundaries**.

### 5.5 Training Dataset Assembly (from Feedback)

The weekly export script reads `AiCorrectionFeedbacks` and constructs contrastive training pairs:

```python
# tools/training/export_feedback.py
# For each correction where WasAccepted=False:
#   Input:  DocumentTextSnippet + field context
#   Label:  UserCorrectedValue  (ground truth)
#   Noise:  AiSuggestedValue    (what was wrong)
#
# For each correction where WasAccepted=True:
#   Input:  DocumentTextSnippet
#   Label:  UserCorrectedValue  (confirmed correct)
#
# These form the basis for:
#   - NER span correction fine-tuning
#   - Prompt-level few-shot examples for the LLM fallback
```

Acceptance rate per field is also tracked. If a field has acceptance rate > 95% over 1000 samples, it no longer needs user review — the system can auto-approve it.

---

## 6. Training Data Pipeline

### 6.1 Data Collection Phase (Weeks 1–4)

**Goal:** Accumulate 300–500 annotated insolvency documents across all tenants.

Steps:
1. Enable annotation UI in Settings → Incoming Documents
2. Each uploaded document is auto-processed by the current LLM extractor
3. User reviews and corrects extracted fields inline
4. Every field save writes an `AiCorrectionFeedback` row (anonymised, global)
5. On "Submit Annotation", the corrected JSON is saved to `IncomingDocumentProfile.AnnotationsJson`
6. Documents with `reviewStatus = "approved"` are eligible for tenant-scoped training; all correction signals are eligible for global training immediately

**Data quality checks:**
- Require all mandatory fields (`caseNumber`, `debtorName`, `openingDate`) to be non-empty before approval
- Flag documents where LLM and annotation disagree by > 2 fields (potential noise)
- Maintain 80/10/10 train/validation/test split

### 6.2 Feature Extraction

1. Extract raw text from each document (`DocumentAiService.ExtractTextFromFilesAsync`)
2. Run tokenisation using the [Romanian spaCy model](https://spacy.io/models/ro) (`ro_core_news_lg`)
3. Map character-level spans in annotations to token-level spans for NER training
4. For classification tasks (e.g. `procedureType`): use CLS token embedding

**Training format (NER — IOB2 tagging):**

```
Tribunalul  B-COURT
Constanța   I-COURT
,           O
Secția      B-COURT_SECTION
a           I-COURT_SECTION
IX-a        I-COURT_SECTION
```

### 6.3 Model Selection

| Task | Recommended model | Rationale |
|---|---|---|
| Named Entity Recognition (NER) | `dumitrescustefan/bert-base-romanian-cased-v1` | Romanian BERT, strong NER baseline |
| Date extraction | Rule-based regex + NER confirmation | Romanian date patterns are highly structured |
| Procedure classification | Fine-tuned BERT classifier | ~12 classes, high accuracy expected |
| Span extraction | SpanBERT or XLM-RoBERTa | Better span prediction for long documents |

Start with `bert-base-romanian-cased-v1` for all sequence-labelling tasks before investing in larger models.

### 6.4 Fine-Tuning (Two Tracks)

**Track 1 — Global model** (trained on anonymised `AiCorrectionFeedbacks` from all tenants)
- Input: `(DocumentTextSnippet, FieldName, AiSuggestedValue, UserCorrectedValue)` pairs
- Used to improve the base model for all tenants
- Scheduled monthly

**Track 2 — Tenant-specific fine-tune** (top-up on tenant's own approved annotations, optional)
- Only available for tenants with ≥ 100 approved annotations
- Produces a tenant-specific adapter layer over the global model
- Must run within the tenant's data boundary

**Framework:** Hugging Face `transformers` + `datasets`

```python
# tools/training/train_ner.py
from transformers import AutoTokenizer, AutoModelForTokenClassification, TrainingArguments, Trainer
from datasets import Dataset

# Load annotated data exported from DB
# Train NER model
# Evaluate on validation set
# Export to ONNX for embedding in .NET
```

**Hyperparameters (starting point):**
- Learning rate: `2e-5`
- Batch size: `16`
- Epochs: `5`
- Warmup ratio: `0.1`
- Weight decay: `0.01`
- Evaluation strategy: `epoch`

**Compute:**
- Local GPU (RTX 3090 / A100) or Azure ML `Standard_NC6s_v3`
- Expected training time for 500 documents × 5 epochs: ~30–60 minutes on a single GPU

### 6.5 ONNX Export and .NET Integration

After training, export the model to ONNX and serve it via ML.NET:

```csharp
// Insolvio.ML/Services/OnnxDocumentExtractorService.cs
public class OnnxDocumentExtractorService : IDocumentExtractorService
{
    // Load ONNX runtime session
    // Tokenise input text using HuggingFaceBPETokenizer
    // Run inference → IOB2 tags → extract field spans
    // Return AiDocumentTextResult
}
```

Alternatively, wrap the Python model in a FastAPI server and call it from `Insolvio.Integrations`.

---

## 7. Active Learning Loop

After the initial model is deployed:

1. Every document processed in production is scored with a **confidence** value per field
2. Documents where any field confidence < 0.75 are flagged for human review
3. Corrected documents flow back into the annotation store
4. A weekly scheduled job (`TrainingDataExportJob`) exports new approved annotations
5. A monthly fine-tuning run updates the model with fresh data
6. New model version is A/B tested (10% traffic) before promotion

```
Production document
      ↓
  [Model inference]
      ↓
 confidence < 0.75?
     /        \
   Yes         No → use result
    ↓
 Queue for review
    ↓
 User corrects → approved annotation
    ↓
 Weekly export → monthly retrain
    ↓
 New model version → A/B test → promote
```

---

## 8. Settings UI: Incoming Documents

### Page: Settings → Incoming Documents

**Upload section:**
- Drop zone: accepts `.pdf`, `.docx`, `.doc`
- On upload: calls `POST /api/training/documents` → runs current extractor → returns `IncomingDocumentProfile` with pre-filled JSON

**Annotation panel (per document):**
- Left: rendered document text (or PDF viewer)
- Right: field-by-field form pre-populated with AI extraction
- Each field has:
  - Current AI value (editable)
  - Confidence indicator (green/amber/red)
  - Optional: text highlight in document to show the span
- Bottom: "Approve and Submit" button → sets `reviewStatus = "approved"`

**Training status section:**
- Count of approved documents
- "Start Training Run" button (requires ≥ 50 approved documents)
- Training job progress (polling `/api/training/status`)
- Model version history with accuracy metrics

### API Endpoints (new)

| Method | Route | Purpose |
|---|---|---|
| `GET` | `/api/training/documents` | List all uploaded training documents |
| `POST` | `/api/training/documents` | Upload new document for annotation |
| `PUT` | `/api/training/documents/{id}/annotations` | Save field annotations |
| `POST` | `/api/training/documents/{id}/approve` | Mark document as approved for training |
| `POST` | `/api/training/run` | Trigger a fine-tuning job |
| `GET` | `/api/training/status` | Get current training job status |
| `GET` | `/api/training/models` | List trained model versions and metrics |
| `POST` | `/api/training/models/{version}/activate` | Promote a model version to production |

---

## 9. Implementation Phases

### Phase 1 — Annotation UI (Weeks 1–4)
- [ ] Add annotation form to Settings → Incoming Documents
- [ ] Wire `PUT /api/training/documents/{id}/annotations` endpoint
- [ ] Extend `IncomingDocumentProfile` entity if needed
- [ ] Display per-field confidence from existing LLM extractor

### Phase 2 — Data Accumulation (Weeks 4–12)
- [ ] Collect 300+ approved annotations from real case uploads
- [ ] Run data quality checks, remove noise
- [ ] Export training dataset: `tools/training/export_dataset.py`

### Phase 3 — Model Training (Week 12–14)
- [ ] Set up training environment (Hugging Face + GPU compute)
- [ ] Train NER model on Romanian insolvency corpus
- [ ] Evaluate: target F1 ≥ 0.88 on test set for all core fields
- [ ] Export to ONNX

### Phase 4 — Integration (Weeks 14–16)
- [ ] Integrate ONNX model or FastAPI service into `DocumentAiService`
- [ ] Implement confidence-gated fallback to LLM
- [ ] A/B testing: 10% traffic to new model, compare extraction accuracy
- [ ] Promote new model if F1 improves and error rate stays low

### Phase 5 — Active Learning (Week 16+)
- [ ] Automate annotation queue for low-confidence documents
- [ ] Schedule weekly export and monthly retraining jobs
- [ ] Monitor model drift (test set accuracy, user correction rate)

---

## 10. Accuracy Targets

| Field | Target F1 | Notes |
|---|---|---|
| `caseNumber` | ≥ 0.97 | Highly structured pattern |
| `debtorName` | ≥ 0.92 | Varies by document style |
| `debtorCui` | ≥ 0.98 | Strict format (RO + digits) |
| `courtName` | ≥ 0.95 | Fixed vocabulary |
| `openingDate` | ≥ 0.95 | Romanian date formats |
| `claimsDeadline` | ≥ 0.88 | Less explicit in some documents |
| `procedureType` | ≥ 0.90 | Classification task |

---

## 11. Privacy and Security Considerations

- All training documents contain sensitive case data — they must **never leave the tenant's data boundary** unless explicitly consented
- Recommended: self-hosted training environment (on-premise GPU or private Azure ML workspace)
- If using cloud training: anonymise party names and fiscal IDs before export
- Model weights do not contain raw document data — only learned feature representations
- Access to the training dataset export is gated behind `Permission.GlobalAdmin`

---

*Last updated: March 2025*
