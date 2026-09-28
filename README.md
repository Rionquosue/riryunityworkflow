# Riry Unity Workflow

Editor-only workflow tools targeting Unity 2022.3 LTS.

## Install

In Unity Package Manager, choose **Add package from git URL** and enter:

```text
https://github.com/Rionquosue/riryunityworkflow.git
```

The private repository must already be accessible through your Git credentials.

## Clean Animation Keys

Run the cleaner in any of these ways:

- Main menu: **Tools > Rirytools > Clean Animation Keys**
- Windows menu navigation: **Alt**, **T**, **R**, **C**
- Project window: right-click a native `.anim` asset, then choose **Tools > Remove blank shapekeys**
- Animation window: click **Clean keys**

The main-menu command uses the active clip from the focused Animation window. If
another window is focused, it uses the selected AnimationClip in the Project
window.

The cleaner removes inactive blendshape curves and static two-endpoint bone
transform curves. It rounds bone translation, rotation, and scale values to
three decimal places for endpoint comparison. Active or ambiguous curves are
preserved in a review window unless **Remove keys anyways** is explicitly
enabled.

Imported and read-only clips are never edited. Changes support Unity Undo.

## GUID Tools

Open the GUID Tools window from:

- Main menu: **Tools > Rirytools > GUID Tools**
- Windows menu navigation: **Alt**, **T**, **R**, **G**

To run directly from the Project window, select one or more assets or folders,
then right-click and choose **Rirytools > Regenerate GUID**. Selected folders are
expanded recursively; their own folder GUIDs are never changed.

The tool generates a new GUID for every selected asset and updates exact GUID
references in the project's text-serialized files without changing local
file IDs. It requires **Force Text** asset serialization and bypasses Unity
Undo, so commit or back up the project first.

Reverse-reference indexes are cached under
`Library/RegenerateGuidCache`. Delete them from the GUID Tools window or with
**Tools > Rirytools > Delete GUID Reference Cache**. Diagnostic logs are written
beside `RegenerateAssetGuid.cs` under `logs/`; the active log rotates at 10 MiB
and archived logs are trimmed oldest-first above 100 MiB.

