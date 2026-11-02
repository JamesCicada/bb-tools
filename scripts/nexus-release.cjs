const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');

// Read-only preflight. Nexus mutations belong exclusively to the pinned official
// upload action, after release/ownership/checksum/duplicate checks have passed.
module.exports = async function prepare({ github, context, core, root = process.cwd(), env = process.env, fetcher = fetch }) {
  const dryRun = context.eventName === 'workflow_dispatch' && context.payload.inputs?.validate_only !== 'false';
  const tag = context.eventName === 'release' ? context.payload.release?.tag_name : context.payload.inputs?.tag;
  if (!/^v\d+\.\d+\.\d+$/.test(tag || '')) throw new Error('Select a version tag such as v0.7.0.');
  let release;
  try { release = (await github.rest.repos.getReleaseByTag({ ...context.repo, tag })).data; }
  catch (error) { if (!dryRun || error.status !== 404) throw error; }
  if (release && (release.prerelease || (release.draft && !dryRun))) throw new Error('Nexus uploads require a published stable GitHub release.');
  const metadata = require('./release-metadata.cjs')(root);
  if (metadata.tag !== tag) throw new Error('Checked-out source does not match the selected release version.');
  if (!env.NEXUSMODS_API_KEY) {
    core.notice('Nexus upload is not configured. Add the repository secret NEXUSMODS_API_KEY; GitHub release creation is unaffected.');
    return { upload: 'false', configured: 'false' };
  }

  const [major, minor] = metadata.version.split('.').map(Number);
  const unifiedRelease = major > 0 || minor >= 7;
  const linuxZipSeed = env.NEXUS_LINUX_ZIP_FILE_VERSION_ID;
  const linuxTarSeed = env.NEXUS_LINUX_TARGZ_FILE_VERSION_ID;
  if (unifiedRelease && (!linuxZipSeed || !linuxTarSeed))
    throw new Error('Configure repository variables NEXUS_LINUX_ZIP_FILE_VERSION_ID and NEXUS_LINUX_TARGZ_FILE_VERSION_ID before publishing a shared Windows/Linux release.');
  const targets = [
    {
      key: 'windows', archiveName: `BBTools-${tag}-win-x64.zip`, checksumName: 'SHA256SUMS.txt', seed: env.NEXUS_WINDOWS_FILE_VERSION_ID || '3154',
      display_name: `BB Tools v${metadata.version} — Windows x64`,
      description: 'Windows x64 desktop build. Extract the ZIP and run BBTools.exe. No installer is required.',
      category: 'main', update_mod_version: true, changelog: true,
    },
    ...(unifiedRelease ? [
      {
        key: 'linux_zip', archiveName: `BBTools-${tag}-linux-x64.zip`, checksumName: 'SHA256SUMS-linux.txt', seed: linuxZipSeed,
        display_name: `BB Tools v${metadata.version} — Linux x64 (ZIP)`,
        description: 'Linux x64 desktop build in ZIP format for graphical archive managers. Extract it, make BBTools.LinuxDesktop executable, then launch bbport and connect to bb-probe. Same app as the tar.gz download—choose one.',
        category: 'optional', update_mod_version: false, changelog: false,
      },
      {
        key: 'linux_tar_gz', archiveName: `BBTools-${tag}-linux-x64.tar.gz`, checksumName: 'SHA256SUMS-linux.txt', seed: linuxTarSeed,
        display_name: `BB Tools v${metadata.version} — Linux x64 (tar.gz)`,
        description: 'Linux x64 desktop build in tar.gz format for terminals and Steam Deck. Extract it, make BBTools.LinuxDesktop executable, then launch bbport and connect to bb-probe. Same app as the ZIP download—choose one.',
        category: 'optional', update_mod_version: false, changelog: false,
      },
    ] : []),
  ];
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
  const seedId = value => {
    if (typeof value !== 'string' || !/^\d{1,20}$/.test(value)) throw new Error('Invalid Nexus file-version seed.');
    return value;
  };

  const mod = await get('/games/bloodborne/mods/614');
  const modId = id(mod.id);
  const files = await get(`/mods/${modId}/files`);
  if (!Array.isArray(files.mod_files)) throw new Error('Nexus file list is unavailable. No upload was attempted.');
  for (const target of targets) {
    const seed = await get(`/games/bloodborne/mod-file-versions/${seedId(target.seed)}`);
    target.file_id = id(seed.file?.id);
    if (!files.mod_files.some(file => file.id === target.file_id)) throw new Error(`The selected ${target.key} Nexus file does not belong to Bloodborne mod 614.`);
    const versions = await get(`/mod-files/${target.file_id}/versions`);
    if (!Array.isArray(versions.versions)) throw new Error(`Nexus version history is unavailable for ${target.key}. No upload was attempted.`);
    target.duplicate = versions.versions.some(entry => entry.version === metadata.version);
  }
  if (!release) {
    core.notice('Nexus credentials and all configured file lineages were verified. This read-only GitHub token cannot see a draft release; asset/checksum verification will run after publication. No Nexus changes were made.');
    return { upload: 'false', configured: 'true', assets_verified: 'false', validation_only: 'true', version: metadata.version };
  }
  const assets = release.assets || [];
  const find = name => {
    const matches = assets.filter(asset => asset.name === name && asset.state === 'uploaded');
    if (matches.length !== 1) throw new Error(`Release must contain exactly one uploaded ${name}.`);
    return matches[0];
  };
  const download = async asset => Buffer.from((await github.rest.repos.getReleaseAsset({ ...context.repo, asset_id: asset.id, headers: { accept: 'application/octet-stream' } })).data);
  const downloadedChecksums = new Map();
  const directory = path.join(root, 'nexus-artifacts');
  fs.mkdirSync(directory, { recursive: true });
  for (const target of targets) {
    const archive = await download(find(target.archiveName));
    if (!downloadedChecksums.has(target.checksumName)) downloadedChecksums.set(target.checksumName, (await download(find(target.checksumName))).toString('utf8'));
    const digest = crypto.createHash('sha256').update(archive).digest('hex');
    const checksumLine = `${digest}  ${target.archiveName}`;
    if (!downloadedChecksums.get(target.checksumName).split(/\r?\n/).some(line => line.trim() === checksumLine))
      throw new Error(`GitHub release checksum mismatch for ${target.archiveName}. No Nexus upload was attempted.`);
    target.filename = path.join(directory, target.archiveName);
    fs.writeFileSync(target.filename, archive);
    target.upload = String(!dryRun && !target.duplicate);
    target.version = metadata.version;
    target.mod_id = modId;
    target.changelog = target.changelog ? metadata.notes.replace(/^#+\s*/gm, '').replace(/\*\*/g, '') : '';
    if (target.duplicate) core.notice(`Nexus already contains ${target.key} version ${metadata.version}; skipping that file lineage.`);
  }
  if (dryRun) core.notice('Validation-only run: every selected package, checksum and Nexus destination was verified; no Nexus changes were made.');
  const windows = targets.find(target => target.key === 'windows');
  // Keep the original Windows fields for callers that validate legacy releases;
  // the namespaced fields below drive the multi-file workflow.
  const result = {
    upload: String(targets.some(target => target.upload === 'true')), configured: 'true', assets_verified: 'true',
    validation_only: String(dryRun), duplicate: String(windows.duplicate), file_id: windows.file_id,
    mod_id: windows.mod_id, filename: windows.filename, version: windows.version, changelog: windows.changelog, targets,
  };
  for (const target of targets) for (const [key, value] of Object.entries(target)) result[`${target.key}_${key}`] = String(value);
  return result;
};
