## File structure rules

1. Code reused by several tools goes in `Editor/Components/`.
2. Every file that draws UI goes in a `UI/` folder. A tool's logic stays in its own folder; a file holding both a window and logic is split, the window going to `UI/`.
3. Code that relies on a dependency goes in a folder for that dependency (its own assembly, e.g. `Editor/VRChat/`). That folder holds a `UI/` folder, one folder per tool (e.g. `MenuTool/`, `ConstraintTool/`), and a `Components/` folder only for shared code that needs that dependency's assembly.
4. A moved file keeps its `.meta`.
