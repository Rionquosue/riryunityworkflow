using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// FTEmulator's preferences, kept per user and per project (EditorUserSettings, in UserSettings/). Only choices are
    /// kept: no endpoint, no session state and nothing from the tracking input. Opening the Editor with these set
    /// starts nothing: a session only starts from the Start button, in Play Mode. No avatar asset is changed.
    /// </summary>
    internal static class FTEmulatorSettings
    {
        internal const int DefaultPort = 9000;
        private const string InputKey = "RiryTools.FTEmulator.Input";
        private const string HardwareKey = "RiryTools.FTEmulator.Hardware";
        private const string BehaviorKey = "RiryTools.FTEmulator.Behavior";
        private const string BackendKey = "RiryTools.FTEmulator.Backend";
        private const string PortKey = "RiryTools.FTEmulator.Port";
        private const string DiscoveryKey = "RiryTools.FTEmulator.Discovery";
        private const string ManualOutputKey = "RiryTools.FTEmulator.ManualOutput";
        private const string CustomMappingsKey = "RiryTools.FTEmulator.CustomMappings";

        internal static FTEmulatorInput Input
        {
            get { return EditorUserSettings.GetConfigValue(InputKey) == FTEmulatorInput.Manual.ToString() ? FTEmulatorInput.Manual : FTEmulatorInput.VrcftOsc; }
            set { EditorUserSettings.SetConfigValue(InputKey, value.ToString()); }
        }

        /// <summary>A hardware profile's id; unset means the unrestricted profile.</summary>
        internal static string Hardware
        {
            get
            {
                string id = EditorUserSettings.GetConfigValue(HardwareKey);
                return string.IsNullOrEmpty(id) ? FTEmulatorCatalog.UnrestrictedId : id;
            }
            set { EditorUserSettings.SetConfigValue(HardwareKey, value); }
        }

        internal static FTEmulatorBehavior Behavior
        {
            get
            {
                return EditorUserSettings.GetConfigValue(BehaviorKey) == FTEmulatorBehavior.ModuleBehavior.ToString()
                    ? FTEmulatorBehavior.ModuleBehavior : FTEmulatorBehavior.Strict;
            }
            set { EditorUserSettings.SetConfigValue(BehaviorKey, value.ToString()); }
        }

        /// <summary>FTEmulatorTemplateResolver.Auto, NativeChoice or an avatar profile's id.</summary>
        internal static string Backend
        {
            get
            {
                string choice = EditorUserSettings.GetConfigValue(BackendKey);
                return string.IsNullOrEmpty(choice) ? FTEmulatorTemplateResolver.Auto : choice;
            }
            set { EditorUserSettings.SetConfigValue(BackendKey, value); }
        }

        /// <summary>The UDP port the receiver binds on 127.0.0.1: the port VRCFaceTracking sends to.</summary>
        internal static int Port
        {
            get
            {
                return int.TryParse(EditorUserSettings.GetConfigValue(PortKey), NumberStyles.Integer, CultureInfo.InvariantCulture, out int port) && port > 0 && port <= 65535
                    ? port : DefaultPort;
            }
            set { EditorUserSettings.SetConfigValue(PortKey, Math.Min(Math.Max(value, 1), 65535).ToString(CultureInfo.InvariantCulture)); }
        }

        internal static bool Discovery
        {
            get { return EditorUserSettings.GetConfigValue(DiscoveryKey) != "0"; }
            set { EditorUserSettings.SetConfigValue(DiscoveryKey, value ? "1" : "0"); }
        }

        internal static bool ManualOutput
        {
            get { return EditorUserSettings.GetConfigValue(ManualOutputKey) != "0"; }
            set { EditorUserSettings.SetConfigValue(ManualOutputKey, value ? "1" : "0"); }
        }

        /// <summary>The user's own mappings: an exact parameter name and a formula of tracking keys. Kept as a JSON object.</summary>
        internal static Dictionary<string, string> CustomMappings
        {
            get
            {
                var result = new Dictionary<string, string>(StringComparer.Ordinal);
                string text = EditorUserSettings.GetConfigValue(CustomMappingsKey);
                if (string.IsNullOrEmpty(text) || !FTEmulatorJson.TryParse(text, out object value, out string _)) return result;
                if (!(value is Dictionary<string, object> map)) return result;
                foreach (KeyValuePair<string, object> pair in map)
                {
                    if (pair.Value is string formula) result[pair.Key] = formula;
                }
                return result;
            }
            set
            {
                var map = new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (KeyValuePair<string, string> pair in value) map[pair.Key] = pair.Value;
                EditorUserSettings.SetConfigValue(CustomMappingsKey, FTEmulatorJson.Write(map));
            }
        }

        /// <summary>The preferences as a session's options.</summary>
        internal static FTEmulatorSessionOptions Capture()
        {
            return new FTEmulatorSessionOptions
            {
                Input = Input, HardwareId = Hardware, Behavior = Behavior, Backend = Backend, Port = Port, Discovery = Discovery,
                ManualOutput = ManualOutput, CustomMappings = CustomMappings,
            };
        }
    }
}

// ============================================================================
// GLOSSARY: FTEmulator settings (EditorUserSettings: per user and per project)
// ============================================================================
//
// FTEmulatorSettings (this file)
//   Input                    RiryTools.FTEmulator.Input: "VrcftOsc" (default) or "Manual".
//   Hardware                 RiryTools.FTEmulator.Hardware: a hardware profile's id (default "unrestricted").
//   Behavior                 RiryTools.FTEmulator.Behavior: "Strict" (default) or "ModuleBehavior".
//   Backend                  RiryTools.FTEmulator.Backend: "auto" (default), "native" or an avatar profile's id.
//   Port                     RiryTools.FTEmulator.Port: the receive port (default DefaultPort, 9000).
//   Discovery                RiryTools.FTEmulator.Discovery: "1" (default) or "0": the OSCQuery service.
//   ManualOutput             RiryTools.FTEmulator.ManualOutput: "1" (default) or "0": the Manual input writes to the avatar.
//   CustomMappings           RiryTools.FTEmulator.CustomMappings: a JSON object of parameter name to formula.
//   Capture()                All of them as FTEmulatorSessionOptions.
