using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// FTEmulator's JSON reader and writer: profile data files, OSCQuery replies, and the HOST_INFO a tracking app
    /// answers with. Contained here so no JSON library has to be in the project, and bounded (depth and size) because
    /// replies come from another program. Objects are Dictionary&lt;string, object&gt;, arrays List&lt;object&gt;,
    /// numbers double, then string, bool and null.
    /// </summary>
    internal static class FTEmulatorJson
    {
        internal const int MaxDepth = 64;
        internal const int MaxLength = 4 * 1024 * 1024;

        internal static object Parse(string text)
        {
            if (text == null) throw new FormatException("JSON text is missing.");
            if (text.Length > MaxLength) throw new FormatException("JSON text is longer than " + MaxLength + " characters.");
            int at = 0;
            object value = Value(text, ref at, 0);
            Skip(text, ref at);
            if (at != text.Length) throw Problem("unexpected text after the value", at);
            return value;
        }

        internal static bool TryParse(string text, out object value, out string error)
        {
            try
            {
                value = Parse(text);
                error = null;
                return true;
            }
            catch (FormatException exception)
            {
                value = null;
                error = exception.Message;
                return false;
            }
        }

        private static FormatException Problem(string what, int at)
        {
            return new FormatException("JSON: " + what + " at character " + (at + 1) + ".");
        }

        private static void Skip(string text, ref int at)
        {
            while (at < text.Length && (text[at] == ' ' || text[at] == '\t' || text[at] == '\r' || text[at] == '\n' || text[at] == '﻿')) at++;
        }

        private static object Value(string text, ref int at, int depth)
        {
            if (depth > MaxDepth) throw Problem("nested deeper than " + MaxDepth, at);
            Skip(text, ref at);
            if (at >= text.Length) throw Problem("the text ends where a value is expected", at);
            char c = text[at];
            if (c == '{')
            {
                var result = new Dictionary<string, object>(StringComparer.Ordinal);
                at++;
                Skip(text, ref at);
                if (at < text.Length && text[at] == '}') { at++; return result; }
                while (true)
                {
                    Skip(text, ref at);
                    if (at >= text.Length || text[at] != '"') throw Problem("a property name is expected", at);
                    string name = Text(text, ref at);
                    Skip(text, ref at);
                    if (at >= text.Length || text[at] != ':') throw Problem("':' is expected", at);
                    at++;
                    result[name] = Value(text, ref at, depth + 1);
                    Skip(text, ref at);
                    if (at < text.Length && text[at] == ',') { at++; continue; }
                    if (at < text.Length && text[at] == '}') { at++; return result; }
                    throw Problem("',' or '}' is expected", at);
                }
            }
            if (c == '[')
            {
                var result = new List<object>();
                at++;
                Skip(text, ref at);
                if (at < text.Length && text[at] == ']') { at++; return result; }
                while (true)
                {
                    result.Add(Value(text, ref at, depth + 1));
                    Skip(text, ref at);
                    if (at < text.Length && text[at] == ',') { at++; continue; }
                    if (at < text.Length && text[at] == ']') { at++; return result; }
                    throw Problem("',' or ']' is expected", at);
                }
            }
            if (c == '"') return Text(text, ref at);
            if (Word(text, ref at, "true")) return true;
            if (Word(text, ref at, "false")) return false;
            if (Word(text, ref at, "null")) return null;
            int start = at;
            while (at < text.Length && (char.IsDigit(text[at]) || text[at] == '-' || text[at] == '+' || text[at] == '.' || text[at] == 'e' || text[at] == 'E')) at++;
            if (at == start) throw Problem("unexpected character '" + c + "'", at);
            if (!double.TryParse(text.Substring(start, at - start), NumberStyles.Float, CultureInfo.InvariantCulture, out double number) ||
                double.IsNaN(number) || double.IsInfinity(number))
                throw Problem("not a number", start);
            return number;
        }

        private static bool Word(string text, ref int at, string word)
        {
            if (string.CompareOrdinal(text, at, word, 0, word.Length) != 0) return false;
            at += word.Length;
            return true;
        }

        private static string Text(string text, ref int at)
        {
            var result = new StringBuilder();
            at++;
            while (true)
            {
                if (at >= text.Length) throw Problem("a string is not closed", at);
                char c = text[at++];
                if (c == '"') return result.ToString();
                if (c != '\\') { result.Append(c); continue; }
                if (at >= text.Length) throw Problem("a string is not closed", at);
                char escape = text[at++];
                switch (escape)
                {
                    case '"': result.Append('"'); break;
                    case '\\': result.Append('\\'); break;
                    case '/': result.Append('/'); break;
                    case 'b': result.Append('\b'); break;
                    case 'f': result.Append('\f'); break;
                    case 'n': result.Append('\n'); break;
                    case 'r': result.Append('\r'); break;
                    case 't': result.Append('\t'); break;
                    case 'u':
                        if (at + 4 > text.Length || !int.TryParse(text.Substring(at, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code))
                            throw Problem("a \\u escape needs four hex digits", at);
                        result.Append((char)code);
                        at += 4;
                        break;
                    default: throw Problem("unknown escape '\\" + escape + "'", at - 1);
                }
            }
        }

        /// <summary>Compact JSON for dictionaries with string keys, lists and arrays, strings, bools, numbers and null.</summary>
        internal static string Write(object value)
        {
            var result = new StringBuilder();
            Write(result, value, 0);
            return result.ToString();
        }

        private static void Write(StringBuilder result, object value, int depth)
        {
            if (depth > MaxDepth) throw new FormatException("JSON: nested deeper than " + MaxDepth + ".");
            switch (value)
            {
                case null: result.Append("null"); return;
                case string text: Quote(result, text); return;
                case bool flag: result.Append(flag ? "true" : "false"); return;
                case float single: result.Append(Finite(single).ToString("R", CultureInfo.InvariantCulture)); return;
                case double number: result.Append(Finite(number).ToString("R", CultureInfo.InvariantCulture)); return;
                case int whole: result.Append(whole.ToString(CultureInfo.InvariantCulture)); return;
                case long large: result.Append(large.ToString(CultureInfo.InvariantCulture)); return;
                case IDictionary<string, object> map:
                {
                    result.Append('{');
                    bool first = true;
                    foreach (KeyValuePair<string, object> pair in map)
                    {
                        if (!first) result.Append(',');
                        first = false;
                        Quote(result, pair.Key);
                        result.Append(':');
                        Write(result, pair.Value, depth + 1);
                    }
                    result.Append('}');
                    return;
                }
                case IEnumerable list:
                {
                    result.Append('[');
                    bool first = true;
                    foreach (object item in list)
                    {
                        if (!first) result.Append(',');
                        first = false;
                        Write(result, item, depth + 1);
                    }
                    result.Append(']');
                    return;
                }
                default: throw new FormatException("JSON: cannot write a " + value.GetType().Name + ".");
            }
        }

        private static double Finite(double number)
        {
            return double.IsNaN(number) || double.IsInfinity(number) ? 0d : number;
        }

        private static void Quote(StringBuilder result, string text)
        {
            result.Append('"');
            foreach (char c in text)
            {
                switch (c)
                {
                    case '"': result.Append("\\\""); break;
                    case '\\': result.Append("\\\\"); break;
                    case '\n': result.Append("\\n"); break;
                    case '\r': result.Append("\\r"); break;
                    case '\t': result.Append("\\t"); break;
                    default:
                        if (c < ' ') result.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else result.Append(c);
                        break;
                }
            }
            result.Append('"');
        }

        // ---- Reading a parsed object: each says what is wrong, by property name ----

        internal static Dictionary<string, object> Object(object value, string what)
        {
            return value as Dictionary<string, object> ?? throw new FormatException(what + " must be an object.");
        }

        internal static List<object> List(Dictionary<string, object> from, string name)
        {
            if (!from.TryGetValue(name, out object value) || value == null) return new List<object>();
            return value as List<object> ?? throw new FormatException("\"" + name + "\" must be a list.");
        }

        internal static string Text(Dictionary<string, object> from, string name, string fallback = null)
        {
            if (!from.TryGetValue(name, out object value) || value == null) return fallback;
            return value as string ?? throw new FormatException("\"" + name + "\" must be text.");
        }

        internal static double Number(Dictionary<string, object> from, string name, double fallback)
        {
            if (!from.TryGetValue(name, out object value) || value == null) return fallback;
            return value is double number ? number : throw new FormatException("\"" + name + "\" must be a number.");
        }

        internal static bool Flag(Dictionary<string, object> from, string name, bool fallback = false)
        {
            if (!from.TryGetValue(name, out object value) || value == null) return fallback;
            return value is bool flag ? flag : throw new FormatException("\"" + name + "\" must be true or false.");
        }

        internal static string[] Texts(Dictionary<string, object> from, string name)
        {
            List<object> list = List(from, name);
            var result = new string[list.Count];
            for (int i = 0; i < list.Count; i++)
                result[i] = list[i] as string ?? throw new FormatException("\"" + name + "\" must list text only.");
            return result;
        }
    }
}

// ============================================================================
// GLOSSARY: FTEmulator JSON
// ============================================================================
//
// FTEmulatorJson (this file)
//   Parse(text) / TryParse   A JSON value: Dictionary<string, object>, List<object>, string, double, bool or null. FormatException
//                            names the character. Bounded by MaxDepth (64 levels) and MaxLength (4 M characters).
//   Write(value)             Compact JSON of dictionaries, lists, strings, bools, numbers and null; a number that is not finite
//                            is written as 0.
//   Object / List / Text / Number / Flag / Texts  Typed reads of a parsed object's properties, each failing with the property's
//                            name; a missing property gives the fallback (an empty list for List and Texts).
