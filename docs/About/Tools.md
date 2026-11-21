# Tool Documentation Structure

This file lists each tool with a brief description of what it does, based on the menu structure.

## Tools
## Avatar Migration Assistant

Avatar Migration Assistant: copies components and settings from an old avatar (source) to a new one (target). Objects are matched between the two avatars, with Manual matching for anything left unmatched; the run then copies the chosen component types, repoints references to the target's objects, corrects rotation and scale, and drops duplicates of components the target already had. Dry run does the same run and reports it, then undoes it all. Retarget animations duplicates the target's controllers and the clips they use into a migrated folder, rewrites the clips' paths for the new avatar through the object match, and points the target at the duplicates. The source and the original files are never changed, and a copy run is one undo step. Without the VRChat SDK the VRChat parts are left out.

## Blendshape Sync

Blendshape Sync: copies the blendshape weights of a master mesh to child meshes that have blendshapes of the same name (case-sensitive or not, plus manual source-to-destination links). Each job is a master and its children with a name (by default the master and its child count); jobs sync automatically or on Sync Now, keep running after their window closes, are saved per scene, and can set every weight back to its original value. Only renderer weights change; meshes and animation clips are never changed.

## Constraint Tools

Constraint Tools: one window with a tab per subtool, all built on VRChat constraints:
- Train: sets up VRC Parent Constraints on a bone chain and builds a Motion Time clip that moves the chain onto physics chains as a staggered train. Every bone starts at one Start position; the real chain's end bone is where the train stops, so each bone stops one physics-chain bone further along, then blends from one physics chain to the next. Physics chains can be generated. The constraints are set up switched off with every source at 0 and the bones left at rest; the clip switches them on.
- Bezier: rigs a bone chain to one cubic Bezier curve (two anchors, two handles) so moving the handles bends the chain, with optional twist and stretch.
- Insertable prop: generates a draw, drop and reinsert system for a prop and its socket (a dagger and sheath, for example): grabbed by a PhysBone, slides back in when released near the socket, otherwise drops. Needs VRChat SDK 3.10.6 or newer.
- Dual axis detector / Three axis detector: VRC contacts that read how far Source moves left/right and up/down across a flat plane fixed to Relative to. Build adds TiltDetector_<source> under Relative to (at Source's rest position, axes from Relative to (Local) or the world (World), the plane facing X, Y or Z) with Proximity receivers Offset away and twice that in radius, and TiltSender_<source> under Source with the sender, sharing one collision tag. Dual axis lays out two receivers in an L (`Tilt/Right`, `Tilt/Up`); Three axis three in a triangle 120° apart (`Tilt/Top`, `Tilt/BottomLeft`, `Tilt/BottomRight`). Nothing existing is changed; on a prefab instance both are added objects, so it stays packed. One undo step.

## FTEmulator

FTEmulator: a face tracking emulator for Play Mode, paired with Gesture Manager (VRChat Avatars SDK 3.7.0 or newer; native face tracking needs 3.10.6). Input is VRCFaceTracking over OSC (FTEmulator listens on 127.0.0.1 and advertises the avatar through an OSCQuery service, so no VRChat client runs) or values typed by hand. A hardware profile (Base Quest Pro, Quest Pro with TongueOut, Babble lower face, the Vive configurations, or unrestricted) limits the input to the channels that device has and keeps channels it shares coupled. Output goes to the avatar Gesture Manager runs: native face tracking (Blendshapes Sets, Parameter Behaviors, set weight state behaviors) or the FT/v2 parameters of Adjerry and Pawlygon 1.x setups, detected from the avatar. The debug window lists parameters, tracking keys and hardware controls with live and applied values, lets each be held, typed or switched off, and shows what is blocked, unmapped or only partly previewed. Record keeps the OSC packets the receiver takes and saves them to Documents/RiryTools/FTEmulator/Recordings; Play recording feeds a saved recording back through the receiver at its recorded times. Tools > RiryTools > FTEmulator, and the Main Menu's FTEmulator tab.

The VRCFaceTracking companion (a module, a Recorder app that relays VRCFaceTracking's output to VRChat, records it with the visemes and microphone, and can start with SteamVR, and an app that turns videos into face tracking and visemes) is described in [Tools/FTEmulatorCompanion.md](Tools/FTEmulatorCompanion.md).

## GUID Tools

GUID Tools: regenerates GUIDs for selected assets and folders' contents while keeping every reference to them intact (folder GUIDs never change), and repairs missing GUID references, using a permanent ledger of replaced GUIDs kept in ProjectSettings and its Git history. Check for Broken References lists the missing references of the open scenes and the files they use, with the object and field holding each one (click to select it); the scan stops at animator controllers and shaders, which are only opened from the Project window's Repair Missing GUID References. Empty slots, prefab bookkeeping, unused controller sub-assets, material texture slots the shader doesn't have and references nothing reads are not listed. A reference can be ignored, and Prefab Cleaner removes unused prefab overrides; Call material cleaner opens Material Cleaner. Prefab Cleaner is also in the Project window's right-click menu and the Main Menu's PopUp Tools, where it asks for a target (Scene, Project or Prefab). After a repair, Controller Cleaner is offered on the controllers it touched. It also offers Check for Broken References when an opened scene or prefab points at missing assets.

## Menu Tools

Menu Tools: traces expression menu controls to their sources (avatar descriptor, VRCFury; Modular Avatar is not read for now). The Parameter budget tab compares synced parameter cost with and without VRCFury's Parameter Compressor. The Menu manager tab warns about controls sharing a name in one submenu and icons over the 256 px the SDK uploads (Apply menu_icon sets their max size with the menu_icon import preset), and previews the assembled radial menu in Edit Mode with Gesture Manager's radial, and plans moves, new menus, new controls, icons, renames and deletes, plus Move to <NULL> (a VRCFury Move Menu Item to a blank path). Apply makes them through the system owning each control, as one undo step: menu assets are edited in place, and VRCFury paths inside a moved or renamed submenu follow it. The radial's state can be replayed when Gesture Manager starts Play Mode.

## AutoSync

AutoSync: while on, every scene or prefab save is committed to the checked-out Git branch, titled with the time of the save (or an editable template using {time}, {files} and {branch}), and pushed to the chosen remote. When the remote has new commits, the branch is fast-forwarded or rebased onto them first; a conflicting rebase is aborted and the commits are moved to a new autosync-conflict branch, pushed there, and the user is told to merge it by hand, with Sync left on. Only the saved files and their .meta files are committed. Configuration sets the branch, remote, .gitignore and .gitattributes. Off until turned on, per user and per project.

## Clean Animation Keys

Clean Animation Keys: removes blank blendshape curves and static bone transform curves from animations that key every bone and shapekey. Built-in options and a name whitelist decide what is kept, and Preview lists what Clean would remove.

## Controller Cleaner

Controller Cleaner (Animator Tools): scans one animator controller, the controllers the open scenes use, or every controller in the project for sub-assets no layer reaches (states, state machines, transitions, blend trees and behaviours), and removes them on Clean. It asks first; each Clean is one undo step and is logged.

## Find and Replace

Find and Replace (Animator Tools): finds strings in animator state behaviours (case-insensitive) across an avatar's controllers (playable layers, VRCFury Full Controllers, Modular Avatar Merge Animators) or one animator controller, and replaces them as one undo step: the whole string, only the searched text (Partial), or regular expression matches (Regex). Play Audio paths can be repathed to a scene object and Parameter Driver names mapped to a controller parameter. Changes can be applied to the main prefab or a child prefab. Each result opens its state in the Animator window.

## Import Presets

Import Presets: assigns import presets to folders by file type (images, models, and so on, or name tags and endings like "_normal"), applied to files imported for the first time in that folder; reimports never change settings. While files from outside Unity are dragged over such a folder, the preset each will get is shown, and the scroll wheel picks another when there is a choice. Filters choose whether tags and "_word" endings make file types of their own, or only extensions count. Also saves presets from an asset's importer and keeps a shared preset library.

## Material Cleaner

Material Cleaner: scans one material, the materials the open scenes use, or every material in the project for texture slots, floats and colors its current shader doesn't have, and removes them, with the textures they reference, on Clean. A material whose shader is missing is skipped. Also in the Project window's right-click menu (with the selected material) and the Broken References window. Each Clean is one undo step and is logged.

## Attach To

Attach To (Hierarchy right-click): moves the selected objects to the global position of one of an avatar's bones, then asks whether to move them in the hierarchy; Yes reparents them to the bone, keeping their position, rotation and scale. A prefab instance is only unpacked after a warning. One undo step.

## Refresh Scripts

Refresh Scripts: imports changed files, closes the open RiryTools windows, reloads all scripts, then reopens those windows at their old positions with fresh state, so UI changes show without restarting Unity.

## Stop Auto Game

Stop Auto Game: when Play Mode starts, gives focus back to the windows that had it instead of the Game view, and closes a Game view that Play Mode opened when Play Mode ends.

## Script Load Guard

Script Load Guard: detects a scene or prefab that loaded with far fewer objects than its file holds, names the script that logged errors during the load, blocks saving it so the file is not overwritten, and holds the editor in a window until it is resolved. Can be turned off.

## IconCap

IconCap: captures square PNG icons from the Scene View, runs them through an effect stack and saves them to an export folder. Captures can be saved as jobs and remade later.

## Find Null References

Find Null References: reads the Console's NullReferenceException entries, shows the project or package file and line each was thrown at, and lists empty or missing references on the scene components those errors involved. If the log gives nothing, it scans the whole open scene for missing references and missing scripts. Runs once and shows the results in a popup.

## Extract Animations

Extract Animations (Animator Tools): copies a model's animation clips into an Animations folder (or a chosen destination) as plain bone-curve clips, importing the model as Generic for the copy, optionally cleaning them with Clean Animation Keys. Can run automatically on import.

## Menu Mockup

Menu Mockup: lays out an example editor window from blocks (every component is a block, blocks nest and lay out like flexbox) using a Components palette. Mockups are saved as .yml files in Documents/RiryTools/Mockups; nothing is compiled.

## Animator Presets

Animator Presets: a separate package (`com.riry.animatortemplates`) of animator templates. YGDR templates (layers and blend trees) appear in YGDR Animator Editor's layer **+** menu and **Import Template** list; MACS templates (blend trees) appear in a **Riry** submenu of the Animator window's blend tree right-click and are inserted with a window that names each parameter, remakes the AAP clips and adds missing parameters, as one undo step. Needs the VRChat SDK (Harmony) and the tool each extends.

## Stop all tool scripts

Stop all tool scripts (Debug Tools): an emergency switch that keeps every RiryTools script from running. While on, load hooks, importers and save processors do nothing, windows show only a notice and menu entries are greyed out, except this toggle, the Main Menu and Documentation. It stays on for this user and project until turned off; toggling it reloads scripts.
