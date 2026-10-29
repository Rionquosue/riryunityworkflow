# Tutorial: GUID Tools

Give assets new GUIDs without breaking references, and fix references that
point to assets that no longer exist.

Before starting: set **Edit > Project Settings > Editor > Asset Serialization**
to **Force Text**, save your scenes, close Prefab Mode, and commit or back up
the project. These tools write files directly and bypass Undo.

## Regenerate GUIDs

Use this when two assets share a GUID (for example after copying a package
folder) or a copied asset must stop being treated as the original.

1. Select the assets or folders in the Project window. Folders include
   everything inside them; folder GUIDs themselves never change.
2. Right-click and choose **RiryTools > Regenerate GUID**, or open
   **Tools > RiryTools > GUID Tools** and click **Regenerate GUID**.
3. Confirm. Every text reference in the project is updated, and the open scenes
   are reloaded from disk.

Each change is recorded in `ProjectSettings/RiryToolsGuidRemap.tsv`. Commit that file;
the repair tool uses it later.

## Repair missing references

Use this when a scene or prefab shows missing scripts or missing assets, for
example after restoring an old copy of a file.

1. Select the broken scenes, prefabs or their folders.
2. Right-click and choose **RiryTools > Repair Missing GUID References** (or
   **Repair Missing GUID References** in the GUID Tools window).
3. The **Broken References** window lists each missing GUID and the files that
   use it. Where it can, it fills in **Redirect to** from the remap ledger, the
   GUID Tools logs or the Git history.
4. Rows still in a red box have no suggestion: drag the asset the reference
   should use into **Redirect to**.
5. Click **Apply**. The files are rewritten and the open scenes reloaded.

## Housekeeping

**Delete GUID Reference Cache** in the GUID Tools window (or **Tools > RiryTools >
Delete GUID Reference Cache**) clears the speed-up cache in `Library`. Logs are
in `Logs/RiryTools/GuidTools`.

Reference: [design/guid-tools.md](../design/guid-tools.md)
