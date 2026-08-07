// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Terminfo;

using Capabilities;

/// <summary>
/// Owns the finite ordinal mapping from validated terminal-description names to
/// opaque compiled programs.
/// </summary>
internal sealed class Programs
{
    private readonly Dictionary<string, DescriptionProgram> _values;
    private long _validatedContracts;
    private long _executableContracts;
    private int _validatedPairs;
    private int _executablePairs;

    /// <summary>Initializes an owned immutable snapshot of compiled programs.</summary>
    /// <param name="programs">The non-null named programs to copy.</param>
    /// <exception cref="ArgumentNullException"><paramref name="programs"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A program name is empty or contains only white space.</exception>
    public Programs(IReadOnlyDictionary<string, DescriptionProgram> programs)
    {
        ArgumentNullException.ThrowIfNull(programs);

        _values = new Dictionary<string, DescriptionProgram>(programs.Count, StringComparer.Ordinal);

        foreach (var pair in programs)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pair.Key);
            _values.Add(pair.Key, pair.Value);
        }

    }

    /// <summary>Gets the stable empty compiled-program aggregate.</summary>
    public static Programs Empty { get; } = new(new Dictionary<string, DescriptionProgram>());

    /// <summary>Gets the number of retained named programs.</summary>
    public int Count => _values.Count;

    /// <summary>
    /// Gets whether the required absolute cursor, reset, and clearing programs
    /// satisfy their exact arity and non-empty expansion contracts.
    /// </summary>
    public bool FullScreenReady =>
        Has(CapabilityNames.Cup) &&
        Has(CapabilityNames.Sgr0) &&
        (Has(CapabilityNames.Clear) || (Has(CapabilityNames.El) && Has(CapabilityNames.Ed)));

    /// <summary>Gets the highest color tier backed by complete directional programs.</summary>
    /// <param name="declared">The semantic fidelity declared by the active profile.</param>
    /// <returns>The highest safely emittable color tier.</returns>
    public ColorDepth EffectiveColorDepth(ColorDepth declared)
    {
        var indexed = Has("setaf") && Has("setab");
        var defaults = Has("op") || (Has("setdf") && Has("setdb")) || Has("sgr0");

        var indexedDepth = declared switch
        {
            ColorDepth.TrueColor or ColorDepth.Indexed256 => ColorDepth.Indexed256,
            ColorDepth.Basic16 => ColorDepth.Basic16,
            ColorDepth.Monochrome => ColorDepth.Monochrome,
            _ => throw new UnreachableException()
        };

        return !indexed || !defaults
            ? ColorDepth.Monochrome
            : declared == ColorDepth.TrueColor && Has("setrgbf") && Has("setrgbb")
                ? ColorDepth.TrueColor
                : indexedDepth;
    }

    /// <summary>Gets whether one exact named program satisfies its typed output contract.</summary>
    /// <param name="name">The non-blank ordinal program name.</param>
    /// <returns><see langword="true"/> when the retained program is executable for its known contract.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty or contains only white space.</exception>
    public bool Has(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!_values.TryGetValue(name, out var program) ||
            (!program.Builtin && !program.Compiled))
        {
            return false;
        }

        var contract = ContractIndex(name);
        return contract < 0 || IsExecutableContract(name, program, contract);
    }

    /// <summary>Attempts to get one exact named compiled program.</summary>
    /// <param name="name">The non-blank ordinal program name.</param>
    /// <param name="program">Receives the retained program when present.</param>
    /// <returns><see langword="true"/> when the name is retained, including an empty program.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty or contains only white space.</exception>
    public bool TryGet(string name, out DescriptionProgram program)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return _values.TryGetValue(name, out program);
    }

    /// <summary>Expands one named program into a caller-owned staging destination.</summary>
    /// <param name="name">The non-blank ordinal program name.</param>
    /// <param name="parameters">Numeric parameters in the description program's documented order.</param>
    /// <param name="interpreter">The non-null renderer-owned interpreter.</param>
    /// <param name="destination">The non-null synchronous destination.</param>
    /// <returns><see langword="true"/> only when a present program appended non-empty output.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty or contains only white space.</exception>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    /// <exception cref="InvalidOperationException">Execution overlaps another interpreter call.</exception>
    public bool TryWrite(
        string name,
        ReadOnlySpan<int> parameters,
        Interpreter interpreter,
        IBufferWriter<byte> destination)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(interpreter);
        ArgumentNullException.ThrowIfNull(destination);

        if (!_values.TryGetValue(name, out var program) || program.IsEmpty)
        {
            return false;
        }

        var contract = ContractIndex(name);
        var executable = contract < 0 ||
            (parameters.Length == ExpectedParameterCount(name) &&
             IsExecutableContract(name, program, contract));
        return executable &&
            (program.Compiled
                ? interpreter.TryWriteNumbersNonEmpty(program, parameters, destination)
                : program.Builtin && TryWriteIntrinsic(name, parameters, destination));
    }

    /// <summary>Compares exact program names, kinds, and raw representations.</summary>
    /// <param name="other">The comparison aggregate.</param>
    /// <returns>Whether both aggregates encode the same named programs.</returns>
    public bool SemanticallyEquals(Programs other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (_values.Count != other._values.Count)
        {
            return false;
        }

        foreach (var pair in _values)
        {
            if (!other._values.TryGetValue(pair.Key, out var candidate) ||
                pair.Value.Builtin != candidate.Builtin ||
                !pair.Value.Representation.Span.SequenceEqual(candidate.Representation.Span))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Gets whether an exact zero-parameter prefix/suffix pair expands to non-empty output.</summary>
    /// <param name="prefixName">The non-blank prefix program name.</param>
    /// <param name="suffixName">The non-blank suffix program name.</param>
    /// <returns>Whether both programs accept no parameters and prove non-empty paired output.</returns>
    public bool HasZeroParameterPair(string prefixName, string suffixName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefixName);
        ArgumentException.ThrowIfNullOrWhiteSpace(suffixName);

        var pair = PairIndex(prefixName, suffixName);
        return pair >= 0 && IsExecutableZeroParameterPair(prefixName, suffixName, pair);
    }

    /// <summary>
    /// Attempts to expand a matched zero-parameter lifecycle pair atomically,
    /// preserving session-scoped static variables only when both programs succeed.
    /// </summary>
    /// <param name="enableName">The non-blank acquisition program name.</param>
    /// <param name="disableName">The non-blank restoration program name.</param>
    /// <param name="interpreter">The non-null session-owned interpreter.</param>
    /// <param name="enable">Receives an owned exact acquisition snapshot, or empty on failure.</param>
    /// <param name="disable">Receives an independently owned exact restoration snapshot, or empty on failure.</param>
    /// <returns><see langword="true"/> only when both owned non-empty outputs are ready.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="interpreter"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Either name is blank.</exception>
    public bool TryExpandPair(
        string enableName,
        string disableName,
        Interpreter interpreter,
        out ReadOnlyMemory<byte> enable,
        out ReadOnlyMemory<byte> disable)
    {
        enable = ReadOnlyMemory<byte>.Empty;
        disable = ReadOnlyMemory<byte>.Empty;
        ArgumentException.ThrowIfNullOrWhiteSpace(enableName);
        ArgumentException.ThrowIfNullOrWhiteSpace(disableName);
        ArgumentNullException.ThrowIfNull(interpreter);

        if (!_values.TryGetValue(enableName, out var enableProgram) ||
            !_values.TryGetValue(disableName, out var disableProgram) ||
            enableProgram.IsEmpty ||
            disableProgram.IsEmpty)
        {
            return false;
        }

        var enableBuffer = new ArrayBufferWriter<byte>();
        var disableBuffer = new ArrayBufferWriter<byte>();

        try
        {
            if (enableProgram.Compiled && disableProgram.Compiled)
            {
                interpreter.WritePair(
                    enableProgram,
                    disableProgram,
                    enableBuffer,
                    disableBuffer);
            }
            else if (enableProgram.Builtin && disableProgram.Builtin)
            {
                if (!TryWriteIntrinsic(enableName, [], enableBuffer) ||
                    !TryWriteIntrinsic(disableName, [], disableBuffer))
                {
                    return false;
                }
            }
            else
            {
                // A matched lifecycle pair comes from one description source.
                // Reject mixed intrinsic/compiler pairs rather than commit static
                // state before discovering a non-emittable counterpart.
                return false;
            }
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            DivideByZeroException or
            InvalidOperationException)
        {
            return false;
        }

        if (enableBuffer.WrittenCount is 0 || disableBuffer.WrittenCount is 0 ||
            enableBuffer.WrittenCount > interpreter.MaxOutputBytes ||
            disableBuffer.WrittenCount > interpreter.MaxOutputBytes)
        {
            return false;
        }

        enable = enableBuffer.WrittenMemory;
        disable = disableBuffer.WrittenMemory;
        return true;
    }

    private bool IsExecutableContract(string name, DescriptionProgram program, int contract)
    {
        var bit = 1L << contract;
        var validated = Volatile.Read(ref _validatedContracts);

        if ((validated & bit) != 0)
        {
            return (Volatile.Read(ref _executableContracts) & bit) != 0;
        }

        lock (_values)
        {
            validated = _validatedContracts;

            if ((validated & bit) != 0)
            {
                return (_executableContracts & bit) != 0;
            }

            var executable = ClassifyExecutableContract(name, program);

            if (executable)
            {
                Volatile.Write(ref _executableContracts, _executableContracts | bit);
            }

            Volatile.Write(ref _validatedContracts, validated | bit);
            return executable;
        }
    }

    private static bool ClassifyExecutableContract(string name, DescriptionProgram program)
    {
        var expected = ExpectedParameterCount(name);

        return string.Equals(name, "Ms", StringComparison.Ordinal)
            ? program.Compiled &&
                program.ParameterCount == expected &&
                ProbeStringProgram(program)
            : program.Builtin
                ? IntrinsicSupports(name)
                : program.Compiled &&
                  program.ParameterCount == expected &&
                  (program.HasProvablyNonEmptyNumericExpansion ||
                   (!RequiresStaticCursorContract(name) && ProbeProgram(name, program, expected)));
    }

    private bool IsExecutableZeroParameterPair(string enableName, string disableName, int pair)
    {
        var bit = 1 << pair;
        var validated = Volatile.Read(ref _validatedPairs);

        if ((validated & bit) != 0)
        {
            return (Volatile.Read(ref _executablePairs) & bit) != 0;
        }

        lock (_values)
        {
            validated = _validatedPairs;

            if ((validated & bit) != 0)
            {
                return (_executablePairs & bit) != 0;
            }

            var executable = ClassifyZeroParameterPair(enableName, disableName);

            if (executable)
            {
                Volatile.Write(ref _executablePairs, _executablePairs | bit);
            }

            Volatile.Write(ref _validatedPairs, validated | bit);
            return executable;
        }
    }

    private bool ClassifyZeroParameterPair(string enableName, string disableName)
    {
        if (!_values.TryGetValue(enableName, out var enable) ||
            !_values.TryGetValue(disableName, out var disable) ||
            (!enable.Builtin && enable.ParameterCount != 0) ||
            (!disable.Builtin && disable.ParameterCount != 0))
        {
            return false;
        }

        if (enable.Builtin || disable.Builtin)
        {
            return enable.Builtin &&
                disable.Builtin &&
                IntrinsicSupports(enableName) &&
                IntrinsicSupports(disableName);
        }

        if (!enable.Compiled || !disable.Compiled)
        {
            return false;
        }

        if (enable.HasProvablyNonEmptyNumericExpansion &&
            disable.HasProvablyNonEmptyNumericExpansion)
        {
            return true;
        }

        if (string.Equals(enableName, "civis", StringComparison.Ordinal) &&
            string.Equals(disableName, "cnorm", StringComparison.Ordinal))
        {
            return false;
        }

        // Compatibility probing only interprets already-validated compiled programs with
        // small representative test parameters to check they don't throw; it never touches
        // real terminal I/O, so a caller-configured ProgramLimits is not reachable here.
        var interpreter = new Interpreter(ProgramLimits.Default);
        var enableDestination = new ArrayBufferWriter<byte>();
        var disableDestination = new ArrayBufferWriter<byte>();
        interpreter.BeginTransaction();

        try
        {
            interpreter.WritePair(enable, disable, enableDestination, disableDestination);
            return true;
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            DivideByZeroException or
            InvalidOperationException)
        {
            return false;
        }
        finally
        {
            interpreter.RollbackTransaction();
        }
    }

    private static bool ProbeProgram(string name, DescriptionProgram program, int expected)
    {
        var interpreter = new Interpreter(ProgramLimits.Default);
        var destination = new ArrayBufferWriter<byte>();
        Span<int> parameters = stackalloc int[3];
        FillRepresentativeParameters(name, parameters);
        interpreter.BeginTransaction();

        try
        {
            interpreter.WriteNumbers(program, parameters[..expected], destination);
            return destination.WrittenCount > 0;
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            DivideByZeroException or
            InvalidOperationException)
        {
            return false;
        }
        finally
        {
            interpreter.RollbackTransaction();
        }
    }

    private static bool ProbeStringProgram(DescriptionProgram program)
    {
        var interpreter = new Interpreter(ProgramLimits.Default);
        var destination = new ArrayBufferWriter<byte>();
        object?[] parameters = ["c"u8.ToArray(), "?"u8.ToArray()];
        interpreter.BeginTransaction();

        try
        {
            interpreter.Write(program, parameters, destination);
            return destination.WrittenCount > 0;
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            DivideByZeroException or
            InvalidOperationException)
        {
            return false;
        }
        finally
        {
            interpreter.RollbackTransaction();
        }
    }

    private static int ContractIndex(string name) => name switch
    {
        "cup" => 0,
        "setrgbf" => 1,
        "setrgbb" => 2,
        "setaf" => 3,
        "setab" => 4,
        "Smulx" => 5,
        "Setulc" => 6,
        "Ss" => 7,
        "sgr0" => 8,
        "clear" => 9,
        "el" => 10,
        "ed" => 11,
        "bold" => 12,
        "dim" => 13,
        "sitm" => 14,
        "smul" => 15,
        "blink" => 16,
        "rev" => 17,
        "invis" => 18,
        "smxx" => 19,
        "Smol" => 20,
        "Rmol" => 21,
        "setdf" => 22,
        "setdb" => 23,
        "op" => 24,
        "civis" => 25,
        "cnorm" => 26,
        "Se" => 27,
        "bel" => 28,
        "Ms" => 29,
        _ => -1
    };

    private static int PairIndex(string enableName, string disableName) =>
        (enableName, disableName) switch
        {
            ("TS", "fsl") => 0,
            ("civis", "cnorm") => 1,
            _ => -1
        };

    private static int ExpectedParameterCount(string name) => name switch
    {
        "cup" => 2,
        "setrgbf" or "setrgbb" => 3,
        "setaf" or "setab" or "Smulx" or "Setulc" or "Ss" => 1,
        "Ms" => 2,
        "sgr0" or "clear" or "el" or "ed" or
        "bold" or "dim" or "sitm" or "smul" or "blink" or "rev" or
        "invis" or "smxx" or "Smol" or "Rmol" or
        "setdf" or "setdb" or "op" or "civis" or "cnorm" or "Se" or "bel" => 0,
        _ => -1
    };

    private static bool IntrinsicSupports(string name) => name is
        "cup" or "sgr0" or "clear" or "el" or "ed" or
        "bold" or "dim" or "sitm" or "smul" or "blink" or "rev" or
        "invis" or "smxx" or "Smol" or
        "setaf" or "setab" or "setrgbf" or "setrgbb" or
        "setdf" or "setdb" or "op" or "civis" or "cnorm" or "bel";

    private static bool RequiresStaticCursorContract(string name) => name is
        "civis" or "cnorm" or "Ss" or "Se";

    private static void FillRepresentativeParameters(string name, Span<int> parameters)
    {
        parameters.Clear();

        switch (name)
        {
            case "setrgbf":
            case "setrgbb":
                parameters[0] = 1;
                parameters[1] = 2;
                parameters[2] = 3;
                break;
            case "Ss":
                parameters[0] = 4;
                break;
            case "Smulx":
                parameters[0] = 3;
                break;
            case "Setulc":
                parameters[0] = 0x010203;
                break;
            case "setaf":
            case "setab":
                parameters[0] = 1;
                break;
            default:
                break;
        }
    }

    private static bool TryWriteIntrinsic(
        string name,
        ReadOnlySpan<int> parameters,
        IBufferWriter<byte> destination)
    {
        var writer = new ProtocolWriter(destination);

        switch (name)
        {
            case "cup" when parameters.Length == 2:
                Csi.Position(writer, parameters[0] + 1, parameters[1] + 1);
                return true;
            case "sgr0" when parameters.IsEmpty:
                Sgr.Reset(writer);
                return true;
            case "clear" when parameters.IsEmpty:
                Csi.Position(writer, 1, 1);
                Csi.EraseDisplay(writer, EraseArea.All);
                return true;
            case "el" when parameters.IsEmpty:
                Csi.EraseLine(writer, EraseArea.After);
                return true;
            case "ed" when parameters.IsEmpty:
                Csi.EraseDisplay(writer, EraseArea.After);
                return true;
            case "bold" when parameters.IsEmpty:
                Sgr.Apply(writer, Rendition.Bold);
                return true;
            case "dim" when parameters.IsEmpty:
                Sgr.Apply(writer, Rendition.Dim);
                return true;
            case "sitm" when parameters.IsEmpty:
                Sgr.Apply(writer, Rendition.Italic);
                return true;
            case "smul" when parameters.IsEmpty:
                Sgr.Apply(writer, Rendition.Underline);
                return true;
            case "blink" when parameters.IsEmpty:
                Sgr.Apply(writer, Rendition.SlowBlink);
                return true;
            case "rev" when parameters.IsEmpty:
                Sgr.Apply(writer, Rendition.Reverse);
                return true;
            case "invis" when parameters.IsEmpty:
                Sgr.Apply(writer, Rendition.Hidden);
                return true;
            case "smxx" when parameters.IsEmpty:
                Sgr.Apply(writer, Rendition.Strike);
                return true;
            case "Smol" when parameters.IsEmpty:
                Sgr.Apply(writer, Rendition.Overline);
                return true;
            case "setaf" when parameters.Length == 1:
                Sgr.ForegroundPalette(writer, parameters[0]);
                return true;
            case "setab" when parameters.Length == 1:
                Sgr.BackgroundPalette(writer, parameters[0]);
                return true;
            case "setrgbf" when parameters.Length == 3:
                Sgr.Foreground(writer, Color.Rgb(parameters[0], parameters[1], parameters[2]));
                return true;
            case "setrgbb" when parameters.Length == 3:
                Sgr.Background(writer, Color.Rgb(parameters[0], parameters[1], parameters[2]));
                return true;
            case "setdf" when parameters.IsEmpty:
                Sgr.Foreground(writer, Color.Default);
                return true;
            case "setdb" when parameters.IsEmpty:
                Sgr.Background(writer, Color.Default);
                return true;
            case "op" when parameters.IsEmpty:
                Sgr.Foreground(writer, Color.Default);
                Sgr.Background(writer, Color.Default);
                return true;
            case "smcup":
                ProtocolModes.AlternateScreen(writer, enabled: true);
                return true;
            case "rmcup":
                ProtocolModes.AlternateScreen(writer, enabled: false);
                return true;
            case "civis":
                ProtocolModes.CursorVisible(writer, visible: false);
                return true;
            case "cnorm":
                ProtocolModes.CursorVisible(writer, visible: true);
                return true;
            case "bel" when parameters.IsEmpty:
                destination.Write("\a"u8);
                return true;
            default:
                return false;
        }
    }
}
