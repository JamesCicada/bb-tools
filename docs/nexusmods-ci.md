# Nexus Mods release integration

BB Tools uses the official [Nexus Mods upload action](https://github.com/Nexus-Mods/upload-action)
and the current v3 API. The action is pinned to v1.0.0-beta.11's commit.

## One-time setup

Add your author API key from [Nexus account settings](https://www.nexusmods.com/settings/api-keys)
as the repository Actions secret `NEXUSMODS_API_KEY`. The key must have permission
to upload to [BB TOOLS, Bloodborne mod 614](https://www.nexusmods.com/bloodborne/mods/614).
No key belongs in source files, workflow inputs or release notes.

The preflight resolves the mod's global ID from `bloodborne/mods/614`, then resolves
its existing main file from public version-history ID `3154` (v0.5.0). It verifies
that the file belongs to this mod before uploading. The seed identifies the
persistent file; it does not restrict uploads to version 0.5.0. If that Nexus file
is deleted or you intentionally change file groups, update the seed in
`scripts/nexus-release.cjs` to the intended file's version-history ID.

## Release flow

1. Push a version bump with `docs/releases/vX.Y.Z.md`.
2. Windows CI builds/checks Debug and Release, creates the tag and GitHub draft,
   and attaches the ZIP and SHA256SUMS.txt.
3. Publish the stable GitHub release when it is ready. This triggers
   **Publish to Nexus Mods**, reusing the exact ZIP and verifying its checksum
   and its two entries (BBTools.exe and README.md).
4. The official action adds a new main-file version and the version-specific
   changelog, and updates the Nexus mod version. Mod-manager installation is
   disabled because this is a standalone tool. Existing files are not deleted.

Drafts, prereleases and ordinary pushes do not publish anything to Nexus.
If credentials are missing, the Nexus workflow posts a setup notice and skips.
Version history prevents reruns from uploading the same version or appending its
changelog twice. Concurrent Nexus jobs are serialized.

## Validate or retry

Run **Actions → Publish to Nexus Mods → Run workflow**, enter the release tag,
and leave **validate_only** checked to verify credentials and the target file
without changing Nexus. The workflow keeps contents:read permissions. If GitHub
hides a private draft from that token, validation explicitly defers asset/checksum
checks until publication; it never authorizes upload without those checks.
Published release assets are validated in the same run.
Uncheck it only to retry an already published stable GitHub release. An existing
Nexus version is skipped; metadata from a partially successful upload may need
manual correction rather than creating a duplicate version.

The desktop app never contains or uses this API key. Upload permission and a live
upload are validated separately from mocked workflow checks.


## Separate Linux beta file

Linux `0.6.6-linux-beta.1` is **Beta — NOT TESTED IN-GAME**. Its CI build produces
`BBTools-v0.6.6-linux-beta.1-linux-x64.zip`, containing the native desktop executable
and README only. Use a separate **Optional files** group and keep the main Windows
file and overall mod version unchanged. The initial Linux file group must be
created on Nexus before its file ID can be used by the official uploader.

[Linux file name, description and upload settings](nexusmods-linux-beta.md) are
ready to paste. The stable Windows workflow deliberately excludes prereleases;
a Linux beta cannot accidentally replace the Windows main download.
