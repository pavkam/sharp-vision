namespace SharpVision.Tests.Controls;

using System.Text;

using SharpVision.Controls;
using SharpVision.Layout;
using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Protocols;
using SharpVision.Terminal.Rendering;
using SharpVision.Tests.Support;

using Shouldly;

using ControlText = SharpVision.Controls.Text;

/// <summary>Verifies Border ownership, layout, glyph validation, styling, and cells.</summary>
public sealed class BorderTests
{
    #region Presets

    /// <summary>Verifies every named family exposes exact corner and edge glyphs.</summary>
    [Theory]
    [MemberData(nameof(PresetCases))]
    public void Glyphs_WhenPresetIsSelected_UsesExactRunes(
        Glyphs glyphs,
        char corner,
        char horizontal,
        char vertical)
    {
        glyphs.TopLeft.ShouldBe(new Rune(corner));
        glyphs.Top.ShouldBe(new Rune(horizontal));
        glyphs.Left.ShouldBe(new Rune(vertical));
    }

    /// <summary>Provides the supported Unicode and portable border families.</summary>
    public static TheoryData<Glyphs, char, char, char> PresetCases => new()
    {
        { Glyphs.Light, '┌', '─', '│' },
        { Glyphs.Heavy, '┏', '━', '┃' },
        { Glyphs.Paired, '╔', '═', '║' },
        { Glyphs.Rounded, '╭', '─', '│' },
        { Glyphs.Ascii, '+', '-', '|' },
        { Glyphs.Solid, '█', '█', '█' },
        { Glyphs.LightShade, '░', '░', '░' },
        { Glyphs.MediumShade, '▒', '▒', '▒' },
        { Glyphs.DarkShade, '▓', '▓', '▓' },
    };

    #endregion

    #region Ownership and layout

    /// <summary>Verifies documented defaults and capacity-one ownership.</summary>
    [Fact]
    public void Constructor_WhenCreated_UsesDocumentedDefaults()
    {
        var border = new Border();

        border.Child.ShouldBeNull();
        border.BorderThickness.ShouldBe(default);
        border.Glyphs.ShouldBe(Glyphs.Default);
        border.Children.Count.ShouldBe(0);
        border.BorderColor.ShouldBeNull();
        border.Background.ShouldBeNull();
    }

    /// <summary>Verifies replacement is atomic when the candidate already belongs elsewhere.</summary>
    [Fact]
    public void Child_WhenReplacementIsInvalid_PreservesPreviousOwnership()
    {
        var border = new Border();
        var previous = new ProbeControl();
        var owner = new Overlay();
        var invalid = new ProbeControl();
        border.Child = previous;
        owner.Children.Add(invalid);

        _ = Should.Throw<ArgumentException>(() => border.Child = invalid);
        _ = Should.Throw<ArgumentException>(() => border.Child = border);

        border.Child.ShouldBeSameAs(previous);
        previous.Parent.ShouldBeSameAs(border);
        invalid.Parent.ShouldBeSameAs(owner);
        _ = Should.Throw<InvalidOperationException>(() => border.Children.Add(new ProbeControl()));
    }

    /// <summary>Verifies thickness accepts only zero or one before mutation.</summary>
    [Fact]
    public void BorderThickness_WhenAnEdgeExceedsOne_ThrowsBeforeMutation()
    {
        var border = new Border { BorderThickness = new Thickness(1) };

        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            border.BorderThickness = new Thickness(2, 0, 0, 0));

        border.BorderThickness.ShouldBe(new Thickness(1));
    }

    /// <summary>Verifies every glyph must be a printable narrow Rune.</summary>
    [Theory]
    [InlineData('\n')]
    [InlineData('界')]
    public void Constructor_WhenGlyphIsNotPrintableNarrow_Throws(char value)
    {
        _ = Should.Throw<ArgumentException>(() => new Glyphs(
            new Rune(value),
            new Rune('-'),
            new Rune('+'),
            new Rune('|'),
            new Rune('+'),
            new Rune('-'),
            new Rune('+'),
            new Rune('|')));
    }

    /// <summary>Verifies border and padding participate in child measure and arrange.</summary>
    [Fact]
    public void Layout_WhenChildHasMarginPaddingAndBorder_ComputesExactBounds()
    {
        var child = new ProbeControl(new Size(2, 1)) { Margin = new Thickness(1) };
        var border = new Border
        {
            Child = child,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(1),
        };

        new Engine().Layout(border, new Size(8, 7));

        border.DesiredSize.ShouldBe(new Size(8, 7));
        child.Bounds.ShouldBe(new Rect(3, 3, 2, 1));
    }

    #endregion

    #region Rendering

    /// <summary>Verifies default glyphs and Unicode child content render exact cells.</summary>
    [Fact]
    public void Render_WhenBorderIsComplete_WritesCornersEdgesAndChild()
    {
        var border = new Border
        {
            Child = new ControlText("界"),
            BorderThickness = new Thickness(1),
        };
        new Engine().Layout(border, new Size(4, 3));
        using var frame = new Frame(new Size(4, 3));

        border.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("┌");
        FrameOracle.Get(frame, new Point(3, 0)).ShouldBe("┐");
        FrameOracle.Get(frame, new Point(0, 2)).ShouldBe("└");
        FrameOracle.Get(frame, new Point(3, 2)).ShouldBe("┘");
        FrameOracle.Get(frame, new Point(1, 1)).ShouldBe("界");
        frame.GetCell(new Point(2, 1)).IsContinuation.ShouldBeTrue();
    }

    /// <summary>Verifies partial edges, custom glyphs, background, and color remain exact.</summary>
    [Fact]
    public void Render_WhenEdgesArePartial_UsesOnlyActiveCustomGlyphsAndStyles()
    {
        var border = new Border
        {
            BorderThickness = new Thickness(1, 1, 0, 0),
            Glyphs = new Glyphs(
                new Rune('+'),
                new Rune('-'),
                new Rune('+'),
                new Rune('|'),
                new Rune('+'),
                new Rune('-'),
                new Rune('+'),
                new Rune('|')),
            BorderColor = Color.Indexed(3),
            Background = Color.Indexed(4),
        };
        new Engine().Layout(border, new Size(3, 2));
        using var frame = new Frame(new Size(3, 2));

        border.Render(frame.Canvas);

        FrameOracle.Get(frame, default).ShouldBe("+");
        FrameOracle.Get(frame, new Point(1, 0)).ShouldBe("-");
        FrameOracle.Get(frame, new Point(0, 1)).ShouldBe("|");
        frame.GetCell(default).Style.Foreground.ShouldBe(Color.Indexed(3));
        frame.GetCell(new Point(1, 1)).Style.Background.ShouldBe(Color.Indexed(4));
    }

    /// <summary>Verifies zero and tiny bounds never emit incomplete corners outside clipping.</summary>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(1, 3)]
    public void Render_WhenBoundsAreTiny_RemainsContained(int width, int height)
    {
        var border = new Border { BorderThickness = new Thickness(1) };
        new Engine().Layout(border, new Size(width, height));
        using var frame = new Frame(new Size(Math.Max(1, width), Math.Max(1, height)));

        Should.NotThrow(() => border.Render(frame.Canvas));
    }

    #endregion
}
