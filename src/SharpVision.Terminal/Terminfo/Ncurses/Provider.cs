// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Terminfo.Ncurses;

using Capabilities;

/// <summary>
/// Loads one ncurses terminal description under the process-global terminal lock and
/// retains only owned, allowlisted values.
/// </summary>
internal sealed class Provider: IDescriptionProvider
{
    private static readonly Lock _terminalLock = new();
    private readonly Func<IReadOnlyList<string>, INative?> _openNative;
    private readonly Func<string, string?> _readEnvironment;

    /// <summary>Initializes a provider using dynamic ncurses library discovery.</summary>
    public Provider() : this(NcursesLibrary.Open, Environment.GetEnvironmentVariable)
    {
    }

    /// <summary>Initializes a provider with a deterministic native-boundary factory.</summary>
    /// <param name="openNative">The non-null factory returning an owned native boundary for one
    /// candidate library name list.</param>
    /// <exception cref="ArgumentNullException"><paramref name="openNative"/> is <see langword="null"/>.</exception>
    public Provider(Func<IReadOnlyList<string>, INative?> openNative)
        : this(openNative, Environment.GetEnvironmentVariable)
    {
    }

    /// <summary>Initializes a provider with deterministic native and live-environment seams.</summary>
    /// <param name="openNative">The non-null factory returning an owned native boundary for one
    /// candidate library name list.</param>
    /// <param name="readEnvironment">The non-null live environment reader invoked under the global lock.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public Provider(Func<IReadOnlyList<string>, INative?> openNative, Func<string, string?> readEnvironment)
    {
        ArgumentNullException.ThrowIfNull(openNative);
        ArgumentNullException.ThrowIfNull(readEnvironment);
        _openNative = openNative;
        _readEnvironment = readEnvironment;
    }

    /// <inheritdoc/>
    public DescriptionResult Load(DescriptionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Platform == DescriptionPlatform.Windows)
        {
            return DescriptionResult.PlatformUnavailable();
        }

        INative? native;

        try
        {
            native = _openNative(request.Limits.NcursesLibraryNames);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return DescriptionResult.ProviderFailed(
            [
                new DescriptionDiagnostic(DescriptionDiagnosticCode.NativeFailure)
            ]);
        }

        if (native is null)
        {
            return DescriptionResult.PlatformUnavailable();
        }

        lock (_terminalLock)
        {
            return LoadSerialized(native, request, _readEnvironment);
        }
    }

    private static DescriptionResult LoadSerialized(
        INative native,
        DescriptionRequest request,
        Func<string, string?> readEnvironment)
    {
        var diagnostics = new List<DescriptionDiagnostic>();
        var status = DescriptionLoadStatus.ProviderFailed;
        TerminalProfile? profile = null;
        var previousTerminal = (nint) 0;
        var hasPreviousTerminal = false;
        var extendedNames = false;
        var hasExtendedNamesState = false;
        var attemptedSetup = false;

        try
        {
            previousTerminal = native.CurrentTerminal;
            hasPreviousTerminal = true;
            extendedNames = native.EnableExtendedNames(true);
            hasExtendedNamesState = true;

            if (!DescriptionEnvironment.TryCapture(
                readEnvironment,
                request.TerminalName,
                request.Limits,
                out var environment,
                out var environmentDiagnostic))
            {
                diagnostics.Add(environmentDiagnostic);
                status = DescriptionLoadStatus.ProviderFailed;
            }
            else
            {
                // Setup is the call that actually mutates ncurses' process-global cur_term, so it
                // marks the point past which rollback becomes relevant — set before the call
                // itself in case Setup throws, since the mutation risk exists as soon as it's
                // attempted.
                attemptedSetup = true;
                var setupStatus = native.Setup(
                    request.TerminalName,
                    request.OutputFileDescriptor,
                    out var setupError);

                if (setupStatus == 0 && setupError == 1)
                {
                    if (native.CurrentTerminal == 0)
                    {
                        status = DescriptionLoadStatus.ProviderFailed;
                        diagnostics.Add(new DescriptionDiagnostic(DescriptionDiagnosticCode.NativeFailure));
                    }
                    else
                    {
                        profile = Snapshot(native, request, diagnostics, environment.ByteCount);
                        status = DescriptionLoadStatus.Loaded;
                    }
                }
                else if (setupStatus == -1 && setupError == 1)
                {
                    profile = new TerminalProfile(
                        new Description(
                            request.TerminalName,
                            DescriptionOrigin.Database,
                            Suitability.Hardcopy),
                        TerminalCapabilities.Conservative,
                        Programs.Empty,
                        KeyMap.Empty);
                    status = DescriptionLoadStatus.Loaded;
                }
                else if (setupStatus == -1 && setupError == 0)
                {
                    diagnostics.Add(new DescriptionDiagnostic(DescriptionDiagnosticCode.MissingOrGeneric));

                    if (environment.HasMatchingInlineTermcap(request.TerminalName))
                    {
                        diagnostics.Add(new DescriptionDiagnostic(DescriptionDiagnosticCode.NativeFailure));
                        status = DescriptionLoadStatus.ProviderFailed;
                    }
                    else
                    {
                        status = DescriptionLoadStatus.MissingOrGeneric;
                    }
                }
                else
                {
                    status = DescriptionLoadStatus.ProviderFailed;
                    diagnostics.Add(new DescriptionDiagnostic(DescriptionDiagnosticCode.NativeFailure));
                }
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            status = DescriptionLoadStatus.ProviderFailed;
            profile = null;
            diagnostics.Add(new DescriptionDiagnostic(DescriptionDiagnosticCode.NativeFailure));
        }
        finally
        {
            Cleanup(
                native,
                previousTerminal,
                hasPreviousTerminal,
                attemptedSetup,
                extendedNames,
                hasExtendedNamesState,
                diagnostics);
        }

        return CreateResult(status, profile, diagnostics);
    }

    private static TerminalProfile Snapshot(
        INative native,
        DescriptionRequest request,
        List<DescriptionDiagnostic> diagnostics,
        int environmentBytes)
    {
        var flags = ReadFlags(native, diagnostics);
        var numbers = ReadNumbers(native, diagnostics);
        var strings = ReadStrings(native, request.Limits, diagnostics);
        ValidateRgb(native, numbers.GetValueOrDefault("colors", -1), request.Limits, diagnostics);
        if (checked(environmentBytes + SnapshotSize(flags, numbers, strings)) >
            request.Limits.MaxDescriptionSnapshotBytes)
        {
            throw new ArgumentException("The accepted terminal-description snapshot exceeds its byte limit.");
        }
        var programs = CompilePrograms(strings, diagnostics, out var paddedRequired);
        var keyMap = CreateKeyMap(strings, request.Limits, request.ParserLimits, diagnostics);
        var programSet = new Programs(programs);
        var suitability = flags.GetValueOrDefault("gn")
            ? Suitability.Generic
            : flags.GetValueOrDefault("hc")
                ? Suitability.Hardcopy
                : programSet.FullScreenReady
                    ? Suitability.Usable
                    : paddedRequired
                        ? Suitability.UnsupportedPadding
                        : Suitability.Incomplete;

        if (suitability is Suitability.Incomplete or Suitability.UnsupportedPadding)
        {
            diagnostics.Add(new DescriptionDiagnostic(DescriptionDiagnosticCode.MissingRequired));
        }

        var columns = Positive(numbers.GetValueOrDefault("cols", -1));
        var lines = Positive(numbers.GetValueOrDefault("lines", -1));
        var colors = Positive(numbers.GetValueOrDefault("colors", -1));
        var description = new Description(
            request.TerminalName,
            DescriptionOrigin.Database,
            suitability,
            columns,
            lines,
            colors,
            flags.GetValueOrDefault("am"),
            flags.GetValueOrDefault("bce"),
            flags.GetValueOrDefault("xenl"));
        var capabilities = CreateCapabilities(colors, programs);

        return new TerminalProfile(description, capabilities, programSet, keyMap);
    }

    private static Dictionary<string, bool> ReadFlags(
        INative native,
        List<DescriptionDiagnostic> diagnostics)
    {
        var values = new Dictionary<string, bool>(StringComparer.Ordinal);

        foreach (var name in NcursesNames.Booleans)
        {
            var value = native.GetFlag(name);

            if (value == -1)
            {
                diagnostics.Add(new DescriptionDiagnostic(DescriptionDiagnosticCode.WrongType, name));
            }
            else if (value > 0)
            {
                values.Add(name, true);
            }
        }

        return values;
    }

    private static Dictionary<string, int> ReadNumbers(
        INative native,
        List<DescriptionDiagnostic> diagnostics)
    {
        var values = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var name in NcursesNames.Numbers)
        {
            var value = native.GetNumber(name);

            if (value == -2)
            {
                diagnostics.Add(new DescriptionDiagnostic(DescriptionDiagnosticCode.WrongType, name));
            }
            else if (value >= 0)
            {
                values.Add(name, value);
            }
        }

        return values;
    }

    private static Dictionary<string, byte[]> ReadStrings(
        INative native,
        DescriptionLimits limits,
        List<DescriptionDiagnostic> diagnostics)
    {
        var values = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var acceptedBytes = 0;

        foreach (var name in NcursesNames.Strings)
        {
            var value = native.GetString(name, ProgramLimits.Default.MaxProgramBytes);

            if (value.Status == NativeStringStatus.WrongType)
            {
                diagnostics.Add(new DescriptionDiagnostic(DescriptionDiagnosticCode.WrongType, name));
                continue;
            }

            if (value.Status == NativeStringStatus.OverLimit)
            {
                diagnostics.Add(new DescriptionDiagnostic(DescriptionDiagnosticCode.InvalidProgram, name));
                throw new ArgumentException("A terminal-description string exceeds its configured byte limit.");
            }

            if (value.Status != NativeStringStatus.Present)
            {
                continue;
            }

            acceptedBytes = checked(acceptedBytes + value.Bytes.Length);

            if (acceptedBytes > limits.MaxDescriptionSnapshotBytes)
            {
                throw new ArgumentException("The accepted terminal-description snapshot exceeds its byte limit.");
            }

            values.Add(name, value.Bytes.ToArray());
        }

        return values;
    }

    private static Dictionary<string, DescriptionProgram> CompilePrograms(
        IReadOnlyDictionary<string, byte[]> strings,
        List<DescriptionDiagnostic> diagnostics,
        out bool paddedRequired)
    {
        var programs = new Dictionary<string, DescriptionProgram>(StringComparer.Ordinal);
        paddedRequired = false;

        foreach (var pair in strings)
        {
            if (pair.Key.IsKey() || pair.Key == "RGB")
            {
                continue;
            }

            try
            {
                if (pair.Value.Length == 0)
                {
                    diagnostics.Add(new DescriptionDiagnostic(DescriptionDiagnosticCode.InvalidProgram, pair.Key));
                    continue;
                }

                programs.Add(pair.Key, pair.Value.Compile(ProgramLimits.Default));
            }
            catch (NotSupportedException)
            {
                paddedRequired |= IsRequiredCandidate(pair.Key);
                diagnostics.Add(new DescriptionDiagnostic(DescriptionDiagnosticCode.UnsupportedPadding, pair.Key));
            }
            catch (Exception exception) when (exception is ArgumentException or FormatException)
            {
                diagnostics.Add(new DescriptionDiagnostic(DescriptionDiagnosticCode.InvalidProgram, pair.Key));
            }
        }

        return programs;
    }

    private static KeyMap CreateKeyMap(
        IReadOnlyDictionary<string, byte[]> strings,
        DescriptionLimits limits,
        ParserLimits parserLimits,
        List<DescriptionDiagnostic> diagnostics)
    {
        var bindings = new List<KeyBinding>();
        var rejected = new List<byte[]>();
        var rejectedSignatures = new List<KeySignature>();

        foreach (var pair in strings)
        {
            if (!pair.Key.IsKey() ||
                !pair.Key.TryMapKey(out var code, out var modifiers))
            {
                continue;
            }

            KeyBinding candidate;

            try
            {
                candidate = new KeyBinding(pair.Value, code, modifiers, parserLimits);
            }
            catch (ArgumentException)
            {
                diagnostics.Add(new DescriptionDiagnostic(DescriptionDiagnosticCode.InvalidKey, pair.Key));
                continue;
            }

            if (rejected.Any(value => value.AsSpan().SequenceEqual(pair.Value)) ||
                (candidate.Signature is { } candidateSignature &&
                 rejectedSignatures.Contains(candidateSignature)))
            {
                continue;
            }

            var conflict = bindings.FindIndex(value =>
                (value.Sequence.Span.SequenceEqual(pair.Value) ||
                 (value.Signature is { } valueSignature &&
                  candidate.Signature is { } matchSignature &&
                  valueSignature.Equals(matchSignature))) &&
                (value.Code != code || value.Modifiers != modifiers));

            if (conflict >= 0)
            {
                var removed = bindings[conflict];
                bindings.RemoveAt(conflict);
                rejected.Add(pair.Value);
                rejected.Add(removed.Sequence.ToArray());

                if (candidate.Signature is { } rejectedCandidate)
                {
                    rejectedSignatures.Add(rejectedCandidate);
                }

                if (removed.Signature is { } rejectedRemoved)
                {
                    rejectedSignatures.Add(rejectedRemoved);
                }

                diagnostics.Add(new DescriptionDiagnostic(DescriptionDiagnosticCode.ConflictingKey, pair.Key));
                continue;
            }

            if (bindings.Count >= limits.MaxDescriptionKeyBindings)
            {
                throw new ArgumentException("The terminal-description key map exceeds its binding limit.");
            }

            bindings.Add(candidate);
        }

        return new KeyMap(bindings);
    }

    private static TerminalCapabilities CreateCapabilities(
        int? colors,
        IReadOnlyDictionary<string, DescriptionProgram> programs)
    {
        var supported = new Feature(CapabilitySupport.Supported, Origin.Database);
        var colorDepth = colors switch
        {
            >= 16_777_216 => ColorDepth.TrueColor,
            >= 256 => ColorDepth.Indexed256,
            >= 16 => ColorDepth.Basic16,
            _ => ColorDepth.Monochrome
        };

        return TerminalCapabilities.Conservative with
        {
            ColorDepth = colorDepth,
            ColorOrigin = Origin.Database,
            FocusReporting = HasAll(programs, "fe", "fd", "kxIN", "kxOUT") ? supported : Feature.Unknown,
            BracketedPaste = HasAll(programs, "BE", "BD", "PS", "PE") ? supported : Feature.Unknown,
            CellMouse = HasAll(programs, "XM") && HasAny(programs, "kmous", "xm")
                ? supported
                : Feature.Unknown,
            Osc52 = HasAny(programs, "Ms") ? supported : Feature.Unknown,
            StyledUnderlines = HasAny(programs, "Smulx") ? supported : Feature.Unknown,
            UnderlineColor = HasAny(programs, "Setulc") ? supported : Feature.Unknown,
            Overline = HasAll(programs, "Smol", "Rmol") ? supported : Feature.Unknown
        };
    }

    private static bool HasAll(IReadOnlyDictionary<string, DescriptionProgram> programs, params string[] names) =>
        names.All(name => programs.TryGetValue(name, out var program) && program.Compiled);

    private static bool HasAny(IReadOnlyDictionary<string, DescriptionProgram> programs, params string[] names) =>
        names.Any(name => programs.TryGetValue(name, out var program) && program.Compiled);

    private static bool IsRequiredCandidate(string name) =>
        name is "cup" or "sgr0" or "clear" or "el" or "ed";

    private static int? Positive(int value) => value > 0 ? value : null;

    private static DescriptionResult CreateResult(
        DescriptionLoadStatus status,
        TerminalProfile? profile,
        IReadOnlyList<DescriptionDiagnostic> diagnostics) =>
        profile is not null
            ? DescriptionResult.Loaded(profile, diagnostics)
            : status switch
            {
                DescriptionLoadStatus.MissingOrGeneric => DescriptionResult.MissingOrGeneric(diagnostics),
                DescriptionLoadStatus.PlatformUnavailable => DescriptionResult.PlatformUnavailable(diagnostics),
                DescriptionLoadStatus.ProviderFailed => DescriptionResult.ProviderFailed(diagnostics),
                DescriptionLoadStatus.Loaded => throw new UnreachableException(),
                _ => throw new UnreachableException()
            };

    private static void Cleanup(
        INative native,
        nint previousTerminal,
        bool hasPreviousTerminal,
        bool attemptedSetup,
        bool extendedNames,
        bool hasExtendedNamesState,
        List<DescriptionDiagnostic> diagnostics)
    {
        // Freeing the native library handle is unsafe only once a call that could have mutated
        // ncurses' process-global cur_term was actually attempted and its rollback isn't
        // confirmed. Before that point — most notably when the very first native call
        // (CurrentTerminal) fails before Setup ever runs — nothing was mutated, so disposal stays
        // safe regardless of whether that first read itself succeeded.
        var safeToDispose = true;

        if (attemptedSetup)
        {
            safeToDispose = hasPreviousTerminal;
            var currentTerminal = (nint) 0;

            try
            {
                currentTerminal = native.CurrentTerminal;
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                diagnostics.Add(new DescriptionDiagnostic(DescriptionDiagnosticCode.CleanupFailure));
                safeToDispose = false;
            }

            if (safeToDispose && currentTerminal != previousTerminal)
            {
                try
                {
                    var replaced = native.SetCurrentTerminal(previousTerminal);

                    if (replaced != currentTerminal)
                    {
                        throw new InvalidOperationException("ncurses restored a terminal other than the active lookup terminal.");
                    }
                }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                    diagnostics.Add(new DescriptionDiagnostic(DescriptionDiagnosticCode.CleanupFailure));
                    safeToDispose = false;
                }

                if (safeToDispose && currentTerminal != 0)
                {
                    try
                    {
                        native.DeleteTerminal(currentTerminal);
                    }
                    catch (Exception exception) when (exception is not OutOfMemoryException)
                    {
                        diagnostics.Add(new DescriptionDiagnostic(DescriptionDiagnosticCode.CleanupFailure));
                    }
                }
            }
        }

        if (hasExtendedNamesState)
        {
            try
            {
                _ = native.EnableExtendedNames(extendedNames);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                diagnostics.Add(new DescriptionDiagnostic(DescriptionDiagnosticCode.CleanupFailure));
            }
        }

        if (safeToDispose)
        {
            try
            {
                native.Dispose();
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                diagnostics.Add(new DescriptionDiagnostic(DescriptionDiagnosticCode.CleanupFailure));
            }
        }
    }

    private static int SnapshotSize(
        IReadOnlyDictionary<string, bool> flags,
        IReadOnlyDictionary<string, int> numbers,
        IReadOnlyDictionary<string, byte[]> strings)
    {
        var bytes = 0;

        foreach (var pair in flags)
        {
            bytes = checked(bytes + Encoding.UTF8.GetByteCount(pair.Key) + 1);
        }

        foreach (var pair in numbers)
        {
            bytes = checked(bytes + Encoding.UTF8.GetByteCount(pair.Key) + sizeof(int));
        }

        foreach (var pair in strings)
        {
            bytes = checked(bytes + Encoding.UTF8.GetByteCount(pair.Key) + pair.Value.Length);
        }

        return bytes;
    }

    private static void ValidateRgb(
        INative native,
        int colors,
        DescriptionLimits limits,
        List<DescriptionDiagnostic> diagnostics)
    {
        var flag = native.GetFlag("RGB");
        var number = native.GetNumber("RGB");
        var text = native.GetString("RGB", ProgramLimits.Default.MaxProgramBytes);

        if (text.Status == NativeStringStatus.OverLimit)
        {
            diagnostics.Add(new DescriptionDiagnostic(DescriptionDiagnosticCode.InvalidProgram, "RGB"));
            throw new ArgumentException("The RGB descriptor exceeds its configured byte limit.");
        }

        var valid = (flag > 0 && IsPowerOfTwo(colors)) ||
            (number > 0 && number <= limits.MaxDescriptionRgbComponentBits &&
            DescribesColors(number * 3, colors)) ||
            (text.Status == NativeStringStatus.Present &&
            TryParseRgb(text.Bytes.Span, limits.MaxDescriptionRgbComponentBits, out var totalBits) &&
            DescribesColors(totalBits, colors));
        var present = flag > 0 || number >= 0 || text.Status == NativeStringStatus.Present;

        if (present && !valid)
        {
            diagnostics.Add(new DescriptionDiagnostic(DescriptionDiagnosticCode.InvalidProgram, "RGB"));
        }
    }

    [Pure]
    private static bool TryParseRgb(ReadOnlySpan<byte> value, int maximumBits, out int totalBits)
    {
        totalBits = 0;
        var fields = 0;

        // A loop keyed on "value is not yet empty" cannot see a trailing separator: once the
        // last real field is consumed, slicing past its separator leaves an empty span and the
        // loop simply stops, silently accepting "8/8/8/" as if it were "8/8/8". Looping
        // unconditionally and breaking only when no further separator remains forces a dangling
        // separator to surface as one more (empty, unparsable) field instead.
        while (true)
        {
            var separator = value.IndexOf((byte) '/');
            var field = separator < 0 ? value : value[..separator];

            if (!Utf8Parser.TryParse(field, out int bits, out var consumed) ||
                consumed != field.Length ||
                bits is <= 0 ||
                bits > maximumBits)
            {
                return false;
            }

            totalBits = checked(totalBits + bits);
            fields++;

            if (separator < 0)
            {
                break;
            }

            value = value[(separator + 1)..];
        }

        return fields == 3;
    }

    private static bool DescribesColors(int bits, int colors) =>
        bits is > 0 and < 31 && colors == 1 << bits;

    private static bool IsPowerOfTwo(int value) => value > 0 && (value & (value - 1)) == 0;
}
