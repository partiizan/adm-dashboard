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
export function makeIntelParser(universe) {
  const escape = s => s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  const names = new RegExp("(?<![\\p{L}\\p{N}'-])(?:" + universe.systems.map(s => s.name).sort((a,b) => b.length-a.length).map(escape).join('|') + ")(?![\\p{L}\\p{N}'-])", 'giu');
  return (line, source = 'Manual', allowPlain = false, now = Date.now()) => {
    if (!line || line.length > 8192) return null;
    const match = line.match(/^\s*\[\s*(\d{4}\.\d{2}\.\d{2} \d{2}:\d{2}:\d{2})\s*\]\s*(.*?)\s*>\s*(.*)$/);
    if (!match && !allowPlain) return null;
    const speaker = match ? match[2] : 'Manual report', message = match ? match[3] : line;
    if (speaker.toLowerCase() === 'eve system') return null;
    const iso = match ? match[1].replaceAll('.', '-').replace(' ', 'T') + 'Z' : null;
    const time = iso ? Date.parse(iso) : now;
    if (!Number.isFinite(time) || (iso && new Date(time).toISOString().slice(0,19) !== iso.slice(0,19))) return null;
    const systems = [...new Set([...message.matchAll(names)].map(m => universe.lookup(m[0]).name))];
    if (!systems.length) return null;
    const clear = /\b(clear|clr)\b/i.test(message) && !/[?]|\b(not|no|never|isn't|isnt|hostile|hostiles|neut|neuts|red|reds)\b/i.test(message);
    return { time, speaker, message, systems, clear, source };
  };
}
export function decodeLog(buffer) {
  const bytes = new Uint8Array(buffer);
  const encoding = bytes[0] === 255 && bytes[1] === 254 ? 'utf-16le' : bytes[0] === 254 && bytes[1] === 255 ? 'utf-16be' : 'utf-8';
  return new TextDecoder(encoding).decode(bytes);
}
export function recentReports(reports, now = Date.now()) {
  const result = new Map();
  for (const r of [...reports].sort((a,b) => a.time-b.time)) {
    if (r.time < now - 15*60000 || r.time > now + 60000) continue;
    for (const name of r.systems) result.set(name, r);
  }
  return result;
}
export const securityColor = n => n >= .45 ? '#74d6bd' : n > 0 ? '#eac076' : '#cb8fa5';
