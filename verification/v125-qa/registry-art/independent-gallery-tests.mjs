// Independent wave QA. All negative/approval-cache fixtures are memory-only or
// isolated temporary copies. This script never changes real review records/art.
import assert from 'node:assert/strict';
import crypto from 'node:crypto';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import {spawnSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';
import vm from 'node:vm';
import test from 'node:test';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../..');
const builder = 'verification/v125-qa/build-catalog-review.mjs';
const template = 'assets/catalog-v125/review-template.html';
const namespaces = [['phantom4', 'dji-phantom-4'], ['air2s', 'dji-air-2s']];
const digest = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
const bytes = Buffer.from('Independent synthetic fixture only; not a PNG or DJI appearance');
const official = 'https://www.dji.com/qa-fixture';
const read = file => fs.readFileSync(path.join(root, file), 'utf8');
const fileSha = file => digest(fs.readFileSync(path.join(root, file)));
function row(id) {
  return {id, englishName:'Synthetic independent QA ' + id, introductionZh:'纯合成 QA，不是实际机型确认。', categoryId:'qa-line', familyId:'qa-family', appearanceStatus:'pending_user', review:{status:'pending_user', approvedBy:null, approvedAt:null}, sourceUrls:[official]};
}
function catalog(rows) {
  return {schemaVersion:1, asOf:'2026-10-09', categories:[{id:'qa-line'}], families:[{id:'qa-family'}], aircraft:rows, coverageGaps:['Synthetic, deliberately incomplete']};
}
function draft(namespace, id) {
  return {aircraftId:id, image:'assets/v125-art/' + namespace + '/qa-independent.png', revision:'qa-independent-r2', sha256:digest(bytes), kind:'generated_review_bitmap', appearanceStatus:'pending_user', sourceUrls:[official]};
}
function isolated(namespace, id, mutate = () => {}) {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'aerosim-independent-art-'));
  const write = (file, value) => {
    const target = path.join(dir, file); fs.mkdirSync(path.dirname(target), {recursive:true});
    fs.writeFileSync(target, Buffer.isBuffer(value) || typeof value === 'string' ? value : JSON.stringify(value));
  };
  try {
    write(builder, read(builder)); write(template, read(template));
    const data = catalog([row(id)]), art = draft(namespace, id);
    write(art.image, bytes);
    const fixture = {dir, write, data, art, namespace, id}; mutate(fixture);
    write('assets/catalog-v125/dji-aircraft.json', data);
    write('assets/v125-art/' + namespace + '/review-drafts.json', fixture.drafts || [art]);
    const result = spawnSync(process.execPath, [path.join(dir, builder)], {encoding:'utf8', timeout:10000});
    const output = path.join(dir, 'AeroSim-1.25-机型逐一审核.html');
    return {status:result.status, stderr:result.stderr, html:fs.existsSync(output) ? fs.readFileSync(output, 'utf8') : ''};
  } finally { fs.rmSync(dir, {recursive:true, force:true}); }
}
function reject(result, label) {
  assert.notEqual(result.status, 0, label); assert.equal(result.html, '', label + ': rejected input must not publish a new gallery');
}

// Minimal event/DOM shim: checks program logic, NOT a browser, layout, security
// origin or real user consent. Deliberately separate from the author's test shim.
class FakeNode {
  constructor(nodes) { this.nodes=nodes; this.children=[]; this.listeners={}; this.dataset={}; this.value=''; this.textContent=''; this.disabled=false; }
  set id(id) { this.identifier=id; this.nodes.set(id,this); }
  get id() { return this.identifier; }
  append(...items) { this.children.push(...items); }
  replaceChildren(...items) { this.children=items; }
  setAttribute() {}
  addEventListener(event, fn) { (this.listeners[event] ||= []).push(fn); }
  click() { if (!this.disabled) for (const fn of this.listeners.click || []) fn(); }
  focus() {}
}
function logic(data, art, saved = {}) {
  const nodes = new Map(), stores = new Map([['aerosim-v125-review-v1', JSON.stringify(saved)]]);
  for (const id of ['catalog-data','draft-data','build-data','search','category','filter','stats','feedback','list','count','detail','export','asof','coverage']) { const n=new FakeNode(nodes); n.id=id; }
  nodes.get('catalog-data').textContent=JSON.stringify(data); nodes.get('draft-data').textContent=JSON.stringify(art);
  nodes.get('build-data').textContent=JSON.stringify({catalogSha256:'synthetic-catalog-r2'});
  nodes.get('category').value=nodes.get('filter').value='all';
  const context=vm.createContext({document:{getElementById:id=>nodes.get(id), createElement:()=>new FakeNode(nodes)}, localStorage:{getItem:key=>stores.get(key) || null, setItem:(key,value)=>stores.set(key,value)}, URL, Date, Blob, setTimeout:()=>{}});
  const program=read(template).match(/<script>\s*([\s\S]*?)<\/script>/)[1];
  vm.runInContext(program + '\nglobalThis.qaIndependent={state,record,select:id=>{selected=id;renderList();renderDetail();},saved:()=>saved};',context);
  return {api:context.qaIndependent,nodes,stores};
}
function record(id, art, changes = {}) {
  return {aircraftId:id, revision:art.revision, imageSha256:art.sha256, catalogSha256:'synthetic-catalog-r2', status:'approved', reviewSource:'local_user_review', engineeringAcceptance:'pending_root_verification', recordedAt:'2026-10-11T00:00:00Z', ...changes};
}

for (const [namespace,id] of namespaces) {
  test('synthetic ' + namespace + ': exact ID and namespace accepted without any approval', () => {
    const r=isolated(namespace,id); assert.equal(r.status,0,r.stderr);
    const inline=JSON.parse(r.html.match(/<script id="draft-data" type="application\/json">([\s\S]*?)<\/script>/)[1]);
    assert.equal(inline.length,1); assert.equal(inline[0].aircraftId,id); assert.equal(inline[0].appearanceStatus,'pending_user');
  });
  test('synthetic ' + namespace + ': known-but-wrong model and appearance variants rejected', () => {
    for (const other of ['dji-phantom-4-pro','dji-air-2','dji-air-2s-regional-qa']) reject(isolated(namespace,id, f=>{f.data.aircraft=[row(other)];f.art.aircraftId=other;}),other);
    reject(isolated(namespace,id,f=>{f.drafts=[f.art,{...f.art}];}),'duplicate draft');
  });
  test('synthetic ' + namespace + ': cross-namespace and path escapes rejected even when bytes exist', () => {
    const paths=['assets/v125-art/legacy/qa-independent.png','assets/v125-art/' + (namespace==='phantom4'?'air2s':'phantom4') + '/qa-independent.png',
      'assets/v125-art/' + namespace + '/../qa-independent.png','assets/v125-art/' + namespace + '/nested/qa-independent.png',
      'assets/v125-art/' + namespace + '/%2e%2e%2fqa-independent.png','assets/v125-art/' + namespace + '/qa-independent.png?x=1',
      'assets/v125-art/' + namespace + '/QA-INDEPENDENT.png','assets/v125-art/' + namespace + '/qa-independent.jpg',
      'assets\\v125-art\\' + namespace + '\\qa-independent.png','https://www.dji.com/qa-independent.png','file:///private/tmp/qa-independent.png','/private/tmp/qa-independent.png'];
    for (const image of paths) reject(isolated(namespace,id,f=>{if(image.startsWith('assets/'))f.write(image,bytes);f.art.image=image;}),image);
  });
  test('synthetic ' + namespace + ': missing/current/wrong/case-altered hash cannot be reused', () => {
    for (const hash of [null,'',digest(Buffer.from('another image')),digest(bytes).toUpperCase()]) reject(isolated(namespace,id,f=>{f.art.sha256=hash;}),String(hash));
    reject(isolated(namespace,id,f=>f.write(f.art.image,Buffer.from('changed after review'))),'changed bytes');
  });
  test('synthetic ' + namespace + ': pending marker, kind, revision and official-source requirement stay closed', () => {
    for (const changes of [{appearanceStatus:'approved'},{appearanceStatus:'changes_requested'},{appearanceStatus:null},{kind:'official_photo'},{kind:'production_mesh'},{revision:''},{revision:null},{sourceUrls:[]},{sourceUrls:null}])
      reject(isolated(namespace,id,f=>Object.assign(f.art,changes)),JSON.stringify(changes));
    reject(isolated(namespace,id,f=>{f.data.aircraft[0].review.approvedBy='synthetic automation';}),'seeded approvedBy');
  });
  test('synthetic ' + namespace + ': draft URLs reject executable, HTTP and official-host spoofs', () => {
    for (const url of ['http://www.dji.com/a','https://dji.com.evil.test/a','https://evildji.com/a','https://www1.djicdn.com.evil.test/a',
      'https://se-cdn.djiits.com.evil.test/a','https://www2.djicdn.com/a','https://www-cdn.djiits.com/a','javascript:alert(1)','file:///private/tmp/a','data:image/png;base64,a'])
      reject(isolated(namespace,id,f=>{f.art.sourceUrls=[url];}),url);
  });
  test('synthetic ' + namespace + ': copied old-model or regional approval never applies to this model', () => {
    const art=draft(namespace,id), target=row(id);
    for (const oldId of ['dji-phantom-1','dji-mavic-pro','dji-mavic-4-pro','dji-phantom-4-pro','dji-air-2',id+'-regional-qa']) {
      // Deliberately give the stale record current hash/revision/catalog, isolating ID binding.
      const r=logic(catalog([target]),[art],{[id]:record(oldId,art)});
      assert.equal(r.api.state(target),'pending_user',oldId); assert.equal(r.api.record(target),null,oldId);
    }
  });
  test('synthetic ' + namespace + ': stale same-ID approval or automation/engineering claim is ineffective', () => {
    const art=draft(namespace,id), target=row(id);
    const variants=[{revision:'qa-independent-r1'},{imageSha256:digest(Buffer.from('old bitmap'))},{catalogSha256:'old-catalog'},
      {reviewSource:'automation'},{reviewSource:'local-user-review'},{engineeringAcceptance:'approved'},{status:'production_accepted'},{aircraftId:null}];
    for (const changes of variants) {
      const r=logic(catalog([target]),[art],{[id]:record(id,art,changes)}); assert.equal(r.api.state(target),'pending_user',JSON.stringify(changes));
    }
    const noArt=logic(catalog([target]),[],{[id]:record(id,art)}); assert.equal(noArt.api.state(target),'pending_user'); assert.equal(noArt.nodes.get('approve-model').disabled,true);
  });
  test('synthetic ' + namespace + ': held evidence rejects matching cached confirmation without exporting approval', () => {
    const art=draft(namespace,id);
    for(const changes of [{recordKind:'hold_release_evidence'},{cutoffStatus:'hold_release_evidence'}]) {
      const target={...row(id),...changes}, r=logic(catalog([target]),[art],{[id]:record(id,art)});
      assert.equal(r.api.state(target),'pending_user'); assert.equal(r.nodes.get('approve-model').disabled,true);
      r.nodes.get('approve-model').click(); assert.equal(r.api.state(target),'pending_user');
    }
  });
}

test('read-only real guard: 16 old files and all 23 original targets remain byte-identical', () => {
  const guard=JSON.parse(read('verification/v125-qa/registry-wave-readonly-guard.json'));
  assert.equal(guard.files.length,16); assert.equal(guard.originalConcepts.length,23);
  for(const item of [...guard.files,...guard.originalConcepts]) assert.equal(fileSha(item.path),item.sha256,item.path);
});
test('frozen real gallery: 141 pending entries, seven exact current drafts, zero approval and 134 without drafts', () => {
  const html=read('AeroSim-1.25-机型逐一审核.html');
  const inline=id=>JSON.parse(html.match(new RegExp('<script id="'+id+'" type="application/json">([\\s\\S]*?)<\\/script>'))[1]);
  const data=inline('catalog-data'), art=inline('draft-data'), build=inline('build-data');
  assert.equal(data.reviewBaseCount,132); assert.equal(data.reviewVariantCount,9); assert.equal(data.aircraft.length,141); assert.equal(art.length,7);
  const expected=['dji-phantom-1','dji-mini-4-pro','dji-inspire-1','dji-mavic-pro','dji-mavic-4-pro','dji-phantom-4','dji-air-2s'];
  assert.deepEqual(art.map(x=>x.aircraftId).sort(),expected.sort());
  assert.equal(build.catalogSha256,fileSha('assets/catalog-v125/dji-aircraft.json'));
  assert.equal(data.aircraft.filter(a=>!art.some(d=>d.aircraftId===a.id)).length,134);
  const r=logic(data,art); // Empty memory store: never click real aircraft approval controls.
  for(const a of data.aircraft) { assert.equal(a.appearanceStatus,'pending_user',a.id); assert.equal(a.review.status,'pending_user',a.id); assert.notEqual(a.productionAssetAllowed,true,a.id); assert.equal(r.api.state(a),'pending_user',a.id); }
  assert.equal(Object.keys(r.api.saved()).length,0);
  for(const [namespace,id] of namespaces) {
    const raw=JSON.parse(read('assets/v125-art/'+namespace+'/review-drafts.json')), drafts=Array.isArray(raw)?raw:raw.drafts;
    assert.equal(drafts.length,1); assert.equal(drafts[0].aircraftId,id); assert.equal(drafts[0].appearanceStatus,'pending_user');
    assert.equal(fileSha(drafts[0].image),drafts[0].sha256);
    assert.equal(JSON.stringify(art.find(x=>x.aircraftId===id)),JSON.stringify(drafts[0]));
  }
});
test('frozen real PRD: both new drafts embedded without replacing 23 target concepts or declaring a new native build', () => {
  const prd=read('飞行模拟器产品设计文档.md');
  for(const [namespace,id] of namespaces) {
    const raw=JSON.parse(read('assets/v125-art/'+namespace+'/review-drafts.json')), art=(Array.isArray(raw)?raw:raw.drafts)[0];
    assert.equal(art.aircraftId,id); assert.ok(prd.includes(']('+art.image+')'),art.image); assert.ok(prd.includes(art.sha256),id+' SHA');
  }
  const refs=JSON.parse(read('verification/v1-qa/reference-manifest.json')).screens;
  assert.equal(refs.length,23); for(const original of refs) assert.ok(prd.includes(']('+original.file+')'),original.file);
  // Bind the actual PRD declaration, not an English spelling absent from its
  // Chinese header. This is still a source declaration check, not a native build.
  assert.match(prd,/已构建应用：1\.0\.0 开发候选08/); assert.match(prd,/pending_user/);
});
