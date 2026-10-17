const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const crypto = require('node:crypto');
const draft = require('./draft-release.cjs');

function fixture(t, options = {}) {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'bbtools-release-'));
  t.after(() => fs.rmSync(root, { recursive: true, force: true }));
  for (const directory of ['src/BBTools', 'release-artifacts', 'docs/releases']) fs.mkdirSync(path.join(root, directory), { recursive: true });
  fs.writeFileSync(path.join(root, 'src/BBTools/BBTools.csproj'), '<Project><Version>0.2.0</Version></Project>');
  fs.writeFileSync(path.join(root, 'docs/releases/v0.2.0.md'), 'Reviewed release notes');
  const name = 'BBTools-v0.2.0-win-x64.zip', archive = Buffer.from('fixture ZIP');
  fs.writeFileSync(path.join(root, 'release-artifacts', name), archive);
  fs.writeFileSync(path.join(root, 'release-artifacts/SHA256SUMS.txt'), `${crypto.createHash('sha256').update(archive).digest('hex')}  ${name}\n`);
  const calls = [];
  const method = (name, result) => async args => { calls.push([name, args]); return { data: result }; };
  const github = { rest: { git: {
    getRef: options.reference ? method('getRef', { object: options.reference }) : async () => { throw { status: 404 }; },
    getTag: method('getTag', { object: { type: 'commit', sha: 'commit' } }),
    createTag: method('createTag', { sha: 'annotated' }), createRef: method('createRef', {}),
  }, repos: {
    listReleases: 'releases', listReleaseAssets: 'assets',
    createRelease: method('createRelease', { id: 12, html_url: 'draft' }),
    uploadReleaseAsset: method('uploadReleaseAsset', {}), deleteReleaseAsset: method('deleteReleaseAsset', {}),
  } }, paginate: async name => name === 'releases' ? options.releases ?? [] : options.assets ?? [] };
  return { root, github, context: { repo: { owner: 'owner', repo: 'repo' }, sha: 'commit' }, core: { info() {} }, calls };
}

test('creates an annotated tag at the checked commit and a draft with both verified assets', async t => {
  const f = fixture(t); await draft(f);
  assert.deepEqual(f.calls.map(x => x[0]), ['createTag', 'createRef', 'createRelease', 'uploadReleaseAsset', 'uploadReleaseAsset']);
  assert.equal(f.calls[0][1].object, 'commit');
  assert.equal(f.calls[1][1].sha, 'annotated');
  assert.equal(f.calls[2][1].draft, true);
  assert.equal(f.calls[2][1].body, 'Reviewed release notes');
  assert.deepEqual(f.calls.slice(3).map(x => x[1].name), ['BBTools-v0.2.0-win-x64.zip', 'SHA256SUMS.txt']);
});
test('checksum failure happens before any GitHub mutation', async t => {
  const f = fixture(t); fs.appendFileSync(path.join(f.root, 'release-artifacts/BBTools-v0.2.0-win-x64.zip'), 'changed');
  await assert.rejects(draft(f), /checksum/); assert.equal(f.calls.length, 0);
});
test('existing published release is left unchanged', async t => {
  const f = fixture(t, { releases: [{ tag_name: 'v0.2.0', draft: false }] });
  await draft(f); assert.equal(f.calls.length, 0);
});
test('a tag on another commit is never moved', async t => {
  const f = fixture(t, { reference: { type: 'commit', sha: 'other' } });
  await assert.rejects(draft(f), /another commit/); assert.deepEqual(f.calls.map(x => x[0]), ['getRef']);
});
test('an interrupted draft resumes missing uploads without creating another release', async t => {
  const f = fixture(t, { releases: [{ tag_name: 'v0.2.0', draft: true, id: 12 }], reference: { type: 'tag', sha: 'annotated' } });
  await draft(f);
  assert.deepEqual(f.calls.map(x => x[0]), ['getRef', 'getTag', 'uploadReleaseAsset', 'uploadReleaseAsset']);
});
test('retry refreshes a differing draft asset only at the same tagged commit', async t => {
  const f = fixture(t, { releases: [{ tag_name: 'v0.2.0', draft: true, id: 12 }], reference: { type: 'commit', sha: 'commit' },
    assets: [{ id: 99, name: 'BBTools-v0.2.0-win-x64.zip', state: 'uploaded', size: 1 }] });
  await draft(f);
  assert.deepEqual(f.calls.map(x => x[0]), ['getRef', 'deleteReleaseAsset', 'uploadReleaseAsset', 'uploadReleaseAsset']);
  assert.equal(f.calls[1][1].asset_id, 99);
});
