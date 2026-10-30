# AutoSync

Commits every scene and prefab save to Git, so power loss, a crash or a
misbehaving script never costs more than the last save.

## Menu

The same entries are under **Tools > RiryTools > AutoSync** and in the **Git**
menu in the main menu bar:

- **Sync**: a check-mark toggle, off until you turn it on.
- **Configuration**: templates and the push remote.

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
or `.prefab` files and their `.meta` files (or an Extract Animation settings
file when it is written); anything else that is staged stays
staged and out of the commit. Ignored files are skipped, and a save with no
changes commits nothing. The title is the time of the save, for example
`2026-10-30T14:05:12+01:00`. Commits are flushed to disk before Git reports
success (Git 2.36+).

After the commits, the branch is pushed to the remote chosen in Configuration
(by default `origin`, else the first remote), with `--set-upstream` when the
branch has none. Without a remote, or on a detached HEAD, nothing is pushed. A
sign-in goes through Git's credential manager, which shows its own window; Git
never waits on a terminal prompt. A failed commit or push is logged in the
Console once.

To an HTTP(S) remote, the push is held while the commits not yet on that remote
hold a file over 100 MiB: Sync keeps committing, and the **Push Held** dialog
says the push would fail behind Cloudflare and lists the files. **Fix Now** opens
**Large Files**; **Do Not Show Again** turns the dialog off for that remote. It
shows once per remote per editor session, so a different remote gets its own.

Saves not yet committed when scripts reload are committed afterwards. On quit,
they are committed without pushing; the next push sends them.

## Configuration

- **Templates** (one box): pick a `.gitignore` and a `.gitattributes` from
  `Documents/RiryTools/GitTemplates`. Templates are `<name>.gitignore` and
  `<name>.gitattributes`, for example `vrcproject.gitignore`. **Template name**
  with **Save .gitignore** or **Save .gitattributes** stores the project's
  current file under that name, and **Open Template Folder** shows the folder.
  Without any templates, GitHub's
  [Unity.gitignore](https://github.com/github/gitignore/blob/main/Unity.gitignore)
  and the gitattributes project's
  [Unity.gitattributes](https://github.com/gitattributes/gitattributes/blob/master/Unity.gitattributes)
  are downloaded as `Unity`, and a built-in LFS template is added as `UnityLFS`.
- **Branch**: the local branches, then the chosen remote's branches with no
  local branch. Choosing one offers to save modified scenes, commits pending
  saves, and runs `git switch`.
- **Remote** (with several remotes): which one Sync pushes to, per user and
  per project.
- **Push to remote**: the chosen remote's URL. **Add Remote** adds `origin` when
  there is no remote; **Change Remote** saves a different URL once one exists.
  Needs a repository, so turn Sync on first.
- **!** (yellow, right of the remote button): shown for an HTTP(S) remote while
  any tracked file is over 100 MiB. It opens **Large Files**.
- **Git LFS status**: when the window opens, when another remote is chosen, and
  after Add Remote or Change Remote, the remote's server is asked whether Git
  LFS is enabled. Nothing is uploaded: over HTTP(S) it is an LFS batch download
  request for one object that does not exist (an LFS server answers 200, a
  server without LFS 404), signed in through Git's credential manager if the
  server asks; over SSH it is `git-lfs-authenticate <path> download`.
- **Apply**: copies the chosen templates into the project, asking before
  replacing a different file, then saves **Push to remote** when it changed.

### Large Files

Lists every tracked file over 100 MiB under the Cloudflare warning. Clicking a
file selects it in the Project window. **Add to ignore** marks it; **OK** adds
the marked files to `.gitignore` and takes them out of every commit not yet on
the chosen remote, leaving them on disk. Those commits are rewritten (new
hashes; a commit that only held those files is dropped) and the index matches,
so the files are not committed again. Unpushed merges cannot be rewritten.

### Cloudflare

Servers behind Cloudflare proxies, including Zero Trust tunnels to Gitea or
Forgejo, reject any single HTTPS upload over 100 MB, and Git LFS cannot split
uploads. Configuration warns under **Push to remote** when the URL uses HTTP or
HTTPS; SSH URLs show no warning.
Use an SSH remote (`git@host:owner/repo.git`) and enable LFS over SSH on the
server (`LFS_ALLOW_PURE_SSH = true` under `[server]` in Gitea's or Forgejo's
`app.ini`).

## Broken references

GUID Tools repairs broken references, and searches the Git history Sync builds
up; see [GUID Tools](guid-tools.md#broken-references-window).

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
