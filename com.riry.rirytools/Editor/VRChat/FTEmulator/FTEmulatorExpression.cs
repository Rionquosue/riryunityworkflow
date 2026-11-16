using System;
using System.Collections.Generic;
using System.Globalization;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// The math language of VRChat's native face tracking Parameter Behaviors (Custom combine mode), compiled to a short
    /// instruction list and evaluated on a small stack: numbers, names, + - * /, unary minus, parentheses and abs(), min(),
    /// max(), clamp(). The same language writes the formulas in FTEmulator's profile files. Nothing is compiled to C# and
    /// nothing is called by reflection; a name is only ever an index into the value array given to Evaluate.
    /// The limits are the ones the SDK's inspector states for the client (FaceTrackingExpressionValidator in SDK 3.10.6).
    /// A division by zero and a result that is not finite give 0: the client's own rule is not documented.
    /// </summary>
    internal sealed class FTEmulatorExpression
    {
        internal const int MaxLength = 1024;
        internal const int MaxTokens = 256;
        internal const int MaxInstructions = 256;
        internal const int MaxNesting = 32;
        internal const int MaxStack = 32;
        internal const int MaxLiteralLength = 20;
        internal const float MaxLiteral = 10000f;

        private enum Op : byte { Constant, Variable, Negate, Add, Subtract, Multiply, Divide, Abs, Min, Max, Clamp }

        private struct Instruction
        {
            internal Op Op;
            internal float Constant;
            internal int Index;
        }

        private readonly Instruction[] program;
        // Evaluate runs on one thread at a time (the main thread): the stack is kept, so evaluating allocates nothing.
        private readonly float[] stack;

        internal readonly string Text;
        /// <summary>The value indices the expression reads, each once, ascending.</summary>
        internal readonly int[] Dependencies;

        internal int InstructionCount
        {
            get { return program.Length; }
        }

        private FTEmulatorExpression(string text, Instruction[] program, int depth, int[] dependencies)
        {
            Text = text;
            this.program = program;
            stack = new float[Math.Max(1, depth)];
            Dependencies = dependencies;
        }

        /// <summary>
        /// Compiles text. variable gives a name's index in the values Evaluate will be given, or below 0 for a name it
        /// does not know. Returns null and says why when the text is not a valid expression or is past a limit.
        /// </summary>
        internal static FTEmulatorExpression Compile(string text, Func<string, int> variable, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(text)) { error = "the expression is empty"; return null; }
            if (text.Length > MaxLength) { error = "the expression is " + text.Length + " characters long (max " + MaxLength + ")"; return null; }
            var parser = new Parser(text, variable);
            try
            {
                parser.Expression(0);
                if (parser.Kind != Token.End) throw new FormatException("unexpected '" + parser.Current + "' at position " + (parser.Start + 1));
            }
            catch (FormatException exception)
            {
                error = exception.Message;
                return null;
            }
            if (parser.Program.Count > MaxInstructions)
            {
                error = "the expression is too complex (" + parser.Program.Count + " instructions, max " + MaxInstructions + ")";
                return null;
            }
            int depth = 0, most = 0;
            foreach (Instruction instruction in parser.Program)
            {
                depth += instruction.Op == Op.Constant || instruction.Op == Op.Variable ? 1
                    : instruction.Op == Op.Negate || instruction.Op == Op.Abs ? 0
                    : instruction.Op == Op.Clamp ? -2 : -1;
                most = Math.Max(most, depth);
            }
            if (most > MaxStack) { error = "the expression needs an evaluation stack " + most + " deep (max " + MaxStack + ")"; return null; }
            var dependencies = new List<int>(parser.Used);
            dependencies.Sort();
            return new FTEmulatorExpression(text, parser.Program.ToArray(), most, dependencies.ToArray());
        }

        /// <summary>The expression's value over values. An index past the end of values reads 0.</summary>
        internal float Evaluate(float[] values)
        {
            int top = 0;
            foreach (Instruction instruction in program)
            {
                switch (instruction.Op)
                {
                    case Op.Constant: stack[top++] = instruction.Constant; break;
                    case Op.Variable: stack[top++] = instruction.Index < values.Length ? values[instruction.Index] : 0f; break;
                    case Op.Negate: stack[top - 1] = -stack[top - 1]; break;
                    case Op.Abs: stack[top - 1] = Math.Abs(stack[top - 1]); break;
                    case Op.Add: top--; stack[top - 1] += stack[top]; break;
                    case Op.Subtract: top--; stack[top - 1] -= stack[top]; break;
                    case Op.Multiply: top--; stack[top - 1] *= stack[top]; break;
                    case Op.Divide: top--; stack[top - 1] = stack[top] == 0f ? 0f : stack[top - 1] / stack[top]; break;
                    case Op.Min: top--; stack[top - 1] = Math.Min(stack[top - 1], stack[top]); break;
                    case Op.Max: top--; stack[top - 1] = Math.Max(stack[top - 1], stack[top]); break;
                    case Op.Clamp:
                        top -= 2;
                        stack[top - 1] = Math.Min(Math.Max(stack[top - 1], stack[top]), stack[top + 1]);
                        break;
                }
            }
            float result = stack[0];
            return float.IsNaN(result) || float.IsInfinity(result) ? 0f : result;
        }

        private enum Token { End, Number, Name, Open, Close, Comma, Plus, Minus, Star, Slash }

        /// <summary>Recursive descent, emitting instructions in evaluation order.</summary>
        private sealed class Parser
        {
            private readonly string text;
            private readonly Func<string, int> variable;
            private int at;
            private int tokens;

            internal readonly List<Instruction> Program = new List<Instruction>();
            internal readonly HashSet<int> Used = new HashSet<int>();
            internal Token Kind;
            internal string Current = "";
            internal int Start;

            internal Parser(string text, Func<string, int> variable)
            {
                this.text = text;
                this.variable = variable;
                Next();
            }

            private void Next()
            {
                while (at < text.Length && char.IsWhiteSpace(text[at])) at++;
                Start = at;
                if (at >= text.Length) { Kind = Token.End; Current = "end"; return; }
                if (++tokens > MaxTokens) throw new FormatException("the expression has more than " + MaxTokens + " tokens");
                char c = text[at];
                if (char.IsDigit(c) || c == '.' && at + 1 < text.Length && char.IsDigit(text[at + 1]))
                {
                    bool point = false;
                    while (at < text.Length && (char.IsDigit(text[at]) || text[at] == '.' && !point))
                    {
                        if (text[at] == '.') point = true;
                        at++;
                    }
                    Kind = Token.Number;
                }
                else if (char.IsLetter(c) || c == '_')
                {
                    while (at < text.Length && (char.IsLetterOrDigit(text[at]) || text[at] == '_')) at++;
                    Kind = Token.Name;
                }
                else
                {
                    at++;
                    switch (c)
                    {
                        case '(': Kind = Token.Open; break;
                        case ')': Kind = Token.Close; break;
                        case ',': Kind = Token.Comma; break;
                        case '+': Kind = Token.Plus; break;
                        case '-': Kind = Token.Minus; break;
                        case '*': Kind = Token.Star; break;
                        case '/': Kind = Token.Slash; break;
                        default: throw new FormatException("unexpected character '" + c + "' at position " + at);
                    }
                }
                Current = text.Substring(Start, at - Start);
            }

            private void Emit(Op op, float constant = 0f, int index = 0)
            {
                Program.Add(new Instruction { Op = op, Constant = constant, Index = index });
            }

            internal void Expression(int nesting)
            {
                Term(nesting);
                while (Kind == Token.Plus || Kind == Token.Minus)
                {
                    Op op = Kind == Token.Plus ? Op.Add : Op.Subtract;
                    Next();
                    Term(nesting);
                    Emit(op);
                }
            }

            private void Term(int nesting)
            {
                Unary(nesting);
                while (Kind == Token.Star || Kind == Token.Slash)
                {
                    Op op = Kind == Token.Star ? Op.Multiply : Op.Divide;
                    Next();
                    Unary(nesting);
                    Emit(op);
                }
            }

            // Unary minus binds tighter than any binary operator. Repeated minus signs are counted, not recursed into.
            private void Unary(int nesting)
            {
                int negations = 0;
                while (Kind == Token.Minus)
                {
                    negations++;
                    Next();
                }
                Primary(nesting);
                for (int i = 0; i < negations; i++) Emit(Op.Negate);
            }

            private void Primary(int nesting)
            {
                if (Kind == Token.Number)
                {
                    if (Current.Length > MaxLiteralLength) throw new FormatException("the number '" + Current + "' at position " + (Start + 1) + " is too long");
                    if (!float.TryParse(Current, NumberStyles.Float, CultureInfo.InvariantCulture, out float number) ||
                        float.IsNaN(number) || float.IsInfinity(number) || Math.Abs(number) > MaxLiteral)
                        throw new FormatException("the number '" + Current + "' at position " + (Start + 1) + " is outside the supported range");
                    Emit(Op.Constant, number);
                    Next();
                    return;
                }
                if (Kind == Token.Name)
                {
                    string name = Current;
                    int position = Start + 1;
                    Next();
                    if (Kind != Token.Open)
                    {
                        int index = variable != null ? variable(name) : -1;
                        if (index < 0) throw new FormatException("unknown name '" + name + "' at position " + position);
                        Used.Add(index);
                        Emit(Op.Variable, 0f, index);
                        return;
                    }
                    Op function;
                    int arguments;
                    switch (name)
                    {
                        case "abs": function = Op.Abs; arguments = 1; break;
                        case "min": function = Op.Min; arguments = 2; break;
                        case "max": function = Op.Max; arguments = 2; break;
                        case "clamp": function = Op.Clamp; arguments = 3; break;
                        default: throw new FormatException("unknown function '" + name + "' at position " + position + " (supported: abs, min, max, clamp)");
                    }
                    if (nesting >= MaxNesting) throw new FormatException("the expression nests more than " + MaxNesting + " levels deep");
                    Next();
                    int given = 0;
                    while (true)
                    {
                        Expression(nesting + 1);
                        given++;
                        if (Kind != Token.Comma) break;
                        Next();
                    }
                    if (Kind != Token.Close) throw new FormatException("')' is expected at position " + (Start + 1));
                    if (given != arguments) throw new FormatException(name + "() takes " + arguments + " argument" + (arguments == 1 ? "" : "s") + " but got " + given);
                    Next();
                    Emit(function);
                    return;
                }
                if (Kind == Token.Open)
                {
                    if (nesting >= MaxNesting) throw new FormatException("the expression nests more than " + MaxNesting + " levels deep");
                    Next();
                    Expression(nesting + 1);
                    if (Kind != Token.Close) throw new FormatException("')' is expected at position " + (Start + 1));
                    Next();
                    return;
                }
                throw new FormatException(Kind == Token.End ? "the expression ends without a final operand"
                    : "unexpected '" + Current + "' at position " + (Start + 1) + ": an operand is expected there");
            }
        }
    }
}

// ============================================================================
// GLOSSARY: FTEmulator expressions
// ============================================================================
//
// FTEmulatorExpression (this file)
//   Compile(text, variable, out error)  The compiled expression, or null with the reason. variable maps a name to its index
//                            in the values (below 0: unknown). Names are case-sensitive.
//   Evaluate(values)         The value over a float array; no allocation. Divide by zero and non-finite results give 0.
//   Text / Dependencies / InstructionCount  The source text, the value indices read, and the number of instructions.
//   Limits                   MaxLength 1024 characters, MaxTokens 256, MaxInstructions 256, MaxNesting 32 levels of
//                            parentheses, MaxStack 32, MaxLiteralLength 20 characters, MaxLiteral 10000.
//   Grammar                  expression: term (+|- term)*; term: unary (*|/ unary)*; unary: - unary | primary;
//                            primary: number | name | function(arguments) | (expression). Functions: abs(1), min(2), max(2),
//                            clamp(3: value, low, high).
