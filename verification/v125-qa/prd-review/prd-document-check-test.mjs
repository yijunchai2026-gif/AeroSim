// Isolated synthetic fixtures test the checker, never modify or approve repository art.
import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import crypto from 'node:crypto';
import {spawnSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';

const checker = path.join(path.dirname(fileURLToPath(import.meta.url)), 'prd-document-check.mjs');
const digest = b => crypto.createHash('sha256').update(b).digest('hex');
function fixture(mutate = () => {}) {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'aerosim-prd-checker-qa-'));
  const write = (file, data) => { fs.mkdirSync(path.dirname(path.join(dir, file)), {recursive:true}); fs.writeFileSync(path.join(dir, file), typeof data === 'object' && !Buffer.isBuffer(data) ? JSON.stringify(data) : data); };
  try {
    fs.mkdirSync(path.join(dir, 'verification/v125-qa/prd-review'), {recursive:true});
    fs.copyFileSync(checker, path.join(dir, 'verification/v125-qa/prd-review/prd-document-check.mjs'));
    const originals = Array.from({length:23}, (_, i) => {
      const file = 'assets/aerosim-us-' + String(i + 1).padStart(2, '0') + '-synthetic.png';
      const bytes = Buffer.from('synthetic reference fixture ' + i); write(file, bytes);
      return {id:String(i + 1), file, sha256:digest(bytes)};
    });
    const reference = {screens:originals}; write('verification/v1-qa/reference-manifest.json', reference);
    const oldFile = 'assets/v125-art/mavic/mavic-pro-draft-02.png', newFile = 'assets/v125-art/mavic/mavic-4-pro-draft-01.png';
    const oldBytes = Buffer.from('synthetic 2016 bitmap bytes, not real art'), newBytes = Buffer.from('synthetic 2025 bitmap bytes, not real art');
    write(oldFile, oldBytes); write(newFile, newBytes);
    const old = {aircraftId:'dji-mavic-pro', image:oldFile, revision:'draft-02', sha256:digest(oldBytes), appearanceStatus:'pending_user', kind:'generated_review_bitmap'};
    const modern = {aircraftId:'dji-mavic-4-pro', image:newFile, revision:'draft-01', sha256:digest(newBytes), appearanceStatus:'pending_user', kind:'generated_review_bitmap'};
    const drafts = [old, modern];
    write('assets/v125-art/mavic/review-drafts.json', drafts);
    const catalog = {approvalPolicy:{onlyHumanUserMayApprove:true, unapprovedMayAppearInProductionFlight:false}, aircraft:[
      {id:'dji-mavic-pro', englishName:'Mavic Pro', appearanceStatus:'pending_user', review:{status:'pending_user'}, productionAssetAllowed:false},
      {id:'dji-mavic-4-pro', englishName:'Mavic 4 Pro', appearanceStatus:'pending_user', review:{status:'pending_user'}, productionAssetAllowed:false}
    ]};
    write('assets/catalog-v125/dji-aircraft.json', catalog);
    const baseline = {originalConcepts:originals, referenceManifestSha256:digest(fs.readFileSync(path.join(dir,'verification/v1-qa/reference-manifest.json'))),
      preservedMavicFiles:[{file:oldFile, sha256:old.sha256}], oldMavicCurrent:{...old}, initialPrdMissingLinks:[]};
    write('verification/v125-qa/prd-review/baseline.json', baseline);
    const prd = '# AeroSim 1.25 target\n## 0. Current contract\n电脑 iPad iPhone\nActual 1.0.0 开发候选；不是iPhone/iPad真机。\n自动化不得确认：逐机型 pending_user SHA。\n' +
      originals.map(x => '![synthetic](' + x.file + ')').join('\n') + '\n![Mavic 4 synthetic](' + newFile + ')\n';
    write('飞行模拟器产品设计文档.md', prd);
    write('AeroSim-1.25-机型逐一审核.html', '<script id="draft-data" type="application/json">' + JSON.stringify(drafts) + '</script>');
    mutate({dir, write, originals, drafts, catalog, baseline, prd, oldFile, newFile});
    const result = spawnSync(process.execPath, [path.join(dir,'verification/v125-qa/prd-review/prd-document-check.mjs')], {encoding:'utf8', timeout:10000});
    return {exit:result.status, report:JSON.parse(result.stdout), stderr:result.stderr};
  } finally { fs.rmSync(dir, {recursive:true, force:true}); }
}
function rejects(prefix, mutate) {
  const r = fixture(mutate); assert.notEqual(r.exit, 0); assert.ok(r.report.failures.some(x => x.id.startsWith(prefix)), JSON.stringify(r.report.failures));
}
test('synthetic valid 23-reference document passes without user approval', () => {
  const r = fixture(); assert.equal(r.exit, 0, r.stderr); assert.equal(r.report.summary.status, 'PASS_AUTOMATED_ONLY');
});
test('changed reference bytes are rejected', () => rejects('ORIGINAL_BYTES:', ({write, originals}) => write(originals[0].file, 'changed synthetic bytes')));
test('removed original embed is rejected and reports evidence', () => rejects('ORIGINAL_EMBEDDED:', ({write, prd, originals}) => write('飞行模拟器产品设计文档.md', prd.replace('![synthetic](' + originals[0].file + ')', ''))));
test('missing local file link is rejected', () => rejects('LINK_PRESENT:', ({write, prd}) => write('飞行模拟器产品设计文档.md', prd + '\n[missing](docs/missing.md)')));
test('existing absolute local link remains nonportable', () => rejects('LINK_PORTABLE:', ({dir, write, prd}) => write('飞行模拟器产品设计文档.md', prd + '\n[absolute](<' + path.join(dir,'verification/v1-qa/reference-manifest.json') + '>)')));
test('2016 model swapped to Mavic 4 ID cannot replace the historical binding', () => rejects('MAVIC_2016_CURRENT_ID_REV_HASH_PRESERVED', ({write, drafts}) => {
  drafts[0].aircraftId = 'dji-mavic-4-pro'; write('assets/v125-art/mavic/review-drafts.json', drafts);
}));
test('new Mavic 4 draft cannot reuse old Mavic Pro image', () => rejects('MAVIC4_DISTINCT_CURRENT_DRAFT', ({write, drafts}) => {
  drafts[1].image = drafts[0].image; drafts[1].sha256 = drafts[0].sha256; write('assets/v125-art/mavic/review-drafts.json', drafts);
}));
test('Mavic 4 altered bitmap bytes reject recorded SHA', () => rejects('DRAFT_HASH:dji-mavic-4-pro', ({write, newFile}) => write(newFile, 'changed modern synthetic bitmap')));
test('ambiguous feedback must not cause seeded approval', () => rejects('CATALOG_REMAINS_PENDING', ({write, catalog}) => {
  catalog.aircraft[1].appearanceStatus = 'approved'; catalog.aircraft[1].review = {status:'approved', approvedBy:'automation converted basically correct'};
  write('assets/catalog-v125/dji-aircraft.json', catalog);
}));
test('gallery containing stale revision is rejected', () => rejects('GALLERY_FROZEN_DRAFTS_MATCH', ({write, drafts}) => {
  drafts[1].revision = 'stale'; write('AeroSim-1.25-机型逐一审核.html','<script id="draft-data" type="application/json">'+JSON.stringify(drafts)+'</script>');
}));
