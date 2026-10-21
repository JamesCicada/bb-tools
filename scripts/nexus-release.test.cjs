const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const crypto = require('node:crypto');
const prepare = require('./nexus-release.cjs');

function fixture(t, options = {}) {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'bbtools-nexus-'));
  t.after(() => fs.rmSync(root, { recursive: true, force: true }));
  fs.mkdirSync(path.join(root, 'src/BBTools'), { recursive: true });
  fs.mkdirSync(path.join(root, 'docs/releases'), { recursive: true });
  fs.writeFileSync(path.join(root, 'src/BBTools/BBTools.csproj'), '<Version>0.6.1</Version>');
  fs.writeFileSync(path.join(root, 'docs/releases/v0.6.1.md'), '# BB Tools v0.6.1\n\n- Fix Discord game data.');
  const archive = Buffer.from('mock release bytes');
  const digest = crypto.createHash('sha256').update(archive).digest('hex');
  const calls = [], notices = [];
  const context = { eventName: 'release', repo: { owner: 'JamesCicada', repo: 'bb-tools' }, payload: { release: { tag_name: 'v0.6.1' } } };
  const release = { draft: false, prerelease: false, assets: [
    { id: 1, name: 'BBTools-v0.6.1-win-x64.zip', state: 'uploaded' },
    { id: 2, name: 'SHA256SUMS.txt', state: 'uploaded' },
  ], ...options.release };
  return { root, context, env: { NEXUSMODS_API_KEY: 'test-secret-not-real' }, calls, notices,
    core: { notice: text => notices.push(text) },
    github: { rest: { repos: {
      getReleaseByTag: async () => ({ data: release }),
      getReleaseAsset: async ({ asset_id }) => ({ data: asset_id === 1 ? archive : Buffer.from(`${options.badChecksum ? '0'.repeat(64) : digest}  BBTools-v0.6.1-win-x64.zip\n`) }),
    } } },
    fetcher: async (url, request) => {
      assert.equal(request.method || 'GET', 'GET');
      assert.equal(request.headers.apikey, 'test-secret-not-real');
      calls.push(url);
      const endpoint = url.replace('https://api.nexusmods.com/v3', '');
      const responses = {
        '/games/bloodborne/mods/614': { id: 'bbtools-mod' },
        '/games/bloodborne/mod-file-versions/3154': { file: { id: 'bbtools-file' } },
        '/mods/bbtools-mod/files': { mod_files: [{ id: options.wrongMod ? 'another-file' : 'bbtools-file', name: 'BB Tools' }] },
        '/mod-files/bbtools-file/versions': { versions: options.duplicate ? [{ version: '0.6.1' }] : [{ version: '0.5.0' }] },
      };
      assert.ok(responses[endpoint], `Unexpected endpoint ${endpoint}`);
      return { ok: !options.apiFailure, status: options.apiFailure ? 401 : 200, json: async () => ({ data: responses[endpoint] }) };
    },
  };
}

test('published stable releases resolve the correct mod/file and verify the original GitHub asset', async t => {
  const f = fixture(t), result = await prepare(f);
  assert.equal(result.upload, 'true');
  assert.equal(result.file_id, 'bbtools-file');
  assert.equal(result.mod_id, 'bbtools-mod');
  assert.equal(result.version, '0.6.1');
  assert.ok(fs.existsSync(result.filename));
  assert.match(result.changelog, /Fix Discord game data/);
  assert.equal(f.calls.length, 4);
});
test('missing API credentials skip Nexus without preventing the GitHub release', async t => {
  const f = fixture(t); f.env = {};
  assert.equal((await prepare(f)).configured, 'false');
  assert.equal(f.calls.length, 0);
});
test('automatic uploads reject drafts and prereleases before Nexus requests', async t => {
  for (const release of [{ draft: true }, { prerelease: true }]) {
    const f = fixture(t, { release });
    await assert.rejects(prepare(f), /published stable/);
    assert.equal(f.calls.length, 0);
  }
});
test('manual validation accepts a draft but never authorizes upload', async t => {
  const f = fixture(t, { release: { draft: true } });
  f.context = { ...f.context, eventName: 'workflow_dispatch', payload: { inputs: { tag: 'v0.6.1', validate_only: 'true' } } };
  const result = await prepare(f);
  assert.equal(result.upload, 'false');
  assert.equal(result.validation_only, 'true');
  assert.equal(result.configured, 'true');
});
test('a manual upload still cannot publish a draft', async t => {
  const f = fixture(t, { release: { draft: true } });
  f.context = { ...f.context, eventName: 'workflow_dispatch', payload: { inputs: { tag: 'v0.6.1', validate_only: 'false' } } };
  await assert.rejects(prepare(f), /published stable/);
});
test('read-only validation can check Nexus when GitHub hides draft metadata', async t => {
  const f = fixture(t);
  f.context = { ...f.context, eventName: 'workflow_dispatch', payload: { inputs: { tag: 'v0.6.1', validate_only: 'true' } } };
  f.github.rest.repos.getReleaseByTag = async () => { throw Object.assign(new Error('Not Found'), { status: 404 }); };
  const result = await prepare(f);
  assert.equal(result.upload, 'false'); assert.equal(result.configured, 'true'); assert.equal(result.assets_verified, 'false');
  assert.equal(f.calls.length, 4);
});
test('an inaccessible release never falls back when upload is requested', async t => {
  const f = fixture(t);
  f.github.rest.repos.getReleaseByTag = async () => { throw Object.assign(new Error('Not Found'), { status: 404 }); };
  await assert.rejects(prepare(f), /Not Found/); assert.equal(f.calls.length, 0);
});
test('existing Nexus versions skip duplicate files and changelog appends', async t => {
  const result = await prepare(fixture(t, { duplicate: true }));
  assert.equal(result.upload, 'false'); assert.equal(result.duplicate, 'true');
});
test('checksum errors cannot authorize an upload', async t => {
  await assert.rejects(prepare(fixture(t, { badChecksum: true })), /checksum mismatch/);
});
test('a file belonging to another Nexus mod is rejected', async t => {
  await assert.rejects(prepare(fixture(t, { wrongMod: true })), /does not belong/);
});
test('API errors contain no API key or response body', async t => {
  await assert.rejects(prepare(fixture(t, { apiFailure: true })), error => {
    assert.match(error.message, /401/); assert.doesNotMatch(error.message, /test-secret/); return true;
  });
});
test('source/tag version mismatch stops before credentials are used', async t => {
  const f = fixture(t); f.context.payload.release.tag_name = 'v0.6.2';
  await assert.rejects(prepare(f), /source does not match/);
  assert.equal(f.calls.length, 0);
});
test('malformed tag input is rejected', async t => {
  const f = fixture(t); f.context.payload.release.tag_name = '../main';
  await assert.rejects(prepare(f), /version tag/);
});
