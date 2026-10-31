const fs = require('node:fs');
const path = require('node:path');

module.exports = function linuxReleaseMetadata(root) {
  const readVersion = project => fs.readFileSync(path.join(root, `src/BBTools.${project}/BBTools.${project}.csproj`), 'utf8').match(/<Version>([^<]+)<\/Version>/)?.[1];
  const version = readVersion('LinuxDesktop');
  const beta = /-linux-beta\./.test(version || '');
  if (!/^\d+\.\d+\.\d+(?:-linux-beta\.[1-9]\d*)?$/.test(version || '')) throw new Error('Expected a synchronized release or explicit Linux beta version.');
  if (['LinuxCore', 'LinuxProbe'].some(project => readVersion(project) !== version)) throw new Error('Linux project versions must match.');
  const windows = fs.readFileSync(path.join(root, 'src/BBTools/BBTools.csproj'), 'utf8').match(/<Version>([^<]+)<\/Version>/)?.[1];
  if (beta ? !version.startsWith(`${windows}-linux-beta.`) : version !== windows) throw new Error('Linux must match the Windows base version.');
  const tag = `v${version}`;
  const notes = fs.readFileSync(path.join(root, 'docs/releases', `${tag}.md`), 'utf8');
  if (beta && (!/not tested in-game/i.test(notes) || !/beta/i.test(notes))) throw new Error('Linux release notes must explicitly state Beta and not tested in-game.');
  const assets = [`BBTools-${tag}-linux-x64.tar.gz`, `BBTools-${tag}-linux-x64.zip`, `BBTools-LinuxProbe-${tag}-linux-x64.tar.gz`];
  return { version, tag, notes, assets, checksum: 'SHA256SUMS-linux.txt' };
};
