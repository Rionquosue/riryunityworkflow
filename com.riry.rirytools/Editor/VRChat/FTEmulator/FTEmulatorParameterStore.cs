using System;
using System.Collections.Generic;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// The Live / Held / Manual / Disabled policy of every row the debug window can change: the hardware profile's
    /// controls, and avatar parameters that arrive already mapped. It holds tracking input, not the avatar's pose: animator
    /// smoothing and unrelated animation keep moving.
    /// A value is refused when its row is blocked (the hardware profile does not allow it), so an override cannot get past
    /// the hardware limits.
    /// </summary>
    internal sealed class FTEmulatorParameterStore
    {
        /// <summary>How long a Live row keeps its last value after input stops arriving.</summary>
        internal const double GraceSeconds = 0.25;
        /// <summary>How long it then takes to fade to neutral, after which the row is not driven.</summary>
        internal const double FadeSeconds = 0.2;
        internal const string ControlPrefix = "control:";
        internal const string ParameterPrefix = "parameter:";

        internal sealed class Entry
        {
            internal string Key;
            internal FTEmulatorValueType Type;
            internal float Min;
            internal float Max = 1f;
            internal float Neutral;
            /// <summary>False for values a source sends only when they change: the last one stays until the next.</summary>
            internal bool Expires = true;
            internal FTEmulatorRowMode Mode;
            internal bool HasLive;
            internal float Live;
            internal double LiveTime;
            internal bool HasHeld;
            internal float Held;
            internal float Manual;
            /// <summary>Why the row may not be driven; null when it may.</summary>
            internal string Blocked;
            /// <summary>After a resume: keep the held value until a new sample arrives, so nothing old is replayed.</summary>
            internal bool AwaitingFresh;
            internal bool HasEffective;
            internal float Effective;
        }

        private readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private readonly List<Entry> ordered = new List<Entry>();

        /// <summary>Freeze output: every row keeps the value it had; Live values go on being recorded.</summary>
        internal bool Frozen { get; private set; }
        /// <summary>The source was stopped (Active listening off): Live rows keep the value they had.</summary>
        internal bool SourceStopped { get; private set; }

        internal IReadOnlyList<Entry> Entries
        {
            get { return ordered; }
        }

        internal Entry Find(string key)
        {
            return key != null && entries.TryGetValue(key, out Entry entry) ? entry : null;
        }

        /// <summary>The row for key, made when it is new. An existing row keeps its mode and values and takes the new range.</summary>
        internal Entry Ensure(string key, FTEmulatorValueType type, float min, float max, float neutral, bool expires = true)
        {
            if (!entries.TryGetValue(key, out Entry entry))
            {
                entry = new Entry { Key = key };
                entries[key] = entry;
                ordered.Add(entry);
            }
            entry.Type = type;
            entry.Min = min;
            entry.Max = max;
            entry.Neutral = neutral;
            entry.Expires = expires;
            return entry;
        }

        /// <summary>
        /// Marks a row as blocked (reason) or allowed (null). A blocked row loses its manual and held value at once and
        /// cannot be given one.
        /// </summary>
        internal void SetBlocked(string key, string reason)
        {
            Entry entry = Find(key);
            if (entry == null) return;
            entry.Blocked = reason;
            if (reason == null) return;
            if (entry.Mode == FTEmulatorRowMode.Manual || entry.Mode == FTEmulatorRowMode.Held) entry.Mode = FTEmulatorRowMode.Live;
            entry.HasHeld = false;
            entry.HasLive = false;
            entry.AwaitingFresh = false;
        }

        /// <summary>Records the source's value for a row.</summary>
        internal void SetLive(string key, float value, double now)
        {
            Entry entry = Find(key);
            if (entry == null || float.IsNaN(value) || float.IsInfinity(value)) return;
            entry.Live = Clamp(entry, value);
            entry.LiveTime = now;
            entry.HasLive = true;
            // While frozen the row keeps showing what it held; the fresh sample is used once output resumes.
            if (!Frozen) entry.AwaitingFresh = false;
        }

        /// <summary>False when the row is unknown, or blocked and asked to be Manual or Held.</summary>
        internal bool SetMode(string key, FTEmulatorRowMode mode, double now)
        {
            Entry entry = Find(key);
            if (entry == null) return false;
            if (entry.Blocked != null && (mode == FTEmulatorRowMode.Manual || mode == FTEmulatorRowMode.Held)) return false;
            if (mode == FTEmulatorRowMode.Held)
            {
                entry.HasHeld = TryEffective(entry, now, out float held);
                entry.Held = held;
            }
            else if (mode == FTEmulatorRowMode.Manual && entry.Mode != FTEmulatorRowMode.Manual)
            {
                // Start from what the row shows now, so switching to Manual does not jump.
                entry.Manual = TryEffective(entry, now, out float current) ? current : entry.Neutral;
            }
            entry.Mode = mode;
            return true;
        }

        /// <summary>Gives a row a typed value and makes it Manual. False when the row is unknown or blocked.</summary>
        internal bool SetManual(string key, float value)
        {
            Entry entry = Find(key);
            if (entry == null || entry.Blocked != null || float.IsNaN(value) || float.IsInfinity(value)) return false;
            entry.Manual = Clamp(entry, value);
            entry.Mode = FTEmulatorRowMode.Manual;
            return true;
        }

        private static float Clamp(Entry entry, float value)
        {
            if (entry.Type == FTEmulatorValueType.Bool) return value != 0f ? 1f : 0f;
            if (entry.Type == FTEmulatorValueType.Int) return (float)Math.Round(value);
            return Math.Min(Math.Max(value, entry.Min), entry.Max);
        }

        /// <summary>Freeze output on or off. Turning it on keeps every row's current value.</summary>
        internal void SetFrozen(bool frozen, double now)
        {
            if (frozen == Frozen) return;
            if (frozen && !SourceStopped) Snapshot(now);
            Frozen = frozen;
            if (!frozen && !SourceStopped) AwaitFresh();
        }

        /// <summary>The source was stopped (Active listening off) or runs again. Stopped: Live rows keep their current value.</summary>
        internal void SetSourceStopped(bool stopped, double now)
        {
            if (stopped == SourceStopped) return;
            if (stopped && !Frozen) Snapshot(now);
            SourceStopped = stopped;
            if (!stopped && !Frozen) AwaitFresh();
        }

        /// <summary>Freeze output: every row keeps its value; Live values are still recorded.</summary>
        internal void FreezeAll(double now)
        {
            SetFrozen(true, now);
        }

        /// <summary>
        /// Ends Freeze output and a stopped source. Live rows keep the held value until a new sample arrives; what arrived
        /// meanwhile is not replayed.
        /// </summary>
        internal void ResumeLive(double now)
        {
            bool held = Frozen || SourceStopped;
            Frozen = false;
            SourceStopped = false;
            if (held) AwaitFresh();
        }

        private void Snapshot(double now)
        {
            foreach (Entry entry in ordered)
            {
                if (entry.Mode == FTEmulatorRowMode.Held) continue;
                entry.HasHeld = TryEffective(entry, now, out float held);
                entry.Held = held;
            }
        }

        // Output runs again: the Live values recorded while it was held are dropped, and each Live row keeps its held
        // value until a new sample arrives.
        private void AwaitFresh()
        {
            foreach (Entry entry in ordered)
            {
                entry.HasLive = false;
                entry.AwaitingFresh = entry.HasHeld && entry.Mode == FTEmulatorRowMode.Live;
            }
        }

        /// <summary>Drops Live values that were recorded and not yet used (pause, a source change).</summary>
        internal void ClearLive()
        {
            foreach (Entry entry in ordered) entry.HasLive = false;
        }

        /// <summary>Every row back to Live, with no manual or held value.</summary>
        internal void ResetOverrides()
        {
            foreach (Entry entry in ordered)
            {
                entry.Mode = FTEmulatorRowMode.Live;
                entry.HasHeld = false;
                entry.AwaitingFresh = false;
            }
        }

        /// <summary>Removes the rows keep says no to (a hardware profile change: rows of the old device are dropped).</summary>
        internal void Retain(Func<string, bool> keep)
        {
            for (int i = ordered.Count - 1; i >= 0; i--)
            {
                if (keep(ordered[i].Key)) continue;
                entries.Remove(ordered[i].Key);
                ordered.RemoveAt(i);
            }
        }

        /// <summary>The value the row drives its outputs with now. False when it drives nothing.</summary>
        internal bool TryEffective(string key, double now, out float value)
        {
            Entry entry = Find(key);
            if (entry == null) { value = 0f; return false; }
            bool has = TryEffective(entry, now, out value);
            entry.HasEffective = has;
            entry.Effective = value;
            return has;
        }

        private bool TryEffective(Entry entry, double now, out float value)
        {
            value = entry.Neutral;
            if (entry.Blocked != null || entry.Mode == FTEmulatorRowMode.Disabled) return false;
            if (entry.Mode == FTEmulatorRowMode.Manual) { value = entry.Manual; return true; }
            if (entry.Mode == FTEmulatorRowMode.Held || Frozen || SourceStopped || entry.AwaitingFresh)
            {
                value = entry.Held;
                return entry.HasHeld;
            }
            if (!entry.HasLive) return false;
            double age = now - entry.LiveTime;
            if (!entry.Expires || age <= GraceSeconds) { value = entry.Live; return true; }
            if (age >= GraceSeconds + FadeSeconds) return false;
            // Bool and Int do not fade: they keep their value to the end of the fade, then stop being driven.
            if (entry.Type != FTEmulatorValueType.Float) { value = entry.Live; return true; }
            float fade = (float)((age - GraceSeconds) / FadeSeconds);
            value = entry.Live + (entry.Neutral - entry.Live) * fade;
            return true;
        }

        /// <summary>The row's owner as the debug window names it.</summary>
        internal static string Owner(Entry entry, bool frozen, bool sourceStopped)
        {
            if (entry.Blocked != null) return "None";
            switch (entry.Mode)
            {
                case FTEmulatorRowMode.Manual: return "Manual";
                case FTEmulatorRowMode.Disabled: return "Disabled";
                case FTEmulatorRowMode.Held: return "Held";
                default: return frozen || sourceStopped || entry.AwaitingFresh ? "Held" : entry.HasLive ? "Live" : "None";
            }
        }
    }
}

// ============================================================================
// GLOSSARY: FTEmulator parameter store
// ============================================================================
//
// FTEmulatorParameterStore (this file)
//   Entry                    One row: Key ("control:<name>" or "parameter:<name>"), Type, Min, Max, Neutral, Mode, the Live
//                            value and its time, the Held value, the Manual value, Blocked (the reason, or null),
//                            AwaitingFresh, and the last Effective value.
//   Ensure(key, type, min, max, neutral, expires)  The row, made when new. expires false: its Live value never goes stale
//                            (parameters a source sends only when they change).
//   SetBlocked(key, reason)  Blocks or allows a row; a blocked row loses its manual and held value.
//   SetLive(key, value, now) The source's value (kept to the row's range; NaN and infinity are dropped).
//   SetMode(key, mode, now)  Live, Held (keeps the current value), Manual (starts from the current value) or Disabled.
//                            False for an unknown row, or a blocked row asked to be Manual or Held.
//   SetManual(key, value)    A typed value; the row becomes Manual. False for an unknown or blocked row.
//   SetFrozen(frozen, now) / FreezeAll(now)  Freeze output: every row keeps its value; Live values are still recorded.
//   SetSourceStopped(stopped, now)  Active listening off: Live rows keep their value.
//   ResumeLive(now)          Ends both. Live rows keep the held value until a new sample arrives; nothing old is replayed.
//   ClearLive() / ResetOverrides() / Retain(keep)  Drops unread Live values; every row back to Live; removes rows.
//   TryEffective(key, now, out value)  What the row drives with: Manual, then Held, then Live. A Live value older than
//                            GraceSeconds (0.25 s) fades to neutral over FadeSeconds (0.2 s), then the row drives nothing.
//   Owner(entry, ...)        "Live", "Held", "Manual", "Disabled" or "None".
