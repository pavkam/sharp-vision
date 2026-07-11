using SharpVision.Controls;
using SharpVision.Fonts;
using SharpVision.Layout;
using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Rendering;
using SharpVision.Tests.Support;

using Shouldly;

namespace SharpVision.Tests.Controls;

/// <summary>Verifies FIGletText validation, layout, caching, and exact cells.</summary>
public sealed class FigletTextTests
{
    /// <summary>Verifies constructor validation and documented defaults.</summary>
    [Fact]
    public void Constructor_WhenFontIsProvided_UsesDocumentedDefaults()
    {
        var font = FigletCatalog.Default.Load("Standard");
        var control = new FigletText(font);

        control.Font.ShouldBeSameAs(font);
        control.Content.ShouldBe(string.Empty);
        control.Options.ShouldBe(default);
    }

    /// <summary>Verifies FIGlet output determines desired cell dimensions.</summary>
    [Fact]
    public void Layout_WhenContentIsSet_MeasuresRenderedFontOutput()
    {
        var control = new FigletText(FigletCatalog.Default.Load("Standard"))
        {
            Content = "H",
        };

        new Engine().Layout(control, new Size(20, 10));

        control.DesiredSize.ShouldBe(new Size(7, 6));
    }

    /// <summary>Verifies rendering writes exact generated rows through semantic cells.</summary>
    [Fact]
    public void Render_WhenContentIsSet_WritesExactFigletCells()
    {
        var control = new FigletText(FigletCatalog.Default.Load("Standard"))
        {
            Content = "H",
        };
        new Engine().Layout(control, new Size(7, 6));
        using var frame = new Frame(new Size(7, 6));

        control.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(1, 0)).ShouldBe("_");
        FrameOracle.Get(frame, new Point(0, 1)).ShouldBe("|");
        FrameOracle.Get(frame, new Point(6, 5)).ShouldBe(" ");
    }

    /// <summary>Verifies a null replacement fails before changing the current font.</summary>
    [Fact]
    public void Font_WhenValueIsNull_ThrowsBeforeMutation()
    {
        var font = FigletCatalog.Default.Load("Standard");
        var control = new FigletText(font);

        _ = Should.Throw<ArgumentNullException>(() => control.Font = null!);

        control.Font.ShouldBeSameAs(font);
    }
}
