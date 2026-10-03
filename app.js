import {makeUniverse,route,parseSovereignty,makeIntelParser,decodeLog,securityColor} from './core.js';
import {StarMap} from './map.js';
const $=id=>document.getElementById(id);
const read=(key,fallback)=>{try{return JSON.parse(localStorage.getItem(key))??fallback;}catch{return fallback;}};
const write=(key,value)=>{try{localStorage.setItem(key,JSON.stringify(value));}catch{/* Private mode or full storage: session still works. */}};
const prefs=read('smt-atlas-prefs',{}),cache=read('smt-atlas-adm',null);
const state={universe:null,map:null,region:null,selected:null,reports:[],demoReports:[],demo:false,sov:new Map(),checked:null,etag:null,stale:true,nextCheck:0,fetching:false};
function save(){write('smt-atlas-prefs',{region:state.region?.name,selected:state.selected?.name,from:$('route-from').value,to:$('route-to').value,high:$('high-sec').checked,showAdm:$('show-adm').checked});}
function el(tag,text,cls){const node=document.createElement(tag);if(text!=null)node.textContent=text;if(cls)node.className=cls;return node;}
function panel(name){document.querySelector('.workspace').dataset.panel=name;for(const b of document.querySelectorAll('[data-panel].mobile-nav button, .mobile-nav button'))b.classList.toggle('active',b.dataset.panel===name);}
for(const button of document.querySelectorAll('.mobile-nav button'))button.addEventListener('click',()=>panel(button.dataset.panel));
$('about-open').onclick=()=>$('about').showModal();$('about-close').onclick=()=>$('about').close();
function chooseRegion(name){const region=state.universe.regions.find(r=>r.name===name);if(!region)return;state.region=region;$('region').value=name;$('region-name').textContent=name;$('region-summary').textContent=`${region.nodes.filter(n=>!n.outside).length} systems · Stargate network`;state.map.setRegion(region);save();}
function selectSystem(name,showMap=false){const s=state.universe.lookup(name);if(!s)return;state.selected=s;state.map.selected=s.name;
  if(!state.region?.nodes.some(n=>n.name===s.name)){const r=state.universe.regions.find(r=>r.name===s.region)||state.universe.regions.find(r=>r.nodes.some(n=>n.name===s.name));if(r)chooseRegion(r.name);}
  $('system-name').textContent=s.name;$('system-info').textContent=`${s.region}\nSecurity ${s.security.toFixed(2)} · ${s.jumps.length} gates`+(s.station?'\nNPC station present':'');$('set-start').disabled=$('set-end').disabled=false;updateDetail();state.map.draw();save();
  if(showMap)panel('map');
}
function updateDetail(){const s=state.selected;if(!s)return;const v=state.sov.get(s.id),box=$('adm-detail');box.style.color=state.stale?'#d9b573':'';
  if(!v){box.textContent='ADM · unavailable / not reported';return;}
  box.textContent=v.adm!=null?`ADM ${v.adm.toFixed(1)}×${state.stale?' · cached/old':''}\nMilitary ${v.military??'—'} · Industry ${v.industrial??'—'}\nStrategic ${v.strategic??'—'}${v.capital?' · Capital':''}`:['faction','unclaimed'].includes(v.kind)?'ADM · N/A (no applicable sov ADM)':'ADM · not reported by ESI';
}
function applySov(){if(!state.map)return;state.map.sov=state.sov;state.map.stale=state.stale;state.map.draw();updateDetail();$('feed-dot').classList.toggle('fresh',!state.stale);}
const utc=time=>new Date(time).toLocaleTimeString('en-GB',{timeZone:'UTC',hour:'2-digit',minute:'2-digit'});
async function refreshAdm(){
  if(state.fetching)return;
  if(Date.now()<state.nextCheck){$('adm-status').textContent=`${state.stale?'Cached · ':''}Checked ${state.checked?utc(state.checked)+' UTC':'not yet'} · next check ${utc(state.nextCheck)} UTC`;return;}
  state.fetching=true;$('refresh-adm').disabled=true;$('adm-status').textContent='Checking public ESI…';state.nextCheck=Date.now()+300000;
  try{
    const headers={'X-Compatibility-Date':'2026-05-19'};if(state.etag)headers['If-None-Match']=state.etag;
    const response=await fetch('https://esi.evetech.net/sovereignty/systems',{headers,signal:AbortSignal.timeout(25000)});
    const retry=response.headers.get('Retry-After');if(retry){const n=Number(retry),until=Number.isFinite(n)?Date.now()+n*1000:Date.parse(retry);if(Number.isFinite(until))state.nextCheck=Math.max(state.nextCheck,until);}
    let raw;
    if(response.status===304){if(!state.checked)throw new Error('No cached response available');raw=state.raw;}
    else{if(!response.ok)throw new Error(`ESI returned ${response.status}`);raw=await response.json();const sov=parseSovereignty(raw);state.sov=sov;state.raw=raw;state.etag=response.headers.get('ETag');}
    state.checked=Date.now();state.stale=false;write('smt-atlas-adm',{checked:state.checked,etag:state.etag,data:raw});
    $('adm-status').textContent=`Public ESI · checked ${utc(state.checked)} UTC · 5m refresh`;$('adm-error').hidden=true;
  }catch(error){state.stale=true;$('adm-status').textContent=state.checked?`Cached · checked ${new Date(state.checked).toLocaleDateString()} ${utc(state.checked)} UTC`:'ADM unavailable · map remains usable';$('adm-error').textContent=`ESI unavailable. ${state.checked?'Amber values are cached. ':''}Retry after ${utc(state.nextCheck)} UTC.`;$('adm-error').hidden=false;}
  finally{state.fetching=false;$('refresh-adm').disabled=false;applySov();}
}
function plotRoute(event){event?.preventDefault();const from=$('route-from').value,to=$('route-to').value;
  if(!state.universe.lookup(from)||!state.universe.lookup(to)){$('route-summary').textContent='Enter two complete system names. Search above can help.';return;}
  const result=route(state.universe,from,to,$('high-sec').checked);state.map.route=result;$('route-list').replaceChildren();
  $('route-summary').textContent=result.length?`${result.length-1} jumps · ${result.filter(s=>s.security<.45).length} low/null-sec systems. Snapshot gates; verify in game.`:'No route found with these restrictions.';
  result.forEach((s,index)=>{const li=el('li'),button=el('button');button.type='button';button.append(el('span',String(index).padStart(2,'0'),'step'),el('b',s.name));const sec=el('em',s.security.toFixed(1));sec.style.color=securityColor(s.security);button.append(sec);button.onclick=()=>selectSystem(s.name,true);li.append(button);$('route-list').append(li);});
  if(result.length)selectSystem(result[0].name);state.map.draw();save();
}
function showReports(){const reports=state.demo?state.demoReports:state.reports;state.map.reports=reports;state.map.draw();$('intel-count').textContent=$('mobile-count').textContent=reports.length;$('demo-banner').hidden=!state.demo;$('demo-button').textContent=state.demo?'Exit demo':'Try demo';
  $('intel-message').disabled=$('intel-form').querySelector('button').disabled=$('import-button').disabled=state.demo;
  const list=$('intel-list');list.replaceChildren();
  if(!reports.length){const empty=el('div',null,'empty-intel');empty.append(el('span','◎'),el('h3','Your intel, on the map.'),el('p','Import EVE chat logs or add a report below. Select a report to find its system.'));list.append(empty);}
  for(const r of [...reports].sort((a,b)=>b.time-a.time).slice(0,100)){const b=el('button',null,'intel-report');b.append(el('strong',r.systems.join(' · ')+(r.clear?' / CLEAR REPORTED':' / REPORT'),r.clear?'report-clear':''),el('p',r.message));const old=Date.now()-r.time>900000;b.append(el('small',`${new Date(r.time).toLocaleDateString()} ${utc(r.time)} UTC · ${r.speaker} · ${r.source}${old?' · expired from map':''}`));b.onclick=()=>selectSystem(r.systems[0],true);list.append(b);}
}
function addReport(r){if(state.reports.some(x=>x.time===r.time&&x.speaker===r.speaker&&x.message===r.message&&x.source===r.source))return;state.reports.push(r);state.reports.sort((a,b)=>a.time-b.time);if(state.reports.length>500)state.reports.splice(0,state.reports.length-500);}
async function init(){
  try{const response=await fetch('./data/universe.json');if(!response.ok)throw new Error('Map data unavailable');state.universe=makeUniverse(await response.json());}
  catch{$('map-loading').textContent='Could not load map data. Check your connection and reload this page.';$('app-status').textContent='Map download failed.';return;}
  const u=state.universe,parse=makeIntelParser(u);state.map=new StarMap($('map'),u,name=>selectSystem(name));
  $('region').replaceChildren(...[...u.regions].sort((a,b)=>a.name.localeCompare(b.name)).map(r=>{const o=el('option',r.name);o.value=r.name;return o;}));$('region').disabled=false;
  $('route-from').value=prefs.from||'';$('route-to').value=prefs.to||'';$('high-sec').checked=!!prefs.high;$('show-adm').checked=prefs.showAdm!==false;state.map.showAdm=$('show-adm').checked;
  chooseRegion(u.regions.some(r=>r.name===prefs.region)?prefs.region:'Delve');if(prefs.selected)selectSystem(prefs.selected);
  $('data-summary').textContent=`${u.systems.length.toLocaleString()} systems · ${u.regions.length} regional maps · Public ESI`;$('map-loading').hidden=true;
  $('region').onchange=()=>chooseRegion($('region').value);
  $('search').oninput=()=>{const q=$('search').value.trim().toLowerCase();$('search-results').replaceChildren();if(!q)return;const found=u.systems.filter(s=>s.name.toLowerCase().includes(q)).sort((a,b)=>Number(!a.name.toLowerCase().startsWith(q))-Number(!b.name.toLowerCase().startsWith(q))||a.name.localeCompare(b.name)).slice(0,12);for(const s of found){const b=el('button',s.name);b.append(el('small',s.region));b.onclick=()=>{selectSystem(s.name);$('search-results').replaceChildren();$('search').value='';};$('search-results').append(b);}if(!found.length)$('search-results').append(el('p','No matching systems.','muted small'));};
  $('search').onkeydown=e=>{if(e.key==='Enter'){$('search-results').querySelector('button')?.click();e.preventDefault();}if(e.key==='Escape'){$('search-results').replaceChildren();}};
  $('set-start').onclick=()=>{if(state.selected){$('route-from').value=state.selected.name;save();}};$('set-end').onclick=()=>{if(state.selected){$('route-to').value=state.selected.name;save();}};
  $('route-form').onsubmit=plotRoute;$('clear-route').onclick=()=>{state.map.route=[];$('route-list').replaceChildren();$('route-summary').textContent='Route cleared.';state.map.draw();};
  $('zoom-in').onclick=()=>state.map.zoom(1.25);$('zoom-out').onclick=()=>state.map.zoom(.8);$('fit').onclick=()=>state.map.fit();$('show-adm').onchange=()=>{state.map.showAdm=$('show-adm').checked;state.map.draw();save();};
  $('demo-button').onclick=()=>{state.demo=!state.demo;if(state.demo){chooseRegion('Delve');selectSystem('1DQ1-A');state.demoReports=['1DQ1-A 3 neutrals on the gate','T5ZI-S clear','N-8YET interceptor towards 1DQ1-A'].map((m,i)=>parse(m,'DEMO',true,Date.now()-(i+1)*60000));}else state.demoReports=[];showReports();$('intel-status').textContent=state.demo?'Synthetic intel only. ADM values remain real ESI data.':'';};
  $('intel-form').onsubmit=e=>{e.preventDefault();if(state.demo)return;const r=parse($('intel-message').value,'Manual',true);if(!r){$('intel-status').textContent='Include a complete system name, such as 1DQ1-A.';return;}addReport(r);$('intel-message').value='';$('intel-status').textContent='Report added locally.';showReports();};
  $('clear-intel').onclick=()=>{if(state.demo)state.demoReports=[];else state.reports=[];showReports();};
  $('import-button').onclick=()=>$('log-files').click();
  $('log-files').onchange=async()=>{if(state.demo)return;const files=[...$('log-files').files];if(files.length>10){$('intel-status').textContent='Choose at most 10 files per import.';return;}let matched=0,failed=0;$('import-button').disabled=true;
    try{for(const file of files){if(file.size>5*1024*1024){failed++;continue;}try{const text=decodeLog(await file.arrayBuffer());for(const line of text.split(/\r?\n/)){const r=parse(line,file.name);if(r){addReport(r);matched++;}}}catch{failed++;}await new Promise(r=>setTimeout(r,0));}showReports();$('intel-status').textContent=`${matched} matching lines read; duplicate reports merged.${failed?' '+failed+' files skipped (unreadable or over 5 MB).':''} Old reports do not highlight the map.`;}
    finally{$('import-button').disabled=state.demo;$('log-files').value='';}
  };
  try{if(cache?.data&&Number.isFinite(cache.checked)&&cache.checked<=Date.now()+60000){state.sov=parseSovereignty(cache.data);state.raw=cache.data;state.checked=cache.checked;state.etag=cache.etag;$('adm-status').textContent=`Cached · checked ${utc(cache.checked)} UTC`;applySov();}}catch{/* Invalid cache is replaced on the next fetch. */}
  const context=document.modelContext;
  if(context?.registerTool){
    const lifecycle=new AbortController();
    addEventListener('pagehide',()=>lifecycle.abort(),{once:true});
    try{Promise.resolve(context.registerTool({name:'select_star_system',description:'Select a star system and display its regional map and public ADM details.',inputSchema:{type:'object',properties:{name:{type:'string'}},required:['name'],additionalProperties:false},annotations:{readOnlyHint:false},execute(input){if(typeof input?.name!=='string'||!u.lookup(input.name))throw new Error('Unknown system');selectSystem(input.name,true);return {system:state.selected.name,region:state.region.name,adm:state.sov.get(state.selected.id)?.adm??null,cached:state.stale};}},{signal:lifecycle.signal})).catch(()=>{});}catch{/* Optional browser integration. */}
  }
  $('refresh-adm').onclick=refreshAdm;refreshAdm();setInterval(()=>{state.map.draw();if(document.visibilityState==='visible'&&Date.now()>=state.nextCheck)refreshAdm();},30000);
  document.addEventListener('visibilitychange',()=>{if(document.visibilityState==='visible'){if(state.checked&&Date.now()-state.checked>600000){state.stale=true;applySov();}refreshAdm();}});
}
init();
