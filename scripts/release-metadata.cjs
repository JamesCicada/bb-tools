const fs = require('node:fs');
const path = require('node:path');

module.exports = function releaseMetadata(root) {
  const project = fs.readFileSync(path.join(root, 'src/BBTools/BBTools.csproj'), 'utf8');
  const version = project.match(/<Version>(\d+\.\d+\.\d+)<\/Version>/)?.[1];
  if (!version) throw new Error('Expected a three-part project version.');
  const tag = `v${version}`;
  const file = path.join(root, 'docs/releases', `${tag}.md`);
  if (!fs.existsSync(file)) throw new Error(`Missing release notes: docs/releases/${tag}.md. Add them before releasing ${tag}.`);
  const notes = fs.readFileSync(file, 'utf8');
  if (!notes.trim()) throw new Error(`Empty release notes: docs/releases/${tag}.md.`);
  return { version, tag, notes };
};
