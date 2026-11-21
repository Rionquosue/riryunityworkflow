## File structure rules

The full rules, tool names and script locations are in `filestructure.md`. In short:

1. Code reused by several tools, or by several subtools of one tool, goes in its assembly's `Components/` folder (`Editor/Components/`, `Editor/VRChat/Components/`).
2. Keep UI and feature logic in separate scripts and types. Follow the UI placement and naming rules in `filestructure.md`.
3. Code that relies on a dependency goes in a folder for that dependency (its own assembly, e.g. `Editor/VRChat/`). That folder holds a `UI/` folder, one folder per tool (e.g. `MenuTool/`, `ConstraintTools/`), and a `Components/` folder only for shared code that needs that dependency's assembly. A tool that needs the dependency for any part of it lives entirely in that folder.
4. Always keep the .meta file alongside to allow for smooth updating experiences when using the VPM manifest.
5. Where possible, all of a tool's scripts except for reused component scripts will be stored in a single folder. Tools that are a single script with a single job share a category folder instead (`Debug/`, `Utilities/`, `AnimatorTools/`), as do the tools the owner places there (Menu Mockup, Attach To).
6. One job per script. A tool that indexes the avatar hierarchy for searching and then applies changes based on that search has one script for the index and search, and another for applying the changes.

## Factoring and name rules

1. All names must be consistent. If a tool is named "FlipTool" then all classes and scripts related should be along the lines of "FlipToolMenu.cs" or "FlipToolChecks.cs"
2. All files must be human readable and refactorable. Keep names descriptive, comment where clarity is needed. An example is commenting "This segment calls /Editor/Components/FuzzyMatch.cs to embed a fuzzy search window."
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
