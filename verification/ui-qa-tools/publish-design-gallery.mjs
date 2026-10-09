// Copy selected QA artifacts verbatim into versioned assets for GitHub Markdown.
// Does not regenerate screenshots or imply additional visual acceptance.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const qa = path.join(root, 'verification/ui-scale-qa-v04-ui-fix-20261007-release');
const document = fs.readFileSync(path.join(root, '飞行模拟器产品设计文档.md'), 'utf8');
const copies = [];
const desktop = path.join(qa, 'desktop-1920x1080');
for (const name of fs.readdirSync(desktop).sort()) {
  if (/^100-text1-.+\.png$/.test(name)) {
    copies.push([path.join(desktop, name), `assets/ui-v04/${name.replace('100-text1-', '')}`]);
  }
}
const scales = [
  ['desktop-1280x720/150-text1-Aerial-Pause.png', '1280-ui150-pause.png'],
  ['desktop-1280x720/100-text2-Aerial-Home.png', '1280-text200-home.png'],
  ['desktop-1280x720/100-text2-Aerial-Settings.png', '1280-text200-settings.png'],
  ['phone-landscape-844x390/100-text1-Aerial-Flight.png', '844x390-flight.png'],
  ['phone-portrait-390x844/100-text1-Aerial-Replay.png', '390x844-replay.png'],
  ['dpi3-2532x1170/300-text1-Aerial-Flight.png', '2532x1170-scale300-flight.png'],
];
for (const [source, name] of scales) copies.push([path.join(qa, source), `assets/ui-v04/scales/${name}`]);
for (const name of ['hangar-cameras-fpv.png', 'hangar-trainers.png']) {
  copies.push([path.join(qa, 'aircraft', name), `assets/ui-v04/${name}`]);
}
// Preserve historical images under an explicitly historical directory.
for (const match of document.matchAll(/!\[[^\]]*\]\(([^)]+)\)/g)) {
  const source = match[1];
  const relative = path.isAbsolute(source) ? path.relative(root, source) : source;
  if (relative.startsWith('verification/desktop/') || relative.startsWith('verification/graphics-v2/')) {
    copies.push([path.resolve(root, relative), `assets/ui-history/${relative.slice('verification/'.length)}`]);
  }
}
for (const [source, destination] of copies) {
  if (!fs.existsSync(source)) throw new Error(`Missing original screenshot: ${source}`);
  const target = path.join(root, destination);
  fs.mkdirSync(path.dirname(target), { recursive: true });
  fs.copyFileSync(source, target);
}
console.log(JSON.stringify({ copied: copies.length, destination: 'assets/ui-v04 + assets/ui-history' }));
