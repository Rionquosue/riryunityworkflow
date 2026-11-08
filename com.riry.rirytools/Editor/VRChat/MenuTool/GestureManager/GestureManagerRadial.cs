using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BlackStartX.GestureManager.Editor.Modules.Vrc3;
using BlackStartX.GestureManager.Editor.Modules.Vrc3.Params;
using BlackStartX.GestureManager.Modules;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using Object = UnityEngine.Object;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// The Menu Tool's radial preview, drawn by Gesture Manager's own radial menu (RadialMenu) in Edit Mode. Gesture
    /// Manager normally runs it on a ModuleVrc3 set up for Play Mode (InitForAvatar rewrites the avatar's Animator); here
    /// the module is never set up: it belongs to a hidden, inactive stand-in avatar that only carries the preview's
    /// in-memory menu and parameters, and its parameters are filled directly. No animation plays and the real avatar is
    /// never touched. Parameter writes are reported (Written) and kept in memory. Gesture Manager's internals used here
    /// (RadialMenu._menuPath, OpenMenu, GetOrCreateRadial(editor, official), RadialPage._menu) are found by reflection;
    /// when a Gesture Manager version renames them, the preview says so instead of drawing. Built against Gesture Manager 3.9.9.
    /// </summary>
    [InitializeOnLoad]
    internal sealed class GestureManagerRadial : IMenuToolRadial
    {
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly FieldInfo MenuPath = typeof(RadialMenu).GetField("_menuPath", Members);
        private static readonly FieldInfo PageMenu = typeof(RadialPage).GetField("_menu", Members);
        private static readonly MethodInfo OpenMenu = typeof(RadialMenu).GetMethod("OpenMenu", Members, null,
            new[] { typeof(VRCExpressionsMenu), typeof(Vrc3Param), typeof(float) }, null);
        private static readonly MethodInfo OfficialRadial = typeof(ModuleVrc3).GetMethod("GetOrCreateRadial", Members, null,
            new[] { typeof(ScriptableObject), typeof(bool) }, null);

        static GestureManagerRadial()
        {
            MenuToolPreviewMenus.CreateRadial = () => new GestureManagerRadial();
        }

        /// <summary>Why this Gesture Manager cannot be driven, or null when it can.</summary>
        internal static string Unsupported
        {
            get
            {
                if (MenuPath == null || PageMenu == null || OpenMenu == null)
                    return "This Gesture Manager version is not supported by the Menu Tool preview (built for 3.9.9).";
                return null;
            }
        }

        private GameObject avatar;
        private VRCAvatarDescriptor descriptor;
        private ModuleVrc3 module;
        private RadialMenu radial;
        // The key Gesture Manager files its radial under (any ScriptableObject).
        private ScriptableObject owner;

        public event Action<string, float> Written;

        public void Show(VRCExpressionsMenu root, VRCExpressionParameters parameters)
        {
            var values = new Dictionary<string, float>();
            if (module != null)
            {
                foreach (KeyValuePair<string, Vrc3Param> pair in module.Params) values[pair.Key] = pair.Value.FloatValue();
            }
            Hide();
            if (avatar == null)
            {
                avatar = EditorUtility.CreateGameObjectWithHideFlags("RiryTools Menu Tool preview", HideFlags.HideAndDontSave, typeof(Animator));
                avatar.SetActive(false);
                descriptor = avatar.AddComponent<VRCAvatarDescriptor>();
            }
            if (owner == null)
            {
                owner = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
                owner.hideFlags = HideFlags.HideAndDontSave;
            }
            // Set only when different, so an unchanged preview does not touch the stand-in.
            if (!descriptor.customExpressions) descriptor.customExpressions = true;
            if (descriptor.expressionsMenu != root) descriptor.expressionsMenu = root;
            if (descriptor.expressionParameters != parameters) descriptor.expressionParameters = parameters;

            module = new ModuleVrc3(descriptor) { Settings = new ModuleSettings() };
            module.Params.Clear();
            foreach (KeyValuePair<string, Vrc3DefaultParams.DefaultParameter> pair in Vrc3DefaultParams.Parameters)
                module.Params[pair.Key] = new Vrc3Param(pair.Value, pair.Value.SyncType);
            var own = new List<Vrc3Param>();
            foreach (VRCExpressionParameters.Parameter parameter in parameters.parameters)
            {
                if (string.IsNullOrEmpty(parameter.name) || module.Params.ContainsKey(parameter.name)) continue;
                var param = new Vrc3Param(parameter);
                module.Params[parameter.name] = param;
                own.Add(param);
            }
            // Names menus use without declaring them: kept as Floats so the controls still work.
            foreach (string name in Names(root, new HashSet<VRCExpressionsMenu>()))
            {
                if (module.Params.ContainsKey(name)) continue;
                var param = new Vrc3Param(name, AnimatorControllerParameterType.Float);
                module.Params[name] = param;
                own.Add(param);
            }
            foreach (Vrc3Param param in own)
            {
                VRCExpressionParameters.Parameter declared = parameters.parameters.FirstOrDefault(p => p.name == param.Name);
                param.Set(module, values.TryGetValue(param.Name, out float value) ? value : declared != null ? declared.defaultValue : 0f);
                param.SetOnChange((changed, written) => Written?.Invoke(changed.Name, written));
            }

            radial = OfficialRadial != null
                ? (RadialMenu)OfficialRadial.Invoke(module, new object[] { owner, true })
                : module.GetOrCreateRadial(owner);
            if (Unsupported == null) Navigate(new[] { root });
        }

        private static IEnumerable<string> Names(VRCExpressionsMenu menu, HashSet<VRCExpressionsMenu> seen)
        {
            if (menu == null || !seen.Add(menu)) yield break;
            foreach (VRCExpressionsMenu.Control control in menu.controls)
            {
                if (control.parameter != null && !string.IsNullOrEmpty(control.parameter.name)) yield return control.parameter.name;
                foreach (VRCExpressionsMenu.Control.Parameter sub in control.subParameters ?? new VRCExpressionsMenu.Control.Parameter[0])
                {
                    if (sub != null && !string.IsNullOrEmpty(sub.name)) yield return sub.name;
                }
                foreach (string name in Names(control.subMenu, seen)) yield return name;
            }
        }

        public void Navigate(IList<VRCExpressionsMenu> path)
        {
            if (radial == null || Unsupported != null || path == null || path.Count == 0) return;
            var pages = (IList)MenuPath.GetValue(radial);
            pages.Clear();
            foreach (VRCExpressionsMenu menu in path) OpenMenu.Invoke(radial, new object[] { menu, null, 0f });
        }

        public List<VRCExpressionsMenu> Path
        {
            get
            {
                var path = new List<VRCExpressionsMenu>();
                if (radial == null || Unsupported != null) return path;
                foreach (object page in (IList)MenuPath.GetValue(radial))
                {
                    var menu = PageMenu.GetValue(page) as VRCExpressionsMenu;
                    if (menu != null) path.Add(menu);
                }
                return path;
            }
        }

        public void Draw(Rect rect, Rect local, VisualElement root)
        {
            if (radial == null || root == null) return;
            if (Unsupported != null)
            {
                Hide();
                EditorGUI.HelpBox(new Rect(rect.x, rect.y, rect.width, 40f), Unsupported, MessageType.Warning);
                return;
            }
            // Mouse handling works in the OnGUI's coordinates (Rect); the element is placed in the window's root element.
            // The window takes it off when a repaint does not draw it (no timer: IMGUI does not repaint while idle).
            radial.Rect = rect;
            radial.Render(root, local);
        }

        public void Hide()
        {
            if (radial != null) radial.RemoveFromHierarchy();
        }

        public float Value(string parameter)
        {
            Vrc3Param param = module != null ? module.GetParam(parameter) : null;
            return param != null ? param.FloatValue() : 0f;
        }

        public void Dispose()
        {
            Hide();
            radial = null;
            module = null;
            if (avatar != null) Object.DestroyImmediate(avatar);
            if (owner != null) Object.DestroyImmediate(owner);
        }
    }
}

// ============================================================================
// GLOSSARY: Gesture Manager bridge (compiled only with Gesture Manager 3.9+, RIRYTOOLS_GESTURE_MANAGER)
// ============================================================================
//
// GestureManagerRadial (this file): IMenuToolRadial (Editor/VRChat/MenuTool/MenuToolPreview.cs)
//   static constructor       [InitializeOnLoad]: sets MenuToolPreviewMenus.CreateRadial.
//   Show(root, parameters)   A hidden, inactive stand-in avatar (Animator + VRCAvatarDescriptor, HideAndDontSave) carries the
//                            preview menu and parameters; a fresh ModuleVrc3 on it, never InitForAvatar'd; its Params filled
//                            with Gesture Manager's defaults, the preview parameters and names menus use undeclared (Floats),
//                            keeping earlier values; each own parameter reports writes (Written). Gesture Manager's radial
//                            (GetOrCreateRadial(owner, official: true) by reflection, else the public one), opened on the root.
//   Navigate / Path          RadialMenu._menuPath cleared and OpenMenu called per menu; Path reads each RadialPage._menu.
//   Draw(rect, local, root)  Rect (mouse maths) in GUI coordinates; Render places the element at local in root (the window's
//                            rootVisualElement). The window hides it (Hide) when a repaint does not draw it.
//   Unsupported              A Gesture Manager without these members: Draw shows that message instead of the radial.
//   Dispose                  Removes the element, destroys the stand-in avatar and the key object.
