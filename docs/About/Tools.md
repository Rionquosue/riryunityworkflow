# Tool Documentation Structure

This file lists each tool with a brief description of its purpose and functionality based on the menu structure.

## Tools
## Avatar Migration Assistant

The Avatar Migration Assistant tool streamlines avatar migration between versions. It handles prefab copying, rotation scaling, reference repointing, and ensures consistency across versions. This tool is useful for maintaining asset integrity during project transitions and migrations from one version of the avatar to another.

## Blendshape Sync

The Blendshape Sync tool synchronizes blendshape animations between models, aligning blendshape targets and synchronizing during animation preview when active. It supports batch processing of blendshape targets, allowing a user to duplicate their body or customization shapekeys to a target mesh.

## Constraint Tool

Constraint Tool: one window with a tab per subtool, all built on VRChat constraints:
- Train: sets up VRC Parent Constraints on a bone chain and builds a Motion Time clip that moves the chain onto physics chains as a staggered train.
- Bezier: rigs a bone chain to one cubic Bezier curve (two anchors, two handles) so moving the handles bends the chain, with optional twist and stretch.
- Insertable prop: generates a draw, drop and reinsert system for a prop and its socket (a dagger and sheath, for example): grabbed by a PhysBone, slides back in when released near the socket, otherwise drops. Needs VRChat SDK 3.10.6 or newer.

## GUID Tools

GUID Tools: regenerate GUIDs for selected assets and folders' contents while keeping every reference to them intact (folder GUIDs never change), and repair missing GUID references, using a permanent ledger of replaced GUIDs kept in ProjectSettings and its Git history. Also offers Check for Broken References when an opened scene or prefab points at missing assets.

## Menu Tool

Menu Tool: traces expression menu controls to their sources (avatar descriptor, Modular Avatar, VRCFury). The Parameter budget tab compares synced parameter cost with and without VRCFury's Parameter Compressor. The Menu manager tab previews the assembled radial menu in Edit Mode with Gesture Manager's radial, and plans moves, new menus, new controls and icons that Apply makes through the system owning each control, as one undo step.

## AutoSync

AutoSync: while on, every scene or prefab save is committed to the checked-out Git branch, titled with the time of the save, and pushed to the chosen remote. Only the saved files and their .meta files are committed. Configuration sets the branch, remote, .gitignore and .gitattributes. Off until turned on, per user and per project.

## Clean Animation Keys

Clean Animation Keys: removes blank blendshape curves and static bone transform curves from animations that key every bone and shapekey. Built-in options and a name whitelist decide what is kept.

## Find and Replace

Find and Replace (Animator Tools): finds strings in animator state behaviours (case-insensitive) across an avatar's controllers (playable layers, VRCFury Full Controllers, Modular Avatar Merge Animators) or one animator controller, and replaces whole strings as one undo step. Play Audio paths can be repathed to a scene object and Parameter Driver names mapped to a controller parameter. Each result opens its state in the Animator window.

## Import Presets

Import Presets: assigns import presets to folders by file type (images, models, and so on, or name endings like "_normal"), applied to files imported for the first time in that folder; reimports never change settings. Also saves presets from an asset's importer, keeps a shared preset library, and can extract animations on import.

## Refresh Scripts

Refresh Scripts: imports changed files, closes the open RiryTools windows, reloads all scripts, then reopens those windows at their old positions with fresh state, so UI changes show without restarting Unity.

## Stop Auto Game

Stop Auto Game: when Play Mode starts, gives focus back to the windows that had it instead of the Game view, and closes a Game view that Play Mode opened when Play Mode ends.

## IconCap

IconCap: captures square PNG icons from the Scene View, runs them through an effect stack and saves them to an export folder. Captures can be saved as jobs and remade later.

## Find Null References

Find Null References: reads the Console's NullReferenceException entries, shows the project or package file and line each was thrown at, and lists empty or missing references on the scene components those errors involved. If the log gives nothing, it scans the whole open scene for missing references and missing scripts. Runs once and shows the results in a popup.

## Extract Animation

Extract Animation: copies a model's animation clips into an Animations folder (or a chosen destination) as plain bone-curve clips, importing the model as Generic for the copy, optionally cleaning them with Clean Animation Keys. Can run automatically on import.

## Menu Mockup

Menu Mockup: lays out an example editor window from blocks (every component is a block, blocks nest and lay out like flexbox) using a Components palette. Mockups are saved as .yml files in Documents/RiryTools/Mockups; nothing is compiled.
