import fs from 'node:fs/promises';
import path from 'node:path';
import * as pdfjsLib from './tools/node_modules/pdfjs-dist/legacy/build/pdf.mjs';
import { createCanvas } from './tools/node_modules/@napi-rs/canvas/index.js';
import mammoth from './tools/node_modules/mammoth/lib/index.js';

const root = path.dirname(new URL(import.meta.url).pathname.replace(/^\/(.:)/, '$1'));
const pdfPath = path.join(root, 'submission.pdf');
const docxPath = path.join(root, 'task_sheet.docx');
const renderDir = path.join(root, 'submission_pages');
await fs.mkdir(renderDir, { recursive: true });

const pdfData = new Uint8Array(await fs.readFile(pdfPath));
const pdf = await pdfjsLib.getDocument({ data: pdfData, disableWorker: true }).promise;
const pageTexts = [];

for (let pageNumber = 1; pageNumber <= pdf.numPages; pageNumber++) {
  const page = await pdf.getPage(pageNumber);
  const textContent = await page.getTextContent();
  const lines = [];
  let current = [];
  let previousY = null;
  for (const item of textContent.items) {
    const y = Math.round(item.transform[5]);
    if (previousY !== null && Math.abs(y - previousY) > 3) {
      if (current.length) lines.push(current.join(' ').replace(/\s+/g, ' ').trim());
      current = [];
    }
    if (item.str?.trim()) current.push(item.str.trim());
    previousY = y;
  }
  if (current.length) lines.push(current.join(' ').replace(/\s+/g, ' ').trim());
  pageTexts.push(`\n===== PAGE ${pageNumber} =====\n${lines.join('\n')}`);

  const viewport = page.getViewport({ scale: 1.35 });
  const canvas = createCanvas(Math.ceil(viewport.width), Math.ceil(viewport.height));
  const context = canvas.getContext('2d');
  await page.render({ canvasContext: context, viewport }).promise;
  await fs.writeFile(path.join(renderDir, `page-${String(pageNumber).padStart(2, '0')}.png`), canvas.toBuffer('image/png'));
}

await fs.writeFile(path.join(root, 'submission.txt'), pageTexts.join('\n'), 'utf8');
const docx = await mammoth.extractRawText({ path: docxPath });
await fs.writeFile(path.join(root, 'task_sheet.txt'), docx.value, 'utf8');
console.log(JSON.stringify({ pdfPages: pdf.numPages, submissionCharacters: pageTexts.join('\n').length, taskCharacters: docx.value.length }));
