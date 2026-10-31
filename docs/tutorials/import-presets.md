# Tutorial: Import Presets

Make every new texture, model or Blender file in a folder import with the
settings you want, without touching files you already set up by hand.

## 1. Save your settings to the library

1. Select an asset whose import settings you like (for example a texture).
2. Right-click it and choose **RiryTools > Save Import Preset**.
3. Pick a **Configuration** (a group of presets, for example `Avatar`) or type a
   **New name**, name the preset, and click **Save**.

The library lives in `Documents/RiryTools/ImportPresets`, so every project on
the computer can use it. **Open Library** shows it.

## 2. Set a folder's presets

1. Open the folder in the Project window and choose
   **Tools > RiryTools > Import Presets > Set Folder Import Presets**, or type
   part of another folder's name in **Folder** and pick it.
2. Each file type in the folder (**Image files**, **Model files**, ...) has a
   dropdown: choose a preset for it, or **None**. A tag from the **Tags** list
   (PBR map names such as `normal` or `metallicsmoothness`, editable there) or a
   name ending such as `_custom` is listed as its own type (**normal files**,
   **_custom files**) for every file whose name contains it; left on **None**,
   those files get their plain type's preset.
3. If the folder holds `.blend` files, turn on **Export .blend with Better FBX**
   if you want them exported.
4. Click **Apply**. The folder gets an `ImportPreset.md`, and the files of each
   type whose preset changed are set to it now.

For several folders at once, select them (or right-click empty space inside one),
right-click, choose **RiryTools > Set Folder Import Presets**, pick a configuration
(turn off **Apply to all** for one per folder; open **File types** for a preset per
type), and click **Apply**.

From now on, every file imported directly into that folder (not its subfolders)
for the first time gets these settings. Reimports and settings you change by hand
are left alone.

## 3. Models with animations

Right-click a model and choose **RiryTools > Extract Animation**, check
**Destination**, and click **Extract**: every clip is copied, as Generic clips, to
an `Animations` folder beside the model.
Turn on **Automatically clean keys** to clean the clips (**Configure** sets what
is kept), and **Automatically extract animations** to extract that model before
its preset every time. These settings are saved in `.<model>_ani-extract.json~`
beside the model.

## 4. Blender files

1. Install the Better FBX add-on in Blender and enable it.
2. On the **+ Blender** row, keep **Export .blend with Better FBX** on.
3. In **Better FBX (saved for this computer)**, check the **Blender** path (or
   **Browse**) and any **Exporter options**.

Each `.blend` in the folder is exported to an FBX beside it, which then follows
the folder's FBX rules.

Reference: [design/import-presets.md](../design/import-presets.md)
