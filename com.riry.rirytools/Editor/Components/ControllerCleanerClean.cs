using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Controller Cleaner's clean: removes a scan's unused sub-assets from the controller file, then the transition slots
    /// left empty, and saves it. Removing sub-assets can't be undone with Ctrl+Z, so the caller warns first and logs them.
    /// </summary>
    internal static class ControllerCleanerClean
    {
        /// <summary>Returns "name (Type)" for each sub-asset removed.</summary>
        internal static List<string> Clean(ControllerCleanerScan.Result result)
        {
            var removed = new List<string>();
            foreach (Object item in result.Unused)
            {
                if (item == null) continue;
                removed.Add(item.name + " (" + item.GetType().Name + ")");
                AssetDatabase.RemoveObjectFromAsset(item);
                Object.DestroyImmediate(item, true);
            }
            foreach (AnimatorControllerLayer layer in result.Controller.layers)
            {
                if (layer.stateMachine != null) DropEmptyTransitions(layer.stateMachine);
            }
            EditorUtility.SetDirty(result.Controller);
            AssetDatabase.SaveAssetIfDirty(result.Controller);
            result.Unused.Clear();
            return removed;
        }

        private static void DropEmptyTransitions(AnimatorStateMachine machine)
        {
            machine.entryTransitions = machine.entryTransitions.Where(t => t != null).ToArray();
            machine.anyStateTransitions = machine.anyStateTransitions.Where(t => t != null).ToArray();
            foreach (ChildAnimatorState child in machine.states)
            {
                if (child.state == null) continue;
                child.state.transitions = child.state.transitions.Where(t => t != null).ToArray();
                EditorUtility.SetDirty(child.state);
            }
            foreach (ChildAnimatorStateMachine child in machine.stateMachines)
            {
                if (child.stateMachine == null) continue;
                machine.SetStateMachineTransitions(child.stateMachine, machine.GetStateMachineTransitions(child.stateMachine).Where(t => t != null).ToArray());
                DropEmptyTransitions(child.stateMachine);
            }
            EditorUtility.SetDirty(machine);
        }
    }
}
