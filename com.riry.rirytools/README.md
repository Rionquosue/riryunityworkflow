# RiryTools

`com.riry.rirytools`: editor-only workflow tools for Unity 2022.3 LTS.

## Install

Add `https://rionquosue.github.io/rirytools_vpm/index.json` in the VRChat
Creator Companion (**Settings > Packages > Add Repository**). Every change merged
to `main` is published there as a new version after the owner approves it; see
[VPM Publishing](https://github.com/Rionquosue/riryunityworkflow/blob/main/docs/tutorials/vpm-publishing.md).

The package's own files are replaced on every update. Anything that
has to live inside the project is unpacked into **`Assets/RiryTools`** on first
load, and tools save generated assets there when they have no better place
(Constraint Train clips, `_ROOT` variants of read-only prefabs). Existing files
there are never overwritten, so edits survive updates; delete a file to get the
package's copy back. Shader applets will live there too.

## Menus

Every tool is under **Tools > RiryTools**, as one entry or a submenu named after
the tool. On Windows, the menus can be walked from the keyboard by typing each
entry's first letter.

## Documentation

Tutorials and design notes are in the source repository's
[`docs`](https://github.com/Rionquosue/riryunityworkflow/tree/main/docs) folder.

## Blendshape Sync

Open **Tools > RiryTools > Blendshape Sync** to add a job: a popup where you drag in a
master SkinnedMeshRenderer and children. Jobs keep running after the popup closes,
are listed (Auto, Sync Now, Edit, Set All to Original, Remove) in the Main Menu's
Blendshape Sync tab, and are saved per scene. In the popup, an empty child row appears after each addition. Use each child's
**Manual link** foldout to override name matching with Source/Destination names.

**Sync Now** copies matching weights. **Run automatically** performs one pass
every 0.5 seconds. **Case-sensitive** enables exact matching; ambiguous
case-insensitive matches block the pass and offer to switch to it.
**Set All to Original** restores all cached master/child weights and stops auto
sync. The original cache lasts while the job runs, including script
reloads. Closing the window ends that cache.

Sync and restore pause during Play Mode transitions, animation preview/recording,
and Animator graph editing. Changes support Undo and prefab overrides.
See the [tutorial](https://github.com/Rionquosue/riryunityworkflow/blob/main/docs/tutorials/blendshape-sync.md) and [design notes](https://github.com/Rionquosue/riryunityworkflow/blob/main/docs/design/blendshape-sync.md).

## Clean Animation Keys

Open **Tools > RiryTools > Clean Animation Keys**, right-click animations in the
Project window and choose **RiryTools > Clean Animation Keys**, or click **Clean
Keys** in the Animation window. List up to 64 animations, add shapekey and bone
names to the whitelist (with fuzzy suggestions, or per animation with
**Whitelist**), choose which built-in keep rules apply, and click **Clean**. Only
editable `.anim` files are changed, as one Undo step.
See the [tutorial](https://github.com/Rionquosue/riryunityworkflow/blob/main/docs/tutorials/clean-animation-keys.md) and [design notes](https://github.com/Rionquosue/riryunityworkflow/blob/main/docs/design/clean-animation-keys.md).

## Import Presets

Open **Tools > RiryTools > Import Presets > Set Folder Import Presets** to set a
preset per file type (Image files, Model files...) for a folder from
`Documents/RiryTools/ImportPresets`; right-clicking folders and choosing
**RiryTools > Set Folder Import Presets** gives a small popup for several folders.
Apply sets the files already there; new files directly in the folder (not its
subfolders) get them on their first import, and reimports and hand-edited
settings are never changed. Models can have their animations extracted to
`Animations/` as Generic clips before a Humanoid preset applies (with
**RiryTools > Extract Animation**, which can also clean the keys), and `.blend` files can be
exported with the Better FBX Blender add-on. Save presets from any asset with
**RiryTools > Save Import Preset**. See the [tutorial](https://github.com/Rionquosue/riryunityworkflow/blob/main/docs/tutorials/import-presets.md) and [design notes](https://github.com/Rionquosue/riryunityworkflow/blob/main/docs/design/import-presets.md).

## Stop Auto Game

Entering Play Mode normally focuses the Game view. With **Tools > RiryTools > Stop Auto Game** (on by default), the windows
that had focus get it back, and a Game view that Play Mode opened because the layout had none is
closed when Play Mode ends. Choose the entry again to turn it off. It is a
per-user setting. See the [tutorial](https://github.com/Rionquosue/riryunityworkflow/blob/main/docs/tutorials/stop-auto-game.md).

## Constraint Train

Requires the VRChat Avatars SDK 3.7.0 or newer. Open **Tools > RiryTools >
Constraint Train** (or right-click a bone in the Hierarchy > **RiryTools >
Constraint Train**) to drive a real bone chain onto up to five physics chains
with VRC Parent Constraints and a Motion Time clip. The tip leads a staggered
"train" onto physics chain 1, then every bone blends from chain to chain along an
editable curve. It can generate `Phys1_`..`Phys5_` and `Rest_` empty chains,
with endpoints, without unpacking prefabs.
See the [tutorial](https://github.com/Rionquosue/riryunityworkflow/blob/main/docs/tutorials/constraint-train.md) and [design notes](https://github.com/Rionquosue/riryunityworkflow/blob/main/docs/design/constraint-train.md).

## GUID Tools

Open the GUID Tools window from:

- Main menu: **Tools > RiryTools > GUID Tools**

To run directly from the Project window, select one or more assets or folders,
then right-click and choose **RiryTools > Regenerate GUID**. Selected folders are
expanded recursively; their own folder GUIDs are never changed.

The tool generates a new GUID for every selected asset and updates exact GUID
references in the project's text-serialized files without changing local
file IDs. It requires **Force Text** asset serialization, saved scenes, and a
closed Prefab Mode. Open scenes are reloaded from disk afterwards. It bypasses
Unity Undo, so commit or back up the project first.

Reverse-reference indexes are cached under
`Library/RiryTools/GuidReferenceCache`. Delete them from the GUID Tools window. Diagnostic logs are written
to `Logs/RiryTools/GuidTools/` in the project; the active log rotates at 10 MiB, also
during a run, and archived logs are trimmed oldest-first above 100 MiB.

Every regeneration is recorded in `ProjectSettings/RiryToolsGuidRemap.tsv`.
**RiryTools > Repair Missing GUID References** relinks GUIDs that no longer
resolve in damaged or recovered files, using that ledger and then Git history.
See the [tutorial](https://github.com/Rionquosue/riryunityworkflow/blob/main/docs/tutorials/guid-tools.md) and [design notes](https://github.com/Rionquosue/riryunityworkflow/blob/main/docs/design/guid-tools.md).

## AutoSync

**Tools > RiryTools > AutoSync > Sync** (also **Git > Sync**) commits every scene
and prefab save to the checked-out branch, titled with the time of the save, and
pushes it when there is a remote. Turning it on offers to download Git when it is
missing, creates the repository from the Unity `.gitignore` and `.gitattributes`
templates in `Documents/RiryTools/GitTemplates`, and enables Git LFS.
**Configuration** picks other templates and sets **Push to remote**; sign-in
goes through Git's credential manager. AutoSync also blocks saving a scene or
prefab that a script stopped from loading. See the [tutorial](https://github.com/Rionquosue/riryunityworkflow/blob/main/docs/tutorials/autosync.md) and [design notes](https://github.com/Rionquosue/riryunityworkflow/blob/main/docs/design/autosync.md).

## Editing the UI

Every menu entry (Tools dropdown, Project and Hierarchy right-click, Git menu)
is in `Editor/UI/RiryMenus.cs`, except Constraint Train's, which are in
`Editor/VRChat/RiryVRChatMenus.cs` because they need the VRChat SDK. Change a
path or priority there to move an entry. Each window's drawing code is in
`Editor/UI/` (Constraint Train's in `Editor/VRChat/ConstraintTrainWindow.UI.cs`),
with a glossary of its fields and methods at the bottom of each file.
