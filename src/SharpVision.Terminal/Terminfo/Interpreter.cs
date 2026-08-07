// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Terminfo;

/// <summary>Executes compiled terminfo programs synchronously into a bounded staging buffer.</summary>
/// <remarks>
/// One instance owns ncurses static variables, reusable stacks, and output storage. It is
/// render-thread-affine and must not be called concurrently or reentrantly. Dynamic variables
/// reset before every evaluation; static variables commit only after a successful destination write.
/// Static string assignment allocates an immutable owned snapshot; warmed numeric execution does not.
/// </remarks>
internal sealed class Interpreter
{
    private const byte _numeric = 0;
    private const byte _string = 1;
    private const int _alternate = 1 << 0;
    private const int _zeroPad = 1 << 1;
    private const int _leftAligned = 1 << 2;
    private const int _showSign = 1 << 3;
    private const int _leadingSpace = 1 << 4;

    private readonly ProgramLimits _limits;
    private readonly byte[] _output;
    private readonly byte[] _pairedOutput;
    private readonly int[] _stackNumbers;
    private readonly ReadOnlyMemory<byte>[] _stackStrings;
    private readonly byte[] _stackKinds;
    private readonly int[] _parameterNumbers = new int[9];
    private readonly ReadOnlyMemory<byte>[] _parameterStrings = new ReadOnlyMemory<byte>[9];
    private readonly byte[] _parameterKinds = new byte[9];
    private readonly int[] _dynamicNumbers = new int[26];
    private readonly ReadOnlyMemory<byte>[] _dynamicStrings = new ReadOnlyMemory<byte>[26];
    private readonly byte[] _dynamicKinds = new byte[26];
    private readonly int[] _staticNumbers = new int[26];
    private readonly ReadOnlyMemory<byte>[] _staticStrings = new ReadOnlyMemory<byte>[26];
    private readonly byte[] _staticKinds = new byte[26];
    private readonly int[] _stagedStaticNumbers = new int[26];
    private readonly ReadOnlyMemory<byte>[] _stagedStaticStrings = new ReadOnlyMemory<byte>[26];
    private readonly byte[] _stagedStaticKinds = new byte[26];
    private readonly int[] _transactionStaticNumbers = new int[26];
    private readonly ReadOnlyMemory<byte>[] _transactionStaticStrings = new ReadOnlyMemory<byte>[26];
    private readonly byte[] _transactionStaticKinds = new byte[26];
    private int _parameterCount;
    private int _active;
    private bool _transactionActive;

    #region Lifecycle

    /// <summary>Initializes reusable storage from one non-null finite limit profile.</summary>
    /// <param name="limits">The finite program, stack, parameter, and output limits.</param>
    /// <exception cref="ArgumentNullException"><paramref name="limits"/> is <see langword="null"/>.</exception>
    public Interpreter(ProgramLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);

        _limits = limits;
        _output = new byte[limits.MaxProgramOutputBytes];
        _pairedOutput = new byte[limits.MaxProgramOutputBytes];
        _stackNumbers = new int[limits.MaxProgramStackDepth];
        _stackStrings = new ReadOnlyMemory<byte>[limits.MaxProgramStackDepth];
        _stackKinds = new byte[limits.MaxProgramStackDepth];
    }

    /// <summary>Expands one compiled program and appends its exact raw bytes after complete validation.</summary>
    /// <param name="program">The non-empty compiled terminfo program.</param>
    /// <param name="parameters">
    /// Up to nine <see cref="sbyte"/>, <see cref="byte"/>, <see cref="short"/>,
    /// <see cref="ushort"/>, <see cref="int"/>, or raw-byte parameters. Raw values may be byte
    /// arrays, <see cref="Memory{T}"/>, or <see cref="ReadOnlyMemory{T}"/> over bytes.
    /// </param>
    /// <param name="destination">The non-null destination mutated only after successful evaluation.</param>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A parameter or expansion exceeds a bound or has the wrong kind.</exception>
    /// <exception cref="DivideByZeroException">A selected division or modulo operation has a zero divisor.</exception>
    /// <exception cref="InvalidOperationException">
    /// The program is empty, opaque, intrinsic, exceeds this interpreter's limits, or execution is concurrent.
    /// </exception>
    public void Write(
        DescriptionProgram program,
        ReadOnlySpan<object?> parameters,
        IBufferWriter<byte> destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        ValidateProgram(program);

        if (Interlocked.Exchange(ref _active, 1) != 0)
        {
            throw new InvalidOperationException("A terminfo interpreter cannot execute concurrently or reentrantly.");
        }

        try
        {
            Prepare(parameters, resetStaticVariables: true);
            var outputLength = Execute(program);
            var destinationSpan = destination.GetSpan(outputLength);
            _output.AsSpan(0, outputLength).CopyTo(destinationSpan);
            destination.Advance(outputLength);
            CommitStaticVariables();
        }
        finally
        {
            Volatile.Write(ref _active, 0);
        }
    }

    /// <summary>Expands one compiled program with allocation-free numeric parameters.</summary>
    /// <param name="program">The non-empty compiled terminfo program.</param>
    /// <param name="parameters">Up to nine numeric parameters in terminfo order.</param>
    /// <param name="destination">The non-null destination mutated only after successful evaluation.</param>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is null.</exception>
    /// <exception cref="ArgumentException">A parameter or expansion exceeds a bound.</exception>
    /// <exception cref="DivideByZeroException">A selected division or modulo has a zero divisor.</exception>
    /// <exception cref="InvalidOperationException">The program cannot be interpreted or execution overlaps.</exception>
    public void WriteNumbers(
        DescriptionProgram program,
        ReadOnlySpan<int> parameters,
        IBufferWriter<byte> destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ValidateProgram(program);

        if (Interlocked.Exchange(ref _active, 1) != 0)
        {
            throw new InvalidOperationException("A terminfo interpreter cannot execute concurrently or reentrantly.");
        }

        try
        {
            PrepareNumbers(parameters);
            var outputLength = Execute(program);
            var destinationSpan = destination.GetSpan(outputLength);
            _output.AsSpan(0, outputLength).CopyTo(destinationSpan);
            destination.Advance(outputLength);
            CommitStaticVariables();
        }
        finally
        {
            Volatile.Write(ref _active, 0);
        }
    }

    /// <summary>
    /// Attempts one numeric expansion and commits bytes and static variables only
    /// when actual evaluation succeeds with non-empty output.
    /// </summary>
    /// <param name="program">The non-empty compiled terminfo program.</param>
    /// <param name="parameters">Up to nine numeric parameters in terminfo order.</param>
    /// <param name="destination">The non-null destination mutated only on success.</param>
    /// <returns><see langword="true"/> only when non-empty output was appended.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Execution overlaps another interpreter call.</exception>
    public bool TryWriteNumbersNonEmpty(
        DescriptionProgram program,
        ReadOnlySpan<int> parameters,
        IBufferWriter<byte> destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ValidateProgram(program);

        if (Interlocked.Exchange(ref _active, 1) != 0)
        {
            throw new InvalidOperationException("A terminfo interpreter cannot execute concurrently or reentrantly.");
        }

        try
        {
            int outputLength;

            try
            {
                PrepareNumbers(parameters);
                outputLength = Execute(program);
            }
            catch (Exception exception) when (
                exception is ArgumentException or
                DivideByZeroException or
                InvalidOperationException)
            {
                return false;
            }

            if (outputLength == 0)
            {
                return false;
            }

            var destinationSpan = destination.GetSpan(outputLength);
            _output.AsSpan(0, outputLength).CopyTo(destinationSpan);
            destination.Advance(outputLength);
            CommitStaticVariables();
            return true;
        }
        finally
        {
            Volatile.Write(ref _active, 0);
        }
    }

    /// <summary>
    /// Expands two zero-parameter programs as one static-variable transaction and
    /// begins appending outputs only after both expansions complete successfully.
    /// </summary>
    /// <remarks>
    /// Expansion failure leaves both destinations and static variables unchanged.
    /// An arbitrary destination can still throw while the two already-validated
    /// outputs are appended; callers requiring all-or-nothing publication across
    /// destinations must supply non-throwing staging writers, as <see cref="Programs"/> does.
    /// </remarks>
    /// <param name="first">The first non-empty compiled program.</param>
    /// <param name="second">The second non-empty compiled program.</param>
    /// <param name="firstDestination">The non-null first exact-byte destination.</param>
    /// <param name="secondDestination">The non-null second exact-byte destination.</param>
    /// <exception cref="ArgumentNullException">Either destination is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A program requires a parameter or exceeds an output bound.</exception>
    /// <exception cref="DivideByZeroException">A selected division or modulo operation has a zero divisor.</exception>
    /// <exception cref="InvalidOperationException">
    /// A program is empty, opaque, intrinsic, outside interpreter limits, or execution is concurrent.
    /// </exception>
    public void WritePair(
        DescriptionProgram first,
        DescriptionProgram second,
        IBufferWriter<byte> firstDestination,
        IBufferWriter<byte> secondDestination)
    {
        ArgumentNullException.ThrowIfNull(firstDestination);
        ArgumentNullException.ThrowIfNull(secondDestination);
        ValidateProgram(first);
        ValidateProgram(second);

        if (Interlocked.Exchange(ref _active, 1) != 0)
        {
            throw new InvalidOperationException("A terminfo interpreter cannot execute concurrently or reentrantly.");
        }

        try
        {
            Prepare([], resetStaticVariables: true);
            var firstLength = Execute(first);

            if (firstLength == 0)
            {
                throw new InvalidOperationException("A paired terminfo program produced no output.");
            }

            _output.AsSpan(0, firstLength).CopyTo(_pairedOutput);

            // Dynamic variables and parameters reset for every program, while
            // uppercase ncurses variables remain staged across the pair.
            Prepare([], resetStaticVariables: false);
            var secondLength = Execute(second);

            if (secondLength == 0)
            {
                throw new InvalidOperationException("A paired terminfo program produced no output.");
            }

            var firstSpan = firstDestination.GetSpan(firstLength);
            _pairedOutput.AsSpan(0, firstLength).CopyTo(firstSpan);
            firstDestination.Advance(firstLength);
            var secondSpan = secondDestination.GetSpan(secondLength);
            _output.AsSpan(0, secondLength).CopyTo(secondSpan);
            secondDestination.Advance(secondLength);
            CommitStaticVariables();
        }
        finally
        {
            Volatile.Write(ref _active, 0);
        }
    }

    /// <summary>Gets this interpreter's configured maximum output bytes per program.</summary>
    public int MaxOutputBytes => _limits.MaxProgramOutputBytes;

    /// <summary>Begins one outer render transaction over committed static variables.</summary>
    /// <exception cref="InvalidOperationException">A transaction is already active.</exception>
    public void BeginTransaction()
    {
        if (_transactionActive)
        {
            throw new InvalidOperationException("A terminfo interpreter transaction is already active.");
        }

        Array.Copy(_staticNumbers, _transactionStaticNumbers, _staticNumbers.Length);
        Array.Copy(_staticStrings, _transactionStaticStrings, _staticStrings.Length);
        Array.Copy(_staticKinds, _transactionStaticKinds, _staticKinds.Length);
        _transactionActive = true;
    }

    /// <summary>Commits an active render transaction, retaining its static-variable state.</summary>
    /// <exception cref="InvalidOperationException">No transaction is active.</exception>
    public void CommitTransaction()
    {
        if (!_transactionActive)
        {
            throw new InvalidOperationException("No terminfo interpreter transaction is active.");
        }

        _transactionActive = false;
    }

    /// <summary>Rolls an active render transaction back to its prior static-variable state.</summary>
    /// <exception cref="InvalidOperationException">No transaction is active.</exception>
    public void RollbackTransaction()
    {
        if (!_transactionActive)
        {
            throw new InvalidOperationException("No terminfo interpreter transaction is active.");
        }

        Array.Copy(_transactionStaticNumbers, _staticNumbers, _staticNumbers.Length);
        Array.Copy(_transactionStaticStrings, _staticStrings, _staticStrings.Length);
        Array.Copy(_transactionStaticKinds, _staticKinds, _staticKinds.Length);
        _transactionActive = false;
    }

    #endregion

    #region Preparation and execution

    private void Prepare(
        ReadOnlySpan<object?> parameters,
        bool resetStaticVariables)
    {
        if (parameters.Length > 9)
        {
            throw new ArgumentException("Terminfo programs accept at most nine parameters.", nameof(parameters));
        }

        Array.Clear(_parameterNumbers);
        Array.Clear(_parameterStrings);
        Array.Clear(_parameterKinds);
        Array.Clear(_dynamicNumbers);
        Array.Clear(_dynamicStrings);
        Array.Clear(_dynamicKinds);
        Array.Clear(_stackStrings);
        _parameterCount = parameters.Length;

        for (var index = 0; index < parameters.Length; index++)
        {
            switch (parameters[index])
            {
                case sbyte value:
                    _parameterNumbers[index] = value;
                    break;
                case byte value:
                    _parameterNumbers[index] = value;
                    break;
                case short value:
                    _parameterNumbers[index] = value;
                    break;
                case ushort value:
                    _parameterNumbers[index] = value;
                    break;
                case int value:
                    _parameterNumbers[index] = value;
                    break;
                case byte[] value:
                    StoreStringParameter(index, value);
                    break;
                case Memory<byte> value:
                    StoreStringParameter(index, value);
                    break;
                case ReadOnlyMemory<byte> value:
                    StoreStringParameter(index, value);
                    break;
                case null:
                    throw new ArgumentException("A terminfo parameter cannot be null.", nameof(parameters));
                default:
                    throw new ArgumentException(
                        "A terminfo parameter must be a 32-bit integral value or raw byte memory.",
                        nameof(parameters));
            }
        }

        if (resetStaticVariables)
        {
            Array.Copy(_staticNumbers, _stagedStaticNumbers, _staticNumbers.Length);
            Array.Copy(_staticStrings, _stagedStaticStrings, _staticStrings.Length);
            Array.Copy(_staticKinds, _stagedStaticKinds, _staticKinds.Length);
        }
    }

    private void PrepareNumbers(ReadOnlySpan<int> parameters)
    {
        if (parameters.Length > 9)
        {
            throw new ArgumentException("Terminfo programs accept at most nine parameters.", nameof(parameters));
        }

        Array.Clear(_parameterNumbers);
        Array.Clear(_parameterStrings);
        Array.Clear(_parameterKinds);
        Array.Clear(_dynamicNumbers);
        Array.Clear(_dynamicStrings);
        Array.Clear(_dynamicKinds);
        Array.Clear(_stackStrings);
        _parameterCount = parameters.Length;
        parameters.CopyTo(_parameterNumbers);
        Array.Copy(_staticNumbers, _stagedStaticNumbers, _staticNumbers.Length);
        Array.Copy(_staticStrings, _stagedStaticStrings, _staticStrings.Length);
        Array.Copy(_staticKinds, _stagedStaticKinds, _staticKinds.Length);
    }

    private void ValidateProgram(DescriptionProgram program)
    {
        if (!program.Compiled || program.Builtin || program.IsEmpty)
        {
            throw new InvalidOperationException("Only compiler-produced terminfo programs can be interpreted.");
        }

        if (program.OperationCount > _limits.MaxProgramOperations ||
            program.Representation.Length > _limits.MaxProgramBytes ||
            program.MaximumStackDepth > _limits.MaxProgramStackDepth)
        {
            throw new InvalidOperationException("The compiled program exceeds this interpreter's limits.");
        }
    }

    private void StoreStringParameter(int index, ReadOnlyMemory<byte> value)
    {
        if (value.Length > _limits.MaxStringParameterBytes)
        {
            throw new ArgumentException("A string parameter exceeds the configured byte limit.");
        }

        _parameterKinds[index] = _string;
        _parameterStrings[index] = value;
    }

    private int Execute(DescriptionProgram program)
    {
        var operations = program.Operations.Span;
        var literals = program.Literals.Span;
        var stackDepth = 0;
        var outputLength = 0;

        for (var instruction = 0; instruction < operations.Length; instruction++)
        {
            var operation = operations[instruction];

            switch (operation.Code)
            {
                case TerminfoOperation.Literal:
                    Append(
                        literals.Slice(operation.Operand, operation.Secondary),
                        ref outputLength);
                    break;
                case TerminfoOperation.PushParameter:
                    PushParameter(operation.Operand, ref stackDepth);
                    break;
                case TerminfoOperation.IncrementParameters:
                    IncrementFirstTwoParameters();
                    break;
                case TerminfoOperation.PushConstant:
                    PushNumber(operation.Operand, ref stackDepth);
                    break;
                case TerminfoOperation.StoreVariable:
                    StoreVariable(operation.Operand, ref stackDepth);
                    break;
                case TerminfoOperation.LoadVariable:
                    LoadVariable(operation.Operand, ref stackDepth);
                    break;
                case TerminfoOperation.StringLength:
                    PushNumber(PopString(ref stackDepth).Length, ref stackDepth);
                    break;
                case TerminfoOperation.Add:
                    Binary(ref stackDepth, static (left, right) => unchecked(left + right));
                    break;
                case TerminfoOperation.Subtract:
                    Binary(ref stackDepth, static (left, right) => unchecked(left - right));
                    break;
                case TerminfoOperation.Multiply:
                    Binary(ref stackDepth, static (left, right) => unchecked(left * right));
                    break;
                case TerminfoOperation.Divide:
                    Divide(ref stackDepth);
                    break;
                case TerminfoOperation.Modulo:
                    Modulo(ref stackDepth);
                    break;
                case TerminfoOperation.BitAnd:
                    Binary(ref stackDepth, static (left, right) => left & right);
                    break;
                case TerminfoOperation.BitOr:
                    Binary(ref stackDepth, static (left, right) => left | right);
                    break;
                case TerminfoOperation.BitXor:
                    Binary(ref stackDepth, static (left, right) => left ^ right);
                    break;
                case TerminfoOperation.Equal:
                    Binary(ref stackDepth, static (left, right) => left == right ? 1 : 0);
                    break;
                case TerminfoOperation.LessThan:
                    Binary(ref stackDepth, static (left, right) => left < right ? 1 : 0);
                    break;
                case TerminfoOperation.GreaterThan:
                    Binary(ref stackDepth, static (left, right) => left > right ? 1 : 0);
                    break;
                case TerminfoOperation.LogicalAnd:
                    Binary(ref stackDepth, static (left, right) => left != 0 && right != 0 ? 1 : 0);
                    break;
                case TerminfoOperation.LogicalOr:
                    Binary(ref stackDepth, static (left, right) => left != 0 || right != 0 ? 1 : 0);
                    break;
                case TerminfoOperation.LogicalNot:
                    _stackNumbers[RequireNumeric(stackDepth - 1)] =
                        _stackNumbers[stackDepth - 1] == 0 ? 1 : 0;
                    break;
                case TerminfoOperation.BitNot:
                    _stackNumbers[RequireNumeric(stackDepth - 1)] = ~_stackNumbers[stackDepth - 1];
                    break;
                case TerminfoOperation.JumpIfFalse:
                    if (PopNumber(ref stackDepth) == 0)
                    {
                        instruction = operation.Operand - 1;
                    }

                    break;
                case TerminfoOperation.Jump:
                    instruction = operation.Operand - 1;
                    break;
                case TerminfoOperation.FormatNumber:
                    FormatNumber(operation, PopNumber(ref stackDepth), ref outputLength);
                    break;
                case TerminfoOperation.FormatCharacter:
                    FormatCharacter(operation, PopNumber(ref stackDepth), ref outputLength);
                    break;
                case TerminfoOperation.FormatString:
                    FormatString(operation, PopString(ref stackDepth), ref outputLength);
                    break;
                default:
                    throw new InvalidOperationException("The compiled program contains an unknown instruction.");
            }
        }

        return outputLength;
    }

    #endregion

    #region Stack and variables

    private void PushParameter(int index, ref int stackDepth)
    {
        if (index >= _parameterCount)
        {
            throw new ArgumentException("The terminfo program requires a missing positional parameter.");
        }

        if (_parameterKinds[index] == _string)
        {
            PushString(_parameterStrings[index], ref stackDepth);
        }
        else
        {
            PushNumber(_parameterNumbers[index], ref stackDepth);
        }
    }

    private void IncrementFirstTwoParameters()
    {
        if (_parameterCount < 2)
        {
            throw new ArgumentException("The terminfo increment directive requires parameters one and two.");
        }

        for (var index = 0; index < 2; index++)
        {
            if (_parameterKinds[index] != _numeric)
            {
                throw WrongParameterKind();
            }

            _parameterNumbers[index] = unchecked(_parameterNumbers[index] + 1);
        }
    }

    private void PushNumber(int value, ref int stackDepth)
    {
        EnsureStackSlot(stackDepth);
        _stackKinds[stackDepth] = _numeric;
        _stackNumbers[stackDepth] = value;
        _stackStrings[stackDepth] = default;
        stackDepth++;
    }

    private void PushString(ReadOnlyMemory<byte> value, ref int stackDepth)
    {
        EnsureStackSlot(stackDepth);
        _stackKinds[stackDepth] = _string;
        _stackStrings[stackDepth] = value;
        _stackNumbers[stackDepth] = 0;
        stackDepth++;
    }

    private int PopNumber(ref int stackDepth)
    {
        stackDepth--;
        var index = RequireNumeric(stackDepth);
        return _stackNumbers[index];
    }

    private ReadOnlyMemory<byte> PopString(ref int stackDepth)
    {
        stackDepth--;

        return stackDepth < 0 || _stackKinds[stackDepth] != _string ? throw WrongParameterKind() : _stackStrings[stackDepth];
    }

    private int RequireNumeric(int index) => index < 0 || _stackKinds[index] != _numeric ? throw WrongParameterKind() : index;

    private void Binary(ref int stackDepth, Func<int, int, int> operation)
    {
        var right = PopNumber(ref stackDepth);
        var left = PopNumber(ref stackDepth);
        PushNumber(operation(left, right), ref stackDepth);
    }

    private void Divide(ref int stackDepth)
    {
        var right = PopNumber(ref stackDepth);
        var left = PopNumber(ref stackDepth);

        if (right == 0)
        {
            throw new DivideByZeroException("A terminfo program attempted division by zero.");
        }

        var result = left == int.MinValue && right == -1 ? int.MinValue : left / right;
        PushNumber(result, ref stackDepth);
    }

    private void Modulo(ref int stackDepth)
    {
        var right = PopNumber(ref stackDepth);
        var left = PopNumber(ref stackDepth);

        if (right == 0)
        {
            throw new DivideByZeroException("A terminfo program attempted modulo by zero.");
        }

        var result = left == int.MinValue && right == -1 ? 0 : left % right;
        PushNumber(result, ref stackDepth);
    }

    private void StoreVariable(int variable, ref int stackDepth)
    {
        stackDepth--;

        if (stackDepth < 0)
        {
            throw new InvalidOperationException("The compiled program underflowed its evaluation stack.");
        }

        if (variable < 26)
        {
            _dynamicKinds[variable] = _stackKinds[stackDepth];
            _dynamicNumbers[variable] = _stackNumbers[stackDepth];
            _dynamicStrings[variable] = _stackStrings[stackDepth];
            return;
        }

        var staticIndex = variable - 26;
        _stagedStaticKinds[staticIndex] = _stackKinds[stackDepth];
        _stagedStaticNumbers[staticIndex] = _stackNumbers[stackDepth];

        _stagedStaticStrings[staticIndex] = _stackKinds[stackDepth] == _string ? (ReadOnlyMemory<byte>) _stackStrings[stackDepth].ToArray() : default;
    }

    private void LoadVariable(int variable, ref int stackDepth)
    {
        if (variable < 26)
        {
            PushVariable(
                _dynamicKinds[variable],
                _dynamicNumbers[variable],
                _dynamicStrings[variable],
                ref stackDepth);
            return;
        }

        var staticIndex = variable - 26;
        PushVariable(
            _stagedStaticKinds[staticIndex],
            _stagedStaticNumbers[staticIndex],
            _stagedStaticStrings[staticIndex],
            ref stackDepth);
    }

    private void PushVariable(
        byte kind,
        int number,
        ReadOnlyMemory<byte> text,
        ref int stackDepth)
    {
        if (kind == _string)
        {
            PushString(text, ref stackDepth);
        }
        else
        {
            PushNumber(number, ref stackDepth);
        }
    }

    #endregion

    #region Formatting

    private void FormatNumber(TerminfoOperation operation, int value, ref int outputLength)
    {
        Span<byte> digits = stackalloc byte[32];
        var conversion = (byte) operation.Operand;
        var radix = conversion switch
        {
            (byte) 'o' => 8,
            (byte) 'x' or (byte) 'X' => 16,
            _ => 10
        };
        var precision = UnpackPrecision(operation.Tertiary);
        var flags = operation.Tertiary & 0xff;
        var negative = conversion == (byte) 'd' && value < 0;
        var magnitude = negative ? (uint) -(long) value : unchecked((uint) value);
        var digitCount = 0;

        do
        {
            var digit = (int) (magnitude % radix);
            digits[digits.Length - 1 - digitCount] = digit < 10
                ? (byte) ('0' + digit)
                : (byte) ((conversion == (byte) 'X' ? 'A' : 'a') + digit - 10);
            digitCount++;
            magnitude /= (uint) radix;
        }
        while (magnitude != 0);

        if (precision == 0 && value == 0)
        {
            digitCount = 0;
        }

        var precisionZeros = Math.Max(0, precision - digitCount);

        if (conversion == (byte) 'o' && (flags & _alternate) != 0)
        {
            if (value == 0 && digitCount == 0)
            {
                digitCount = 1;
            }
            else if (value != 0)
            {
                precisionZeros = Math.Max(1, precisionZeros);
            }
        }

        var prefixLength = PrefixLength(conversion, value, flags);
        var signLength = negative || (flags & (_showSign | _leadingSpace)) != 0 ? 1 : 0;
        var contentLength = signLength + prefixLength + precisionZeros + digitCount;
        var width = Math.Max(0, operation.Secondary);
        var padding = Math.Max(0, width - contentLength);
        var left = (flags & _leftAligned) != 0;
        var zero = (flags & _zeroPad) != 0 && !left && precision < 0;

        EnsureOutput(outputLength, contentLength + padding);

        if (!left && !zero)
        {
            AppendRepeated((byte) ' ', padding, ref outputLength);
        }

        if (negative)
        {
            _output[outputLength++] = (byte) '-';
        }
        else if ((flags & _showSign) != 0)
        {
            _output[outputLength++] = (byte) '+';
        }
        else if ((flags & _leadingSpace) != 0)
        {
            _output[outputLength++] = (byte) ' ';
        }

        AppendPrefix(conversion, value, flags, ref outputLength);

        if (zero)
        {
            AppendRepeated((byte) '0', padding, ref outputLength);
        }

        AppendRepeated((byte) '0', precisionZeros, ref outputLength);
        Append(digits.Slice(digits.Length - digitCount, digitCount), ref outputLength);

        if (left)
        {
            AppendRepeated((byte) ' ', padding, ref outputLength);
        }
    }

    private void FormatCharacter(TerminfoOperation operation, int value, ref int outputLength)
    {
        var width = Math.Max(1, operation.Secondary);
        var flags = operation.Tertiary & 0xff;
        var padding = width - 1;
        EnsureOutput(outputLength, width);

        if ((flags & _leftAligned) == 0)
        {
            AppendRepeated((byte) ' ', padding, ref outputLength);
        }

        _output[outputLength++] = unchecked((byte) value);

        if ((flags & _leftAligned) != 0)
        {
            AppendRepeated((byte) ' ', padding, ref outputLength);
        }
    }

    private void FormatString(TerminfoOperation operation, ReadOnlyMemory<byte> value, ref int outputLength)
    {
        var precision = UnpackPrecision(operation.Tertiary);
        var length = precision < 0 ? value.Length : Math.Min(value.Length, precision);
        var width = Math.Max(0, operation.Secondary);
        var padding = Math.Max(0, width - length);
        var left = (operation.Tertiary & _leftAligned) != 0;
        EnsureOutput(outputLength, length + padding);

        if (!left)
        {
            AppendRepeated((byte) ' ', padding, ref outputLength);
        }

        Append(value.Span[..length], ref outputLength);

        if (left)
        {
            AppendRepeated((byte) ' ', padding, ref outputLength);
        }
    }

    private static int PrefixLength(byte conversion, int value, int flags)
    {
        return (flags & _alternate) == 0
            ? 0
            : conversion switch
            {
                (byte) 'x' or (byte) 'X' when value != 0 => 2,
                _ => 0
            };
    }

    private void AppendPrefix(byte conversion, int value, int flags, ref int outputLength)
    {
        if ((flags & _alternate) == 0 || value == 0)
        {
            return;
        }

        if (conversion is (byte) 'x' or (byte) 'X')
        {
            _output[outputLength++] = (byte) '0';
            _output[outputLength++] = conversion;
        }
    }

    #endregion

    #region Bounds and commit

    private void Append(ReadOnlySpan<byte> value, ref int outputLength)
    {
        EnsureOutput(outputLength, value.Length);
        value.CopyTo(_output.AsSpan(outputLength));
        outputLength += value.Length;
    }

    private void AppendRepeated(byte value, int count, ref int outputLength)
    {
        EnsureOutput(outputLength, count);
        _output.AsSpan(outputLength, count).Fill(value);
        outputLength += count;
    }

    private void EnsureOutput(int currentLength, int additionalLength)
    {
        if (additionalLength < 0 || currentLength > _output.Length - additionalLength)
        {
            throw new ArgumentException("The expansion exceeds the configured output-byte limit.");
        }
    }

    private void EnsureStackSlot(int stackDepth)
    {
        if ((uint) stackDepth >= (uint) _stackNumbers.Length)
        {
            throw new InvalidOperationException("The compiled program exceeded its validated stack depth.");
        }
    }

    private void CommitStaticVariables()
    {
        Array.Copy(_stagedStaticNumbers, _staticNumbers, _staticNumbers.Length);
        Array.Copy(_stagedStaticStrings, _staticStrings, _staticStrings.Length);
        Array.Copy(_stagedStaticKinds, _staticKinds, _staticKinds.Length);
    }

    private static int UnpackPrecision(int packed) => (packed >> 8) - 1;

    private static ArgumentException WrongParameterKind() =>
        new("A terminfo operation received a parameter or variable of the wrong kind.");

    #endregion
}
