const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');

module.exports = async ({ github, context, core, root = process.cwd() }) => {
  const metadata = require('./linux-release-metadata.cjs')(root);
  if (!/-linux-beta\./.test(metadata.version)) throw new Error("Stable Linux packages use the unified desktop release workflow.");
  const { tag, notes, assets, checksum } = metadata;
  const directory = path.join(root, 'linux-release-artifacts');
  const sums = fs.readFileSync(path.join(directory, checksum), 'utf8').trim().split(/\r?\n/);
  if (sums.length !== assets.length) throw new Error('Linux checksum manifest has unexpected entries.');
  const files = assets.map(name => {
    const data = fs.readFileSync(path.join(directory, name));
    const digest = crypto.createHash('sha256').update(data).digest('hex');
    if (sums.filter(line => line === `${digest}  ${name}`).length !== 1) throw new Error(`Linux checksum mismatch: ${name}`);
    return [name, data];
  });
  files.push([checksum, Buffer.from(sums.join('\n') + '\n')]);
  const repo = context.repo;
  const releases = await github.paginate(github.rest.repos.listReleases, { ...repo, per_page: 100 });
  const existing = releases.find(release => release.tag_name === tag);
  if (existing && !existing.prerelease) throw new Error('A Linux beta must be marked as a GitHub prerelease.');
  if (existing && !existing.draft) { core.info(`${tag} is already published; leaving it unchanged.`); return; }
  let reference;
  try { reference = (await github.rest.git.getRef({ ...repo, ref: `tags/${tag}` })).data; }
  catch (error) { if (error.status !== 404) throw error; }
  if (reference) {
    let object = reference.object;
    for (let depth = 0; object.type === 'tag' && depth < 10; depth++) object = (await github.rest.git.getTag({ ...repo, tag_sha: object.sha })).data.object;
    if (object.type !== 'commit' || object.sha !== context.sha) throw new Error('Linux beta tag points to another commit. Bump the beta version.');
  } else {
    const annotated = await github.rest.git.createTag({ ...repo, tag, message: `BB Tools ${tag} — Linux beta, not tested in-game`, object: context.sha, type: 'commit' });
    await github.rest.git.createRef({ ...repo, ref: `refs/tags/${tag}`, sha: annotated.data.sha });
  }
  const release = existing ?? (await github.rest.repos.createRelease({ ...repo, tag_name: tag, target_commitish: context.sha,
    name: `BB Tools ${tag} — Linux BETA — NOT TESTED`, body: notes, draft: true, prerelease: true, make_latest: 'false' })).data;
  const previous = existing ? await github.paginate(github.rest.repos.listReleaseAssets, { ...repo, release_id: release.id, per_page: 100 }) : [];
  for (const [name, data] of files) {
    const old = previous.find(asset => asset.name === name);
    if (old) {
      if (old.state === 'uploaded' && old.digest === `sha256:${crypto.createHash('sha256').update(data).digest('hex')}` && old.size === data.length) continue;
      await github.rest.repos.deleteReleaseAsset({ ...repo, asset_id: old.id });
    }
    await github.rest.repos.uploadReleaseAsset({ ...repo, release_id: release.id, name, data,
      headers: { 'content-type': name.endsWith('.zip') ? 'application/zip' : name.endsWith('.gz') ? 'application/gzip' : 'text/plain', 'content-length': data.length } });
  }
  core.info(`Linux beta draft ready: ${release.html_url}. Not tested in-game; never mark as latest stable.`);
};
