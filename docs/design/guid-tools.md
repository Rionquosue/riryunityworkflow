# GUID Tools

## Entry points

- **Tools > RiryTools > GUID Tools** opens the window. It has every GUID Tools
  action: **Regenerate GUID**, **Repair Missing GUID References**, **Check for
  Broken References** and **Delete GUID Reference Cache**.
- In the Project window, right-click selected assets or folders and choose
  **RiryTools > Regenerate GUID** or **Repair Missing GUID References**. These
  run straight away, without the window.

The context command uses the current Project selection. Multiple assets are
processed as one batch. Folders are expanded recursively, but folder `.meta`
files are not selected as targets and folder GUIDs remain unchanged.

## Reference-preserving operation

The command requires **Edit > Project Settings > Editor > Asset Serialization >
Force Text**. Prefab Mode must be closed, and every open scene must be saved:
choosing **Don't Save** stops the command, because an unsaved scene would later
be saved over the updated files with stale references. Dirty assets are saved
before scanning.

For each selected asset, the tool:

1. Reads its current 32-character GUID and generates a unique replacement.
2. Scans eligible text files under `Assets`, `ProjectSettings`, and the
   project-local `Packages` directory for exact GUID occurrences.
3. Verifies that every affected file is writable and that each expected GUID is
   still present at its scanned byte offset.
4. Rewrites references first and selected `.meta` files last. Reference
   `fileID` values and all unrelated bytes remain unchanged.
5. Refreshes the Asset Database, verifies every new asset GUID, and reconciles
   files Unity changed during import.
6. Reloads the open scenes from disk so no in-memory object keeps an old
   reference. This also happens after a rollback.

If a write or verification fails, the command attempts to restore every GUID
occurrence it already changed before refreshing Unity again. The operation does
not use Unity Undo, so source control or a project backup remains the final
safety net.

## Reference cache

Each processed asset receives a compact binary reverse-reference index under:

```text
Library/RiryTools/GuidReferenceCache/
```

On a later run for the same asset path and GUID, unchanged cached files are
validated at their stored byte offsets; every other file is scanned. The cache
is derived data and can be deleted at any time.

## Diagnostics

Runs append UTC-timestamped diagnostics to the project's `Logs` folder, outside
any package folder:

```text
Logs/RiryTools/GuidTools/guid-tools.log
Logs/RiryTools/GuidTools/archive/
```

An active log larger than 10 MiB is moved into the archive. If archived logs
exceed 100 MiB, the oldest archived files are deleted until the archive is
within the limit.

## Remap ledger and repair

Every completed run appends `old GUID, new GUID, asset path, UTC time` to
`ProjectSettings/RiryToolsGuidRemap.tsv`. Commit this file.

**RiryTools > Repair Missing GUID References** (or the GUID Tools window) opens
the Broken References window for the selected text assets and folders; **Check
for Broken References** opens it for the open scenes and the prefab in Prefab
Mode. GUID Tools also offers it when an opened scene or prefab contains asset
GUIDs with no asset, or when Unity logs several missing-prefab or broken-reference
messages within 30 seconds.

## Broken References window

Each missing GUID lists every replacement found, each with how it was found
(one candidate can be found several ways):

- **GUID match: remap ledger**, **remap ledger in Git history** (every record any
  version of the ledger held, so records lost when the file was deleted,
  reverted or overwritten still count) and **GUID Tools logs**: the old to new
  GUID chain ends at an asset that exists.
- **Git history: the asset now at `<path>`**: the old GUID last lived at that
  path in the Git history of `.meta` files (every branch, remote and the
  reflog), and another asset is there now.
- **Name match: "`<name>`"**: an asset whose name equals, starts with, contains
  or is contained in the old asset's last known name (from Git history, the
  ledger or the logs), the same file type first, up to 5 per name.
- **Git history backup: `<path>` at `<commit>`**: the missing asset itself, with
  its `.meta`, as it was in Git. Restoring it brings the GUID back, so the
  references work again without being rewritten.
- **Other**: any asset you pick.

Nothing is chosen for you, except when a missing GUID has exactly one
candidate. A missing GUID with nothing chosen stays in a red box.

When a Git restore is chosen, it is checked for what restoring only that file
would break. If it breaks anything, an orange **Break detected** button appears;
clicking it opens the problems, each with **Fix** and **Accept break**. If any
break has no Fix applied when you click Apply, a popup says "This repair will
cause further broken references. Please run tool again afterwards." with
**ABORT** and **ACCEPT BROKEN REFERENCES**. The breaks:

- **It references assets that are missing now.** Fix adds those GUIDs to the
  list, each with its own candidates.
- **Another file is at that path now.** Fix restores beside it under a free
  name; Accept break overwrites it.
- **Another asset uses that GUID now.** Accept break only; Unity then gives one
  of them a new GUID.

When the window scans, it follows every Git restore's missing references, and
theirs, to the end, and caches every candidate it finds, so nothing is searched
twice. One pass shows three levels: the broken references, plus two levels added
by **Fix**. A restore at the third level that still breaks something shows a red
**RECURSIVE BREAK DETECTED** button with the same options and the note "*
Broken references are inevitable. Please run tool again after applying."; its
Fix leaves those references for the next pass.

After **Apply**, the window starts the next pass by itself: it scans the same
files plus the restored ones and lists what is still broken, reusing the cache.
This repeats until no broken references are found. **Rescan** starts over with
an empty cache.

Closing the window while broken GUID references are still listed asks "Exit
without repairing all broken GUID references?" through Unity's close prompt:
**Discard** closes it; **Save** and **Cancel** keep it open.

Saving a scene, or the prefab open in Prefab Mode, that holds broken asset
references in memory is interrupted by a popup that says, in big red text,
"SAVING WITH BROKEN GUID'S CAN CORRUPT A SCENE. Click continue anyways three
times to save regardless." Under it, **Broken references:** lists each one as
`Skin.mat in Avatar/Body > Mesh Renderer > Materials[0] (GUID:...)`: the missing
asset's last known name (from the GUID remap ledger or Git history, else
"Unknown asset"), where it is used, and its GUID. **ABORT** skips that file's
save. **CONTINUE ANYWAY** counts down: the first click shows [2], the second [1],
and the third saves. A reference counts as broken when it is empty and its asset
GUID no longer exists, or a prefab instance's prefab is missing. An empty
reference whose GUID still exists (for example one left over from a scene opened
earlier) does not count, and one with no GUID to look up counts only when the
file on disk names a missing asset. Missing scripts do not count.

Only asset references are repaired: images, prefabs, meshes, materials, text and
other assets. Script references (an `m_Script` field or MonoScript's
`fileID: 11500000`) are skipped, and missing-script messages do not open the
prompt; scripts and DLLs are never offered as name matches.

**Apply** restores the chosen assets (through Git's filters, so Git LFS files
come back as their content), then rewrites the redirected GUIDs byte-exactly in
the scanned and restored files, then reloads the open scenes from disk instead
of saving them. Any failure rolls back every write, restores included.

## Safety limits

- Only target assets under `Assets` are accepted.
- Read-only affected files abort the operation before any write.
- Binary Unity assets with a detected dependency on a target abort the
  operation and must be reserialized as text first.
- A symbolic link loop or two paths differing only by letter case stop the run.
- Cache deletion removes only `Library/RiryTools/GuidReferenceCache`.
- Temporary scan collections, buffers, and index objects are cleared after a
  run.

