import test from 'node:test';
import assert from 'node:assert/strict';
import {parseSkyhooks,vulnerableInRegion,skyhookFeedFresh} from '../skyhooks-core.js';
const now=Date.parse('2026-10-27T20:00:00Z'),systems=new Map([[1,{region:'Querious'}],[2,{region:'Delve'}]]);
const row=(planet,system,start,end)=>({planet_id:planet,solar_system_id:system,theft_vulnerability:{start:new Date(now+start).toISOString(),end:new Date(now+end).toISOString()}});
test('Only active windows in the exact region; start inclusive, end exclusive',()=>{
 const parsed=parseSkyhooks({skyhooks:[row(1,1,0,60000),row(2,1,-60000,0),row(3,1,1,60000),row(4,2,-1000,60000),row(5,999,-1000,60000)]});
 assert.deepEqual(vulnerableInRegion(parsed,systems,'Querious',now).map(s=>s.planetId),[1]);
 assert.equal(vulnerableInRegion(parsed,systems,'Querious',now+60000).length,0);
});
test('Empty response valid; invalid or incomplete feed fails rather than reporting clear',()=>{
 assert.deepEqual(parseSkyhooks({skyhooks:[]}),[]);
 for(const data of [[],{}, {skyhooks:[{planet_id:1}]},{skyhooks:[row(1,1,100,0)]}])assert.throws(()=>parseSkyhooks(data));
});
test('Duplicate planets deduplicated; multiple skyhooks per system retained',()=>{
 assert.equal(parseSkyhooks({skyhooks:[row(1,1,0,5000),row(1,1,0,5000),row(2,1,0,5000)]}).length,2);
});
test('Failed, absent, future or stale checks cannot generate current highlights',()=>{
 assert.equal(skyhookFeedFresh(now-1000,'',now),true);
 for(const [checked,error] of [[0,''],[now+1,''],[now-600001,''],[now,'unavailable']])assert.equal(skyhookFeedFresh(checked,error,now),false);
});
