# GUID Tools

## 1. Where to find it

| Access | Location | Item |
| --- | --- | --- |
| Menu bar | RiryTools > GUID Tools | |
| Main Menu | General Tools > GUID Tools | Regenerate GUID, Repair Missing GUID References, Check for Broken References, Delete GUID Reference Cache |
| Right Click (project) | RiryTools > Regenerate GUID | |
| Right Click (project) | RiryTools > Repair Missing GUID References | |
| Right Click (project) | RiryTools > Scrub Unused Overrides | |
| Prompt | When an opened scene or prefab points at missing assets | |

## 2. Brief description

Regenerates asset GUIDs without breaking references, and repairs references to assets that no longer exist.

## 3. What the tool can be used for

- Giving copied assets their own GUIDs (for example after copying a package folder) so they stop being treated as the original.
- Fixing references broken by deleted, replaced or re-imported assets.

## 4. How the tool behaves

- **Regenerate GUID** gives selected assets (and folders' contents) new GUIDs and rewrites every reference to them; folder GUIDs never change. Replaced GUIDs go into a permanent ledger in ProjectSettings, read with its Git history.
- **Check for Broken References** scans the open scenes and the files they use. The scan stops at animator controllers and shaders, which are only checked for being present; the Project window's **Repair Missing GUID References** also opens them.
- Not listed: empty slots, prefab bookkeeping, references in a controller's unused sub-assets, material texture slots the shader doesn't have, references outside objects that hold a missing reference when loaded, and references from a file to its own missing parts.
- Each missing GUID offers candidates (redirects, name matches, Git restores) or a manual pick. Each place it sits shows the object and field holding it; clicking selects the object.
- A reference can be ignored (kept out of the list and the save check). **Scrub unused overrides** removes prefab overrides whose target or property no longer exists. **Call material cleaner** opens Material Cleaner. The Project window's **RiryTools > Scrub Unused Overrides** does the same scrub on the selected scenes and prefabs, and those in selected folders.
- After Apply, Controller Cleaner is offered on controllers the repair touched.
- When an opened scene or prefab points at missing assets, it offers Check for Broken References; saving such a file is guarded.

## 5. Flow chart

```mermaid
flowchart TD
    A[Check for Broken References] --> B[Walk files and dependencies]
    B --> C{Controller or shader?}
    C -- Yes --> D[Only check it exists, unless opened from the Project window]
    C -- No --> E[Read GUID references]
    E --> F[Drop empty slots, bookkeeping, unused and inactive references]
    F --> G[Missing GUIDs and missing objects, with locations]
    G --> H[Candidates: ledger, name matches, Git restores, manual pick]
    H --> I[Apply: rewrite references]
    I --> J{Controllers touched with unused sub-assets?}
    J -- Yes --> K[Offer Controller Cleaner]
```

## 6. References

- Unity's text serialization (YAML) and `.meta` files, Git history for restores, Unity's `PrefabUtility` property modifications.
