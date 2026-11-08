using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.ScriptableObjects;
using ControlType = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control.ControlType;
using Object = UnityEngine.Object;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// A radial menu preview the Menu Tool draws: implemented by the Gesture Manager bridge
    /// (Editor/VRChat/MenuTool/GestureManager), which only compiles when Gesture Manager is installed.
    /// </summary>
    internal interface IMenuToolRadial : IDisposable
    {
        /// <summary>Shows a menu tree; values of parameters it already had are kept.</summary>
        void Show(VRCExpressionsMenu root, VRCExpressionParameters parameters);

        /// <summary>Opens the menus from the root down to the last one.</summary>
        void Navigate(IList<VRCExpressionsMenu> path);

        /// <summary>The menus open now, from the root ("" at Gesture Manager's own main page).</summary>
        List<VRCExpressionsMenu> Path { get; }

        /// <summary>
        /// Draws it: rect in GUI coordinates of the current OnGUI (for the mouse), local the same spot in root's coordinates
        /// (where the element is placed).
        /// </summary>
        void Draw(Rect rect, Rect local, VisualElement root);

        /// <summary>Takes it off the window (another tab is shown).</summary>
        void Hide();

        float Value(string parameter);

        /// <summary>Every parameter write the preview makes: name and value.</summary>
        event Action<string, float> Written;
    }

    /// <summary>
    /// The in-memory menus a preview shows: one VRCExpressionsMenu per submenu of the planned layout, and the parameters,
    /// never saved (HideAndDontSave) and destroyed with Dispose. With VRCFury installed, menus over 8 controls are paged
    /// into "Next" submenus as VRCFury does. Signature describes everything the radial shows, so a rebuilt preview that
    /// shows the same can be dropped (Adopt) and the radial left as it is.
    /// </summary>
    internal sealed class MenuToolPreviewMenus : IDisposable
    {
        internal static Func<IMenuToolRadial> CreateRadial;

        internal VRCExpressionsMenu Root;
        internal VRCExpressionParameters Parameters;
        internal readonly Dictionary<VRCExpressionsMenu, MenuEntry> EntryOf = new Dictionary<VRCExpressionsMenu, MenuEntry>();
        internal readonly Dictionary<MenuEntry, VRCExpressionsMenu> MenuOf = new Dictionary<MenuEntry, VRCExpressionsMenu>();
        private readonly List<Object> made = new List<Object>();
        private readonly StringBuilder signature = new StringBuilder();

        /// <summary>Every menu, control and parameter the preview holds, in build order.</summary>
        internal string Signature
        {
            get { return signature.ToString(); }
        }

        internal static MenuToolPreviewMenus Build(MenuEntry root, MenuScan scan)
        {
            var menus = new MenuToolPreviewMenus();
            menus.Root = menus.Menu(root, scan.VrcfuryInstalled);
            menus.Parameters = menus.Make<VRCExpressionParameters>("Menu Tool preview parameters");
            menus.Parameters.parameters = scan.Params.Values.Select(p => new VRCExpressionParameters.Parameter
            {
                name = p.Name,
                valueType = p.Type,
                defaultValue = p.Default,
                saved = p.Saved,
                networkSynced = p.Synced
            }).ToArray();
            foreach (VRCExpressionParameters.Parameter p in menus.Parameters.parameters)
                menus.signature.Append("P|").Append(p.name).Append('|').Append(p.valueType).Append('|').Append(p.defaultValue.ToString("R"))
                    .Append('|').Append(p.saved).Append('|').Append(p.networkSynced).Append('\n');
            return menus;
        }

        /// <summary>
        /// When fresh (built for a new planned layout) shows the same as this preview, this one takes fresh's entries and is
        /// kept, so the radial keeps its menus; fresh is then no longer needed. False when they differ.
        /// </summary>
        internal bool Adopt(MenuToolPreviewMenus fresh)
        {
            if (fresh == null || fresh.Signature != Signature || fresh.made.Count != made.Count) return false;
            // Built the same way, so each object stands at the same place in both lists.
            var mine = new Dictionary<Object, Object>();
            for (int i = 0; i < made.Count; i++) mine[fresh.made[i]] = made[i];
            EntryOf.Clear();
            MenuOf.Clear();
            foreach (KeyValuePair<VRCExpressionsMenu, MenuEntry> pair in fresh.EntryOf) EntryOf[(VRCExpressionsMenu)mine[pair.Key]] = pair.Value;
            foreach (KeyValuePair<MenuEntry, VRCExpressionsMenu> pair in fresh.MenuOf) MenuOf[pair.Key] = (VRCExpressionsMenu)mine[pair.Value];
            return true;
        }

        private T Make<T>(string name) where T : ScriptableObject
        {
            var made = ScriptableObject.CreateInstance<T>();
            made.name = name;
            made.hideFlags = HideFlags.HideAndDontSave;
            this.made.Add(made);
            signature.Append("M|").Append(typeof(T).Name).Append('|').Append(name).Append('\n');
            return made;
        }

        private VRCExpressionsMenu Menu(MenuEntry entry, bool paginate)
        {
            VRCExpressionsMenu menu = Make<VRCExpressionsMenu>(string.IsNullOrEmpty(entry.Name) ? "Root" : entry.Name);
            menu.controls = new List<VRCExpressionsMenu.Control>();
            EntryOf[menu] = entry;
            MenuOf[entry] = menu;
            VRCExpressionsMenu page = menu;
            int pageNumber = 2;
            for (int i = 0; i < entry.Children.Count; i++)
            {
                if (paginate && page.controls.Count == MenuToolScanner.MaxControls - 1 && entry.Children.Count - i > 1)
                {
                    VRCExpressionsMenu next = Make<VRCExpressionsMenu>(menu.name + " (Page " + pageNumber++ + ")");
                    next.controls = new List<VRCExpressionsMenu.Control>();
                    EntryOf[next] = entry;
                    page.controls.Add(new VRCExpressionsMenu.Control { name = "Next", type = ControlType.SubMenu, subMenu = next, parameter = new VRCExpressionsMenu.Control.Parameter { name = "" } });
                    page = next;
                }
                page.controls.Add(Control(entry.Children[i], paginate));
            }
            return menu;
        }

        private VRCExpressionsMenu.Control Control(MenuEntry entry, bool paginate)
        {
            signature.Append("C|").Append(entry.Name).Append('|').Append(entry.Icon != null ? entry.Icon.GetInstanceID() : 0).Append('|')
                .Append(entry.Type).Append('|').Append(entry.Parameter).Append('|').Append(entry.Value.ToString("R")).Append('|')
                .Append(string.Join(",", entry.SubParameters)).Append('|')
                .Append(string.Join(",", entry.Labels.Select(l => l.name + ":" + (l.icon != null ? l.icon.GetInstanceID() : 0)))).Append('\n');
            return new VRCExpressionsMenu.Control
            {
                name = entry.Name,
                icon = entry.Icon,
                type = entry.Type,
                parameter = new VRCExpressionsMenu.Control.Parameter { name = entry.Parameter ?? "" },
                value = entry.Value,
                subParameters = entry.SubParameters.Select(name => new VRCExpressionsMenu.Control.Parameter { name = name }).ToArray(),
                labels = entry.Labels.ToArray(),
                subMenu = entry.IsSubMenu ? Menu(entry, paginate) : null
            };
        }

        public void Dispose()
        {
            foreach (Object item in made)
            {
                if (item != null) Object.DestroyImmediate(item);
            }
            made.Clear();
            signature.Clear();
            EntryOf.Clear();
            MenuOf.Clear();
        }
    }
}

// ============================================================================
// GLOSSARY: Menu Tool preview
// ============================================================================
//
// IMenuToolRadial            The radial preview: Show(root, parameters), Navigate(path), Path, Draw(rect, local, root), Hide, Value,
//                            Written (each parameter write). Implemented by GestureManagerRadial (GestureManager/ folder).
// MenuToolPreviewMenus (this file)
//   CreateRadial             Set by the Gesture Manager bridge when Gesture Manager is installed; null otherwise.
//   Build(root, scan)        In-memory VRCExpressionsMenu per submenu of the planned layout (EntryOf / MenuOf map both ways) and
//                            a VRCExpressionParameters of every parameter, all HideAndDontSave; paged into "Next" submenus with
//                            VRCFury installed, as VRCFury does. Dispose destroys them.
//   Signature                Each menu (type, name), control (name, icon, type, parameter, value, sub-parameters, labels) and
//                            parameter (name, type, default, saved, synced) in build order.
//   Adopt(fresh)             Same Signature: takes fresh's EntryOf / MenuOf onto its own menus (same build order) and returns
//                            true, so the caller keeps it and disposes fresh; false when they differ.
