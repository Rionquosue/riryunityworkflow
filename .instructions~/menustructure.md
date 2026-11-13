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
    * Constraint Tool >
        * Train
        * Bezier
        * Insertable prop
    * GUID Tools
    * Menu Tool

**Project folder**

    * Animator Tools
    * Clean Animation Keys
    * GUID Tools >
        * Regenerate GUID(s)
    * Import Presets
        * Set Folder Preset
        * Save Import Preset (only when clicking file, else grayed)
        * Extract Animation (only when clicking mesh like FBX or library file containing multiple animations, else grayed)

### Toolbar menu

    Formatted as breadcrumbs where > expands into a submenu.

**Tools > RiryTools**

    * Main Menu
    * AutoSync >
        * Configure
        * [ ] Sync now
    * [ ] Stop Auto Game
    * Constraint Tool
    * Animator Tools >
        * Clean Animation Keys
        * Find and Replace
    * Avatar Tools >
        * Avatar Migration Assistant
        * Blendshape Sync
        * Menu Tool
    * Utilities >
        * GUID Rebuild tool
        * GUID Repair tool
        * IconCap
    * Import Presets >
        * Set Folder Presets
        * Save Import Preset
        * Extract Animation
        * Import Preset Library
    * Debug Tools >
        * Refresh Scripts
        * Menu Mockup
        * [ ] Stop all tool scripts
    * Documentation

### Main menu

Structured in such a way that it can be presented as a table. Laid out as cells. Cells left blank are to be considered blank space in the tool tab. If components of a tool are not present they can be assumed to be filled in, as this table is primarily to structure the tabs and what is laid out within them instead of the structure of each tool. Where there is no [ ] as a selected menu item, this tab is assumed to be an unordered list of buttons that will call the popups for those selected tools.

#### Main tab

Where [] contains the currently active tab.

| [Utilities] | Animator Tools | Avatar Tools | Constraint Tool | Import Presets | Debug Tools |
| :---: | :---: | :---: | :---: | :---: | :---: |
| [General tools] | AutoSync |  |  |  |  |
| ☐ Stop Auto Game |  |  |  |  |  |
| GUID Rebuild tool |  |  |  |  |  |
| GUID repair tool |  |  |  |  |  |
| Check for Broken References |  |  |  |  |  |
| Delete GUID Reference Cache |  |  |  |  |  |
| IconCap |  |  |  |  |  |

---

| [Utilities] | Animator Tools | Avatar Tools | Constraint Tool | Import Presets | Debug Tools |
| :---: | :---: | :---: | :---: | :---: | :---: |
| General tools | [AutoSync] |  |  |  |  |
| ☐ Sync |  |  |  |  |  |
| Git Configuration block |  |  |  |  |  |

---

| Utilities | [Animator Tools] | Avatar Tools | Constraint Tool | Import Presets | Debug Tools |
| :---: | :---: | :---: | :---: | :---: | :---: |
| [Find and Replace] | Clean Animation Keys |  |  |  |  |
| Find and Replace UI here |  |  |  |  |  |

---

| Utilities | [Animator Tools] | Avatar Tools | Constraint Tool | Import Presets | Debug Tools |
| :---: | :---: | :---: | :---: | :---: | :---: |
| Find and Replace | [Clean Animation Keys] |  |  |  |  |
| [Select Clean Target(s) field] |  |  |  |  |  |
| Clean animation keys UI here |  |  |  |  |  |

---

| Utilities | Animator Tools | [Avatar Tools] | Constraint Tool | Import Presets | Debug Tools |
| :---: | :---: | :---: | :---: | :---: | :---: |
| Avatar Migration Assistant |  |  |  |  |  |
| Menu Tool |  |  |  |  |  |
| Blendshape Sync |  |  |  |  |  |

---

| Utilities | Animator Tools | [Avatar Tools] | Constraint Tool | Import Presets | Debug Tools |
| :---: | :---: | :---: | :---: | :---: | :---: |
| [Constraint Tool] |  |  |  |  |  |
| [Train] | Bezier | Insertable prop |  |  |  |
| Train UI here |  |  |  |  |  |

---

| Utilities | Animator Tools | [Avatar Tools] | Constraint Tool | Import Presets | Debug Tools |
| :---: | :---: | :---: | :---: | :---: | :---: |
| [Constraint Tool] |  |  |  |  |  |
| Train | [Bezier] | Insertable prop |  |  |  |
| Bezier UI here |  |  |  |  |  |

---

| Utilities | Animator Tools | [Avatar Tools] | Constraint Tool | Import Presets | Debug Tools |
| :---: | :---: | :---: | :---: | :---: | :---: |
| [Constraint Tool] |  |  |  |  |  |
| Train | Bezier | [Insertable prop] |  |  |  |
| Insertable prop UI here |  |  |  |  |  |

---

| Utilities | Animator Tools | [Avatar Tools] | Constraint Tool | Import Presets | Debug Tools |
| :---: | :---: | :---: | :---: | :---: | :---: |
| [Blendshape Sync] |  |  |  |  |  |
| Running sync job list |  |  |  |  |  |
| Add / edit job (opens popup) |  |  |  |  |  |

---

| Utilities | Animator Tools | Avatar Tools | Constraint Tool | [Import Presets] | Debug Tools |
| :---: | :---: | :---: | :---: | :---: | :---: |
| [Set Folder Import Presets] | Extract Animation | Import Preset Library |  |  |  |
| Set Folder Import Presets UI here |  |  |  |  |  |

---

| Utilities | Animator Tools | Avatar Tools | Constraint Tool | [Import Presets] | Debug Tools |
| :---: | :---: | :---: | :---: | :---: | :---: |
| Set Folder Import Presets | [Extract Animation] | Import Preset Library |  |  |  |
| Extract Animation UI here |  |  |  |  |  |

---

| Utilities | Animator Tools | Avatar Tools | Constraint Tool | [Import Presets] | Debug Tools |
| :---: | :---: | :---: | :---: | :---: | :---: |
| Set Folder Import Presets | Extract Animation | [Import Preset Library] |  |  |  |
| Display Import Preset Library here |  |  |  |  |  |
| Click to open in explorer | File input box here | Save import preset (creates from selected in input box) |  |  |  |

---

| Utilities | Animator Tools | Avatar Tools | Constraint Tool | Import Presets | [Debug Tools] |
| :---: | :---: | :---: | :---: | :---: | :---: |
| Refresh Scripts |  |  |  |  |  |
| Find Null references |  |  |  |  |  |
| Menu Mockup |  |  |  |  |  |
| ☐ Stop all tool scripts |  |  |  |  |  |
