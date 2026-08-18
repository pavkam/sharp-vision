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
                CheckBoxStyle.Default.Face,
                CheckBoxStyle.Default.Border,
                CheckBoxStyle.Default.Shadow,
                CheckBoxMarkStyle.Square,
                new CheckBoxGlyphs(new Rune('o'), new Rune('·'), new Rune('-')))
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
        frame.GetCell(new Point(1, 0)).Continuation.ShouldBeTrue();
        frame.Cursor.Position.ShouldBe(new Point(2, 0));
    }

    /// <summary>Verifies PasswordCharacter validates the mask against the tree's already-active
    /// ambient policy rather than always Narrow, so a mask that is genuinely two cells wide under
    /// the active Wide policy is rejected instead of silently accepted.</summary>
    [Fact]
    public void PasswordCharacter_WhenAmbiguousWidthIsAlreadyWide_RejectsAMaskThatIsNotOneCellUnderIt()
    {
        var input = new TextInput();
        input.SetCellPolicy(new UnicodePolicy(Ambiguous.Wide));

        _ = Should.Throw<ArgumentException>(() => input.PasswordCharacter = new Rune('·'));
    }

    /// <summary>Verifies a TreeView's Loading status-row indicator - the default is U+2022 BULLET,
    /// an East Asian Ambiguous-width code point - degrades to its portable fallback instead of
    /// throwing when the inherited policy resolves Ambiguous width as wide.</summary>
    [Fact]
    public void TreeViewStatusRow_WhenLoadingAndAmbiguousWidthIsWide_RendersPortableGlyph()
    {
        var owner = new TreeViewItem("Owner");
        var row = new TreeViewStatusRow(owner) { Kind = TreeViewStatusRowKind.Loading };
        row.SetCellPolicy(new UnicodePolicy(Ambiguous.Wide));
        new LayoutEngine().Layout(row, new Size(5, 1));
        using Frame frame = new(new Size(5, 1), ambiguousWidth: Ambiguous.Wide);

        Should.NotThrow(() => row.Render(frame.Canvas));

        FrameOracle.Get(frame, default).ShouldBe("*");
    }

    /// <summary>Verifies a TreeView's LoadFailed status-row indicator resolves against the active
    /// width policy the same way the Loading indicator does. The default glyph, U+2717 BALLOT X, is
    /// East Asian Narrow (not Ambiguous like Loading's bullet), so it renders unchanged rather than
    /// degrading to its fallback - the resolve call still runs on every draw without throwing.</summary>
    [Fact]
    public void TreeViewStatusRow_WhenLoadFailedAndAmbiguousWidthIsWide_RendersPortableGlyph()
    {
        var owner = new TreeViewItem("Owner");
        var row = new TreeViewStatusRow(owner) { Kind = TreeViewStatusRowKind.LoadFailed };
        row.SetCellPolicy(new UnicodePolicy(Ambiguous.Wide));
        new LayoutEngine().Layout(row, new Size(5, 1));
        using Frame frame = new(new Size(5, 1), ambiguousWidth: Ambiguous.Wide);

        Should.NotThrow(() => row.Render(frame.Canvas));

        FrameOracle.Get(frame, default).ShouldBe("✗");
    }

    /// <summary>Verifies a Calendar's previous-month navigation arrow degrades to its portable
    /// fallback instead of throwing when a theme replaces it with an East Asian Ambiguous-width
    /// rune (U+2022 BULLET) and the inherited policy resolves Ambiguous width as wide.</summary>
    [Fact]
    public void Calendar_WhenPreviousMonthGlyphIsAmbiguousAndAmbiguousWidthIsWide_RendersPortableGlyph()
    {
        var style = CalendarStyle.Default with { PreviousMonthGlyph = new Rune('•') };
        using var calendar = new UiCalendar
        {
            Culture = CultureInfo.InvariantCulture,
            DisplayMonth = new DateOnly(2026, 7, 1),
            Style = style
        };
        calendar.SetCellPolicy(new UnicodePolicy(Ambiguous.Wide));
        new LayoutEngine().Layout(calendar, new Size(32, 10));
        using Frame frame = new(new Size(32, 10), ambiguousWidth: Ambiguous.Wide);

        Should.NotThrow(() => calendar.Render(frame.Canvas));

        var inset = calendar.ActualStyle.ContentInset;
        var glyphPoint = new Point(calendar.ContentBounds.X + inset.Left, calendar.ContentBounds.Y + inset.Top);
        FrameOracle.Get(frame, glyphPoint).ShouldBe("<");
    }

    /// <summary>Verifies a Calendar's next-month navigation arrow resolves against the active width
    /// policy the same way the previous-month arrow does.</summary>
    [Fact]
    public void Calendar_WhenNextMonthGlyphIsAmbiguousAndAmbiguousWidthIsWide_RendersPortableGlyph()
    {
        var style = CalendarStyle.Default with { NextMonthGlyph = new Rune('•') };
        using var calendar = new UiCalendar
        {
            Culture = CultureInfo.InvariantCulture,
            DisplayMonth = new DateOnly(2026, 7, 1),
            Style = style
        };
        calendar.SetCellPolicy(new UnicodePolicy(Ambiguous.Wide));
        new LayoutEngine().Layout(calendar, new Size(32, 10));
        using Frame frame = new(new Size(32, 10), ambiguousWidth: Ambiguous.Wide);

        Should.NotThrow(() => calendar.Render(frame.Canvas));

        var inset = calendar.ActualStyle.ContentInset;
        var glyphPoint = new Point(calendar.ContentBounds.Right - inset.Right - 1, calendar.ContentBounds.Y + inset.Top);
        FrameOracle.Get(frame, glyphPoint).ShouldBe(">");
    }
}
