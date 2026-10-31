const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const crypto = require('node:crypto');
const metadata = require('./linux-release-metadata.cjs');
const draft = require('./draft-linux-release.cjs');

function fixture(t) {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'bbtools-linux-release-'));
  t.after(() => fs.rmSync(root, { recursive: true, force: true }));
  for (const project of ['LinuxDesktop', 'LinuxCore', 'LinuxProbe']) {
    fs.mkdirSync(path.join(root, `src/BBTools.${project}`), { recursive: true });
    fs.writeFileSync(path.join(root, `src/BBTools.${project}/BBTools.${project}.csproj`), '<Version>0.6.6-linux-beta.1</Version>');
  }
  fs.mkdirSync(path.join(root, 'src/BBTools'), { recursive: true });
  fs.writeFileSync(path.join(root, 'src/BBTools/BBTools.csproj'), '<Version>0.6.6</Version>');
  fs.mkdirSync(path.join(root, 'docs/releases'), { recursive: true });
  fs.writeFileSync(path.join(root, 'docs/releases/v0.6.6-linux-beta.1.md'), '# Linux Beta\n\nNot tested in-game.');
  const m = metadata(root), directory = path.join(root, 'linux-release-artifacts');
  fs.mkdirSync(directory);
  const sums = m.assets.map(name => {
    const data = Buffer.from(`fixture ${name}`); fs.writeFileSync(path.join(directory, name), data);
    return `${crypto.createHash('sha256').update(data).digest('hex')}  ${name}`;
  });
  fs.writeFileSync(path.join(directory, m.checksum), sums.join('\n') + '\n');
  const calls = [];
  const github = { paginate: async () => [], rest: {
    repos: { listReleases() {}, listReleaseAssets() {},
      createRelease: async args => { calls.push(['release', args]); return { data: { id: 10, html_url: 'https://github.com/JamesCicada/bb-tools/releases/tag/' + m.tag } }; },
      uploadReleaseAsset: async args => calls.push(['upload', args]),
      deleteReleaseAsset: async () => assert.fail('No existing asset should be deleted') },
    git: { getRef: async () => { throw { status: 404 }; },
      createTag: async args => { calls.push(['tag', args]); return { data: { sha: 'tag-object' } }; },
      createRef: async args => calls.push(['ref', args]), getTag: async () => assert.fail('Unexpected tag lookup') }
  } };
  return { root, github, context: { repo: { owner: 'JamesCicada', repo: 'bb-tools' }, sha: 'commit-sha' }, core: { info() {} }, calls };
}
test('Linux release is separate, explicit beta, not latest and contains all verified assets', async t => {
  const f = fixture(t); await draft(f);
  const release = f.calls.find(c => c[0] === 'release')[1];
  assert.equal(release.tag_name, 'v0.6.6-linux-beta.1');
  assert.equal(release.prerelease, true); assert.equal(release.draft, true); assert.equal(release.make_latest, 'false');
  assert.match(release.name, /NOT TESTED/); assert.match(release.body, /Not tested in-game/);
  assert.equal(f.calls.filter(c => c[0] === 'upload').length, 4);
  assert.equal(f.calls.find(c => c[0] === 'ref')[1].ref, 'refs/tags/v0.6.6-linux-beta.1');
});
test('Linux checksum mismatch stops before tag/release mutations', async t => {
  const f = fixture(t); fs.appendFileSync(path.join(f.root, 'linux-release-artifacts', metadata(f.root).assets[0]), 'corrupt');
  await assert.rejects(draft(f), /checksum mismatch/); assert.deepEqual(f.calls, []);
});
test('Linux existing tag cannot be moved to a different commit', async t => {
  const f = fixture(t); f.github.rest.git.getRef = async () => ({ data: { object: { type: 'commit', sha: 'another-commit' } } });
  await assert.rejects(draft(f), /another commit/); assert.deepEqual(f.calls, []);
});
test('Linux existing stable release is rejected', async t => {
  const f = fixture(t); f.github.paginate = async () => [{ tag_name: metadata(f.root).tag, prerelease: false }];
  await assert.rejects(draft(f), /prerelease/); assert.deepEqual(f.calls, []);
});
test('Published Linux beta is immutable on rerun', async t => {
  const f = fixture(t); f.github.paginate = async () => [{ tag_name: metadata(f.root).tag, prerelease: true, draft: false }];
  await draft(f); assert.deepEqual(f.calls, []);
});
test('Linux metadata rejects missing test disclaimer and inconsistent project versions', t => {
  const f = fixture(t);
  fs.writeFileSync(path.join(f.root, 'docs/releases/v0.6.6-linux-beta.1.md'), 'Beta ready');
  assert.throws(() => metadata(f.root), /not tested in-game/);
  fs.writeFileSync(path.join(f.root, 'src/BBTools.LinuxCore/BBTools.LinuxCore.csproj'), '<Version>0.6.5</Version>');
  assert.throws(() => metadata(f.root), /versions must match/);
});
test('Linux checksum manifest cannot add an unverified file', async t => {
  const f = fixture(t); fs.appendFileSync(path.join(f.root, 'linux-release-artifacts/SHA256SUMS-linux.txt'), 'unexpected\n');
  await assert.rejects(draft(f), /unexpected entries/); assert.deepEqual(f.calls, []);
});

test('stable Linux versions cannot create a separate prerelease tag',async t=>{
 const f=fixture(t);
 for(const project of ['LinuxDesktop','LinuxCore','LinuxProbe'])fs.writeFileSync(path.join(f.root,`src/BBTools.${project}/BBTools.${project}.csproj`),'<Version>0.7.0</Version>');
 fs.writeFileSync(path.join(f.root,'src/BBTools/BBTools.csproj'),'<Version>0.7.0</Version>');
 fs.writeFileSync(path.join(f.root,'docs/releases/v0.7.0.md'),'v0.7.0 unified desktop release');
 await assert.rejects(draft(f),/unified desktop release/);assert.equal(f.calls.length,0);
});
