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
                    (state.Vrcfury ? HierarchyPath.Of(m.Avatar.transform) == state.Path : MenuToolEntryState.IdOf(m.Avatar) == state.Avatar));
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
                Vrc3Param param = module.GetParam(write.Name) ?? Renamed(write.Name);
                if (param != null) param.Set(module, write.Value);
                else RiryLog.Warn(RiryLog.MenuTool, "Parameter not found: " + write.Name, module.Avatar);
                return;
            }
            if (state.Menu.Length == 0 || Radials == null || !GestureManagerMenuPath.Available)
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
                foreach (RadialMenu radial in radials) GestureManagerMenuPath.Open(radial, path);
            }
            Stop();
        }

        // A parameter VRCFury renamed in its build ("VF12_Name"): the closest match (FuzzyMatch) among the built FX layer's
        // parameters, for the saved name without the preview's estimated "VF_" prefix; null when nothing matches.
        private static Vrc3Param Renamed(string saved)
        {
            var descriptor = module.Avatar.GetComponent<VRCAvatarDescriptor>();
            if (descriptor == null) return null;
            var fx = descriptor.baseAnimationLayers.FirstOrDefault(l => l.type == VRCAvatarDescriptor.AnimLayerType.FX).animatorController
                as UnityEditor.Animations.AnimatorController;
            if (fx == null) return null;
            string name = saved.StartsWith("VF_", System.StringComparison.Ordinal) ? saved.Substring(3) : saved;
            string best = fx.parameters.Select(p => p.name).Where(p => FuzzyMatch.Score(p, name) > 0)
                .OrderByDescending(p => FuzzyMatch.Score(p, name)).ThenBy(p => p.Length).FirstOrDefault();
            return best != null ? module.GetParam(best) : null;
        }

        // The menus from the root down the submenus named by the path, as far as they are found.
        private static List<VRCExpressionsMenu> MenuChain(VRCExpressionsMenu root, string path)
        {
            if (root == null) return null;
            var chain = new List<VRCExpressionsMenu> { root };
            VRCExpressionsMenu at = root;
            foreach (string name in MenuToolVrcfuryMenu.Split(path))
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
