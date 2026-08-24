// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Document.Tests;

// The project's own namespace, SharpVision.Document.Tests, nests textually under the SharpVision.Document
// segment, so an unqualified "Document" would otherwise resolve to that segment (as a namespace)
// rather than the Document control - this in-namespace alias, unlike a global one, takes priority
// over that enclosing-segment lookup in every position, including local-variable and return types.
using Document = Controls.Documents.Document;

/// <summary>Verifies <see cref="DocumentStyle"/> and <see cref="DocumentGlyphs"/> as values, and the
/// paint-time face and glyph resolution that keeps a live style or theme swap from leaving any part
/// of the tree stale.</summary>
public sealed class DocumentStyleTests
{
    /// <summary>Verifies every bundled theme resolves DocumentStyle to the code-owned face
    /// mapping - heading and marker on Accent, quote on ControlText, code and table on
    /// SurfaceText/Surface, rule on Muted, callout on Warning, link on Info, and literal bold
    /// weight on the marker face. All fifteen bundled themes used to author one byte-identical
    /// "sharpVision.document" section carrying exactly this mapping; it has since been folded into
    /// these code-owned defaults and deleted, so this pins that the fold changed nothing
    /// observable.</summary>
    [Fact]
    public void EveryTheme_WhenDocumentStyleResolves_UsesTheCodeOwnedDocumentProfile()
    {
        foreach (var slug in ThemeCatalog.Slugs)
        {
            var theme = ThemeCatalog.Load(slug);
            var style = DocumentStyle.Definition.Resolve(null, theme);

            style.MarkerFace.Attributes.Literal.ShouldBe(TerminalAttributes.Bold, slug);
            style.HeadingFace.Foreground.SemanticColor.ShouldBe(SemanticColor.Accent, slug);
            style.MarkerFace.Foreground.SemanticColor.ShouldBe(SemanticColor.Accent, slug);
            style.QuoteFace.Foreground.SemanticColor.ShouldBe(SemanticColor.ControlText, slug);
            style.CodeFace.Foreground.SemanticColor.ShouldBe(SemanticColor.SurfaceText, slug);
            style.CodeFace.Background.SemanticColor.ShouldBe(SemanticColor.Surface, slug);
            style.RuleFace.Foreground.SemanticColor.ShouldBe(SemanticColor.Muted, slug);
            style.CalloutFace.Foreground.SemanticColor.ShouldBe(SemanticColor.Warning, slug);
            style.CalloutTitleFace.Foreground.SemanticColor.ShouldBe(SemanticColor.Warning, slug);
            style.TableFace.Foreground.SemanticColor.ShouldBe(SemanticColor.SurfaceText, slug);
            style.TableHeaderFace.Foreground.SemanticColor.ShouldBe(SemanticColor.Accent, slug);
            style.LinkFace.Foreground.SemanticColor.ShouldBe(SemanticColor.Info, slug);
            style.ActiveLinkFace.Foreground.SemanticColor.ShouldBe(SemanticColor.SelectedText, slug);
            style.DisabledLinkFace.Foreground.SemanticColor.ShouldBe(SemanticColor.DisabledText, slug);
            style.ActionLinkFace.Foreground.SemanticColor.ShouldBe(SemanticColor.SelectedText, slug);
            style.ActiveActionLinkFace.Foreground.SemanticColor.ShouldBe(SemanticColor.PressedText, slug);
        }
    }

    /// <summary>Verifies the code-owned glyph family uses its documented ambiguous-width defaults.</summary>
    [Fact]
    public void Default_WhenGlyphsAreRead_UsesTheDocumentedFamily()
    {
        // Arrange and act
        var glyphs = DocumentGlyphs.Default;

        // Assert
        glyphs.FirstBullet.ShouldBe(new Rune('\u2022'));
        glyphs.SecondBullet.ShouldBe(new Rune('\u25E6'));
        glyphs.ThirdBullet.ShouldBe(new Rune('\u25AA'));
        glyphs.QuoteBar.ShouldBe(new Rune('\u2502'));
        glyphs.Rule.ShouldBe(new Rune('\u2500'));
    }

    /// <summary>Verifies the language default remains a valid glyph family rather than exposing
    /// control-code NUL values through the public value type.</summary>
    [Fact]
    public void DefaultValue_WhenGlyphsAreRead_UsesTheEstablishedFamily()
    {
        // Arrange and act
        var glyphs = default(DocumentGlyphs);

        // Assert
        glyphs.FirstBullet.ShouldBe(DocumentGlyphs.Default.FirstBullet);
        glyphs.SecondBullet.ShouldBe(DocumentGlyphs.Default.SecondBullet);
        glyphs.ThirdBullet.ShouldBe(DocumentGlyphs.Default.ThirdBullet);
        glyphs.QuoteBar.ShouldBe(DocumentGlyphs.Default.QuoteBar);
        glyphs.Rule.ShouldBe(DocumentGlyphs.Default.Rule);
    }

    /// <summary>Verifies the glyph family is a value, so two families built from the same runes
    /// compare equal and a replacement produces a distinct value.</summary>
    [Fact]
    public void Equals_WhenGlyphFamiliesCarryTheSameRunes_ComparesEqual()
    {
        // Arrange
        var first = DocumentGlyphs.Default;
        var second = new DocumentGlyphs(
            new Rune('\u2022'),
            new Rune('\u25E6'),
            new Rune('\u25AA'),
            new Rune('\u2502'),
            new Rune('\u2500'));

        // Act
        var replaced = first with { FirstBullet = new Rune('x') };

        // Assert
        second.ShouldBe(first);
        replaced.ShouldNotBe(first);
        replaced.FirstBullet.ShouldBe(new Rune('x'));
    }

    /// <summary>Verifies a glyph wider than one cell is rejected by the constructor under the
    /// argument's own name.</summary>
    [Fact]
    public void Constructor_WhenGlyphIsNotOneCell_ThrowsArgumentException()
    {
        // Arrange
        var wide = new Rune('\u754C');

        // Act and assert
        var exception = Should.Throw<ArgumentException>(() => new DocumentGlyphs(
            wide,
            DocumentGlyphs.Default.SecondBullet,
            DocumentGlyphs.Default.ThirdBullet,
            DocumentGlyphs.Default.QuoteBar,
            DocumentGlyphs.Default.Rule));
        exception.ParamName.ShouldBe("firstBullet");
    }

    /// <summary>Verifies a glyph replacement through a with-expression is validated just as the
    /// constructor validates its arguments.</summary>
    [Fact]
    public void Rule_WhenReplacedWithAWideGlyph_ThrowsArgumentException() =>
        // Arrange, act, and assert
        _ = Should.Throw<ArgumentException>(static () => DocumentGlyphs.Default with { Rule = new Rune('\u754C') });

    /// <summary>Verifies the standard document presentation authors every face and carries the
    /// code-owned glyph family.</summary>
    [Fact]
    public void Default_WhenStyleIsRead_AuthorsEveryFaceAndTheGlyphFamily()
    {
        // Arrange and act
        var style = DocumentStyle.Default;

        // Assert
        style.Glyphs.ShouldBe(DocumentGlyphs.Default);
        style.HeadingFace.ShouldNotBe(style.Face);
        style.ActiveLinkFace.ShouldNotBe(style.LinkFace);
        style.DisabledLinkFace.ShouldNotBe(style.LinkFace);
        style.QuoteFace.ShouldNotBe(style.CodeFace);
        style.MarkerFace.ShouldNotBe(style.RuleFace);
        style.ActionLinkFace.ShouldNotBe(style.LinkFace);
        style.ActiveActionLinkFace.ShouldNotBe(style.ActionLinkFace);
        style.ActiveActionLinkFace.ShouldNotBe(style.ActiveLinkFace);
        style.CalloutFace.Foreground.ShouldBe(style.CalloutTitleFace.Foreground);
        style.CalloutFace.Attributes.ShouldNotBe(style.CalloutTitleFace.Attributes);
    }

    /// <summary>Verifies the quote face keeps full contrast: it uses the same ControlText
    /// foreground as the ordinary body face rather than a separately colored, lower-contrast tone,
    /// and marks a quotation through italics instead of dimming it. A muted foreground on a
    /// transparent background previously left quoted text barely readable against a themed surface
    /// background.</summary>
    [Fact]
    public void Default_WhenQuoteFaceIsRead_PreservesContrastThroughItalicsNotColor()
    {
        // Arrange and act
        var style = DocumentStyle.Default;

        // Assert
        style.QuoteFace.Foreground.ShouldBe((ControlColor) SemanticColor.ControlText);
        style.QuoteFace.Background.ShouldBe((ControlColor) Color.Transparent);
        style.QuoteFace.Attributes.ShouldBe((ControlDecoration) TerminalAttributes.Italic);
    }

    /// <summary>Verifies a quoted paragraph renders with the same foreground as an ordinary
    /// paragraph, proving the quote face's inherited foreground actually reaches the painted cell
    /// rather than only the style value.</summary>
    [Fact]
    public void Render_WhenAParagraphIsQuoted_KeepsTheSameForegroundAsOrdinaryBodyText()
    {
        // Arrange
        var document = new Document
        {
            Blocks = { new DocumentParagraph("Body"), new DocumentBlockQuote("Quoted") }
        };

        // Act
        using var probe = new DocumentRenderProbe(document, new Size(20, 3));

        // Assert - the quote indents its content 2 cells past the bar, so quoted text starts there.
        probe.Row(0).ShouldBe("Body");
        probe.Row(2).ShouldBe("\u2502 Quoted");
        probe.Cell(0, 0).Style.Foreground.ShouldBe(probe.Cell(2, 2).Style.Foreground);
        probe.Cell(2, 2).Style.Attributes.ShouldBe(TerminalAttributes.Italic);
    }

    /// <summary>Verifies a callout's bar, title, and body share one semantic foreground while the
    /// title alone retains bold emphasis.</summary>
    [Fact]
    public async Task Render_WhenCalloutContainsBody_UsesOneForegroundAcrossTheEntireCalloutAsync()
    {
        // Arrange
        var callout = new DocumentCallout { Title = "Genuine controls" };
        callout.Blocks.Add(new DocumentParagraph("Body"));
        var document = new Document { Blocks = { callout } };
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(30, 2),
            ThemeCatalog.Load("default-dark"),
            TestContext.Current.CancellationToken);

        // Act
        var title = surface.Cell(new Point(2, 0)).Style;
        var body = surface.Cell(new Point(2, 1)).Style;
        var titleBar = surface.Cell(new Point(0, 0)).Style;
        var bodyBar = surface.Cell(new Point(0, 1)).Style;

        // Assert
        body.Foreground.ShouldBe(title.Foreground);
        titleBar.Foreground.ShouldBe(title.Foreground);
        bodyBar.Foreground.ShouldBe(title.Foreground);
        (title.Attributes & TerminalAttributes.Bold).ShouldBe(TerminalAttributes.Bold);
        (body.Attributes & TerminalAttributes.Bold).ShouldBe(TerminalAttributes.None);
    }

    /// <summary>Verifies the five standard Markdown alert kinds resolve through distinct semantic
    /// colors while every alert keeps one foreground across its bar, title, and body.</summary>
    [Fact]
    public async Task Render_WhenStandardCalloutKindsAreUsed_ResolvesTheirSemanticColorsAsync()
    {
        // Arrange
        var expected = new (string Kind, SemanticColor Color)[]
        {
            ("note", SemanticColor.Info),
            ("TIP", SemanticColor.Success),
            ("IMPORTANT", SemanticColor.Accent),
            ("WARNING", SemanticColor.Warning),
            ("CAUTION", SemanticColor.Error)
        };
        var document = new Document();

        foreach (var item in expected)
        {
            var callout = new DocumentCallout { Kind = item.Kind, Title = "Title" };
            callout.Blocks.Add(new DocumentParagraph("Body"));
            document.Blocks.Add(callout);
        }

        var theme = ThemeCatalog.Load("default-dark");
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(24, 14),
            theme,
            TestContext.Current.CancellationToken);

        // Act and assert
        for (var index = 0; index < expected.Length; index++)
        {
            var titleLine = index * 3;
            var bodyLine = titleLine + 1;
            var foreground = theme.ResolveColor(expected[index].Color);

            surface.Cell(new Point(0, titleLine)).Style.Foreground.ShouldBe(foreground, expected[index].Kind);
            surface.Cell(new Point(2, titleLine)).Style.Foreground.ShouldBe(foreground, expected[index].Kind);
            surface.Cell(new Point(0, bodyLine)).Style.Foreground.ShouldBe(foreground, expected[index].Kind);
            surface.Cell(new Point(2, bodyLine)).Style.Foreground.ShouldBe(foreground, expected[index].Kind);
        }
    }

    /// <summary>Verifies a callout's semantic foreground cascades through every nested block face
    /// while each descendant keeps its own typographic attributes and background.</summary>
    [Fact]
    public async Task Render_WhenCalloutContainsMixedBlocks_KeepsOneForegroundAcrossTheRegionAsync()
    {
        // Arrange
        var callout = new DocumentCallout { Kind = "WARNING", Title = "Mixed content" };
        callout.Blocks.Add(new DocumentHeading(2)
        {
            Inlines = { new DocumentTextRun("<info><b>Heading</b></info>") }
        });
        callout.Blocks.Add(new DocumentList { Items = { new DocumentListItem("Item") } });
        callout.Blocks.Add(new DocumentBlockQuote("Quote"));
        callout.Blocks.Add(new DocumentCodeBlock("Code"));
        callout.Blocks.Add(new DocumentSeparator());
        callout.Blocks.Add(new DocumentTable
        {
            Rows = { new DocumentTableRow { Cells = { new DocumentTableCell("Cell") } } }
        });
        var theme = ThemeCatalog.Load("default-dark");
        await using var surface = await ComponentSurface.MountAsync(
            new Document { Style = DocumentStyle.Default, Blocks = { callout } },
            new Size(32, 12),
            theme,
            TestContext.Current.CancellationToken);
        var expected = theme.ResolveColor(SemanticColor.Warning);
        var contentCells = new[]
        {
            new Point(0, 0),
            new Point(2, 0),
            new Point(2, 1),
            new Point(2, 3),
            new Point(4, 3),
            new Point(2, 5),
            new Point(4, 5),
            new Point(2, 7),
            new Point(2, 9),
            new Point(2, 11),
            new Point(4, 11)
        };

        // Act and assert
        foreach (var point in contentCells)
        {
            surface.Cell(point).Style.Foreground.ShouldBe(expected, point.ToString());
        }

        (surface.Cell(new Point(2, 1)).Style.Attributes & TerminalAttributes.Bold)
            .ShouldBe(TerminalAttributes.Bold);
        (surface.Cell(new Point(4, 5)).Style.Attributes & TerminalAttributes.Italic)
            .ShouldBe(TerminalAttributes.Italic);
    }

    /// <summary>Verifies a style whose only difference is one of the two action-link faces still
    /// reaches a mounted action-emphasis link's repaint, proving both new faces participate in the
    /// same style-change detection as every other face.</summary>
    [Fact]
    public async Task Style_WhenActionLinkFaceIsReplacedOnAMountedDocument_RepaintsTheActionLinkAsync()
    {
        // Arrange
        var link = new DocumentLink("Go") { Emphasis = DocumentLinkEmphasis.Action };
        var paragraph = new DocumentParagraph();
        paragraph.Inlines.Add(link);
        var document = new Document { Blocks = { paragraph } };
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(6, 1),
            TestContext.Current.CancellationToken);
        var before = surface.Cell(new Point(0, 0)).Style;

        // Act
        await surface.UpdateAsync(
            () => document.Style = DocumentStyle.Default with
            {
                ActionLinkFace = DocumentStyle.Default.ActionLinkFace with
                {
                    Foreground = new ControlColor(SemanticColor.Error)
                }
            },
            "replace action link face");

        // Assert
        surface.Cell(new Point(0, 0)).Style.ShouldNotBe(before);
        surface.Cell(new Point(0, 0)).Text.ShouldBe("G");
    }

    /// <summary>Verifies a document with no local style reports the resolved style, and that
    /// assigning and clearing a local style is observable on both properties.</summary>
    [Fact]
    public void Style_WhenAssignedAndCleared_TracksLocalOwnership()
    {
        // Arrange
        var document = new Document();
        var replacement = DocumentStyle.Default with { Glyphs = DocumentGlyphs.Default with { Rule = new Rune('=') } };

        // Act
        document.Style.ShouldBeNull();
        document.Style = replacement;

        // Assert
        document.Style.ShouldBe(replacement);
        document.ActualStyle.ShouldBe(replacement);

        // Act clear
        document.Style = null;

        // Assert
        document.Style.ShouldBeNull();
        document.ActualStyle.Glyphs.ShouldBe(DocumentGlyphs.Default);
    }

    /// <summary>Verifies the generated vertical bar's style forwards to and resolves through the
    /// private scrolling host.</summary>
    [Fact]
    public void ScrollBarStyle_WhenAssignedOrUnassigned_ForwardsToTheGeneratedBar()
    {
        // Arrange
        var document = new Document();

        // Act and assert
        document.ScrollBarStyle.ShouldBeNull();
        document.ActualScrollBarStyle.ShouldBe(ScrollBar.ResolveStyle(null, document.Theme));

        // Act
        var style = ScrollBar.ResolveStyle(null, document.Theme) with { };
        document.ScrollBarStyle = style;

        // Assert
        document.ScrollBarStyle.ShouldBe(style);
        document.ActualScrollBarStyle.ShouldBe(style);
    }

    /// <summary>Verifies assigning a style whose heading face differs restyles the mounted heading on
    /// the next frame with no other action, because faces resolve during the paint pass instead of
    /// being cached onto a node when it is created.</summary>
    [Fact]
    public async Task Style_WhenHeadingFaceIsReplacedOnAMountedDocument_RepaintsTheHeadingAsync()
    {
        // Arrange
        var document = new Document { Blocks = { new DocumentHeading(1, "T") } };
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(6, 1),
            TestContext.Current.CancellationToken);
        var before = surface.Cell(new Point(0, 0)).Style;

        // Act
        await surface.UpdateAsync(
            () => document.Style = DocumentStyle.Default with
            {
                HeadingFace = DocumentStyle.Default.HeadingFace with
                {
                    Foreground = new ControlColor(SemanticColor.Error)
                }
            },
            "replace heading face");

        // Assert
        var after = surface.Cell(new Point(0, 0)).Style;
        after.ShouldNotBe(before);
        surface.Cell(new Point(0, 0)).Text.ShouldBe("T");
    }

    /// <summary>Verifies assigning a style whose glyph family differs re-measures and repaints the
    /// mounted markers, bars, and rules on the next frame with no other action.</summary>
    [Fact]
    public async Task Style_WhenGlyphsAreReplacedOnAMountedDocument_RepaintsMarkersBarsAndRulesAsync()
    {
        // Arrange
        var list = new DocumentList { Items = { new DocumentListItem("A") } };
        var document = new Document
        {
            Blocks = { list, new DocumentBlockQuote("Q"), new DocumentSeparator() }
        };
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(8, 5),
            TestContext.Current.CancellationToken);
        surface.Cell(new Point(0, 0)).Text.ShouldBe("\u2022");
        surface.Cell(new Point(0, 2)).Text.ShouldBe("\u2502");
        surface.Cell(new Point(0, 4)).Text.ShouldBe("\u2500");

        // Act
        await surface.UpdateAsync(
            () => document.Style = DocumentStyle.Default with
            {
                Glyphs = DocumentGlyphs.Default with
                {
                    FirstBullet = new Rune('#'),
                    QuoteBar = new Rune('!'),
                    Rule = new Rune('=')
                }
            },
            "replace glyph family");

        // Assert
        surface.Cell(new Point(0, 0)).Text.ShouldBe("#");
        surface.Cell(new Point(0, 2)).Text.ShouldBe("!");
        surface.Cell(new Point(0, 4)).Text.ShouldBe("=");
    }

    /// <summary>Verifies a disabled document dims uniformly: every heading, marker, quote bar, code,
    /// and link cell resolves to the same disabled body style the paragraphs use, so nothing stays
    /// bright while the text around it fades.</summary>
    [Fact]
    public async Task IsEnabled_WhenDocumentIsDisabled_ResolvesEveryFaceToTheDisabledBodyStyleAsync()
    {
        // Arrange
        var linkParagraph = new DocumentParagraph();
        linkParagraph.Inlines.Add(new DocumentLink("L"));
        var list = new DocumentList { Items = { new DocumentListItem("A") } };
        var document = new Document
        {
            Blocks =
            {
                new DocumentHeading(1, "H"),
                new DocumentParagraph { Inlines = { new DocumentTextRun("<error>P</error>") } },
                list,
                new DocumentBlockQuote("Q"),
                new DocumentCodeBlock("C"),
                linkParagraph
            }
        };
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(8, 11),
            TestContext.Current.CancellationToken);

        // Assert enabled - a heading is genuinely distinct from body text before the transition.
        surface.Cell(new Point(0, 0)).Style.ShouldNotBe(surface.Cell(new Point(0, 2)).Style);

        // Act
        await surface.UpdateAsync(() => document.IsEnabled = false, "disable document");

        // Assert - heading, marker, quote bar, code, and link all match the disabled paragraph.
        var body = surface.Cell(new Point(0, 2)).Style;
        surface.Cell(new Point(0, 0)).Style.ShouldBe(body);
        surface.Cell(new Point(0, 4)).Style.ShouldBe(body);
        surface.Cell(new Point(0, 6)).Style.ShouldBe(body);
        surface.Cell(new Point(0, 8)).Style.ShouldBe(body);
        surface.Cell(new Point(0, 10)).Style.ShouldBe(body);
        surface.Cell(new Point(0, 0)).Text.ShouldBe("H");
        surface.Cell(new Point(0, 10)).Text.ShouldBe("L");
    }

    /// <summary>Verifies a live theme swap repaints semantic document faces even when the symbolic style is unchanged.</summary>
    [Fact]
    public async Task Theme_WhenOnlyDocumentSemanticColorChanges_RepaintsTheMountedDocumentAsync()
    {
        // Arrange
        var themeA = WithSemanticColor(SemanticColor.Accent, Color.Rgb(10, 20, 30));
        var themeB = WithSemanticColor(SemanticColor.Accent, Color.Rgb(210, 200, 190));
        var document = new Document { Blocks = { new DocumentHeading(1, "Heading") } };
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(10, 1),
            themeA,
            TestContext.Current.CancellationToken);
        surface.Cell(new Point(0, 0)).Style.Foreground.ShouldBe(
            TerminalPalette.Project(themeA.ResolveColor(SemanticColor.Accent), ColorDepth.Basic16));

        // Act
        await surface.UpdateAsync(() => surface.Application.Theme = themeB, "swap document semantic color");

        // Assert
        surface.Cell(new Point(0, 0)).Style.Foreground.ShouldBe(
            TerminalPalette.Project(themeB.ResolveColor(SemanticColor.Accent), ColorDepth.Basic16));
    }

    /// <summary>Verifies callout-only semantic roles participate in theme invalidation even when a
    /// local literal document style no longer references those roles.</summary>
    [Fact]
    public async Task Theme_WhenOnlyCalloutSemanticColorChanges_RepaintsTheCalloutAsync()
    {
        // Arrange
        var themeA = WithSemanticColor(SemanticColor.Success, Color.Rgb(10, 20, 30));
        var themeB = WithSemanticColor(SemanticColor.Success, Color.Rgb(240, 230, 220));
        var callout = new DocumentCallout { Kind = "TIP", Title = "Tip" };
        var document = new Document
        {
            Style = DocumentStyle.Default,
            Blocks = { callout }
        };
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(12, 1),
            themeA,
            TestContext.Current.CancellationToken);
        var before = surface.Cell(new Point(2, 0)).Style.Foreground;

        // Act
        await surface.UpdateAsync(() => surface.Application.Theme = themeB, "swap callout semantic color");

        // Assert
        surface.Cell(new Point(2, 0)).Style.Foreground.ShouldNotBe(before);
    }

    /// <summary>Verifies a theme-owned FocusedText semantic-decoration change does not repaint the
    /// document. Every DocumentStyle face that once resolved its weight through that symbolic
    /// decoration now carries a literal Bold instead, precisely so a theme that redefines
    /// FocusedText for its own interactive-focus purposes (for example to Reverse) cannot also
    /// reskin this static typography.</summary>
    [Fact]
    public async Task Theme_WhenOnlyDocumentSemanticDecorationChanges_LeavesTheDocumentUnaffectedAsync()
    {
        // Arrange
        var themeA = WithSemanticDecoration(SemanticDecoration.FocusedText, TerminalAttributes.None);
        var themeB = WithSemanticDecoration(SemanticDecoration.FocusedText, TerminalAttributes.Reverse);
        var document = new Document
        {
            Blocks = { new DocumentList { Items = { new DocumentListItem("Item") } } }
        };
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(12, 1),
            themeA,
            TestContext.Current.CancellationToken);
        var before = surface.Cell(new Point(0, 0)).Style.Attributes;

        // Act
        await surface.UpdateAsync(() => surface.Application.Theme = themeB, "swap document semantic decoration");

        // Assert
        surface.Cell(new Point(0, 0)).Style.Attributes.ShouldBe(before);
        (before & TerminalAttributes.Bold).ShouldBe(TerminalAttributes.Bold);
    }

    private static Theme WithSemanticColor(SemanticColor role, Color value)
    {
        var source = ThemeCatalog.Dark;
        var theme = new Theme(
            source.Palette,
            source.Name,
            source.Slug,
            source.ColorScheme,
            source.Author,
            source.License,
            source.Source);

        foreach (var color in Enum.GetValues<SemanticColor>())
        {
            theme.SetColor(color, color == role ? value : source.ResolveColor(color));
        }

        foreach (var decoration in Enum.GetValues<SemanticDecoration>())
        {
            theme.SetAttributes(decoration, source.ResolveAttributes(decoration));
        }

        theme.SetStyleSections(source.StyleSections.ToDictionary(static pair => pair.Key, static pair => pair.Value));
        theme.Freeze();
        return theme;
    }

    private static Theme WithSemanticDecoration(SemanticDecoration role, TerminalAttributes value)
    {
        var source = ThemeCatalog.Dark;
        var theme = new Theme(
            source.Palette,
            source.Name,
            source.Slug,
            source.ColorScheme,
            source.Author,
            source.License,
            source.Source);

        foreach (var color in Enum.GetValues<SemanticColor>())
        {
            theme.SetColor(color, source.ResolveColor(color));
        }

        foreach (var decoration in Enum.GetValues<SemanticDecoration>())
        {
            theme.SetAttributes(decoration, decoration == role ? value : source.ResolveAttributes(decoration));
        }

        theme.SetStyleSections(source.StyleSections.ToDictionary(static pair => pair.Key, static pair => pair.Value));
        theme.Freeze();
        return theme;
    }
}
