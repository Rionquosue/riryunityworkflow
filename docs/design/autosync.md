# AutoSync

Commits every scene and prefab save to Git, so power loss, a crash or a
misbehaving script never costs more than the last save.

## Menu

The same entries are under **Tools > RiryTools > AutoSync** and in the **Git**
menu in the main menu bar:

- **Sync**: a check-mark toggle, off until you turn it on.
- **Configuration...**: templates and the push remote.

**Git > Check for Broken References...** is part of GUID Tools.

## Turning Sync on

1. Without Git installed, a dialog offers the download page
   (https://git-scm.com/install/) and Sync stays off.
2. Without a repository, the template folder `Documents/RiryTools/GitTemplates`
   is filled with the Unity defaults if it has no templates, with a dialog
   naming the folder. `git init` creates the repository in the project folder,
   and the `Unity` `.gitignore` and `.gitattributes` templates are copied in
   unless the project already has those files.
3. Git LFS is enabled (`git lfs install --local`). Without Git LFS, a warning is
   shown and Sync still turns on.

The repository is always the whole project folder: `.git` sits beside `Assets`.
A project inside a repository higher up gets its own repository after
confirmation.

## Commits

Each save of a scene or prefab, from Ctrl+S, Prefab Mode or any other save, is
one commit on the checked-out branch. The commit holds only the saved `.unity`
or `.prefab` files and their `.meta` files; anything else that is staged stays
staged and out of the commit. Ignored files are skipped, and a save with no
changes commits nothing. The title is the time of the save, for example
`2026-10-30T14:05:12+01:00`. Commits are flushed to disk before Git reports
success (Git 2.36+).

After the commits, the branch is pushed to its upstream, or to `origin` with
`--set-upstream` when it has none. Without `origin` or an upstream, or on a
detached HEAD, nothing is pushed. A sign-in goes through Git's credential
manager, which shows its own window; Git never waits on a terminal prompt. A
failed commit or push is logged in the Console once.

Saves not yet committed when scripts reload are committed afterwards. On quit,
they are committed without pushing; the next push sends them.

## Configuration

- **Templates**: pick a `.gitignore` and a `.gitattributes` from
  `Documents/RiryTools/GitTemplates`; **Apply** copies them into the project,
  asking before replacing a different file. Templates are `<name>.gitignore` and
  `<name>.gitattributes`, for example `vrcproject.gitignore`. **Save** stores the
  project's current files under a name, and **Open Template Folder** shows the
  folder. Without any templates, GitHub's
  [Unity.gitignore](https://github.com/github/gitignore/blob/main/Unity.gitignore)
  and the gitattributes project's
  [Unity.gitattributes](https://github.com/gitattributes/gitattributes/blob/master/Unity.gitattributes)
  are downloaded as `Unity`, and a built-in LFS template is added as `UnityLFS`.
- **Push to remote**: the URL of `origin`. **Apply** adds it, or changes it when
  it differs. Needs a repository, so turn Sync on first.

### Cloudflare

Servers behind Cloudflare proxies, including Zero Trust tunnels to Gitea or
Forgejo, reject any single HTTPS upload over 100 MB, and Git LFS cannot split
uploads. Configuration warns under **Push to remote** when the URL uses HTTPS.
Use an SSH remote (`git@host:owner/repo.git`) and enable LFS over SSH on the
server (`LFS_ALLOW_PURE_SSH = true` under `[server]` in Gitea's or Forgejo's
`app.ini`).

## Broken references

GUID Tools offers **Check for Broken References** when an opened scene or prefab
contains GUIDs with no asset, or when Unity logs several missing-script or
missing-prefab messages within 30 seconds. The window lists each missing GUID
with the files using it and a **Redirect to** field. Redirects are filled in
from the GUID remap ledger, the GUID Tools logs and the Git history of `.meta`
files; a GUID with none stays in a red box until an asset is
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

`git checkout <commit> -- path/to/Scene.unity path/to/Scene.unity.meta`, with a
commit from `git log -- path/to/Scene.unity`.
