using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Controller Cleaner's clean: removes a scan's unused sub-assets from the controller file, then the transition slots
    /// left empty, and saves it. Everything is recorded for Undo, so the caller's undo group is undone with one Ctrl+Z; the
    /// restored controller is written to its file on the next save.
    /// </summary>
    internal static class ControllerCleanerClean
    {
        /// <summary>Returns "name (Type)" for each sub-asset removed.</summary>
        internal static List<string> Clean(ControllerCleanerScan.Result result)
        {
            var removed = new List<string>();
            // The used objects whose transition lists change below, recorded before anything is destroyed.
            Undo.RegisterCompleteObjectUndo(UsedObjects(result).ToArray(), "Controller Cleaner");
            foreach (Object item in result.Unused)
            {
                if (item == null) continue;
                removed.Add(item.name + " (" + item.GetType().Name + ")");
                Undo.DestroyObjectImmediate(item);
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

        // The controller and every state machine and state its layers still reach.
        private static List<Object> UsedObjects(ControllerCleanerScan.Result result)
        {
            var used = new List<Object> { result.Controller };
            foreach (AnimatorControllerLayer layer in result.Controller.layers) AddMachine(layer.stateMachine, used, result.Unused);
            return used;
        }

        private static void AddMachine(AnimatorStateMachine machine, List<Object> used, ICollection<Object> unused)
        {
            if (machine == null || unused.Contains(machine) || used.Contains(machine)) return;
            used.Add(machine);
            foreach (ChildAnimatorState child in machine.states)
            {
                if (child.state != null && !unused.Contains(child.state)) used.Add(child.state);
            }
            foreach (ChildAnimatorStateMachine child in machine.stateMachines) AddMachine(child.stateMachine, used, unused);
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
