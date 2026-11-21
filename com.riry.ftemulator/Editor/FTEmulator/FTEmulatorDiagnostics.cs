using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// FTEmulator's bounded diagnostics: problems grouped by code with a count (a thousand malformed packets are one
    /// line, not a thousand), and named counters and readings. Safe to call from the receiver's threads. It never holds
    /// images or packet contents. The first time a code is recorded FirstSeen is called, so each problem reaches the
    /// Console once.
    /// </summary>
    internal sealed class FTEmulatorDiagnostics
    {
        internal const int MaxCodes = 128;

        // Codes. A code is a category: the context says which packet, parameter or port.
        internal const string PacketRejected = "Malformed OSC packet";
        internal const string NativeBlobRejected = "Native face message ignored";
        internal const string AddressLimit = "Too many OSC addresses";
        internal const string PortInUse = "Receive port in use";
        internal const string ReceiverFault = "Receiver stopped";
        internal const string QueryFault = "OSCQuery service";
        internal const string DiscoveryFault = "Discovery";
        internal const string OtherClient = "Another VRChat client is advertised";
        internal const string StaleGeneration = "Input from an earlier session dropped";
        internal const string MappingFailure = "Mapping";
        internal const string ProfileBlock = "Blocked by the hardware profile";
        internal const string OwnershipConflict = "Output not taken";
        internal const string TypeConflict = "Parameter type conflict";
        internal const string NativeUnsupported = "Native feature not emulated";

        internal sealed class Item
        {
            internal string Code;
            internal long Count;
            internal string First;
            internal string Last;
            internal double LastTime;
        }

        private readonly object gate = new object();
        private readonly Dictionary<string, Item> items = new Dictionary<string, Item>(StringComparer.Ordinal);
        private readonly List<Item> ordered = new List<Item>();
        private readonly Dictionary<string, long> counters = new Dictionary<string, long>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> readings = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>Called once per code, on whichever thread recorded it first: (code, context).</summary>
        internal Action<string, string> FirstSeen;

        /// <summary>Counts a problem under its code. context says what it concerns, without packet contents.</summary>
        internal void Record(string code, string context)
        {
            bool first = false;
            lock (gate)
            {
                if (!items.TryGetValue(code, out Item item))
                {
                    if (items.Count >= MaxCodes) return;
                    item = new Item { Code = code, First = context };
                    items[code] = item;
                    ordered.Add(item);
                    first = true;
                }
                item.Count++;
                item.Last = context;
                item.LastTime = FTEmulatorClock.Now;
            }
            if (first) FirstSeen?.Invoke(code, context);
        }

        internal void Add(string counter, long amount = 1)
        {
            lock (gate)
            {
                counters.TryGetValue(counter, out long value);
                counters[counter] = value + amount;
            }
        }

        internal void Set(string reading, string value)
        {
            lock (gate) readings[reading] = value;
        }

        internal long Counter(string counter)
        {
            lock (gate) return counters.TryGetValue(counter, out long value) ? value : 0;
        }

        internal long Count(string code)
        {
            lock (gate) return items.TryGetValue(code, out Item item) ? item.Count : 0;
        }

        /// <summary>A copy of the recorded problems, oldest first.</summary>
        internal List<Item> Snapshot()
        {
            var result = new List<Item>();
            lock (gate)
            {
                foreach (Item item in ordered)
                    result.Add(new Item { Code = item.Code, Count = item.Count, First = item.First, Last = item.Last, LastTime = item.LastTime });
            }
            return result;
        }

        /// <summary>Everything as text for Copy diagnostics: the header lines given, then readings, counters and problems.</summary>
        internal string ExportText(IEnumerable<string> header)
        {
            var text = new StringBuilder();
            text.AppendLine("RiryTools FTEmulator diagnostics");
            if (header != null)
            {
                foreach (string line in header) text.AppendLine(line);
            }
            lock (gate)
            {
                if (readings.Count > 0)
                {
                    text.AppendLine().AppendLine("Readings");
                    foreach (KeyValuePair<string, string> pair in Sorted(readings)) text.Append("  ").Append(pair.Key).Append(": ").AppendLine(pair.Value);
                }
                if (counters.Count > 0)
                {
                    text.AppendLine().AppendLine("Counters");
                    foreach (KeyValuePair<string, long> pair in Sorted(counters))
                        text.Append("  ").Append(pair.Key).Append(": ").AppendLine(pair.Value.ToString(CultureInfo.InvariantCulture));
                }
                text.AppendLine().AppendLine(ordered.Count == 0 ? "Problems: none" : "Problems");
                foreach (Item item in ordered)
                {
                    text.Append("  ").Append(item.Code).Append(" x").Append(item.Count.ToString(CultureInfo.InvariantCulture)).Append(": ").AppendLine(item.First);
                    if (item.Count > 1 && item.Last != item.First) text.Append("    latest: ").AppendLine(item.Last);
                }
            }
            return text.ToString();
        }

        private static List<KeyValuePair<string, T>> Sorted<T>(Dictionary<string, T> map)
        {
            var list = new List<KeyValuePair<string, T>>(map);
            list.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            return list;
        }
    }
}

// ============================================================================
// GLOSSARY: FTEmulator diagnostics
// ============================================================================
//
// FTEmulatorDiagnostics (this file)
//   Record(code, context)    Counts a problem under its code (at most MaxCodes, 128, codes); FirstSeen(code, context) is
//                            called the first time a code is recorded.
//   Add(counter, amount) / Counter(counter)  Named counters (packets received, frames dropped, ...).
//   Set(reading, value)      A named reading shown in the export (versions, endpoints).
//   Count(code) / Snapshot() How often a code was recorded; a copy of every Item (Code, Count, First, Last, LastTime).
//   ExportText(header)       Everything as text, without images or packet contents.
//   Codes                    PacketRejected, NativeBlobRejected, AddressLimit, PortInUse, ReceiverFault, QueryFault,
//                            DiscoveryFault, OtherClient, StaleGeneration, MappingFailure, ProfileBlock, OwnershipConflict,
//                            TypeConflict, NativeUnsupported.
