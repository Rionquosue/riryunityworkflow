# Regenerate Asset GUIDs

## Entry points

- Open the window with **Tools > Rirytools > GUID Tools**.
- On Windows, the menu-navigation sequence is **Alt**, **T**, **R**, **G**.
- In the Project window, right-click selected assets or folders and choose
  **Rirytools > Regenerate GUID**.
- Delete the derived index with the window button or
  **Tools > Rirytools > Delete GUID Reference Cache**.

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
Library/RegenerateGuidCache/
```

On a later run for the same asset path and GUID, unchanged cached files are
validated at their stored byte offsets; every other file is scanned. The cache
is derived data and can be deleted at any time.

## Diagnostics

Runs append UTC-timestamped diagnostics to the project's `Logs` folder, outside
any package folder:

```text
Logs/RiryGuidTools/guid-tools.log
Logs/RiryGuidTools/archive/
```

An active log larger than 10 MiB is moved into the archive. If archived logs
exceed 100 MiB, the oldest archived files are deleted until the archive is
within the limit.

## Safety limits

- Only target assets under `Assets` are accepted.
- Read-only affected files abort the operation before any write.
- Binary Unity assets with a detected dependency on a target abort the
  operation and must be reserialized as text first.
- A symbolic link loop or two paths differing only by letter case stop the run.
- Cache deletion removes only `Library/RegenerateGuidCache`.
- Temporary scan collections, buffers, and index objects are cleared after a
  run.

## Tests

`RegenerateAssetGuidTests` covers block-boundary scanning, byte-exact patching
with the target `.meta` written last, refusal to patch a changed file, rollback,
the rule that the cache never hides an uncached file, case-only path
collisions, short cache names for long asset names, and Unicode path matching. The tests use
temporary files outside the project.

