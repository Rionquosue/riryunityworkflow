# Import Presets

## 1. Where to find it

| Access | Location | Item |
| --- | --- | --- |
| Menu bar | RiryTools > Import Presets > Set Folder Import Presets / Save Import Preset / Open Import Preset Library | |
| Main Menu | Import Presets | Set Folder Import Presets, Save Import Preset, Open Import Preset Library |
| Right Click (project) | RiryTools > Set Folder Import Presets | |
| Right Click (project) | RiryTools > Save Import Preset | |

## 2. Brief description

Assigns import presets to folders by file type and applies them to newly imported files.

## 3. What the tool can be used for

- Making every new texture, model or Blender file in a folder import with the settings you want, without touching files you already set up by hand.

## 4. How the tool behaves

- **Save Import Preset** saves a preset from an asset's importer into a shared library in `Documents/RiryTools/ImportPresets`, grouped into configurations.
- **Set Folder Import Presets** picks a preset per file type in a folder. File types are told apart by extension, and, as the **Filters** choose, by tags in the file name (PBR map names by default, editable) and `_word` name endings.
- A folder's presets apply only to files imported for the first time in that folder; reimports never change settings.
- Better FBX can export Blender files through Blender.

## 5. Flow chart

```mermaid
flowchart TD
    A[File imported for the first time] --> B{Folder has presets?}
    B -- No --> Z[Unity defaults]
    B -- Yes --> C[File type: extension, plus tag or _word ending per Filters]
    C --> D{Preset for that type?}
    D -- Yes --> E[Apply the preset to the importer]
    D -- No --> Z
```

## 6. References

- Unity's Preset system; Substance Painter's PBR map names for the default tags; Blender for Better FBX.
