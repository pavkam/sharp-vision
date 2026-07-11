using SharpVision.Controls;
using SharpVision.Layout;
using SharpVision.Styling;
using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Protocols;
using SharpVision.Terminal.Rendering;
using SharpVision.Terminal.Unicode;
using SharpVision.Tests.Performance;
using SharpVision.Tests.Support;
using SharpVision.Text;

using Shouldly;

using ControlText = SharpVision.Controls.Text;
using TerminalAttributes = SharpVision.Terminal.Rendering.Attributes;
using UiStyle = SharpVision.Styling.Style;

namespace SharpVision.Tests.Controls;

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
        value.Wrapping.ShouldBe(Wrapping.None);
        value.Trimming.ShouldBe(Trimming.None);
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
        _ = Should.Throw<ArgumentOutOfRangeException>(() => text.Wrapping = (Wrapping) 99);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => text.Trimming = (Trimming) 99);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => text.TextAlignment = (Alignment) 99);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => text.AmbiguousWidth = (Ambiguous) 99);
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            text.Attributes = (TerminalAttributes) int.MaxValue);

        text.Content.ShouldBe("safe");
        text.Wrapping.ShouldBe(Wrapping.None);
        text.Attributes.ShouldBeNull();
    }

    /// <summary>Verifies wrapped Unicode commits exact line metrics and desired size.</summary>
    [Fact]
    public void Layout_WhenContentWraps_CommitsGraphemeSafeLines()
    {
        var text = new ControlText("e\u0301界x") { Wrapping = Wrapping.Grapheme };

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
            Wrapping = Wrapping.Grapheme,
            TextAlignment = Alignment.End,
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
        var text = new ControlText("ab界c\nZ") { Trimming = Trimming.GraphemeEllipsis };
        new Engine().Layout(text, new Size(4, 2));
        using var frame = new Frame(new Size(4, 2));

        text.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("a");
        FrameOracle.Get(frame, new Point(1, 0)).ShouldBe("b");
        FrameOracle.Get(frame, new Point(2, 0)).ShouldBe("…");
        FrameOracle.Get(frame, new Point(0, 1)).ShouldBe("Z");
    }

    /// <summary>Verifies ambiguous-wide ellipsis measurement matches semantic rendering.</summary>
    [Fact]
    public void Render_WhenEllipsisIsAmbiguousWide_ReservesTwoCompleteCells()
    {
        var text = new ControlText("abcde")
        {
            Trimming = Trimming.GraphemeEllipsis,
            AmbiguousWidth = Ambiguous.Wide,
        };
        new Engine().Layout(text, new Size(4, 1));
        using var frame = new Frame(new Size(4, 1), ambiguousWidth: Ambiguous.Wide);

        text.Render(frame.Canvas);

        text.Lines.Span[0].Cells.ShouldBe(4);
        FrameOracle.Get(frame, new Point(2, 0)).ShouldBe("…");
        frame.GetCell(new Point(3, 0)).IsContinuation.ShouldBeTrue();
    }

    /// <summary>Verifies direct overrides compose over inherited resolved appearance.</summary>
    [Fact]
    public void Render_WhenOverridesAreSet_ComposesExactCellStyle()
    {
        var style = new UiStyle();
        style.Set(
            State.Normal,
            new Appearance(
                foreground: Color.Indexed(1),
                background: Color.Indexed(2),
                attributes: TerminalAttributes.Bold));
        var text = new ControlText("A")
        {
            Style = style,
            Foreground = Color.Indexed(7),
            Attributes = TerminalAttributes.Underline,
        };
        new Engine().Layout(text, new Size(1, 1));
        using var frame = new Frame(new Size(1, 1));

        text.Render(frame.Canvas);

        var cell = frame.GetCell(default);
        cell.Style.Foreground.ShouldBe(Color.Indexed(7));
        cell.Style.Background.ShouldBe(Color.Indexed(2));
        cell.Style.Attributes.ShouldBe(TerminalAttributes.Underline);
    }

    /// <summary>Verifies hidden and collapsed text do not draw stale cells.</summary>
    [Theory]
    [InlineData(Visibility.Hidden)]
    [InlineData(Visibility.Collapsed)]
    public void Render_WhenTextIsUnavailable_WritesNoCells(Visibility visibility)
    {
        var text = new ControlText("secret") { Visibility = visibility };
        new Engine().Layout(text, new Size(6, 1));
        using var frame = new Frame(new Size(6, 1));

        text.Render(frame.Canvas);

        FrameOracle.Get(frame, default).ShouldBe(string.Empty);
        text.DesiredSize.ShouldBe(visibility == Visibility.Collapsed ? default : new Size(6, 1));
    }

    /// <summary>Verifies a warmed unchanged layout/render cycle allocates no managed memory.</summary>
    [Fact]
    public void Render_WhenLayoutIsUnchanged_AllocatesNoManagedMemoryAfterWarmup()
    {
        var text = new ControlText("e\u0301 · 界 · 👩‍💻") { Wrapping = Wrapping.Word };
        var engine = new Engine();
        var size = new Size(80, 2);
        using var frame = new Frame(size);
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
}
