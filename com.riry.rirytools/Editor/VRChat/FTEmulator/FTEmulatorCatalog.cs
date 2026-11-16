using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Riry.RiryTools.Editor
{
    /// <summary>A channel a device measures on its own, and how Unified Expression input reduces to it.</summary>
    internal sealed class FTEmulatorHardwareControl
    {
        internal string Name;
        internal string Group;
        internal float Min;
        internal float Max = 1f;
        internal float Neutral;
        /// <summary>The control's value from tracking keys; null when it cannot be worked out from them (manual only).</summary>
        internal FTEmulatorExpression From;
    }

    /// <summary>A tracking key a device drives, from its controls.</summary>
    internal sealed class FTEmulatorHardwareOutput
    {
        internal int Key;
        internal FTEmulatorExpression Value;
        /// <summary>Added by the device's VRCFT module, not measured: only with Documented module behavior.</summary>
        internal bool Derived;
        internal string Note;
    }

    /// <summary>What a device can track. A software capability list, not a model of its accuracy.</summary>
    internal sealed class FTEmulatorHardwareProfile
    {
        internal string Id;
        internal string Name;
        internal string Detail;
        internal string Version;
        internal int Order;
        internal bool Unrestricted;
        internal bool EyeTracking;
        internal bool FaceTracking;
        internal string[] Evidence;
        internal string[] Notes;
        internal FTEmulatorHardwareControl[] Controls;
        internal FTEmulatorHardwareOutput[] Outputs;
        internal Dictionary<string, int> ControlIndex;

        internal string Label
        {
            get { return string.IsNullOrEmpty(Detail) ? Name : Name + " (" + Detail + ")"; }
        }
    }

    /// <summary>A legacy avatar parameter and the formula that gives its value from tracking keys.</summary>
    internal sealed class FTEmulatorProfileParameter
    {
        internal string Name;
        internal FTEmulatorExpression Value;
        internal float Min;
        internal float Max = 1f;
        /// <summary>A Bool parameter of this name is true while the value is below this.</summary>
        internal float BoolBelow = 0.5f;
    }

    internal sealed class FTEmulatorProfilePackage
    {
        internal string Name;
        internal string MinVersion;
        internal string MaxVersion;
    }

    /// <summary>
    /// Either a parameter set (Kind "parameters": exact names, types and formulas of a legacy parameter family) or a
    /// template (Kind "template": the signs one author's setup leaves on an avatar, and the backend it uses).
    /// </summary>
    internal sealed class FTEmulatorAvatarProfile
    {
        internal const string Parameters = "parameters";
        internal const string Template = "template";
        internal const string Native = "native";
        internal const string Legacy = "legacy";

        internal string Id;
        internal string Kind;
        internal string Name;
        internal string Version;
        internal string[] Evidence;
        internal string[] Notes;

        // Kind "parameters".
        internal FTEmulatorProfileParameter[] Entries = new FTEmulatorProfileParameter[0];
        internal string[] ExcludePrefixes = new string[0];
        /// <summary>Bool parameters the emulator owns: true while tracking of that region ("eye" or "face") is available.</summary>
        internal KeyValuePair<string, string>[] Status = new KeyValuePair<string, string>[0];

        // Kind "template".
        internal string Backend;
        internal string ParameterSet;
        internal string[] SignaturePrefixes = new string[0];
        internal string[] SignatureComponents = new string[0];
        internal FTEmulatorProfilePackage[] SignaturePackages = new FTEmulatorProfilePackage[0];
    }

    /// <summary>
    /// FTEmulator's data: the tracking keys in wire order (MappingSchemas), the hardware profiles (HardwareProfiles) and
    /// the avatar profiles (AvatarProfiles), read from the JSON files beside this script and checked. A catalog is not
    /// changed after it is read: reading the files again makes a new one, so a running session keeps the one it started with.
    /// </summary>
    internal sealed class FTEmulatorCatalog
    {
        internal const int SchemaVersion = 1;
        internal const string UnrestrictedId = "unrestricted";
        internal const string EyeRegion = "eye";
        internal const string FaceRegion = "face";
        // The folders' GUIDs (their .meta files), so the data is found wherever the package is installed.
        internal const string SchemaFolderGuid = "311b68250f50442e9fd0792f0665a214";
        internal const string HardwareFolderGuid = "762a0ef7a65b45ab944c36b36148869d";
        internal const string AvatarFolderGuid = "e173195361fc4488ba7d04cd43776d4a";

        /// <summary>The tracking keys; a key's place is its byte in the /tracking/face/v1 blob.</summary>
        internal string[] Keys = new string[0];
        internal bool[] EyeKey = new bool[0];
        internal string Address = "";
        internal Dictionary<string, int> KeyIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        internal readonly List<FTEmulatorHardwareProfile> Hardware = new List<FTEmulatorHardwareProfile>();
        internal readonly List<FTEmulatorAvatarProfile> Avatars = new List<FTEmulatorAvatarProfile>();
        /// <summary>What was wrong with the files. A file with a problem is left out; a schema problem leaves no keys.</summary>
        internal readonly List<string> Problems = new List<string>();

        /// <summary>False when the tracking key schema could not be read: nothing can run.</summary>
        internal bool Usable
        {
            get { return Keys.Length > 0 && Hardware.Count > 0; }
        }

        internal int Key(string name)
        {
            return name != null && KeyIndex.TryGetValue(name, out int index) ? index : -1;
        }

        internal FTEmulatorHardwareProfile GetHardware(string id)
        {
            return Hardware.Find(profile => profile.Id == id);
        }

        internal FTEmulatorAvatarProfile GetAvatarProfile(string id)
        {
            return Avatars.Find(profile => profile.Id == id);
        }

        /// <summary>Reads the package's data folders, found by GUID.</summary>
        internal static FTEmulatorCatalog LoadFromPackage()
        {
            string schema = UnityEditor.AssetDatabase.GUIDToAssetPath(SchemaFolderGuid);
            string hardware = UnityEditor.AssetDatabase.GUIDToAssetPath(HardwareFolderGuid);
            string avatars = UnityEditor.AssetDatabase.GUIDToAssetPath(AvatarFolderGuid);
            if (string.IsNullOrEmpty(schema) || string.IsNullOrEmpty(hardware) || string.IsNullOrEmpty(avatars))
            {
                var missing = new FTEmulatorCatalog();
                missing.Problems.Add("FTEmulator's data folders (MappingSchemas, HardwareProfiles, AvatarProfiles) are missing from the package.");
                return missing;
            }
            return Load(Path.GetFullPath(schema), Path.GetFullPath(hardware), Path.GetFullPath(avatars));
        }

        /// <summary>Reads every .json file of the three folders.</summary>
        internal static FTEmulatorCatalog Load(string schemaFolder, string hardwareFolder, string avatarFolder)
        {
            return Parse(Files(schemaFolder), Files(hardwareFolder), Files(avatarFolder));
        }

        private static List<KeyValuePair<string, string>> Files(string folder)
        {
            var result = new List<KeyValuePair<string, string>>();
            if (!Directory.Exists(folder)) return result;
            string[] paths = Directory.GetFiles(folder, "*.json");
            Array.Sort(paths, StringComparer.Ordinal);
            foreach (string path in paths) result.Add(new KeyValuePair<string, string>(Path.GetFileName(path), File.ReadAllText(path)));
            return result;
        }

        /// <summary>Builds a catalog from file texts (name, JSON), as Load does from folders.</summary>
        internal static FTEmulatorCatalog Parse(IEnumerable<KeyValuePair<string, string>> schemas, IEnumerable<KeyValuePair<string, string>> hardware,
            IEnumerable<KeyValuePair<string, string>> avatars)
        {
            var catalog = new FTEmulatorCatalog();
            foreach (KeyValuePair<string, string> file in schemas)
            {
                try
                {
                    if (catalog.Keys.Length > 0) throw new FormatException("a tracking key schema was already read; only one is supported");
                    catalog.ReadSchema(Root(file.Value));
                }
                catch (FormatException exception)
                {
                    catalog.Problems.Add(file.Key + ": " + exception.Message);
                }
            }
            if (catalog.Keys.Length == 0)
            {
                catalog.Problems.Add("No tracking key schema could be read, so no profile was loaded.");
                return catalog;
            }
            foreach (KeyValuePair<string, string> file in hardware)
            {
                try
                {
                    FTEmulatorHardwareProfile profile = catalog.ReadHardware(Root(file.Value));
                    if (catalog.GetHardware(profile.Id) != null) throw new FormatException("the id \"" + profile.Id + "\" is used by another hardware profile");
                    catalog.Hardware.Add(profile);
                }
                catch (FormatException exception)
                {
                    catalog.Problems.Add(file.Key + ": " + exception.Message);
                }
            }
            catalog.Hardware.Sort((a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : string.CompareOrdinal(a.Id, b.Id));
            foreach (KeyValuePair<string, string> file in avatars)
            {
                try
                {
                    FTEmulatorAvatarProfile profile = catalog.ReadAvatar(Root(file.Value));
                    if (catalog.GetAvatarProfile(profile.Id) != null) throw new FormatException("the id \"" + profile.Id + "\" is used by another avatar profile");
                    catalog.Avatars.Add(profile);
                }
                catch (FormatException exception)
                {
                    catalog.Problems.Add(file.Key + ": " + exception.Message);
                }
            }
            // A template that names a parameter set needs that set.
            foreach (FTEmulatorAvatarProfile profile in catalog.Avatars.ToList())
            {
                if (profile.Kind != FTEmulatorAvatarProfile.Template || string.IsNullOrEmpty(profile.ParameterSet)) continue;
                FTEmulatorAvatarProfile set = catalog.GetAvatarProfile(profile.ParameterSet);
                if (set != null && set.Kind == FTEmulatorAvatarProfile.Parameters) continue;
                catalog.Problems.Add(profile.Id + ": its parameter set \"" + profile.ParameterSet + "\" was not read.");
                catalog.Avatars.Remove(profile);
            }
            return catalog;
        }

        private static Dictionary<string, object> Root(string text)
        {
            Dictionary<string, object> root = FTEmulatorJson.Object(FTEmulatorJson.Parse(text), "The file");
            double schema = FTEmulatorJson.Number(root, "schema", -1);
            if (schema != SchemaVersion) throw new FormatException("\"schema\" is " + schema + "; this version reads schema " + SchemaVersion + " only");
            return root;
        }

        private static string Id(Dictionary<string, object> root)
        {
            string id = FTEmulatorJson.Text(root, "id");
            if (string.IsNullOrWhiteSpace(id)) throw new FormatException("\"id\" is missing");
            return id;
        }

        private static string[] Evidence(Dictionary<string, object> root, bool needed)
        {
            string[] evidence = FTEmulatorJson.Texts(root, "evidence");
            if (needed && evidence.Length == 0) throw new FormatException("\"evidence\" is empty: a profile must say what it was read from");
            return evidence;
        }

        private static bool Identifier(string name)
        {
            if (string.IsNullOrEmpty(name) || !(char.IsLetter(name[0]) || name[0] == '_')) return false;
            foreach (char c in name)
            {
                if (!char.IsLetterOrDigit(c) && c != '_') return false;
            }
            return true;
        }

        private void ReadSchema(Dictionary<string, object> root)
        {
            Id(root);
            Evidence(root, true);
            List<object> list = FTEmulatorJson.List(root, "keys");
            int length = (int)FTEmulatorJson.Number(root, "blobLength", -1);
            if (list.Count == 0 || list.Count != length) throw new FormatException("\"keys\" has " + list.Count + " entries but \"blobLength\" is " + length);
            var keys = new string[list.Count];
            var eye = new bool[list.Count];
            var index = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < list.Count; i++)
            {
                Dictionary<string, object> entry = FTEmulatorJson.Object(list[i], "A key");
                string name = FTEmulatorJson.Text(entry, "name");
                if (!Identifier(name)) throw new FormatException("key " + i + " has no usable name");
                if (index.ContainsKey(name)) throw new FormatException("the key \"" + name + "\" is listed twice");
                string region = FTEmulatorJson.Text(entry, "region", FaceRegion);
                if (region != EyeRegion && region != FaceRegion) throw new FormatException("the key \"" + name + "\" has the unknown region \"" + region + "\"");
                keys[i] = name;
                eye[i] = region == EyeRegion;
                index[name] = i;
            }
            Keys = keys;
            EyeKey = eye;
            KeyIndex = index;
            Address = FTEmulatorJson.Text(root, "address", "");
        }

        private FTEmulatorHardwareProfile ReadHardware(Dictionary<string, object> root)
        {
            var profile = new FTEmulatorHardwareProfile
            {
                Id = Id(root),
                Name = FTEmulatorJson.Text(root, "name"),
                Detail = FTEmulatorJson.Text(root, "detail", ""),
                Version = FTEmulatorJson.Text(root, "version", ""),
                Order = (int)FTEmulatorJson.Number(root, "order", 1000),
                Unrestricted = FTEmulatorJson.Flag(root, "unrestricted"),
                EyeTracking = FTEmulatorJson.Flag(root, "eyeTracking"),
                FaceTracking = FTEmulatorJson.Flag(root, "faceTracking"),
                Notes = FTEmulatorJson.Texts(root, "notes"),
            };
            if (string.IsNullOrWhiteSpace(profile.Name)) throw new FormatException("\"name\" is missing");
            profile.Evidence = Evidence(root, !profile.Unrestricted);
            var controls = new List<FTEmulatorHardwareControl>();
            var outputs = new List<FTEmulatorHardwareOutput>();
            var controlIndex = new Dictionary<string, int>(StringComparer.Ordinal);
            if (profile.Unrestricted)
            {
                // Every tracking key is its own control.
                for (int key = 0; key < Keys.Length; key++)
                {
                    int index = key;
                    controlIndex[Keys[key]] = key;
                    controls.Add(new FTEmulatorHardwareControl { Name = Keys[key], Group = EyeKey[key] ? "Eye" : "Face", From = Compile(Keys[key], Key, "key") });
                    outputs.Add(new FTEmulatorHardwareOutput { Key = key, Value = Compile(Keys[key], name => name == Keys[index] ? index : -1, "control") });
                }
            }
            else
            {
                foreach (object item in FTEmulatorJson.List(root, "controls"))
                {
                    Dictionary<string, object> entry = FTEmulatorJson.Object(item, "A control");
                    var control = new FTEmulatorHardwareControl
                    {
                        Name = FTEmulatorJson.Text(entry, "name"),
                        Group = FTEmulatorJson.Text(entry, "group", ""),
                        Min = (float)FTEmulatorJson.Number(entry, "min", 0),
                        Max = (float)FTEmulatorJson.Number(entry, "max", 1),
                        Neutral = (float)FTEmulatorJson.Number(entry, "neutral", 0),
                    };
                    if (!Identifier(control.Name)) throw new FormatException("a control has no usable name (letters, digits and _)");
                    if (controlIndex.ContainsKey(control.Name)) throw new FormatException("the control \"" + control.Name + "\" is listed twice");
                    if (!(control.Min < control.Max) || control.Neutral < control.Min || control.Neutral > control.Max)
                        throw new FormatException("the control \"" + control.Name + "\" has an unusable range (min, max, neutral)");
                    string from = FTEmulatorJson.Text(entry, "from");
                    // A control reads tracking keys only, never another control, so couplings cannot form a loop.
                    if (from != null) control.From = Compile(from, Key, "control \"" + control.Name + "\": tracking key");
                    controlIndex[control.Name] = controls.Count;
                    controls.Add(control);
                }
                var driven = new HashSet<int>();
                foreach (object item in FTEmulatorJson.List(root, "outputs"))
                {
                    Dictionary<string, object> entry = FTEmulatorJson.Object(item, "An output");
                    string key = FTEmulatorJson.Text(entry, "key");
                    int index = Key(key);
                    if (index < 0) throw new FormatException("an output names the unknown tracking key \"" + key + "\"");
                    if (!driven.Add(index)) throw new FormatException("the tracking key \"" + key + "\" has two outputs");
                    outputs.Add(new FTEmulatorHardwareOutput
                    {
                        Key = index,
                        Value = Compile(FTEmulatorJson.Text(entry, "value"), name => controlIndex.TryGetValue(name, out int at) ? at : -1,
                            "output \"" + key + "\": control"),
                        Derived = FTEmulatorJson.Flag(entry, "derived"),
                        Note = FTEmulatorJson.Text(entry, "note", ""),
                    });
                }
            }
            profile.Controls = controls.ToArray();
            profile.Outputs = outputs.ToArray();
            profile.ControlIndex = controlIndex;
            return profile;
        }

        private static FTEmulatorExpression Compile(string text, Func<string, int> names, string what)
        {
            FTEmulatorExpression expression = FTEmulatorExpression.Compile(text, names, out string error);
            if (expression == null) throw new FormatException(what + ": " + error + " in \"" + text + "\"");
            return expression;
        }

        private FTEmulatorAvatarProfile ReadAvatar(Dictionary<string, object> root)
        {
            var profile = new FTEmulatorAvatarProfile
            {
                Id = Id(root),
                Kind = FTEmulatorJson.Text(root, "kind"),
                Name = FTEmulatorJson.Text(root, "name"),
                Version = FTEmulatorJson.Text(root, "version", ""),
                Evidence = Evidence(root, true),
                Notes = FTEmulatorJson.Texts(root, "notes"),
            };
            if (string.IsNullOrWhiteSpace(profile.Name)) throw new FormatException("\"name\" is missing");
            if (profile.Kind == FTEmulatorAvatarProfile.Parameters)
            {
                var entries = new List<FTEmulatorProfileParameter>();
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (object item in FTEmulatorJson.List(root, "parameters"))
                {
                    Dictionary<string, object> entry = FTEmulatorJson.Object(item, "A parameter");
                    var parameter = new FTEmulatorProfileParameter
                    {
                        Name = FTEmulatorJson.Text(entry, "name"),
                        Min = (float)FTEmulatorJson.Number(entry, "min", 0),
                        Max = (float)FTEmulatorJson.Number(entry, "max", 1),
                        BoolBelow = (float)FTEmulatorJson.Number(entry, "boolBelow", 0.5),
                    };
                    if (string.IsNullOrWhiteSpace(parameter.Name)) throw new FormatException("a parameter has no name");
                    if (!names.Add(parameter.Name)) throw new FormatException("the parameter \"" + parameter.Name + "\" is listed twice");
                    if (!(parameter.Min < parameter.Max)) throw new FormatException("the parameter \"" + parameter.Name + "\" has an unusable range");
                    parameter.Value = Compile(FTEmulatorJson.Text(entry, "value"), Key, "parameter \"" + parameter.Name + "\": tracking key");
                    entries.Add(parameter);
                }
                if (entries.Count == 0) throw new FormatException("\"parameters\" is empty");
                profile.Entries = entries.ToArray();
                profile.ExcludePrefixes = FTEmulatorJson.Texts(root, "excludePrefixes");
                var status = new List<KeyValuePair<string, string>>();
                foreach (object item in FTEmulatorJson.List(root, "status"))
                {
                    Dictionary<string, object> entry = FTEmulatorJson.Object(item, "A status parameter");
                    string name = FTEmulatorJson.Text(entry, "name");
                    string region = FTEmulatorJson.Text(entry, "region");
                    if (string.IsNullOrWhiteSpace(name) || region != EyeRegion && region != FaceRegion)
                        throw new FormatException("a status parameter needs a name and the region \"eye\" or \"face\"");
                    status.Add(new KeyValuePair<string, string>(name, region));
                }
                profile.Status = status.ToArray();
            }
            else if (profile.Kind == FTEmulatorAvatarProfile.Template)
            {
                profile.Backend = FTEmulatorJson.Text(root, "backend");
                if (profile.Backend != FTEmulatorAvatarProfile.Native && profile.Backend != FTEmulatorAvatarProfile.Legacy)
                    throw new FormatException("\"backend\" must be \"native\" or \"legacy\"");
                profile.ParameterSet = FTEmulatorJson.Text(root, "parameterSet");
                if (profile.Backend == FTEmulatorAvatarProfile.Legacy && string.IsNullOrEmpty(profile.ParameterSet))
                    throw new FormatException("a legacy template needs \"parameterSet\"");
                if (root.TryGetValue("signatures", out object value) && value != null)
                {
                    Dictionary<string, object> signatures = FTEmulatorJson.Object(value, "\"signatures\"");
                    profile.SignaturePrefixes = FTEmulatorJson.Texts(signatures, "parameterPrefixes");
                    profile.SignatureComponents = FTEmulatorJson.Texts(signatures, "componentTypes");
                    var packages = new List<FTEmulatorProfilePackage>();
                    foreach (object item in FTEmulatorJson.List(signatures, "packages"))
                    {
                        Dictionary<string, object> entry = FTEmulatorJson.Object(item, "A package");
                        var package = new FTEmulatorProfilePackage
                        {
                            Name = FTEmulatorJson.Text(entry, "name"),
                            MinVersion = FTEmulatorJson.Text(entry, "minVersion"),
                            MaxVersion = FTEmulatorJson.Text(entry, "maxVersion"),
                        };
                        if (string.IsNullOrWhiteSpace(package.Name)) throw new FormatException("a package signature has no name");
                        packages.Add(package);
                    }
                    profile.SignaturePackages = packages.ToArray();
                }
            }
            else throw new FormatException("\"kind\" must be \"parameters\" or \"template\"");
            return profile;
        }

        /// <summary>
        /// Compares two versions by their leading numbers ("2.0.7" with "1.999.999"): below 0, 0 or above 0. Text after the
        /// numbers (a pre-release label) is not compared.
        /// </summary>
        internal static int CompareVersions(string a, string b)
        {
            int[] left = Numbers(a), right = Numbers(b);
            for (int i = 0; i < Math.Max(left.Length, right.Length); i++)
            {
                int l = i < left.Length ? left[i] : 0, r = i < right.Length ? right[i] : 0;
                if (l != r) return l.CompareTo(r);
            }
            return 0;
        }

        private static int[] Numbers(string version)
        {
            var result = new List<int>();
            foreach (string part in (version ?? "").Split('.', '-', '+'))
            {
                if (!int.TryParse(part, out int number)) break;
                result.Add(number);
            }
            return result.ToArray();
        }
    }
}

// ============================================================================
// GLOSSARY: FTEmulator catalog (profile data)
// ============================================================================
//
// FTEmulatorCatalog (this file)
//   Keys / EyeKey / KeyIndex / Key(name)  The tracking keys in wire order (a key's place is its byte in the native blob),
//                            which are in the eye region, and a key's place by name (below 0: unknown).
//   Hardware / GetHardware(id)   The hardware profiles, sorted by "order".
//   Avatars / GetAvatarProfile(id)  The avatar profiles: parameter sets and templates.
//   Problems / Usable        What was wrong with the files; Usable is false without keys or without any hardware profile.
//   LoadFromPackage()        Reads the three data folders, found by SchemaFolderGuid, HardwareFolderGuid and AvatarFolderGuid.
//   Load(folders) / Parse(file texts)  Every .json file; a file with a problem is left out and named in Problems. Rejected: a
//                            schema other than SchemaVersion (1), a missing or repeated id, no evidence (except the
//                            unrestricted profile), a repeated control, key or parameter, a formula that does not compile
//                            (unknown names included), a key with two outputs, an unusable range, a template whose
//                            parameter set was not read.
//   CompareVersions(a, b)    Compares the leading numbers of two versions.
// FTEmulatorHardwareProfile  Id, Name, Detail, Version, Order, Unrestricted, EyeTracking, FaceTracking, Evidence, Notes,
//                            Controls, Outputs, ControlIndex, Label. The unrestricted profile gets one control per key.
// FTEmulatorHardwareControl  Name, Group, Min, Max, Neutral, From (its value from tracking keys; null: manual only).
// FTEmulatorHardwareOutput   Key, Value (from controls), Derived (only with Documented module behavior), Note.
// FTEmulatorAvatarProfile    Kind "parameters": Entries (FTEmulatorProfileParameter: Name, Value, Min, Max, BoolBelow),
//                            ExcludePrefixes, Status (name, region). Kind "template": Backend ("native" or "legacy"),
//                            ParameterSet, SignaturePrefixes, SignatureComponents, SignaturePackages.
