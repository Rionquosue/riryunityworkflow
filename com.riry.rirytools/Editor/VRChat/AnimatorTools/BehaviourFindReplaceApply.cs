using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using static Riry.RiryTools.Editor.BehaviourFindReplace;

namespace Riry.RiryTools.Editor
{
    /// <summary>Find and Replace's Apply: writes the new strings into the found behaviours, as one undo step.</summary>
    internal static class BehaviourFindReplaceApply
    {
        // Play Audio paths take the repath object's path when one is set (not for a controller asset), Parameter Driver
        // names the mapped parameter when one is typed; every other found string becomes replace, when it is not empty.
        // One undo step. Returns what changed.
        internal static List<Change> Apply(List<Hit> hits, string replace, GameObject repath, string map, bool asset, bool log = true)
        {
            var made = new List<Change>();
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Find and Replace");
            foreach (IGrouping<StateMachineBehaviour, Hit> behaviour in hits.Where(h => h.Behaviour != null).GroupBy(h => h.Behaviour))
            {
                var serialized = new SerializedObject(behaviour.Key);
                foreach (Hit hit in behaviour)
                {
                    string after = NewValue(hit, replace, asset ? null : repath, map);
                    if (after == null) continue;
                    SerializedProperty property = serialized.FindProperty(hit.Property);
                    if (property == null || property.propertyType != SerializedPropertyType.String || property.stringValue == after) continue;
                    made.Add(new Change { Hit = hit, Before = property.stringValue, After = after });
                    property.stringValue = after;
                }
                serialized.ApplyModifiedProperties();
            }
            Undo.CollapseUndoOperations(group);
            foreach (AnimatorController controller in made.Select(c => c.Hit.Controller).Distinct()) EditorUtility.SetDirty(controller);
            if (log) RiryLog.Info(RiryLog.FindReplace, "Changed " + made.Count + " behaviour string(s) in " +
                made.Select(c => c.Hit.Controller).Distinct().Count() + " controller(s).");
            return made;
        }

        private static string NewValue(Hit hit, string replace, GameObject repath, string map)
        {
            if (hit.Kind == Kind.PlayAudio && repath != null)
            {
                if (hit.Root != null && (repath.transform == hit.Root || repath.transform.IsChildOf(hit.Root)))
                    return AnimationUtility.CalculateTransformPath(repath.transform, hit.Root);
            }
            else if (hit.Kind == Kind.Driver && !string.IsNullOrEmpty(map)) return map;
            return string.IsNullOrEmpty(replace) ? null : replace;
        }
    }
}
