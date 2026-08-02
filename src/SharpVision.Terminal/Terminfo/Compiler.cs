// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Terminfo;

/// <summary>Compiles the bounded ncurses terminfo parameter language into immutable instructions.</summary>
internal static class Compiler
{
    private const int _flagAlternate = 1 << 0;
    private const int _flagZeroPad = 1 << 1;
    private const int _flagLeft = 1 << 2;
    private const int _flagSign = 1 << 3;
    private const int _flagSpace = 1 << 4;

    #region Compilation

    extension(ReadOnlySpan<byte> source)
    {
        /// <summary>Compiles one raw terminal-description program without executing native tparm or tputs.</summary>
        /// <param name="limits">The non-null finite compilation and execution limits.</param>
        /// <returns>An immutable compile-once program.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="limits"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">The program exceeds a configured bound.</exception>
        /// <exception cref="FormatException">The program is empty, malformed, unsupported, or stack-invalid.</exception>
        /// <exception cref="NotSupportedException">The program requests hardware padding.</exception>
        [SuppressMessage(
            "Design",
            "CA2208:Instantiate argument exceptions correctly",
            Justification = "'source' is the extension receiver, which the analyzer doesn't recognize as a valid ArgumentException paramName yet.")]
        public Program Compile(ProgramLimits limits)
        {
            ArgumentNullException.ThrowIfNull(limits);

            if (source.IsEmpty)
            {
                throw new FormatException("A terminfo parameter program cannot be empty.");
            }

            if (source.Length > limits.MaxProgramBytes)
            {
                throw new ArgumentException("The terminfo program exceeds the configured byte limit.", nameof(source));
            }

            if (source.IndexOf("$<"u8) >= 0)
            {
                throw new NotSupportedException("SharpVision does not execute terminfo padding requests.");
            }

            var operations = new List<Operation>(Math.Min(source.Length, limits.MaxProgramOperations));
            var literals = new List<byte>(source.Length);
            var conditionalBase = new int[limits.MaxProgramStackDepth];
            var conditionalFalseJump = new int[limits.MaxProgramStackDepth];
            var conditionalEndJumps = new List<int>?[limits.MaxProgramStackDepth];
            var conditionalThenDepth = new int[limits.MaxProgramStackDepth];
            var conditionalHasThenDepth = new bool[limits.MaxProgramStackDepth];
            var conditionalState = new byte[limits.MaxProgramStackDepth];
            var conditionalDepth = 0;
            var stackDepth = 0;
            var maximumStackDepth = 0;
            var index = 0;

            while (index < source.Length)
            {
                if (source[index] != (byte) '%')
                {
                    var start = index;

                    while (index < source.Length && source[index] != (byte) '%')
                    {
                        index++;
                    }

                    var literalOffset = literals.Count;

                    for (var literalIndex = start; literalIndex < index; literalIndex++)
                    {
                        literals.Add(source[literalIndex]);
                    }

                    AddOperation(
                        operations,
                        new Operation(Operation.Literal, literalOffset, index - start),
                        limits);
                    continue;
                }

                index++;

                if (index >= source.Length)
                {
                    throw Malformed("A percent introducer is missing its directive.");
                }

                var directive = source[index++];

                switch (directive)
                {
                    case (byte) '%':
                        {
                            var literalOffset = literals.Count;
                            literals.Add((byte) '%');
                            AddOperation(operations, new Operation(Operation.Literal, literalOffset, 1), limits);
                            break;
                        }
                    case (byte) 'p':
                        if (index >= source.Length || source[index] is < (byte) '1' or > (byte) '9')
                        {
                            throw Malformed("A parameter directive requires an index from 1 through 9.");
                        }

                        AddOperation(
                            operations,
                            new Operation(Operation.PushParameter, source[index++] - (byte) '1'),
                            limits);
                        Push(ref stackDepth, ref maximumStackDepth, limits);
                        break;
                    case (byte) 'i':
                        AddOperation(operations, new Operation(Operation.IncrementParameters), limits);
                        break;
                    case (byte) '{':
                        {
                            var value = ParseInteger(source, ref index, (byte) '}');
                            AddOperation(operations, new Operation(Operation.PushConstant, value), limits);
                            Push(ref stackDepth, ref maximumStackDepth, limits);
                            break;
                        }
                    case (byte) '\'':
                        if (index + 1 >= source.Length || source[index + 1] != (byte) '\'')
                        {
                            throw Malformed("A character constant must contain exactly one raw byte.");
                        }

                        AddOperation(
                            operations,
                            new Operation(Operation.PushConstant, source[index]),
                            limits);
                        index += 2;
                        Push(ref stackDepth, ref maximumStackDepth, limits);
                        break;
                    case (byte) 'P':
                        AddOperation(
                            operations,
                            new Operation(Operation.StoreVariable, ParseVariable(source, ref index)),
                            limits);
                        Pop(ref stackDepth);
                        break;
                    case (byte) 'g':
                        AddOperation(
                            operations,
                            new Operation(Operation.LoadVariable, ParseVariable(source, ref index)),
                            limits);
                        Push(ref stackDepth, ref maximumStackDepth, limits);
                        break;
                    case (byte) 'l':
                        Unary(operations, Operation.StringLength, limits, stackDepth);
                        break;
                    case (byte) '+':
                        Binary(operations, Operation.Add, limits, ref stackDepth);
                        break;
                    case (byte) '-':
                        Binary(operations, Operation.Subtract, limits, ref stackDepth);
                        break;
                    case (byte) '*':
                        Binary(operations, Operation.Multiply, limits, ref stackDepth);
                        break;
                    case (byte) '/':
                        Binary(operations, Operation.Divide, limits, ref stackDepth);
                        break;
                    case (byte) 'm':
                        Binary(operations, Operation.Modulo, limits, ref stackDepth);
                        break;
                    case (byte) '&':
                        Binary(operations, Operation.BitAnd, limits, ref stackDepth);
                        break;
                    case (byte) '|':
                        Binary(operations, Operation.BitOr, limits, ref stackDepth);
                        break;
                    case (byte) '^':
                        Binary(operations, Operation.BitXor, limits, ref stackDepth);
                        break;
                    case (byte) '=':
                        Binary(operations, Operation.Equal, limits, ref stackDepth);
                        break;
                    case (byte) '<':
                        Binary(operations, Operation.LessThan, limits, ref stackDepth);
                        break;
                    case (byte) '>':
                        Binary(operations, Operation.GreaterThan, limits, ref stackDepth);
                        break;
                    case (byte) 'A':
                        Binary(operations, Operation.LogicalAnd, limits, ref stackDepth);
                        break;
                    case (byte) 'O':
                        Binary(operations, Operation.LogicalOr, limits, ref stackDepth);
                        break;
                    case (byte) '!':
                        Unary(operations, Operation.LogicalNot, limits, stackDepth);
                        break;
                    case (byte) '~':
                        Unary(operations, Operation.BitNot, limits, stackDepth);
                        break;
                    case (byte) '?':
                        if (conditionalDepth >= conditionalBase.Length)
                        {
                            throw new ArgumentException(
                                "Conditional nesting exceeds the configured stack-depth limit.",
                                nameof(source));
                        }

                        conditionalBase[conditionalDepth] = stackDepth;
                        conditionalState[conditionalDepth] = 0;
                        conditionalHasThenDepth[conditionalDepth] = false;
                        conditionalEndJumps[conditionalDepth] = [];
                        conditionalDepth++;
                        break;
                    case (byte) 't':
                        if (conditionalDepth == 0 ||
                            conditionalState[conditionalDepth - 1] is not (0 or 2))
                        {
                            throw Malformed("A conditional test marker is out of place.");
                        }

                        Pop(ref stackDepth);

                        if (stackDepth != conditionalBase[conditionalDepth - 1])
                        {
                            throw Malformed("A conditional expression must leave exactly one test value.");
                        }

                        conditionalFalseJump[conditionalDepth - 1] = operations.Count;
                        AddOperation(operations, new Operation(Operation.JumpIfFalse, -1), limits);
                        conditionalState[conditionalDepth - 1] = 1;
                        break;
                    case (byte) 'e':
                        if (conditionalDepth == 0 || conditionalState[conditionalDepth - 1] != 1)
                        {
                            throw Malformed("A conditional else marker is out of place.");
                        }

                        var elseIndex = conditionalDepth - 1;

                        if (conditionalHasThenDepth[elseIndex] &&
                            conditionalThenDepth[elseIndex] != stackDepth)
                        {
                            throw Malformed("Conditional branches must leave the same stack depth.");
                        }

                        conditionalThenDepth[elseIndex] = stackDepth;
                        conditionalHasThenDepth[elseIndex] = true;
                        conditionalEndJumps[elseIndex]!.Add(operations.Count);
                        AddOperation(operations, new Operation(Operation.Jump, -1), limits);
                        PatchJump(operations, conditionalFalseJump[elseIndex], operations.Count);
                        stackDepth = conditionalBase[elseIndex];
                        conditionalState[elseIndex] = 2;
                        break;
                    case (byte) ';':
                        if (conditionalDepth == 0 || conditionalState[conditionalDepth - 1] == 0)
                        {
                            throw Malformed("A conditional end marker is out of place.");
                        }

                        var conditionIndex = conditionalDepth - 1;

                        if (conditionalState[conditionIndex] == 2)
                        {
                            if (stackDepth != conditionalThenDepth[conditionIndex])
                            {
                                throw Malformed("Conditional branches must leave the same stack depth.");
                            }

                        }
                        else
                        {
                            if (conditionalHasThenDepth[conditionIndex] &&
                                stackDepth != conditionalThenDepth[conditionIndex])
                            {
                                throw Malformed("Conditional branches must leave the same stack depth.");
                            }

                            if (stackDepth != conditionalBase[conditionIndex])
                            {
                                throw Malformed("A conditional without else must leave its stack unchanged.");
                            }

                            PatchJump(operations, conditionalFalseJump[conditionIndex], operations.Count);
                        }

                        foreach (var jump in conditionalEndJumps[conditionIndex]!)
                        {
                            PatchJump(operations, jump, operations.Count);
                        }

                        conditionalEndJumps[conditionIndex] = null;
                        conditionalDepth--;
                        break;
                    default:
                        CompileFormat(
                            directive,
                            source,
                            ref index,
                            operations,
                            limits,
                            ref stackDepth);
                        break;
                }
            }

            return conditionalDepth != 0
                ? throw Malformed("A conditional is missing its end marker.")
                : new Program(
                source,
                CollectionsMarshal.AsSpan(operations),
                CollectionsMarshal.AsSpan(literals),
                maximumStackDepth);
        }
    }

    #endregion

    #region Directives

    [SuppressMessage(
        "Style",
        "IDE0051:Remove unused private members",
        Justification = "Called only from within extension(...) blocks; the analyzer doesn't track that usage yet.")]
    private static void CompileFormat(
        byte first,
        ReadOnlySpan<byte> source,
        ref int index,
        List<Operation> operations,
        ProgramLimits limits,
        ref int stackDepth)
    {
        var flags = 0;
        var width = -1;
        var precision = -1;
        var directive = first;
        var formatted = first == (byte) ':' || IsFormatPrefix(first);

        if (formatted)
        {
            if (first == (byte) ':')
            {
                if (index >= source.Length)
                {
                    throw Malformed("A printf colon is missing its format.");
                }

                directive = source[index++];
            }

            while (true)
            {
                var flag = directive switch
                {
                    (byte) '#' => _flagAlternate,
                    (byte) '0' => _flagZeroPad,
                    (byte) '-' => _flagLeft,
                    (byte) '+' => _flagSign,
                    (byte) ' ' => _flagSpace,
                    _ => 0
                };

                if (flag == 0)
                {
                    break;
                }

                flags |= flag;

                if (index >= source.Length)
                {
                    throw Malformed("A printf flag is missing its conversion.");
                }

                directive = source[index++];
            }

            if (directive is >= (byte) '0' and <= (byte) '9')
            {
                width = ParseUnsigned(source, ref index, directive);

                if (width > limits.MaxProgramOutputBytes)
                {
                    throw new ArgumentException("A printf width exceeds the configured output-byte limit.");
                }

                directive = NextFormatByte(source, ref index);
            }

            if (directive == (byte) '.')
            {
                var precisionFirst = NextFormatByte(source, ref index);

                if (IsConversion(precisionFirst))
                {
                    precision = 0;
                    directive = precisionFirst;
                }
                else if (precisionFirst is >= (byte) '0' and <= (byte) '9')
                {
                    precision = ParseUnsigned(source, ref index, precisionFirst);

                    if (precision > limits.MaxProgramOutputBytes)
                    {
                        throw new ArgumentException("A printf precision exceeds the configured output-byte limit.");
                    }

                    directive = NextFormatByte(source, ref index);
                }
                else
                {
                    throw Malformed("A printf precision must be followed by digits or a conversion.");
                }
            }
        }

        if (!IsConversion(directive))
        {
            throw Malformed($"Unsupported terminfo directive %{(char) directive}.");
        }

        Pop(ref stackDepth);
        var code = directive switch
        {
            (byte) 'c' => Operation.FormatCharacter,
            (byte) 's' => Operation.FormatString,
            _ => Operation.FormatNumber
        };
        AddOperation(operations, new Operation(code, directive, width, PackFormat(flags, precision)), limits);
    }

    [SuppressMessage(
        "Style",
        "IDE0051:Remove unused private members",
        Justification = "Called only from within extension(...) blocks; the analyzer doesn't track that usage yet.")]
    private static void Binary(
        List<Operation> operations,
        byte code,
        ProgramLimits limits,
        ref int stackDepth)
    {
        Pop(ref stackDepth);
        Pop(ref stackDepth);
        stackDepth++;
        AddOperation(operations, new Operation(code), limits);
    }

    [SuppressMessage(
        "Style",
        "IDE0051:Remove unused private members",
        Justification = "Called only from within extension(...) blocks; the analyzer doesn't track that usage yet.")]
    private static void Unary(
        List<Operation> operations,
        byte code,
        ProgramLimits limits,
        int stackDepth)
    {
        if (stackDepth == 0)
        {
            throw Malformed("A unary operation underflows the evaluation stack.");
        }

        AddOperation(operations, new Operation(code), limits);
    }

    #endregion

    #region Parsing and bounds

    [SuppressMessage(
        "Style",
        "IDE0051:Remove unused private members",
        Justification = "Called only from within extension(...) blocks; the analyzer doesn't track that usage yet.")]
    private static void Push(ref int stackDepth, ref int maximumStackDepth, ProgramLimits limits)
    {
        stackDepth++;

        if (stackDepth > limits.MaxProgramStackDepth)
        {
            throw new ArgumentException("The terminfo program exceeds the configured stack-depth limit.");
        }

        maximumStackDepth = Math.Max(maximumStackDepth, stackDepth);
    }

    private static void Pop(ref int stackDepth)
    {
        if (stackDepth == 0)
        {
            throw Malformed("The terminfo program underflows its evaluation stack.");
        }

        stackDepth--;
    }

    [SuppressMessage(
        "Style",
        "IDE0051:Remove unused private members",
        Justification = "Called only from within extension(...) blocks; the analyzer doesn't track that usage yet.")]
    private static int ParseInteger(ReadOnlySpan<byte> source, ref int index, byte terminator)
    {
        var negative = index < source.Length && source[index] == (byte) '-';

        if (negative)
        {
            index++;
        }

        if (index >= source.Length || source[index] is < (byte) '0' or > (byte) '9')
        {
            throw Malformed("An integer constant requires decimal digits.");
        }

        long value = 0;

        while (index < source.Length && source[index] is >= (byte) '0' and <= (byte) '9')
        {
            var digit = source[index] - (byte) '0';
            var maximum = negative ? (long) int.MaxValue + 1 : int.MaxValue;

            if (value > (maximum - digit) / 10)
            {
                throw Malformed("An integer constant is outside the supported 32-bit range.");
            }

            value = (value * 10) + digit;
            index++;
        }

        return index >= source.Length || source[index++] != terminator
            ? throw Malformed("An integer constant is missing its terminator.")
            : negative ? checked((int) -value) : (int) value;
    }

    [SuppressMessage(
        "Style",
        "IDE0051:Remove unused private members",
        Justification = "Called only from within extension(...) blocks; the analyzer doesn't track that usage yet.")]
    private static int ParseVariable(ReadOnlySpan<byte> source, ref int index)
    {
        if (index >= source.Length)
        {
            throw Malformed("A variable directive requires a name.");
        }

        var name = source[index++];

        return name switch
        {
            >= (byte) 'a' and <= (byte) 'z' => name - (byte) 'a',
            >= (byte) 'A' and <= (byte) 'Z' => 26 + name - (byte) 'A',
            _ => throw Malformed("A variable name must be in a-z or A-Z.")
        };
    }

    private static int ParseUnsigned(ReadOnlySpan<byte> source, ref int index, byte first)
    {
        var value = first - (byte) '0';

        while (index < source.Length && source[index] is >= (byte) '0' and <= (byte) '9')
        {
            var digit = source[index] - (byte) '0';

            if (value > (int.MaxValue - digit) / 10)
            {
                throw Malformed("A printf width or precision is outside the supported range.");
            }

            value = (value * 10) + digit;
            index++;
        }

        return value;
    }

    private static byte NextFormatByte(ReadOnlySpan<byte> source, ref int index) => index >= source.Length ? throw Malformed("A printf format is missing its conversion.") : source[index++];

    private static bool IsFormatPrefix(byte value) =>
        value is (byte) '#' or (byte) '0' or (byte) ' ' or (byte) '.' or
        (>= (byte) '1' and <= (byte) '9');

    private static bool IsConversion(byte value) =>
        value is (byte) 'd' or (byte) 'o' or (byte) 'x' or (byte) 'X' or (byte) 'c' or (byte) 's';

    private static int PackFormat(int flags, int precision) =>
        flags | ((precision + 1) << 8);

    private static void AddOperation(List<Operation> operations, Operation operation, ProgramLimits limits)
    {
        if (operations.Count >= limits.MaxProgramOperations)
        {
            throw new ArgumentException("The terminfo program exceeds the configured operation limit.");
        }

        operations.Add(operation);
    }

    [SuppressMessage(
        "Style",
        "IDE0051:Remove unused private members",
        Justification = "Called only from within extension(...) blocks; the analyzer doesn't track that usage yet.")]
    private static void PatchJump(List<Operation> operations, int index, int target)
    {
        var operation = operations[index];
        operations[index] = new Operation(
            operation.Code,
            target,
            operation.Secondary,
            operation.Tertiary);
    }

    private static FormatException Malformed(string message) => new(message);

    #endregion
}
