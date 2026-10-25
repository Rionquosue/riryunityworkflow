# Git Autosave

Backs up scenes, prefabs and `.meta` files to a Git branch as they change, so
power loss or a damaged file never costs more than a few seconds of work.

## Setup

Open **Tools > Rirytools > Git Autosave**:

| Setting | Default | Meaning |
| --- | --- | --- |
| Enabled | off | Snapshot automatically. **Snapshot Now** works either way. |
| Autosave branch | `autosave/<user>` | Branch that receives snapshots. Must not be checked out. |
| Base branch | `main` | Where a new autosave branch starts, and what the workflow rebases onto. |
| Push | on | Push after each snapshot. |
| Remote | `origin` | Push target. |

Settings are per user and project (`UserSettings`), so each person can use
their own branch. Git 2.25 or newer must be on `PATH`.

## What a snapshot contains

Three seconds after a scene or prefab save, an import, a move or a deletion,
the tool commits every `.unity`, `.prefab` and `.meta` file under `Assets` and
`Packages`, plus `ProjectSettings/RiryGuidRemap.tsv`, exactly as they are in the
working copy. Other files come from the branch's previous snapshot, or from the
base branch for the first one. Ignored files are skipped. A pass with no changes
commits nothing.

Snapshots use a separate index in `Library/RiryGitAutosave`, so the checked-out
branch, the working copy and staged changes are never touched. Commits are
flushed to disk before Git reports success (Git 2.36+). On quit, a pending
snapshot is committed locally without pushing.

## Rebase workflow

**Install Rebase Workflow** writes `.github/workflows/riry-autosave-rebase.yaml`
to the repository. Commit and push it to the base branch. On every push to the
base or an autosave branch, every 6 hours and on demand, it replays each
autosave commit onto the base branch. When both changed a file, the autosave
version wins, so a backup is never lost to a conflict.

When the workflow has rewritten the remote branch, the next push from the
editor is rejected. The editor then fetches the branch, replays its unpushed
snapshots onto it and pushes again.

## Recovering

- **A damaged scene or prefab:** `git checkout autosave/<user> -- path/to/file`
  and its `.meta`.
- **Broken references:** run **Rirytools > Repair Missing GUID References** on
  the affected files. It searches autosave branches too. See
  [GUID Tools](regenerate-asset-guids.md).

## Tests

`GitAutosaveTests` covers branch-name validation, workflow generation,
snapshot contents (deletions, ignored files, literal names, untouched checkout
and staged changes), replaying onto a rebased remote branch and the refusal to
use a checked-out autosave branch. Git tests are skipped when Git is missing.
