# InsolvPOC – Contract Document Extractor

A proof-of-concept React app that extracts key information from public-sector contracts (PDFs and images) using OpenAI GPT-4o Vision, persists cases in Firebase Firestore, and deploys to Netlify.

## Features

- **Drag-and-drop upload** for PDF and image files (PNG, JPG, WEBP)
- **AI-powered extraction** of contract fields (parties, dates, financials, legal clauses)
- **Case management** with editable fields, edit tracking, and sidebar navigation
- **Mock user login** (two predefined users, no password)
- **Cloud persistence** via Firebase Firestore (all users share the same case list)
- **Secure API key** – OpenAI calls run in a Netlify serverless function; the key never reaches the browser

## Tech Stack

- React 19 + TypeScript + Vite
- Tailwind CSS v4
- Firebase Firestore (database)
- OpenAI GPT-4o Vision (via Netlify Function)
- pdfjs-dist (PDF rendering)
- react-dropzone (file upload)

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
3. Enable **Firestore Database** (start in **test mode** for the POC)
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

For local development you need the [Netlify CLI](https://docs.netlify.com/cli/get-started/) so the serverless function is available:

```bash
npm install -g netlify-cli
```

Create a `.env` file (or add to the existing one) for the serverless function:

```
OPENAI_API_KEY=sk-your-openai-key
```

Then start the dev server:

```bash
netlify dev
```

This starts both the Vite dev server and the Netlify Functions runtime. Open the URL shown in the terminal (usually `http://localhost:8888`).

> **Tip:** You can also run `npm run dev` for front-end only development (Firestore will work, but document extraction won't since it needs the serverless function).

---

## Deploying to Netlify

### 1. Connect the repo

1. Go to [app.netlify.com](https://app.netlify.com)
2. Click **Add new site → Import an existing project**
3. Connect the GitHub repo `InsolvPOC`
4. Build settings are auto-detected from `netlify.toml`:
   - **Build command:** `npm run build`
   - **Publish directory:** `dist`
   - **Functions directory:** `netlify/functions`

### 2. Set environment variables

In the Netlify dashboard under **Site configuration → Environment variables**, add:

| Variable | Value |
|---|---|
| `OPENAI_API_KEY` | Your OpenAI API key |
| `VITE_FIREBASE_API_KEY` | Firebase API key |
| `VITE_FIREBASE_AUTH_DOMAIN` | Firebase auth domain |
| `VITE_FIREBASE_PROJECT_ID` | Firebase project ID |
| `VITE_FIREBASE_STORAGE_BUCKET` | Firebase storage bucket |
| `VITE_FIREBASE_MESSAGING_SENDER_ID` | Firebase messaging sender ID |
| `VITE_FIREBASE_APP_ID` | Firebase app ID |

### 3. Deploy

Push to `main` (or trigger a deploy from the Netlify dashboard). The site will build and deploy automatically.

---

## Architecture

```
Browser (React + Vite on Netlify CDN)
  │
  ├── reads/writes cases ──→ Firebase Firestore
  │
  └── POST /.netlify/functions/extract
        │
        └── Netlify Function ──→ OpenAI GPT-4o Vision
                                    │
                                    └── structured JSON ──→ Browser
```

1. **File Upload** – User drops a PDF or image file
2. **File Processing** – PDFs are rendered page-by-page to images via pdfjs-dist
3. **AI Extraction** – Images are sent to the Netlify serverless function, which calls OpenAI GPT-4o Vision
4. **Storage** – Extracted data is saved as a structured case in Firestore
5. **Display** – Cases are shown in an editable sidebar layout with edit tracking
