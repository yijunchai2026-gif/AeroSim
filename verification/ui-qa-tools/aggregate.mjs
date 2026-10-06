import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {fileURLToPath} from 'node:url';

const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../..');
const dir=path.join(root,'verification/ui-scale-qa');
const expected={
  'desktop-1280x720':[1280,720,7], 'desktop-1680x945':[1680,945,6],
  'desktop-1920x1080':[1920,1080,7], 'desktop-2560x1440':[2560,1440,6],
  'desktop-3840x2160':[3840,2160,6], 'ultrawide-2520x1080':[2520,1080,6],
  'desktop-4k-fullscreen':[3840,2160,6],
  'acceptance-2560x1080':[2560,1080,6],
  'tablet-1024x768':[1024,768,1], 'phone-landscape-844x390':[844,390,1],
  'phone-portrait-390x844':[390,844,1], 'dpi2-2048x1536':[2048,1536,1],
  'dpi3-2532x1170':[2532,1170,1],
};
const hashes={
  'AeroSim.Runtime.dll':'fc0fc875807222e359d53b68cec41b92a2cdf8cdfaec844e3bd3c0744efb2f07',
  'resources.assets':'492bc1253b545e377cc3b348570d7f0b6cd728100355474bfd093b639da9a130',
};
const sha=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const integrity=Object.entries(hashes).map(([name,before])=>{
  const rel='AeroSim/Builds/macOS-AircraftV3/AeroSim.app/Contents/Resources/Data/'+(name.endsWith('.dll')?'Managed/':'')+name;
  const after=sha(path.join(root,rel));return {name,before,after,unchanged:before===after};
});
const rules={},cases=[],runs=[],failures=[],evidenceErrors=[],coverageNotes=[];
const effectivePassed=(check,c)=>{
  if(check.passed)return true;
  if(check.rule==='design.minimum-font-12'&&c.elements.some(e=>(e.name||e.text)===check.target&&e.classes.split(' ').includes('row-icon')))return true;
  // UI Toolkit rounds layout to physical pixels. Retain the raw finding, but do
  // not label a <= 1 physical pixel discrepancy as a real 44-point button defect.
  if(check.rule==='design.action-height-44'){
    const match=/height:([\d.]+)/.exec(check.evidence);
    if(match&&+match[1]>=44-1.05/c.scale)return true;
  }
  return false;
};
for(const [name,[w,h,matrixCount]] of Object.entries(expected)){
  const p=path.join(dir,name,'report.json');
  if(!fs.existsSync(p)){evidenceErrors.push('Missing run: '+name);continue;}
  const report=JSON.parse(fs.readFileSync(p,'utf8'));
  if(report.width!==w||report.height!==h)coverageNotes.push(`${name}: requested ${w}×${h}, actual ${report.width}×${report.height}`);
  if(report.cases.length!==matrixCount*31)evidenceErrors.push(`${name}: missing states, actual ${report.cases.length}`);
  runs.push({name,width:report.width,height:report.height,requestedWidth:w,requestedHeight:h,states:report.cases.length,runtimeErrors:report.runtimeErrors});
  for(const c of report.cases){
    let counts={layout:0,design:0,warning:0,interaction:0,regression:0,rounding:0,decorative:0};
    const ff=[];
    for(const check of c.checks){
      const passed=effectivePassed(check,c);
      const warning=check.rule==='layout.text-fits';
      rules[check.rule]??={total:0,passed:0,failed:0,rawFailed:0,kind:warning?'warning':check.rule.split('.')[0]};
      const r=rules[check.rule];r.total++;r.passed+=+passed;r.failed+=+!passed;r.rawFailed+=+!check.passed;
      if(!check.passed&&passed)counts[check.rule==='design.action-height-44'?'rounding':'decorative']++;
      if(!passed){
        const kind=warning?'warning':check.rule.split('.')[0];counts[kind]++;
        const f={run:name,state:c.state,scale:c.scale,textMultiplier:c.textMultiplier,...check,kind};failures.push(f);ff.push(f);
      }
    }
    let screenshot=null;
    if(c.screenshot){
      const img=path.join(dir,name,c.screenshot);
      if(!fs.existsSync(img))evidenceErrors.push('Missing image: '+img);
      else {
        const bytes=fs.readFileSync(img);
        if(bytes.readUInt32BE(16)!==c.width||bytes.readUInt32BE(20)!==c.height)evidenceErrors.push('PNG size mismatch: '+img);
        screenshot='verification/ui-scale-qa/'+name+'/'+c.screenshot;
      }
    }
    cases.push({run:name,state:c.state,width:c.width,height:c.height,scale:Math.round(c.scale*100)/100,text:c.textMultiplier,
      logicalWidth:c.logicalWidth,logicalHeight:c.logicalHeight,screenshot,checks:c.checks.length,counts,failures:ff.filter(f=>f.kind!=='warning'),warningCount:ff.filter(f=>f.kind==='warning').length});
  }
}
const issuesFile=path.join(dir,'issues.json');
const issues=fs.existsSync(issuesFile)?JSON.parse(fs.readFileSync(issuesFile,'utf8')):[];
const result={generated:new Date().toISOString(),status:'NOT PASSED',integrity,evidenceErrors,coverageNotes,runs,
  totals:{states:cases.length,configurations:new Set(cases.map(c=>c.run+'|'+c.scale+'|'+c.text)).size,
    checks:Object.values(rules).reduce((a,r)=>a+r.total,0),screenshots:cases.filter(c=>c.screenshot).length,
    runtimeErrors:runs.reduce((a,r)=>a+r.runtimeErrors,0),
    designFailures:failures.filter(f=>f.kind==='design').length,layoutFailures:failures.filter(f=>f.kind==='layout').length,
    textWarnings:failures.filter(f=>f.kind==='warning').length,
    interactions:rules['interaction.launch']?.total+rules['interaction.pause']?.total+rules['interaction.resume']?.total,
    interactionFailures:failures.filter(f=>f.kind==='interaction').length,
    roundingFindings:cases.reduce((a,c)=>a+c.counts.rounding,0),decorativeGlyphFindings:cases.reduce((a,c)=>a+c.counts.decorative,0)},rules,issues};
fs.writeFileSync(path.join(dir,'summary.json'),JSON.stringify(result,null,2));
fs.writeFileSync(path.join(dir,'failures.json'),JSON.stringify(failures,null,2));
const esc=s=>String(s??'').replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('>','&gt;').replaceAll('"','&quot;');
const stateDesign={
 'Aerial-Onboarding':'01-onboarding-learning-path','Aerial-Controller':'02-controller-calibration',
 'Aerial-Home':'03-aerial-home','Aerial-Academy':'04-aerial-academy','Aerial-FreeFlight':'05-free-flight-setup',
 'FPV-Home':'06-fpv-home','FPV-Challenges':'07-fpv-challenges','FPV-Tuning':'08-fpv-tuning','FPV-Flight':'09-fpv-race-osd',
 'Professional-Home':'10-professional-dashboard','Professional-Preflight':'11-professional-preflight',
 'Professional-Flight':'12-professional-mission','Professional-Instructor':'13-instructor-live-session',
 'Aerial-Results':'14-flight-results','Aerial-Replay':'15-replay-analysis','Aerial-Readiness':'16-us-flight-readiness',
 'Aerial-Report':'17-professional-report','Aerial-Flight':'flight-training-ui-v1','FPV-OsdEditor':'18-fpv-osd-editor',
 'Professional-Brief':'19-professional-mission-brief','Professional-Catalog':'20-professional-training-catalog',
 'Professional-MissionEditor':'21-professional-mission-editor','Aerial-Settings':'22-global-settings',
};
const rows=Object.entries(rules).map(([rule,r])=>`<tr><td>${esc(rule)}</td><td>${r.total}</td><td>${r.passed}</td><td class="${r.failed?'bad':''}">${r.failed}</td><td>${r.kind==='warning'?'人工复核候选':r.kind}</td></tr>`).join('');
const issueHtml=issues.map(i=>`<article><h3>${esc(i.id)} · ${esc(i.priority)} · ${esc(i.title)}</h3><p>${esc(i.finding)}</p><p class="muted">依据：${esc(i.contract)}。${esc(i.recommendation)}</p>${i.image?`<a href="${esc(i.image)}">查看证据原图</a>`:''}</article>`).join('');
const matrixRows=[];
for(const r of runs){
  for(const key of new Set(cases.filter(c=>c.run===r.name).map(c=>c.scale+'|'+c.text))){
    const [s,t]=key.split('|').map(Number),cc=cases.filter(c=>c.run===r.name&&c.scale===s&&c.text===t);
    const sum=k=>cc.reduce((a,c)=>a+c.counts[k],0);
    matrixRows.push(`<tr><td>${esc(r.name)}</td><td>${r.width}×${r.height}</td><td>${Math.round(s*100)}% / ${t===2?'文字 200%':'文字 100%'}</td><td>${cc.length}</td><td>${sum('layout')}</td><td>${sum('design')}</td><td>${sum('warning')}</td><td>${sum('interaction')}</td></tr>`);
  }
}
const html=`<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>AeroSim UI 缩放 QA</title>
<style>:root{color-scheme:dark}*{box-sizing:border-box}body{margin:0;background:#09111d;color:#e8eef7;font:16px/1.65 system-ui,sans-serif}main{max-width:1500px;margin:auto;padding:32px}h1{font-size:32px;margin:0}h2{margin:36px 0 12px}h3{margin:0 0 8px}a{color:#72bcff}p{margin:8px 0}.muted,small{color:#a5b2c7}.bad{color:#ff8990}.good{color:#7ce2b6}.banner,article{padding:22px;border:1px solid #30445e;background:#111f31;border-radius:12px;margin:14px 0}.banner{border-left:5px solid #ff8990}.metrics{display:flex;gap:12px;flex-wrap:wrap}.metrics span{padding:16px;background:#14273c;border-radius:8px}.metrics strong{display:block;font-size:24px}.table-wrap{overflow:auto}table{border-collapse:collapse;width:100%;font-size:14px}th,td{text-align:left;padding:9px;border-bottom:1px solid #26384f;vertical-align:top}th{background:#14273c}.controls{display:flex;gap:14px;flex-wrap:wrap;padding:16px;background:#14273c;border-radius:8px}label{display:flex;flex-direction:column;font-size:13px;gap:5px}select{font:inherit;max-width:100%;padding:9px;background:#071321;color:white;border:1px solid #426182;border-radius:6px}.compare{display:grid;grid-template-columns:1fr 1fr;gap:18px;margin-top:18px}.frame{background:#03070d;padding:12px;border:1px solid #26384f;border-radius:10px;min-width:0}.frame img{display:block;width:100%;height:auto;margin-top:10px}.empty{padding:30px;color:#a5b2c7}details{margin-top:16px}summary{cursor:pointer}code{overflow-wrap:anywhere}#caseChecks{max-height:380px;overflow:auto}@media(max-width:900px){main{padding:18px}.compare{grid-template-columns:1fr}h1{font-size:26px}}</style>
<main><small>本地验收报告 · 2026-10-05 · 已交付 AeroSim 0.4 隔离副本</small><h1>UI 多尺寸 / 多缩放 QA</h1>
<div class="banner"><h3 class="bad">结论：未通过原产品设计的一致性验收</h3><p>存在已确认的 HUD 遮挡、移动尺寸裁切、缩放设置缺失和页面结构差异。自动检测的重复失败次数不是独立 bug 数；文本测量候选不作为已确认裁切数量。</p><p>本轮只测试，不修改产品界面，不用旧源码覆盖已交付应用。</p></div>
<div class="metrics"><span><strong>${result.totals.configurations}</strong>尺寸 × 倍率 / 文本配置</span><span><strong>${result.totals.states}</strong>页面与交互状态</span><span><strong>${result.totals.screenshots}</strong>原始截图</span><span><strong>${result.totals.interactions-result.totals.interactionFailures}/${result.totals.interactions}</strong>启动 / 暂停 / 继续点击断言通过</span><span><strong>${result.totals.runtimeErrors}</strong>运行时错误</span></div>
<h2>测试边界与基线</h2><p>优先采用<a href="飞行模拟器产品设计文档.md">产品设计文档第 18 章</a>的尺寸与流程规范，再人工比较原始美国版效果图。效果图是概念稿，不是可逐像素比对的可执行设计组件；其中模拟 GPS、在线连接等示意状态不要求离线产品伪装实现。</p><p>80–150% 是测试代码改变真实 PanelSettings.scale 后重新布局，不是改图片大小，也不表示设置页已提供此功能。文字 200% 仅放大字体，每个元素只放大一次；2×/3× 是桌面逻辑 DPI 模拟，不是 iOS 真机或 macOS 显示缩放测试。</p><p>包含三个工作台、25 个菜单页面、三种飞行 HUD、暂停、回放与真实 Input System 指针点击。长页面正常滚动不判失败，关键动作会检查滚动后的可达性；只探测首尾关键动作及机库选择按钮，并非点击所有控件。首次向导仅首步；弹出菜单、完整键盘/手柄流程及 hover/focus/loading/error 全状态没有覆盖。</p>
<p>结果和回放使用独立 <code>--data-dir</code> 内标明 QA fixture 的合成记录，不是用户成绩。用户飞行记录不被测试写入。原应用二进制与资源 SHA-256 前后${integrity.every(x=>x.unchanged)?'相同':'不同（异常）'}；工作区源码与 0.4 交付件不一致，阻碍常规从源码复现。</p>
<p class="muted">4K 窗口请求被系统限制为 3840×1992，因此额外使用全屏模式，完成真实 3840×2160 的六档缩放测试。1680×945 请求实际为 1680×946，按实际尺寸记录，不声称精确 945 高度。</p>
<h2>已确认问题</h2>${issueHtml}
<h2>所见即所得：原设计 / 实际应用</h2><p class="muted">选择已有截图的尺寸、倍率和页面。图像按报告宽度显示，可点击打开原始像素尺寸。两边不是同一数据状态；此处比较结构、相对位置与层级，不给出虚假的像素匹配率。</p>
<div class="controls"><label>窗口尺寸<select id="run"></select></label><label>UI / 文本倍率<select id="scale"></select></label><label>页面<select id="state"></select></label></div>
<p id="caseMeta"></p><div class="compare"><div class="frame"><strong>原始美国版设计效果图</strong><div id="design"></div></div><div class="frame"><strong>真实 AeroSim 0.4 QA 截图</strong><div id="actual"></div></div></div>
<details open><summary>此页面的设计 / 布局异常</summary><div id="caseChecks"></div></details>
<h2>完整测试矩阵</h2><div class="table-wrap"><table><thead><tr><th>测试组</th><th>实际像素尺寸</th><th>UI / 文本</th><th>状态数</th><th>几何失败</th><th>设计失败</th><th>文本候选</th><th>点击失败</th></tr></thead><tbody>${matrixRows.join('')}</tbody></table></div>
<h2>规则统计</h2><p class="muted">允许不超过 1 个实际像素的布局取整；原始日志中的 ${result.totals.roundingFindings} 个按钮高度取整候选仍保留，但不当作实际尺寸缺陷。${result.totals.decorativeGlyphFindings} 个 row-icon 图标字形不算正文最小字号缺陷。所有文本测量失败均需要逐张人工复核。</p><div class="table-wrap"><table><thead><tr><th>规则</th><th>断言数</th><th>通过</th><th>未通过 / 候选</th><th>类型</th></tr></thead><tbody>${rows}</tbody></table></div>
<h2>证据与复现</h2><p><a href="verification/ui-scale-qa/REPORT.md">中文问题报告</a> · <a href="verification/ui-scale-qa/summary.json">汇总 JSON</a> · <a href="verification/ui-scale-qa/failures.json">完整失败 / 候选列表</a> · <a href="verification/ui-qa-tools/CONTRACT.md">验收基线</a> · <a href="verification/ui-qa-tools/run-qa.sh">运行矩阵脚本</a> · <a href="verification/ui-qa-tools/LayoutQa.cs">测试代码</a></p><p>运行 <code>bash verification/ui-qa-tools/build-qa.sh</code> 复制并注入临时 QA 应用，再将打印的路径传给 <code>bash verification/ui-qa-tools/run-qa.sh PATH/AeroSim-UIQA.app</code>，最后 <code>node verification/ui-qa-tools/aggregate.mjs</code>。需要已安装 Unity 6000.3.25f1；不会重新编译产品源码。</p>
<details><summary>每组原始数据和应用日志</summary>${runs.map(r=>`<p>${esc(r.name)}：<a href="verification/ui-scale-qa/${r.name}/report.json">report.json</a> · <a href="verification/ui-scale-qa/${r.name}/player.log">player.log</a></p>`).join('')}</details>
<details><summary>证据完整性 / 原应用校验</summary><pre>${esc(JSON.stringify({integrity,evidenceErrors,coverageNotes},null,2))}</pre></details>
<p class="muted">未测试 Windows 实机、iPad / iPhone 真机、系统动态字体、VoiceOver、色彩对比度或所有交互状态。本报告不能代替这些验收。</p></main>
<script>const cases=${JSON.stringify(cases).replaceAll('<','\\u003c')};const designs=${JSON.stringify(stateDesign)};
const R=document.getElementById('run'),S=document.getElementById('scale'),T=document.getElementById('state');
function opts(el,values,label){const old=el.value;el.replaceChildren(...values.map(v=>{const o=document.createElement('option');o.value=v;o.textContent=label(v);return o}));if(values.includes(old))el.value=old;}
const captured=cases.filter(c=>c.screenshot);opts(R,[...new Set(captured.map(c=>c.run))],x=>x);R.value='desktop-1920x1080';
function scales(){opts(S,[...new Set(captured.filter(c=>c.run===R.value).map(c=>c.scale+'|'+c.text))],v=>{let[s,t]=v.split('|').map(Number);return Math.round(s*100)+'% UI · '+(t*100)+'% text'});states()}
function states(){const[s,t]=S.value.split('|').map(Number);opts(T,captured.filter(c=>c.run===R.value&&c.scale===s&&c.text===t).map(c=>c.state),x=>x);if(!T.value)T.value='Aerial-Home';show()}
function imgInto(id,src,alt){const host=document.getElementById(id);host.replaceChildren();if(!src){host.textContent='此页面没有对应的原始独立效果图；以 PRD 文字规范验收。';host.className='empty';return}host.className='';const a=document.createElement('a');a.href=src;a.target='_blank';const img=document.createElement('img');img.src=src;img.alt=alt;a.append(img);host.append(a)}
function show(){const[s,t]=S.value.split('|').map(Number),c=captured.find(c=>c.run===R.value&&c.scale===s&&c.text===t&&c.state===T.value);if(!c)return;const d=designs[c.state];imgInto('design',d?'assets/aerosim-us-'+d+'.png':null,'原产品设计效果图');imgInto('actual',c.screenshot,'实际应用 '+c.state);document.getElementById('caseMeta').textContent=c.width+'×'+c.height+' 像素 · 逻辑视口 '+c.logicalWidth.toFixed(1)+'×'+c.logicalHeight.toFixed(1)+' · '+c.counts.layout+' 个几何失败 / '+c.counts.design+' 个设计失败 / '+c.warningCount+' 个文本测量候选';const list=document.getElementById('caseChecks');list.replaceChildren();if(!c.failures.length){list.textContent='该页面自动几何 / 数值规则无失败；不代表所有交互或视觉设计均验收通过。'}else for(const f of c.failures){const p=document.createElement('p');p.textContent=f.rule+' · '+f.target+' · '+f.evidence;list.append(p)}}R.onchange=scales;S.onchange=states;T.onchange=show;scales();T.value='Aerial-Home';show();</script></html>`;
fs.writeFileSync(path.join(root,'AeroSim-UI缩放QA.html'),html);
const csv=['run,width,height,uiScale,textScale,state,layoutFailures,designFailures,textCandidates,interactionFailures',...cases.map(c=>[c.run,c.width,c.height,c.scale,c.text,c.state,c.counts.layout,c.counts.design,c.warningCount,c.counts.interaction].join(','))].join('\n');
fs.writeFileSync(path.join(dir,'matrix.csv'),csv+'\n');
console.log(JSON.stringify({totals:result.totals,runs:runs.length,integrity:integrity.every(i=>i.unchanged),evidenceErrors},null,2));
