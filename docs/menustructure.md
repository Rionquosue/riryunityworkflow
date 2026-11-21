# This file dictates how the menus should be structured

As the tool updates, please follow existing menu structure. If a tool does not have an assigned section, it should be assigned the one most relevant to its behaviour or given a new entry accordingly.

## Common rules

All items should have a single window built for them. The main menu will embed or call these windows as required.

All naming conventions should be consistent across the board. Any names that do not match up should be reviewed.

Consistent names should all be simple and descriptive of what that tool is used for. Such as "GUID Tools" being a tool that will find and repair missing GUID's, regenerate all GUID's for a selected object, or regenerate all GUID's within a specific directory. "GUID Tools" is simple and descriptive, the rest of the buttons and conventions as sub-tools of this should follow suit.

If an item requires fixed height or width to be usable, it should not be docked in the Main Menu and instead be moved to a window-call button within the appropriate category. Such as "Tools" "Debug" or "Animator Tools"

### Right click menu

**Hierarchy**

    * Avatar Migration Assistant
    * Blendshape Sync
    * Constraint Tools >
        * Train
        * Bezier
        * Insertable prop
        * Dual axis detector
        * Three axis detector
    * GUID Tools
    * Menu Tools

**Project folder**

    * Animator Tools >
        * Clean Animation Keys
        * Extract Animations
        * Find And Replace
    * GUID Tools >
        * Regenerate GUID(s)
        * Prefab Cleaner
    * Material Cleaner
    * Import Presets
        * Set Folder Preset
        * Save Import Preset (only when clicking file, else grayed)
        
### Toolbar menu

    Formatted as breadcrumbs where > expands into a submenu.

**Tools > RiryTools**

    * Main Menu
    * AutoSync >
        * Configure
        * [ ] Sync now
    * [ ] Stop Auto Game
    * Constraint Tools
    * Animator Tools >
        * Clean Animation Keys
        * Find and Replace
        * Controller Cleaner
        * Extract Animations
    * Avatar Tools >
        * Avatar Migration Assistant
        * Blendshape Sync
        * Menu Tools
    * Utilities >
        * GUID Rebuild tool
        * GUID Repair tool
        * IconCap
        * Material Cleaner
    * Import Presets >
        * Set Folder Presets
        * Save Import Preset
        * Import Preset Library
    * Debug Tools >
        * Refresh Scripts
        * Menu Mockup
        * [ ] Stop all tool scripts
    * FTEmulator
    * Documentation

FTEmulator has no right-click entry. The entry and the Main Menu's FTEmulator tab come from FTEmulator's own package, `com.riry.ftemulator`; without it (or without the VRChat SDK) RiryTools keeps the entry and the tab, saying why FTEmulator is not available.

### Main menu

Structured in such a way that it can be presented as a table. Laid out as cells. Cells left blank are to be considered blank space in the tool tab. If components of a tool are not present they can be assumed to be filled in, as this table is primarily to structure the tabs and what is laid out within them instead of the structure of each tool. Where there is no [ ] as a selected menu item, this tab is assumed to be an unordered list of buttons that will call the popups for those selected tools.

#### Main tab

Where [] contains the currently active tab. The primary tabs are General Tools, Menu Tools, Animator Tools, Debug Tools and FTEmulator, always on one row. General Tools has the sub-tabs Blendshape Sync, Import Presets, Constraint Tools, AutoSync and PopUp Tools; Import Presets and Constraint Tools have sub-tabs of their own. PopUp Tools lists the tools that need their own window, as buttons. FTEmulator's debug tables need the width, so they open as a popup (Open debug window).

| [General Tools] | Menu Tools | Animator Tools | Debug Tools | FTEmulator |  |
| :---: | :---: | :---: | :---: | :---: | :---: |
| [Blendshape Sync] | Import Presets | Constraint Tools | AutoSync | PopUp Tools |  |
| Running sync job list |  |  |  |  |  |
| Add / edit job (opens popup) |  |  |  |  |  |

---

| [General Tools] | Menu Tools | Animator Tools | Debug Tools | FTEmulator |  |
| :---: | :---: | :---: | :---: | :---: | :---: |
| Blendshape Sync | [Import Presets] | Constraint Tools | AutoSync | PopUp Tools |  |
| [Set Folder Import Presets] | Save Import Preset | Import Preset Library |  |  |  |
| Set Folder Import Presets UI here |  |  |  |  |  |

---

| [General Tools] | Menu Tools | Animator Tools | Debug Tools | FTEmulator |  |
| :---: | :---: | :---: | :---: | :---: | :---: |
| Blendshape Sync | [Import Presets] | Constraint Tools | AutoSync | PopUp Tools |  |
| Set Folder Import Presets | [Save Import Preset] | Import Preset Library |  |  |  |
| Asset: the selected asset's path |  |  |  |  |  |
| Importer: its importer type |  |  |  |  |  |
| Configuration ▾ (New configuration, or an existing one) |  |  |  |  |  |
| New name (with New configuration) |  |  |  |  |  |
| Preset name |  |  |  |  |  |
| Open Library | Save | Close |  |  |  |

---

| [General Tools] | Menu Tools | Animator Tools | Debug Tools | FTEmulator |  |
| :---: | :---: | :---: | :---: | :---: | :---: |
| Blendshape Sync | [Import Presets] | Constraint Tools | AutoSync | PopUp Tools |  |
| Set Folder Import Presets | Save Import Preset | [Import Preset Library] |  |  |  |
| Display Import Preset Library here |  |  |  |  |  |
| Click to open in explorer | File input box here | Save import preset (creates from selected in input box) |  |  |  |

---

| [General Tools] | Menu Tools | Animator Tools | Debug Tools | FTEmulator |  |
| :---: | :---: | :---: | :---: | :---: | :---: |
| Blendshape Sync | Import Presets | [Constraint Tools] | AutoSync | PopUp Tools |  |
| [Train] | Bezier | Insertable prop | Dual axis detector | Three axis detector |  |
| Train UI here |  |  |  |  |  |

---

| [General Tools] | Menu Tools | Animator Tools | Debug Tools | FTEmulator |  |
| :---: | :---: | :---: | :---: | :---: | :---: |
| Blendshape Sync | Import Presets | [Constraint Tools] | AutoSync | PopUp Tools |  |
| Train | [Bezier] | Insertable prop | Dual axis detector | Three axis detector |  |
| Bezier UI here |  |  |  |  |  |

---

| [General Tools] | Menu Tools | Animator Tools | Debug Tools | FTEmulator |  |
| :---: | :---: | :---: | :---: | :---: | :---: |
| Blendshape Sync | Import Presets | [Constraint Tools] | AutoSync | PopUp Tools |  |
| Train | Bezier | [Insertable prop] | Dual axis detector | Three axis detector |  |
| Insertable prop UI here |  |  |  |  |  |

---

| [General Tools] | Menu Tools | Animator Tools | Debug Tools | FTEmulator |  |
| :---: | :---: | :---: | :---: | :---: | :---: |
| Blendshape Sync | Import Presets | [Constraint Tools] | AutoSync | PopUp Tools |  |
| Train | Bezier | Insertable prop | [Dual axis detector] | Three axis detector |  |
| Source field |  |  |  |  |  |
| Relative to field |  |  |  |  |  |
| Flat plane ▾ (X, Y, Z) |  |  |  |  |  |
| Space ▾ (Local, World) |  |  |  |  |  |
| Offset |  |  |  |  |  |
| Parameters: Right, Up |  |  |  |  |  |
| Build dual axis detector |  |  |  |  |  |

---

| [General Tools] | Menu Tools | Animator Tools | Debug Tools | FTEmulator |  |
| :---: | :---: | :---: | :---: | :---: | :---: |
| Blendshape Sync | Import Presets | [Constraint Tools] | AutoSync | PopUp Tools |  |
| Train | Bezier | Insertable prop | Dual axis detector | [Three axis detector] |  |
| Source field |  |  |  |  |  |
| Relative to field |  |  |  |  |  |
| Flat plane ▾ (X, Y, Z) |  |  |  |  |  |
| Space ▾ (Local, World) |  |  |  |  |  |
| Offset |  |  |  |  |  |
| Parameters: Top, BottomLeft, BottomRight |  |  |  |  |  |
| Build three axis detector |  |  |  |  |  |

---

| [General Tools] | Menu Tools | Animator Tools | Debug Tools | FTEmulator |  |
| :---: | :---: | :---: | :---: | :---: | :---: |
| Blendshape Sync | Import Presets | Constraint Tools | [AutoSync] | PopUp Tools |  |
| ☐ Sync |  |  |  |  |  |
| Git Configuration block |  |  |  |  |  |

---

| [General Tools] | Menu Tools | Animator Tools | Debug Tools | FTEmulator |  |
| :---: | :---: | :---: | :---: | :---: | :---: |
| Blendshape Sync | Import Presets | Constraint Tools | AutoSync | [PopUp Tools] |  |
| Avatar Migration Assistant |  |  |  |  |  |
| IconCap |  |  |  |  |  |
| Material Cleaner |  |  |  |  |  |
| Prefab Cleaner (popup: Target Scene, Project or Prefab) |  |  |  |  |  |
| GUID Regenerator (popup: assets and folders, filled from the Project selection) |  |  |  |  |  |
| GUID Repair (popup: assets and folders, filled from the Project selection) |  |  |  |  |  |
| GUID Broken Reference Checker |  |  |  |  |  |

---

| General Tools | [Menu Tools] | Animator Tools | Debug Tools | FTEmulator |  |
| :---: | :---: | :---: | :---: | :---: | :---: |
| Parameter budget |  |  |  |  |  |
| Menu manager (opens the Menu Tools window) |  |  |  |  |  |

---

| General Tools | Menu Tools | [Animator Tools] | Debug Tools | FTEmulator |  |
| :---: | :---: | :---: | :---: | :---: | :---: |
| [Find and Replace] | Clean Animation Keys | Controller Cleaner | Extract Animations |  |  |
| Find and Replace UI here |  |  |  |  |  |

---

| General Tools | Menu Tools | [Animator Tools] | Debug Tools | FTEmulator |  |
| :---: | :---: | :---: | :---: | :---: | :---: |
| Find and Replace | [Clean Animation Keys] | Controller Cleaner | Extract Animations |  |  |
| [Select Clean Target(s) field] |  |  |  |  |  |
| Clean animation keys UI here |  |  |  |  |  |

---

| General Tools | Menu Tools | [Animator Tools] | Debug Tools | FTEmulator |  |
| :---: | :---: | :---: | :---: | :---: | :---: |
| Find and Replace | Clean Animation Keys | [Controller Cleaner] | Extract Animations |  |  |
| Controller field + Scan |  |  |  |  |  |
| Scan scene controllers | Scan all controllers in project |  |  |  |  |
| Clean all |  |  |  |  |  |
| Results: controller, unused count, Clean |  |  |  |  |  |

---

| General Tools | Menu Tools | [Animator Tools] | Debug Tools | FTEmulator |  |
| :---: | :---: | :---: | :---: | :---: | :---: |
| Find and Replace | Clean Animation Keys | Controller Cleaner | [Extract Animations] |  |  |
| Extract Animations UI here |  |  |  |  |  |

---

| General Tools | Menu Tools | Animator Tools | [Debug Tools] | FTEmulator |  |
| :---: | :---: | :---: | :---: | :---: | :---: |
| Refresh Scripts |  |  |  |  |  |
| Find Null references |  |  |  |  |  |
| Menu Mockup |  |  |  |  |  |
| Delete GUID Reference Cache |  |  |  |  |  |
| ☐ Stop Auto Game |  |  |  |  |  |
| ☐ Script Load Guard |  |  |  |  |  |
| ☐ Stop all tool scripts |  |  |  |  |  |

---

| General Tools | Menu Tools | Animator Tools | Debug Tools | [FTEmulator] |  |
| :---: | :---: | :---: | :---: | :---: | :---: |
| State, Gesture Manager, Avatar, Avatar backend |  |  |  |  |  |
| Input, Hardware limits, Behavior, View allowed channels |  |  |  |  |  |
| Start | Stop |  |  |  |  |
| ☐ Active listening |  |  |  |  |  |
| ☐ Freeze output |  |  |  |  |  |
| Open debug window (opens popup) | Reset overrides |  |  |  |  |
| OSC connection or Manual input |  |  |  |  |  |
| Record / Stop recording | Play recording / Stop playback |  |  |  |  |
