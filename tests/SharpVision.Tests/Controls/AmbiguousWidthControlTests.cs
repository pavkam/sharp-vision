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
        var border = new LayoutProbe
        {
            Border = AppearanceTestValues.Border(BorderSide.All),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        border.SetCellPolicy(new UnicodePolicy(Ambiguous.Wide));
        new LayoutEngine().Layout(border, new Size(3, 2));
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
        var shadow = new LayoutProbe
        {
            Shadow = AppearanceTestValues.Shadow(visible: true, mode: ShadowMode.BlockGlyph, offset: new Point(1, 1), glyph: new Rune('▓'), attributes: TerminalAttributes.Dim),
        };
        shadow.Children.Add(new ProbeControl(new Size(2, 1)));
        shadow.SetCellPolicy(new UnicodePolicy(Ambiguous.Wide));
        new LayoutEngine().Layout(shadow, new Size(2, 1));
        using Frame frame = new(new Size(3, 2), ambiguousWidth: Ambiguous.Wide);

        shadow.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(2, 1)).ShouldBe("#");
    }

    /// <summary>Verifies generated scrollbar chrome occupies exactly its arranged cells.</summary>
    [Fact]
    public void ScrollBar_WhenAmbiguousWidthIsWide_RendersPortableChrome()
    {
        var scrollBar = new ScrollBar
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        scrollBar.SetCellPolicy(new UnicodePolicy(Ambiguous.Wide));
        new LayoutEngine().Layout(scrollBar, new Size(5, 1));
        using Frame frame = new(new Size(5, 1), ambiguousWidth: Ambiguous.Wide);

        scrollBar.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("<");
        FrameOracle.Get(frame, new Point(4, 0)).ShouldBe(">");
    }

    /// <summary>Verifies checkbox marks retain their documented one-cell width.</summary>
    [Fact]
    public void CheckBox_WhenAmbiguousWidthIsWide_RendersPortableMark()
    {
        var checkBox = new CheckBox
        {
            IsChecked = true,
            Style = new CheckBoxStyle(
                CheckBoxMarkStyle.Square,
                new CheckBoxGlyphs(new Rune('o'), new Rune('·'), new Rune('-')),
                CheckBoxStyle.Default.Appearance)
        };
        checkBox.SetCellPolicy(new UnicodePolicy(Ambiguous.Wide));
        new LayoutEngine().Layout(checkBox, new Size(1, 1));
        using Frame frame = new(new Size(1, 1), ambiguousWidth: Ambiguous.Wide);

        checkBox.Render(frame.Canvas);

        FrameOracle.Get(frame, default).ShouldBe("x");
    }

    /// <summary>Verifies password measurement and rendering share the inherited policy.</summary>
    [Fact]
    public void PasswordCharacter_WhenAmbiguousWidthIsWide_UsesTwoCellMaskGeometry()
    {
        var input = new TextInput
        {
            Text = "a",
            PasswordCharacter = new Rune('·'),
            Width = Length.Cells(4)
        };
        input.SetCellPolicy(new UnicodePolicy(Ambiguous.Wide));
        input.SetTheme(TestThemes.BorderlessInput);
        input.SetFocused(true);
        new LayoutEngine().Layout(input, new Size(4, 1));
        using Frame frame = new(new Size(4, 1), ambiguousWidth: Ambiguous.Wide);

        input.Render(frame.Canvas);

        FrameOracle.Get(frame, default).ShouldBe("·");
        frame.GetCell(new Point(1, 0)).IsContinuation.ShouldBeTrue();
        frame.Cursor.Position.ShouldBe(new Point(2, 0));
    }

    /// <summary>Verifies PasswordCharacter validates the mask against the tree's already-active
    /// ambient policy rather than always Narrow, so a mask that is genuinely two cells wide under
    /// the active Wide policy is rejected instead of silently accepted (see #271).</summary>
    [Fact]
    public void PasswordCharacter_WhenAmbiguousWidthIsAlreadyWide_RejectsAMaskThatIsNotOneCellUnderIt()
    {
        var input = new TextInput();
        input.SetCellPolicy(new UnicodePolicy(Ambiguous.Wide));

        _ = Should.Throw<ArgumentException>(() => input.PasswordCharacter = new Rune('·'));
    }
}
