# Coding Standards

Standards for C# scripts in RiryTools. All code is Editor-only.

## 1. Project Structure

### 1.1 Assemblies
- **Riry.RiryTools.Editor** (`com.riry.rirytools/Editor/`): core tools and utilities
- **Riry.RiryTools.Editor.VRChat**: VRChat-specific tools (requires `RIRYTOOLS_VRC_CONSTRAINTS`)
- **Riry.RiryTools.Editor.GestureManager**: Gesture Manager integration (requires `RIRYTOOLS_VRC_CONSTRAINTS` and `RIRYTOOLS_GESTURE_MANAGER`)
- **Riry.RiryTools.Editor.FTEmulator** (`com.riry.ftemulator/Editor/`): FTEmulator (requires `RIRYTOOLS_VRC_CONSTRAINTS`)
- **Riry.RiryTools.Editor.FTEmulator.GestureManager**: FTEmulator Gesture Manager integration (requires `RIRYTOOLS_VRC_CONSTRAINTS` and `RIRYTOOLS_GESTURE_MANAGER`)

### 1.2 File Placement
- **Shared components**: `Editor/Components/` or `Editor/VRChat/Components/`
- **Tool-specific code**: one folder per tool (e.g., `MenuTool/`, `ConstraintTools/`)
- **UI and logic separation**: UI scripts are separate from feature logic
- **Dependency folders**: code that relies on a dependency lives in that dependency's folder
- **Meta files**: every `.cs` file has a `.meta` file with a fresh uuid4 hex GUID

### 1.3 One Job Per Script
- Example: one script for indexing/searching, another for applying changes

## 2. Naming

- **Classes, enums, methods, properties, events**: PascalCase
- **Interfaces**: `I` prefix (e.g., `IFTEmulatorOutput`)
- **Constants**: PascalCase (e.g., `SchemaVersion`, `MaxCodes`)
- **Private fields**: camelCase, no prefix (e.g., `catalog`, `lastSnapshot`)
- **Internal/public fields**: PascalCase (e.g., `NativeSupported`)
- **Tool names**: consistent across a tool's classes (e.g., `FlipToolMenu.cs`, `FlipToolChecks.cs`)

## 3. Formatting

- 4 spaces per indent, no tabs
- Allman braces: opening brace on its own line

```csharp
internal class ClassName
{
    private const int MaxValue = 100;
    private int count;

    internal void Method()
    {
        if (count > MaxValue)
        {
            return;
        }
    }
}
```

### 3.1 Comments
- XML documentation (`///`) for public members
- Comment **why**, not **what**

## 4. Methods
- Use guard clauses (early returns) to reduce nesting
- Use default parameters for optional behavior instead of overloads

## 5. Error Handling
- Prefer returning meaningful values over throwing exceptions
- Log with the appropriate level (`Debug.Log`, `Debug.LogWarning`, `Debug.LogError`)
- Never swallow exceptions silently

## 6. Documentation
- **docs/variables.md**: every stored key, path, GUID, symbol, constant, menu path and workflow variable
- **docs/ui-languagespec.md**: where every UI element's text lives (file, method, kind of text, lines)
- **docs/datamodels.md**: data model standards
- **docs/filestructure.md**: UI placement and naming rules
- **docs/instructions/for_agents.md**: instructions for AI agents
- **docs/instructions/structure-rules.md**: file structure and organization rules

## 7. Version Control
- Branches: `main`, `beta`, `betastaging`
- Commit messages: `Tool or area: what changed` (e.g., `Docs: Tools.md lists where each tool is opened from`)

## 8. UI

### 8.1 Layout
- Split information up so the user isn't overwhelmed
- Put whitelists/blacklists in separate popups or tabs
- Fixed-size components are popup-only, opened from the Main menu or the menu bar

### 8.2 Tabs
- Tabs re-order, not auto-resize
- Primary tabs: light grey active, dark grey inactive
- Subtabs: medium grey active, dark grey inactive

### 8.3 User Agency
- The most impactful tools (visual filters, destructive tools) are presented up front
- Destructive tools always warn and prompt to save

### 8.4 Undo and Safety
- A single-click action undoes in a single Ctrl+Z step
- No overlapping UI elements
- No race conditions or interrupts that can destroy user data

## 9. Review Checklist
- [ ] Naming follows section 2
- [ ] No magic numbers (use named constants)
- [ ] Compiles without warnings
- [ ] UI elements don't overlap
- [ ] Destructive actions warn
- [ ] Actions undo in a single Ctrl+Z step
