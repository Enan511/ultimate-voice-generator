export function sanitize(text = "") {
  return text.normalize("NFC").replace(/\s+/gu, " ").trim();
}
export function pronounce(text, rules = []) {
  if (!rules.length) return text;
  const escape = (s) => s.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
  const map = new Map(rules.map((r) => [r.word.toLowerCase(), r.sayAs]));
  const keys = rules
    .map((r) => r.word)
    .filter(Boolean)
    .sort((a, b) => b.length - a.length);
  if (!keys.length) return text;
  return text.replace(
    new RegExp(
      `(?<![\\p{L}\\p{N}])(?:${keys.map(escape).join("|")})(?![\\p{L}\\p{N}])`,
      "giu",
    ),
    (m) => map.get(m.toLowerCase()),
  );
}
export function stageLines(raw = "") {
  return raw
    .split(/\r\n|\r|\n/u)
    .map(sanitize)
    .filter(Boolean);
}

