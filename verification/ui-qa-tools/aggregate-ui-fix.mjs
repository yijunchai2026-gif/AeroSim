import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {fileURLToPath} from 'node:url';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../..');
const folder=process.argv[2]||'ui-scale-qa-v04-ui-fix-20261007-release';
if(!/^ui-scale-qa-v04-ui-fix-[\w-]+$/.test(folder))throw Error('Invalid verification folder');
const dir=path.join(root,'verification',folder);
const expected=['desktop-1280x720','desktop-1680x945','desktop-1920x1080','desktop-2560x1440','desktop-3840x2160','desktop-4k-fullscreen','ultrawide-2520x1080','acceptance-2560x1080','tablet-1024x768','phone-landscape-844x390','phone-portrait-390x844','dpi2-2048x1536','dpi3-2532x1170'];
const rules={},cases=[],runs=[],missing=[],evidenceErrors=[];
for(const run of expected){
 const source=path.join(dir,run,'report.json');if(!fs.existsSync(source)){missing.push(run);continue;}
 const report=JSON.parse(fs.readFileSync(source,'utf8'));
 const configs=run==='desktop-1280x720'||run==='desktop-1920x1080'?7:run.startsWith('desktop-')||run.startsWith('ultrawide-')||run.startsWith('acceptance-')?6:1;
 if(report.cases.length!==configs*31+1)evidenceErrors.push(run+': incomplete state matrix');
 if(report.version!=='0.4.0'||!report.startedUtc)evidenceErrors.push(run+': missing current-build version/timestamp');
 runs.push({run,width:report.width,height:report.height,states:report.cases.length,runtimeErrors:report.runtimeErrors});
 for(const c of report.cases){
  const failures=[];
  for(const check of c.checks){
   const r=rules[check.rule]??={checks:0,failed:0};r.checks++;
   if(!check.passed){r.failed++;failures.push(check);}
  }
  cases.push({...c,run,elements:undefined,checks:undefined,failures,screenshot:c.screenshot?`verification/${folder}/${run}/${c.screenshot}`:null});
 }
}
const totals={runs:runs.length,states:cases.length,checks:Object.values(rules).reduce((n,r)=>n+r.checks,0),runtimeErrors:runs.reduce((n,r)=>n+r.runtimeErrors,0),geometryFailures:0,designFailures:0,textCandidates:rules['layout.text-fits']?.failed||0,interactionFailures:0,regressionFailures:0};
for(const [key,r]of Object.entries(rules)){
 if(key.startsWith('layout.')&&key!=='layout.text-fits')totals.geometryFailures+=r.failed;
 if(key.startsWith('design.'))totals.designFailures+=r.failed;
 if(key.startsWith('interaction.'))totals.interactionFailures+=r.failed;
 if(key.startsWith('regression.'))totals.regressionFailures+=r.failed;
}
const smokeFile=path.join(dir,'native-smoke.txt');
const smoke=fs.existsSync(smokeFile)?fs.readFileSync(smokeFile,'utf8'):'';
const summary={version:'0.4.0',date:'2026-10-07',mode:'Native UI/HUD/Text preferences, isolated profile and app copy',totals,rules,runs,missing,smoke:{passed:(smoke.match(/^PASS /gm)||[]).length,failed:(smoke.match(/^FAIL /gm)||[]).length,available:!!smoke},limitations:['Desktop macOS native tests only; tablet/phone/DPI cases are logical viewport probes, not iOS hardware tests.','Text measurements are candidates requiring screenshot review, not a pixel-perfect visual score.','Pause actions may require scrolling; viewport containment and each scrolled action are checked.','This fixes proportions, not all page information architecture differences from concept art.']};
summary.evidenceErrors=evidenceErrors;
summary.limitations.push('Full-page 200% text stress is tested at 100% UI/HUD scale on 1280 and 1920 desktop widths; all mixed maximum-zoom/text combinations are not covered.');
const candidate=process.argv[3]||path.join(root,'AeroSim/Builds/macOS-AircraftV3/AeroSim.app');
const data=path.join(candidate,'Contents/Resources/Data');
const sha=file=>crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
if(fs.existsSync(data))summary.build={runtimeSha256:sha(path.join(data,'Managed/AeroSim.Runtime.dll')),resourcesSha256:sha(path.join(data,'resources.assets')),responsiveSourceSha256:sha(path.join(root,'AeroSim/Assets/AeroSim/Resources/UI/Responsive.uss'))};
fs.writeFileSync(path.join(dir,'summary.json'),JSON.stringify(summary,null,2));
fs.writeFileSync(path.join(dir,'failures.json'),JSON.stringify(cases.filter(c=>c.failures.length),null,2));
const esc=x=>String(x).replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('>','&gt;').replaceAll('"','&quot;');
const designs={'Aerial-Home':'03-aerial-home','Aerial-FreeFlight':'05-free-flight-setup','Aerial-Settings':'22-global-settings','FPV-OsdEditor':'18-fpv-osd-editor','Professional-MissionEditor':'21-professional-mission-editor','Aerial-Replay':'15-replay-analysis','Aerial-Flight':'flight-training-ui-v1'};
const html=`<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>AeroSim 0.4 UI 比例修复验收</title><style>body{background:#071722;color:#e6f1f7;font:16px/1.6 system-ui;margin:0}main{max-width:1600px;margin:auto;padding:32px}a{color:#60d9f1}select{font:inherit;background:#18384b;color:white;padding:8px;margin:4px;border:1px solid #52798e;border-radius:6px}.compare{display:grid;grid-template-columns:1fr 1fr;gap:20px}img{width:100%;height:auto;background:#112737}table{border-collapse:collapse;width:100%;font-size:14px}td,th{padding:8px;border:1px solid #385363;text-align:left}.bad{color:#ffb184}pre{white-space:pre-wrap}details{margin:20px 0}h1{font-size:28px}@media(max-width:900px){.compare{grid-template-columns:1fr}main{padding:16px}}</style><main><h1>AeroSim 0.4 · UI 比例修复</h1><p>2026-10-07 · 真正的 UI/HUD 独立缩放、原生 200% 文字重排、64px 顶栏、240/72px 导航、1600px 内容上限、48/56px HUD 上下栏、中央视野避让、可滚动弹窗。</p><p>${totals.runs} 组 / ${totals.states} 个状态 / ${totals.checks} 项断言；几何失败 ${totals.geometryFailures}，设计数值失败 ${totals.designFailures}，交互失败 ${totals.interactionFailures}，回归失败 ${totals.regressionFailures}，运行错误 ${totals.runtimeErrors}。文本测量候选 ${totals.textCandidates}（需人工复核）。原生功能回归 ${summary.smoke.passed} 通过 / ${summary.smoke.failed} 失败。</p><p>不宣称全部页面与概念图逐像素一致，也不宣称已经通过 iPhone/iPad 真机验收。长页面与弹窗允许正常滚动。历史测试报告未覆盖。</p><p><a href="verification/${folder}/summary.json">详细汇总</a> · <a href="verification/${folder}/native-smoke.txt">原生功能回归</a></p><h2>所见即所得 · 设计参考与实际截图</h2><label>测试配置 <select id="config"></select></label><label>页面 <select id="page"></select></label><p id="meta"></p><div class="compare"><section><h3>原始设计参考</h3><div id="design"></div></section><section><h3>0.4 实际运行截图</h3><div id="actual"></div></section></div><details><summary>当前页面自动检查发现</summary><pre id="findings"></pre></details><h2>测试矩阵</h2><table><tr><th>组</th><th>实际分辨率</th><th>状态数</th><th>运行错误</th></tr>${runs.map(r=>`<tr><td>${esc(r.run)}</td><td>${r.width}×${r.height}</td><td>${r.states}</td><td>${r.runtimeErrors}</td></tr>`).join('')}</table><h2>完整规则统计</h2><table><tr><th>规则</th><th>检查数</th><th>未通过 / 文本候选</th></tr>${Object.entries(rules).map(([k,v])=>`<tr><td>${esc(k)}</td><td>${v.checks}</td><td class="${v.failed?'bad':''}">${v.failed}</td></tr>`).join('')}</table><p>缺失组：${esc(missing.join(', ')||'无')}。2×/3× 为桌面逻辑密度测试；未测试 Windows 或 iOS 真机、系统动态字体、VoiceOver 及全部交互状态。</p></main><script>const cases=${JSON.stringify(cases.filter(c=>c.screenshot)).replaceAll('<','\\u003c')},designs=${JSON.stringify(designs)};const C=document.querySelector('#config'),P=document.querySelector('#page');const key=c=>c.run+' | '+Math.round(c.scale*100)+'% UI | '+Math.round(c.textMultiplier*100)+'% text';function options(el,values){const previous=el.value;el.replaceChildren(...values.map(v=>{let o=document.createElement('option');o.value=o.textContent=v;return o}));if(values.includes(previous))el.value=previous}options(C,[...new Set(cases.map(key))]);if([...C.options].some(o=>o.value==='desktop-1920x1080 | 100% UI | 100% text'))C.value='desktop-1920x1080 | 100% UI | 100% text';function image(id,src){const host=document.querySelector(id);host.replaceChildren();if(!src){host.textContent='此页面没有独立设计参考图；请按产品文档文字规范验收。';return}let a=document.createElement('a'),i=document.createElement('img');a.href=src;a.target='_blank';i.src=src;a.append(i);host.append(a)}function show(){const c=cases.find(c=>key(c)===C.value&&c.state===P.value);if(!c)return;image('#actual',c.screenshot);image('#design',designs[c.state]?'assets/aerosim-us-'+designs[c.state]+'.png':null);document.querySelector('#meta').textContent=c.width+'×'+c.height+' 实际像素 · '+c.logicalWidth.toFixed(1)+'×'+c.logicalHeight.toFixed(1)+' 逻辑视口';document.querySelector('#findings').textContent=c.failures.length?c.failures.map(f=>f.rule+' · '+f.target+' · '+f.evidence).join('\n'):'本页面自动规则无失败；仍需人工视觉验收。'}function pages(){options(P,cases.filter(c=>key(c)===C.value).map(c=>c.state));show()}C.onchange=pages;P.onchange=show;pages();</script></html>`;
fs.writeFileSync(path.join(root,'AeroSim-0.4-UI比例修复.html'),html);
fs.writeFileSync(path.join(dir,'REPORT.md'),`# AeroSim 0.4 UI 比例修复 QA\n\n原生缩放设置测试，隔离用户数据。\n\n\`\`\`json\n${JSON.stringify(summary,null,2)}\n\`\`\`\n\n全部实际截图见根目录 AeroSim-0.4-UI比例修复.html。文字候选需人工视觉检查；不代表全设计逐像素一致或 iOS 真机验证。\n`);
console.log(JSON.stringify(summary,null,2));
