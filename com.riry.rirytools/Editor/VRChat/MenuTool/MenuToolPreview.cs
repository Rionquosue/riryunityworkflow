using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
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

        /// <summary>Draws it in rect (GUI coordinates of the current OnGUI) of the window actually shown.</summary>
        void Draw(Rect rect, EditorWindow surface);

        /// <summary>Takes it off the window (another tab is shown).</summary>
        void Hide();

        float Value(string parameter);

        /// <summary>Every parameter write the preview makes: name and value.</summary>
        event Action<string, float> Written;
    }

    /// <summary>
    /// The in-memory menus a preview shows: one VRCExpressionsMenu per submenu of the planned layout, and the parameters,
    /// never saved (HideAndDontSave) and destroyed with Dispose. With VRCFury installed, menus over 8 controls are paged
    /// into "Next" submenus as VRCFury does.
    /// </summary>
    internal sealed class MenuToolPreviewMenus : IDisposable
    {
        internal static Func<IMenuToolRadial> CreateRadial;

        internal VRCExpressionsMenu Root;
        internal VRCExpressionParameters Parameters;
        internal readonly Dictionary<VRCExpressionsMenu, MenuEntry> EntryOf = new Dictionary<VRCExpressionsMenu, MenuEntry>();
        internal readonly Dictionary<MenuEntry, VRCExpressionsMenu> MenuOf = new Dictionary<MenuEntry, VRCExpressionsMenu>();
        private readonly List<Object> made = new List<Object>();

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
            return menus;
        }

        private T Make<T>(string name) where T : ScriptableObject
        {
            var made = ScriptableObject.CreateInstance<T>();
            made.name = name;
            made.hideFlags = HideFlags.HideAndDontSave;
            this.made.Add(made);
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
            EntryOf.Clear();
            MenuOf.Clear();
        }
    }
}

// ============================================================================
// GLOSSARY: Menu Tool preview
// ============================================================================
//
// IMenuToolRadial            The radial preview: Show(root, parameters), Navigate(path), Path, Draw(rect, surface), Hide, Value,
//                            Written (each parameter write). Implemented by GestureManagerRadial (GestureManager/ folder).
// MenuToolPreviewMenus (this file)
//   CreateRadial             Set by the Gesture Manager bridge when Gesture Manager is installed; null otherwise.
//   Build(root, scan)        In-memory VRCExpressionsMenu per submenu of the planned layout (EntryOf / MenuOf map both ways) and
//                            a VRCExpressionParameters of every parameter, all HideAndDontSave; paged into "Next" submenus with
//                            VRCFury installed, as VRCFury does. Dispose destroys them.
