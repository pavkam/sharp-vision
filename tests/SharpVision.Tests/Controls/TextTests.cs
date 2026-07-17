// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;


using SharpVision.Tests.Performance;


using TerminalAttributes = Attributes;

/// <summary>Verifies cached Text measurement, rendering, validation, and styling.</summary>
[Collection(PerformanceGroup.Name)]
public sealed class TextTests
{
    /// <summary>Verifies constructor content and documented defaults.</summary>
    [Fact]
    public void Constructor_WhenCreated_UsesDocumentedDefaults()
    {
        var empty = new ControlText();
        var value = new ControlText("hello");

        empty.Content.ShouldBe(string.Empty);
        value.Content.ShouldBe("hello");
        value.Overflow.ShouldBe(Overflow.Visible);
        value.TextAlignment.ShouldBe(Alignment.Start);
        value.AmbiguousWidth.ShouldBe(Ambiguous.Narrow);
        value.Foreground.ShouldBeNull();
        value.Background.ShouldBeNull();
        value.Attributes.ShouldBeNull();
        value.Lines.Length.ShouldBe(0);
        value.CanFocus.ShouldBeFalse();
    }

    /// <summary>Verifies invalid content and enum values throw before mutation.</summary>
    [Fact]
    public void Setters_WhenValuesAreInvalid_ThrowBeforeMutation()
    {
        var text = new ControlText("safe");

        _ = Should.Throw<ArgumentNullException>(() => text.Content = null!);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => text.Overflow = (Overflow) 99);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => text.TextAlignment = (Alignment) 99);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => text.AmbiguousWidth = (Ambiguous) 99);
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            text.Attributes = (TerminalAttributes) int.MaxValue);

        text.Content.ShouldBe("safe");
        text.Overflow.ShouldBe(Overflow.Visible);
        text.Attributes.ShouldBeNull();
    }

    /// <summary>Verifies wrapped Unicode commits exact line metrics and desired size.</summary>
    [Fact]
    public void Layout_WhenContentWraps_CommitsGraphemeSafeLines()
    {
        var text = new ControlText("e\u0301界x") { Overflow = Overflow.WrapAnywhere };

        new Engine().Layout(text, new Size(2, 4));

        text.DesiredSize.ShouldBe(new Size(2, 3));
        text.Lines.ToArray().ShouldBe([
            new Line(0, 2, 1, 0, false),
            new Line(2, 1, 2, 0, false),
            new Line(3, 1, 1, 0, false),
        ]);
    }

    /// <summary>Verifies final resize reflows lines and recomputes alignment.</summary>
    [Fact]
    public void Layout_WhenViewportResizes_ReflowsAndRealignsLines()
    {
        var text = new ControlText("abcd")
        {
            Overflow = Overflow.WrapAnywhere,
            TextAlignment = Alignment.End,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        var engine = new Engine();

        engine.Layout(text, new Size(2, 3));
        text.Lines.Length.ShouldBe(2);
        engine.Layout(text, new Size(6, 1));

        text.Lines.Length.ShouldBe(1);
        text.Lines.Span[0].Leading.ShouldBe(2);
    }

    /// <summary>Verifies multiline and ellipsis output occupy exact semantic cells.</summary>
    [Fact]
    public void Render_WhenContentIsTrimmedAndMultiline_WritesExpectedCells()
    {
        var text = new ControlText("ab界c\nZ") { Overflow = Overflow.Ellipsis };
        new Engine().Layout(text, new Size(4, 2));
        using Frame frame = new(new Size(4, 2));

        text.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("a");
        FrameOracle.Get(frame, new Point(1, 0)).ShouldBe("b");
        FrameOracle.Get(frame, new Point(2, 0)).ShouldBe("…");
        FrameOracle.Get(frame, new Point(0, 1)).ShouldBe("Z");
    }

    /// <summary>Verifies an ambiguous-wide ellipsis uses its one-cell theme fallback.</summary>
    [Fact]
    public void Render_WhenEllipsisIsAmbiguousWide_UsesThemeFallback()
    {
        var text = new ControlText("abcde")
        {
            Overflow = Overflow.Ellipsis,
            AmbiguousWidth = Ambiguous.Wide,
        };
        new Engine().Layout(text, new Size(4, 1));
        using Frame frame = new(new Size(4, 1), ambiguousWidth: Ambiguous.Wide);

        text.Render(frame.Canvas);

        text.Lines.Span[0].Cells.ShouldBe(4);
        FrameOracle.Get(frame, new Point(2, 0)).ShouldBe("c");
        FrameOracle.Get(frame, new Point(3, 0)).ShouldBe(".");
        frame.GetCell(new Point(3, 0)).IsContinuation.ShouldBeFalse();
    }

    /// <summary>Verifies an ellipsis replacing the first wide cluster keeps that cluster's markup style.</summary>
    [Fact]
    public void Render_WhenMarkedWideClusterBecomesEllipsis_PreservesMarkupStyle()
    {
        var text = new ControlText("<red>界</red>") { Overflow = Overflow.Ellipsis };
        new Engine().Layout(text, new Size(1, 1));
        using Frame frame = new(new Size(1, 1));

        text.Render(frame.Canvas);

        FrameOracle.Get(frame, default).ShouldBe("…");
        frame.GetCell(default).Style.Foreground.ShouldBe(Color.Indexed(1));
    }

    /// <summary>Verifies text without a declared background preserves the already-painted surface.</summary>
    [Fact]
    public void Render_WhenBackgroundIsUnset_PreservesDestinationSurface()
    {
        var text = new ControlText("A") { Foreground = Color.Indexed(45) };
        new Engine().Layout(text, new Size(1, 1));
        using Frame frame = new(new Size(1, 1));
        frame.Canvas.Fill(
            new Rect(0, 0, 1, 1),
            new Rune(' '),
            new TerminalStyle(Color.Indexed(255), Color.Indexed(238)));

        text.Render(frame.Canvas);

        frame.GetCell(default).Style.ShouldBe(new TerminalStyle(Color.Indexed(45), Color.Indexed(238)));
    }

    /// <summary>Verifies hidden and collapsed text do not draw stale cells.</summary>
    [Theory]
    [InlineData(Visibility.Hidden)]
    [InlineData(Visibility.Collapsed)]
    public void Render_WhenTextIsUnavailable_WritesNoCells(Visibility visibility)
    {
        var text = new ControlText("secret") { Visibility = visibility };
        new Engine().Layout(text, new Size(6, 1));
        using Frame frame = new(new Size(6, 1));

        text.Render(frame.Canvas);

        FrameOracle.Get(frame, default).ShouldBe(string.Empty);
        text.DesiredSize.ShouldBe(visibility == Visibility.Collapsed ? default : new Size(6, 1));
    }

    /// <summary>Verifies a warmed unchanged layout/render cycle allocates no managed memory.</summary>
    [Fact]
    public void Render_WhenLayoutIsUnchanged_AllocatesNoManagedMemoryAfterWarmup()
    {
        var text = new ControlText("e\u0301 · 界 · 👩‍💻") { Overflow = Overflow.Wrap };
        var engine = new Engine();
        var size = new Size(80, 2);
        using Frame frame = new(size);
        Render();

        for (var index = 0; index < 1_000; index++)
        {
            Render();
        }

        var minimum = long.MaxValue;

        for (var sample = 0; sample < 5; sample++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();

            for (var index = 0; index < 1_000; index++)
            {
                Render();
            }

            minimum = Math.Min(minimum, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        minimum.ShouldBe(0);

        void Render()
        {
            engine.Layout(text, size);
            frame.Clear();
            text.Invalidate(Invalidation.Render);
            text.Render(frame.Canvas);
        }
    }

    /// <summary>Verifies valid tags are removed before measuring visible content.</summary>
    [Fact]
    public void Layout_WhenContentContainsMarkup_MeasuresVisibleTextOnly()
    {
        ControlText text = new("<b>hi</b>");

        new Engine().Layout(text, new Size(80, 1));

        text.DesiredSize.ShouldBe(new Size(2, 1));
        text.Lines.ToArray().ShouldBe([new Line(0, 2, 2, 0, false)]);
    }

    /// <summary>Verifies malformed markup remains visible and never fails during rendering.</summary>
    [Fact]
    public void Render_WhenMarkupIsMalformed_PreservesLiteralContent()
    {
        ControlText text = new("<unknown <b>x");
        new Engine().Layout(text, new Size(40, 1));
        using Frame frame = new(new Size(40, 1));

        Should.NotThrow(() => text.Render(frame.Canvas));

        FrameOracle.Get(frame, default).ShouldBe("<");
        text.DesiredSize.Width.ShouldBe(13);
    }

    /// <summary>Verifies dynamic text escapes into literal visible content.</summary>
    [Fact]
    public void Escape_WhenAssignedToContent_RendersLiteralText()
    {
        ControlText text = new(ControlText.Escape(@"a < b\c"));
        new Engine().Layout(text, new Size(20, 1));
        using Frame frame = new(new Size(20, 1));

        text.Render(frame.Canvas);

        text.DesiredSize.Width.ShouldBe(7);
        FrameOracle.Get(frame, new Point(2, 0)).ShouldBe("<");
        FrameOracle.Get(frame, new Point(5, 0)).ShouldBe("\\");
    }

    /// <summary>Verifies markup facets compose into exact semantic cell metadata.</summary>
    [Fact]
    public void Render_WhenContentIsMarked_AppliesCompleteSemanticStyle()
    {
        ControlText text = new(
            "<fg=2><bg=#102030><u=curly><uc=214><link=https://example.test>x</link></uc></u></bg></fg>");
        new Engine().Layout(text, new Size(1, 1));
        using Frame frame = new(new Size(1, 1));

        text.Render(frame.Canvas);

        var style = frame.GetCell(default).Style;
        style.Foreground.ShouldBe(Color.Indexed(2));
        style.Background.ShouldBe(Color.Rgb(16, 32, 48));
        style.Underline.ShouldBe(Underline.Curly);
        style.UnderlineColor.ShouldBe(Color.Indexed(214));
        style.Hyperlink.ShouldBe("https://example.test");
    }

    /// <summary>Verifies a markup boundary inside one grapheme never splits its cell ownership.</summary>
    [Fact]
    public void Render_WhenStyleBoundarySplitsGrapheme_UsesStyleAtClusterStart()
    {
        ControlText text = new("<red>e</red><blue>\u0301</blue>");
        new Engine().Layout(text, new Size(1, 1));
        using Frame frame = new(new Size(1, 1));

        text.Render(frame.Canvas);

        FrameOracle.Get(frame, default).ShouldBe("e\u0301");
        frame.GetCell(default).Style.Foreground.ShouldBe(Color.Indexed(1));
    }

    /// <summary>Verifies markup blink tags override an incompatible inherited blink kind.</summary>
    [Fact]
    public void Render_WhenMarkupOverridesBlink_ProducesValidLatestBlinkStyle()
    {
        ControlText text = new("<rapidblink>x</rapidblink>")
        {
            Attributes = TerminalAttributes.Blink,
        };
        new Engine().Layout(text, new Size(1, 1));
        using Frame frame = new(new Size(1, 1));

        Should.NotThrow(() => text.Render(frame.Canvas));

        frame.GetCell(default).Style.Attributes.ShouldBe(TerminalAttributes.RapidBlink);
    }

    /// <summary>Verifies a typed markup underline overrides an inherited legacy underline.</summary>
    [Fact]
    public void Render_WhenMarkupOverridesUnderline_UsesMarkupShape()
    {
        ControlText text = new("<u=dashed>x</u>")
        {
            Attributes = TerminalAttributes.Underline,
        };
        new Engine().Layout(text, new Size(1, 1));
        using Frame frame = new(new Size(1, 1));

        text.Render(frame.Canvas);

        var style = frame.GetCell(default).Style;
        style.Attributes.ShouldBe(TerminalAttributes.None);
        style.Underline.ShouldBe(Underline.Dashed);
    }
}
