using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace Riry.RiryTools.Editor
{
    /// <summary>Which authoring system a menu entry or parameter comes from.</summary>
    internal enum MenuOrigin
    {
        Native,
        VRCFury,
        ModularAvatar
    }

    /// <summary>
    /// One control of the assembled expression menu, or a submenu. The assembled menu is what the avatar would show once
    /// built: the descriptor's menu, plus what Modular Avatar and VRCFury add, after VRCFury's own move, reorder and icon
    /// rules. Built by MenuToolScanner; nothing in it is saved.
    /// </summary>
    internal sealed class MenuEntry
    {
        internal string Name = "";
        internal VRCExpressionsMenu.Control.ControlType Type = VRCExpressionsMenu.Control.ControlType.SubMenu;
        internal Texture2D Icon;
        internal string Parameter = "";
        internal float Value = 1f;
        internal string[] SubParameters = new string[0];
        internal VRCExpressionsMenu.Control.Label[] Labels = new VRCExpressionsMenu.Control.Label[0];
        internal List<MenuEntry> Children = new List<MenuEntry>();
        internal MenuEntry Parent;

        internal MenuOrigin Origin;
        // The component or asset that contributes it, for "select the source".
        internal Object Source;
        // The content block it belongs to: the base avatar, an installed prefab, or a feature's object.
        internal string Block = "";
        // VRCFury entries and their parameters are worked out from VRCFury's settings, not built: names may differ.
        internal bool Estimated;
        // Added by pagination (VRCFury's "Next" pages), not by any source.
        internal bool Generated;
        // A submenu the plan adds (Add menu), made on Apply by this edit (NewMenuAsset or NewMaItem, Ambiguous until
        // chosen); null for everything the sources already hold.
        internal MoveEdit? Added;

        // Where it lives, for edits. Native: the menu asset holding the control and its index there.
        internal VRCExpressionsMenu OwnerMenu;
        internal int OwnerIndex = -1;
        // A native submenu's own menu asset (also for submenus whose items come from an asset through MA or VRCFury).
        internal VRCExpressionsMenu SubMenuAsset;
        // Modular Avatar: the menu item component, and the installer that installs it (on it or an ancestor).
        internal Component MaItem;
        internal Component MaInstaller;
        // Modular Avatar: the object whose children are this submenu's items (MenuSource Children).
        internal GameObject MaChildrenSource;
        // VRCFury: the component, and the path the feature gives before any move rule.
        internal Component VrcfComponent;
        internal string VrcfPath;

        internal bool IsSubMenu
        {
            get { return Type == VRCExpressionsMenu.Control.ControlType.SubMenu; }
        }

        /// <summary>The path in the assembled menu, submenu names joined by "/" (the root is "").</summary>
        internal string Path
        {
            get
            {
                if (Parent == null) return "";
                string parent = Parent.Path;
                return parent.Length == 0 ? Name : parent + "/" + Name;
            }
        }

        internal IEnumerable<MenuEntry> Descendants()
        {
            foreach (MenuEntry child in Children)
            {
                yield return child;
                foreach (MenuEntry below in child.Descendants()) yield return below;
            }
        }

        /// <summary>The parameters it writes: its own and its puppet's sub-parameters, without empty names.</summary>
        internal IEnumerable<string> Parameters()
        {
            if (!string.IsNullOrEmpty(Parameter)) yield return Parameter;
            foreach (string sub in SubParameters)
            {
                if (!string.IsNullOrEmpty(sub)) yield return sub;
            }
        }

        internal void Add(MenuEntry child, int index = -1)
        {
            if (child.Parent != null) child.Parent.Children.Remove(child);
            child.Parent = this;
            if (index < 0 || index > Children.Count) Children.Add(child);
            else Children.Insert(index, child);
        }

        /// <summary>A copy of this entry and everything under it, with no parent.</summary>
        internal MenuEntry Clone()
        {
            var copy = (MenuEntry)MemberwiseClone();
            // MemberwiseClone shares the list: give the copy its own.
            copy.Children = new List<MenuEntry>();
            copy.Parent = null;
            foreach (MenuEntry child in Children) copy.Add(child.Clone());
            return copy;
        }

        /// <summary>The direct child submenu or control with this name, or null.</summary>
        internal MenuEntry Child(string name)
        {
            return Children.FirstOrDefault(child => child.Name == name);
        }

        /// <summary>The entry at a path under this one ("" is this one), or null.</summary>
        internal MenuEntry Find(string path)
        {
            if (string.IsNullOrEmpty(path)) return this;
            MenuEntry at = this;
            foreach (string part in path.Split('/'))
            {
                at = at.Child(part);
                if (at == null) return null;
            }
            return at;
        }
    }

    /// <summary>One expression parameter of the assembled avatar, counted once however many blocks declare it.</summary>
    internal sealed class ParamEntry
    {
        internal string Name = "";
        internal VRCExpressionParameters.ValueType Type;
        internal bool Synced = true;
        internal bool Saved;
        internal float Default;
        internal bool Estimated;
        // Declared in the expression parameters (or by MA / VRCFury as such); false for names only menus or animators use.
        internal bool Declared;
        // An Int's or Float's synced width in bits (the VRChat SDK's GetBitWidthByType: 2 to 8; 8 unless set), and whether
        // its network representation is flipped (SDK 3.10.6 and later).
        internal int Width = 8;
        internal bool Flipped;
        internal readonly List<string> Blocks = new List<string>();
        internal readonly HashSet<VRCExpressionParameters.ValueType> DeclaredTypes = new HashSet<VRCExpressionParameters.ValueType>();
        internal readonly List<MenuEntry> UsedBy = new List<MenuEntry>();
        internal readonly List<Object> Sources = new List<Object>();

        internal bool TypeConflict
        {
            get { return DeclaredTypes.Count > 1; }
        }

        /// <summary>Synced bits: one for a Bool, Width (8 unless set narrower) for an Int or Float, none when not synced.</summary>
        internal int Bits
        {
            get { return !Synced ? 0 : Cost; }
        }

        /// <summary>Its bits when synced (VRCFury's TypeCost).</summary>
        internal int Cost
        {
            get { return Type == VRCExpressionParameters.ValueType.Bool ? 1 : Width; }
        }

        /// <summary>A Bool, or an 8-bit, unflipped Int or Float: what VRCFury's compressor can carry (HasStandardNetworkEncoding).</summary>
        internal bool StandardEncoding
        {
            get { return Type == VRCExpressionParameters.ValueType.Bool || (Width == 8 && !Flipped); }
        }
    }

    /// <summary>The parameters one animator layer reads or drives.</summary>
    internal sealed class LayerUsage
    {
        internal string Name = "";
        internal readonly List<string> Parameters = new List<string>();
    }

    /// <summary>An animator controller the avatar uses (descriptor layer, VRCFury Full Controller, MA Merge Animator).</summary>
    internal sealed class ControllerUsage
    {
        internal string Name = "";
        internal Object Controller;
        internal Object Source;
        internal readonly List<LayerUsage> Layers = new List<LayerUsage>();
        // Parameters a VRChat Parameter Driver in it changes with Add: VRCFury's compressor leaves them out unless it is
        // also compressing two- and four-axis puppets.
        internal readonly HashSet<string> AddDriven = new HashSet<string>();
    }

    /// <summary>One VRCFury Set Icon, Move Menu Item or Reorder Menu Item rule, applied in VRCFury's order after the features.</summary>
    internal sealed class VrcfuryMenuRule
    {
        internal string Kind = "";
        internal Component Component;
        internal string Path = "";
        internal string To = "";
        internal int Position;
        internal Texture2D Icon;
    }

    /// <summary>Everything one scan found: the assembled menu, its parameters, controllers and notes.</summary>
    internal sealed class MenuScan
    {
        internal GameObject Target;
        internal VRCAvatarDescriptor Avatar;
        // No avatar descriptor above the target: its contributions only, and placement and cost depend on the host.
        internal bool Standalone;
        internal MenuEntry Root = new MenuEntry { Name = "" };
        internal readonly Dictionary<string, ParamEntry> Params = new Dictionary<string, ParamEntry>();
        internal readonly List<string> Blocks = new List<string>();
        internal readonly List<ControllerUsage> Controllers = new List<ControllerUsage>();
        // Unsupported generators, missing references and other things worth knowing, each once.
        internal readonly List<string> Notes = new List<string>();
        internal bool VrcfuryInstalled;
        internal bool ModularAvatarInstalled;

        internal void Note(string text)
        {
            if (!Notes.Contains(text)) Notes.Add(text);
        }

        internal ParamEntry Param(string name)
        {
            ParamEntry entry;
            return name != null && Params.TryGetValue(name, out entry) ? entry : null;
        }

        /// <summary>Whether an object is inside the target (the scan's filter); everything is, for an avatar root target.</summary>
        internal bool InFilter(Object source)
        {
            if (Target == null) return true;
            if (Avatar != null && Target == Avatar.gameObject) return true;
            var component = source as Component;
            var gameObject = component != null ? component.gameObject : source as GameObject;
            return gameObject != null && (gameObject.transform == Target.transform || gameObject.transform.IsChildOf(Target.transform));
        }
    }
}

// ============================================================================
// GLOSSARY: Menu Tool model
// ============================================================================
//
// MenuOrigin                 Native (the descriptor's menu and parameter assets), VRCFury or ModularAvatar.
// MenuEntry                  A control or submenu of the assembled menu: Name, Type, Icon, Parameter, Value, SubParameters,
//                            Labels, Children/Parent; Origin, Source (to select), Block (content block), Estimated (worked out
//                            from VRCFury settings), Generated (a VRCFury "Next" page), Added (a submenu the plan adds, with
//                            the edit that makes it on Apply; planned layout only). Ownership for edits: OwnerMenu/OwnerIndex
//                            (native control), SubMenuAsset, MaItem/MaInstaller/MaChildrenSource, VrcfComponent/VrcfPath.
//   Path / Find / Child      "A/B/C" path in the assembled menu; lookups by path or name.
//   Parameters()             Parameter and sub-parameters, non-empty.
//   Clone()                  Deep copy for the planned layout.
// ParamEntry                 One parameter, counted once: Type, Synced, Saved, Default, Estimated, Declared, Width and Flipped
//                            (SDK 3.10.6 bit widths), Blocks, DeclaredTypes (TypeConflict when more than one), UsedBy (menu
//                            entries), Sources, Cost (1 Bool, Width Int/Float), Bits (Cost when synced, else 0),
//                            StandardEncoding (what VRCFury's compressor can carry).
// ControllerUsage / LayerUsage  An animator controller and, per layer, the parameters it reads or drives; AddDriven, the
//                            parameters its Parameter Drivers change with Add.
// VrcfuryMenuRule            A VRCFury SetIcon / MoveMenuItem / ReorderMenuItem rule (Kind, Component, Path, To, Position,
//                            Icon), applied by MenuToolVrcfuryMenu in VRCFury's order.
// MenuScan                   The scan: Target, Avatar, Standalone, Root, Params, Blocks, Controllers, Notes, VrcfuryInstalled,
//                            ModularAvatarInstalled; InFilter(source).
