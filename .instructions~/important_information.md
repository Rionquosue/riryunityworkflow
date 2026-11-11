## File structure rules

1. Code reused by several tools goes in `Editor/Components/`.
2. Every file that draws UI goes in a `UI/` folder. A tool's logic stays in its own folder; If a file holds both UI and logic in a single script, they should be divided into two, and sorted appropriately by the following rules.
3. Code that relies on a dependency goes in a folder for that dependency (its own assembly, e.g. `Editor/VRChat/`). That folder holds a `UI/` folder, one folder per tool (e.g. `MenuTool/`, `ConstraintTool/`), and a `Components/` folder only for shared code that needs that dependency's assembly.
4. Always keep the .meta file alongside to allow for smooth updating experiences when using the VPM manifest.
5. Where possible, all scripts except for reused component scripts will be stored in a single folder. 

## Factoring and name rules

1. All names must be consistent. If a tool is named "FlipTool" then all classes and scripts related should be along the lines of "FlipToolMenu.cs" or "FlipToolChecks.cs"
2. All files must be human readable and refactorable. Keep names descriptive, comment where clarity is needed. An example is commenting "This segment calls /Editor/Components/Fuzzymatch.cs to embed a fuzzy search window."
2. All names must be simple and descriptive. Things should be named something like "PreFlight" if they run before, "PostFlight" if after. 

## UI Design rules

1. Always divide things cleanly, do not overload the user with a lot of information all at once.
2. If possible, break whitelists and blacklists out into separate popups or opening tab-bars so that they do not overwhelm the UI.
3. Any component that requires a fixed width or height should be a popup-only tool that is called from Main menu or the Menu bar. This allows the user the flexibility to lay out their editor window as they desire.
4. Tabs should not automatically resize, but should re-order. We want tabs to cascade downwards in priority, sub tabs should always have a slightly different shade of colour to primary tabs. Primary tabs should always be selected with a "Active" colour that lightens it. Example is light grey vs dark grey for all unused tabs. Subtab active tab is highlighted with a medium grey while inactive tabs are dark grey. This provides separation at a glance.
5. User agency is of utmost priority. The most impactful tools such as visual filters or destructive tools should always be presented up front.
6. In the case of a destructive tool, always provide a warning. Prompt the user to save before running a destructive tool.

## Undo and consistency rules

1. All actions that can be applied in a single click should be undone in a single ctrl-z step.
2. Always check for overlapping UI elements.
3. Always check for any bugs, race conditions, or interrupts that may destroy user data unintentionally.
