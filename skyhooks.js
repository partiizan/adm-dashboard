import {SKYHOOK_URL,SKYHOOK_REFRESH,parseSkyhooks,vulnerableInRegion,skyhookFeedFresh,planetLabel} from './skyhooks-core.js';
const $=id=>document.getElementById(id);
const el=(tag,text,cls)=>{const node=document.createElement(tag);node.textContent=text;if(cls)node.className=cls;return node;};
export function initSkyhooks(universe,map,getRegion,selectSystem){
 const systems=new Map(universe.systems.map(s=>[s.id,s]));
 let rows=[],checked=0,error='',busy=false,next=0;
 const planets=new Map(),planetQueue=[];let planetRequests=0,planetBackoff=0;
 const location=hook=>{
  const planet=planets.get(hook.planetId),system=systems.get(hook.systemId)?.name||'Unknown system';
  return planet?.name?planetLabel(planet.name,system):system+': Planet '+hook.planetId+(planet?.pending?' (loading name…)':' (name unavailable)');
 };
 map.skyhookLocation=location;
 function queuePlanet(hook){
  const existing=planets.get(hook.planetId);
  if(existing&&(existing.name||existing.pending||Date.now()<existing.retry))return;
  const entry={pending:true,retry:0};planets.set(hook.planetId,entry);planetQueue.push({hook,entry});
 }
 function pumpPlanets(){
  if(Date.now()<planetBackoff)return;
  while(planetRequests<3&&planetQueue.length){
   const {hook,entry}=planetQueue.shift();planetRequests++;
   fetch('https://esi.evetech.net/universe/planets/'+hook.planetId+'/?compatibility_date=2026-05-19',{signal:AbortSignal.timeout(15000)})
    .then(async response=>{
     if([420,429,503].includes(response.status)){
      const retry=response.headers.get('Retry-After'),seconds=Number(retry);
      planetBackoff=Math.max(Date.now()+60000,retry?(Number.isFinite(seconds)?Date.now()+seconds*1000:Date.parse(retry))||0:0);
     }
     if(!response.ok)throw new Error('Planet lookup unavailable');
     const data=await response.json();
     if(data.system_id!==hook.systemId||typeof data.name!=='string'||!data.name.trim())throw new Error('Planet response mismatch');
     entry.name=data.name;
    }).catch(()=>{entry.retry=Date.now()+300000;})
    .finally(()=>{entry.pending=false;planetRequests--;render();});
  }
 }
 const toggle=$('show-skyhooks'),button=$('refresh-skyhooks');
 try{toggle.checked=localStorage.getItem('adm-skyhooks-overlay')!=='off';}catch{}
 function render(){
  const region=getRegion(),now=Date.now(),fresh=skyhookFeedFresh(checked,error,now);
  const active=fresh?vulnerableInRegion(rows,systems,region,now):[];
  active.forEach(queuePlanet);
  $('skyhook-region').textContent=region||'Selected region';$('skyhook-count').textContent=fresh?String(active.length):'—';
  const groups=new Map();for(const s of active){if(!groups.has(s.systemId))groups.set(s.systemId,[]);groups.get(s.systemId).push(s);}
  map.skyhooks=toggle.checked?groups:new Map();map.draw();if(map.hoverPoint)map.tooltip(map.hoverPoint);
  $('skyhook-status').textContent=busy?'Checking public ESI…':error?'Skyhook feed unavailable · current vulnerability unknown.':!checked?'Waiting for public ESI…':!fresh?'Skyhook data is stale · highlights hidden until refreshed.':'Public ESI · checked '+new Date(checked).toLocaleTimeString()+' · 5m refresh';
  if(checked&&error)$('skyhook-status').textContent+=' Last successful check: '+new Date(checked).toLocaleTimeString()+'.';
  const list=$('skyhook-list');list.replaceChildren();
  if(!fresh)list.append(el('p','A missing highlight does not mean a system is safe.','muted small'));
  else if(!active.length)list.append(el('p','No currently theft-vulnerable skyhooks reported in this region.','muted small'));
  for(const s of active){
   const sys=systems.get(s.systemId),card=el('article','', 'connection-card skyhook-card');
   card.append(el('strong',sys.name),el('p',location(s),'muted small'));
   const sovereignty=map.sov.get(s.systemId);
   const holder=map.sovereigntyLabel?.(sovereignty)??'Sovereignty unavailable';
   const sovLine=el('p','System sovereignty: '+holder+(sovereignty&&map.stale?' (cached/unverified)':''),'muted small skyhook-sovereignty');
   sovLine.title='System sovereignty holder; the public skyhook feed does not identify the skyhook owner.';
   card.append(sovLine);
   card.append(el('p','Window closes '+new Date(s.end).toLocaleTimeString('en-GB',{timeZone:'UTC',hour:'2-digit',minute:'2-digit'})+' UTC · '+Math.max(1,Math.ceil((s.end-now)/60000))+'m remaining','expiry skyhook-color'));
   const show=el('button','Show on map');show.type='button';show.onclick=()=>selectSystem(sys.name,true);card.append(show);list.append(card);
  }
  pumpPlanets();
  button.disabled=busy||Date.now()<next;
  button.title=Date.now()<next?'Next check after '+new Date(next).toLocaleTimeString():'Refresh skyhook feed';
 }
 async function refresh(){
  if(busy||Date.now()<next)return;busy=true;next=Date.now()+SKYHOOK_REFRESH;render();
  try{
   const response=await fetch(SKYHOOK_URL,{cache:'no-store',signal:AbortSignal.timeout(20000)});
   const retry=response.headers.get('Retry-After');
   if(retry){const seconds=Number(retry),until=Number.isFinite(seconds)?Date.now()+seconds*1000:Date.parse(retry);if(Number.isFinite(until))next=Math.max(next,until);}
   if(!response.ok)throw new Error('HTTP '+response.status);
   rows=parseSkyhooks(await response.json());checked=Date.now();error='';
  }catch{error='Unavailable';}
  finally{busy=false;render();}
 }
 toggle.onchange=()=>{try{localStorage.setItem('adm-skyhooks-overlay',toggle.checked?'on':'off');}catch{}render();};
 button.onclick=refresh;
 setInterval(()=>{render();if(document.visibilityState==='visible')refresh();},15000);
 document.addEventListener('visibilitychange',()=>{if(document.visibilityState==='visible'){render();refresh();}});
 render();refresh();return {render};
}
