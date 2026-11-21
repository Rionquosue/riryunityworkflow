# Menu Tools

## 1. Where to find it

| Access | Location | Item |
| --- | --- | --- |
| Menu bar | RiryTools > Menu Tools | |
| Right Click (hierarchy) | RiryTools > Menu Tools | the selected avatar as target |
| Main Menu | Menu Tools | Parameter budget, Menu manager |

## 2. Brief description

Shows an avatar's assembled expression menu and parameters, traced to their sources, and plans changes to the menu.

## 3. What the tool can be used for

- Seeing the menu the avatar will have once VRCFury has built it, and where each control comes from.
- Checking the synced parameter budget, with and without VRCFury's compression.
- Moving, renaming, deleting and adding menu items, and assigning icons, from one place.

## 4. How the tool behaves

- Traces expression menu controls and parameters to their sources: the avatar descriptor and VRCFury (Modular Avatar is not read for now). VRCFury's menu rules (Move, Reorder, Set Icon, SPS, Security Lock, Override Menu Settings) are applied as VRCFury applies them.
- **Parameter budget** compares synced parameter cost with and without VRCFury's Parameter Compressor, estimated; for exact numbers, build a VRCFury test copy.
- **Menu manager** previews the assembled radial menu in Edit Mode with Gesture Manager's radial, and a list of every entry.
- Planned changes: moves (drag, or **Move to…**), Destructive Move, new menus and controls, **Assign Menu Icon**, **Rename Menu Entry** (radial right-click or triple-click in the list), **Delete Menu Entry** (radial right-click or Delete key) and **Move to… > <NULL>** (a VRCFury Move Menu Item to a blank path). Nothing changes until Apply; the plan is saved per avatar.
- **Apply** makes each change through the system owning the control, as one undo step: menu assets are edited in place, VRCFury items get VRCFury rules, and VRCFury paths inside a moved or renamed submenu follow it.
- The radial's parameter writes and submenu can be replayed when Gesture Manager starts Play Mode.
- Rich text tags in control names (such as `<b>` or `<color=#DDDDDD>`) are left out wherever Menu Tools shows a name (the menu list, Move to…, the path bar, the plan and its messages, the same-name box and the Parameter budget); renaming (triple-click, or Rename Menu Entry) shows and edits the name with its tags.
- **Menu manager** warns, under the radial, in boxes collapsed until opened: controls sharing a name in one submenu, each linking to the menu asset or prefab it comes from (click to select it in the Project window), and icons over 256 px, which the VRChat SDK won't upload. **Apply menu_icon** sets those icons' max size to 256 with the `menu_icon` import preset (made in the Import Presets folder the first time). It changes import settings, so it asks first; Ctrl+Z can't undo it. Assign Menu Icon also warns when the picked icon is over 256 px.
- **Rescan** rebuilds the scan, preview and radial.

## 5. Flow chart

```mermaid
flowchart TD
    A[Pick avatar] --> B[Scan: descriptor menu and parameters, VRCFury features and rules]
    B --> C[Assembled menu and parameter budget]
    C --> D[Plan changes: move, rename, delete, NULL, icon, add]
    D --> E[Preview planned layout in the radial]
    E --> F{Apply}
    F --> G[Per change: menu asset edit or VRCFury rule]
    G --> H[Repath VRCFury paths in moved or renamed submenus]
    H --> I[Rescan and verify, one undo step]
```

## 6. References

- The owner's Menu Tools specification (`Menu-Tool.md`).
- VRCFury (reference only): the Parameter Compressor (`Editor-Avatars/Service/Compressor/ParameterCompressorService.cs`), followed for the compressed budget; its menu path and Full Controller rewrite rules; Move / Reorder Menu Item and Set Icon behaviour.
- Gesture Manager 3.9.9: its radial menu, called by reflection, for the preview.
- Modular Avatar: menu installers and items, read by type name (currently off).
