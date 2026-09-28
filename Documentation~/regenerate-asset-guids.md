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
Force Text**. Before writing, it saves modified scenes when approved and saves
dirty assets.

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
validated at their stored byte offsets. Only changed, newly discovered, or
uncached candidate files need a full scan. The cache is derived data and can be
deleted at any time; the next run performs a complete scan.

## Diagnostics

Runs append detailed UTC-timestamped diagnostics beneath the physical directory
containing `RegenerateAssetGuid.cs`:

```text
logs/guid-tools.log
logs/archive/
```

At the start of each run, an active log larger than 10 MiB is moved into the
archive. If archived logs exceed 100 MiB, the oldest archived files are deleted
until the archive is within the limit.

## Safety limits

- Only target assets under `Assets` are accepted.
- Read-only affected files abort the operation before any write.
- Binary Unity assets with a detected dependency on a target abort the
  operation and must be reserialized as text first.
- Cache deletion removes only `Library/RegenerateGuidCache`.
- Temporary scan collections, buffers, and index objects are cleared after a
  run, followed by an explicit managed garbage collection.

