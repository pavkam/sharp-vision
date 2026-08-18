// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Display;

/// <summary>Verifies FIGletText validation, layout, caching, and exact cells.</summary>
public sealed class FigletTextTests
{
    /// <summary>Verifies constructor validation and documented defaults.</summary>
    [ComponentUnitEvidence(typeof(FigletText))]
    [Fact]
    public void Constructor_WhenFontIsProvided_UsesDocumentedDefaults()
    {
        var font = FigletCatalog.Default.Load("standard");
        var control = new FigletText(font);

        control.Font.ShouldBeSameAs(font);
        control.Content.ShouldBe(string.Empty);
        control.Options.ShouldBe(default);
    }

    /// <summary>Verifies FIGlet output determines desired cell dimensions.</summary>
    [Fact]
    public void Layout_WhenContentIsSet_MeasuresRenderedFontOutput()
    {
        var control = new FigletText(FigletCatalog.Default.Load("standard")) { Content = "H" };

        new LayoutEngine().Layout(control, new Size(20, 10));

        control.DesiredSize.ShouldBe(new Size(7, 6));
    }

    /// <summary>Verifies rendering writes exact generated rows through semantic cells.</summary>
    [Fact]
    public void Render_WhenContentIsSet_WritesExactFigletCells()
    {
        var control = new FigletText(FigletCatalog.Default.Load("standard")) { Content = "H" };
        new LayoutEngine().Layout(control, new Size(7, 6));
        using Frame frame = new(new Size(7, 6));

        control.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(1, 0)).ShouldBe("_");
        FrameOracle.Get(frame, new Point(0, 1)).ShouldBe("|");
        FrameOracle.Get(frame, new Point(6, 5)).ShouldBe(" ");
    }

    /// <summary>Verifies FIGlet cells preserve the already-painted parent surface when no background is declared.</summary>
    [Fact]
    public void Render_WhenBackgroundIsUnset_PreservesDestinationSurface()
    {
        var control = new FigletText(FigletCatalog.Default.Load("standard"))
        {
            Content = "H",
            Face = AppearanceTestValues.Face(foreground: ReferenceColors.Get(45)),
        };
        new LayoutEngine().Layout(control, new Size(7, 6));
        using Frame frame = new(new Size(7, 6));
        var destinationBackground = ReferenceColors.Get(238);
        frame.Canvas.Fill(
            frame.Canvas.Bounds,
            new Rune(' '),
            new TerminalStyle(ReferenceColors.Get(255), destinationBackground));

        control.Render(frame.Canvas);

        frame.GetCell(new Point(1, 0)).Style.ShouldBe(
            new TerminalStyle(ReferenceColors.Get(45), destinationBackground));
        frame.GetCell(default).Style.Background.ShouldBe(destinationBackground);
    }

    /// <summary>Verifies a null replacement fails before changing the current font.</summary>
    [Fact]
    public void Font_WhenValueIsNull_ThrowsBeforeMutation()
    {
        var font = FigletCatalog.Default.Load("standard");
        var control = new FigletText(font);

        _ = Should.Throw<ArgumentNullException>(() => control.Font = null!);

        control.Font.ShouldBeSameAs(font);
    }

    /// <summary>Verifies direct and ancestor-inherited IsEnabled changes compute the effective
    /// disabled state FigletText's mounted disabled contract depends on.</summary>
    [ComponentUnitEvidence(typeof(FigletText), ComponentBehavior.Disabled)]
    [Fact]
    public void IsEnabled_WhenDisabledDirectlyOrByAncestor_ComputesEffectiveState()
    {
        var control = new FigletText(FigletCatalog.Default.Load("standard"));
        control.EffectiveIsEnabled.ShouldBeTrue();

        control.IsEnabled = false;
        control.EffectiveIsEnabled.ShouldBeFalse();

        control.IsEnabled = true;
        var stack = new Stack { Children = { control } };
        control.EffectiveIsEnabled.ShouldBeTrue();

        stack.IsEnabled = false;
        control.EffectiveIsEnabled.ShouldBeFalse();
    }
}
