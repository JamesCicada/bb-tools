const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');

// Called only after both Windows build configurations and checks have passed.
module.exports = async ({ github, context, core, root = process.cwd() }) => {
  const { version, tag, notes } = require('./release-metadata.cjs')(root);
  const name = `BBTools-${tag}-win-x64.zip`;
  const directory = path.join(root, 'release-artifacts');
  const archive = fs.readFileSync(path.join(directory, name));
  const checksum = fs.readFileSync(path.join(directory, 'SHA256SUMS.txt'), 'utf8');
  const digest = crypto.createHash('sha256').update(archive).digest('hex');
  if (checksum.trim() !== `${digest}  ${name}`) throw new Error('Release checksum mismatch.');
  const files = [[name, archive, 'application/zip'], ['SHA256SUMS.txt', Buffer.from(checksum), 'text/plain']];
  // From 0.7.0 both platforms are gated and published in one versioned draft.
  const parts=version.split('.').map(Number);
  if (parts[0]>0 || parts[1]>=7) {
    const linux=require('./linux-release-metadata.cjs')(root);
    if(linux.version!==version)throw new Error('Linux release version differs from Windows.');
    const manifest=fs.readFileSync(path.join(directory,linux.checksum),'utf8');
    const sums=manifest.trim().split(/\r?\n/);
    if(sums.length!==linux.assets.length)throw new Error('Linux checksum manifest has unexpected entries.');
    for(const asset of linux.assets){
      const data=fs.readFileSync(path.join(directory,asset));
      const digest=crypto.createHash('sha256').update(data).digest('hex');
      if(sums.filter(line=>line===`${digest}  ${asset}`).length!==1)throw new Error(`Linux checksum mismatch: ${asset}`);
      files.push([asset,data,asset.endsWith('.zip')?'application/zip':'application/gzip']);
    }
    files.push([linux.checksum,Buffer.from(manifest),'text/plain']);
  }
  const repo = context.repo;
  const releases = await github.paginate(github.rest.repos.listReleases, { ...repo, per_page: 100 });
  const existing = releases.find(release => release.tag_name === tag);
  if (existing && !existing.draft) {
    core.info(`${tag} already has a release; leaving it unchanged.`);
    return;
  }
  let reference;
  try { reference = (await github.rest.git.getRef({ ...repo, ref: `tags/${tag}` })).data; }
  catch (error) { if (error.status !== 404) throw error; }
  if (reference) {
    let object = reference.object;
    for (let depth = 0; object.type === 'tag' && depth < 10; depth++)
      object = (await github.rest.git.getTag({ ...repo, tag_sha: object.sha })).data.object;
    if (object.type !== 'commit' || object.sha !== context.sha)
      throw new Error(`${tag} points to another commit. Bump the project version.`);
  } else {
    const annotated = await github.rest.git.createTag({ ...repo, tag, message: `BB Tools ${tag}`, object: context.sha, type: 'commit' });
    await github.rest.git.createRef({ ...repo, ref: `refs/tags/${tag}`, sha: annotated.data.sha });
  }
  const release = existing ?? (await github.rest.repos.createRelease({
    ...repo, tag_name: tag, target_commitish: context.sha, name: `BB Tools ${tag}`,
    body: notes, draft: true, prerelease: false,
  })).data;
  const assets = existing ? await github.paginate(github.rest.repos.listReleaseAssets, { ...repo, release_id: release.id, per_page: 100 }) : [];
  for (const [asset, data, contentType] of files) {
    const previousAsset = assets.find(entry => entry.name === asset);
    if (previousAsset) {
      // Only an unpublished draft at this exact commit reaches this branch.
      // Reruns may produce a different ZIP timestamp; refresh its paired assets.
      if (previousAsset.state === 'uploaded' && previousAsset.size === data.length &&
          previousAsset.digest === `sha256:${crypto.createHash('sha256').update(data).digest('hex')}`) continue;
      core.info(`Refreshing draft asset ${asset}.`);
      await github.rest.repos.deleteReleaseAsset({ ...repo, asset_id: previousAsset.id });
    }
    await github.rest.repos.uploadReleaseAsset({ ...repo, release_id: release.id, name: asset, data,
      headers: { 'content-type': contentType, 'content-length': data.length } });
  }
  core.info(`Created draft ${release.html_url}. Publish it manually after live testing.`);
};
