# AutoSync

Backs up scenes, prefabs and `.meta` files to a Git branch as they change, so
power loss, a crash or a misbehaving script never costs more than a few seconds
of work. Everything is in the **GIT** menu in the main menu bar.

## GIT menu

- **AutoSync Scene∕Prefab**: on by default once the project is in a Git
  repository.
- **Sync Now**: snapshot (and push) immediately.
- **Check for Broken References...**: scan the open scenes and prefab.
- **Configure Sync...**: available while there is no repository or no remote.
- **AutoSync Settings...**: branch, base branch, remote, push, rebase workflow.
- **Help**: what, why and how, with a link to download Git.

Without Git installed, every Git action throws `GitNotInstalledException` and
offers the download page (https://git-scm.com/install/). Credentials are left
to Git and its credential manager.

## Configure Sync

The repository is always the whole project folder: `.git` sits beside `Assets`,
`Library`, `Packages`, `ProjectSettings` and the `.csproj` files. A project that
only sits inside a repository higher up does not count; AutoSync refuses it and
Configure Sync creates a separate repository in the project folder.

Pick a `.gitignore` and a `.gitattributes` template, then **Apply**:

1. `.git` is created in the project folder if it is not a repository yet.
2. The chosen templates are copied to the top of the project folder. A different existing
   file is replaced only after confirmation.
3. Git LFS is enabled for the repository (`git lfs install --local`).
4. An optional remote URL is added.
5. The LFS check runs: binary files of 1 MiB or more that are not stored with
   LFS are listed, with a button to track their extensions. Files over 100 MB
   are listed. With a remote, a tiny test object is uploaded through LFS to
   prove pushing works.

This also applies to a repository with no remote.

Templates are `<name>.gitignore` and `<name>.gitattributes` files in
`Documents/UnityTools/ImportPresets`, for example `vrcproject.gitignore` or
`gameproject.gitattributes`. **Save** in the window stores the project's
current files under a name. Without any templates, GitHub's
[Unity.gitignore](https://github.com/github/gitignore/blob/main/Unity.gitignore)
and the gitattributes project's
[Unity.gitattributes](https://github.com/gitattributes/gitattributes/blob/master/Unity.gitattributes)
are downloaded as `Unity`, and a built-in LFS template is added as `UnityLFS`.

When the project has no repository, Unity offers Configure Sync once per
session (or never, after **Don't Ask Again**).

### Cloudflare

Servers behind Cloudflare proxies, including Zero Trust tunnels to Gitea or
Forgejo, reject any single HTTPS upload over 100 MB, and Git LFS cannot split
uploads. Configure Sync warns when the remote uses HTTPS. Use an SSH remote
(`git@host:owner/repo.git`) and enable LFS over SSH on the server
(`LFS_ALLOW_PURE_SSH = true` under `[server]` in Gitea's or Forgejo's `app.ini`).

## Snapshots

Three seconds after a scene or prefab save, an import, a move or a deletion,
every `.unity`, `.prefab` and `.meta` file under `Assets` and `Packages`, plus
`.gitignore`, `.gitattributes`, `.lfsconfig` and the GUID remap ledger, is
committed exactly as it is in the working copy to the `AutoSync` branch (or the
branch chosen in AutoSync Settings) and pushed. Other files come from the
previous snapshot, or from the base branch for the first one. Ignored files are
skipped, LFS attributes apply, and a pass with no changes commits nothing.

Snapshots use a separate index in `Library/RiryGitAutosave`, so the checked-out
branch, the working copy and staged changes are never touched. Commits are
flushed to disk before Git reports success (Git 2.36+). On quit, a pending
snapshot is committed locally without pushing. Git 2.25 or newer is required.

## Rebase workflow

**Install Rebase Workflow** in AutoSync Settings writes
`.github/workflows/riry-autosave-rebase.yaml`. Commit and push it to the base
branch. On every push to the base or AutoSync branch, every 6 hours and on
demand, it replays each AutoSync commit onto the base branch; when both changed
a file, the AutoSync version wins. When the workflow has rewritten the remote
branch, the editor replays its unpushed snapshots onto it and pushes again.

## Broken references

AutoSync offers **Check for Broken References** when an opened scene or prefab
contains GUIDs with no asset, or when Unity logs several missing-script or
missing-prefab messages within 30 seconds. The window lists each missing GUID
with the files using it and a **Redirect to** field. Redirects are filled in
from the GUID remap ledger, the GUID Tools logs and the Git history of `.meta`
files (AutoSync included); a GUID with none stays in a red box until an asset is
chosen. **Apply** rewrites the GUIDs byte-exactly with rollback, then reloads the
open scenes from disk instead of saving them.

## Script causing failed load

After a scene or prefab opens, the number of objects that loaded is compared
with the number its file holds. When none, or fewer than half, loaded (for
example a package script that makes a scene load empty), the
**SCRIPT CAUSING FAILED LOAD** window appears:

- It names the script from the first non-Unity frame of the errors logged while
  the file opened, with its path, when it can.
- Saving that file is blocked, so the good file on disk is not overwritten.
- The window is modal and returns if closed, until you **Reload From Disk**,
  **Open Empty Scene**, **Quit Unity Without Saving**, or confirm that the file
  loaded correctly.

## Recovering a file

`git checkout AutoSync -- path/to/Scene.unity path/to/Scene.unity.meta`

## Tests

`GitAutosaveTests` covers branch names, workflow generation, snapshot contents,
replay onto a rebased remote, the checked-out branch refusal, and the LFS check
and probe upload. `GuidRepairTests` covers the GUID scan, ledger, history lookup,
load-failure counting and script identification. Git and LFS tests are skipped
when they are not installed.
