import {parseConnections,activeConnections} from './live-core.js';
const $=id=>document.getElementById(id);
const el=(tag,text,cls)=>{const e=document.createElement(tag);if(text!=null)e.textContent=text;if(cls)e.className=cls;return e;};
export function initLive(selectSystem,lookup){
 const state={Thera:{rows:[]},Turnur:{rows:[]},storms:{rows:[]}};let busy=false,next=0;
 const stamp=s=>s?new Date(s).toLocaleString(undefined,{dateStyle:'short',timeStyle:'short'}):'not yet checked';
 function status(name,maxAge){const s=state[name];return !s.checked?`${name}: ${s.error?'unavailable':'waiting for data'}`:`${name}: ${s.error?'unavailable · retained data':Date.now()-Date.parse(s.checked)>maxAge?'stale snapshot':'checked'} ${stamp(s.checked)}`;}
 function mapButton(system){const b=el('button','Show on map');b.type='button';b.disabled=!lookup(system);b.onclick=()=>selectSystem(system,true);return b;}
 function render(){
 const rows=activeConnections([...state.Thera.rows,...state.Turnur.rows],$('live-hub').value,$('live-search').value);
 $('wormhole-count').textContent=String(rows.length);const list=$('live-wormholes');list.replaceChildren();
 if(!rows.length)list.append(el('p','No active connections match these filters, or the feed is unavailable.','muted small'));
 for(const w of rows){const card=el('article',null,'connection-card');const title=el('div',null,'connection-title');title.append(el('span',w.hub,'hub-badge'),el('strong',w.system));card.append(title,el('p',`${w.region} · ${{hs:'High sec',ls:'Low sec',ns:'Null sec',wh:'W-space'}[w.security]||'Security unknown'}`,'muted small'));
 const sig=el('p',`${w.hub}: ${w.signature}  ·  Local: ${w.local}`,'signature-line');card.append(sig);
 const mins=Math.max(1,Math.ceil((Date.parse(w.expires)-Date.now())/60000));const expiry=el('p',`Max size: ${w.size} · ${mins<60?mins+'m':Math.floor(mins/60)+'h '+mins%60+'m'} remaining`,mins<120?'expiry warning':'expiry');expiry.title='Estimated expiry: '+new Date(w.expires).toUTCString();card.append(expiry);
 const actions=el('div',null,'button-row');const copy=el('button','Copy');copy.type='button';copy.onclick=async()=>{try{await navigator.clipboard.writeText(`${w.hub} ↔ ${w.system} (${w.region}) | ${sig.textContent} | Max size: ${w.size} | Expires ${w.expires}`);$('live-message').textContent='Connection details copied.';}catch{$('live-message').textContent='Clipboard unavailable; select and copy the signatures above.';}};actions.append(mapButton(w.system),copy);card.append(actions);list.append(card);}
 $('wormhole-status').textContent=status('Thera',300000)+'\n'+status('Turnur',300000);
 const storms=$('live-storms');storms.replaceChildren();for(const s of state.storms.rows){const c=el('article',null,'connection-card');c.append(el('strong',`${s.system} · ${s.type}`),el('p',`${s.region} · ${s.name}`,'muted small'),el('p',`Source report: ${s.reported}`,'muted small'),mapButton(s.system));storms.append(c);}if(!state.storms.rows.length)storms.append(el('p','Storm data is currently unavailable.','muted small'));
 $('storm-status').textContent=status('storms',1800000);
 }
 async function refresh(){if(busy||Date.now()<next)return;busy=true;next=Date.now()+120000;$('refresh-live').disabled=true;
 let snapshot={};try{const r=await fetch('./data/live.json',{cache:'no-store',signal:AbortSignal.timeout(15000)});if(!r.ok)throw new Error();snapshot=await r.json();if(Array.isArray(snapshot.storms?.rows))state.storms=snapshot.storms;else throw new Error();}catch{state.storms.error='Snapshot unavailable';}
 await Promise.all(['Thera','Turnur'].map(async hub=>{try{const r=await fetch('https://api.eve-scout.com/v2/public/signatures?system_name='+hub,{signal:AbortSignal.timeout(15000)});if(!r.ok)throw new Error();state[hub]={rows:parseConnections(await r.json(),hub),checked:new Date().toISOString(),error:null};}catch{if(Array.isArray(snapshot[hub]?.rows)&&Date.parse(snapshot[hub].checked)>Date.parse(state[hub].checked||0))state[hub]=snapshot[hub];state[hub].error='Live source unavailable';}}));
 busy=false;$('refresh-live').disabled=false;render();}
 $('live-hub').onchange=render;$('live-search').oninput=render;$('refresh-live').onclick=()=>{if(Date.now()<next){$('live-message').textContent='Next feed check after '+new Date(next).toLocaleTimeString();return;}refresh();};
 render();refresh();setInterval(()=>{render();if(document.visibilityState==='visible')refresh();},30000);document.addEventListener('visibilitychange',()=>{if(document.visibilityState==='visible'){render();refresh();}});
}
