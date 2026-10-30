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
the Broken References window for the selected text assets and folders. Each
missing GUID gets a redirect from, in order: the ledger; every record any
version of the ledger held in Git (so records lost when the file was deleted,
reverted or overwritten still count); the GUID Tools logs; or the Git history of
`.meta` files (every branch, remote and the reflog). Otherwise you choose one; see [AutoSync](autosync.md#broken-references). Writes use the same
byte-exact patching and rollback as regeneration.

## Safety limits

- Only target assets under `Assets` are accepted.
- Read-only affected files abort the operation before any write.
- Binary Unity assets with a detected dependency on a target abort the
  operation and must be reserialized as text first.
- A symbolic link loop or two paths differing only by letter case stop the run.
- Cache deletion removes only `Library/RiryTools/GuidReferenceCache`.
- Temporary scan collections, buffers, and index objects are cleared after a
  run.

