import fs from 'node:fs/promises';
import path from 'node:path';
import {
  AlignmentType, BorderStyle, Document, ExternalHyperlink, Footer, Header, HeadingLevel,
  ImageRun, LevelFormat, PageBreak, PageNumber, Packer, Paragraph, ShadingType, Table,
  TableCell, TableLayoutType, TableRow, TextRun, VerticalAlign, WidthType
} from './tools/node_modules/docx/dist/index.mjs';

const root = path.dirname(new URL(import.meta.url).pathname.replace(/^\/(.:)/, '$1'));
const outPath = path.resolve(root, '..', '..', 'output', 'documents', 'Gordon_Zhao_Assessment_2_OOP_Report_Revised.docx');
await fs.mkdir(path.dirname(outPath), { recursive: true });

const BLUE = '2E74B5';
const DARK = '1F2933';
const MID = '526575';
const PALE = 'E8F1F8';
const GREEN = 'E6F3EE';
const AMBER = 'FFF2D8';
const WHITE = 'FFFFFF';
const usable = 9360;

const run = (text, options = {}) => new TextRun({ text, font: 'Calibri', size: options.size ?? 22, color: options.color ?? DARK, bold: options.bold, italics: options.italics, break: options.break });
const para = (text = '', options = {}) => new Paragraph({
  children: Array.isArray(text) ? text : [run(text, options)],
  alignment: options.align,
  spacing: { before: options.before ?? 0, after: options.after ?? 120, line: options.line ?? 264 },
  keepNext: options.keepNext,
  pageBreakBefore: options.pageBreakBefore,
  style: options.style
});
const heading = (text, level = 1, pageBreakBefore = false) => new Paragraph({
  text,
  heading: level === 1 ? HeadingLevel.HEADING_1 : level === 2 ? HeadingLevel.HEADING_2 : HeadingLevel.HEADING_3,
  pageBreakBefore,
  keepNext: true
});
const pageBreak = () => new Paragraph({ children: [new PageBreak()] });
const bullet = (text, level = 0) => new Paragraph({ children: [run(text)], numbering: { reference: 'bullet-list', level }, spacing: { after: 80, line: 264 } });
const numberItem = (text, level = 0) => new Paragraph({ children: [run(text)], numbering: { reference: 'number-list', level }, spacing: { after: 80, line: 264 } });

function cell(text, width, options = {}) {
  const contents = Array.isArray(text) ? text : [para(text, { after: 0, line: 240 })];
  return new TableCell({
    children: contents,
    width: { size: width, type: WidthType.DXA },
    verticalAlign: VerticalAlign.CENTER,
    shading: options.fill ? { type: ShadingType.CLEAR, fill: options.fill } : undefined,
    margins: { top: 100, bottom: 100, left: 120, right: 120 }
  });
}

function table(headers, rows, widths) {
  const borders = { style: BorderStyle.SINGLE, size: 4, color: 'AEBBC6' };
  const makeRow = (values, header = false) => new TableRow({
    tableHeader: header,
    children: values.map((value, i) => cell(
      [para([run(String(value), { bold: header, color: header ? WHITE : DARK, size: header ? 20 : 19 })], { after: 0, line: 230 })],
      widths[i],
      { fill: header ? BLUE : (rows.indexOf(values) % 2 === 1 ? 'F7F9FB' : WHITE) }
    ))
  });
  return new Table({
    rows: [makeRow(headers, true), ...rows.map((row) => makeRow(row))],
    width: { size: widths.reduce((a, b) => a + b, 0), type: WidthType.DXA },
    columnWidths: widths,
    layout: TableLayoutType.FIXED,
    borders: { top: borders, bottom: borders, left: borders, right: borders, insideHorizontal: borders, insideVertical: borders },
    margins: { top: 80, bottom: 80, left: 120, right: 120 }
  });
}

function callout(label, text, fill = PALE) {
  return new Table({
    rows: [new TableRow({ children: [cell([
      para([run(label.toUpperCase(), { bold: true, color: BLUE, size: 19 })], { after: 50 }),
      para(text, { after: 0 })
    ], usable, { fill })] })],
    width: { size: usable, type: WidthType.DXA }, columnWidths: [usable], layout: TableLayoutType.FIXED,
    borders: { top: { style: BorderStyle.SINGLE, size: 8, color: BLUE }, bottom: { style: BorderStyle.SINGLE, size: 4, color: 'C7D5E0' }, left: { style: BorderStyle.SINGLE, size: 8, color: BLUE }, right: { style: BorderStyle.SINGLE, size: 4, color: 'C7D5E0' } }
  });
}

function figure(filename, caption, width = 620, height = 370) {
  const data = fs.readFile(path.join(root, 'diagrams', filename));
  return data.then((buffer) => [
    new Paragraph({ children: [new ImageRun({ data: buffer, transformation: { width, height }, type: 'png' })], alignment: AlignmentType.CENTER, spacing: { before: 100, after: 60 } }),
    para(caption, { align: AlignmentType.CENTER, italics: true, color: MID, size: 18, after: 140 })
  ]);
}

function evidenceFigure(filename, caption, width, height) {
  const data = fs.readFile(path.join(root, 'evidence', filename));
  return data.then((buffer) => [
    new Paragraph({ children: [new ImageRun({ data: buffer, transformation: { width, height }, type: 'png' })], alignment: AlignmentType.CENTER, spacing: { before: 120, after: 60 } }),
    para(caption, { align: AlignmentType.CENTER, italics: true, color: MID, size: 18, after: 160 })
  ]);
}

function sourceItem(number, title, url, detail) {
  return new Paragraph({
    children: [run(`[${number}] `, { bold: true }), run(`${title}. `), new ExternalHyperlink({ children: [run(url, { color: BLUE })], link: url }), run(` ${detail}`, { color: MID })],
    spacing: { after: 120, line: 264 }
  });
}

const content = [];

// Cover - editorial cover pattern with restrained technical styling.
content.push(
  para('FORT STREET HIGH SCHOOL', { align: AlignmentType.CENTER, bold: true, color: MID, size: 20, after: 900 }),
  para('SOFTWARE ENGINEERING - ASSESSMENT 2', { align: AlignmentType.CENTER, bold: true, color: BLUE, size: 22, after: 260 }),
  para('Term 2 SWE project', { align: AlignmentType.CENTER, bold: true, color: DARK, size: 52, after: 180, line: 560 }),
  para('Object-Oriented Programming Project Report', { align: AlignmentType.CENTER, color: MID, size: 30, after: 900 }),
  callout('Project in one sentence', 'A fast first-person arena roguelite where movement, combat, procedural generation and progression are separated into interacting C# objects.', PALE),
  para('', { after: 700 }),
  table(['Student', 'Course', 'Due date'], [['Gordon Zhao', 'Year 11 Software Engineering', '19 June 2026']], [2800, 3560, 3000]),
  para('Built with Unity 6000.4.3f1 and C#', { align: AlignmentType.CENTER, color: MID, size: 19, before: 500 })
);

content.push(pageBreak(), heading('Contents', 1));
[
  ['1', 'Project definition and OOP research', 'Identifying and defining - 20 marks'],
  ['2', 'Object and environment design', 'Characters, objects, attributes and methods'],
  ['3', 'Planning and system diagrams', 'Research and planning - 20 marks'],
  ['4', 'Production and implementation', 'Producing and implementing - 20 marks'],
  ['5', 'Testing and evaluation', 'Testing and evaluating - 20 marks'],
  ['6', 'Development journal', 'Project journal - 20 marks'],
  ['7', 'Presentation plan', 'Presentation preparation'],
  ['8', 'Conclusion', 'Final evaluation and next steps'],
  ['9', 'Bibliography', 'Research and project evidence']
].forEach(([n, title, detail]) => content.push(para([run(`${n}. ${title}`, { bold: true }), run(` - ${detail}`, { color: MID })], { after: 160 })));
content.push(callout('Scope note', 'This was completed as an individual Unity/C# project rather than the guided Python Wumpus game. The report still addresses the same OOP, planning, production, testing and documentation criteria.'));

content.push(pageBreak(), heading('1. Project definition and OOP research', 1), heading('1.1 What I made', 2));
content.push(para('Term 2 SWE project is a fast first-person arena roguelite. The player enters a generated floor, completes terminals, clears the enemies, then moves to a shop or boss floor. A run keeps going while enemy health and enemy count increase. The run only ends when the player dies.'));
content.push(para('The main point of the game is movement. The player can dash, slide jump, wall run, slam jump and grapple. These systems carry momentum into each other, so the arena generation has to support high speed instead of only normal walking. Combat uses four weapons with separate primary fire and abilities. Shops allow one purchase per floor, which makes the player choose between healing, movement, a weapon or a weapon upgrade.'));
content.push(heading('1.2 Why OOP suits a game', 2));
content.push(para('A game has many things that exist at the same time and keep their own state. For example, every enemy needs health, a position, an attack state and a target. OOP lets each enemy be an object instead of storing everything in unrelated global variables. Unity also works around components, so classes such as PlayerController, Gun and BasicEnemyAI can be attached to GameObjects and updated independently.'));
content.push(bullet('Encapsulation: each class keeps the data and methods for its own job. Gun owns weapon cooldowns; RunState owns run progression.'));
content.push(bullet('Inheritance: Terminal, CybergrindShopStation and CybergrindWeaponReward reuse the Interactable contract. Generation passes reuse PostProcessor.'));
content.push(bullet('Polymorphism: PlayerController, BasicEnemyAI and Target all implement IDamageable, so combat code calls TakeDamage without needing separate logic for every target type.'));
content.push(bullet('Abstraction: IGrappleMassTarget exposes only the mass class and pull behaviour needed by the grapple system.'));
content.push(bullet('Modularity: movement, combat, AI, generation, UI and run progression can be changed without putting the entire game in one script.'));
content.push(para('The current repository contains 67 C# scripts, about 30,897 lines, 65 classes and two gameplay interfaces. This is large enough that a procedural approach based on one main loop would become difficult to debug. The class structure makes the system manageable.'));

content.push(pageBreak(), heading('1.3 Hunt the Wumpus research', 2));
content.push(para('Hunt the Wumpus was created by Gregory Yob and published in BASIC Computer Games. The original program represents the cave as numbered rooms connected in a fixed graph. Variables and numbered arrays store the player, hazards and Wumpus state, while GOTO, GOSUB and condition checks control the game [1].'));
content.push(para('The useful idea I took from Wumpus was not the theme. It was the structure of moving through connected spaces while hazards and enemies change the risk. My project translates this into generated 3D floors. The arena generator creates the space, the director tracks the current objective, and enemy objects act inside that space.'));
content.push(heading('1.4 Procedural compared with OOP', 2));
content.push(table(
  ['Area', 'Procedural version', 'OOP version in this project'],
  [
    ['Enemy state', 'Arrays or global variables store health and position.', 'Each BasicEnemyAI object stores its own health, state and movement data.'],
    ['Taking damage', 'The main program checks the target type and edits the correct variable.', 'Gun finds IDamageable and calls TakeDamage(amount).'],
    ['Interaction', 'One long input branch checks every possible item.', 'PlayerController focuses an Interactable and calls OnInteract(player).'],
    ['Floor progression', 'Global flags control whether the next room can open.', 'CybergrindArenaDirector reads terminals and enemy objects, then changes floor state.'],
    ['New content', 'More conditions are added to the main procedure.', 'A new subclass or component can implement the existing contract.']
  ], [1600, 3650, 4110]
));
content.push(heading('One concrete comparison', 3));
content.push(callout('Procedural idea', 'if targetType == ENEMY then enemyHealth[id] = enemyHealth[id] - damage; else if targetType == PLAYER then playerHealth = playerHealth - damage;', AMBER));
content.push(para('The project instead gets an IDamageable reference and calls TakeDamage(damage). The caller does not need to know if it hit the player, an enemy or a target dummy. That is a small example, but it removes repeated branches from every weapon.'));

content.push(pageBreak(), heading('2. Object and environment design', 1), heading('2.1 Characters and active objects', 2));
content.push(table(
  ['Object', 'Purpose', 'Important state', 'Main behaviour'],
  [
    ['Player', 'The first-person character controlled by the user.', 'health, momentum, grounded state, dash charges, grapple target', 'Move, jump, dash, slide, wall run, slam, grapple, interact and take damage.'],
    ['Ground enemy', 'Pressures the player from arena routes.', 'health, target, NavMesh path, attack cooldown, mass class', 'Checks line of sight, paths around walls and attacks when active.'],
    ['Flying enemy', 'Creates pressure above ground routes.', 'hover point, dash state, volley cooldown', 'Stops, shoots, dashes and repositions instead of circling forever.'],
    ['Boss', 'A stronger encounter on boss floors.', 'phase, attack routine, health, telegraph state', 'Chooses larger attack patterns and drops a weapon reward.'],
    ['Gun', 'Controls the equipped weapon and its ability.', 'preset, family, cooldowns, modifiers, tagged target', 'Fires, applies recoil, creates trails, runs abilities and switches variants.'],
    ['Terminal', 'Short objective required to clear a floor.', 'puzzle type, solved state, prompt', 'Accepts interaction, runs a puzzle and reports completion.'],
    ['Shop station', 'Offers one run upgrade.', 'service, price, spent state', 'Shows a preview, checks coins and applies one purchase.'],
    ['Arena director', 'Controls the current run and floor.', 'floor, objective state, timer, reward state', 'Starts encounters, scales enemies and begins transitions.']
  ], [1400, 2400, 2700, 2860]
));

content.push(pageBreak(), heading('2.2 Environment and items', 2));
content.push(bullet('Combat floors: generated platforms, walls, stairs, rails and vertical routes used for enemy encounters.'));
content.push(bullet('Shop floors: a quieter floor with simple pedestals showing the real gun or modifier model.'));
content.push(bullet('Boss floors: larger encounter spaces for boss attack patterns.'));
content.push(bullet('Jump pads: launch the player and preserve the fast movement loop.'));
content.push(bullet('Terminals: short interaction objectives that must be completed before leaving.'));
content.push(bullet('Weapon rewards: unlock and equip a weapon after an encounter.'));
content.push(bullet('Coins and weapon ability objects: temporary objects used by abilities, including Vesper ricochets.'));
content.push(bullet('Loading transition: a small Rubik-style cube runs while the next arena is actually generated.'));
content.push(heading('2.3 Inheritance and shared characteristics', 2));
content.push(table(
  ['Parent/interface', 'Children/implementations', 'Shared contract'],
  [
    ['MonoBehaviour', 'Most runtime components', 'Unity lifecycle, transform and component access.'],
    ['Interactable', 'Terminal, shop station, weapon reward and older door prototypes', 'Prompt range, focus feedback and OnInteract(PlayerController).'],
    ['Terminal', 'CybergrindPuzzleTerminal, KeypadTerminal and SwitchTerminal', 'Solved state, prompt, completion events and terminal visuals.'],
    ['PostProcessor', 'PathRepairProcessor, MicroPopulator and BossRoomPreset', 'Process(WFCGenerator3D) after generation.'],
    ['IDamageable', 'PlayerController, BasicEnemyAI and Target', 'TakeDamage(float amount).'],
    ['IGrappleMassTarget', 'BasicEnemyAI and Target', 'Light/heavy mass classification and grapple pull method.']
  ], [2100, 3500, 3760]
));
content.push(callout('Screenshot evidence', '[SCREENSHOT HERE - IDamageable.cs beside BasicEnemyAI.cs and PlayerController.cs. Caption: The same TakeDamage contract is implemented by different game objects.]', AMBER));
content.push(callout('Screenshot evidence', '[SCREENSHOT HERE - Interactable.cs beside CybergrindShopStation.cs or CybergrindPuzzleTerminal.cs. Caption: Child interactables override the shared OnInteract(PlayerController) method.]', AMBER));
content.push(heading('2.4 Object interaction examples', 2));
content.push(numberItem('Player input is read by PlayerController. Movement methods update momentum and the CharacterController.'));
content.push(numberItem('When firing, Gun performs the shot, creates a BulletTrail and calls TakeDamage on an IDamageable target.'));
content.push(numberItem('When the player presses interact, PlayerController calls OnInteract on the focused Interactable.'));
content.push(numberItem('A solved terminal reports its state. CybergrindArenaDirector checks terminals and remaining BasicEnemyAI objects before unlocking progression.'));
content.push(numberItem('During a transition, PersistentLoadingScreen and CybergrindTransitionController cover the scene while CybergrindArenaGenerator builds the next floor.'));

content.push(pageBreak(), heading('3. Research and planning', 1), heading('3.1 Project management', 2));
content.push(para('I used Git commits as checkpoints rather than trying to finish the game in one scene file. The commit history shows the project moving from basic interaction, to combat, to generation, then to progression and polish. I also used separate test scenes and an in-game debug menu while systems were unstable.'));
content.push(para('The plan changed during development. Early doors, keycards and generic WFC scripts were useful prototypes, but the final direction became an infinite arena roguelite. I kept the active direction written in PROJECT_GAME_INTENT.md so later changes targeted the correct systems.'));
content.push(callout('Planning decision', 'The final project does not pretend every prototype became a feature. Old scripts may remain as scaffolding, but the active game loop is PlayerController + Gun + BasicEnemyAI + ArenaDirector + ArenaGenerator.'));
content.push(heading('3.2 Data flow diagram', 2));
content.push(...await figure('data-flow.png', 'Figure 1. Runtime data flow from player input to game state and HUD feedback.', 650, 368));
content.push(callout('Development evidence', '[SCREENSHOT HERE - early test arena on the left and current generated arena on the right. Caption: The environment changed from a fixed test space into runtime-generated combat floors.]', AMBER));
content.push(pageBreak(), heading('3.3 Structure chart', 2));
content.push(...await figure('structure-chart.png', 'Figure 2. High-level structure chart showing the five main runtime systems.', 650, 403));
content.push(pageBreak(), heading('3.4 Class diagram', 2));
content.push(...await figure('class-diagram.png', 'Figure 3. Main inheritance and interface relationships used by the final game.', 650, 442));

content.push(pageBreak(), heading('3.5 Data dictionary', 2));
content.push(table(
  ['Name', 'Type', 'Owner', 'Meaning'],
  [
    ['health', 'float', 'PlayerController / BasicEnemyAI', 'Current hit points.'],
    ['momentum', 'Vector3', 'PlayerController', 'Velocity preserved between movement actions.'],
    ['isGrounded', 'bool', 'PlayerController', 'Whether the ground check is valid.'],
    ['dashCharges', 'int', 'PlayerController', 'Available air/ground dashes shown around the crosshair.'],
    ['grappleRange', 'float', 'PlayerController', 'Maximum valid grapple distance.'],
    ['grappleMassClass', 'enum', 'BasicEnemyAI / Target', 'Light targets are pulled; heavy targets pull the player.'],
    ['activePresetIndex', 'int', 'Gun', 'Index of the currently equipped weapon preset.'],
    ['nextTimeToFire', 'float', 'Gun', 'Time gate for the next primary shot.'],
    ['nextAltFireTime', 'float', 'Gun', 'Per-weapon ability cooldown.'],
    ['damage', 'float', 'WeaponPreset / Projectile', 'Base damage before run modifiers.'],
    ['agent', 'NavMeshAgent', 'BasicEnemyAI', 'Ground-enemy pathfinding component.'],
    ['target', 'Transform', 'BasicEnemyAI', 'Current player target.'],
    ['floor', 'int', 'CybergrindArenaDirector', 'Current run floor used for scaling.'],
    ['arenaMode', 'enum', 'CybergrindArenaGenerator', 'Combat, shop or boss generation mode.'],
    ['isSolved', 'bool', 'Terminal', 'Whether a terminal objective is complete.'],
    ['shopPurchaseUsed', 'bool', 'CybergrindRunState', 'Prevents a second purchase on the same floor.'],
    ['runDamageMultiplier', 'float', 'Gun', 'Damage modifier applied during the run.'],
    ['remainingTime', 'float', 'Floor timer system', 'Time left before the player dies.']
  ], [1900, 1700, 2700, 3060]
));

content.push(pageBreak(), heading('3.6 Quality success criteria', 2));
content.push(table(
  ['#', 'Criterion', 'How it is measured'],
  [
    ['1', 'Movement is responsive at high speed.', 'Immediate forward/reverse input; momentum chains reset on timeout or collision; no unwanted forward push from slam jumps.'],
    ['2', 'Generated floors are playable.', 'Test at least 10 seeded floors. Every spawn, objective and exit must be reachable, with stairs or another valid vertical route.'],
    ['3', 'Combat information is clear.', 'Weapon trails remain visible, abilities have feedback, enemies show damage and the last enemies are highlighted.'],
    ['4', 'The code demonstrates OOP.', 'Clear responsibilities plus working inheritance, interfaces, encapsulated state and polymorphic calls.'],
    ['5', 'The game performs consistently.', 'At 1920 x 1080, target at least 144 average FPS and 60 FPS 1% low during one fixed 60-second benchmark.'],
    ['6', 'The UI is readable and not crowded.', 'A new player identifies health, speed, dash state and the current objective within 30 seconds without explanation.'],
    ['7', 'The run supports replayability.', 'Floors continue indefinitely, themes vary and enemy health/count scale with depth.'],
    ['8', 'The project remains stable.', 'All automated tests pass, there are no C# compile errors, and 10 consecutive transitions complete without exposing the arena.']
  ], [500, 4000, 4860]
));

content.push(pageBreak(), heading('4. Production and implementation', 1), heading('4.1 Development approach', 2));
content.push(para('I built the project in layers. Movement came first because every arena and enemy decision depends on how fast the player can move. Combat and target objects came next. Procedural generation was added after there was something playable to generate. Progression, shops, UI and presentation were added once the core loop worked.'));
content.push(heading('4.2 Main systems implemented', 2));
content.push(table(
  ['System', 'Implementation'],
  [
    ['Movement', 'CharacterController movement with separate momentum, slide jumps, stacking speed, dash charges, wall running, slam jumps, air steering and speed feedback.'],
    ['Grapple', 'Surface raycasts, ledge aim assistance, valid-target reticle, physical pull forces, light/heavy target rules and failed-hit/release animations.'],
    ['Weapons', 'Four presets split into pistol and shotgun families. Each has primary fire, an RMB ability, independent cooldowns, trails, impacts and run modifiers.'],
    ['Enemy AI', 'Ground NavMesh pathfinding, flying movement states, 360-degree sensing blocked by solid walls, attack routines, bosses and floor scaling.'],
    ['Generation', 'Runtime arena construction, theme selection, traversal validation, stairs, rails, walls, spawn points, terminals, shops and boss layouts.'],
    ['Progression', 'Infinite floors, one shop purchase per floor, coins, upgrades, weapon unlocks, floor timer and death/restart flow.'],
    ['UI/presentation', 'Start screen, settings, HUD, dash indicator, prompts, hints, loading cube, transitions, enemy highlight and death screen.']
  ], [1900, 7460]
));
content.push(callout('Gameplay evidence 1', '[SCREENSHOT HERE - player moving quickly through a generated combat floor. Show the speed meter and dash indicator.]', AMBER));
content.push(callout('Gameplay evidence 2', '[SCREENSHOT HERE - grapple reticle locked to a wall or ledge with the grapple line visible.]', AMBER));
content.push(callout('Gameplay evidence 3', '[SCREENSHOT HERE - a shop pedestal showing the real gun or modifier model and the purchase prompt.]', AMBER));
content.push(callout('Gameplay evidence 4', '[SCREENSHOT HERE - weapon ability or enemy encounter showing trails, impact VFX and enemy feedback.]', AMBER));
content.push(heading('4.3 OOP in the implementation', 2));
content.push(para('The strongest OOP example is the way systems communicate through small contracts. Gun does not contain enemy movement code. BasicEnemyAI does not contain HUD layout code. A shop station does not directly regenerate the entire arena. These objects send results to the systems that own that state.'));
content.push(para('IDamageable is used by the player, enemies and targets. This lets hitscan and projectile code treat them the same way. IGrappleMassTarget adds a second independent contract. An enemy can be damageable and grappleable without inheriting from a special combined parent class. This avoids a deep inheritance tree.'));

content.push(pageBreak(), heading('5. Testing and evaluation', 1), heading('5.1 Testing methods', 2));
content.push(table(
  ['Method', 'How I used it'],
  [
    ['Unit-level white-box', 'Inspected and exercised one method or state rule at a time, such as cooldown reset, damage interfaces, shop purchase state and floor scaling calculations.'],
    ['Subsystem testing', 'Tested connected groups such as Gun + BulletTrail + IDamageable, or PlayerController + grapple projectile + target mass.'],
    ['System testing', 'Ran complete floors from load, through terminals and combat, to shop/transition/death.'],
    ['White-box testing', 'Used knowledge of branches, state variables, raycasts, masks and cooldowns to target specific paths.'],
    ['Grey-box testing', 'Played normally while using the debug HUD, Unity Console and known system rules to diagnose failures.'],
    ['Quality assurance', 'Repeated compile checks, removed deprecated API use, checked the Unity Console and kept generated/cache files out of Git.'],
    ['Black-box/user testing', 'Reserved for a classmate using only the controls and visible UI. Results are intentionally left for the real tester.']
  ], [2300, 7060]
));
content.push(heading('5.2 Automated EditMode tests', 2));
content.push(para('I added CybergrindRules as a small deterministic rules class and an NUnit test file at Assets/Tests/Editor/CybergrindRulesTests.cs. These tests do not need a generated scene, which makes failures easier to reproduce. The final EditMode run completed on 21 June 2026 at 7:55 PM: 6 of 6 tests passed, with zero failures, skipped or inconclusive tests. Total recorded duration was 0.0599 seconds. The exported evidence file is TestResults_20260621_195552.xml.'));
content.push(callout('Automated test result', 'PASS - 6/6 EditMode tests passed. 0 failed, 0 skipped, 0 inconclusive. Exported from Unity Test Runner on 21 June 2026.', GREEN));
content.push(table(
  ['Automated test', 'Input', 'Expected result', 'Recorded result'],
  [
    ['EnemyHealthMultiplier_StartsAtOneAndIncreasesWithFloor', 'Floor 1, 2 and 10', 'Floor 1 = 1.0; later floors are greater.', 'PASS - 0.001105 s'],
    ['EnemyCountBonus_StartsAtZeroAndIncreasesWithFloor', 'Floor 1, 2 and 10', 'Floor 1 bonus = 0; later floors are greater.', 'PASS - 0.017804 s'],
    ['WeaponDamage_AppliesEveryMultiplier', '100 x 0.88 x 1.10 x 1.16 x 1.08', 'Damage = 121.27104.', 'PASS - 0.000133 s'],
    ['ShopLock_ReflectsWhetherPurchaseWasMadeThisFloor', 'false, then true', 'Unlocked, then locked.', 'PASS - 0.000291 s'],
    ['TimerNormalized_ClampsToValidHudRange', '30/60, 90/60 and 10/0', '0.5, 1.0 and 0.0.', 'PASS - 0.000205 s'],
    ['TimerTick_StopsAtZero', '1 - 0.25 and 0.1 - 0.5', '0.75 and 0.0.', 'PASS - 0.000155 s']
  ], [2400, 1900, 2450, 2610]
));
content.push(callout('Screenshot evidence', '[SCREENSHOT HERE - Unity Test Runner in EditMode with CybergrindRulesTests expanded. Caption: All six deterministic rules tests passed on 21 June 2026. The exported XML records 6 passed and 0 failed.]', AMBER));
content.push(heading('5.3 Additional white-box checks', 2));
content.push(table(
  ['Test', 'Code path checked', 'Expected result', 'Result'],
  [
    ['Damage contract', 'Gun hit resolution -> IDamageable.TakeDamage', 'Player, enemy and target receive damage without type-specific branches.', 'Pass - all three implement the same interface.'],
    ['Weapon switch cooldown', 'Gun.ResetTransientAbilityStateForSwitch', 'Changing weapons resets the switched weapon ability state.', 'Pass - cooldown and temporary charge/tether state are reset.'],
    ['Shop purchase limit', 'CybergrindShopStation + CybergrindRunState', 'Only one station can be purchased on a floor.', 'Pass - spent/purchase state blocks later stations.'],
    ['Grapple mass', 'IGrappleMassTarget + GrappleMassClass', 'Light enemy is pulled; heavy enemy pulls the player.', 'Pass - behaviour branches on the exposed mass class.'],
    ['Enemy awareness', 'BasicEnemyAI line-of-sight raycasts', 'Enemy senses in 360 degrees but a solid wall blocks aggro.', 'Pass for direct LOS; generated navigation still needs system testing.'],
    ['Generation pacing', 'GenerateArenaRoutine coroutine', 'Large generation work is split across frames.', 'Pass - the routine yields between major construction stages.'],
    ['Compile stability', 'Unity editor compilation', 'No C# errors after rules/tests were added.', 'Pass on 21 June - latest Editor log showed no C# compile errors.']
  ], [1700, 2450, 2900, 2310]
));

content.push(pageBreak(), heading('5.4 Grey-box and subsystem test record', 2));
content.push(table(
  ['Area', 'Test performed', 'Problem found', 'Change made / current result'],
  [
    ['Movement reversal', 'Move forward, then instantly reverse at speed.', 'The player originally made a wide U-turn.', 'Air steering and reversal handling were separated. Reversal now reacts quickly without removing all momentum.'],
    ['Slide-jump chain', 'Repeat crouch jump on landing and then miss the timing window or collide.', 'Early versions stacked forever or reset too quickly.', 'The window was lengthened; speed stacks without a fixed cap but resets on timeout/collision.'],
    ['Slam jump', 'Slam, jump on landing and repeat.', 'The jump added unwanted forward movement.', 'Launch was changed to vertical and repeated timing adds a small height increase.'],
    ['Weapon trails', 'Fire repeatedly at maximum rate and hit coins.', 'Trails disappeared or continued through a coin while a second trail spawned.', 'Trail lifetime/pooling and coin-chain trail ownership were revised.'],
    ['Grapple release', 'Release manually, hit an invalid floor, move behind geometry and remain at long range.', 'The hook sometimes vanished or disconnected too early.', 'Release/retract states and invalid-hit bounce feedback were added; this remains a regression focus.'],
    ['Loading transition', 'Start a run and move between floors while generation takes different times.', 'The arena flashed through and loading animation could finish before loading.', 'A black loading overlay remains active while actual generation runs; the cube loops until ready.'],
    ['Enemy navigation', 'Fight across rails, stairs and vertical platforms.', 'Enemies attempted shortcuts through rails or became stranded where stairs were missing.', 'Path repair, wall classification and stair checks were added. Some unusual generated layouts remain the main known risk.'],
    ['HUD health', 'Take repeated damage while moving and changing floors.', 'A decorative health bar did not decrease and duplicated HUDs overlapped.', 'Duplicate HUDs were removed and fill scale now follows current/max health.'],
    ['Performance', 'Watch average and 1% FPS during generation and combat.', 'Average was around 150-200 FPS but 1% lows could fall heavily during spikes.', 'Generation was spread across frames and repeated allocations/searches were reduced. Average improved, but 1% lows still need profiling.']
  ], [1500, 2350, 2550, 2960]
));
content.push(callout('Before/after evidence', '[SCREENSHOT HERE - one broken generated stair/pathfinding layout beside the repaired layout. Mark the inaccessible route with an arrow.]', AMBER));
content.push(callout('Before/after evidence', '[SCREENSHOT HERE - old overlapping or incorrect health HUD beside the current compact health HUD.]', AMBER));
content.push(callout('Before/after evidence', '[SCREENSHOT HERE - loading overlay or grapple feedback before and after the fix.]', AMBER));

content.push(pageBreak(), heading('5.5 Black-box and classmate testing', 2));
content.push(callout('Complete after the real test', 'Give the build to someone who has not watched development. Do not explain the controls unless they ask. Record what they try, where they get stuck and the exact feedback they give.', AMBER));
content.push(table(
  ['Tester/date', 'Task', 'Observed result or feedback', 'Change made'],
  [
    ['________________', 'Start a run and identify the objective.', '________________________________________', '________________________________'],
    ['________________', 'Use movement, grapple and both weapon families.', '________________________________________', '________________________________'],
    ['________________', 'Clear a floor, buy one shop item and continue.', '________________________________________', '________________________________'],
    ['________________', 'Explain the HUD without help.', '________________________________________', '________________________________']
  ], [1700, 2700, 3000, 1960]
));
content.push(heading('Suggested black-box questions', 2));
content.push(bullet('What did you think the objective was?'));
content.push(bullet('Which controls did you understand without being told?'));
content.push(bullet('Was any HUD element confusing or too large?'));
content.push(bullet('Did a death, shop or loading transition leave you unsure what to do next?'));
content.push(bullet('Which movement or weapon action felt unreliable?'));

content.push(pageBreak(), heading('5.6 Evaluation against success criteria', 2));
content.push(table(
  ['Criterion', 'Evidence', 'Evaluation'],
  [
    ['1. Responsive movement', 'Multiple reversal, slide-jump, wall-run and slam-jump passes were made.', 'Mostly met. Movement is the strongest part of the game, but extreme-speed edge cases still require regression testing.'],
    ['2. Playable generation', 'Path repair, stairs, rails, wall colliders and traversal checks exist.', 'Partly met. Normal layouts work, but unusual vertical generation can still strand enemies.'],
    ['3. Clear combat', 'Trails, impacts, ability reticles, glints, charge feedback and highlights were added.', 'Mostly met. Core feedback is present; grapple and some abilities can still be improved.'],
    ['4. OOP design', 'Abstract Interactable/PostProcessor classes and IDamageable/IGrappleMassTarget interfaces are active.', 'Met. The final systems use inheritance, interfaces, encapsulation and component composition.'],
    ['5. Performance', 'Fixed benchmark: about 200 average FPS, 30 FPS 1% low, 8.05 ms selected CPU frame and about 1 second generation.', 'Partly met. Average FPS and generation targets passed, but the 60 FPS 1% low and 6.94 ms CPU-frame targets were not met.'],
    ['6. UI clarity', 'Compact health HUD, speed meter, dash indicator, prompts and hints are implemented.', 'Mostly met. The UI is less crowded, but the real black-box test is still required.'],
    ['7. Replayability', 'Infinite progression, scaling counts/health, variable themes, shops and weapon upgrades.', 'Met. The run continues until death and later floors increase pressure.'],
    ['8. Stability', 'Latest Editor log has no C# compile errors; loading is tied to generation completion; all 6 EditMode tests passed.', 'Met at the time of this report, with procedural edge cases listed as known risks.']
  ], [1900, 3600, 3860]
));

content.push(pageBreak(), heading('5.7 Efficiency and optimisation', 2));
content.push(para('The main performance issue was not the number of features by itself. It was expensive work happening in one frame, repeated object searches, physics allocations and visual objects being created too often. The optimisation work focused on those causes.'));
content.push(table(
  ['Measure', 'Why it helps'],
  [
    ['Coroutine-based arena generation', 'GenerateArenaRoutine yields between major stages so the game does not freeze for one long frame.'],
    ['Non-alloc physics where practical', 'OverlapSphereNonAlloc reuses a buffer instead of creating a new array every query.'],
    ['Cached component references', 'Frequently used cameras, controllers and agents are stored instead of repeatedly calling GetComponent or scene searches.'],
    ['Pooled impact effects', 'Projectile impact objects can be reused instead of constantly instantiated and destroyed.'],
    ['Restricted active weapon roster', 'Only supported weapons are exposed, reducing unused runtime/UI paths.'],
    ['Simplified HUD and debug visibility', 'Duplicate canvases and always-visible debug information were removed.'],
    ['Git repository cleanup', 'The generated Unity Library directory was removed from version control, reducing repository size and merge noise.'],
    ['Deprecated API replacement', 'Unity 6 obsolete calls were replaced to keep the project forward-compatible and the Console readable.']
  ], [3000, 6360]
));
content.push(heading('Fixed performance benchmark', 2));
content.push(para('This benchmark was recorded in the Unity Editor on one combat floor. The run used the same active quality settings for the full sample and included combat plus arena generation. The in-game summary was approximately 200 average FPS and 30 FPS 1% low. The captured HUD frame shows 186 FPS and 25 FPS 1% low, which demonstrates normal variation during the run.'));
content.push(table(
  ['Measurement', 'Target', 'Current result', 'Evaluation'],
  [
    ['Average FPS', 'At least 144 FPS', 'Approximately 200 FPS', 'Pass'],
    ['1% low FPS', 'At least 60 FPS', 'Approximately 30 FPS', 'Not met'],
    ['CPU frame time', 'At most 6.94 ms', '8.05 ms selected frame', 'Not met'],
    ['PlayerLoop cost', 'Record for diagnosis', '6.29 ms / 78.2%', 'UpdateScene is the largest visible child'],
    ['Largest visible child', 'Identify the main cost', 'UpdateScene: 5.11 ms / 63.5%', 'Expand UpdateScene for a method-level follow-up'],
    ['Peak active enemies', 'Record tested load', '6 on the tested floor', 'Enemy count scales on later floors'],
    ['Arena generation time', 'At most 2 seconds', 'Approximately 1 second', 'Pass']
  ], [2200, 2100, 2500, 2560]
));
content.push(...await evidenceFigure('performance-profiler.png', 'Figure 4. Unity CPU Profiler capture. The selected frame is 8.05 ms overall; PlayerLoop uses 6.29 ms (78.2%) and UpdateScene is the largest visible child at 5.11 ms (63.5%).', 650, 266));
content.push(...await evidenceFigure('performance-fps-counter.png', 'Figure 5. In-game performance HUD captured during the benchmark. This frame shows 186 FPS and a 25 FPS 1% low.', 650, 133));
content.push(heading('Known technical debt', 2));
content.push(bullet('PlayerController, Gun, BasicEnemyAI and FINALArenaGenerator are very large classes. They work, but should be split into smaller movement, ability, attack-state and generation modules.'));
content.push(bullet('There is no retained automated EditMode/PlayMode test suite. The Unity Test Framework package is installed, so the next step is to add tests around pure calculations and state rules [4].'));
content.push(bullet('Generation and enemy navigation need seeded benchmark scenes so the same difficult layouts can be reproduced.'));
content.push(bullet('Performance results should use a fixed route and profiler capture instead of only the live FPS HUD [3].'));

content.push(pageBreak(), heading('6. Reconstructed development journal', 1));
content.push(para('This journal was reconstructed from dated Git commits and the files changed in each commit. It does not claim that every note was written on the day. It records the actual order in which the project developed.'));
content.push(table(
  ['Date', 'Progress and decisions', 'Problem / learning'],
  [
    ['22 Apr (e0f7a781)', 'Created the repository and imported the Unity URP project.', 'Learned the basic Unity project structure and what should be tracked in Git.'],
    ['4 May (95465a00)', 'Configured scenes, packages and project settings.', 'The project still had no clear gameplay loop, so setup came before feature work.'],
    ['8 May (304bdf7e)', 'Added PlayerController and the Interactable base class.', 'Interaction needed a shared parent instead of separate input checks for every object.'],
    ['10 May (d2bb0e4b)', 'Added doors, keycards, terminals, switches and collectible inheritance.', 'These prototypes taught inheritance and event-based interaction, even though most were not used in the final loop.'],
    ['25 May (1dd9f2b6)', 'Built the first proper playable scene and visual test environment.', 'A test space was needed before procedural generation could be judged.'],
    ['26 May (433492ac)', 'Added Gun, Projectile, BulletTrail, Target, IDamageable and BasicEnemyAI.', 'Combat became several connected objects instead of one shooting script.'],
    ['26 May (ae50b0ad)', 'Started WFCGenerator3D, tiles, macro generation, path repair and jump pads.', 'Random placement alone could create unreachable spaces, so repair passes were required.'],
    ['27 May (efa4b743)', 'Expanded generation with hangar, sequential-room, boss and population systems.', 'The generator needed separate macro layout and post-processing responsibilities.'],
    ['28 May (0197e92c)', 'Created the Arena scene and current arena director/generator/terminal loop.', 'The project direction changed from small puzzles to a replayable arena run.'],
    ['28 May (b7db0233)', 'Removed Unity Library from Git and added generated pickups/progression.', 'Generated cache files made the repository unnecessarily large and unstable.'],
    ['7 Jun (b617004e)', 'Expanded movement, weapons, enemies and debug tools; wrote the game-intent file.', 'Old prototypes and current systems were becoming easy to confuse.'],
    ['8 Jun (2414e737)', 'Added run state, shops, rewards, boss HUD and transitions.', 'Progression required one owner for run-level state instead of scattered flags.'],
    ['12 Jun (74497161)', 'Continued combat, movement, jump-pad, terminal and arena refinement.', 'Game feel bugs often involved several systems, so subsystem testing became important.'],
    ['17 Jun (65bfd3ac)', 'Added combat feedback, run HUD, start menu, settings, shop preview and weapon models.', 'The game worked but was hard to read, so UI and feedback became the priority.'],
    ['18 Jun (47156265)', 'Reworked loading and arena transitions and continued enemy/combat fixes.', 'The loading animation had to cover real loading rather than play before generation.'],
    ['19 Jun (f6b02722)', 'Added the grapple projectile and integrated grapple input, settings and generation changes.', 'Grappling exposed problems with arena scale, valid surfaces and momentum transfer.'],
    ['Final pass', 'Reduced the weapon roster, made runs infinite, added scaling, limited shop purchases and simplified UI copy.', 'Removing unsupported content made the final game clearer and easier to test.']
  ], [1200, 4950, 3210]
));
content.push(callout('Journal evidence', '[SCREENSHOT HERE - GitHub Desktop or git log showing commits from April, May and June. The commit dates and hashes must be readable.]', AMBER));

content.push(pageBreak(), heading('7. Presentation plan', 1));
content.push(para('Target length: 10-12 minutes. The presentation should show the game early, then explain the technical decisions using the report diagrams.'));
content.push(table(
  ['Time', 'Content', 'Visual'],
  [
    ['0:00-1:00', 'Hook: show fast movement and explain the core problem - building arenas and systems that still work at high speed.', 'Short gameplay clip.'],
    ['1:00-2:00', 'Explain the final loop: combat floor, terminals, shop, boss and infinite scaling.', 'One simple loop diagram or gameplay sequence.'],
    ['2:00-4:00', 'Explain OOP: Interactable inheritance, IDamageable polymorphism and separate system responsibilities.', 'Class diagram and a short code example.'],
    ['4:00-6:00', 'Explain procedural generation and why path repair/stairs/rails were difficult.', 'Structure chart plus two contrasting generated floors.'],
    ['6:00-8:00', 'Show movement, grapple, weapons and enemy behaviour.', 'Gameplay clip with HUD visible.'],
    ['8:00-10:00', 'Testing and optimisation: show a bug, the cause and the fix; explain average FPS and remaining 1% low risk.', 'Before/after clip or profiler/debug HUD.'],
    ['10:00-11:00', 'Evaluation: what met the criteria, what is still technical debt and what I would do next.', 'Success-criteria summary.'],
    ['11:00-12:00', 'Conclusion and questions.', 'Final gameplay shot and three key takeaways.']
  ], [1200, 5750, 2410]
));
content.push(heading('Likely Q&A', 2));
content.push(bullet('Why use Unity/C# instead of the guided Python project?'));
content.push(bullet('Where does the project demonstrate polymorphism rather than only classes?'));
content.push(bullet('How does the generator stop impossible layouts?'));
content.push(bullet('What caused the largest performance or pathfinding problem?'));
content.push(bullet('What would you split up if the project continued?'));

content.push(pageBreak(), heading('8. Conclusion', 1));
content.push(para('The final project became much larger than the first interaction prototype. It now has a complete repeatable loop: generated floors, fast movement, grappling, combat, terminals, shops, bosses, scaling and death/restart. The strongest OOP decision was using small contracts such as IDamageable and IGrappleMassTarget. They let unrelated objects share behaviour without forcing the combat and grapple systems into one inheritance tree.'));
content.push(para('The hardest problem was procedural generation supporting both fast player movement and enemy navigation. A layout can look playable to the player while still stranding a ground enemy behind a rail or vertical gap. Path repair, stairs and wall classification improved this, but seeded traversal tests are still the clearest next step.'));
content.push(para('The project meets most of the success criteria, especially movement, OOP structure and replayability. The weaker evidence is consistent performance measurement and new-player testing. The automated rules tests added for the final report are a start, but future work should add PlayMode tests for transitions, shops and generated traversal. The final black-box test should decide whether the HUD and objectives are actually clear without developer explanation.'));

content.push(pageBreak(), heading('9. Bibliography', 1));
content.push(sourceItem(1, 'Ahl, David H. - BASIC Computer Games: Hunt the Wumpus', 'https://www.atariarchives.org/bcc1/showpage.php?page=247', 'Accessed June 2026. Original BASIC listing and explanation of the cave game.'));
content.push(sourceItem(2, 'Microsoft Learn - Object-oriented programming in C#', 'https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/tutorials/oop', 'Accessed June 2026. Used for definitions of abstraction, encapsulation, inheritance and polymorphism.'));
content.push(sourceItem(3, 'Unity Manual - Profiler overview', 'https://docs.unity3d.com/Manual/Profiler.html', 'Accessed June 2026. Used for performance-testing and profiling methodology.'));
content.push(sourceItem(4, 'Unity Test Framework 1.6 manual', 'https://docs.unity3d.com/Packages/com.unity.test-framework@1.6/manual/index.html', 'Accessed June 2026. Used for EditMode and PlayMode test planning.'));
content.push(sourceItem(5, 'Unity AI Navigation 2.0 manual', 'https://docs.unity3d.com/Packages/com.unity.ai.navigation@2.0/manual/index.html', 'Accessed June 2026. Used for NavMesh-based enemy navigation.'));
content.push(sourceItem(6, 'Unity Manual - Coroutines', 'https://docs.unity3d.com/Manual/Coroutines.html', 'Accessed June 2026. Used when splitting arena generation and timed sequences across frames.'));
content.push(heading('Project evidence', 2));
content.push(para('Repository files used as direct evidence: Assets/Scripts/PlayerController.cs; Assets/Scripts/Combat/Gun.cs; Assets/Scripts/Combat/IDamageable.cs; Assets/Scripts/Combat/IGrappleMassTarget.cs; Assets/Scripts/AI/BasicEnemyAI.cs; Assets/Scripts/WFC/FINALArenaGenerator.cs; Assets/Scripts/WFC/FINALArenaDirector.cs; Assets/Scripts/WFC/FINALPuzzleTerminal.cs; README.md; and Git history from 22 April to 19 June 2026.'));

const header = new Header({ children: [new Paragraph({ children: [run('TERM 2 SWE PROJECT', { bold: true, color: MID, size: 17 }), run('   |   OOP PROJECT REPORT', { color: MID, size: 17 })], alignment: AlignmentType.RIGHT, spacing: { after: 0 } })] });
const footer = new Footer({ children: [new Paragraph({ children: [run('Gordon Zhao', { color: MID, size: 17 }), run('                                                        ', { size: 17 }), run('Page ', { color: MID, size: 17 }), new TextRun({ children: [PageNumber.CURRENT], font: 'Calibri', size: 17, color: MID })], alignment: AlignmentType.CENTER })] });

const doc = new Document({
  creator: 'Gordon Zhao', title: 'Term 2 SWE project - Object-Oriented Programming Project Report', subject: 'Software Engineering Assessment 2',
  styles: {
    default: { document: { run: { font: 'Calibri', size: 22, color: DARK }, paragraph: { spacing: { after: 120, line: 264 } } } },
    paragraphStyles: [
      { id: 'Heading1', name: 'Heading 1', basedOn: 'Normal', next: 'Normal', quickFormat: true, run: { font: 'Calibri', size: 32, bold: true, color: BLUE }, paragraph: { spacing: { before: 320, after: 160 }, keepNext: true } },
      { id: 'Heading2', name: 'Heading 2', basedOn: 'Normal', next: 'Normal', quickFormat: true, run: { font: 'Calibri', size: 26, bold: true, color: BLUE }, paragraph: { spacing: { before: 240, after: 120 }, keepNext: true } },
      { id: 'Heading3', name: 'Heading 3', basedOn: 'Normal', next: 'Normal', quickFormat: true, run: { font: 'Calibri', size: 23, bold: true, color: '1F4D78' }, paragraph: { spacing: { before: 160, after: 80 }, keepNext: true } }
    ]
  },
  numbering: {
    config: [
      { reference: 'bullet-list', levels: [{ level: 0, format: LevelFormat.BULLET, text: '•', alignment: AlignmentType.LEFT, style: { paragraph: { indent: { left: 720, hanging: 360 } } } }] },
      { reference: 'number-list', levels: [{ level: 0, format: LevelFormat.DECIMAL, text: '%1.', alignment: AlignmentType.LEFT, style: { paragraph: { indent: { left: 720, hanging: 360 } } } }] }
    ]
  },
  sections: [{
    properties: { page: { size: { width: 12240, height: 15840 }, margin: { top: 1080, right: 1440, bottom: 1080, left: 1440, header: 560, footer: 560 } } },
    headers: { default: header }, footers: { default: footer }, children: content
  }]
});

await fs.writeFile(outPath, await Packer.toBuffer(doc));
console.log(outPath);
