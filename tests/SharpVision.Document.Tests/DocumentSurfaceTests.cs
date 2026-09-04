// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Document.Tests;

using SharpVision.Text;

using Document = Controls.Document.Document;

/// <summary>Verifies a nested document exports its semantic projection rather than its private
/// presentation tree to an enclosing document selection.</summary>
public sealed class DocumentSurfaceTests
{
    /// <summary>Verifies block separators surround nested semantic content and the nested
    /// document's own local selection never limits what it exports.</summary>
    [Fact]
    public async Task SelectAll_WhenDocumentContainsDocument_CopiesCompleteNestedSemanticStreamAsync()
    {
        // Arrange
        var inner = new Document { Blocks = { new DocumentParagraph("inner") } };
        var outer = new Document
        {
            Blocks =
            {
                new DocumentParagraph("before"),
                new DocumentBlockControl(inner),
                new DocumentParagraph("after")
            }
        };
        await using var surface = await ComponentSurface.MountAsync(
            outer,
            new Size(24, 8),
            TestContext.Current.CancellationToken);

        // Act
        await surface.UpdateAsync(
            () =>
            {
                inner.SetSelection(new Selection(1, 3));
                outer.SelectAll();
            },
            "select nested and outer documents");

        // Assert
        var result = await surface.Application.Dispatcher.InvokeAsync(
            () => (outer.CopySelection(), inner.GetSelectableTextSnapshot().Text),
            TestContext.Current.CancellationToken);
        result.ShouldBe(("before\ninner\nafter", "inner"));
    }

    /// <summary>Verifies changing nested semantic content invalidates an enclosing selection once
    /// and immediately supplies the replacement stream.</summary>
    [Fact]
    public async Task SelectedText_WhenNestedDocumentChanges_ClearsOuterSelectionExactlyOnceAsync()
    {
        // Arrange
        var run = new DocumentTextRun("old");
        var paragraph = new DocumentParagraph { Inlines = { run } };
        var inner = new Document { Blocks = { paragraph } };
        var outer = new Document
        {
            Blocks = { new DocumentParagraph("before"), new DocumentBlockControl(inner) }
        };
        await using var surface = await ComponentSurface.MountAsync(
            outer,
            new Size(20, 6),
            TestContext.Current.CancellationToken);
        var observed = new List<Selection>();
        await surface.UpdateAsync(
            () =>
            {
                outer.SelectAll();
                outer.SelectionChanged += (_, _) => observed.Add(outer.Selection);
                run.Text = "new";
            },
            "replace nested document text");

        // Act
        var state = await surface.Application.Dispatcher.InvokeAsync(
            () => (outer.Selection, outer.SelectedText, outer.GetSelectableTextSnapshot().Text),
            TestContext.Current.CancellationToken);

        // Assert
        state.ShouldBe((default(Selection), string.Empty, "before\nnew"));
        observed.ShouldBe([default]);
    }

    /// <summary>Verifies a partial outer range highlights nested glyphs after both documents paint
    /// without recoloring either document's intrinsic border.</summary>
    [Fact]
    public async Task Render_WhenOuterSelectionTargetsNestedText_HighlightsTextWithoutChromeAsync()
    {
        // Arrange
        var selectionBackground = Color.Rgb(255, 0, 255);
        var border = new Border(
            BorderSide.All,
            BorderGlyphStyle.Rounded,
            SemanticColor.ControlBorder,
            Color.Transparent,
            SemanticDecoration.Border);
        var inner = new Document
        {
            Width = Length.Cells(9),
            Height = Length.Cells(4),
            Style = DocumentStyle.Default with { Border = border },
            Blocks = { new DocumentParagraph("inner") }
        };
        var outer = new Document
        {
            Style = DocumentStyle.Default with
            {
                Border = border,
                SelectionFace = new Face(
                    Color.Rgb(250, 250, 250),
                    selectionBackground,
                    TerminalAttributes.Bold,
                    Underline.None,
                    Color.Default)
            },
            Blocks =
            {
                new DocumentParagraph("before"),
                new DocumentBlockControl(inner),
                new DocumentParagraph("after")
            }
        };
        await using var surface = await ComponentSurface.MountAsync(
            outer,
            new Size(24, 9),
            TestContext.Current.CancellationToken);
        var geometry = await surface.Application.Dispatcher.InvokeAsync(
            () =>
            {
                var source = outer.SelectionMap.Sources.Single(item => ReferenceEquals(item.Source, inner));
                var glyph = inner.GetSelectableTextSnapshot().Glyphs[0];
                return (
                    source.Range,
                    Text: new Point(inner.Bounds.X + glyph.Bounds.X, inner.Bounds.Y + glyph.Bounds.Y),
                    InnerChrome: new Point(inner.Bounds.X, inner.Bounds.Y),
                    OuterChrome: new Point(outer.Bounds.X, outer.Bounds.Y));
            },
            TestContext.Current.CancellationToken);
        var innerChromeBefore = surface.Cell(geometry.InnerChrome).Style;
        var outerChromeBefore = surface.Cell(geometry.OuterChrome).Style;

        // Act
        await surface.UpdateAsync(
            () => outer.SetSelection(geometry.Range),
            "select nested document text from outer document");

        // Assert
        surface.Cell(geometry.Text).Style.Background.ShouldBe(
            TerminalPalette.Project(selectionBackground, ColorDepth.Basic16));
        surface.Cell(geometry.InnerChrome).Style.ShouldBe(innerChromeBefore);
        surface.Cell(geometry.OuterChrome).Style.ShouldBe(outerChromeBefore);
        (await surface.Application.Dispatcher.InvokeAsync(
            outer.CopySelection,
            TestContext.Current.CancellationToken)).ShouldBe("inner");
    }

    /// <summary>Verifies nested exports retain clipped semantic-only text while visible wide
    /// graphemes remain atomic and move with the nested document's own viewport.</summary>
    [Fact]
    public async Task Snapshot_WhenNestedDocumentScrolls_ExportsFullTextAndVisibleWideGeometryAsync()
    {
        // Arrange
        var inner = new Document
        {
            Width = Length.Cells(8),
            Height = Length.Cells(3),
            ShowScrollBars = ShowScrollBars.Never,
            Blocks =
            {
                new DocumentParagraph("top"),
                new DocumentParagraph("界"),
                new DocumentParagraph("bottom")
            }
        };
        var outer = new Document { Blocks = { new DocumentBlockControl(inner) } };
        await using var surface = await ComponentSurface.MountAsync(
            outer,
            new Size(12, 5),
            TestContext.Current.CancellationToken);
        var before = await surface.Application.Dispatcher.InvokeAsync(
            inner.GetSelectableTextSnapshot,
            TestContext.Current.CancellationToken);

        // Act
        await surface.UpdateAsync(() => inner.VerticalOffset = 1, "scroll nested document");
        var after = await surface.Application.Dispatcher.InvokeAsync(
            inner.GetSelectableTextSnapshot,
            TestContext.Current.CancellationToken);

        // Assert
        before.IsAuthoritative.ShouldBeTrue();
        before.Text.ShouldBe("top\n界\nbottom");
        after.Text.ShouldBe(before.Text);
        before.Glyphs.ShouldContain(glyph => glyph.Range == new Selection(0, 1));
        after.Glyphs.ShouldNotContain(glyph => glyph.Range.Start < 4);
        var wide = after.Glyphs.Single(glyph => glyph.Range == new Selection(4, 5));
        wide.Bounds.Width.ShouldBe(2);
        wide.Bounds.X.ShouldBeGreaterThanOrEqualTo(0);
        wide.Bounds.Y.ShouldBeGreaterThanOrEqualTo(0);
        wide.Bounds.Right.ShouldBeLessThanOrEqualTo(inner.Bounds.Width);
        wide.Bounds.Bottom.ShouldBeLessThanOrEqualTo(inner.Bounds.Height);
    }

    /// <summary>Verifies generic document autoscroll offers an edge request to a nested document's
    /// intrinsic selectable viewport before moving the enclosing document.</summary>
    [Fact]
    public async Task AutoScrollSelection_WhenNestedDocumentCanScroll_MovesInnerViewportFirstAsync()
    {
        // Arrange
        var inner = new Document
        {
            Width = Length.Cells(8),
            Height = Length.Cells(2),
            ShowScrollBars = ShowScrollBars.Never,
            Blocks =
            {
                new DocumentParagraph("one"),
                new DocumentParagraph("two"),
                new DocumentParagraph("three")
            }
        };
        var outer = new Document
        {
            Height = Length.Cells(3),
            Blocks =
            {
                new DocumentBlockControl(inner),
                new DocumentParagraph("outer tail")
            }
        };
        await using var surface = await ComponentSurface.MountAsync(
            outer,
            new Size(10, 3),
            TestContext.Current.CancellationToken);

        // Act
        var result = await surface.Application.Dispatcher.InvokeAsync(
            () =>
            {
                var source = outer.SelectionMap.Sources.Single(item => ReferenceEquals(item.Source, inner));
                var moved = outer.AutoScrollTextSelection(
                    new Point(inner.Bounds.X, inner.Bounds.Bottom),
                    source,
                    out _);
                return (moved, Inner: inner.VerticalOffset, Outer: outer.VerticalOffset);
            },
            TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBe((true, 1, 0));
    }

    /// <summary>Verifies keyboard focus is genuinely observable on a mounted Document's own
    /// rendered body through the theme's focused text cue without reversing the whole surface
    /// behind its independently colored heading, quote, code, table, and link faces.</summary>
    [Fact]
    public async Task Input_WhenMountedDocumentReceivesFocus_UsesTextCueWithoutReversingSurfaceAsync()
    {
        // Arrange
        var document = new Document { Blocks = { new DocumentParagraph("Body") } };
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(12, 2),
            ThemeCatalog.Load("default-dark"),
            TestContext.Current.CancellationToken);
        var restingStyle = surface.Cell(default).Style;
        (restingStyle.Attributes & TerminalAttributes.Bold).ShouldBe(TerminalAttributes.None);

        // Act
        await surface.UpdateAsync(() => document.Focus().ShouldBeTrue(), "focus document");

        // Assert
        document.IsFocused.ShouldBeTrue();
        var focusedStyle = surface.Cell(default).Style;
        (focusedStyle.Attributes & TerminalAttributes.Bold).ShouldBe(TerminalAttributes.Bold);
        (focusedStyle.Attributes & TerminalAttributes.Reverse).ShouldBe(TerminalAttributes.None);
        focusedStyle.Foreground.ShouldBe(restingStyle.Foreground);
        focusedStyle.Background.ShouldBe(restingStyle.Background);
    }
}
