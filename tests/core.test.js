import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {makeUniverse,route,parseSovereignty} from '../core.js';
const u=makeUniverse(JSON.parse(readFileSync(new URL('../data/universe.json',import.meta.url))));
test('real universe routing and invalid destinations',()=>{assert.ok(u.systems.length>5000);assert.equal(u.lookup(' jita ').name,'Jita');assert.deepEqual(route(u,'Jita','Perimeter').map(s=>s.name),['Jita','Perimeter']);assert.equal(route(u,'Jita','1DQ1-A',true).length,0);assert.equal(route(u,'unknown','Jita').length,0);assert.equal(route(u,'Jita','Jita').length,1);});
test('sovereignty distinguishes absent ADM, NPC and zero index',()=>{const s=parseSovereignty({solar_systems:[{solar_system_id:1,claim:{alliance:{development:{activity_defense_multiplier:4.1,military_level:4,industrial_level:0,strategic_level:5}}}},{solar_system_id:2,claim:{faction:{faction_id:1}}},{solar_system_id:3,claim:{alliance:{development:{activity_defense_multiplier:99}}}}]});assert.equal(s.get(1).adm,4.1);assert.equal(s.get(1).industrial,0);assert.equal(s.get(2).kind,'faction');assert.equal(s.get(2).adm,null);assert.equal(s.get(3).adm,null);assert.throws(()=>parseSovereignty({}));});
