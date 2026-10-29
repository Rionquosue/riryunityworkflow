# RiryTools

`com.riry.rirytools`: editor-only workflow tools for Unity 2022.3 LTS.

## Install

Add `https://rionquosue.github.io/rirytools_vpm/index.json` in the VRChat
Creator Companion (**Settings > Packages > Add Repository**). Every change merged
to `main` is published there as a new version after the owner approves it; see
[publishing as a VPM package](https://github.com/Rionquosue/riryunityworkflow/blob/main/docs/tutorials/vpm-publishing.md).

The package's own files are replaced on every update. Anything that
has to live inside the project is unpacked into **`Assets/RiryTools`** on first
load, and tools save generated assets there when they have no better place
(Constraint Train clips, `_ROOT` variants of read-only prefabs). Existing files
there are never overwritten, so edits survive updates; delete a file to get the
package's copy back. Shader applets will live there too.

## Documentation

Tutorials and design notes are in the source repository's
[`docs`](https://github.com/Rionquosue/riryunityworkflow/tree/main/docs) folder.

## Blendshape Sync

Open **Tools > RiryTools > Blendshape Sync**. Drag in a master SkinnedMeshRenderer
and children; an empty child row appears after each addition. Use each child's
**Manual link** foldout to override name matching with Source/Destination names.

**Sync Now** copies matching weights. **Run automatically** performs one pass
every 0.5 seconds. **Case-sensitive** enables exact matching; ambiguous
case-insensitive matches block the pass and offer to switch to it.
**Set All to Original** restores all cached master/child weights and stops auto
sync. The original cache lasts while the window remains open, including script
reloads. Closing the window ends that cache.

Sync and restore pause during Play Mode transitions, animation preview/recording,
and Animator graph editing. Changes support Undo and prefab overrides.
See the [tutorial](https://github.com/Rionquosue/riryunityworkflow/blob/main/docs/tutorials/blendshape-sync.md) and [design notes](https://github.com/Rionquosue/riryunityworkflow/blob/main/docs/design/blendshape-sync.md).

## Editing the UI

Every menu entry (Tools dropdown, Project and Hierarchy right-click, Git menu)
is in `Editor/UI/RiryMenus.cs`, except Constraint Train's, which are in
`Editor/VRChat/RiryVRChatMenus.cs` because they need the VRChat SDK. Change a
path or priority there to move an entry. Each window's drawing code is in
`Editor/UI/` (Constraint Train's in `Editor/VRChat/ConstraintTrainWindow.UI.cs`),
with a glossary of its fields and methods at the bottom of each file.

## Clean Animation Keys

Run the cleaner in any of these ways:

- Main menu: **Tools > RiryTools > Clean Animation Keys**
- Windows menu navigation: **Alt**, **T**, **R**, **C**
- Project window: right-click a native `.anim` asset, then choose **RiryTools > Clean Animation Keys**
- Animation window: click **Clean Keys**

The main-menu command uses the active clip from the focused Animation window. If
another window is focused, it uses the selected AnimationClip in the Project
window.

The cleaner removes inactive blendshape curves (every key at zero, with no
tangent leaving zero) and static two-endpoint bone transform curves. It rounds bone translation, rotation, and scale values to
three decimal places for endpoint comparison. Active or ambiguous curves are
preserved in a review window unless **Remove keys anyway** is explicitly
enabled.

Imported and read-only clips are never edited. Changes support Unity Undo.
See the [tutorial](https://github.com/Rionquosue/riryunityworkflow/blob/main/docs/tutorials/clean-animation-keys.md) and [design notes](https://github.com/Rionquosue/riryunityworkflow/blob/main/docs/design/clean-animation-keys.md).

## Import Presets

Right-click a folder (or empty space in it) and choose **RiryTools > Import
Preset...** to set presets per file type (fbx, obj, png, tga, blend...) from
`Documents/RiryTools/ImportPresets`. New files in that folder and its
subfolders get them on their first import; reimports and hand-edited settings
are never changed. Models can have their animations extracted to `Animations/`
as Generic clips before a Humanoid preset applies, and `.blend` files can be
exported with the Better FBX Blender add-on. Save presets from any asset with
**RiryTools > Save Import Settings to Preset Library...**. See the [tutorial](https://github.com/Rionquosue/riryunityworkflow/blob/main/docs/tutorials/import-presets.md) and [design notes](https://github.com/Rionquosue/riryunityworkflow/blob/main/docs/design/import-presets.md).

## Play Mode Windows

Entering Play Mode normally brings the Game view to the front and focuses it.
With **Tools > RiryTools > Play Mode > Keep Tabs in Front** (on by default), the
tabs that were in front, and the window that had focus, come back after Play Mode
starts, and a Game view that Play Mode opened is closed when it ends. **Close Game
View Opened by Play Mode** closes such a Game view as soon as Play Mode starts.
Both are per-user settings. See the [tutorial](https://github.com/Rionquosue/riryunityworkflow/blob/main/docs/tutorials/play-mode-windows.md).

## Constraint Train

Requires the VRChat Avatars SDK 3.7.0 or newer. Open **Tools > RiryTools >
Constraint Train** (or right-click a bone in the Hierarchy > **RiryTools >
Constraint Train...**) to drive a real bone chain onto up to five physics chains
with VRC Parent Constraints and a Motion Time clip. The tip leads a staggered
"train" onto physics chain 1, then every bone blends from chain to chain along an
editable curve. It can generate `Phys1_`..`Phys5_` and `Rest_` empty chains,
with endpoints, without unpacking prefabs.
See the [tutorial](https://github.com/Rionquosue/riryunityworkflow/blob/main/docs/tutorials/constraint-train.md) and [design notes](https://github.com/Rionquosue/riryunityworkflow/blob/main/docs/design/constraint-train.md).

## GUID Tools

Open the GUID Tools window from:

- Main menu: **Tools > RiryTools > GUID Tools**
- Windows menu navigation: **Alt**, **T**, **R**, **G**

To run directly from the Project window, select one or more assets or folders,
then right-click and choose **RiryTools > Regenerate GUID**. Selected folders are
expanded recursively; their own folder GUIDs are never changed.

The tool generates a new GUID for every selected asset and updates exact GUID
references in the project's text-serialized files without changing local
file IDs. It requires **Force Text** asset serialization, saved scenes, and a
closed Prefab Mode. Open scenes are reloaded from disk afterwards. It bypasses
Unity Undo, so commit or back up the project first.

Reverse-reference indexes are cached under
`Library/RiryTools/GuidReferenceCache`. Delete them from the GUID Tools window or with
**Tools > RiryTools > Delete GUID Reference Cache**. Diagnostic logs are written
to `Logs/RiryTools/GuidTools/` in the project; the active log rotates at 10 MiB, also
during a run, and archived logs are trimmed oldest-first above 100 MiB.

Every regeneration is recorded in `ProjectSettings/RiryToolsGuidRemap.tsv`.
**RiryTools > Repair Missing GUID References** relinks GUIDs that no longer
resolve in damaged or recovered files, using that ledger and then Git history.
See the [tutorial](https://github.com/Rionquosue/riryunityworkflow/blob/main/docs/tutorials/guid-tools.md) and [design notes](https://github.com/Rionquosue/riryunityworkflow/blob/main/docs/design/guid-tools.md).

## AutoSync (Git menu)

The **Git** menu backs up scenes, prefabs and `.meta` files to an `AutoSync`
branch seconds after they change, through a separate Git index, and pushes it.
**Configure Sync...** creates the repository from named `.gitignore` and
`.gitattributes` templates in `Documents/RiryTools/GitTemplates`, enables Git
LFS and checks that large files use it and can be pushed. AutoSync offers to fix
broken references from its history, and blocks saving a scene or prefab that a
script stopped from loading. See the [tutorial](https://github.com/Rionquosue/riryunityworkflow/blob/main/docs/tutorials/autosync.md) and [design notes](https://github.com/Rionquosue/riryunityworkflow/blob/main/docs/design/autosync.md).

