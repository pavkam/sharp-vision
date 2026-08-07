// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Rendering;

using SharpVision.Terminal.Capabilities;

/// <summary>
/// Verifies deterministic exact bytes for full and incremental frame encoding; rendering through
/// compiled terminal-description programs; incremental output reaching the same semantic terminal
/// state as full output; and fixed-seed incremental/full equivalence across random states.
/// </summary>
public sealed class FrameEncoderTests
{
    private const int _seed = 0xD1FF;

    private static TerminalCapabilities TrueColorCapabilities { get; } =
        TerminalCapabilities.Conservative with { ColorDepth = ColorDepth.TrueColor };

    /// <summary>Provides exact foreground/background degradation for every color tier.</summary>
    public static TheoryData<ColorDepth, string> ColorDepthCases => new()
    {
        {
            ColorDepth.TrueColor,
            "\u001b]8;;\u001b\\\u001b[0m\u001b[1;1H\u001b[2J\u001b[1;1H\u001b[38;2;95;135;175m\u001b[48;2;255;0;0mx\u001b[0m\u001b[1;1H\u001b[?25l"
        },
        { ColorDepth.Indexed256, "\u001b]8;;\u001b\\\u001b[0m\u001b[1;1H\u001b[2J\u001b[1;1H\u001b[38;5;67m\u001b[48;5;9mx\u001b[0m\u001b[1;1H\u001b[?25l" },
        { ColorDepth.Basic16, "\u001b]8;;\u001b\\\u001b[0m\u001b[1;1H\u001b[2J\u001b[1;1H\u001b[90m\u001b[101mx\u001b[0m\u001b[1;1H\u001b[?25l" },
        { ColorDepth.Monochrome, "\u001b]8;;\u001b\\\u001b[0m\u001b[1;1H\u001b[2J\u001b[1;1Hx\u001b[1;1H\u001b[?25l" }
    };

    /// <summary>Verifies semantic colors project to exact bytes at every capability tier.</summary>
    /// <param name="depth">The active color fidelity.</param>
    /// <param name="expected">The complete expected frame output.</param>
    [Theory]
    [MemberData(nameof(ColorDepthCases))]
    public void Encode_WhenColorDepthChanges_WritesHighestSupportedRepresentation(
        ColorDepth depth,
        string expected)
    {
        using Frame back = new(new Size(1, 1));
        var style = new CellStyle(Color.Rgb(95, 135, 175), Color.Rgb(255, 0, 0));
        _ = back.Canvas.Draw("x", default, style);
        var destination = new ArrayBufferWriter<byte>();
        var capabilities = TerminalCapabilities.Conservative with { ColorDepth = depth };

        _ = FrameEncoder.Encode(null, back, destination, capabilities);

        destination.WrittenSpan.ToArray().ShouldBe(Encoding.ASCII.GetBytes(expected));
    }

    /// <summary>Verifies RGB colors collapsing to one basic color emit one transition.</summary>
    [Fact]
    public void Encode_WhenRgbColorsProjectEqually_DoesNotEmitRedundantTransition()
    {
        using Frame back = new(new Size(2, 1));
        _ = back.Canvas.Draw("a", default, new CellStyle(Color.Rgb(255, 0, 0)));
        _ = back.Canvas.Draw("b", new Point(1, 0), new CellStyle(Color.Rgb(250, 5, 5)));
        var destination = new ArrayBufferWriter<byte>();

        _ = FrameEncoder.Encode(
            null,
            back,
            destination,
            TerminalCapabilities.Conservative);

        destination.WrittenSpan.ToArray().ShouldBe(
            "\u001b]8;;\u001b\\\u001b[0m\u001b[1;1H\u001b[2J\u001b[1;1H\u001b[91mab\u001b[0m\u001b[1;1H\u001b[?25l"u8.ToArray());
    }

    /// <summary>Verifies a missing capability snapshot fails before destination mutation.</summary>
    [Fact]
    public void Encode_WhenCapabilitiesAreNull_ThrowsBeforeWriting()
    {
        using var back = Create("x");
        var destination = new ArrayBufferWriter<byte>();

        _ = Should.Throw<ArgumentNullException>(() =>
            FrameEncoder.Encode(null, back, destination, (TerminalCapabilities) null!));

        destination.WrittenCount.ShouldBe(0);
    }

    /// <summary>Verifies unknown optional decoration support uses conservative fallbacks.</summary>
    [Fact]
    public void Encode_WhenModernDecorationsAreUnknown_DegradesWithoutUnsupportedBytes()
    {
        using Frame back = new(new Size(1, 1));
        var style = new CellStyle(
            attributes: TerminalAttributes.RapidBlink | TerminalAttributes.Overline,
            underline: Underline.Curly,
            underlineColor: Color.Rgb(1, 2, 3));
        _ = back.Canvas.Draw("x", default, style);
        var destination = new ArrayBufferWriter<byte>();

        _ = FrameEncoder.Encode(
            null,
            back,
            destination,
            TrueColorCapabilities);

        destination.WrittenSpan.ToArray().ShouldBe(
            "\u001b]8;;\u001b\\\u001b[0m\u001b[1;1H\u001b[2J\u001b[1;1H\u001b[6m\u001b[4mx\u001b[0m\u001b[1;1H\u001b[?25l"u8.ToArray());
    }

    /// <summary>Verifies proven optional decoration support emits exact modern SGR.</summary>
    [Fact]
    public void Encode_WhenModernDecorationsAreSupported_WritesExactBytes()
    {
        using Frame back = new(new Size(1, 1));
        var style = new CellStyle(
            attributes: TerminalAttributes.RapidBlink | TerminalAttributes.Overline,
            underline: Underline.Curly,
            underlineColor: Color.Rgb(1, 2, 3));
        _ = back.Canvas.Draw("x", default, style);
        var destination = new ArrayBufferWriter<byte>();
        var supported = new Feature(CapabilitySupport.Supported, Origin.Override);
        var capabilities = TrueColorCapabilities with
        {
            StyledUnderlines = supported,
            UnderlineColor = supported,
            Overline = supported
        };

        _ = FrameEncoder.Encode(null, back, destination, capabilities);

        destination.WrittenSpan.ToArray().ShouldBe(
            "\u001b]8;;\u001b\\\u001b[0m\u001b[1;1H\u001b[2J\u001b[1;1H\u001b[6m\u001b[53m\u001b[4:3m\u001b[58;2;1;2;3mx"u8.ToArray()
                .Concat("\u001b[0m\u001b[1;1H\u001b[?25l"u8.ToArray())
                .ToArray());
    }

    /// <summary>Verifies an orphan component is emitted as an independent replacement cell.</summary>
    [Fact]
    public void Encode_WhenFrameContainsOrphanMark_WritesReplacementBytes()
    {
        // Arrange
        using Frame back = new(new Size(2, 1));
        _ = back.Canvas.Draw("a".AsSpan(), new Point(0, 0));
        _ = back.Canvas.Draw("\u0301".AsSpan(), new Point(1, 0));
        var destination = new ArrayBufferWriter<byte>();

        // Act
        _ = FrameEncoder.Encode(null, back, destination, TrueColorCapabilities);

        // Assert
        destination.WrittenSpan.ToArray().ShouldBe(
            "\u001b]8;;\u001b\\\u001b[0m\u001b[1;1H\u001b[2J\u001b[1;1Ha�\u001b[1;1H\u001b[?25l"u8.ToArray());
        destination.WrittenSpan.IndexOf("\u0301"u8).ShouldBe(-1);
    }

    /// <summary>
    /// Verifies a complete default-style frame emits exact position/text/cursor bytes.
    /// </summary>
    [Fact]
    public void Encode_WhenFrameIsFull_WritesExactBytes()
    {
        using var back = Create("ab");
        var destination = new ArrayBufferWriter<byte>();

        var result = FrameEncoder.Encode(null, back, destination, TrueColorCapabilities);

        destination.WrittenSpan.ToArray().ShouldBe(
            "\u001b]8;;\u001b\\\u001b[0m\u001b[1;1H\u001b[2J\u001b[1;1Hab\u001b[1;1H\u001b[?25l"u8.ToArray());
        result.ShouldBe(new EncodeResult(1, true));
    }

    /// <summary>
    /// Verifies a sparse change emits only its run and restores cursor position.
    /// </summary>
    [Fact]
    public void Encode_WhenOneCellChanges_WritesSparseRun()
    {
        using var front = Create("ab");
        using var back = Create("ac");
        var destination = new ArrayBufferWriter<byte>();

        var result = FrameEncoder.Encode(front, back, destination, TrueColorCapabilities);

        destination.WrittenSpan.ToArray().ShouldBe(
            "\u001b[1;2Hc\u001b[1;1H"u8.ToArray());
        result.Full.ShouldBeFalse();
        result.Spans.ShouldBe(1);
    }

    /// <summary>
    /// Verifies equal content and cursor state emits no bytes.
    /// </summary>
    [Fact]
    public void Encode_WhenFrameIsUnchanged_WritesNothing()
    {
        using var front = Create("ab");
        using var back = Create("ab");
        var destination = new ArrayBufferWriter<byte>();

        var result = FrameEncoder.Encode(front, back, destination, TrueColorCapabilities);

        destination.WrittenCount.ShouldBe(0);
        result.ShouldBe(new EncodeResult(0, false));
    }

    /// <summary>
    /// Verifies style and hyperlink transitions close and reset to known state.
    /// </summary>
    [Fact]
    public void Encode_WhenCellIsStyled_WritesExactTransitions()
    {
        using Frame back = new(new Size(1, 1));
        var style = new CellStyle(
            attributes: TerminalAttributes.Bold,
            hyperlink: "https://example.test");
        _ = back.Canvas.Draw("x".AsSpan(), new Point(0, 0), style);
        var destination = new ArrayBufferWriter<byte>();

        _ = FrameEncoder.Encode(null, back, destination, TrueColorCapabilities);

        destination.WrittenSpan.ToArray().ShouldBe(
            Encoding.UTF8.GetBytes(
                "\u001b]8;;\u001b\\" +
                "\u001b[0m\u001b[1;1H\u001b[2J" +
                "\u001b[1;1H" +
                "\u001b]8;;https://example.test\u001b\\" +
                "\u001b[1m" +
                "x" +
                "\u001b]8;;\u001b\\" +
                "\u001b[0m" +
                "\u001b[1;1H" +
                "\u001b[?25l"));
    }

    /// <summary>
    /// Verifies the requested visible cursor state is restored after drawing.
    /// </summary>
    [Fact]
    public void Encode_WhenCursorIsVisible_RestoresPositionAndVisibility()
    {
        using var back = Create("ab");
        back.SetCursor(new Point(1, 0), visible: true);
        var destination = new ArrayBufferWriter<byte>();

        _ = FrameEncoder.Encode(null, back, destination, TrueColorCapabilities);

        destination.WrittenSpan.EndsWith("\u001b[1;2H\u001b[?25h"u8).ShouldBeTrue();
    }

    private static Frame Create(string value)
    {
        var frame = new Frame(new Size(value.Length, 1));
        _ = frame.Canvas.Draw(value.AsSpan(), new Point(0, 0));
        return frame;
    }

    /// <summary>Verifies one-sided cursor visibility programs cannot create unrestorable state.</summary>
    /// <param name="program">The only retained visibility program.</param>
    /// <param name="visible">The requested cursor visibility.</param>
    [Theory]
    [InlineData("civis", false)]
    [InlineData("cnorm", true)]
    public void Encode_WhenCursorVisibilityPairIsIncomplete_OmitsBothPrograms(string program, bool visible)
    {
        using Frame frame = new(new Size(1, 1));
        _ = frame.Canvas.Draw("x", default);
        frame.SetCursor(default, visible);
        var programs = CorePrograms();
        _ = programs.Remove("civis");
        _ = programs.Remove("cnorm");
        programs[program] = new DescriptionProgram(program == "civis" ? "HIDE"u8 : "SHOW"u8);
        var profile = CreateProfile(ColorDepth.Monochrome, programs);
        var destination = new ArrayBufferWriter<byte>();

        _ = FrameEncoder.Encode(null, frame, destination, profile);

        destination.WrittenSpan.IndexOf("HIDE"u8).ShouldBe(-1);
        destination.WrittenSpan.IndexOf("SHOW"u8).ShouldBe(-1);
    }

    /// <summary>Verifies non-executable optional contracts neither fault nor project semantic state.</summary>
    [Fact]
    public void Encode_WhenOptionalProgramsCannotExecute_DegradesWithoutOutputOrFault()
    {
        using Frame frame = new(new Size(1, 1));
        _ = frame.Canvas.Draw(
            "x",
            default,
            new CellStyle(
                ReferenceColors.Get(4),
                attributes: TerminalAttributes.Bold | TerminalAttributes.Underline,
                underlineColor: ReferenceColors.Get(2)));
        frame.SetCursor(default, visible: true, CursorShape.Bar);
        using Frame expected = new(new Size(1, 1));
        _ = expected.Canvas.Draw("x", default);
        expected.SetCursor(default, visible: true, CursorShape.Block);
        var programs = CorePrograms();
        programs["bold"] = new DescriptionProgram("%{1}%PA"u8);
        programs["setaf"] = new DescriptionProgram("WRONG-COLOR"u8);
        programs["setab"] = new DescriptionProgram("\u001b[48;5;%p1%dm"u8);
        programs["op"] = new DescriptionProgram("%p1%d"u8);
        programs["el"] = new DescriptionProgram("%{1}%PA"u8);
        programs["Ss"] = new DescriptionProgram("WRONG-SHAPE"u8);
        programs["Se"] = new DescriptionProgram("\u001b[0 q"u8);
        programs["Setulc"] = new DescriptionProgram("%{1}%PA"u8);
        var supported = new Feature(CapabilitySupport.Supported, Origin.Database);
        var profile = new TerminalProfile(
            new Description(
                "invalid-optional",
                DescriptionOrigin.Database,
                Suitability.Usable,
                colors: 256,
                backColorErase: true),
            TerminalCapabilities.Conservative with
            {
                ColorDepth = ColorDepth.Indexed256,
                UnderlineColor = supported
            },
            new Programs(programs),
            KeyMap.Empty);
        var destination = new ArrayBufferWriter<byte>();

        _ = FrameEncoder.Encode(null, frame, destination, profile);

        destination.WrittenSpan.IndexOf("WRONG-COLOR"u8).ShouldBe(-1);
        destination.WrittenSpan.IndexOf("WRONG-SHAPE"u8).ShouldBe(-1);
        var screen = new VirtualScreen(frame.Size);
        screen.Apply(destination.WrittenSpan);
        screen.ShouldMatch(expected);
    }

    /// <summary>Verifies an actual conditional color miss does not advance projected encoder state.</summary>
    [Fact]
    public void Encode_WhenActualColorExpansionIsEmpty_RetainsDefaultProjectedState()
    {
        using Frame frame = new(new Size(2, 1));
        _ = frame.Canvas.Draw("x", default, new CellStyle(ReferenceColors.Get(2)));
        _ = frame.Canvas.Draw("y", new Point(1, 0));
        using Frame expected = new(new Size(2, 1));
        _ = expected.Canvas.Draw("xy", default);
        var programs = CorePrograms();
        programs["setaf"] = new DescriptionProgram("%?%p1%{1}%=%t\u001b[38;5;1m%;"u8);
        programs["setab"] = new DescriptionProgram("\u001b[48;5;%p1%dm"u8);
        var profile = CreateProfile(ColorDepth.Indexed256, programs);
        var destination = new ArrayBufferWriter<byte>();

        _ = FrameEncoder.Encode(null, frame, destination, profile);

        var firstReset = destination.WrittenSpan.IndexOf("\u001b[0m"u8);
        firstReset.ShouldBeGreaterThanOrEqualTo(0);
        destination.WrittenSpan[(firstReset + 4)..].IndexOf("\u001b[0m"u8).ShouldBe(-1);
        var screen = new VirtualScreen(frame.Size);
        screen.Apply(destination.WrittenSpan);
        screen.ShouldMatch(expected);
    }

    /// <summary>Verifies full redraw first resets inherited rendition and clears inherited cells.</summary>
    [Fact]
    public void Encode_WhenTerminalStateIsUnknown_ResetsAndClearsBeforeDrawing()
    {
        using Frame frame = new(new Size(2, 1));
        _ = frame.Canvas.Draw("ok", default);
        var profile = CreateProfile(ColorDepth.Monochrome, new Dictionary<string, DescriptionProgram>
        {
            ["cup"] = new DescriptionProgram("\u001b[%i%p1%d;%p2%df"u8),
            ["sgr0"] = new DescriptionProgram("\u001b[m"u8),
            ["clear"] = new DescriptionProgram("\u001b[2J\u001b[H"u8),
            ["civis"] = new DescriptionProgram("\u001b[?25l"u8),
            ["cnorm"] = new DescriptionProgram("\u001b[?25h"u8)
        });
        var destination = new ArrayBufferWriter<byte>();
        var screen = new VirtualScreen(frame.Size);
        screen.Apply("\u001b[1;1H\u001b[1mzz"u8);

        _ = FrameEncoder.Encode(null, frame, destination, profile);

        destination.WrittenSpan.StartsWith("\u001b]8;;\u001b\\\u001b[m\u001b[2J\u001b[H"u8).ShouldBeTrue();
        screen.Apply(destination.WrittenSpan);
        screen.ShouldMatch(frame);
    }

    /// <summary>Verifies a usable description without clear resets, homes, and erases through cup plus ed.</summary>
    [Fact]
    public void Encode_WhenFullScreenProfileHasElAndEdWithoutClear_UsesExactEraseFallback()
    {
        using Frame frame = new(new Size(2, 1));
        _ = frame.Canvas.Draw("ok", default);
        var profile = CreateProfile(ColorDepth.Monochrome, new Dictionary<string, DescriptionProgram>
        {
            ["cup"] = new DescriptionProgram("\u001b[%i%p1%d;%p2%df"u8),
            ["sgr0"] = new DescriptionProgram("\u001b[m"u8),
            ["el"] = new DescriptionProgram("\u001b[K"u8),
            ["ed"] = new DescriptionProgram("\u001b[2J"u8),
            ["civis"] = new DescriptionProgram("\u001b[?25l"u8),
            ["cnorm"] = new DescriptionProgram("\u001b[?25h"u8)
        });
        var destination = new ArrayBufferWriter<byte>();
        var screen = new VirtualScreen(frame.Size);
        screen.Apply("\u001b[1;1H\u001b[1mzz"u8);

        _ = FrameEncoder.Encode(null, frame, destination, profile);

        destination.WrittenSpan.StartsWith("\u001b]8;;\u001b\\\u001b[m\u001b[1;1f\u001b[2J"u8).ShouldBeTrue();
        screen.Apply(destination.WrittenSpan);
        screen.ShouldMatch(frame);
    }

    /// <summary>Verifies an advertised tier without complete foreground/background programs degrades safely.</summary>
    [Fact]
    public void Encode_WhenColorProgramsAreOneSided_DegradesToMonochromeState()
    {
        using Frame frame = new(new Size(1, 1));
        _ = frame.Canvas.Draw(
            "x",
            default,
            new CellStyle(Color.Rgb(1, 2, 3), Color.Rgb(4, 5, 6)));
        using Frame expected = new(new Size(1, 1));
        _ = expected.Canvas.Draw("x", default);
        var profile = CreateProfile(ColorDepth.TrueColor, new Dictionary<string, DescriptionProgram>
        {
            ["cup"] = new DescriptionProgram("\u001b[%i%p1%d;%p2%dH"u8),
            ["sgr0"] = new DescriptionProgram("\u001b[0m"u8),
            ["clear"] = new DescriptionProgram("\u001b[2J"u8),
            ["setrgbf"] = new DescriptionProgram("\u001b[38;2;%p1%d;%p2%d;%p3%dm"u8),
            ["civis"] = new DescriptionProgram("\u001b[?25l"u8),
            ["cnorm"] = new DescriptionProgram("\u001b[?25h"u8)
        });
        var destination = new ArrayBufferWriter<byte>();

        _ = FrameEncoder.Encode(null, frame, destination, profile);

        destination.WrittenSpan.IndexOf("38;2"u8).ShouldBe(-1);
        var screen = new VirtualScreen(frame.Size);
        screen.Apply(destination.WrittenSpan);
        screen.ShouldMatch(expected);
    }

    /// <summary>Verifies complete direct and indexed pairs preserve true-color foreground and background.</summary>
    [Fact]
    public void Encode_WhenTrueColorProgramsAreComplete_PreservesFinalColorState()
    {
        using Frame frame = new(new Size(1, 1));
        _ = frame.Canvas.Draw(
            "x",
            default,
            new CellStyle(Color.Rgb(1, 2, 3), Color.Rgb(4, 5, 6)));
        var profile = CreateProfile(ColorDepth.TrueColor, ColorPrograms());
        var destination = new ArrayBufferWriter<byte>();

        _ = FrameEncoder.Encode(null, frame, destination, profile);

        var screen = new VirtualScreen(frame.Size);
        screen.Apply(destination.WrittenSpan);
        screen.ShouldMatch(frame);
    }

    /// <summary>Verifies non-canonical compiled cursor, rendition, and color programs drive observable state.</summary>
    [Fact]
    public void Encode_WhenDescriptionProgramsAreNonCanonical_AppliesTheirTerminalSemantics()
    {
        using Frame frame = new(new Size(2, 1));
        _ = frame.Canvas.Draw(
            "xy",
            default,
            new CellStyle(ReferenceColors.Get(9), attributes: TerminalAttributes.Bold));
        frame.SetCursor(new Point(1, 0), visible: true, CursorShape.Underline);
        var profile = CreateProfile(
            ColorDepth.Indexed256,
            new Dictionary<string, DescriptionProgram>
            {
                ["cup"] = new DescriptionProgram("\u001b[%i%p1%d;%p2%df"u8),
                ["sgr0"] = new DescriptionProgram("\u001b[0m"u8),
                ["clear"] = new DescriptionProgram("\u001b[2J"u8),
                ["bold"] = new DescriptionProgram("\u001b[1m"u8),
                ["setaf"] = new DescriptionProgram("\u001b[38;5;%p1%dm"u8),
                ["setab"] = new DescriptionProgram("\u001b[48;5;%p1%dm"u8),
                ["civis"] = new DescriptionProgram("\u001b[?25l"u8),
                ["cnorm"] = new DescriptionProgram("\u001b[?25h"u8),
                ["Ss"] = new DescriptionProgram("\u001b[%p1%d q"u8),
                ["Se"] = new DescriptionProgram("\u001b[0 q"u8)
            });
        var destination = new ArrayBufferWriter<byte>();

        _ = FrameEncoder.Encode(null, frame, destination, profile);

        destination.WrittenSpan.IndexOf("\u001b[1;1f\u001b[1m\u001b[38;5;9mxy"u8)
            .ShouldBeGreaterThanOrEqualTo(0);
        destination.WrittenSpan.EndsWith("\u001b[1;2f\u001b[?25h\u001b[4 q"u8).ShouldBeTrue();
        var screen = new VirtualScreen(frame.Size);
        screen.Apply(destination.WrittenSpan);
        screen.ShouldMatch(frame);
    }

    /// <summary>Verifies direct-color grammar is ignored below the true-color semantic tier.</summary>
    [Fact]
    public void Encode_WhenRgbProgramExistsButProfileIsBasic16_UsesIndexedProgramWithProjectedColor()
    {
        using Frame frame = new(new Size(1, 1));
        _ = frame.Canvas.Draw("x", default, new CellStyle(Color.Rgb(255, 0, 0)));
        var profile = CreateProfile(
            ColorDepth.Basic16,
            new Dictionary<string, DescriptionProgram>
            {
                ["cup"] = new DescriptionProgram("\u001b[%i%p1%d;%p2%dH"u8),
                ["sgr0"] = new DescriptionProgram("\u001b[0m"u8),
                ["clear"] = new DescriptionProgram("\u001b[2J"u8),
                ["setaf"] = new DescriptionProgram("\u001b[38;5;%p1%dm"u8),
                ["setab"] = new DescriptionProgram("\u001b[48;5;%p1%dm"u8),
                ["setrgbf"] = new DescriptionProgram("RGB(%p1%d,%p2%d,%p3%d)"u8)
            });
        var destination = new ArrayBufferWriter<byte>();

        _ = FrameEncoder.Encode(null, frame, destination, profile);

        destination.WrittenSpan.IndexOf("\u001b[38;5;9m"u8).ShouldBeGreaterThanOrEqualTo(0);
        destination.WrittenSpan.IndexOf("RGB("u8).ShouldBe(-1);
    }

    /// <summary>Verifies a style without a corresponding description program is omitted.</summary>
    [Fact]
    public void Encode_WhenItalicProgramIsAbsent_OmitsItalicBytes()
    {
        using Frame frame = new(new Size(1, 1));
        _ = frame.Canvas.Draw("x", default, new CellStyle(attributes: TerminalAttributes.Italic));
        var profile = CreateProfile(ColorDepth.Monochrome, new Dictionary<string, DescriptionProgram>
        {
            ["cup"] = new DescriptionProgram("\u001b[%i%p1%d;%p2%dH"u8),
            ["sgr0"] = new DescriptionProgram("\u001b[0m"u8),
            ["clear"] = new DescriptionProgram("\u001b[2J"u8)
        });
        var destination = new ArrayBufferWriter<byte>();

        _ = FrameEncoder.Encode(null, frame, destination, profile);

        destination.WrittenSpan.IndexOf("\u001b[3m"u8).ShouldBe(-1);
    }

    /// <summary>Verifies BCE permits an exact trailing erase while absent BCE emits explicit spaces.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Encode_WhenTrailingCellsAreBlank_UsesEraseOnlyWithBce(bool backColorErase)
    {
        using Frame frame = new(new Size(3, 1));
        frame.Clear(new CellStyle(background: ReferenceColors.Get(4)));
        var profile = CreateProfile(
            ColorDepth.Indexed256,
            new Dictionary<string, DescriptionProgram>
            {
                ["cup"] = new DescriptionProgram("\u001b[%i%p1%d;%p2%dH"u8),
                ["sgr0"] = new DescriptionProgram("\u001b[0m"u8),
                ["clear"] = new DescriptionProgram("\u001b[2J"u8),
                ["setab"] = new DescriptionProgram("\u001b[48;5;%p1%dm"u8),
                ["setaf"] = new DescriptionProgram("\u001b[38;5;%p1%dm"u8),
                ["el"] = new DescriptionProgram("\u001b[0K"u8),
                ["civis"] = new DescriptionProgram("\u001b[?25l"u8),
                ["cnorm"] = new DescriptionProgram("\u001b[?25h"u8)
            },
            backColorErase: backColorErase);
        var destination = new ArrayBufferWriter<byte>();

        _ = FrameEncoder.Encode(null, frame, destination, profile);

        (destination.WrittenSpan.IndexOf("\u001b[0K"u8) >= 0).ShouldBe(backColorErase);
        (destination.WrittenSpan.IndexOf("   "u8) >= 0).ShouldBe(!backColorErase);
        var screen = new VirtualScreen(frame.Size);
        screen.Apply(destination.WrittenSpan);
        screen.ShouldMatch(frame);
    }

    /// <summary>Verifies a wide owner ending in the final column is followed by an absolute wrap-state repair.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Encode_WhenWideOwnerEndsAtMargin_PreservesScreenWithoutWrap(
        bool automaticMargins,
        bool eatNewlineGlitch)
    {
        using Frame frame = new(new Size(3, 1));
        _ = frame.Canvas.Draw("a界", default);
        var profile = CreateProfile(
            ColorDepth.Monochrome,
            new Dictionary<string, DescriptionProgram>
            {
                ["cup"] = new DescriptionProgram("\u001b[%i%p1%d;%p2%dH"u8),
                ["sgr0"] = new DescriptionProgram("\u001b[0m"u8),
                ["clear"] = new DescriptionProgram("\u001b[2J"u8),
                ["civis"] = new DescriptionProgram("\u001b[?25l"u8),
                ["cnorm"] = new DescriptionProgram("\u001b[?25h"u8)
            },
            automaticMargins: automaticMargins,
            eatNewlineGlitch: eatNewlineGlitch);
        var destination = new ArrayBufferWriter<byte>();

        _ = FrameEncoder.Encode(null, frame, destination, profile);

        (destination.WrittenSpan.IndexOf("界\u001b[1;3H"u8) >= 0).ShouldBe(automaticMargins);
        var screen = new VirtualScreen(frame.Size, automaticMargins, eatNewlineGlitch);
        screen.Apply(destination.WrittenSpan);
        screen.ShouldMatch(frame);
    }

    /// <summary>Verifies described blink deliberately degrades rapid blink to the supported rendition.</summary>
    [Fact]
    public void Encode_WhenOnlySlowBlinkIsDescribed_DegradesRapidBlinkState()
    {
        using Frame frame = new(new Size(1, 1));
        _ = frame.Canvas.Draw("x", default, new CellStyle(attributes: TerminalAttributes.RapidBlink));
        using Frame expected = new(new Size(1, 1));
        _ = expected.Canvas.Draw("x", default, new CellStyle(attributes: TerminalAttributes.Blink));
        var programs = CorePrograms();
        programs["blink"] = new DescriptionProgram("\u001b[5m"u8);
        var profile = CreateProfile(ColorDepth.Monochrome, programs);
        var destination = new ArrayBufferWriter<byte>();

        _ = FrameEncoder.Encode(null, frame, destination, profile);

        var screen = new VirtualScreen(frame.Size);
        screen.Apply(destination.WrittenSpan);
        screen.ShouldMatch(expected);
    }

    /// <summary>Verifies every unavailable optional rendition is absent from final terminal state.</summary>
    [Fact]
    public void Encode_WhenOptionalRenditionsAreUnavailable_OmitsTheirState()
    {
        using Frame frame = new(new Size(1, 1));
        _ = frame.Canvas.Draw(
            "x",
            default,
            new CellStyle(attributes:
                TerminalAttributes.Italic |
                TerminalAttributes.Blink |
                TerminalAttributes.Hidden |
                TerminalAttributes.Strike |
                TerminalAttributes.Overline));
        using Frame expected = new(new Size(1, 1));
        _ = expected.Canvas.Draw("x", default);
        var profile = CreateProfile(ColorDepth.Monochrome, CorePrograms());
        var destination = new ArrayBufferWriter<byte>();

        _ = FrameEncoder.Encode(null, frame, destination, profile);

        var screen = new VirtualScreen(frame.Size);
        screen.Apply(destination.WrittenSpan);
        screen.ShouldMatch(expected);
    }

    /// <summary>Verifies every semantic cursor shape reaches the independent model through Ss/Se.</summary>
    [Theory]
    [InlineData(CursorShape.Block)]
    [InlineData(CursorShape.Underline)]
    [InlineData(CursorShape.Bar)]
    public void Encode_WhenCursorShapeIsSupported_PreservesFinalShape(CursorShape shape)
    {
        using Frame frame = new(new Size(1, 1));
        _ = frame.Canvas.Draw("x", default);
        frame.SetCursor(default, visible: true, shape);
        var programs = CorePrograms();
        programs["Ss"] = new DescriptionProgram("\u001b[%p1%d q"u8);
        programs["Se"] = new DescriptionProgram("\u001b[0 q"u8);
        var profile = CreateProfile(ColorDepth.Monochrome, programs);
        var destination = new ArrayBufferWriter<byte>();

        _ = FrameEncoder.Encode(null, frame, destination, profile);

        var screen = new VirtualScreen(frame.Size);
        screen.Apply(destination.WrittenSpan);
        screen.ShouldMatch(frame);
    }

    /// <summary>Verifies conditional cursor-shape programs cannot commit a shape after an actual miss.</summary>
    /// <param name="failingProgram">The conditional shape program whose representative expansion succeeds.</param>
    [Theory]
    [InlineData("Ss")]
    [InlineData("Se")]
    public void Encode_WhenCursorShapeProgramIsConditional_OmitsShapePair(string failingProgram)
    {
        using Frame first = new(new Size(1, 1));
        _ = first.Canvas.Draw("x", default, new CellStyle(ReferenceColors.Get(5)));
        first.SetCursor(default, visible: true, CursorShape.Underline);
        using Frame second = new(new Size(1, 1));
        _ = second.Canvas.Draw("x", default, new CellStyle(ReferenceColors.Get(9)));
        second.SetCursor(
            default,
            visible: true,
            failingProgram == "Ss" ? CursorShape.Bar : CursorShape.Block);
        var programs = CorePrograms();
        programs["setaf"] = new DescriptionProgram("%p1%PA\u001b[38;5;%p1%dm"u8);
        programs["setab"] = new DescriptionProgram("\u001b[48;5;%p1%dm"u8);
        programs["Ss"] = failingProgram == "Ss"
            ? new DescriptionProgram("%?%p1%{4}%=%t\u001b[4 q%;"u8)
            : new DescriptionProgram("\u001b[%p1%d q"u8);
        programs["Se"] = failingProgram == "Se"
            ? new DescriptionProgram("%?%gA%{0}%=%t\u001b[0 q%;"u8)
            : new DescriptionProgram("\u001b[0 q"u8);
        var profile = CreateProfile(ColorDepth.Indexed256, programs);
        var interpreter = new Interpreter(ProgramLimits.Default);
        var destination = new ArrayBufferWriter<byte>();

        _ = FrameEncoder.Encode(null, first, destination, profile, interpreter);
        _ = FrameEncoder.Encode(first, second, destination, profile, interpreter);

        var screen = new VirtualScreen(second.Size);
        screen.Apply(destination.WrittenSpan);
        using Frame expected = new(new Size(1, 1));
        _ = expected.Canvas.Draw("x", default, new CellStyle(ReferenceColors.Get(9)));
        expected.SetCursor(default, visible: true, CursorShape.Block);
        screen.ShouldMatch(expected);
    }

    /// <summary>Verifies Setulc receives resolved RGB for indexed and basic projections.</summary>
    [Theory]
    [InlineData(ColorDepth.Basic16, 9)]
    [InlineData(ColorDepth.Indexed256, 67)]
    public void Encode_WhenUnderlineColorIsIndexed_PacksResolvedRgb(
        ColorDepth depth,
        int index)
    {
        using Frame frame = new(new Size(1, 1));
        _ = frame.Canvas.Draw(
            "x",
            default,
            new CellStyle(
                attributes: TerminalAttributes.Underline,
                underlineColor: ReferenceColors.Get(index)));
        var programs = ColorPrograms();
        programs["smul"] = new DescriptionProgram("\u001b[4m"u8);
        programs["Setulc"] = new DescriptionProgram("P%p1%d"u8);
        var supported = new Feature(CapabilitySupport.Supported, Origin.Override);
        var profile = new TerminalProfile(
            new Description("underline", DescriptionOrigin.Database, Suitability.Usable, colors: 256),
            TerminalCapabilities.Conservative with
            {
                ColorDepth = depth,
                UnderlineColor = supported
            },
            new Programs(programs),
            KeyMap.Empty);
        var destination = new ArrayBufferWriter<byte>();
        var resolved = ReferenceColors.Get(index);
        var packed = (resolved.Red << 16) | (resolved.Green << 8) | resolved.Blue;

        _ = FrameEncoder.Encode(null, frame, destination, profile);

        destination.WrittenSpan.IndexOf(Encoding.ASCII.GetBytes($"P{packed}"))
            .ShouldBeGreaterThanOrEqualTo(0);
    }

    /// <summary>Verifies described typed underline, packed underline color, and default-color programs are expanded.</summary>
    [Fact]
    public void Encode_WhenDescriptionSuppliesDecorationAndDefaultPrograms_UsesExactPrograms()
    {
        using Frame frame = new(new Size(1, 1));
        _ = frame.Canvas.Draw(
            "x",
            default,
            new CellStyle(
                background: ReferenceColors.Get(1),
                underline: Underline.Curly,
                underlineColor: Color.Rgb(1, 2, 3)));
        var supported = new Feature(CapabilitySupport.Supported, Origin.Override);
        var programs = new Dictionary<string, DescriptionProgram>
        {
            ["cup"] = new DescriptionProgram("\u001b[%i%p1%d;%p2%dH"u8),
            ["sgr0"] = new DescriptionProgram("\u001b[0m"u8),
            ["clear"] = new DescriptionProgram("\u001b[2J"u8),
            ["Smulx"] = new DescriptionProgram("\u001b[4:%p1%dm"u8),
            ["Setulc"] = new DescriptionProgram(
                "\u001b[58;2;%p1%{65536}%/%d;%p1%{256}%/%{255}%&%d;%p1%{255}%&%dm"u8),
            ["setdf"] = new DescriptionProgram("\u001b[39m"u8),
            ["setdb"] = new DescriptionProgram("\u001b[49m"u8),
            ["setaf"] = new DescriptionProgram("\u001b[38;5;%p1%dm"u8),
            ["setab"] = new DescriptionProgram("\u001b[48;5;%p1%dm"u8),
            ["setrgbf"] = new DescriptionProgram("\u001b[38;2;%p1%d;%p2%d;%p3%dm"u8),
            ["setrgbb"] = new DescriptionProgram("\u001b[48;2;%p1%d;%p2%d;%p3%dm"u8),
            ["op"] = new DescriptionProgram("\u001b[39;49m"u8),
            ["civis"] = new DescriptionProgram("\u001b[?25l"u8),
            ["cnorm"] = new DescriptionProgram("\u001b[?25h"u8)
        };
        var profile = new TerminalProfile(
            new Description("decorations", DescriptionOrigin.Database, Suitability.Usable, colors: 256),
            TerminalCapabilities.Conservative with
            {
                ColorDepth = ColorDepth.TrueColor,
                StyledUnderlines = supported,
                UnderlineColor = supported
            },
            new Programs(programs),
            KeyMap.Empty);
        var destination = new ArrayBufferWriter<byte>();

        _ = FrameEncoder.Encode(null, frame, destination, profile);

        destination.WrittenSpan.IndexOf("\u001b[4:3m"u8).ShouldBeGreaterThanOrEqualTo(0);
        destination.WrittenSpan.IndexOf("\u001b[58;2;1;2;3m"u8).ShouldBeGreaterThanOrEqualTo(0);
        destination.WrittenSpan.IndexOf("\u001b[39m\u001b[48;2;205;0;0m"u8)
            .ShouldBeGreaterThanOrEqualTo(0);
    }

    /// <summary>Verifies an all-default color target uses the description's paired default program.</summary>
    [Fact]
    public void Encode_WhenStyledTargetUsesDefaultColors_UsesPairedDefaultProgram()
    {
        using Frame frame = new(new Size(1, 1));
        _ = frame.Canvas.Draw("x", default, new CellStyle(attributes: TerminalAttributes.Bold));
        var profile = CreateProfile(ColorDepth.Indexed256, new Dictionary<string, DescriptionProgram>
        {
            ["cup"] = new DescriptionProgram("\u001b[%i%p1%d;%p2%dH"u8),
            ["sgr0"] = new DescriptionProgram("\u001b[0m"u8),
            ["clear"] = new DescriptionProgram("\u001b[2J"u8),
            ["bold"] = new DescriptionProgram("\u001b[1m"u8),
            ["op"] = new DescriptionProgram("\u001b[39;49m"u8),
            ["civis"] = new DescriptionProgram("\u001b[?25l"u8),
            ["cnorm"] = new DescriptionProgram("\u001b[?25h"u8)
        });
        var destination = new ArrayBufferWriter<byte>();

        _ = FrameEncoder.Encode(null, frame, destination, profile);

        destination.WrittenSpan.IndexOf("\u001b[1m\u001b[39;49m"u8).ShouldBeGreaterThanOrEqualTo(0);
    }

    private static TerminalProfile CreateProfile(
        ColorDepth colorDepth,
        IReadOnlyDictionary<string, DescriptionProgram> programs,
        bool automaticMargins = false,
        bool backColorErase = false,
        bool eatNewlineGlitch = false) => new(
        new Description(
            "description-test",
            DescriptionOrigin.Database,
            Suitability.Usable,
            colors: colorDepth == ColorDepth.Monochrome ? null : 256,
            automaticMargins: automaticMargins,
            backColorErase: backColorErase,
            eatNewlineGlitch: eatNewlineGlitch),
        TerminalCapabilities.Conservative with { ColorDepth = colorDepth },
        new Programs(programs),
        KeyMap.Empty);

    private static Dictionary<string, DescriptionProgram> CorePrograms() => new()
    {
        ["cup"] = new DescriptionProgram("\u001b[%i%p1%d;%p2%dH"u8),
        ["sgr0"] = new DescriptionProgram("\u001b[0m"u8),
        ["clear"] = new DescriptionProgram("\u001b[2J"u8),
        ["civis"] = new DescriptionProgram("\u001b[?25l"u8),
        ["cnorm"] = new DescriptionProgram("\u001b[?25h"u8)
    };

    private static Dictionary<string, DescriptionProgram> ColorPrograms()
    {
        var programs = CorePrograms();
        programs["setaf"] = new DescriptionProgram("\u001b[38;5;%p1%dm"u8);
        programs["setab"] = new DescriptionProgram("\u001b[48;5;%p1%dm"u8);
        programs["setrgbf"] = new DescriptionProgram("\u001b[38;2;%p1%d;%p2%d;%p3%dm"u8);
        programs["setrgbb"] = new DescriptionProgram("\u001b[48;2;%p1%d;%p2%d;%p3%dm"u8);
        return programs;
    }

    /// <summary>
    /// Verifies targeted multi-frame transitions against an independent terminal model.
    /// </summary>
    /// <param name="frontText">The committed front-frame text.</param>
    /// <param name="backText">The target back-frame text.</param>
    [Theory]
    [InlineData("abcd", "abXd")]
    [InlineData("界x", "abx")]
    [InlineData("abx", "界x")]
    [InlineData("e\u0301x", "éx")]
    public void Encode_WhenFrameTransitions_AgreesWithFullRender(
        string frontText,
        string backText)
    {
        using var front = CreateFixedWidthFrame(frontText);
        using var back = CreateFixedWidthFrame(backText);
        var incremental = new VirtualScreen(back.Size);
        incremental.Apply(Encode(null, front));
        incremental.Apply(Encode(front, back));
        var full = new VirtualScreen(back.Size);
        full.Apply(Encode(null, back));

        incremental.ShouldMatch(back);
        full.ShouldMatch(back);
        incremental.ShouldMatch(full);
    }

    /// <summary>
    /// Verifies style, hyperlink, and cursor transitions are semantically equivalent.
    /// </summary>
    [Fact]
    public void Encode_WhenStyleAndCursorChange_AgreesWithFullRender()
    {
        using Frame front = new(new Size(2, 1));
        using Frame back = new(new Size(2, 1));
        _ = front.Canvas.Draw("ab".AsSpan(), new Point(0, 0));
        _ = back.Canvas.Draw(
            "ab".AsSpan(),
            new Point(0, 0),
            new CellStyle(attributes: TerminalAttributes.Bold, hyperlink: "https://example.test"));
        back.SetCursor(new Point(1, 0), visible: true);
        var incremental = new VirtualScreen(back.Size);
        incremental.Apply(Encode(null, front));
        incremental.Apply(Encode(front, back));
        var full = new VirtualScreen(back.Size);
        full.Apply(Encode(null, back));

        incremental.ShouldMatch(back);
        incremental.ShouldMatch(full);
    }

    /// <summary>
    /// Verifies modern decorations survive supported output and independent parsing.
    /// </summary>
    [Fact]
    public void Encode_WhenModernDecorationsAreSupported_AgreesWithFullRender()
    {
        using Frame frame = new(new Size(2, 1));
        var style = new CellStyle(
            attributes: TerminalAttributes.RapidBlink | TerminalAttributes.Overline,
            underline: Underline.Curly,
            underlineColor: Color.Rgb(12, 34, 56));
        _ = frame.Canvas.Draw("ab".AsSpan(), new Point(0, 0), style);
        var screen = new VirtualScreen(frame.Size);

        screen.Apply(Encode(null, frame, ModernDecorationCapabilities));

        screen.ShouldMatch(frame);
    }

    private static TerminalCapabilities ModernDecorationCapabilities =>
        TerminalCapabilities.Conservative with
        {
            ColorDepth = ColorDepth.TrueColor,
            StyledUnderlines = new Feature(CapabilitySupport.Supported, Origin.Override),
            UnderlineColor = new Feature(CapabilitySupport.Supported, Origin.Override),
            Overline = new Feature(CapabilitySupport.Supported, Origin.Override)
        };

    private static byte[] Encode(
        Frame? front,
        Frame back,
        TerminalCapabilities? capabilities = null)
    {
        var destination = new ArrayBufferWriter<byte>();
        _ = FrameEncoder.Encode(
            front,
            back,
            destination,
            capabilities ?? TerminalCapabilities.Conservative with { ColorDepth = ColorDepth.TrueColor });
        return destination.WrittenSpan.ToArray();
    }

    private static Frame CreateFixedWidthFrame(string value)
    {
        var frame = new Frame(new Size(3, 1));
        _ = frame.Canvas.Draw(value.AsSpan(), new Point(0, 0));
        return frame;
    }

    /// <summary>
    /// Verifies random semantic frame pairs converge to the same terminal model.
    /// </summary>
    [Fact]
    public void Encode_WhenFramesAreRandomized_MatchesFullRender()
    {
        var random = new Random(_seed);

        for (var testCase = 0; testCase < 128; testCase++)
        {
            using var front = Create(random);
            using var back = Create(random);

            try
            {
                var incremental = new VirtualScreen(back.Size);
                incremental.Apply(EncodeRandomized(null, front));
                incremental.Apply(EncodeRandomized(front, back));
                var full = new VirtualScreen(back.Size);
                full.Apply(EncodeRandomized(null, back));
                incremental.ShouldMatch(back);
                incremental.ShouldMatch(full);
            }
            catch (ShouldAssertException exception)
            {
                throw new InvalidOperationException(
                    $"Rendering seed {_seed}, case {testCase}.",
                    exception);
            }
        }
    }

    private static Frame Create(Random random)
    {
        var frame = new Frame(new Size(10, 4));
        string[] values = ["a", "Z", "界", "語", "e\u0301", "👩‍💻", " "];
        string?[] links = [null, "https://one.test", "https://two.test"];

        for (var index = 0; index < 24; index++)
        {
            var point = new Point(random.Next(frame.Size.Width), random.Next(frame.Size.Height));
            var attributes = random.Next(8) switch
            {
                0 => TerminalAttributes.None,
                1 => TerminalAttributes.Bold,
                2 => TerminalAttributes.Italic,
                3 => TerminalAttributes.Underline | TerminalAttributes.Reverse,
                4 => TerminalAttributes.Blink,
                5 => TerminalAttributes.RapidBlink,
                6 => TerminalAttributes.Overline,
                _ => TerminalAttributes.RapidBlink | TerminalAttributes.Overline
            };
            var underline = random.Next(6) == 0
                ? (Underline) random.Next((int) Underline.Straight, (int) Underline.Dashed + 1)
                : Underline.None;

            if (underline != Underline.None)
            {
                attributes &= ~TerminalAttributes.Underline;
            }

            var foreground = random.Next(3) == 0
                ? ReferenceColors.Get(random.Next(16))
                : Color.Default;
            var underlineColor = underline != Underline.None && random.Next(2) == 0
                ? Color.Rgb(random.Next(256), random.Next(256), random.Next(256))
                : Color.Default;
            var style = new CellStyle(
                foreground,
                attributes: attributes,
                hyperlink: links[random.Next(links.Length)],
                underline: underline,
                underlineColor: underlineColor);
            _ = frame.Canvas.Draw(
                values[random.Next(values.Length)].AsSpan(),
                point,
                style,
                (Edge) random.Next(3));
        }

        frame.SetCursor(
            new Point(random.Next(frame.Size.Width), random.Next(frame.Size.Height)),
            random.Next(2) == 0);
        return frame;
    }

    private static byte[] EncodeRandomized(Frame? front, Frame back)
    {
        var destination = new ArrayBufferWriter<byte>();
        _ = FrameEncoder.Encode(
            front,
            back,
            destination,
            TerminalCapabilities.Conservative with
            {
                ColorDepth = ColorDepth.TrueColor,
                StyledUnderlines = new Feature(CapabilitySupport.Supported, Origin.Override),
                UnderlineColor = new Feature(CapabilitySupport.Supported, Origin.Override),
                Overline = new Feature(CapabilitySupport.Supported, Origin.Override)
            });
        return destination.WrittenSpan.ToArray();
    }
}
