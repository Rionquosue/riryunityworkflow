# Riry Unity Workflow

Editor-only workflow tools targeting Unity 2022.3 LTS.

## Blendshape Sync

Open **Tools > Rirytools > Blendshape Sync**. Drag in a master SkinnedMeshRenderer
and children; an empty child row appears after each addition. Use each child's
**Manual link** foldout to override name matching with Source/Destination names.

**Sync now** copies matching weights. **Run automatically** performs one pass
every 0.5 seconds. **Debug** enables exact, case-sensitive matching; ambiguous
case-insensitive matches block the pass and offer to disable filtering.
**Set all to original** restores all cached master/child weights and stops auto
sync. The original cache lasts while the window remains open, including script
reloads. Closing the window ends that cache.

Sync and restore pause during Play Mode transitions, animation preview/recording,
and Animator graph editing. Changes support Undo and prefab overrides.
See [usage, cache behavior, and tests](Documentation~/blendshape-sync.md).

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

The cleaner removes inactive blendshape curves (every key at zero, with no
tangent leaving zero) and static two-endpoint bone transform curves. It rounds bone translation, rotation, and scale values to
three decimal places for endpoint comparison. Active or ambiguous curves are
preserved in a review window unless **Remove keys anyways** is explicitly
enabled.

Imported and read-only clips are never edited. Changes support Unity Undo.

## Import Presets

Right-click a folder (or empty space in it) and choose **Rirytools > Import
Preset...** to set presets per file type (fbx, obj, png, tga, blend...) from
`Documents/UnityTools/ImportPresets`. New files in that folder and its
subfolders get them on their first import; reimports and hand-edited settings
are never changed. Models can have their animations extracted to `Animations/`
as Generic clips before a Humanoid preset applies, and `.blend` files can be
exported with the Better FBX Blender add-on. Save presets from any asset with
**Rirytools > Save Import Settings to Preset Library...**. See [usage, rules, and limits](Documentation~/import-presets.md).

## Play Mode Windows

Entering Play Mode normally brings the Game view to the front and focuses it.
With **Tools > Rirytools > Play Mode > Keep Tabs in Front** (on by default), the
tabs that were in front, and the window that had focus, come back after Play Mode
starts, and a Game view that Play Mode opened is closed when it ends. **Close Game
View Opened by Play Mode** closes such a Game view as soon as Play Mode starts.
Both are per-user settings.

## Constraint Train

Requires the VRChat Avatars SDK 3.7.0 or newer. Open **Tools > Rirytools >
Constraint Train** to drive a real bone chain onto up to five physics chains
with VRC Parent Constraints and a Motion Time clip. The tip leads a staggered
"train" onto physics chain 1, then every bone blends from chain to chain along an
editable curve. It can generate `Phys1_`..`Phys5_` and `Rest_` empty chains,
with endpoints, without unpacking prefabs.
See [usage and timing](Documentation~/constraint-train.md).

## GUID Tools

Open the GUID Tools window from:

- Main menu: **Tools > Rirytools > GUID Tools**
- Windows menu navigation: **Alt**, **T**, **R**, **G**

To run directly from the Project window, select one or more assets or folders,
then right-click and choose **Rirytools > Regenerate GUID**. Selected folders are
expanded recursively; their own folder GUIDs are never changed.

The tool generates a new GUID for every selected asset and updates exact GUID
references in the project's text-serialized files without changing local
file IDs. It requires **Force Text** asset serialization, saved scenes, and a
closed Prefab Mode. Open scenes are reloaded from disk afterwards. It bypasses
Unity Undo, so commit or back up the project first.

Reverse-reference indexes are cached under
`Library/RegenerateGuidCache`. Delete them from the GUID Tools window or with
**Tools > Rirytools > Delete GUID Reference Cache**. Diagnostic logs are written
to `Logs/RiryGuidTools/` in the project; the active log rotates at 10 MiB, also
during a run, and archived logs are trimmed oldest-first above 100 MiB.

