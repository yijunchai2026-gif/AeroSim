// Independent QA: only synthetic approval records are exercised in isolated fixtures.
// This never approves real DJI rows or writes the working catalog/gallery.
import assert from 'node:assert/strict';
import crypto from 'node:crypto';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import {spawnSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';
import vm from 'node:vm';
import test from 'node:test';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const templatePath = path.join(root, 'assets/catalog-v125/review-template.html');
const builderPath = path.join(root, 'verification/v125-qa/build-catalog-review.mjs');
const digest = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
const image = Buffer.from('synthetic QA bitmap bytes; not a production image');
const official = 'https://www.dji.com/phantom';

function aircraft(id = 'qa-aircraft') {
  return {id, englishName: 'Synthetic QA Aircraft', chineseName: '合成测试机型', categoryId: 'qa-category', familyId: 'qa-family', introductionZh: '明确的合成 QA 介绍，不代表真实产品。', recordKind: 'aircraft_model', appearanceStatus: 'pending_user', review: {status: 'pending_user', approvedBy: null, approvedAt: null}, sourceUrls: [official], releaseYear: 2013, releaseYearEvidenceUrl: official};
}
function catalog(rows = [aircraft()]) {
  return {schemaVersion: 1, asOf: '2026-10-09', categories: [{id: 'qa-category', labelZh: '测试产品线'}], families: [{id: 'qa-family', labelZh: '测试系列'}], aircraft: rows, coverageGaps: ['合成 QA 数据，不宣称目录完整。']};
}
function draft(id = 'qa-aircraft') {
  return {aircraftId: id, kind: 'generated_review_bitmap', image: 'assets/v125-art/legacy/qa-aircraft.png', sha256: digest(image), revision: 'qa-r01', appearanceStatus: 'pending_user', sourceUrls: [official], notes: ['Synthetic fixture only']};
}
function generateFixture({data = catalog(), drafts = [draft()], bitmap = image, namespace = 'legacy'} = {}) {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'aerosim-catalog-qa-'));
  try {
    for (const rel of ['verification/v125-qa', 'assets/catalog-v125', 'assets/v125-art/' + namespace]) fs.mkdirSync(path.join(dir, rel), {recursive: true});
    fs.copyFileSync(builderPath, path.join(dir, 'verification/v125-qa/build-catalog-review.mjs'));
    fs.copyFileSync(templatePath, path.join(dir, 'assets/catalog-v125/review-template.html'));
    fs.writeFileSync(path.join(dir, 'assets/catalog-v125/dji-aircraft.json'), JSON.stringify(data));
    fs.writeFileSync(path.join(dir, 'assets/v125-art/' + namespace + '/review-drafts.json'), JSON.stringify(drafts));
    fs.writeFileSync(path.join(dir, 'assets/v125-art/' + namespace + '/qa-aircraft.png'), bitmap);
    const result = spawnSync(process.execPath, [path.join(dir, 'verification/v125-qa/build-catalog-review.mjs')], {encoding: 'utf8', timeout: 10000});
    const file = path.join(dir, 'AeroSim-1.25-机型逐一审核.html');
    return {...result, html: fs.existsSync(file) ? fs.readFileSync(file, 'utf8') : ''};
  } finally { fs.rmSync(dir, {recursive: true, force: true}); }
}
function generateWorkspaceFixture() {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'aerosim-real-catalog-qa-'));
  try {
    fs.mkdirSync(path.join(dir, 'verification/v125-qa'), {recursive: true});
    fs.mkdirSync(path.join(dir, 'assets/catalog-v125'), {recursive: true});
    for (const rel of ['verification/v125-qa/build-catalog-review.mjs', 'assets/catalog-v125/review-template.html', 'assets/catalog-v125/dji-aircraft.json']) fs.copyFileSync(path.join(root, rel), path.join(dir, rel));
    fs.cpSync(path.join(root, 'assets/v125-art'), path.join(dir, 'assets/v125-art'), {recursive: true});
    const result = spawnSync(process.execPath, [path.join(dir, 'verification/v125-qa/build-catalog-review.mjs')], {encoding: 'utf8', timeout: 10000});
    const file = path.join(dir, 'AeroSim-1.25-机型逐一审核.html');
    return {...result, html: fs.existsSync(file) ? fs.readFileSync(file, 'utf8') : ''};
  } finally { fs.rmSync(dir, {recursive: true, force: true}); }
}

class Element {
  constructor(tag, registry) { this.tagName = tag; this.registry = registry; this.children = []; this.events = {}; this.dataset = {}; this.attributes = {}; this.value = ''; this.disabled = false; this.textContent = ''; }
  set id(value) { this._id = value; this.registry.set(value, this); }
  get id() { return this._id; }
  append(...nodes) { this.children.push(...nodes); }
  replaceChildren(...nodes) { this.children = [...nodes]; }
  setAttribute(k, v) { this.attributes[k] = v; }
  addEventListener(type, callback) { (this.events[type] ||= []).push(callback); }
  click() { if (!this.disabled) for (const f of this.events.click || []) f({target: this}); }
  focus() { this.focused = true; }
}

function runtime({data = catalog([aircraft(), aircraft('qa-other')]), drafts = [draft()], saved = {}} = {}) {
  const registry = new Map(), storage = new Map([['aerosim-v125-review-v1', JSON.stringify(saved)]]), blobs = [];
  const ids = ['catalog-data', 'draft-data', 'build-data', 'search', 'category', 'filter', 'stats', 'feedback', 'list', 'count', 'detail', 'export', 'asof', 'coverage'];
  for (const id of ids) { const n = new Element('div', registry); n.id = id; }
  registry.get('catalog-data').textContent = JSON.stringify(data);
  registry.get('draft-data').textContent = JSON.stringify(drafts);
  registry.get('build-data').textContent = JSON.stringify({catalogSha256: 'qa-catalog-hash'});
  registry.get('category').value = registry.get('filter').value = 'all';
  const URLShim = class extends URL {};
  URLShim.createObjectURL = b => { blobs.push(b); return 'blob:synthetic-qa'; };
  URLShim.revokeObjectURL = () => {};
  const context = vm.createContext({document: {getElementById: id => registry.get(id), createElement: tag => new Element(tag, registry)}, localStorage: {getItem: k => storage.get(k) || null, setItem: (k, v) => storage.set(k, v)}, URL: URLShim, Blob, Date, setTimeout: () => {}});
  const script = fs.readFileSync(templatePath, 'utf8').match(/<script>\s*([\s\S]*?)<\/script>/)[1];
  vm.runInContext(script + '\n globalThis.__qa = {record, state, safeSource, renderList, renderDetail, getSaved:()=>saved, select:id=>{selected=id;renderList();renderDetail();}};', context);
  return {qa: context.__qa, registry, storage, blobs, data};
}
function savedRecord(id = 'qa-aircraft', changes = {}) {
  return {aircraftId: id, status: 'approved', note: 'Synthetic QA record', revision: 'qa-r01', imageSha256: digest(image), catalogSha256: 'qa-catalog-hash', recordedAt: '2026-10-09T00:00:00Z', reviewSource: 'local_user_review', engineeringAcceptance: 'pending_root_verification', ...changes};
}

test('generator accepts pending synthetic catalog and hashes image', () => {
  const r = generateFixture(); assert.equal(r.status, 0, r.stderr);
  assert.match(r.html, /qa-aircraft/); assert.ok(!r.html.includes('__CATALOG_DATA__'));
});
test('research schema version and asOfDate map to the review schema', () => {
  const data = catalog(); data.schemaVersion = '1.0.0'; data.asOfDate = data.asOf; delete data.asOf;
  const r = generateFixture({data}); assert.equal(r.status, 0, r.stderr);
});
test('duplicate aircraft IDs reject generation', () => {
  const r = generateFixture({data: catalog([aircraft(), aircraft()])}); assert.notEqual(r.status, 0); assert.equal(r.html, '');
});
test('seed approval rejects generation', () => {
  const a = aircraft(); a.review.status = 'approved'; assert.notEqual(generateFixture({data: catalog([a])}).status, 0);
});
test('missing official sources reject generation', () => {
  const a = aircraft(); a.sourceUrls = []; assert.notEqual(generateFixture({data: catalog([a])}).status, 0);
});
test('untrusted domains and HTTP official links reject generation', () => {
  for (const u of ['https://dji.com.evil.test/model', 'https://evildji.com/model', 'http://www.dji.com/model', 'javascript:alert(1)']) {
    const a = aircraft(); a.sourceUrls = [u]; assert.notEqual(generateFixture({data: catalog([a])}).status, 0, u);
  }
});
test('release year without official evidence rejects generation', () => {
  const a = aircraft(); a.releaseYearEvidenceUrl = 'https://example.test/year'; assert.notEqual(generateFixture({data: catalog([a])}).status, 0);
});
test('unknown and duplicate draft aircraft IDs reject generation', () => {
  for (const rows of [[draft('qa-unknown')], [draft(), draft()]]) assert.notEqual(generateFixture({drafts: rows}).status, 0);
});
test('changed image bytes reject recorded draft hash', () => {
  assert.notEqual(generateFixture({bitmap: Buffer.from('changed pixels')}).status, 0);
});
test('draft traversal and external image URLs reject generation', () => {
  for (const imagePath of ['../private.png', 'https://example.test/drone.png', 'assets/v125-art/legacy/../../private.png']) {
    assert.notEqual(generateFixture({drafts: [{...draft(), image: imagePath}]}).status, 0);
  }
});
test('inline data escapes script closing tags and unicode separators', () => {
  const a = aircraft(); a.introductionZh = '</script><script>alert(1)</script>\u2028\u2029';
  const r = generateFixture({data: catalog([a])}); assert.equal(r.status, 0, r.stderr);
  assert.ok(!r.html.includes('</script><script>alert(1)'));
  assert.ok(r.html.includes('\\u003c/script>'));
  assert.ok(r.html.includes('\\u2028\\u2029'));
});
test('all fixture models begin pending and no draft disables approval', () => {
  const r = runtime({drafts: []}); assert.equal(r.qa.state(r.data.aircraft[0]), 'pending_user');
  assert.equal(r.registry.get('approve-model').disabled, true); r.registry.get('approve-model').click();
  assert.equal(Object.keys(r.qa.getSaved()).length, 0);
});
test('held release evidence cannot be approved by the UI', () => {
  const a = aircraft(); a.recordKind = 'hold_release_evidence'; const r = runtime({data: catalog([a])});
  assert.equal(r.registry.get('approve-model').disabled, true); r.registry.get('approve-model').click();
  assert.equal(r.qa.state(a), 'pending_user');
});
test('catalog cutoffStatus hold also disables approval with a draft', () => {
  const a = aircraft(); a.cutoffStatus = 'hold_release_evidence'; const r = runtime({data: catalog([a])});
  assert.equal(r.registry.get('approve-model').disabled, true); r.registry.get('approve-model').click();
  assert.equal(r.qa.state(a), 'pending_user');
});
test('changed image hash, revision or catalog invalidate prior confirmation', () => {
  for (const changes of [{imageSha256: 'changed'}, {revision: 'qa-r02'}, {catalogSha256: 'changed'}]) {
    const r = runtime({saved: {'qa-aircraft': savedRecord('qa-aircraft', changes)}});
    assert.equal(r.qa.state(r.data.aircraft[0]), 'pending_user');
  }
});
test('cross-aircraft cached confirmation is rejected', () => {
  const r = runtime({saved: {'qa-aircraft': savedRecord('qa-other')}});
  assert.equal(r.qa.state(r.data.aircraft[0]), 'pending_user');
});
test('cached automation or engineering-accepted claims are not valid user records', () => {
  for (const changes of [{reviewSource: 'automation'}, {engineeringAcceptance: 'approved'}]) {
    const r = runtime({saved: {'qa-aircraft': savedRecord('qa-aircraft', changes)}});
    assert.equal(r.qa.state(r.data.aircraft[0]), 'pending_user');
  }
});
test('held release evidence also rejects cached approval', () => {
  const a = aircraft(); a.recordKind = 'hold_release_evidence';
  const r = runtime({data: catalog([a]), saved: {'qa-aircraft': savedRecord()}});
  assert.equal(r.qa.state(a), 'pending_user');
});
test('individual synthetic click does not batch approve other aircraft', () => {
  const r = runtime(); r.registry.get('approve-model').click();
  assert.equal(r.qa.state(r.data.aircraft[0]), 'approved'); assert.equal(r.qa.state(r.data.aircraft[1]), 'pending_user');
  assert.equal(Object.keys(r.qa.getSaved()).length, 1);
});
test('request changes requires notes; reset returns to pending', () => {
  const r = runtime(); r.registry.get('request-changes').click(); assert.equal(r.qa.state(r.data.aircraft[0]), 'pending_user');
  r.registry.get('review-note').value = 'Synthetic camera correction'; r.registry.get('request-changes').click();
  assert.equal(r.qa.state(r.data.aircraft[0]), 'changes_requested'); r.registry.get('pending-model').click();
  assert.equal(r.qa.state(r.data.aircraft[0]), 'pending_user');
});
test('export remains engineering-pending and binds concrete ID/revision/hash', async () => {
  const r = runtime(); r.registry.get('approve-model').click(); r.registry.get('export').click();
  const out = JSON.parse(await r.blobs[0].text()); assert.equal(out.engineeringAcceptance, 'pending_root_verification');
  assert.equal(out.records.length, 1); const row = out.records[0];
  assert.equal(row.aircraftId, 'qa-aircraft'); assert.equal(row.revision, 'qa-r01');
  assert.equal(row.imageSha256, digest(image)); assert.equal(row.engineeringAcceptance, 'pending_root_verification');
  assert.equal(row.reviewSource, 'local_user_review'); assert.ok(row.recordedAt);
});
test('source links permit official HTTPS and reject executable/untrusted URLs', () => {
  const r = runtime(); for (const u of [official, 'https://dl.djicdn.com/manual.pdf', 'https://se-cdn.djiits.com/manual.pdf', 'https://download.dji-innovations.com/legacy.pdf', 'https://www.ryzerobotics.com/tello']) assert.equal(r.qa.safeSource(u), true, u);
  for (const u of ['javascript:alert(1)', 'file:///private/secret', 'https://dji.com.evil.test/a']) assert.equal(r.qa.safeSource(u), false, u);
});
test('UI uses textContent and has no batch-approval or evaluation endpoint', () => {
  const source = fs.readFileSync(templatePath, 'utf8');
  assert.ok(!/\.innerHTML\s*=|eval\(|new Function\(|fetch\(/.test(source));
  assert.ok(!/id=["'](?:approve-all|bulk-approve)["']/.test(source));
});
test('real directory retains unique IDs, official sources, pending seed and explicit gaps', {skip: !fs.existsSync(path.join(root, 'assets/catalog-v125/dji-aircraft.json'))}, () => {
  const data = JSON.parse(fs.readFileSync(path.join(root, 'assets/catalog-v125/dji-aircraft.json'), 'utf8'));
  const ids = new Set(); const r = runtime({data, drafts: []});
  for (const a of data.aircraft) {
    assert.ok(!ids.has(a.id), a.id); ids.add(a.id); assert.equal(a.appearanceStatus, 'pending_user', a.id);
    assert.equal(a.review.status, 'pending_user', a.id); assert.ok(a.sourceUrls.length, a.id);
    assert.ok(a.sourceUrls.every(r.qa.safeSource), a.id);
  }
  assert.ok(data.coverageGaps.length > 0, 'open coverage limitations must remain explicit');
});
test('actual catalog and current art manifests generate in an isolated copy', {skip: !fs.existsSync(path.join(root, 'assets/catalog-v125/dji-aircraft.json'))}, () => {
  const r = generateWorkspaceFixture(); assert.equal(r.status, 0, r.stderr);
  const inline = id => JSON.parse(r.html.match(new RegExp('<script id="' + id + '" type="application/json">([\\s\\S]*?)<\\/script>'))[1]);
  const data = inline('catalog-data'), drafts = inline('draft-data'), build = inline('build-data');
  assert.equal(data.asOf || data.asOfDate, '2026-10-09');
  assert.equal(build.catalogSha256, digest(fs.readFileSync(path.join(root, 'assets/catalog-v125/dji-aircraft.json'))));
  const source = JSON.parse(fs.readFileSync(path.join(root, 'assets/catalog-v125/dji-aircraft.json'), 'utf8'));
  assert.equal(data.aircraft.length, source.aircraft.reduce((n, a) => n + 1 + (a.variants || []).length, 0));
  assert.equal(data.reviewBaseCount, source.aircraft.length);
  assert.equal(new Set(data.aircraft.map(a => a.id)).size, data.aircraft.length);
  assert.ok(data.aircraft.every(a => a.appearanceStatus === 'pending_user' && a.review.status === 'pending_user'));
  const ids = new Set(data.aircraft.map(a => a.id));
  for (const d of drafts) { assert.ok(ids.has(d.aircraftId), d.aircraftId); assert.equal(d.appearanceStatus, 'pending_user'); }
  assert.ok(!r.html.includes('__CATALOG_DATA__')); assert.ok(!r.html.includes('__DRAFT_DATA__'));
});

// Additive second-batch gates. Preserve the original 25 tests above unchanged.
const expectedArtIds = {
  legacy: ['dji-phantom-1'],
  current: ['dji-mini-4-pro'],
  inspire: ['dji-inspire-1'],
  mavic: ['dji-mavic-pro', 'dji-mavic-4-pro'],
  phantom4: ['dji-phantom-4'],
  air2s: ['dji-air-2s']
};
const expectedCurrentIds = Object.values(expectedArtIds).flat();
test('six real art namespaces bind exact aircraft IDs, current PNG bytes and pending revisions', () => {
  const source = JSON.parse(fs.readFileSync(path.join(root, 'assets/catalog-v125/dji-aircraft.json'), 'utf8'));
  const realIds = new Set(source.aircraft.flatMap(a => [a.id, ...(a.variants || []).map(v => v.id)]));
  const sources = runtime({data: source, drafts: []});
  const seen = new Set();
  for (const [namespace, expectedIds] of Object.entries(expectedArtIds)) {
    const manifest = JSON.parse(fs.readFileSync(path.join(root, `assets/v125-art/${namespace}/review-drafts.json`), 'utf8'));
    const rows = Array.isArray(manifest) ? manifest : manifest.drafts;
    assert.equal(rows.length, expectedIds.length, namespace + ' must contain exactly the frozen review entries');
    assert.deepEqual(rows.map(d => d.aircraftId).sort(), [...expectedIds].sort(), namespace);
    for (const d of rows) {
      const id = d.aircraftId; assert.ok(realIds.has(id));
      assert.ok(!seen.has(id)); seen.add(id);
      assert.match(d.image, new RegExp(`^assets/v125-art/${namespace}/[a-z0-9-]+\\.png$`));
      assert.equal(d.sha256, digest(fs.readFileSync(path.join(root, d.image))), id);
      assert.equal(d.appearanceStatus, 'pending_user', id); assert.equal(d.kind, 'generated_review_bitmap');
      assert.ok(d.revision && d.sourceUrls.length); assert.ok(d.sourceUrls.every(sources.qa.safeSource), id);
    }
  }
  assert.equal(seen.size, 7);
});
test('isolated real gallery contains 132 main plus 9 variants and exactly seven pending art IDs', () => {
  const r = generateWorkspaceFixture(); assert.equal(r.status, 0, r.stderr);
  const inline = id => JSON.parse(r.html.match(new RegExp('<script id="' + id + '" type="application/json">([\\s\\S]*?)<\\/script>'))[1]);
  const data = inline('catalog-data'), drafts = inline('draft-data');
  assert.equal(data.reviewBaseCount, 132); assert.equal(data.reviewVariantCount, 9); assert.equal(data.aircraft.length, 141);
  assert.equal(drafts.length, 7);
  assert.deepEqual(drafts.map(d => d.aircraftId).sort(), [...expectedCurrentIds].sort());
  assert.deepEqual([...new Set(drafts.map(d => d.image.split('/')[2]))].sort(), Object.keys(expectedArtIds).sort());
  const review = runtime({data, drafts});
  assert.ok(data.aircraft.every(a => review.qa.state(a) === 'pending_user'));
  assert.equal(Object.keys(review.qa.getSaved()).length, 0, 'QA must not create real aircraft approval records');
  for (const id of expectedCurrentIds) {
    review.qa.select(id); assert.equal(review.registry.get('approve-model').disabled, false, id);
  }
  const withoutDraft = data.aircraft.filter(a => !expectedCurrentIds.includes(a.id));
  assert.equal(withoutDraft.length, 134);
  for (const a of withoutDraft) {
    review.qa.select(a.id); assert.equal(review.registry.get('approve-model').disabled, true, a.id);
  }
  assert.equal(Object.keys(review.qa.getSaved()).length, 0);
});

test('Mavic 4 Pro draft is separate from the preserved 2016 Mavic Pro row', () => {
  const rows = JSON.parse(fs.readFileSync(path.join(root, 'assets/v125-art/mavic/review-drafts.json'), 'utf8'));
  const original = rows.find(d => d.aircraftId === 'dji-mavic-pro');
  const modern = rows.find(d => d.aircraftId === 'dji-mavic-4-pro');
  assert.equal(original.revision, 'draft-02');
  assert.equal(original.sha256, 'ace4e4d0edad8555ac726a8b2c674c4a7513201cc8c01bcac8927fd12c462931');
  assert.equal(modern.revision, 'draft-01');
  assert.equal(modern.image, 'assets/v125-art/mavic/mavic-4-pro-draft-01.png');
  assert.equal(modern.sha256, digest(fs.readFileSync(path.join(root, modern.image))));
  assert.notEqual(modern.image, original.image); assert.notEqual(modern.sha256, original.sha256);
  assert.equal(modern.appearanceStatus, 'pending_user');
});
test('synthetic original-Mavic decision cannot approve Mavic 4 Pro or its 512GB variant', () => {
  // Memory-only synthetic records with deliberately mismatched aircraftId; not user approvals.
  const modern = {...draft('dji-mavic-4-pro'), revision: 'qa-modern'};
  const ids = ['dji-mavic-pro', 'dji-mavic-4-pro', 'dji-mavic-4-pro-512gb'];
  const data = catalog(ids.map(id => aircraft(id)));
  const old = savedRecord('dji-mavic-pro', {revision: 'qa-modern'});
  const r = runtime({data, drafts: [modern], saved: {'dji-mavic-4-pro': old, 'dji-mavic-4-pro-512gb': old}});
  for (const a of data.aircraft) assert.equal(r.qa.state(a), 'pending_user', a.id);
  r.qa.select('dji-mavic-4-pro-512gb'); assert.equal(r.registry.get('approve-model').disabled, true);
});
test('two additional official CDN hosts are exact HTTPS allowlist entries, not wildcard domains', () => {
  const r = runtime();
  for (const url of ['https://www1.djicdn.com/reference.png', 'https://www4.djicdn.com/reference.jpg']) assert.equal(r.qa.safeSource(url), true, url);
  for (const url of ['http://www1.djicdn.com/reference.png', 'ftp://www4.djicdn.com/reference.jpg', 'https://www1.djicdn.com.evil.test/p.png', 'https://evildjicdn.com/p.png', 'https://www2.djicdn.com/p.png', 'https://www3.djicdn.com/p.png', 'https://www4-djicdn.com/p.png', 'javascript:alert(1)']) {
    assert.equal(r.qa.safeSource(url), false, url);
    const a = aircraft(); a.sourceUrls = [url]; assert.notEqual(generateFixture({data: catalog([a])}).status, 0, url);
  }
  for (const url of ['https://www1.djicdn.com/reference.png', 'https://www4.djicdn.com/reference.jpg']) {
    const a = aircraft(); a.sourceUrls = [url]; const result = generateFixture({data: catalog([a])}); assert.equal(result.status, 0, result.stderr);
  }
});

// Additive registry/art wave. Original rule coverage remains; new current counts
// reflect the two additional pending drafts, never an automatic approval.
for (const [namespace, id] of [['phantom4', 'dji-phantom-4'], ['air2s', 'dji-air-2s']]) {
  test(namespace + ' accepts only its exact aircraft and matching local image namespace', () => {
    const d = {...draft(id), image: 'assets/v125-art/' + namespace + '/qa-aircraft.png'};
    const data = catalog([aircraft(id)]);
    const valid = generateFixture({namespace, data, drafts:[d]});
    assert.equal(valid.status, 0, valid.stderr);
    assert.notEqual(generateFixture({namespace, data:catalog([aircraft('qa-other')]), drafts:[{...d, aircraftId:'qa-other'}]}).status, 0);
    assert.notEqual(generateFixture({namespace, data, drafts:[{...d, image:'assets/v125-art/legacy/qa-aircraft.png'}]}).status, 0);
  });
  test(namespace + ' preserves hash and pending-user gates', () => {
    const d = {...draft(id), image:'assets/v125-art/' + namespace + '/qa-aircraft.png'};
    const data = catalog([aircraft(id)]);
    assert.notEqual(generateFixture({namespace, data, drafts:[d], bitmap:Buffer.from('changed bytes')}).status, 0);
    assert.notEqual(generateFixture({namespace, data, drafts:[{...d, appearanceStatus:'approved'}]}).status, 0);
    const held = {...aircraft(id), cutoffStatus:'hold_release_evidence'};
    const r = runtime({data:catalog([held]), drafts:[d]});
    r.registry.get('approve-model').click();
    assert.equal(r.qa.state(held), 'pending_user');
    assert.equal(Object.keys(r.qa.getSaved()).length, 0);
  });
}
