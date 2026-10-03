export function makeUniverse(data) {
  const systems = new Map(data.systems.map(s => [s.name.toLowerCase(), s]));
  return { ...data, lookup: name => systems.get(String(name).trim().toLowerCase()) };
}
export function route(universe, from, to, highOnly = false) {
  const start = universe.lookup(from), end = universe.lookup(to);
  if (!start || !end || (highOnly && (start.security < .45 || end.security < .45))) return [];
  const previous = new Map([[start.name, null]]), queue = [start];
  for (let i = 0; i < queue.length; i++) {
    const current = queue[i];
    if (current.name === end.name) {
      const result = [];
      for (let name = end.name; name !== null; name = previous.get(name)) result.push(universe.lookup(name));
      return result.reverse();
    }
    for (const name of current.jumps) {
      const next = universe.lookup(name);
      if (!next || previous.has(next.name) || (highOnly && next.security < .45)) continue;
      previous.set(next.name, current.name); queue.push(next);
    }
  }
  return [];
}
export function parseSovereignty(data) {
  if (!Array.isArray(data?.solar_systems) || !data.solar_systems.length) throw new Error('Unexpected ESI response');
  return new Map(data.solar_systems.map(row => {
    if (!Number.isInteger(row.solar_system_id)) throw new Error('Invalid system identifier');
    const alliance = row.claim?.alliance, d = alliance?.development, value = d?.activity_defense_multiplier;
    const index = n => Number.isInteger(n) && n >= 0 && n <= 5 ? n : null;
    return [row.solar_system_id, { kind: alliance ? 'alliance' : row.claim?.faction ? 'faction' : row.claim ? 'unknown' : 'unclaimed',
      adm: typeof value === 'number' && Number.isFinite(value) && value >= 1 && value <= 6 ? value : null,
      military: index(d?.military_level), industrial: index(d?.industrial_level), strategic: index(d?.strategic_level), capital: alliance?.is_capital_system === true }];
  }));
}
export const securityColor = n => n >= .45 ? '#74d6bd' : n > 0 ? '#eac076' : '#cb8fa5';
