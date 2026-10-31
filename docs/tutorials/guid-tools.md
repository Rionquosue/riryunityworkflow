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
3. The **Broken References** window lists each missing GUID, the files that use
   it, and every replacement found, with how each was found (remap ledger, logs,
   Git history, name match, or a backup restored from Git).
4. Choose one for each row, or pick any asset under **Other**. Rows still in a
   red box have nothing chosen and are left as they are.
5. If you chose a Git backup and it lists problems, click **Fix** or **Accept
   break** for each.
6. Click **Apply**. Backups are restored, the files are rewritten and the open
   scenes reloaded.

## Housekeeping

**Delete GUID Reference Cache** in the GUID Tools window clears the speed-up cache in `Library`. Logs are
in `Logs/RiryTools/GuidTools`.

Reference: [design/guid-tools.md](../design/guid-tools.md)
