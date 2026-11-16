using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Controller Cleaner's scan: the sub-assets of a controller (state machines, states, transitions, behaviours, blend
    /// trees) that nothing in its layers reaches any more. They build up as the controller is edited and only bloat it.
    /// A synced layer's own motions and behaviours count as used.
    /// </summary>
    internal static class ControllerCleanerScan
    {
        internal sealed class Result
        {
            internal AnimatorController Controller;
            internal readonly List<Object> Unused = new List<Object>();
        }

        internal static Result Scan(AnimatorController controller)
        {
            var result = new Result { Controller = controller };
            var used = new HashSet<Object>();
            AnimatorControllerLayer[] layers = controller.layers;
            foreach (AnimatorControllerLayer layer in layers)
            {
                if (layer.stateMachine != null) Machine(layer.stateMachine, true, used);
                // A synced layer reuses another layer's states with its own motions and behaviours.
                if (layer.syncedLayerIndex < 0 || layer.syncedLayerIndex >= layers.Length || layers[layer.syncedLayerIndex].stateMachine == null) continue;
                foreach (AnimatorState state in States(layers[layer.syncedLayerIndex].stateMachine))
                {
                    Tree(layer.GetOverrideMotion(state) as BlendTree, used);
                    foreach (StateMachineBehaviour behaviour in layer.GetOverrideBehaviours(state)) used.Add(behaviour);
                }
            }
            foreach (Object item in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(controller)))
            {
                if (item != null && Scanned(item) && !used.Contains(item)) result.Unused.Add(item);
            }
            return result;
        }

        private static bool Scanned(Object item)
        {
            return item is AnimatorStateMachine || item is AnimatorState || item is AnimatorTransitionBase || item is StateMachineBehaviour || item is BlendTree;
        }

        private static void Machine(AnimatorStateMachine machine, bool root, HashSet<Object> used)
        {
            if (machine == null || !used.Add(machine)) return;
            foreach (StateMachineBehaviour behaviour in machine.behaviours) used.Add(behaviour);
            foreach (ChildAnimatorState child in machine.states)
            {
                AnimatorState state = child.state;
                if (state == null) continue;
                used.Add(state);
                Tree(state.motion as BlendTree, used);
                Transitions(state.transitions, used);
                foreach (StateMachineBehaviour behaviour in state.behaviours) used.Add(behaviour);
            }
            Transitions(machine.entryTransitions, used);
            // Unity only uses Any State transitions on a layer's own state machine.
            if (root) Transitions(machine.anyStateTransitions, used);
            foreach (ChildAnimatorStateMachine child in machine.stateMachines)
            {
                if (child.stateMachine == null) continue;
                Transitions(machine.GetStateMachineTransitions(child.stateMachine), used);
                Machine(child.stateMachine, false, used);
            }
        }

        // A transition counts while it still leads somewhere.
        private static void Transitions(IEnumerable<AnimatorTransitionBase> transitions, HashSet<Object> used)
        {
            foreach (AnimatorTransitionBase transition in transitions)
            {
                if (transition != null && (transition.destinationState != null || transition.destinationStateMachine != null || transition.isExit))
                    used.Add(transition);
            }
        }

        private static void Tree(BlendTree tree, HashSet<Object> used)
        {
            if (tree == null || !used.Add(tree)) return;
            foreach (ChildMotion child in tree.children) Tree(child.motion as BlendTree, used);
        }

        internal static IEnumerable<AnimatorState> States(AnimatorStateMachine machine)
        {
            foreach (ChildAnimatorState child in machine.states)
            {
                if (child.state != null) yield return child.state;
            }
            foreach (ChildAnimatorStateMachine child in machine.stateMachines)
            {
                if (child.stateMachine == null) continue;
                foreach (AnimatorState state in States(child.stateMachine)) yield return state;
            }
        }
    }
}
