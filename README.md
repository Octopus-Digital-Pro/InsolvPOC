# InsolvPOC - Insolvency Document Extractor

A proof-of-concept React app that extracts key information from insolvency documents (PDFs and images) using OpenAI GPT-4o Vision.

## Features

- **Drag-and-drop upload** for PDF and image files (PNG, JPG, WEBP)
- **AI-powered extraction** of insolvency-specific fields:
  - Company name (debtor)
  - Addressee
  - Dates and deadlines
  - Court information
- **Note-taking interface** with editable fields and sidebar navigation
- **Local persistence** via localStorage (designed for easy migration to Firebase)

## Getting Started

### Prerequisites

- Node.js 18+
- An OpenAI API key with access to GPT-4o

### Installation

```bash
npm install
```

### Configuration

Copy the environment template and add your API key:

```bash
cp .env.example .env
```

Edit `.env` and add your OpenAI API key:

```
VITE_OPENAI_API_KEY=sk-your-actual-api-key
```

### Development

```bash
npm run dev
```

Open [http://localhost:5173](http://localhost:5173) in your browser.

## Tech Stack

- React 18 + TypeScript
- Vite
- Tailwind CSS v4
- OpenAI GPT-4o (Vision API)
- pdfjs-dist (PDF rendering)
- react-dropzone (file upload)

## Architecture

The app processes documents through this pipeline:

1. **File Upload** - User drops a PDF or image file
2. **File Processing** - PDFs are rendered page-by-page to images via pdfjs-dist
3. **AI Extraction** - Images are sent to OpenAI GPT-4o Vision with a specialised insolvency prompt
4. **Storage** - Extracted data is saved as a structured note in localStorage
5. **Display** - Notes are shown in an editable, searchable sidebar layout

> **Note:** For this POC, the OpenAI API key is used client-side. A production deployment should proxy API calls through a backend server.
