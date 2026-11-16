using System;
using System.Collections.Generic;

namespace Riry.RiryTools.Editor
{
    /// <summary>Which backend drives an avatar, with the evidence for it.</summary>
    internal sealed class FTEmulatorResolution
    {
        internal const string Confirmed = "Confirmed";
        internal const string Probable = "Probable";
        internal const string Mixed = "Mixed";
        internal const string Unknown = "Unknown";
        internal const string NeedsBuildPreview = "Needs build preview";

        /// <summary>"native", "legacy", or null when nothing may be written.</summary>
        internal string Backend;
        /// <summary>The legacy parameter family found on the avatar, also beside a native setup; null when there is none.</summary>
        internal FTEmulatorAvatarProfile ParameterSet;
        /// <summary>What the setup is called: "Native", "Native / Pawlygon 2.x", "Adjerry legacy", ...</summary>
        internal string Template = "";
        internal string Confidence = Unknown;
        internal readonly List<string> Evidence = new List<string>();
        /// <summary>What stands in the way of a full preview.</summary>
        internal readonly List<string> Missing = new List<string>();
        /// <summary>A native setup and legacy parameters are both on the avatar.</summary>
        internal bool NativeWithLegacy;

        internal string Summary
        {
            get { return (string.IsNullOrEmpty(Template) ? "None" : Template) + " · " + Confidence; }
        }
    }

    /// <summary>
    /// Decides the avatar backend from a snapshot: native settings on the descriptor come first, then the signs a
    /// template leaves (its components, its own parameter prefixes, its package), then the legacy parameter family.
    /// A lone FT/v2 prefix names the family, not the author: Adjerry's and Pawlygon's templates share those inputs, so
    /// the author is only named when a parameter prefix of that template alone is present. A setup the catalog does not
    /// describe is Unknown, never matched to the nearest name. The user's own choice replaces the detection but not the
    /// checks: choosing Native for an avatar without native settings still writes nothing.
    /// </summary>
    internal static class FTEmulatorTemplateResolver
    {
        internal const string Auto = "auto";
        internal const string NativeChoice = "native";

        internal static FTEmulatorResolution Resolve(FTEmulatorAvatarSnapshot snapshot, FTEmulatorCatalog catalog, string choice)
        {
            var result = new FTEmulatorResolution();
            bool native = snapshot.Native != null && snapshot.Native.Configured;

            // The legacy family: the parameter set most of the avatar's Expression Parameters bind to.
            FTEmulatorAvatarProfile family = null;
            int bound = 0;
            foreach (FTEmulatorAvatarProfile profile in catalog.Avatars)
            {
                if (profile.Kind != FTEmulatorAvatarProfile.Parameters) continue;
                int count = new FTEmulatorLegacyMapper(catalog, profile, snapshot.ExpressionParameters, null, null).Bindings.Count;
                if (count > bound)
                {
                    bound = count;
                    family = profile;
                }
            }

            // Templates whose signature is on the avatar.
            var nativeTemplates = new List<FTEmulatorAvatarProfile>();
            var legacyTemplates = new List<FTEmulatorAvatarProfile>();
            var pending = new List<string>();
            foreach (FTEmulatorAvatarProfile profile in catalog.Avatars)
            {
                if (profile.Kind != FTEmulatorAvatarProfile.Template) continue;
                string component = null, prefix = null, package = null;
                foreach (string type in profile.SignatureComponents)
                {
                    if (snapshot.Components.Contains(type)) { component = type; break; }
                }
                foreach (string candidate in profile.SignaturePrefixes)
                {
                    if (HasPrefix(snapshot, candidate)) { prefix = candidate; break; }
                }
                foreach (FTEmulatorProfilePackage candidate in profile.SignaturePackages)
                {
                    if (!snapshot.Packages.TryGetValue(candidate.Name, out string version)) continue;
                    if (candidate.MinVersion != null && FTEmulatorCatalog.CompareVersions(version, candidate.MinVersion) < 0) continue;
                    if (candidate.MaxVersion != null && FTEmulatorCatalog.CompareVersions(version, candidate.MaxVersion) > 0) continue;
                    package = candidate.Name + " " + version;
                    break;
                }
                if (profile.Backend == FTEmulatorAvatarProfile.Native)
                {
                    // A native template is seen by its components (before its build step) or its package (after it).
                    if (component == null && package == null) continue;
                    if (!native && component != null) pending.Add(profile.Name + " (" + component + ")");
                    if (!native) continue;
                    nativeTemplates.Add(profile);
                    result.Evidence.Add(profile.Name + ": " + (component != null ? "component " + component : "package " + package + " installed (weak)"));
                }
                else if (prefix != null)
                {
                    // Only a parameter prefix of its own names a legacy template; its package alone does not.
                    legacyTemplates.Add(profile);
                    result.Evidence.Add(profile.Name + ": parameters under " + prefix + (package != null ? ", package " + package : ""));
                }
            }

            if (native)
            {
                result.Evidence.Insert(0, "Descriptor: Face Tracking enabled, " + snapshot.Native.Sets.Count + " blendshapes set(s), " +
                    snapshot.Native.Behaviors.Count + " parameter behavior(s), " + snapshot.Native.MeshCount + " mesh(es)");
            }
            if (bound > 0) result.Evidence.Add(family.Name + ": " + bound + " Expression Parameter(s) bind");

            bool wantNative, wantLegacy;
            FTEmulatorAvatarProfile chosen = null;
            if (string.IsNullOrEmpty(choice) || choice == Auto)
            {
                wantNative = native;
                wantLegacy = !native && bound > 0;
            }
            else if (choice == NativeChoice)
            {
                wantNative = true;
                wantLegacy = false;
                result.Evidence.Add("Backend chosen by hand: Native");
            }
            else
            {
                chosen = catalog.GetAvatarProfile(choice);
                wantNative = chosen != null && chosen.Kind == FTEmulatorAvatarProfile.Template && chosen.Backend == FTEmulatorAvatarProfile.Native;
                wantLegacy = chosen != null && !wantNative;
                result.Evidence.Add(chosen != null ? "Backend chosen by hand: " + chosen.Name : "The chosen profile '" + choice + "' is not in the catalog");
            }

            if (wantNative)
            {
                if (!snapshot.NativeSupported) result.Missing.Add("Native face tracking needs VRChat Avatars SDK 3.10.6 or newer");
                else if (!native) result.Missing.Add("The avatar descriptor has no native face tracking set up");
                else
                {
                    result.Backend = FTEmulatorAvatarProfile.Native;
                    result.Template = nativeTemplates.Count == 1 ? "Native / " + nativeTemplates[0].Name : "Native";
                    result.Confidence = FTEmulatorResolution.Confirmed;
                    result.ParameterSet = bound > 0 ? family : null;
                    result.NativeWithLegacy = bound > 0;
                    if (chosen != null && chosen.Kind == FTEmulatorAvatarProfile.Template) result.Template = "Native / " + chosen.Name;
                }
            }
            else if (wantLegacy)
            {
                FTEmulatorAvatarProfile set = chosen == null ? family
                    : chosen.Kind == FTEmulatorAvatarProfile.Parameters ? chosen : catalog.GetAvatarProfile(chosen.ParameterSet);
                int count = set == null ? 0 : set == family ? bound : new FTEmulatorLegacyMapper(catalog, set, snapshot.ExpressionParameters, null, null).Bindings.Count;
                if (set == null || count == 0) result.Missing.Add("No Expression Parameter of the avatar belongs to " + (set != null ? set.Name : "a known parameter family"));
                else
                {
                    result.Backend = FTEmulatorAvatarProfile.Legacy;
                    result.ParameterSet = set;
                    if (chosen != null && chosen.Kind == FTEmulatorAvatarProfile.Template)
                    {
                        result.Template = chosen.Name;
                        result.Confidence = legacyTemplates.Contains(chosen) ? FTEmulatorResolution.Confirmed : FTEmulatorResolution.Probable;
                    }
                    else if (legacyTemplates.Count == 1)
                    {
                        result.Template = legacyTemplates[0].Name;
                        result.Confidence = FTEmulatorResolution.Confirmed;
                    }
                    else if (legacyTemplates.Count > 1)
                    {
                        result.Template = set.Name;
                        result.Confidence = FTEmulatorResolution.Mixed;
                        result.Evidence.Add("Parameters of more than one template are present: the author is not established");
                    }
                    else
                    {
                        result.Template = set.Name;
                        result.Confidence = FTEmulatorResolution.Probable;
                        result.Evidence.Add("Only the shared input parameters were found: the family is known, the author is not");
                    }
                }
            }

            if (result.Backend == null)
            {
                if (pending.Count > 0)
                {
                    result.Confidence = FTEmulatorResolution.NeedsBuildPreview;
                    result.Template = "Not built";
                    result.Missing.Add("Set up by components that have not run yet (" + string.Join(", ", pending.ToArray()) +
                        "): play a built test copy, or let the build step run when Play Mode starts");
                }
                else
                {
                    result.Confidence = FTEmulatorResolution.Unknown;
                    if (result.Missing.Count == 0) result.Missing.Add("No face tracking setup the catalog describes was found: add a mapping per parameter in the debug window");
                }
            }
            foreach (string name in snapshot.TypeConflicts)
                result.Missing.Add("'" + name + "' has different types in different controllers and is not in Expression Parameters: it is not written");
            return result;
        }

        private static bool HasPrefix(FTEmulatorAvatarSnapshot snapshot, string prefix)
        {
            foreach (KeyValuePair<string, FTEmulatorValueType> pair in snapshot.ControllerParameters)
            {
                if (pair.Key.StartsWith(prefix, StringComparison.Ordinal)) return true;
            }
            foreach (FTEmulatorAvatarParameter parameter in snapshot.ExpressionParameters)
            {
                if (parameter.Name.StartsWith(prefix, StringComparison.Ordinal)) return true;
            }
            return false;
        }
    }
}

// ============================================================================
// GLOSSARY: FTEmulator template resolver
// ============================================================================
//
// FTEmulatorResolution       Backend ("native", "legacy" or null), ParameterSet, Template, Confidence (Confirmed, Probable,
//                            Mixed, Unknown, Needs build preview), Evidence, Missing, NativeWithLegacy, Summary.
// FTEmulatorTemplateResolver (this file)
//   Resolve(snapshot, catalog, choice)  choice: Auto, NativeChoice, or an avatar profile's id.
//                            Native: the descriptor has Face Tracking set up (Confirmed); a native template is named when its
//                            component or package is found.
//                            Legacy: Expression Parameters bind to a parameter set. One template's own prefix present:
//                            that template, Confirmed. Several: Mixed. None: the family only, Probable.
//                            Neither: Needs build preview when a native template's components are on the avatar and the
//                            descriptor is not set up yet, else Unknown.
//                            Missing also lists parameters with a type conflict.
