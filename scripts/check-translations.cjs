const fs = require('node:fs');
const path = require('node:path');

function placeholders(text) {
  const result = [];
  for (let i = 0; i < text.length; i++) {
    if ((text[i] === '{' && text[i + 1] === '{') || (text[i] === '}' && text[i + 1] === '}')) { i++; continue; }
    if (text[i] === '}') throw new Error('Unmatched closing brace');
    if (text[i] !== '{') continue;
    const end = text.indexOf('}', i + 1);
    if (end < 0) throw new Error('Unmatched opening brace');
    const token = text.slice(i + 1, end);
    if (!/^\d+(?:,-?\d+)?(?::[^{}]+)?$/.test(token)) throw new Error(`Invalid placeholder {${token}}`);
    result.push(token); i = end;
  }
  return result.sort();
}
function validate(source, target) {
  const errors = [];
  if (!target || typeof target !== 'object' || Array.isArray(target)) return ['Expected a flat JSON object'];
  for (const [key, value] of Object.entries(target)) {
    if (!Object.hasOwn(source, key)) { errors.push(`Unknown key: ${key}`); continue; }
    if (typeof value !== 'string') { errors.push(`Not a string: ${key}`); continue; }
    if (!value.trim()) continue; // Empty entries deliberately fall back to English.
    try {
      if (JSON.stringify(placeholders(source[key])) !== JSON.stringify(placeholders(value))) errors.push(`Changed placeholders: ${key}`);
    } catch (e) { errors.push(`${key}: ${e.message}`); }
  }
  return errors;
}
function checkRepository(root) {
  const folder = path.join(root, 'src/BBTools/Localization/Locales');
  const source = JSON.parse(fs.readFileSync(path.join(folder, 'en.json'), 'utf8'));
  const errors = validate(source, source);
  for (const [key, value] of Object.entries(source)) if (typeof value !== 'string' || !value.trim()) errors.push(`Empty English source: ${key}`);
  const reports = [];
  const languages = fs.readFileSync(path.join(root, 'src/BBTools/Localization/Translations.cs'), 'utf8');
  const codes = [...languages.matchAll(/new\("([\w-]+)",/g)].map(m => m[1]);
  for (const code of codes) if (!fs.existsSync(path.join(folder, `${code}.json`))) errors.push(`Missing registered catalog: ${code}`);
  for (const file of fs.readdirSync(folder).filter(f => f.endsWith('.json'))) {
    if (!codes.includes(path.basename(file, '.json'))) errors.push(`Unregistered catalog: ${file}; check Crowdin locale mapping`);
    const values = JSON.parse(fs.readFileSync(path.join(folder, file), 'utf8'));
    errors.push(...validate(source, values).map(e => `${file}: ${e}`));
    reports.push(`${file}: ${Object.values(values).filter(v => typeof v === 'string' && v.trim()).length}/${Object.keys(source).length}`);
  }
  const codeRoot = path.join(root, 'src/BBTools');
  for (const file of fs.readdirSync(codeRoot).filter(f => f.endsWith('.cs') || f.endsWith('.xaml'))) {
    const text = fs.readFileSync(path.join(codeRoot, file), 'utf8');
    for (const match of text.matchAll(/(?:DynamicResource L\.|Translations\.(?:T|Format)\(")([\w.]+)/g))
      if (!Object.hasOwn(source, match[1])) errors.push(`${file}: missing source key ${match[1]}`);
  }
  return { errors, reports };
}
module.exports = { placeholders, validate, checkRepository };
if (require.main === module) {
  const result = checkRepository(path.resolve(__dirname, '..'));
  console.log(result.reports.join('\n'));
  if (result.errors.length) { console.error(result.errors.join('\n')); process.exitCode = 1; }
}
