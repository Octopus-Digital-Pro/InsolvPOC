import * as pdfjsLib from 'pdfjs-dist';

pdfjsLib.GlobalWorkerOptions.workerSrc = new URL(
  'pdfjs-dist/build/pdf.worker.min.mjs',
  import.meta.url
).toString();

const MAX_DIMENSION = 2000;
const JPEG_QUALITY = 0.85;

async function pdfToImages(file: File): Promise<string[]> {
  const arrayBuffer = await file.arrayBuffer();
  const pdf = await pdfjsLib.getDocument({ data: arrayBuffer }).promise;
  const images: string[] = [];

  const pageCount = Math.min(pdf.numPages, 5);

  for (let i = 1; i <= pageCount; i++) {
    const page = await pdf.getPage(i);
    const viewport = page.getViewport({ scale: 1 });

    const scale = Math.min(MAX_DIMENSION / viewport.width, MAX_DIMENSION / viewport.height, 2);
    const scaledViewport = page.getViewport({ scale });

    const canvas = document.createElement('canvas');
    canvas.width = scaledViewport.width;
    canvas.height = scaledViewport.height;

    await page.render({ canvas, viewport: scaledViewport }).promise;

    const dataUrl = canvas.toDataURL('image/jpeg', JPEG_QUALITY);
    images.push(dataUrl);
  }

  return images;
}

async function imageToBase64(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(reader.result as string);
    reader.onerror = () => reject(new Error('Failed to read image file'));
    reader.readAsDataURL(file);
  });
}

export type ProcessedFile = {
  images: string[];
  fileName: string;
};

export async function processFile(file: File): Promise<ProcessedFile> {
  const isPdf = file.type === 'application/pdf' || file.name.toLowerCase().endsWith('.pdf');

  if (isPdf) {
    const images = await pdfToImages(file);
    return { images, fileName: file.name };
  }

  const base64 = await imageToBase64(file);
  return { images: [base64], fileName: file.name };
}

export const ACCEPTED_FILE_TYPES: Record<string, string[]> = {
  'application/pdf': ['.pdf'],
  'image/png': ['.png'],
  'image/jpeg': ['.jpg', '.jpeg'],
  'image/webp': ['.webp'],
};
