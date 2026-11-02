using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Avatar Migration Assistant feature 3: the component types that can be copied, in
    /// run order. Types are found by full name across the loaded assemblies, so VRChat,
    /// DynamicBone and FinalIK types are optional: a type that is not in the project is
    /// left out. Common marks the short list (Pumkin's Avatar Tools' CopierTabs common list).
    /// </summary>
    internal sealed class AvatarCopyType
    {
        internal readonly string Id;
        internal readonly string Label;
        internal readonly bool Common;
        internal readonly bool NeedsSdk;
        internal readonly string[] TypeNames;

        private Type[] resolved;

        private AvatarCopyType(string id, string label, bool common, bool needsSdk, params string[] typeNames)
        {
            Id = id;
            Label = label;
            Common = common;
            NeedsSdk = needsSdk;
            TypeNames = typeNames;
        }

        /// <summary>The types found in this project; empty when none is.</summary>
        internal Type[] Types
        {
            get
            {
                if (resolved != null) return resolved;
                var types = new List<Type>();
                foreach (string name in TypeNames)
                {
                    Type type = AvatarCopyTypes.Find(name);
                    if (type != null && typeof(Component).IsAssignableFrom(type)) types.Add(type);
                }
                resolved = types.ToArray();
                return resolved;
            }
        }

        internal bool Available
        {
            get { return Types.Length > 0 || Id == AvatarCopyTypes.ExtraTypes; }
        }

        internal bool Matches(Component component)
        {
            if (component == null) return false;
            foreach (Type type in Types)
            {
                if (type.IsInstanceOfType(component)) return true;
            }
            return false;
        }

        internal static AvatarCopyType Make(string id, string label, bool common, bool needsSdk, params string[] typeNames)
        {
            return new AvatarCopyType(id, label, common, needsSdk, typeNames);
        }
    }

    internal static class AvatarCopyTypes
    {
        // Ids used by the type rules.
        internal const string Prefabs = "prefab";
        internal const string ParticleSystems = "particlesystem";
        internal const string Colliders = "collider";
        internal const string Animators = "animator";
        internal const string AudioSources = "audiosource";
        internal const string Transforms = "transform";
        internal const string MeshRenderers = "meshrenderer";
        internal const string SkinnedMeshRenderers = "skinnedmeshrenderer";
        internal const string Cameras = "camera";
        internal const string AvatarDescriptor = "vrcavatardescriptor";
        internal const string ExtraTypes = "other";

        internal const string PhysBone = "VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBone";
        internal const string PhysBoneCollider = "VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBoneCollider";
        internal const string ContactSender = "VRC.SDK3.Dynamics.Contact.Components.VRCContactSender";
        internal const string ContactReceiver = "VRC.SDK3.Dynamics.Contact.Components.VRCContactReceiver";
        internal const string DescriptorType = "VRC.SDK3.Avatars.Components.VRCAvatarDescriptor";
        internal const string PipelineManager = "VRC.Core.PipelineManager";
        internal const string SpatialAudioSource = "VRC.SDK3.Avatars.Components.VRCSpatialAudioSource";
        internal const string VrcConstraintPrefix = "VRC.SDK3.Dynamics.Constraint.Components.VRC";

        private static readonly Dictionary<string, Type> Cache = new Dictionary<string, Type>(StringComparer.Ordinal);

        /// <summary>Every type, in the order a run copies them.</summary>
        internal static readonly AvatarCopyType[] All =
        {
            AvatarCopyType.Make(Prefabs, "Prefabs", true, false, "UnityEngine.Transform"),
            AvatarCopyType.Make(ParticleSystems, "ParticleSystems", true, false, "UnityEngine.ParticleSystem"),
            AvatarCopyType.Make(Colliders, "Colliders", false, false, "UnityEngine.Collider"),
            AvatarCopyType.Make("rigidbody", "Rigidbodies", false, false, "UnityEngine.Rigidbody"),
            AvatarCopyType.Make("trailrenderer", "TrailRenderers", true, false, "UnityEngine.TrailRenderer"),
            AvatarCopyType.Make("light", "Lights", true, false, "UnityEngine.Light"),
            AvatarCopyType.Make(Animators, "Animators", false, false, "UnityEngine.Animator"),
            AvatarCopyType.Make(AudioSources, "AudioSources", true, false, "UnityEngine.AudioSource"),
            AvatarCopyType.Make("contactreceiver", "Contact Receivers", true, true, ContactReceiver),
            AvatarCopyType.Make("contactsender", "Contact Senders", true, true, ContactSender),
            AvatarCopyType.Make("physbonecollider", "PhysBone colliders", true, true, PhysBoneCollider),
            AvatarCopyType.Make("physbone", "PhysBones", true, true, PhysBone),
            AvatarCopyType.Make("vrcstation", "VRCStation", true, true, "VRC.SDK3.Avatars.Components.VRCStation"),
            AvatarCopyType.Make("vrcheadchop", "VRCHeadChop", true, true, "VRC.SDK3.Avatars.Components.VRCHeadChop"),
            AvatarCopyType.Make("dynamicbonecollider", "DynamicBone colliders", true, false, "DynamicBoneCollider", "DynamicBonePlaneCollider"),
            AvatarCopyType.Make("dynamicbone", "DynamicBones", true, false, "DynamicBone"),
            AvatarCopyType.Make("aimconstraint", "Aim constraints", true, false, "UnityEngine.Animations.AimConstraint"),
            AvatarCopyType.Make("lookatconstraint", "LookAt constraints", true, false, "UnityEngine.Animations.LookAtConstraint"),
            AvatarCopyType.Make("parentconstraint", "Parent constraints", true, false, "UnityEngine.Animations.ParentConstraint"),
            AvatarCopyType.Make("positionconstraint", "Position constraints", true, false, "UnityEngine.Animations.PositionConstraint"),
            AvatarCopyType.Make("rotationconstraint", "Rotation constraints", true, false, "UnityEngine.Animations.RotationConstraint"),
            AvatarCopyType.Make("scaleconstraint", "Scale constraints", true, false, "UnityEngine.Animations.ScaleConstraint"),
            AvatarCopyType.Make("vrcaimconstraint", "VRC Aim constraints", true, true, VrcConstraintPrefix + "AimConstraint"),
            AvatarCopyType.Make("vrclookatconstraint", "VRC LookAt constraints", true, true, VrcConstraintPrefix + "LookAtConstraint"),
            AvatarCopyType.Make("vrcparentconstraint", "VRC Parent constraints", true, true, VrcConstraintPrefix + "ParentConstraint"),
            AvatarCopyType.Make("vrcpositionconstraint", "VRC Position constraints", true, true, VrcConstraintPrefix + "PositionConstraint"),
            AvatarCopyType.Make("vrcrotationconstraint", "VRC Rotation constraints", true, true, VrcConstraintPrefix + "RotationConstraint"),
            AvatarCopyType.Make("vrcscaleconstraint", "VRC Scale constraints", true, true, VrcConstraintPrefix + "ScaleConstraint"),
            AvatarCopyType.Make("characterjoint", "Character joints", false, false, "UnityEngine.CharacterJoint"),
            AvatarCopyType.Make("configurablejoint", "Configurable joints", false, false, "UnityEngine.ConfigurableJoint"),
            AvatarCopyType.Make("fixedjoint", "Fixed joints", false, false, "UnityEngine.FixedJoint"),
            AvatarCopyType.Make("hingejoint", "Hinge joints", false, false, "UnityEngine.HingeJoint"),
            AvatarCopyType.Make("springjoint", "Spring joints", false, false, "UnityEngine.SpringJoint"),
            AvatarCopyType.Make(Transforms, "Transforms", false, false, "UnityEngine.Transform"),
            AvatarCopyType.Make(MeshRenderers, "MeshRenderers", true, false, "UnityEngine.MeshRenderer"),
            AvatarCopyType.Make(SkinnedMeshRenderers, "SkinnedMeshRenderers", true, false, "UnityEngine.SkinnedMeshRenderer"),
            AvatarCopyType.Make(Cameras, "Cameras", false, false, "UnityEngine.Camera"),
            AvatarCopyType.Make("finalik", "FinalIK", false, false, "RootMotion.FinalIK.CCDIK", "RootMotion.FinalIK.LimbIK",
                "RootMotion.FinalIK.RotationLimit", "RootMotion.FinalIK.FABRIK", "RootMotion.FinalIK.AimIK", "RootMotion.FinalIK.FullBodyBipedIK",
                "RootMotion.FinalIK.VRIK", "RootMotion.FinalIK.Grounder", "RootMotion.FinalIK.IKExecutionOrder"),
            AvatarCopyType.Make(AvatarDescriptor, "Avatar Descriptor", true, true, DescriptorType),
            AvatarCopyType.Make(ExtraTypes, "Extra types", true, false)
        };

        internal static AvatarCopyType Get(string id)
        {
            return Array.Find(All, type => type.Id == id);
        }

        /// <summary>A type by full name in any loaded assembly, or null.</summary>
        internal static Type Find(string fullName)
        {
            Type found;
            if (Cache.TryGetValue(fullName, out found)) return found;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    found = assembly.GetType(fullName, false);
                }
                catch (Exception)
                {
                    found = null;
                }
                if (found != null) break;
            }
            Cache[fullName] = found;
            return found;
        }

        /// <summary>A type by its simple name in any loaded assembly, or null (for SDK editor classes).</summary>
        internal static Type FindByName(string name)
        {
            Type found;
            if (Cache.TryGetValue("?" + name, out found)) return found;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException exception)
                {
                    types = exception.Types;
                }
                foreach (Type type in types)
                {
                    if (type == null || type.Name != name) continue;
                    found = type;
                    break;
                }
                if (found != null) break;
            }
            Cache["?" + name] = found;
            return found;
        }
    }
}

// ============================================================================
// GLOSSARY: Avatar Migration Assistant component types (CopyTypes.cs, part of Editor/AvatarMigrationAssistant.cs)
// ============================================================================
//
// AvatarCopyType     Id, Label, Common (the short list), NeedsSdk (a VRChat type), TypeNames; Types (found in this
//                    project), Available, Matches(component).
// AvatarCopyTypes.All  Run order (feature 3): Prefabs, ParticleSystems, Colliders, Rigidbodies, TrailRenderers, Lights,
//                    Animators, AudioSources, Contacts, PhysBone colliders, PhysBones, VRCStation, VRCHeadChop,
//                    DynamicBone colliders, DynamicBones, Unity constraints, VRC constraints, Joints, Transforms,
//                    MeshRenderers, SkinnedMeshRenderers, Cameras, FinalIK, Avatar Descriptor, Extra types (active
//                    state, tag and layer are Transforms sub-options, run after Cameras). Ids (prefab, collider, ...) key the settings and the type rules.
// Find(fullName) / FindByName(name)  Types by name across the loaded assemblies (cached).
