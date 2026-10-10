// Independent documentation QA. This reads source/assets only and never approves art.
// Run: node verification/v125-qa/prd-review/prd-document-check.mjs --prepare
// Final: node verification/v125-qa/prd-review/prd-document-check.mjs --doc docs/V125_ART_MAVIC4_REFERENCE.md
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {spawnSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';
import assert from 'node:assert/strict';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../..');
const baselinePath = path.join(root, 'verification/v125-qa/prd-review/baseline.json');
const prdPath = '飞行模拟器产品设计文档.md';
const sha = file => crypto.createHash('sha256').update(fs.readFileSync(path.join(root, file))).digest('hex');
const read = file => fs.readFileSync(path.join(root, file), 'utf8');
const args = process.argv.slice(2), prepare = args.includes('--prepare');
const requireTracked = args.includes('--require-tracked');
const extraDocs = args.flatMap((v, i) => v === '--doc' ? [args[i + 1]] : []);
if (extraDocs.some(v => !v || v.startsWith('--'))) throw Error('--doc requires a repository-relative Markdown path');

function stripCode(source) {
  // Preserve line numbers while excluding examples inside code spans/fences.
  return source.replace(/^([ \t]*)(\x60{3,}|~{3,}).*\n[\s\S]*?^\1\2[ \t]*$/gm, s => s.replace(/[^\n]/g, ' '))
    .replace(/(\x60+)([^\n]*?)\1/g, s => s.replace(/[^\n]/g, ' '));
}
function delimited(source, at, open, close) {
  if (source[at] !== open) return null;
  let depth = 1;
  for (let i = at + 1; i < source.length; i++) {
    if (source[i] === '\\') { i++; continue; }
    if (source[i] === open) depth++;
    if (source[i] === close && --depth === 0) return {body: source.slice(at + 1, i), end: i + 1};
    // Closing depth is only decremented above, not for arbitrary characters.
    if (source[i] !== close) { /* keep depth */ }
  }
  return null;
}
function destination(body) {
  const s = body.trim();
  if (s.startsWith('<')) return s.slice(1, s.indexOf('>'));
  let end = 0, depth = 0;
  for (; end < s.length; end++) {
    if (s[end] === '\\') { end++; continue; }
    if (s[end] === '(') depth++;
    if (s[end] === ')') depth--;
    if (/\s/.test(s[end]) && depth === 0) break;
  }
  return s.slice(0, end).replace(/\\([\\()[\] ])/g, '$1');
}
function links(source) {
  const clean = stripCode(source), results = [], definitions = new Map();
  const line = at => clean.slice(0, at).split('\n').length;
  for (const m of clean.matchAll(/^[ \t]{0,3}\[([^\]]+)\]:[ \t]*(.*)$/gm))
    definitions.set(m[1].toLowerCase(), destination(m[2]));
  for (let i = 0; i < clean.length; i++) {
    const image = clean[i] === '!' && clean[i + 1] === '[';
    const start = i;
    if (image) i++;
    if (clean[i] !== '[' || clean[i - 1] === '\\') continue;
    const label = delimited(clean, i, '[', ']');
    if (!label) continue;
    let next = label.end;
    while (/[ \t]/.test(clean[next] || '') && next < clean.length) next++;
    if (clean[next] === ':') { i = label.end - 1; continue; } // reference definition, not another link occurrence
    let url, end = next;
    if (clean[next] === '(') {
      const target = delimited(clean, next, '(', ')');
      if (target) { url = destination(target.body); end = target.end; }
    } else if (clean[next] === '[') {
      const ref = delimited(clean, next, '[', ']');
      if (ref) { url = definitions.get((ref.body || label.body).toLowerCase()); end = ref.end; }
    } else if (definitions.has(label.body.toLowerCase())) {
      url = definitions.get(label.body.toLowerCase());
    }
    if (url !== undefined) results.push({url, image, label: label.body, line: line(start)});
    i = Math.max(i, end - 1);
  }
  for (const m of clean.matchAll(/<(img|a)\b[^>]*?\b(src|href)=["']([^"']+)["'][^>]*>/gi))
    results.push({url: m[3], image: m[1].toLowerCase() === 'img', label: 'HTML ' + m[1], line: line(m.index)});
  return results;
}
function resolveLink(document, link) {
  const url = link.url;
  if (!url || url.startsWith('#') || /^(https?:|mailto:|data:|tel:|codex:|plugin:|\/\/)/i.test(url)) return null;
  let decoded;
  try { decoded = decodeURIComponent(url.replace(/^file:\/\//i, '').split(/[?#]/)[0]); }
  catch { return {...link, document, invalidEncoding: true, target: url}; }
  const absolute = path.isAbsolute(decoded) || /^file:/i.test(url);
  const target = path.resolve(root, path.dirname(document), decoded);
  const rel = path.relative(root, target).split(path.sep).join('/');
  return {...link, document, absolute, target: rel, outsideRoot: rel.startsWith('../') || rel === '..', exists: fs.existsSync(target)};
}
function documentLinks(document) {
  return links(read(document)).map(link => resolveLink(document, link)).filter(Boolean);
}
function selfTest() {
  const sample = '# Heading\n![image](assets/pic.png)\n[file](docs/a.md#anchor)\n[web](https://example.test/a_(b))\n[title](<docs/a b.md> "Title")\n[ref][x]\n[x]: assets/pic2.png\n\x60[not-link](no.md)\x60\n\x60\x60\x60js\n[not-link](no2.md)\n\x60\x60\x60\n[heading](#anchor)\n';
  const found = links(sample);
  assert.equal(found.length, 6); // five file/web links plus one heading; definition is not an occurrence
  assert.equal(found[2].url, 'https://example.test/a_(b)');
  assert.equal(found[3].url, 'docs/a b.md');
  assert.equal(found[4].url, 'assets/pic2.png');
  assert.ok(found.every(x => !x.url.startsWith('no')));
}
selfTest();

if (args.includes('--capture-baseline')) {
  // Capture to stdout; the reviewing agent saves it through apply_patch, never this script.
  if (fs.existsSync(baselinePath)) throw Error('Baseline already exists; refusal to silently replace the original freeze');
  const reference = JSON.parse(read('verification/v1-qa/reference-manifest.json'));
  assert.equal(reference.screens.length, 23);
  for (const item of reference.screens) assert.equal(sha(item.file), item.sha256, 'Original reference was already changed: ' + item.file);
  const initialLinks = documentLinks(prdPath);
  const preserved = ['mavic-pro-draft-01.png', 'mavic-pro-draft-02.png', 'mavic-pro-draft-01.prompt.txt', 'mavic-pro-draft-02.prompt.txt'];
  console.log(JSON.stringify({schemaVersion: 1, capturedAt: new Date().toISOString(), reason: 'Independent pre-reorganization byte freeze; not user acceptance',
    initialPrdSha256: sha(prdPath), referenceManifestSha256: sha('verification/v1-qa/reference-manifest.json'),
    originalConcepts: reference.screens.map(({id, state, file, sha256, width, height}) => ({id, state, file, sha256, width, height})),
    preservedMavicFiles: preserved.map(name => { const file = 'assets/v125-art/mavic/' + name; return {file, sha256: sha(file)}; }),
    oldMavicCurrent: JSON.parse(read('assets/v125-art/mavic/review-drafts.json')).find(x => x.aircraftId === 'dji-mavic-pro'),
    initialPrdMissingLinks: initialLinks.filter(x => !x.exists), initialNonPortableLinks: initialLinks.filter(x => x.absolute || x.outsideRoot)
  }, null, 2));
  process.exit(0);
}

const baseline = JSON.parse(fs.readFileSync(baselinePath, 'utf8'));
const report = {schemaVersion: 1, mode: prepare ? 'prepared_baseline_audit' : 'frozen_document_audit', requireTracked,
  checkedAt: new Date().toISOString(), sourceSha256: {}, checks: [], failures: [], publicationPending: [],
  notes: ['Automated documentation/byte checks do not establish visual equality, user acceptance, iOS hardware coverage, real flight dynamics or copyright authorization.'],
  historicalBrokenLinks: [], localLinks: []};
function check(id, ok, evidence) {
  const item = {id, result: ok ? 'PASS' : 'FAIL', evidence}; report.checks.push(item);
  if (!ok) report.failures.push(item);
}
const initialMissing = new Set(baseline.initialPrdMissingLinks.map(x => x.target));
const documents = [prdPath, ...extraDocs];
const git = spawnSync('git', ['ls-files', '-z'], {cwd: root, encoding: 'utf8'});
const tracked = new Set(git.status === 0 ? git.stdout.split('\0').filter(Boolean) : []);
for (const document of documents) {
  check('DOCUMENT_PRESENT:' + document, fs.existsSync(path.join(root, document)), document);
  if (!fs.existsSync(path.join(root, document))) continue;
  report.sourceSha256[document] = sha(document);
  for (const link of documentLinks(document)) {
    report.localLinks.push({...link, tracked: tracked.has(link.target)});
    if (!link.exists && initialMissing.has(link.target)) report.historicalBrokenLinks.push(link);
    check('LINK_PRESENT:' + document + ':' + link.line + ':' + link.target,
      link.exists && !link.invalidEncoding && !link.outsideRoot, {target: link.target, image: link.image, inheritedMissing: initialMissing.has(link.target)});
    check('LINK_PORTABLE:' + document + ':' + link.line + ':' + link.target,
      !link.absolute && !link.outsideRoot, {target: link.target, absolute: !!link.absolute});
    if (link.exists && !tracked.has(link.target)) report.publicationPending.push({document, line: link.line, target: link.target});
    if (requireTracked) check('LINK_TRACKED:' + document + ':' + link.line + ':' + link.target,
      tracked.has(link.target), {target: link.target, tracked: tracked.has(link.target)});
  }
}
check('ORIGINAL_COUNT_LOCKED_23', baseline.originalConcepts.length === 23, baseline.originalConcepts.length);
check('REFERENCE_MANIFEST_UNCHANGED', sha('verification/v1-qa/reference-manifest.json') === baseline.referenceManifestSha256, baseline.referenceManifestSha256);
const prd = read(prdPath), prdLinks = documentLinks(prdPath);
check('ORIGINAL_LINK_OCCURRENCES_MINIMUM_23', prdLinks.filter(x => x.image && x.target.startsWith('assets/aerosim-us-')).length >= 23, 'The same real-document condition is reported rather than aborting parser self-test before evidence output');
for (const item of baseline.originalConcepts) {
  const exists = fs.existsSync(path.join(root, item.file));
  check('ORIGINAL_BYTES:' + item.id, exists && sha(item.file) === item.sha256, item);
  check('ORIGINAL_EMBEDDED:' + item.id, prdLinks.some(x => x.image && x.target === item.file), item.file);
}
for (const item of baseline.preservedMavicFiles) {
  const exists = fs.existsSync(path.join(root, item.file));
  check('HISTORICAL_MAVIC_BYTES:' + item.file, exists && sha(item.file) === item.sha256, item);
}
for (const item of baseline.preservedCandidate08Screenshots || []) {
  const exists = fs.existsSync(path.join(root, item.file));
  check('EXISTING_CANDIDATE08_BYTES:' + item.file, exists && sha(item.file) === item.sha256, item);
}

const catalog = JSON.parse(read('assets/catalog-v125/dji-aircraft.json'));
const aircraft = catalog.aircraft.flatMap(x => [x, ...(x.variants || [])]);
check('CATALOG_REMAINS_PENDING', aircraft.every(x => x.appearanceStatus === 'pending_user' && x.review?.status === 'pending_user' && x.review?.approvedBy == null && x.review?.approvedAt == null),
  {count: aircraft.length, approved: aircraft.filter(x => x.appearanceStatus !== 'pending_user').map(x => x.id)});
check('CATALOG_PRODUCTION_GATE', catalog.approvalPolicy?.onlyHumanUserMayApprove === true && catalog.approvalPolicy?.unapprovedMayAppearInProductionFlight === false && aircraft.every(x => x.productionAssetAllowed !== true),
  catalog.approvalPolicy);
const manifestPaths = ['legacy', 'current', 'inspire', 'mavic'].map(x => 'assets/v125-art/' + x + '/review-drafts.json');
const drafts = manifestPaths.filter(x => fs.existsSync(path.join(root, x))).flatMap(file => {
  const raw = JSON.parse(read(file)); return (Array.isArray(raw) ? raw : raw.drafts).map(d => ({...d, manifest: file}));
});
check('DRAFT_IDS_UNIQUE', new Set(drafts.map(x => x.aircraftId)).size === drafts.length, drafts.map(x => x.aircraftId));
for (const draft of drafts) {
  const safe = /^assets\/v125-art\/[^/]+\/[a-z0-9-]+\.png$/.test(draft.image);
  check('DRAFT_HASH:' + draft.aircraftId, safe && fs.existsSync(path.join(root, draft.image)) && sha(draft.image) === draft.sha256,
    {image: draft.image, sha256: draft.sha256, revision: draft.revision});
  check('DRAFT_PENDING:' + draft.aircraftId, draft.appearanceStatus === 'pending_user' && draft.kind === 'generated_review_bitmap' && !!draft.revision, draft.appearanceStatus);
}
const oldMavic = drafts.find(x => x.aircraftId === 'dji-mavic-pro');
const newMavic = drafts.find(x => x.aircraftId === 'dji-mavic-4-pro');
check('MAVIC_2016_CURRENT_ID_REV_HASH_PRESERVED', !!oldMavic && ['aircraftId', 'image', 'revision', 'sha256', 'appearanceStatus'].every(key => oldMavic[key] === baseline.oldMavicCurrent[key]), oldMavic);
if (!prepare || newMavic) {
  check('MAVIC4_DISTINCT_CURRENT_DRAFT', !!newMavic && !!oldMavic && newMavic.image !== oldMavic.image && newMavic.sha256 !== oldMavic.sha256 && /mavic-4-pro/.test(newMavic.image), newMavic || 'No Mavic 4 Pro draft yet');
  check('MAVIC4_EMBEDDED_IN_PRD', !!newMavic && prdLinks.some(x => x.image && x.target === newMavic.image), newMavic?.image || null);
  check('MAVIC4_CORRECT_CATALOG_ID', aircraft.some(x => x.id === 'dji-mavic-4-pro' && /Mavic 4 Pro/i.test(x.englishName)), 'dji-mavic-4-pro');
  const html = read('AeroSim-1.25-机型逐一审核.html');
  const embedded = html.match(/<script id="draft-data" type="application\/json">([\s\S]*?)<\/script>/);
  const galleryDrafts = embedded ? JSON.parse(embedded[1]) : [];
  check('GALLERY_FROZEN_DRAFTS_MATCH', JSON.stringify(galleryDrafts.map(x => ({aircraftId:x.aircraftId, image:x.image, sha256:x.sha256, revision:x.revision, appearanceStatus:x.appearanceStatus}))) === JSON.stringify(drafts.map(x => ({aircraftId:x.aircraftId, image:x.image, sha256:x.sha256, revision:x.revision, appearanceStatus:x.appearanceStatus}))),
    {galleryDraftCount: galleryDrafts.length, manifestDraftCount: drafts.length});
  check('CURRENT_CONTRACT_AT_CHAPTER_ZERO', /^## 0[.． ]/m.test(prd), 'Structural assertion only; independent prose review required');
  check('CURRENT_VERSION_TARGET_125', /1\.25/.test(prd.split('\n').slice(0, 8).join('\n')), 'Header declares current target');
  check('THREE_DEVICE_LINES_NAMED', ['电脑', 'iPad', 'iPhone'].every(x => prd.includes(x)), 'Names do not prove hardware testing');
  check('ACTUAL_VERSION_NOT_UPGRADED_BY_DOC', /1\.0\.0/.test(prd) && /开发候选|开发版|开发中/.test(prd), 'Actual runtime evidence retained as 1.0.0 development candidate');
  check('REAL_DEVICE_BOUNDARY_RETAINED', /不是.*(?:iPhone|iPad).*真机|不替代.*(?:iPhone|iPad).*真机|真机.*未验证|尚未.*真机/m.test(prd), 'Only textual boundary; no native device test performed');
  check('PER_MODEL_EXPLICIT_USER_GATE_RETAINED', /pending_user/.test(prd) && /逐机型|每种机型|每一种机型/.test(prd) && /SHA|散列/.test(prd) && /自动化.*(?:不得|不能)|(?:不得|不能).*自动化/.test(prd), 'No ambiguous “basically correct” message is converted into an approval record');
}
report.summary = {checks: report.checks.length, failed: report.failures.length, localLinkOccurrences: report.localLinks.length,
  originalConcepts: baseline.originalConcepts.length, currentDrafts: drafts.length, inheritedMissingOccurrences: report.historicalBrokenLinks.length,
  publicationPendingUnique: new Set(report.publicationPending.map(x => x.target)).size,
  status: report.failures.length ? 'FAIL_DOCUMENT_QA' : prepare ? 'PREPARED_NOT_FINAL' : 'PASS_AUTOMATED_ONLY'};
console.log(JSON.stringify(report, null, 2));
process.exitCode = report.failures.length ? 1 : 0;
