import fs from 'node:fs/promises';
import path from 'node:path';
import { createCanvas, loadImage } from './tools/node_modules/@napi-rs/canvas/index.js';

const root = path.dirname(new URL(import.meta.url).pathname.replace(/^\/(.:)/, '$1'));
const pagesDir = path.join(root, 'submission_pages');
const files = (await fs.readdir(pagesDir)).filter((name) => name.endsWith('.png')).sort();
const thumbWidth = 340;
const gap = 22;
const columns = 3;
const first = await loadImage(path.join(pagesDir, files[0]));
const scale = thumbWidth / first.width;
const thumbHeight = Math.round(first.height * scale);
const rows = Math.ceil(files.length / columns);
const canvas = createCanvas(columns * thumbWidth + (columns + 1) * gap, rows * (thumbHeight + 34) + (rows + 1) * gap);
const ctx = canvas.getContext('2d');
ctx.fillStyle = '#202327';
ctx.fillRect(0, 0, canvas.width, canvas.height);
ctx.font = '18px Arial';
ctx.textAlign = 'center';
for (let i = 0; i < files.length; i++) {
  const img = await loadImage(path.join(pagesDir, files[i]));
  const col = i % columns;
  const row = Math.floor(i / columns);
  const x = gap + col * (thumbWidth + gap);
  const y = gap + row * (thumbHeight + 34 + gap);
  ctx.drawImage(img, x, y, thumbWidth, thumbHeight);
  ctx.fillStyle = '#f4f4f4';
  ctx.fillText(`Page ${i + 1}`, x + thumbWidth / 2, y + thumbHeight + 24);
}
await fs.writeFile(path.join(root, 'submission_contact_sheet.png'), canvas.toBuffer('image/png'));
