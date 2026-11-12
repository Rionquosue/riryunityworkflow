# Changelog


----

## 0.2.0

### Added

- **Attach To**: snaps the selected objects to an avatar's bone, then offers to reparent them, unpacking prefabs only after a warning. In the Hierarchy's right-click menu; each added bone has a Remove button.
- **Avatar Migration Assistant**: copies components and settings from an old avatar to a new one and retargets its animations. Automatic and manual object matching, type rules read from `types.yml`, and migration of unpacked avatars onto packed prefabs with different meshes.
- **IconCap**: captures square PNG icons from the Scene View through an effect stack, with jobs, presets and Icon Generator assets.
- **Main Menu**: one dockable window with a tab per tool, and a Tools tab.
- **Menu Mockup**: lays out an example editor window from blocks, including working tab strips (Tabs block), and saves it as `.yml`.
- **Menu Tool** (VRChat Avatars SDK 3.7.0 or newer): traces expression menu controls to their source (descriptor, Modular Avatar, VRCFury) and compares the parameter budget with VRCFury's Parameter Compressor off and on. Its Menu manager previews the menu in Gesture Manager's radial in Edit Mode and plans moves, new submenus, new controls and menu icons, which Apply makes through the system that owns each control, as one undo step. Move to… is nested by submenu. An entry state can be saved and replayed by Gesture Manager in Play Mode; Reset menu state sets the preview back to its defaults.
- **Animator Tools > Find and Replace** (VRChat Avatars SDK): finds and replaces strings in animator state behaviours across an avatar's controllers or one controller, as one undo step. Play Audio paths can be repathed to a scene object and Parameter Driver names mapped to a controller parameter; results open their state in the Animator window.
- **Find Null references**: shows where each NullReferenceException in the Console was thrown and lists the empty or missing references involved, or scans the open scenes when the log gives nothing.
- **Refresh Scripts**: imports changed files, reloads all scripts, and reopens the open RiryTools windows with fresh state, from the Tools menu or the Main Menu's Tools tab.

### Changed

- **Constraint Train** is now **Constraint Tool**, one window with three subtools:
  - **Train**: drives a bone chain onto physics chains with VRC Parent Constraints and a Motion Time clip.
  - **Bezier**: rigs a bone chain to a cubic Bezier curve with VRC constraints, on the bones or a mechanism chain.
  - **Insertable prop** (SDK 3.10.6 or newer): a grabbable prop that slides back into its socket, or drops (thrown as a physics body on PC, onto a raycast hit on Android).
- **Script Load Guard** can be turned on and off, like Stop Auto Game.
- Project restructure: files and classes carry their tool's prefix; shared code is in `Components/`, windows in `UI/`, and VRChat tools in `VRChat/<tool>/`. The menus are split into `RiryToolsMenuBar` and `RiryToolsRightClickMenu`.
- Every tool logs under its own name, and each problem is logged once, with what it concerns.

### Fixed

- Ctrl+Z undoes each action in one step, and nothing else.
- Tools no longer overwrite or lose data they should keep.
- Overlapping and cut-off window layouts, long text crowding the UI, and the Main Menu's tab height changing while resizing.
- The Menu Tool's radial preview no longer blinks or animates, and stays put while a row is dragged.
- Compile errors from an internal GUIUtility member and a missing using.


----

## 0.1.0

First release of RiryTools (`com.riry.rirytools`), editor-only tools for Unity 2022.3 LTS:

- **AutoSync**: commits every scene and prefab save to the checked-out branch and pushes it, sets up the repository and Git LFS, and blocks saving a scene or prefab that a script stopped from loading.
- **Blendshape Sync**: copies blendshape weights from a master renderer to child renderers.
- **Clean Animation Keys**: removes blank shapekey curves and static bone curves from up to 64 animations, with a whitelist and keep rules.
- **Constraint Train** (VRChat Avatars SDK 3.7.0 or newer): drives a bone chain onto physics chains with VRC Parent Constraints and a Motion Time clip.
- **GUID Tools**: regenerates asset GUIDs while keeping references, and repairs missing GUID references.
- **Import Presets**: per-folder import presets, animation extraction (with an Extract Animation window that can clean keys and extract a model automatically) and Better FBX export.
- **Stop Auto Game**: keeps focus off the Game view when Play Mode starts.
