import fs from 'node:fs/promises';
import path from 'node:path';
import { createCanvas } from './tools/node_modules/@napi-rs/canvas/index.js';

const root = path.dirname(new URL(import.meta.url).pathname.replace(/^\/(.:)/, '$1'));
const out = path.join(root, 'diagrams');
await fs.mkdir(out, { recursive: true });

const palette = { bg: '#f7f9fb', ink: '#1f2933', line: '#5b7083', blue: '#2e74b5', pale: '#e8f1f8', teal: '#147d83', green: '#e6f3ee', amber: '#fff2d8' };

function wrap(ctx, text, maxWidth) {
  const words = text.split(/\s+/);
  const lines = [];
  let line = '';
  for (const word of words) {
    const next = line ? `${line} ${word}` : word;
    if (ctx.measureText(next).width > maxWidth && line) { lines.push(line); line = word; }
    else line = next;
  }
  if (line) lines.push(line);
  return lines;
}

function box(ctx, x, y, w, h, title, body = '', fill = palette.pale) {
  ctx.fillStyle = fill; ctx.strokeStyle = palette.blue; ctx.lineWidth = 3;
  ctx.beginPath(); ctx.roundRect(x, y, w, h, 10); ctx.fill(); ctx.stroke();
  ctx.textAlign = 'center'; ctx.fillStyle = palette.ink;
  ctx.font = 'bold 24px Arial'; ctx.fillText(title, x + w / 2, y + 34);
  if (body) {
    ctx.font = '18px Arial';
    const lines = wrap(ctx, body, w - 26);
    lines.slice(0, 4).forEach((line, i) => ctx.fillText(line, x + w / 2, y + 64 + i * 23));
  }
}

function arrow(ctx, x1, y1, x2, y2, label = '') {
  const a = Math.atan2(y2 - y1, x2 - x1);
  ctx.strokeStyle = palette.line; ctx.fillStyle = palette.line; ctx.lineWidth = 3;
  ctx.beginPath(); ctx.moveTo(x1, y1); ctx.lineTo(x2, y2); ctx.stroke();
  ctx.beginPath(); ctx.moveTo(x2, y2); ctx.lineTo(x2 - 14 * Math.cos(a - Math.PI / 6), y2 - 14 * Math.sin(a - Math.PI / 6));
  ctx.lineTo(x2 - 14 * Math.cos(a + Math.PI / 6), y2 - 14 * Math.sin(a + Math.PI / 6)); ctx.closePath(); ctx.fill();
  if (label) { ctx.font = '16px Arial'; ctx.textAlign = 'center'; ctx.fillStyle = palette.ink; ctx.fillText(label, (x1 + x2) / 2, (y1 + y2) / 2 - 8); }
}

async function save(name, width, height, draw) {
  const canvas = createCanvas(width, height); const ctx = canvas.getContext('2d');
  ctx.fillStyle = palette.bg; ctx.fillRect(0, 0, width, height); draw(ctx);
  await fs.writeFile(path.join(out, name), canvas.toBuffer('image/png'));
}

await save('data-flow.png', 1500, 850, (ctx) => {
  box(ctx, 50, 300, 240, 150, 'Player', 'keyboard and mouse input', palette.amber);
  box(ctx, 390, 285, 310, 180, 'PlayerController', 'movement, grapple and interaction');
  box(ctx, 805, 90, 300, 155, 'Gun', 'fire input and weapon state', palette.green);
  box(ctx, 1170, 70, 275, 195, 'Enemy objects', 'damage, state changes and death', palette.green);
  box(ctx, 805, 345, 300, 165, 'Interactables', 'terminal, shop and reward', palette.green);
  box(ctx, 1170, 340, 275, 175, 'ArenaDirector', 'objectives, timer and floor state');
  box(ctx, 805, 625, 300, 145, 'HUD', 'health, floor, speed and prompts', palette.amber);
  arrow(ctx, 290, 375, 390, 375, 'input'); arrow(ctx, 700, 325, 805, 175, 'fire');
  arrow(ctx, 1105, 170, 1170, 170, 'damage'); arrow(ctx, 700, 405, 805, 425, 'interact');
  arrow(ctx, 1105, 425, 1170, 425, 'events'); arrow(ctx, 1170, 470, 1105, 685, 'run data');
  arrow(ctx, 805, 700, 700, 430, 'feedback');
});

await save('structure-chart.png', 1500, 930, (ctx) => {
  box(ctx, 555, 35, 390, 110, 'Term 2 SWE project', 'runtime game');
  const xs = [45, 345, 645, 945, 1245];
  const titles = ['Player system', 'Combat system', 'Arena system', 'Enemy system', 'UI system'];
  const bodies = [
    'movement, camera, grapple, health',
    'gun, projectile, abilities, damage',
    'generation, floor rules, shops, terminals',
    'states, navigation, attacks, scaling',
    'HUD, menus, prompts, transitions'
  ];
  for (let i = 0; i < xs.length; i++) { arrow(ctx, 750, 145, xs[i] + 105, 250); box(ctx, xs[i], 250, 210, 145, titles[i], bodies[i]); }
  const lower = [
    [['PlayerController', 'GrappleHookProjectile']],
    [['Gun', 'Projectile'], ['IDamageable', 'WeaponAbilityObject']],
    [['ArenaDirector', 'ArenaGenerator'], ['RunState', 'PuzzleTerminal']],
    [['BasicEnemyAI', 'NavMeshAgent'], ['IGrappleMassTarget', 'Target']],
    [['RunStatusHUD', 'HintOverlay'], ['StartMenu', 'LoadingScreen']]
  ];
  for (let i = 0; i < xs.length; i++) {
    const cx = xs[i] + 105;
    for (let j = 0; j < lower[i].length; j++) {
      const y = 500 + j * 190; arrow(ctx, cx, j === 0 ? 395 : 625, cx, y);
      box(ctx, xs[i] - 5, y, 220, 125, lower[i][j][0], lower[i][j][1], '#ffffff');
    }
  }
});

await save('class-diagram.png', 1500, 1020, (ctx) => {
  box(ctx, 570, 35, 360, 120, 'MonoBehaviour', 'Unity component base', '#ffffff');
  box(ctx, 70, 245, 300, 140, 'Interactable (abstract)', 'OnInteract() OnFocus()');
  box(ctx, 420, 245, 300, 140, 'PlayerController', 'implements IDamageable');
  box(ctx, 780, 245, 300, 140, 'BasicEnemyAI', 'implements IDamageable and IGrappleMassTarget');
  box(ctx, 1130, 245, 300, 140, 'PostProcessor (abstract)', 'Process(generator)');
  [220, 570, 930, 1280].forEach((x) => arrow(ctx, 750, 155, x, 245));
  box(ctx, 20, 520, 210, 125, 'Terminal', 'solved state and events', palette.green);
  box(ctx, 245, 520, 210, 125, 'ShopStation', 'purchase rules', palette.green);
  box(ctx, 470, 520, 210, 125, 'WeaponReward', 'unlock and equip', palette.green);
  arrow(ctx, 220, 385, 125, 520); arrow(ctx, 220, 385, 350, 520); arrow(ctx, 220, 385, 575, 520);
  box(ctx, 20, 780, 320, 125, 'CybergrindPuzzleTerminal', 'inherits Terminal', palette.amber); arrow(ctx, 125, 645, 180, 780);
  box(ctx, 735, 520, 300, 125, 'IDamageable', 'TakeDamage(amount)', palette.amber);
  box(ctx, 735, 745, 300, 125, 'IGrappleMassTarget', 'mass class and pull method', palette.amber);
  arrow(ctx, 930, 385, 900, 520); arrow(ctx, 570, 385, 780, 520); arrow(ctx, 930, 385, 900, 745);
  box(ctx, 1130, 520, 300, 125, 'PathRepairProcessor', 'repairs reachability', palette.green);
  box(ctx, 1130, 745, 300, 125, 'MicroPopulator', 'places arena detail', palette.green);
  arrow(ctx, 1280, 385, 1280, 520); arrow(ctx, 1280, 385, 1280, 745);
});

console.log(out);
