// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;




/// <summary>Verifies fixed-cell control chrome adapts to the inherited width policy.</summary>
public sealed class AmbiguousWidthControlTests
{
    /// <summary>Verifies a Unicode border degrades without changing its physical geometry.</summary>
    [Fact]
    public void Border_WhenAmbiguousWidthIsWide_RendersPortableOneCellGlyphs()
    {
        var border = new LayoutProbe()
        {
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        border.SetCellPolicy(new Policy(Ambiguous.Wide));
        new Engine().Layout(border, new Size(3, 2));
        using Frame frame = new(new Size(3, 2), ambiguousWidth: Ambiguous.Wide);

        border.Render(frame.Canvas);

        FrameOracle.Get(frame, default).ShouldBe("+");
        FrameOracle.Get(frame, new Point(1, 0)).ShouldBe("-");
        FrameOracle.Get(frame, new Point(0, 1)).ShouldBe("+");
    }

    /// <summary>Verifies block-glyph shadows remain one cell under a wide policy.</summary>
    [Fact]
    public void Shadow_WhenAmbiguousWidthIsWide_RendersPortableBlockGlyph()
    {
        var shadow = new LayoutProbe()
        {
            HasShadow = true,
            ShadowMode = ShadowMode.BlockGlyph,
            ShadowOffset = new Point(1, 1),
            ShadowGlyph = new Rune('▓'),
            ShadowAttributes = Attributes.Dim,
        };
        shadow.Children.Add(new ProbeControl(new Size(2, 1)));
        shadow.SetCellPolicy(new Policy(Ambiguous.Wide));
        new Engine().Layout(shadow, new Size(2, 1));
        using Frame frame = new(new Size(3, 2), ambiguousWidth: Ambiguous.Wide);

        shadow.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(2, 1)).ShouldBe("#");
    }

    /// <summary>Verifies generated scrollbar chrome occupies exactly its arranged cells.</summary>
    [Fact]
    public void ScrollBar_WhenAmbiguousWidthIsWide_RendersPortableChrome()
    {
        var scrollBar = new ScrollBar()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        scrollBar.SetCellPolicy(new Policy(Ambiguous.Wide));
        new Engine().Layout(scrollBar, new Size(5, 1));
        using Frame frame = new(new Size(5, 1), ambiguousWidth: Ambiguous.Wide);

        scrollBar.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("<");
        FrameOracle.Get(frame, new Point(4, 0)).ShouldBe(">");
    }

    /// <summary>Verifies checkbox marks retain their documented one-cell width.</summary>
    [Fact]
    public void CheckBox_WhenAmbiguousWidthIsWide_RendersPortableMark()
    {
        var checkBox = new CheckBox()
        {
            IsChecked = true,
            MarkStyle = CheckBoxMarks.Square,
            Marks = new Marks(new Rune('o'), new Rune('·'), new Rune('-')),
        };
        checkBox.SetCellPolicy(new Policy(Ambiguous.Wide));
        new Engine().Layout(checkBox, new Size(1, 1));
        using Frame frame = new(new Size(1, 1), ambiguousWidth: Ambiguous.Wide);

        checkBox.Render(frame.Canvas);

        FrameOracle.Get(frame, default).ShouldBe("x");
    }

    /// <summary>Verifies password measurement and rendering share the inherited policy.</summary>
    [Fact]
    public void PasswordCharacter_WhenAmbiguousWidthIsWide_UsesTwoCellMaskGeometry()
    {
        var input = new TextInput()
        {
            Text = "a",
            PasswordCharacter = new Rune('·'),
            Width = Length.Cells(4),
        };
        input.SetCellPolicy(new Policy(Ambiguous.Wide));
        input.SetFocused(true);
        new Engine().Layout(input, new Size(4, 1));
        using Frame frame = new(new Size(4, 1), ambiguousWidth: Ambiguous.Wide);

        input.Render(frame.Canvas);

        FrameOracle.Get(frame, default).ShouldBe("·");
        frame.GetCell(new Point(1, 0)).IsContinuation.ShouldBeTrue();
        frame.Cursor.Position.ShouldBe(new Point(2, 0));
    }
}
