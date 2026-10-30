# Tutorial: Import Presets

Make every new texture, model or Blender file in a folder import with the
settings you want, without touching files you already set up by hand.

## 1. Save your settings to the library

1. Select an asset whose import settings you like (for example a texture).
2. Right-click it and choose **RiryTools > Save Import Settings to Preset
   Library**.
3. Pick a **Configuration** (a group of presets, for example `Avatar`) or type a
   **New name**, name the preset, and click **Save**.

The library lives in `Documents/RiryTools/ImportPresets`, so every project on
the computer can use it. **Open Library** shows it.

## 2. Set a folder's presets

1. Right-click a folder (or empty space inside it) in the Project window and
   choose **RiryTools > Import Preset**.
2. Under **Default for unlisted file types**, choose a configuration.
3. Under **File types**, add rows with **+ Textures**, **+ Models**, **+ Blender**
   or **+ File type**, and choose what each row **Use**s.
4. Optionally turn on **Also apply to files already in the folder now**.
5. Click **Apply**. The folder gets an `ImportPreset.md` describing its rules.

From now on, every file imported into that folder (or its subfolders) for the
first time gets these settings. Reimports and settings you change by hand are
left alone.

## 3. Models with animations

On a model row, turn on **Extract animations to Animations/ first (models)**.
New models are imported as Generic, every clip is copied to an `Animations`
folder beside the model, and then the model's preset (for example Humanoid) is
applied. For models already in the project, right-click them and choose
**RiryTools > Extract Animations (Generic)**.

## 4. Blender files

1. Install the Better FBX add-on in Blender and enable it.
2. On the **+ Blender** row, keep **Export .blend with Better FBX** on.
3. In **Better FBX (saved for this computer)**, check the **Blender** path (or
   **Browse**) and any **Exporter options**.

Each `.blend` in the folder is exported to an FBX beside it, which then follows
the folder's FBX rules.

Reference: [design/import-presets.md](../design/import-presets.md)
