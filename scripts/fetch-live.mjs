import {readFile,writeFile} from 'node:fs/promises';
import {parseConnections,parseStorms} from '../live-core.js';
const universe=JSON.parse(await readFile('data/universe.json','utf8'));
const systems=new Set(universe.systems.map(s=>s.name));
async function get(url){const r=await fetch(url,{signal:AbortSignal.timeout(25000)});if(!r.ok)throw new Error(`HTTP ${r.status}`);return r;}
let previous={};
try{previous=await (await get('https://partiizan.github.io/adm-dashboard/data/live.json')).json();}catch{}
const result={generated:new Date().toISOString()};
await Promise.all(['Thera','Turnur','storms'].map(async name=>{
 try{
 const rows=name==='storms'?parseStorms(await (await get('https://evescoutrescue.com/home/stormtrack.php')).text(),systems):parseConnections(await (await get('https://api.eve-scout.com/v2/public/signatures?system_name='+name)).json(),name);
 result[name]={checked:new Date().toISOString(),rows,error:null};
 }catch(e){result[name]={checked:previous[name]?.checked??null,rows:previous[name]?.rows??[],error:'Source unavailable; retained snapshot may be stale'};console.warn(name+': '+e.message);}
}));
await writeFile('data/live.json',JSON.stringify(result));
