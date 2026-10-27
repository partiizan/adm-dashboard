export const SKYHOOK_URL='https://esi.evetech.net/skyhooks/raidable?compatibility_date=2026-05-19';
export const SKYHOOK_REFRESH=300000;
export const SKYHOOK_MAX_AGE=600000;
export function parseSkyhooks(data){
 if(!Array.isArray(data?.skyhooks))throw new Error('Unexpected skyhook response');
 const result=new Map();
 for(const s of data.skyhooks){
  const start=Date.parse(s?.theft_vulnerability?.start),end=Date.parse(s?.theft_vulnerability?.end);
  if(!Number.isSafeInteger(s?.planet_id)||s.planet_id<=0||!Number.isSafeInteger(s?.solar_system_id)||s.solar_system_id<=0||!Number.isFinite(start)||!Number.isFinite(end)||end<=start)throw new Error('Invalid skyhook entry');
  result.set(s.planet_id,{planetId:s.planet_id,systemId:s.solar_system_id,start,end});
 }
 return [...result.values()];
}
export function vulnerableInRegion(rows,systems,region,now=Date.now()){
 return rows.filter(s=>s.start<=now&&now<s.end&&systems.get(s.systemId)?.region===region).sort((a,b)=>a.end-b.end||a.planetId-b.planetId);
}
export function skyhookFeedFresh(checked,error,now=Date.now()){return !error&&Number.isFinite(checked)&&checked>0&&now>=checked&&now-checked<=SKYHOOK_MAX_AGE;}

export function planetLabel(name,system){
 const prefix=system+' ',suffix=name.startsWith(prefix)?name.slice(prefix.length):'';
 if(/^[IVXLCDM]+$/.test(suffix)){
  const digits={I:1,V:5,X:10,L:50,C:100,D:500,M:1000};let value=0;
  for(let i=0;i<suffix.length;i++)value+=digits[suffix[i]]<(digits[suffix[i+1]]||0)?-digits[suffix[i]]:digits[suffix[i]];
  return system+': Planet '+value;
 }
 return name;
}
