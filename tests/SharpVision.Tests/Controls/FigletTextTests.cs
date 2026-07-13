// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

using SharpVision.Fonts;


/// <summary>Verifies FIGletText validation, layout, caching, and exact cells.</summary>
public sealed class FigletTextTests
{
    /// <summary>Verifies constructor validation and documented defaults.</summary>
    [Fact]
    public void Constructor_WhenFontIsProvided_UsesDocumentedDefaults()
    {
        FigletFont font = FigletCatalog.Default.Load("Standard");
        FigletText control = new(font);

        control.Font.ShouldBeSameAs(font);
        control.Content.ShouldBe(string.Empty);
        control.Options.ShouldBe(default);
    }

    /// <summary>Verifies FIGlet output determines desired cell dimensions.</summary>
    [Fact]
    public void Layout_WhenContentIsSet_MeasuresRenderedFontOutput()
    {
        FigletText control = new(FigletCatalog.Default.Load("Standard"))
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
        FigletText control = new(FigletCatalog.Default.Load("Standard"))
        {
            Content = "H",
        };
        new Engine().Layout(control, new Size(7, 6));
        using Frame frame = new(new Size(7, 6));

        control.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(1, 0)).ShouldBe("_");
        FrameOracle.Get(frame, new Point(0, 1)).ShouldBe("|");
        FrameOracle.Get(frame, new Point(6, 5)).ShouldBe(" ");
    }

    /// <summary>Verifies a null replacement fails before changing the current font.</summary>
    [Fact]
    public void Font_WhenValueIsNull_ThrowsBeforeMutation()
    {
        FigletFont font = FigletCatalog.Default.Load("Standard");
        FigletText control = new(font);

        _ = Should.Throw<ArgumentNullException>(() => control.Font = null!);

        control.Font.ShouldBeSameAs(font);
    }
}
