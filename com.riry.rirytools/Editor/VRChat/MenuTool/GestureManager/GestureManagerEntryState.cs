using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BlackStartX.GestureManager.Editor.Modules.Vrc3;
using BlackStartX.GestureManager.Editor.Modules.Vrc3.Params;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using GmManager = BlackStartX.GestureManager.GestureManager;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Replays the Menu manager's entry state (MenuToolEntryState) in Play Mode: waits until Gesture Manager has set up the
    /// saved avatar (it is in GestureManager.ControlledAvatars), then makes the saved writes in order, one per frame so the
    /// avatar's animators see each, then opens the saved submenu in Gesture Manager's radial once that radial exists.
    /// Once per entry into Play Mode.
    /// </summary>
    [InitializeOnLoad]
    internal static class GestureManagerEntryState
    {
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly PropertyInfo Radials = typeof(ModuleVrc3).GetProperty("Radials", Members);
        private static readonly FieldInfo MenuPath = typeof(RadialMenu).GetField("_menuPath", Members);
        private static readonly MethodInfo OpenMenu = typeof(RadialMenu).GetMethod("OpenMenu", Members, null,
            new[] { typeof(VRCExpressionsMenu), typeof(Vrc3Param), typeof(float) }, null);

        private static MenuToolEntryState state;
        private static ModuleVrc3 module;
        private static int next;
        private static bool started;

        static GestureManagerEntryState()
        {
            EditorApplication.playModeStateChanged += change =>
            {
                if (change == PlayModeStateChange.EnteredPlayMode) Begin();
                else if (change == PlayModeStateChange.ExitingPlayMode) Stop();
            };
            if (EditorApplication.isPlaying) Begin();
        }

        private static void Begin()
        {
            Stop();
            state = MenuToolEntryState.Load();
            if (state == null) return;
            EditorApplication.update += Tick;
        }

        private static void Stop()
        {
            EditorApplication.update -= Tick;
            state = null;
            module = null;
            next = 0;
            started = false;
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying || state == null)
            {
                Stop();
                return;
            }
            if (module == null)
            {
                module = GmManager.ControlledAvatars.Values.OfType<ModuleVrc3>().FirstOrDefault(m => m.Avatar != null &&
                    m.Avatar.scene.name == state.Scene && MenuToolEntryState.PathOf(m.Avatar.transform) == state.Avatar);
                // Its writes start the frame after it is set up.
                return;
            }
            if (module.IsInvalid())
            {
                Stop();
                return;
            }
            if (next < state.Writes.Count)
            {
                MenuToolEntryState.Write write = state.Writes[next++];
                Vrc3Param param = module.GetParam(write.Name);
                if (param != null) param.Set(module, write.Value);
                return;
            }
            if (state.Menu.Length == 0 || Radials == null || MenuPath == null || OpenMenu == null)
            {
                Stop();
                return;
            }
            // The radial exists once Gesture Manager's inspector has shown it.
            var radials = (Radials.GetValue(module) as IEnumerable<RadialMenu>)?.ToList();
            if (radials == null || radials.Count == 0) return;
            var descriptor = module.Avatar.GetComponent<VRCAvatarDescriptor>();
            List<VRCExpressionsMenu> path = descriptor != null ? MenuChain(descriptor.expressionsMenu, state.Menu) : null;
            if (path != null)
            {
                foreach (RadialMenu radial in radials)
                {
                    var pages = (IList)MenuPath.GetValue(radial);
                    pages.Clear();
                    foreach (VRCExpressionsMenu menu in path) OpenMenu.Invoke(radial, new object[] { menu, null, 0f });
                }
            }
            Stop();
        }

        // The menus from the root down the submenus named by the path, as far as they are found.
        private static List<VRCExpressionsMenu> MenuChain(VRCExpressionsMenu root, string path)
        {
            if (root == null) return null;
            var chain = new List<VRCExpressionsMenu> { root };
            VRCExpressionsMenu at = root;
            foreach (string name in path.Split('/'))
            {
                VRCExpressionsMenu.Control control = at.controls.FirstOrDefault(c =>
                    c.type == VRCExpressionsMenu.Control.ControlType.SubMenu && c.name == name && c.subMenu != null);
                if (control == null) break;
                at = control.subMenu;
                chain.Add(at);
            }
            return chain;
        }
    }
}
