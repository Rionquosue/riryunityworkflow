# Changelog


----

## 0.2.0

### Added

- **Attach To**: snaps the selected objects to an avatar's bone, then offers to reparent them, unpacking prefabs only after a warning. In the Hierarchy's right-click menu; each added bone has a Remove button.
- **Avatar Migration Assistant**: copies components and settings from an old avatar to a new one and retargets its animations. Automatic and manual object matching, type rules read from `types.yml`, and migration of unpacked avatars onto packed prefabs with different meshes.
- **FTEmulator** (VRChat Avatars SDK 3.7.0 or newer, Gesture Manager 3.9 or newer; native face tracking needs SDK 3.10.6): a face tracking emulator for Play Mode. Takes VRCFaceTracking's OSC output (its own receiver and OSCQuery service, without a VRChat client) or manual values, holds them to a hardware profile (Quest Pro, Babble, Vive, or unrestricted), and drives the avatar Gesture Manager runs through native face tracking or FT/v2 parameters. A debug window shows live and applied values per parameter, tracking key and hardware control. Webcam input is not included.
- **IconCap**: captures square PNG icons from the Scene View through an effect stack, with jobs, presets and Icon Generator assets.
- **Main Menu**: one dockable window with a tab per tool, and a Tools tab with the sub-tabs General and FTEmulator.
- **Menu Mockup**: lays out an example editor window from blocks, including working tab strips (Tabs block), and saves it as `.yml`.
- **Menu Tool** (VRChat Avatars SDK 3.7.0 or newer): traces expression menu controls to their source (descriptor, VRCFury; Modular Avatar is not read for now) and compares the parameter budget with VRCFury's Parameter Compressor off and on. Its Menu manager previews the menu in Gesture Manager's radial in Edit Mode and plans moves, new submenus, new controls, menu icons, renames (radial right-click or triple-click in the list), deletes (radial right-click or the Delete key) and Move to… > <NULL> (a VRCFury Move Menu Item to a blank path). Apply makes them through the system that owns each control, as one undo step: menu assets are edited in place, never copied, and VRCFury paths inside a moved or renamed submenu follow it. Move to… is nested by submenu. An entry state can be saved and replayed by Gesture Manager in Play Mode; Reset menu state sets the preview back to its defaults. Rescan rebuilds the scan, preview and radial.
- **Animator Tools > Find and Replace** (VRChat Avatars SDK): finds and replaces strings in animator state behaviours across an avatar's controllers or one controller, as one undo step. Play Audio paths can be repathed to a scene object and Parameter Driver names mapped to a controller parameter; results open their state in the Animator window. Changes can be applied to the main prefab or to a child prefab.
- **Find Null references**: shows where each NullReferenceException in the Console was thrown and lists the empty or missing references involved, or scans the open scenes when the log gives nothing.
- **Animator Tools > Controller Cleaner**: finds animator controller sub-assets no layer reaches (states, state machines, transitions, blend trees, behaviours) in one controller, the open scenes' controllers or the whole project, and removes them after asking.
- **Material Cleaner** (Main Menu's Tools tab): removes texture slots a material's current shader doesn't have, for one material, the open scenes' materials or the whole project, one undo step per Clean.
- **Refresh Scripts**: imports changed files, reloads all scripts, and reopens the open RiryTools windows with fresh state, from the Tools menu or the Main Menu's Tools tab.

### Changed

- **Constraint Train** is now **Constraint Tool**, one window with three subtools:
  - **Train**: drives a bone chain onto physics chains with VRC Parent Constraints and a Motion Time clip. Every bone starts at one Start position (the generated Rest chain is gone) and the real chain's end bone is where the train stops. The constraints are set up switched off with every source at 0 and the bones left at rest; the clip switches them on.
  - **Bezier**: rigs a bone chain to a cubic Bezier curve with VRC constraints, on the bones or a mechanism chain.
  - **Insertable prop** (SDK 3.10.6 or newer): a grabbable prop that slides back into its socket, or drops (thrown as a physics body on PC, onto a raycast hit on Android).
- **Script Load Guard** can be turned on and off, like Stop Auto Game.
- **Animator Tools** holds Find and Replace, Clean Animation Keys, Controller Cleaner and Extract Animations, in the Main Menu, the Tools > RiryTools menu and the Project window's right-click menu. The Clean Animation Keys window opens at the left edge of the main window.
- **GUID Tools**: Check for Broken References shows the object and field holding each broken reference (click to select it) and lists missing objects in assets that still exist. References can be ignored (also skipped by the save check), unused prefab overrides scrubbed, and a reference repaired in Prefab Mode. The scan stops at animator controllers and shaders, which only the Project window's Repair Missing GUID References opens, and leaves out empty slots, prefab bookkeeping, unused controller sub-assets, material texture slots the shader doesn't have, references nothing reads and a file's references to itself. After Apply, Controller Cleaner is offered on the controllers it touched.
- **Blendshape Sync**: jobs can be named; blank keeps the master and child count.
- **Import Presets**: Filters choose whether tags and `_word` name endings make file types of their own, or only extensions count.
- Project restructure: files and classes carry their tool's prefix; shared code is in `Components/`, windows in `UI/`, and VRChat tools in `VRChat/<tool>/`. The menus are split into `RiryToolsMenuBar` and `RiryToolsRightClickMenu`.
- Every tool logs under its own name, and each problem is logged once, with what it concerns.

### Fixed

- Ctrl+Z undoes each action in one step, and nothing else.
- Tools no longer overwrite or lose data they should keep.
- Overlapping and cut-off window layouts, long text crowding the UI, and the Main Menu's tab height changing while resizing.
- The Menu Tool's radial preview no longer blinks or animates, and stays put while a row is dragged.
- Compile errors from an internal GUIUtility member and a missing using, and from a PrefabUtility method Unity 2022.3 doesn't have.
- Menu Tool: menu items with a `/` in their name could not be moved, renamed or given an icon.


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
