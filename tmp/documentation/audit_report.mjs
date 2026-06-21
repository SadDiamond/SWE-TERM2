import fs from 'node:fs/promises';
import path from 'node:path';
import JSZip from './tools/node_modules/jszip/lib/index.js';
import mammoth from './tools/node_modules/mammoth/lib/index.js';

const root = path.dirname(new URL(import.meta.url).pathname.replace(/^\/(.:)/, '$1'));
const report = path.resolve(root, '..', '..', 'output', 'documents', 'Gordon_Zhao_Assessment_2_OOP_Report_Revised.docx');
const buffer = await fs.readFile(report);
const zip = await JSZip.loadAsync(buffer);
const documentXml = await zip.file('word/document.xml').async('string');
const stylesXml = await zip.file('word/styles.xml').async('string');
const media = Object.keys(zip.files).filter((name) => name.startsWith('word/media/') && !zip.files[name].dir);
const raw = await mammoth.extractRawText({ buffer });
await fs.writeFile(path.join(root, 'final_report_text.txt'), raw.value, 'utf8');

const required = [
  'Project definition and OOP research', 'Hunt the Wumpus research', 'Data flow diagram',
  'Structure chart', 'Class diagram', 'Data dictionary', 'Quality success criteria',
  'Automated EditMode tests', 'Additional white-box checks', 'Grey-box and subsystem test record', 'Black-box and classmate testing',
  'Evaluation against success criteria', 'Efficiency and optimisation',
  'Reconstructed development journal', 'Presentation plan', 'Bibliography'
];
const missing = required.filter((heading) => !raw.value.includes(heading));
const promptRemnants = ['Delete this comment', '[Game name]', 'Other stuff', 'remove this prompt'].filter((text) => raw.value.includes(text));
const tableCount = (documentXml.match(/<w:tbl>/g) || []).length;
const headingCount = (documentXml.match(/w:val="Heading[123]"/g) || []).length;
const pageBreakCount = (documentXml.match(/w:type="page"/g) || []).length;
const fixedLayoutCount = (documentXml.match(/w:tblLayout w:type="fixed"/g) || []).length;
const hasHeadingStyles = ['Heading1', 'Heading2', 'Heading3'].every((name) => stylesXml.includes(`w:styleId="${name}"`));

console.log(JSON.stringify({
  bytes: buffer.length,
  characters: raw.value.length,
  paragraphsApprox: raw.value.split(/\n+/).filter(Boolean).length,
  tables: tableCount,
  fixedLayoutTables: fixedLayoutCount,
  headings: headingCount,
  explicitPageBreaks: pageBreakCount,
  mediaFiles: media.length,
  headingStylesPresent: hasHeadingStyles,
  missingRequiredSections: missing,
  promptRemnants
}, null, 2));

if (missing.length || promptRemnants.length || media.length < 3 || tableCount < 10 || fixedLayoutCount !== tableCount || !hasHeadingStyles) process.exit(1);
