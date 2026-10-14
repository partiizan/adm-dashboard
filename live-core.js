export function parseConnections(rows,hub,now=Date.now()) {
 if(!Array.isArray(rows))throw new Error('Invalid wormhole response');
 return rows.filter(w=>w.signature_type==='wormhole'&&w.completed!==false&&w.in_system_name&&Date.parse(w.expires_at)>now).map(w=>({hub,system:String(w.in_system_name),region:String(w.in_region_name||''),security:String(w.in_system_class||''),local:String(w.in_signature||'—'),signature:String(w.out_signature||'—'),size:String(w.max_ship_size||'Unknown'),expires:w.expires_at}));
}
export function parseStorms(html,systems){
 const decode=s=>s.replace(/<[^>]*>/g,'').replace(/&amp;/g,'&').replace(/&nbsp;/g,' ').replace(/&#39;/g,"'").replace(/&quot;/g,'"').trim();
 const rows=[...html.matchAll(/<tr\b[^>]*>([\s\S]*?)<\/tr>/gi)].map(r=>[...r[1].matchAll(/<td\b[^>]*>([\s\S]*?)<\/td>/gi)].map(c=>decode(c[1])));
 const storms=rows.filter(c=>c.length>=5&&systems.has(c[1])).map(c=>({region:c[0],system:c[1],name:c[2],type:c[3],reported:c[4]}));
 if(!storms.length)throw new Error('No recognizable storm rows');return storms;
}
export function activeConnections(rows,hub,query,now=Date.now()) {return rows.filter(w=>Date.parse(w.expires)>now&&(!hub||w.hub===hub)&&`${w.system} ${w.region} ${w.local} ${w.signature}`.toLowerCase().includes(query.trim().toLowerCase())).sort((a,b)=>Date.parse(a.expires)-Date.parse(b.expires));}
