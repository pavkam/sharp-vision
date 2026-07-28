// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Rendering;

using SharpVision.Terminal.Capabilities;

/// <summary>Verifies rendering through compiled terminal-description programs.</summary>
public sealed class DescriptionEncoderTests
{
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
        programs[program] = new Program(program == "civis" ? "HIDE"u8 : "SHOW"u8);
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
                attributes: Attributes.Bold | Attributes.Underline,
                underlineColor: ReferenceColors.Get(2)));
        frame.SetCursor(default, visible: true, CursorShape.Bar);
        using Frame expected = new(new Size(1, 1));
        _ = expected.Canvas.Draw("x", default);
        expected.SetCursor(default, visible: true, CursorShape.Block);
        var programs = CorePrograms();
        programs["bold"] = new Program("%{1}%PA"u8);
        programs["setaf"] = new Program("WRONG-COLOR"u8);
        programs["setab"] = new Program("\u001b[48;5;%p1%dm"u8);
        programs["op"] = new Program("%p1%d"u8);
        programs["el"] = new Program("%{1}%PA"u8);
        programs["Ss"] = new Program("WRONG-SHAPE"u8);
        programs["Se"] = new Program("\u001b[0 q"u8);
        programs["Setulc"] = new Program("%{1}%PA"u8);
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
        programs["setaf"] = new Program("%?%p1%{1}%=%t\u001b[38;5;1m%;"u8);
        programs["setab"] = new Program("\u001b[48;5;%p1%dm"u8);
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
        var profile = CreateProfile(ColorDepth.Monochrome, new Dictionary<string, Program>
        {
            ["cup"] = new Program("\u001b[%i%p1%d;%p2%df"u8),
            ["sgr0"] = new Program("\u001b[m"u8),
            ["clear"] = new Program("\u001b[2J\u001b[H"u8),
            ["civis"] = new Program("\u001b[?25l"u8),
            ["cnorm"] = new Program("\u001b[?25h"u8)
        });
        var destination = new ArrayBufferWriter<byte>();
        var screen = new VirtualScreen(frame.Size);
        screen.Apply("\u001b[1;1H\u001b[1mzz"u8);

        _ = FrameEncoder.Encode(null, frame, destination, profile);

        destination.WrittenSpan.StartsWith("\u001b[m\u001b[2J\u001b[H"u8).ShouldBeTrue();
        screen.Apply(destination.WrittenSpan);
        screen.ShouldMatch(frame);
    }

    /// <summary>Verifies a usable description without clear resets, homes, and erases through cup plus ed.</summary>
    [Fact]
    public void Encode_WhenFullScreenProfileHasElAndEdWithoutClear_UsesExactEraseFallback()
    {
        using Frame frame = new(new Size(2, 1));
        _ = frame.Canvas.Draw("ok", default);
        var profile = CreateProfile(ColorDepth.Monochrome, new Dictionary<string, Program>
        {
            ["cup"] = new Program("\u001b[%i%p1%d;%p2%df"u8),
            ["sgr0"] = new Program("\u001b[m"u8),
            ["el"] = new Program("\u001b[K"u8),
            ["ed"] = new Program("\u001b[2J"u8),
            ["civis"] = new Program("\u001b[?25l"u8),
            ["cnorm"] = new Program("\u001b[?25h"u8)
        });
        var destination = new ArrayBufferWriter<byte>();
        var screen = new VirtualScreen(frame.Size);
        screen.Apply("\u001b[1;1H\u001b[1mzz"u8);

        _ = FrameEncoder.Encode(null, frame, destination, profile);

        destination.WrittenSpan.StartsWith("\u001b[m\u001b[1;1f\u001b[2J"u8).ShouldBeTrue();
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
        var profile = CreateProfile(ColorDepth.TrueColor, new Dictionary<string, Program>
        {
            ["cup"] = new Program("\u001b[%i%p1%d;%p2%dH"u8),
            ["sgr0"] = new Program("\u001b[0m"u8),
            ["clear"] = new Program("\u001b[2J"u8),
            ["setrgbf"] = new Program("\u001b[38;2;%p1%d;%p2%d;%p3%dm"u8),
            ["civis"] = new Program("\u001b[?25l"u8),
            ["cnorm"] = new Program("\u001b[?25h"u8)
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
            new CellStyle(ReferenceColors.Get(9), attributes: Attributes.Bold));
        frame.SetCursor(new Point(1, 0), visible: true, CursorShape.Underline);
        var profile = CreateProfile(
            ColorDepth.Indexed256,
            new Dictionary<string, Program>
            {
                ["cup"] = new Program("\u001b[%i%p1%d;%p2%df"u8),
                ["sgr0"] = new Program("\u001b[0m"u8),
                ["clear"] = new Program("\u001b[2J"u8),
                ["bold"] = new Program("\u001b[1m"u8),
                ["setaf"] = new Program("\u001b[38;5;%p1%dm"u8),
                ["setab"] = new Program("\u001b[48;5;%p1%dm"u8),
                ["civis"] = new Program("\u001b[?25l"u8),
                ["cnorm"] = new Program("\u001b[?25h"u8),
                ["Ss"] = new Program("\u001b[%p1%d q"u8),
                ["Se"] = new Program("\u001b[0 q"u8)
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
            new Dictionary<string, Program>
            {
                ["cup"] = new Program("\u001b[%i%p1%d;%p2%dH"u8),
                ["sgr0"] = new Program("\u001b[0m"u8),
                ["clear"] = new Program("\u001b[2J"u8),
                ["setaf"] = new Program("\u001b[38;5;%p1%dm"u8),
                ["setab"] = new Program("\u001b[48;5;%p1%dm"u8),
                ["setrgbf"] = new Program("RGB(%p1%d,%p2%d,%p3%d)"u8)
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
        _ = frame.Canvas.Draw("x", default, new CellStyle(attributes: Attributes.Italic));
        var profile = CreateProfile(ColorDepth.Monochrome, new Dictionary<string, Program>
        {
            ["cup"] = new Program("\u001b[%i%p1%d;%p2%dH"u8),
            ["sgr0"] = new Program("\u001b[0m"u8),
            ["clear"] = new Program("\u001b[2J"u8)
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
            new Dictionary<string, Program>
            {
                ["cup"] = new Program("\u001b[%i%p1%d;%p2%dH"u8),
                ["sgr0"] = new Program("\u001b[0m"u8),
                ["clear"] = new Program("\u001b[2J"u8),
                ["setab"] = new Program("\u001b[48;5;%p1%dm"u8),
                ["setaf"] = new Program("\u001b[38;5;%p1%dm"u8),
                ["el"] = new Program("\u001b[0K"u8),
                ["civis"] = new Program("\u001b[?25l"u8),
                ["cnorm"] = new Program("\u001b[?25h"u8)
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
            new Dictionary<string, Program>
            {
                ["cup"] = new Program("\u001b[%i%p1%d;%p2%dH"u8),
                ["sgr0"] = new Program("\u001b[0m"u8),
                ["clear"] = new Program("\u001b[2J"u8),
                ["civis"] = new Program("\u001b[?25l"u8),
                ["cnorm"] = new Program("\u001b[?25h"u8)
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
        _ = frame.Canvas.Draw("x", default, new CellStyle(attributes: Attributes.RapidBlink));
        using Frame expected = new(new Size(1, 1));
        _ = expected.Canvas.Draw("x", default, new CellStyle(attributes: Attributes.Blink));
        var programs = CorePrograms();
        programs["blink"] = new Program("\u001b[5m"u8);
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
                Attributes.Italic |
                Attributes.Blink |
                Attributes.Hidden |
                Attributes.Strike |
                Attributes.Overline));
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
        programs["Ss"] = new Program("\u001b[%p1%d q"u8);
        programs["Se"] = new Program("\u001b[0 q"u8);
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
        programs["setaf"] = new Program("%p1%PA\u001b[38;5;%p1%dm"u8);
        programs["setab"] = new Program("\u001b[48;5;%p1%dm"u8);
        programs["Ss"] = failingProgram == "Ss"
            ? new Program("%?%p1%{4}%=%t\u001b[4 q%;"u8)
            : new Program("\u001b[%p1%d q"u8);
        programs["Se"] = failingProgram == "Se"
            ? new Program("%?%gA%{0}%=%t\u001b[0 q%;"u8)
            : new Program("\u001b[0 q"u8);
        var profile = CreateProfile(ColorDepth.Indexed256, programs);
        var interpreter = new Interpreter(Limits.Default);
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
                attributes: Attributes.Underline,
                underlineColor: ReferenceColors.Get(index)));
        var programs = ColorPrograms();
        programs["smul"] = new Program("\u001b[4m"u8);
        programs["Setulc"] = new Program("P%p1%d"u8);
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
        var programs = new Dictionary<string, Program>
        {
            ["cup"] = new Program("\u001b[%i%p1%d;%p2%dH"u8),
            ["sgr0"] = new Program("\u001b[0m"u8),
            ["clear"] = new Program("\u001b[2J"u8),
            ["Smulx"] = new Program("\u001b[4:%p1%dm"u8),
            ["Setulc"] = new Program(
                "\u001b[58;2;%p1%{65536}%/%d;%p1%{256}%/%{255}%&%d;%p1%{255}%&%dm"u8),
            ["setdf"] = new Program("\u001b[39m"u8),
            ["setdb"] = new Program("\u001b[49m"u8),
            ["setaf"] = new Program("\u001b[38;5;%p1%dm"u8),
            ["setab"] = new Program("\u001b[48;5;%p1%dm"u8),
            ["setrgbf"] = new Program("\u001b[38;2;%p1%d;%p2%d;%p3%dm"u8),
            ["setrgbb"] = new Program("\u001b[48;2;%p1%d;%p2%d;%p3%dm"u8),
            ["op"] = new Program("\u001b[39;49m"u8),
            ["civis"] = new Program("\u001b[?25l"u8),
            ["cnorm"] = new Program("\u001b[?25h"u8)
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
        _ = frame.Canvas.Draw("x", default, new CellStyle(attributes: Attributes.Bold));
        var profile = CreateProfile(ColorDepth.Indexed256, new Dictionary<string, Program>
        {
            ["cup"] = new Program("\u001b[%i%p1%d;%p2%dH"u8),
            ["sgr0"] = new Program("\u001b[0m"u8),
            ["clear"] = new Program("\u001b[2J"u8),
            ["bold"] = new Program("\u001b[1m"u8),
            ["op"] = new Program("\u001b[39;49m"u8),
            ["civis"] = new Program("\u001b[?25l"u8),
            ["cnorm"] = new Program("\u001b[?25h"u8)
        });
        var destination = new ArrayBufferWriter<byte>();

        _ = FrameEncoder.Encode(null, frame, destination, profile);

        destination.WrittenSpan.IndexOf("\u001b[1m\u001b[39;49m"u8).ShouldBeGreaterThanOrEqualTo(0);
    }

    private static TerminalProfile CreateProfile(
        ColorDepth colorDepth,
        IReadOnlyDictionary<string, Program> programs,
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

    private static Dictionary<string, Program> CorePrograms() => new()
    {
        ["cup"] = new Program("\u001b[%i%p1%d;%p2%dH"u8),
        ["sgr0"] = new Program("\u001b[0m"u8),
        ["clear"] = new Program("\u001b[2J"u8),
        ["civis"] = new Program("\u001b[?25l"u8),
        ["cnorm"] = new Program("\u001b[?25h"u8)
    };

    private static Dictionary<string, Program> ColorPrograms()
    {
        var programs = CorePrograms();
        programs["setaf"] = new Program("\u001b[38;5;%p1%dm"u8);
        programs["setab"] = new Program("\u001b[48;5;%p1%dm"u8);
        programs["setrgbf"] = new Program("\u001b[38;2;%p1%d;%p2%d;%p3%dm"u8);
        programs["setrgbb"] = new Program("\u001b[48;2;%p1%d;%p2%d;%p3%dm"u8);
        return programs;
    }
}
