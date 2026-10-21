const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');

// Read-only preflight. Nexus mutations belong exclusively to the pinned official
// upload action, after release/ownership/checksum/duplicate checks have passed.
module.exports = async function prepare({ github, context, core, root = process.cwd(), env = process.env, fetcher = fetch }) {
  const dryRun = context.eventName === 'workflow_dispatch' && context.payload.inputs?.validate_only !== 'false';
  const tag = context.eventName === 'release' ? context.payload.release?.tag_name : context.payload.inputs?.tag;
  if (!/^v\d+\.\d+\.\d+$/.test(tag || '')) throw new Error('Select a version tag such as v0.6.1.');
  const { data: release } = await github.rest.repos.getReleaseByTag({ ...context.repo, tag });
  if (release.prerelease || (release.draft && !dryRun)) throw new Error('Nexus uploads require a published stable GitHub release.');
  const metadata = require('./release-metadata.cjs')(root);
  if (metadata.tag !== tag) throw new Error('Checked-out source does not match the selected release version.');
  if (!env.NEXUSMODS_API_KEY) {
    core.notice('Nexus upload is not configured. Add the repository secret NEXUSMODS_API_KEY; GitHub release creation is unaffected.');
    return { upload: 'false', configured: 'false' };
  }
  const get = async endpoint => {
    const response = await fetcher(`https://api.nexusmods.com/v3${endpoint}`, {
      headers: { apikey: env.NEXUSMODS_API_KEY, Accept: 'application/json', 'User-Agent': 'BBTools-Release' },
      signal: AbortSignal.timeout(30000),
    });
    if (!response.ok) throw new Error(`Nexus preflight failed (${response.status}) at ${endpoint}. Check API permissions and the configured file.`);
    const body = await response.json();
    if (!body.data) throw new Error('Unexpected Nexus API response. No upload was attempted.');
    return body.data;
  };
  const id = value => {
    if (typeof value !== 'string' || !/^[A-Za-z0-9_-]{1,128}$/.test(value)) throw new Error('Invalid Nexus identifier.');
    return value;
  };
  // This stable seed is the main v0.5.0 file's public version-history ID on
  // https://www.nexusmods.com/bloodborne/mods/614?tab=files
  const mod = await get('/games/bloodborne/mods/614');
  const seed = await get('/games/bloodborne/mod-file-versions/3154');
  const modId = id(mod.id), fileId = id(seed.file?.id);
  const file = await get(`/mod-files/${fileId}`);
  if (file.mod?.id !== modId) throw new Error('The selected Nexus file does not belong to Bloodborne mod 614.');
  const versions = await get(`/mod-files/${fileId}/versions`);
  if (!Array.isArray(versions.versions)) throw new Error('Nexus version history is unavailable. No upload was attempted.');
  const duplicate = versions.versions.some(entry => entry.version === metadata.version);
  const archiveName = `BBTools-${tag}-win-x64.zip`;
  const assets = release.assets || [];
  const find = name => {
    const matches = assets.filter(asset => asset.name === name && asset.state === 'uploaded');
    if (matches.length !== 1) throw new Error(`Release must contain exactly one uploaded ${name}.`);
    return matches[0];
  };
  const download = async asset => {
    const response = await github.rest.repos.getReleaseAsset({ ...context.repo, asset_id: asset.id, headers: { accept: 'application/octet-stream' } });
    return Buffer.from(response.data);
  };
  const archive = await download(find(archiveName));
  const sums = (await download(find('SHA256SUMS.txt'))).toString('utf8').trim();
  const digest = crypto.createHash('sha256').update(archive).digest('hex');
  if (sums !== `${digest}  ${archiveName}`) throw new Error('GitHub release checksum mismatch. No Nexus upload was attempted.');
  const directory = path.join(root, 'nexus-artifacts');
  fs.mkdirSync(directory, { recursive: true });
  const filename = path.join(directory, archiveName);
  fs.writeFileSync(filename, archive);
  if (duplicate) core.notice(`Nexus already contains version ${metadata.version}; skipping duplicate upload and changelog append.`);
  if (dryRun) core.notice('Validation-only run: destination and release checksum verified; no Nexus changes will be made.');
  const changelog = metadata.notes.replace(/^#+\s*/gm, '').replace(/\*\*/g, '');
  return { upload: String(!dryRun && !duplicate), configured: 'true', file_id: fileId, mod_id: modId,
    filename, version: metadata.version, changelog, duplicate: String(duplicate), validation_only: String(dryRun) };
};
