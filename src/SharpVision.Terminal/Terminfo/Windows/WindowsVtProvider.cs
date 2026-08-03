// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Terminfo.Windows;

using System.Collections.Immutable;

using Capabilities;

using Input;

/// <summary>Builds the deterministic baseline guaranteed after Windows VT mode is established.</summary>
internal sealed class WindowsVtProvider: IDescriptionProvider
{
    private const string _descriptionName = "windows-vt";
    private const int _colors = 16;

    private static ImmutableArray<WindowsVtProgramSpec> ProgramSpecifications { get; } =
    [
        new("bel", "\a"),
        new("clear", "\u001b[H\u001b[2J"),
        new("cup", "\u001b[%i%p1%d;%p2%dH"),
        new("home", "\u001b[H"),
        new("cud1", "\u001b[B"),
        new("cuu1", "\u001b[A"),
        new("cub1", "\u001b[D"),
        new("cuf1", "\u001b[C"),
        new("cud", "\u001b[%p1%dB"),
        new("cuu", "\u001b[%p1%dA"),
        new("cub", "\u001b[%p1%dD"),
        new("cuf", "\u001b[%p1%dC"),
        new("ed", "\u001b[J"),
        new("el", "\u001b[K"),
        new("el1", "\u001b[1K"),
        new("ech", "\u001b[%p1%dX"),
        new("sgr0", "\u001b[0m"),
        new("bold", "\u001b[1m"),
        new("smul", "\u001b[4m"),
        new("rmul", "\u001b[24m"),
        new("rev", "\u001b[7m"),
        new("smso", "\u001b[7m"),
        new("rmso", "\u001b[27m"),
        new("setaf", "\u001b[%?%p1%{8}%<%t3%p1%d%e%p1%{16}%<%t9%p1%{8}%-%d%e38;5;%p1%d%;m"),
        new("setab", "\u001b[%?%p1%{8}%<%t4%p1%d%e%p1%{16}%<%t10%p1%{8}%-%d%e48;5;%p1%d%;m"),
        new("setrgbf", "\u001b[38;2;%p1%d;%p2%d;%p3%dm"),
        new("setrgbb", "\u001b[48;2;%p1%d;%p2%d;%p3%dm"),
        new("setdf", "\u001b[39m"),
        new("setdb", "\u001b[49m"),
        new("op", "\u001b[39;49m"),
        new("smcup", "\u001b[?1049h"),
        new("rmcup", "\u001b[?1049l"),
        new("civis", "\u001b[?25l"),
        new("cnorm", "\u001b[?25h"),
        new("smkx", "\u001b[?1h\u001b="),
        new("rmkx", "\u001b[?1l\u001b>")
    ];

    private static ImmutableArray<WindowsVtKeySpec> KeySpecifications { get; } =
    [
        new("\u007f", Code.Backspace),
        new("\t", Code.Tab),
        new("\r", Code.Enter),
        new("\u001b[Z", Code.Tab, Modifiers.Shift),
        new("\u001b[A", Code.Up),
        new("\u001b[B", Code.Down),
        new("\u001b[C", Code.Right),
        new("\u001b[D", Code.Left),
        new("\u001b[H", Code.Home),
        new("\u001b[F", Code.End),
        new("\u001b[2~", Code.Insert),
        new("\u001b[3~", Code.Delete),
        new("\u001b[5~", Code.PageUp),
        new("\u001b[6~", Code.PageDown),
        new("\u001b[15~", Code.F5),
        new("\u001b[17~", Code.F6),
        new("\u001b[18~", Code.F7),
        new("\u001b[19~", Code.F8),
        new("\u001b[20~", Code.F9),
        new("\u001b[21~", Code.F10),
        new("\u001b[23~", Code.F11),
        new("\u001b[24~", Code.F12),
        new("\u001bOA", Code.Up),
        new("\u001bOB", Code.Down),
        new("\u001bOC", Code.Right),
        new("\u001bOD", Code.Left),
        new("\u001bOH", Code.Home),
        new("\u001bOF", Code.End),
        new("\u001bOP", Code.F1),
        new("\u001bOQ", Code.F2),
        new("\u001bOR", Code.F3),
        new("\u001bOS", Code.F4)
    ];

    /// <inheritdoc/>
    public DescriptionResult Load(DescriptionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Platform != DescriptionPlatform.Windows || !request.WindowsVirtualTerminal)
        {
            return DescriptionResult.PlatformUnavailable();
        }

        var snapshotBytes = SnapshotBytes();

        if (KeySpecifications.Length > request.Limits.MaxDescriptionKeyBindings ||
            snapshotBytes > request.Limits.MaxDescriptionSnapshotBytes)
        {
            return DescriptionResult.ProviderFailed(
            [
                new DescriptionDiagnostic(DescriptionDiagnosticCode.DescriptionLimit)
            ]);
        }

        try
        {
            var programs = CreatePrograms(ProgramLimits.Default);
            var keyMap = CreateKeyMap();
            var capabilities = TerminalCapabilities.Conservative with
            {
                ColorDepth = ColorDepth.Basic16,
                ColorOrigin = Origin.Default
            };
            var description = new Description(
                _descriptionName,
                DescriptionOrigin.BuiltIn,
                Suitability.Usable,
                colors: _colors,
                automaticMargins: true,
                backColorErase: false);
            var profile = new TerminalProfile(description, capabilities, programs, keyMap);

            return DescriptionResult.Loaded(profile, Array.Empty<DescriptionDiagnostic>());
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or NotSupportedException)
        {
            return DescriptionResult.ProviderFailed(
            [
                new DescriptionDiagnostic(DescriptionDiagnosticCode.InvalidProgram)
            ]);
        }
    }

    private static int SnapshotBytes()
    {
        var bytes = Encoding.UTF8.GetByteCount(_descriptionName);
        bytes = checked(bytes + Encoding.UTF8.GetByteCount("am") + 1);
        bytes = checked(bytes + Encoding.UTF8.GetByteCount("colors") + sizeof(int));

        foreach (var specification in ProgramSpecifications)
        {
            bytes = checked(bytes + Encoding.UTF8.GetByteCount(specification.Name));
            bytes = checked(bytes + Encoding.UTF8.GetByteCount(specification.Source));
        }

        foreach (var specification in KeySpecifications)
        {
            bytes = checked(bytes + Encoding.UTF8.GetByteCount(specification.Sequence));
        }

        return bytes;
    }

    private static Programs CreatePrograms(ProgramLimits limits)
    {
        var compiled = new Dictionary<string, DescriptionProgram>(ProgramSpecifications.Length, StringComparer.Ordinal);

        foreach (var specification in ProgramSpecifications)
        {
            var source = Encoding.UTF8.GetBytes(specification.Source);
            compiled.Add(specification.Name, source.Compile(limits));
        }

        return new Programs(compiled);
    }

    private static KeyMap CreateKeyMap()
    {
        var bindings = new List<KeyBinding>(KeySpecifications.Length);

        foreach (var specification in KeySpecifications)
        {
            var sequence = Encoding.UTF8.GetBytes(specification.Sequence);
            bindings.Add(new KeyBinding(sequence, specification.Code, specification.Modifiers));
        }

        return new KeyMap(bindings);
    }
}
