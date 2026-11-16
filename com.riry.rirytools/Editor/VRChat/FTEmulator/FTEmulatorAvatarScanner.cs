using System;
using System.Collections.Generic;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace Riry.RiryTools.Editor
{
    /// <summary>What was found on one avatar. Made by one scan and not changed after.</summary>
    internal sealed class FTEmulatorAvatarSnapshot
    {
        internal string Name = "";
        /// <summary>Changes when the avatar's parameters or native settings do: the id the OSCQuery service gives.</summary>
        internal string Id = "";
        /// <summary>The avatar's Expression Parameters, synced or not: the parameters VRChat's OSC input can reach.</summary>
        internal readonly List<FTEmulatorAvatarParameter> ExpressionParameters = new List<FTEmulatorAvatarParameter>();
        /// <summary>Every parameter of the playable layers' controllers and of the output's live registry.</summary>
        internal readonly Dictionary<string, FTEmulatorValueType> ControllerParameters = new Dictionary<string, FTEmulatorValueType>(StringComparer.Ordinal);
        /// <summary>Parameters whose controllers disagree on the type and that Expression Parameters does not declare.</summary>
        internal readonly HashSet<string> TypeConflicts = new HashSet<string>(StringComparer.Ordinal);
        /// <summary>False with an SDK older than 3.10.6.</summary>
        internal bool NativeSupported;
        internal FTEmulatorNativeConfig Native;
        internal SkinnedMeshRenderer[] NativeMeshes = new SkinnedMeshRenderer[0];
        /// <summary>The full type names of the avatar's components that a template's signature names.</summary>
        internal readonly HashSet<string> Components = new HashSet<string>(StringComparer.Ordinal);
        /// <summary>Installed packages that a template's signature names, with their versions.</summary>
        internal readonly Dictionary<string, string> Packages = new Dictionary<string, string>(StringComparer.Ordinal);
    }

    /// <summary>
    /// Collects what is on the avatar Gesture Manager is running: its Expression Parameters, the parameters of its
    /// playable layers' controllers, its native face tracking settings, and the template components and packages the
    /// catalog's signatures name. It reads the avatar as it is in Play Mode, after VRCFury, Modular Avatar or a template's
    /// own build step have run, so what they generate is seen as it will be uploaded; nothing is built or changed here.
    /// Runs on the main thread, when a session starts or the avatar is rescanned, never per frame.
    /// </summary>
    internal static class FTEmulatorAvatarScanner
    {
        internal static FTEmulatorAvatarSnapshot Scan(GameObject avatar, FTEmulatorCatalog catalog, IReadOnlyList<FTEmulatorAvatarParameter> live)
        {
            var snapshot = new FTEmulatorAvatarSnapshot { NativeSupported = FTEmulatorNativeDescriptor.Supported };
            if (avatar == null) return snapshot;
            snapshot.Name = avatar.name;
            var descriptor = avatar.GetComponent<VRCAvatarDescriptor>();
            if (descriptor != null)
            {
                VRCExpressionParameters declared = descriptor.expressionParameters;
                if (declared != null && declared.parameters != null)
                {
                    foreach (VRCExpressionParameters.Parameter parameter in declared.parameters)
                    {
                        if (parameter == null || string.IsNullOrEmpty(parameter.name)) continue;
                        snapshot.ExpressionParameters.Add(new FTEmulatorAvatarParameter
                        {
                            Name = parameter.name,
                            Type = parameter.valueType == VRCExpressionParameters.ValueType.Bool ? FTEmulatorValueType.Bool
                                : parameter.valueType == VRCExpressionParameters.ValueType.Int ? FTEmulatorValueType.Int : FTEmulatorValueType.Float,
                            Synced = parameter.networkSynced,
                        });
                    }
                }
                var disagree = new HashSet<string>(StringComparer.Ordinal);
                Controllers(descriptor.baseAnimationLayers, snapshot, disagree);
                Controllers(descriptor.specialAnimationLayers, snapshot, disagree);
                foreach (string name in disagree)
                {
                    if (!snapshot.ExpressionParameters.Exists(parameter => parameter.Name == name)) snapshot.TypeConflicts.Add(name);
                }
                snapshot.Native = FTEmulatorNativeDescriptor.TryRead(descriptor, out snapshot.NativeMeshes);
            }
            if (live != null)
            {
                foreach (FTEmulatorAvatarParameter parameter in live)
                {
                    if (!string.IsNullOrEmpty(parameter.Name) && !snapshot.ControllerParameters.ContainsKey(parameter.Name))
                        snapshot.ControllerParameters[parameter.Name] = parameter.Type;
                }
            }

            // Template evidence: only what a signature names is looked for.
            var componentNames = new HashSet<string>(StringComparer.Ordinal);
            var packageNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (FTEmulatorAvatarProfile profile in catalog.Avatars)
            {
                foreach (string type in profile.SignatureComponents) componentNames.Add(type);
                foreach (FTEmulatorProfilePackage package in profile.SignaturePackages) packageNames.Add(package.Name);
            }
            if (componentNames.Count > 0)
            {
                foreach (MonoBehaviour component in avatar.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    // A missing script is null.
                    if (component == null) continue;
                    string type = component.GetType().FullName;
                    if (type != null && componentNames.Contains(type)) snapshot.Components.Add(type);
                }
            }
            if (packageNames.Count > 0)
            {
                foreach (UnityEditor.PackageManager.PackageInfo package in UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages())
                {
                    if (packageNames.Contains(package.name)) snapshot.Packages[package.name] = package.version;
                }
            }
            snapshot.Id = "local:RiryFTEmulator:" + Fingerprint(snapshot).ToString("x16");
            return snapshot;
        }

        private static void Controllers(VRCAvatarDescriptor.CustomAnimLayer[] layers, FTEmulatorAvatarSnapshot snapshot, HashSet<string> disagree)
        {
            if (layers == null) return;
            foreach (VRCAvatarDescriptor.CustomAnimLayer layer in layers)
            {
                if (layer.isDefault || layer.animatorController == null) continue;
                RuntimeAnimatorController runtime = layer.animatorController;
                // An override controller plays its base controller's parameters.
                for (int depth = 0; depth < 8 && runtime is AnimatorOverrideController overrides; depth++) runtime = overrides.runtimeAnimatorController;
                if (!(runtime is AnimatorController controller)) continue;
                foreach (AnimatorControllerParameter parameter in controller.parameters)
                {
                    if (string.IsNullOrEmpty(parameter.name)) continue;
                    FTEmulatorValueType type = parameter.type == AnimatorControllerParameterType.Float ? FTEmulatorValueType.Float
                        : parameter.type == AnimatorControllerParameterType.Int ? FTEmulatorValueType.Int : FTEmulatorValueType.Bool;
                    if (snapshot.ControllerParameters.TryGetValue(parameter.name, out FTEmulatorValueType seen))
                    {
                        if (seen != type) disagree.Add(parameter.name);
                    }
                    else snapshot.ControllerParameters[parameter.name] = type;
                }
            }
        }

        // FNV-1a over what the tracking app is told about the avatar, so the id changes exactly when that does.
        internal static ulong Fingerprint(FTEmulatorAvatarSnapshot snapshot)
        {
            ulong hash = 14695981039346656037UL;
            void Mix(string text)
            {
                foreach (char c in text ?? "")
                {
                    hash ^= c;
                    hash *= 1099511628211UL;
                }
                hash ^= 0xFF;
                hash *= 1099511628211UL;
            }
            Mix(snapshot.Name);
            var names = new List<string>();
            foreach (FTEmulatorAvatarParameter parameter in snapshot.ExpressionParameters) names.Add(parameter.Name + ":" + parameter.Type);
            names.Sort(StringComparer.Ordinal);
            foreach (string name in names) Mix(name);
            if (snapshot.Native != null)
            {
                Mix(snapshot.Native.Enabled ? "native" : "no native");
                Mix(snapshot.Native.UseAllTrackingKeys ? "all keys" : "basic keys");
                Mix(snapshot.Native.Sets.Count + " sets " + snapshot.Native.Behaviors.Count + " behaviors");
            }
            return hash;
        }
    }
}

// ============================================================================
// GLOSSARY: FTEmulator avatar scanner
// ============================================================================
//
// FTEmulatorAvatarSnapshot   Name, Id ("local:RiryFTEmulator:" + fingerprint), ExpressionParameters, ControllerParameters,
//                            TypeConflicts, NativeSupported, Native, NativeMeshes, Components, Packages.
// FTEmulatorAvatarScanner (this file)
//   Scan(avatar, catalog, live)  Reads the avatar descriptor's Expression Parameters, the parameters of its non-default
//                            playable layers' controllers (through override controllers), the output's live parameters,
//                            the native face tracking settings (FTEmulatorNativeDescriptor.TryRead), the avatar's
//                            components whose type a template signature names, and the installed packages one names.
//                            A parameter is a type conflict when two controllers give it different types and Expression
//                            Parameters does not declare it.
//   Fingerprint(snapshot)    FNV-1a of the name, the Expression Parameters (name and type) and the native summary.
