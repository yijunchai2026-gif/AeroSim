// Mechanical generation from reviewed source data; file:// works without fetch/CORS.
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {fileURLToPath} from 'node:url';

const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../..');
const read=p=>fs.readFileSync(path.join(root,p),'utf8');
const sha=p=>crypto.createHash('sha256').update(fs.readFileSync(path.join(root,p))).digest('hex');
const dataPath='assets/catalog-v125/dji-aircraft.json';
const sourceCatalog=JSON.parse(read(dataPath));
const catalog={...sourceCatalog,asOf:sourceCatalog.asOf||sourceCatalog.asOfDate};
if(![1,'1.0.0'].includes(catalog.schemaVersion)||catalog.asOf!=='2026-10-09'||!Array.isArray(catalog.aircraft)||!catalog.aircraft.length)throw Error('Invalid catalog schema/date/aircraft');
const baseCount=catalog.aircraft.length;
catalog.aircraft=catalog.aircraft.flatMap(a=>[a,...(a.variants||[]).map(v=>({...v,productLine:a.productLine,recordKind:v.kind||'appearance_variant',variants:[]}))]);
catalog.reviewBaseCount=baseCount;
catalog.reviewVariantCount=catalog.aircraft.length-baseCount;
const ids=new Set(),families=new Set(catalog.families.map(x=>x.id)),categories=new Set(catalog.categories.map(x=>x.id));
function official(url){try{const u=new URL(url);return u.protocol==='https:'&&(/(^|\.)dji\.com$/.test(u.hostname)||['dl.djicdn.com','www1.djicdn.com','www4.djicdn.com'].includes(u.hostname)||u.hostname==='se-cdn.djiits.com'||u.hostname==='download.dji-innovations.com'||/(^|\.)ryzerobotics\.com$/.test(u.hostname));}catch{return false;}}
for(const a of catalog.aircraft){
  if(!/^[a-z0-9][a-z0-9-]+$/.test(a.id)||ids.has(a.id))throw Error('Invalid/duplicate aircraft ID '+a.id);ids.add(a.id);
  if(!a.englishName||!a.introductionZh||!families.has(a.familyId)||!categories.has(a.categoryId))throw Error('Incomplete aircraft '+a.id);
  if(a.appearanceStatus!=='pending_user'||a.review?.status!=='pending_user'||a.review?.approvedBy!=null||a.review?.approvedAt!=null)throw Error('Seed catalog may not approve an aircraft '+a.id);
  if(!a.sourceUrls?.length||a.sourceUrls.some(u=>!official(u)))throw Error('Missing/untrusted official source '+a.id);
  if(a.releaseYear!=null&&(!Number.isInteger(a.releaseYear)||!official(a.releaseYearEvidenceUrl)))throw Error('Year lacks official source '+a.id);
}
const drafts=[];
for(const p of ['assets/v125-art/legacy/review-drafts.json','assets/v125-art/current/review-drafts.json','assets/v125-art/inspire/review-drafts.json','assets/v125-art/mavic/review-drafts.json']){
  if(!fs.existsSync(path.join(root,p)))continue;
  const raw=JSON.parse(read(p));
  for(const d of Array.isArray(raw)?raw:raw.drafts||[]){
    if(!ids.has(d.aircraftId)||drafts.some(x=>x.aircraftId===d.aircraftId))throw Error('Unknown/duplicate draft '+d.aircraftId);
    if(!/^assets\/v125-art\/(legacy|current|inspire|mavic)\/[a-z0-9-]+\.png$/.test(d.image)||sha(d.image)!==d.sha256)throw Error('Draft file/hash mismatch '+d.image);
    if(d.kind!=='generated_review_bitmap'||d.appearanceStatus!=='pending_user'||!d.revision||!d.sourceUrls?.length||d.sourceUrls.some(u=>!official(u)))throw Error('Invalid review draft '+d.aircraftId);
    drafts.push(d);
  }
}
const inline=x=>JSON.stringify(x).replaceAll('<','\\u003c').replaceAll('\u2028','\\u2028').replaceAll('\u2029','\\u2029');
const html=read('assets/catalog-v125/review-template.html').replace('__CATALOG_DATA__',inline(catalog)).replace('__DRAFT_DATA__',inline(drafts)).replace('__BUILD_DATA__',inline({catalogSha256:sha(dataPath)}));
const destination=path.join(root,'AeroSim-1.25-机型逐一审核.html');
fs.writeFileSync(destination,html);
console.log(JSON.stringify({output:destination,aircraft:baseCount,variants:catalog.reviewVariantCount,reviewEntries:ids.size,drafts:drafts.length,catalogSha256:sha(dataPath),appearanceStatus:'pending_user',formalFlightIntegration:false}));
