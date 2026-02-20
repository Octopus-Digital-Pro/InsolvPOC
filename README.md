# InsolvPOC – Insolvency Document Extractor

A proof-of-concept React app for Romanian insolvency (Legea 85/2014) workflows. It extracts structured data from insolvency-related documents (PDFs and images) using OpenAI GPT-4o Vision, manages **insolvency cases** (dosar) and **companies**, supports **company tasks** with deadlines and assignees, and persists everything in Firebase Firestore. Deploys to Netlify.

## Features

- **Drag-and-drop upload** for PDF and image files (PNG, JPG, WEBP)
- **AI-powered extraction** of insolvency document fields (case number, court, debtor, document type, parties, deadlines, claims, procedure type/stage, etc.) via a structured JSON schema
- **Insolvency case management** – cases (dosar) with multiple documents; link cases to companies; edit case/document data
- **Company management** – companies with name, CUI/RO, address; assign companies to users; “Assigned to me” vs “Other”; unassigned cases bucket
- **Company tasks** – tasks per company with title, description, labels, deadline, status (open / blocked / done), and assignee; task table on dashboard (my tasks by deadline); create/edit/delete tasks
- **Extraction flow** – upload → extract → review extracted data → attach to company (new or existing case)
- **Suggested company match** – after extraction, the app suggests a company based on debtor/CUI matching
- **Mock user login** – two predefined users (e.g. Insolvency Practitioner, Firm Admin), no password
- **Cloud persistence** – Firebase Firestore for insolvency cases, documents, companies, and tasks (shared across users for the POC)
- **Secure API key** – OpenAI calls run in a Netlify serverless function; the key never reaches the browser

## Tech Stack

- React 19 + TypeScript + Vite 7
- Tailwind CSS v4
- Radix UI (Popover, Slot), react-day-picker, date-fns, lucide-react
- Firebase Firestore (database)
- OpenAI GPT-4o Vision (via Netlify Function)
- pdfjs-dist (PDF rendering)
- react-dropzone (file upload)

## Project Structure (high level)

- `src/App.tsx` – Root layout, login, main app with sidebar and main content
- `src/components/` – UI (Header, LoginScreen, UploadModal, CompaniesSidebar, CompanyDetailView, CaseDetail, TaskTable, TaskFormModal, ExtractionReviewStep, AttachToCompanyStep, etc.)
- `src/hooks/` – useCases, useCompanies, useTasks
- `src/services/` – storage (Firestore), openai (extract API client), fileProcessor (PDF/images), companyMatch (suggest company)
- `src/types/` – User, Company, InsolvencyCase, InsolvencyDocument, CompanyTask, ContractCase, StorageProvider, etc.
- `netlify/functions/extract.ts` – Serverless function that calls OpenAI for extraction (Romanian insolvency schema)

---

## Getting Started

### Prerequisites

- Node.js 18+
- A Firebase project with Firestore enabled
- An OpenAI API key with access to GPT-4o
- (For deployment) A Netlify account

### 1. Install dependencies

```bash
npm install
```

### 2. Configure Firebase

1. Go to [console.firebase.google.com](https://console.firebase.google.com)
2. Create a new project (or use an existing one)
3. Enable **Firestore Database** (test mode is fine for the POC)
4. Go to **Project Settings → General → Your apps → Add a web app**
5. Copy the Firebase config values

Create a `.env` file from the template:

```bash
cp .env.example .env
```

Fill in your Firebase values:

```
VITE_FIREBASE_API_KEY=AIza...
VITE_FIREBASE_AUTH_DOMAIN=your-project.firebaseapp.com
VITE_FIREBASE_PROJECT_ID=your-project
VITE_FIREBASE_STORAGE_BUCKET=your-project.firebasestorage.app
VITE_FIREBASE_MESSAGING_SENDER_ID=123456789
VITE_FIREBASE_APP_ID=1:123456789:web:abc123
```

### 3. Run locally

For full local development (including document extraction), use the [Netlify CLI](https://docs.netlify.com/cli/get-started/) so the serverless function runs:

```bash
npm install -g netlify-cli
```

Set the OpenAI API key for the serverless function. Either add to `.env` (and ensure it is loaded by Netlify dev) or pass it when starting:

```
OPENAI_API_KEY=sk-your-openai-key
```

Then start the dev server:

```bash
netlify dev
```

This runs both the Vite dev server and the Netlify Functions runtime. Open the URL shown in the terminal (usually `http://localhost:8888`).

**Tip:** You can run `npm run dev` for front-end only (Firestore works, but document extraction will fail without the function).

### Scripts

| Command | Description |
|--------|-------------|
| `npm run dev` | Vite dev server only |
| `npm run build` | TypeScript build + Vite build |
| `npm run preview` | Preview production build |
| `npm run lint` | Run ESLint |

---

## Deploying to Netlify

### 1. Connect the repo

1. Go to [app.netlify.com](https://app.netlify.com)
2. **Add new site → Import an existing project**
3. Connect the GitHub repo
4. Build settings (from `netlify.toml`):
   - **Build command:** `npm run build`
   - **Publish directory:** `dist`
   - **Functions directory:** `netlify/functions`

### 2. Environment variables

In **Site configuration → Environment variables**, add:

| Variable | Value |
|----------|--------|
| `OPENAI_API_KEY` | Your OpenAI API key |
| `VITE_FIREBASE_API_KEY` | Firebase API key |
| `VITE_FIREBASE_AUTH_DOMAIN` | Firebase auth domain |
| `VITE_FIREBASE_PROJECT_ID` | Firebase project ID |
| `VITE_FIREBASE_STORAGE_BUCKET` | Firebase storage bucket |
| `VITE_FIREBASE_MESSAGING_SENDER_ID` | Firebase messaging sender ID |
| `VITE_FIREBASE_APP_ID` | Firebase app ID |

### 3. Deploy

Push to your main branch or trigger a deploy from the Netlify dashboard.

---

## Architecture

```
Browser (React + Vite on Netlify CDN)
  │
  ├── reads/writes ──→ Firebase Firestore
  │     (insolvencyCases, insolvencyDocuments, companies, tasks, cases)
  │
  └── POST /.netlify/functions/extract (images)
        │
        └── Netlify Function ──→ OpenAI GPT-4o Vision
                                    │
                                    └── structured JSON (insolvency schema) ──→ Browser
```

1. **Upload** – User drops a PDF or image (e.g. court decision, claims table, report art. 97/167).
2. **Processing** – PDFs are rendered page-by-page to images (pdfjs-dist).
3. **Extraction** – Images are sent to the Netlify function, which calls OpenAI GPT-4o Vision and returns structured insolvency data (document type, case, parties, deadlines, claims, etc.).
4. **Review & attach** – User reviews extraction, then attaches to a company (new or existing insolvency case).
5. **Storage** – Insolvency cases, documents, companies, and tasks are stored in Firestore.
6. **UI** – Dashboard (my tasks, company cards), company detail (cases + tasks), case detail (documents), task table and task modal.
