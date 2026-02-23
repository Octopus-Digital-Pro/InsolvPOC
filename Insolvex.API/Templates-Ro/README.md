# Mail-merge document templates (Romanian insolvency)

Place your **.doc** / **.docx** or **.pdf** template files here. The backend uses this folder when generating documents for a case (see `MailMergeService`).

## Expected filenames

The API expects these exact filenames (see `MailMergeService.TemplateFileNames`):

| Template type | Filename |
|---------------|----------|
| Court opening decision | `0.sentinta Aderom Mio.pdf` |
| Creditor notification + BPI | `1.Notificare creditori deschidere procedura_BPI.doc` |
| Report Art. 97 (40 days) | `2.Raport 40 zile_AM.doc` |
| Preliminary claims table | `3.Tabel prel.doc` |
| Creditors meeting minutes | `4.proces verbal AGC confirmare lichidator.doc` |
| Definitive claims table | `5.Tabel DEFINITIV.doc` |
| Final report Art. 167 | `7.Raport final_AM.doc` |

## Mail-merge placeholders (future)

Currently the service **copies templates as-is** to storage. To add real mail-merge (e.g. replace `{{CaseNumber}}`, `{{DebtorName}}` with case data), extend:

- **Service:** `Insolvex.API/Services/MailMergeService.cs` — in `GenerateAsync` / `CopyTemplateToStorage`, add a step that opens the document, replaces placeholders from case/firm/company data, then saves.
- **Schema:** `DocumentTemplate.MergeFieldsJson` (in DB) can describe required merge fields for validation and UI.

## Alternative: upload via Settings

Templates can also be uploaded per tenant via **Settings → Document templates** (API: `POST /api/settings/templates/upload`). That uses the database and file storage instead of this folder; tenant-specific templates override the files here.
