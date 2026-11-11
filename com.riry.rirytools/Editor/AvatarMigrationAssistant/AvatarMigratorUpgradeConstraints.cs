using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using Object = UnityEngine.Object;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// "Upgrade to VRCConstraints on target?" (answered Yes in the up-front check): the
    /// source avatar is copied whole into a hidden, inactive temporary object, the VRChat
    /// SDK's converter (AvatarDynamicsSetup.DoConvertUnityConstraints, no dialog) turns that
    /// copy's Unity constraints into VRC constraints, those are copied to the target, and the
    /// copy is deleted at the end of the run. The source itself is never changed. The copy's
    /// objects stand for the source objects they copy (alias), so matching, references,
    /// rotation and scale work as for any other component.
    /// </summary>
    internal sealed partial class AvatarMigratorCopyRun
    {
        private const string ConverterClass = "AvatarDynamicsSetup";
        private const string ConverterMethod = "DoConvertUnityConstraints";

        private GameObject constraintHolder;
        private bool constraintsConverted;
        private bool conversionFailed;

        private void CopyUpgradedConstraints(AvatarCopyType type, AvatarTypeSettings typeSettings)
        {
            if (!ConvertOnCopy())
            {
                foreach (Type component in type.Types) CopyComponents(type, typeSettings, component);
                return;
            }
            if (typeSettings.Replace)
            {
                foreach (Type component in type.Types) ClearTarget(component);
            }
            Type vrcType = RiryToolsTypes.Named(AvatarMigratorCopyTypes.VrcConstraintPrefix + type.Types[0].Name);
            if (vrcType == null) return;
            foreach (Component converted in constraintHolder.GetComponentsInChildren(vrcType, true))
            {
                // Only the constraints the converter made; VRC constraints the source already had are copied by their own step.
                if (alias.ContainsKey(converted)) continue;
                var source = Original(converted.transform) as Transform;
                if (source == null || !InScope(source) || !WasConverted(source, type)) continue;
                Transform target = TargetFor(source, typeSettings.CreateMissing);
                if (target == null)
                {
                    report.NotCopied.Add(DescribeConverted(converted) + ": its object has no match on the target (Create missing objects is off).");
                    continue;
                }
                CopyComponent(converted, target.gameObject);
            }
        }

        private static string DescribeConverted(Component component)
        {
            return UnityEditor.ObjectNames.NicifyVariableName(component.GetType().Name) + " on " + component.name;
        }

        // Whether the source object had a Unity constraint of this type (converted on the copy).
        private static bool WasConverted(Transform source, AvatarCopyType type)
        {
            foreach (Type unityType in type.Types)
            {
                if (source.GetComponent(unityType) != null) return true;
            }
            return false;
        }

        /// <summary>Makes the temporary copy and converts its Unity constraints, once per run; false when it cannot.</summary>
        private bool ConvertOnCopy()
        {
            if (constraintsConverted) return !conversionFailed;
            constraintsConverted = true;
            MethodInfo convert = Converter();
            if (convert == null)
            {
                conversionFailed = true;
                report.PossibleIssues.Add("The VRChat SDK's constraint converter (" + ConverterClass + "." + ConverterMethod + " with 3 parameters) " +
                    "was not found, so Unity constraints were copied without the upgrade.");
                return false;
            }

            constraintHolder = new GameObject("RiryTools Avatar Migration constraint copy") { hideFlags = HideFlags.HideAndDontSave };
            constraintHolder.SetActive(false);
            GameObject copy = Object.Instantiate(sourceRoot.gameObject, constraintHolder.transform);
            copy.hideFlags = HideFlags.HideAndDontSave;
            // Recorded in the run's Undo group, with its removal (RemoveConstraintCopy), so whatever the
            // converter records on the copy undoes and redoes with it: one Ctrl+Z undoes the whole run.
            Undo.RegisterCreatedObjectUndo(constraintHolder, AvatarMigrationAssistant.Title);
            Transform[] from = sourceRoot.GetComponentsInChildren<Transform>(true);
            Transform[] to = copy.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < from.Length && i < to.Length; i++)
            {
                alias[to[i]] = from[i];
                alias[to[i].gameObject] = from[i].gameObject;
                Component[] sourceComponents = from[i].GetComponents<Component>();
                Component[] copyComponents = to[i].GetComponents<Component>();
                for (int j = 0; j < sourceComponents.Length && j < copyComponents.Length; j++)
                {
                    if (copyComponents[j] != null && sourceComponents[j] != null) alias[copyComponents[j]] = sourceComponents[j];
                }
            }

            var constraints = new List<IConstraint>();
            foreach (Component component in copy.GetComponentsInChildren<Component>(true))
            {
                var constraint = component as IConstraint;
                var source = Original(component.transform) as Transform;
                if (constraint != null && source != null && InScope(source)) constraints.Add(constraint);
            }
            try
            {
                object done = convert.Invoke(null, new object[] { constraints.ToArray(), null, false });
                if (done is bool && !(bool)done) throw new InvalidOperationException("the converter reported that it could not convert them.");
            }
            catch (Exception exception)
            {
                conversionFailed = true;
                Exception cause = exception is TargetInvocationException && exception.InnerException != null ? exception.InnerException : exception;
                report.PossibleIssues.Add("Unity constraints could not be upgraded to VRC constraints (" + cause.Message + "); they were copied as Unity constraints.");
                RiryLog.Exception(RiryLog.AvatarMigration, "The constraint upgrade (" + ConverterClass + "." + ConverterMethod + ") failed", cause,
                    sourceRoot.gameObject);
                return false;
            }
            return true;
        }

        private static MethodInfo Converter()
        {
            Type setup = AvatarMigratorCopyTypes.FindByName(ConverterClass);
            if (setup == null) return null;
            foreach (MethodInfo method in setup.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (method.Name == ConverterMethod && method.GetParameters().Length == 3) return method;
            }
            return null;
        }

        private void RemoveConstraintCopy()
        {
            if (constraintHolder != null) Undo.DestroyObjectImmediate(constraintHolder);
            constraintHolder = null;
        }
    }
}

// ============================================================================
// GLOSSARY: Avatar Migration Assistant constraint upgrade (AvatarMigratorUpgradeConstraints.cs, part of Editor/AvatarMigrationAssistant.cs)
// ============================================================================
//
// AvatarMigratorCopyRun (this part)
//   CopyUpgradedConstraints(type, settings)  With the upgrade answered Yes: copies the VRC constraints the converter made
//                          on the temporary copy in place of this Unity type; without the converter, the Unity ones as they are.
//   ConvertOnCopy()        Once per run: the source copied whole under a hidden, inactive holder; alias maps the copy's
//                          objects and components to the source's; AvatarDynamicsSetup.DoConvertUnityConstraints(constraints,
//                          null, false) converts the in-scope ones. Failures go to the report's possible issues.
//   RemoveConstraintCopy() Deletes the temporary copy at the end of the run, through Undo; its creation is recorded
//                          too, so the run stays one Undo step that the converter's own records undo within.
