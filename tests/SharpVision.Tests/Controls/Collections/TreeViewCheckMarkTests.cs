// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Collections;

/// <summary>
/// Verifies tree check marks reach the same mark families a standalone CheckBox offers, with a
/// shared reservation, hit area, and degradation.
/// </summary>
public sealed class TreeViewCheckMarkTests
{
    /// <summary>
    /// Verifies a tree defaults to the same bracket mark a standalone CheckBox uses, so the two
    /// controls no longer disagree about what an unconfigured check mark looks like.
    /// </summary>
    [Fact]
    public void ActualCheckMark_WhenUnassigned_IsTheBracketFamily()
    {
        var tree = new TreeView();
        var item = new TreeViewItem { Header = "a", Checkable = true };
        tree.Items.Add(item);

        tree.CheckMark.ShouldBeNull();
        item.CheckMark.ShouldBeNull();
        tree.ActualCheckMark.ShouldBe(CheckMark.Brackets);
        item.ActualCheckMark.ShouldBe(CheckMark.Brackets);
        item.ActualCheckMark.Width.ShouldBe(3);
        default(CheckMark).ShouldBe(CheckMark.Brackets);
    }

    /// <summary>Verifies precedence runs local item, then owning tree, then library default.</summary>
    [Fact]
    public void ActualCheckMark_WhenTreeAndItemBothAssign_PrefersTheItem()
    {
        var tree = new TreeView { CheckMark = CheckMark.Tick };
        var inherited = new TreeViewItem { Header = "a", Checkable = true };
        var overridden = new TreeViewItem { Header = "b", Checkable = true, CheckMark = CheckMark.Brackets };
        tree.Items.Add(inherited);
        tree.Items.Add(overridden);

        inherited.ActualCheckMark.ShouldBe(CheckMark.Tick);
        overridden.ActualCheckMark.ShouldBe(CheckMark.Brackets);

        overridden.CheckMark = null;

        overridden.ActualCheckMark.ShouldBe(CheckMark.Tick);
    }

    /// <summary>Verifies a detached item still resolves the library default.</summary>
    [Fact]
    public void ActualCheckMark_WhenItemIsDetached_ResolvesTheLibraryDefault()
    {
        var item = new TreeViewItem { Header = "a", Checkable = true };

        item.ActualCheckMark.ShouldBe(CheckMark.Brackets);
    }

    /// <summary>Verifies a width change invalidates measure while a glyph change does not.</summary>
    [Fact]
    public void CheckMark_WhenWidthChanges_InvalidatesMeasure()
    {
        var tree = new TreeView();
        var item = new TreeViewItem { Header = "a", Checkable = true };
        tree.Items.Add(item);
        List<string?> changed = [];
        item.PropertyChanged += (_, eventArgs) => changed.Add(eventArgs.PropertyName);

        item.CheckMark = CheckMark.Brackets;

        changed.ShouldContain(nameof(TreeViewItem.CheckMark));
        item.Pending.ShouldNotBe(Invalidation.None);
        item.ActualCheckMark.Width.ShouldBe(3);
    }

    /// <summary>Verifies assigning the same mark publishes nothing.</summary>
    [Fact]
    public void CheckMark_WhenUnchanged_RaisesNothing()
    {
        var item = new TreeViewItem { Header = "a", Checkable = true, CheckMark = CheckMark.Tick };
        List<string?> changed = [];
        item.PropertyChanged += (_, eventArgs) => changed.Add(eventArgs.PropertyName);

        item.CheckMark = CheckMark.Tick;

        changed.ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies the glyph projection publishes a notification, which the previous glyph-only
    /// surface never did because it bypassed the standard mutation path.
    /// </summary>
    [Fact]
    public void CheckGlyphs_WhenAssigned_RaisesPropertyChangedAndKeepsTheMarkLayout()
    {
        var item = new TreeViewItem { Header = "a", Checkable = true, CheckMark = CheckMark.Brackets };
        List<string?> changed = [];
        item.PropertyChanged += (_, eventArgs) => changed.Add(eventArgs.PropertyName);
        var glyphs = new CheckBoxGlyphs(new Rune('.'), new Rune('x'), new Rune('-'));

        item.CheckGlyphs = glyphs;

        changed.ShouldContain(nameof(TreeViewItem.CheckGlyphs));
        item.CheckGlyphs.ShouldBe(glyphs);

        // The projection replaces glyphs only; the three-cell bracket layout survives.
        item.ActualCheckMark.MarkStyle.ShouldBe(CheckBoxMarkStyle.Brackets);
        item.ActualCheckMark.Width.ShouldBe(3);
    }

    /// <summary>Verifies the glyph projection reports the resolved glyphs rather than a local copy.</summary>
    [Fact]
    public void CheckGlyphs_WhenTreeSuppliesTheMark_ReportsTheResolvedGlyphs()
    {
        var tree = new TreeView { CheckMark = CheckMark.Tick };
        var item = new TreeViewItem { Header = "a", Checkable = true };
        tree.Items.Add(item);

        item.CheckGlyphs.ShouldBe(CheckMark.Tick.Glyphs);
    }

    /// <summary>Verifies an invalid glyph is rejected before any state changes.</summary>
    [Fact]
    public void CheckGlyphs_WhenGlyphIsNotOneCell_Throws()
    {
        var item = new TreeViewItem { Header = "a", Checkable = true };

        _ = Should.Throw<ArgumentException>(
            () => item.CheckGlyphs = new CheckBoxGlyphs(new Rune('\u4f60'), new Rune('x'), new Rune('-')));
        item.ActualCheckMark.ShouldBe(CheckMark.Brackets);
    }

    /// <summary>Verifies an undefined mark family is rejected by the shared value type.</summary>
    [Fact]
    public void CheckMark_WhenMarkStyleIsUndefined_Throws() =>
        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => new CheckMark((CheckBoxMarkStyle) 42, CheckBoxGlyphs.Default));

    /// <summary>
    /// Verifies the measured width matches what the row actually draws for every mark family and
    /// for a non-checkable row, which previously disagreed by one cell.
    /// </summary>
    /// <param name="checkable">Whether the row draws a mark.</param>
    /// <param name="reserved">The expected gap plus mark reservation in cells.</param>
    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 4)]
    public void MeasureOverride_WhenRowIsMeasured_MatchesTheRenderedLayout(bool checkable, int reserved)
    {
        var tree = new TreeView();
        var item = new TreeViewItem { Header = "abc", Checkable = checkable };
        tree.Items.Add(item);

        item.Measure(new Constraint(80, 1));

        // indent(0) + disclosure(1) + [gap(1) + mark] + leading space(1) + header(3)
        item.DesiredSize.Width.ShouldBe(1 + reserved + 1 + 3);
    }

    /// <summary>Verifies a pointer press lands on the disclosure glyph or the check mark at
    /// exactly the columns OnRenderContent draws them at, matching the hit-zone arithmetic in
    /// OnEvent cell-for-cell against the render arithmetic rather than merely against the total
    /// measured width (see MeasureOverride_WhenRowIsMeasured_MatchesTheRenderedLayout above).
    /// </summary>
    [Fact]
    public void OnEvent_WhenPressedAtRenderedGlyphColumns_TogglesExpansionAndCheckState()
    {
        var tree = new TreeView { Bounds = new Rect(0, 0, 20, 3) };
        var item = new TreeViewItem { Header = "abc", Checkable = true };
        item.Children.Add(new TreeViewItem { Header = "child" });
        tree.Items.Add(item);
        new LayoutEngine().Layout(tree, new Size(20, 3));

        // Column layout: [disclosure][gap][check mark (3 cells)][leading space]header
        var glyphX = item.ContentBounds.X;
        var checkX = glyphX + 1 + 1;

        item.Expanded.ShouldBeTrue();
        _ = Press(item, glyphX);
        item.Expanded.ShouldBeFalse();

        _ = Press(item, checkX + 2);
        item.IsChecked.ShouldBe(true);

        return;

        static RouteResult Press(TreeViewItem target, int x) =>
            Router.Route(
                target,
                Events.Pointer,
                new PointerEventArgs(new Pointer(
                    new Point(x, target.Bounds.Y),
                    pixels: null,
                    Buttons.Primary,
                    PointerAction.Press,
                    wheelX: 0,
                    wheelY: 0,
                    Modifiers.None,
                    isMotion: false,
                    isCellPositionInferred: false)));
    }

    /// <summary>Verifies a one-cell family reserves correspondingly less than the bracket default.</summary>
    [Fact]
    public void MeasureOverride_WhenMarkIsOneCell_ReservesTwoFewerCells()
    {
        var tree = new TreeView { CheckMark = CheckMark.Tick };
        var item = new TreeViewItem { Header = "abc", Checkable = true };
        tree.Items.Add(item);

        item.Measure(new Constraint(80, 1));

        // indent(0) + disclosure(1) + gap(1) + mark(1) + leading space(1) + header(3)
        item.DesiredSize.Width.ShouldBe(1 + 1 + 1 + 1 + 3);
    }
}
