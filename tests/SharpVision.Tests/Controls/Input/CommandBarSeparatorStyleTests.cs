// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Verifies the immutable CommandBarSeparator presentation and glyph validation.</summary>
public sealed class CommandBarSeparatorStyleTests
{
    /// <summary>Verifies the default is passive chrome with a portable vertical divider.</summary>
    [Fact]
    public void Default_WhenRead_UsesDocumentedPresentation()
    {
        var style = CommandBarSeparatorStyle.Default;

        style.Face.ShouldBe(ControlStyle.Default.Face with { Background = SemanticColor.Bar });
        style.Border.ShouldBe(ControlStyle.Default.Border);
        style.Shadow.ShouldBe(ControlStyle.Default.Shadow);
        style.Glyph.ShouldBe(ControlGlyphs.Separators.Vertical);
    }

    /// <summary>Verifies constructor and with-expression assignment reject an invalid zero glyph pair.</summary>
    [Fact]
    public void Glyph_WhenNotOnePrintableCell_RejectsBeforeConstruction()
    {
        var valid = CommandBarSeparatorStyle.Default;

        _ = Should.Throw<ArgumentException>(() => new CommandBarSeparatorStyle(
            valid.Face,
            valid.Border,
            valid.Shadow,
            default));
        _ = Should.Throw<ArgumentException>(() => valid with { Glyph = default });
    }

    /// <summary>Verifies glyph geometry changes require measurement while color-only changes render.</summary>
    [Fact]
    public void Definition_Compare_WhenPresentationChanges_ReturnsExpectedImpact()
    {
        var style = CommandBarSeparatorStyle.Default;

        CommandBarSeparatorStyle.Definition.Compare(
                style,
                null,
                style with { Glyph = new ControlGlyph(new Rune('|'), new Rune('|')) },
                null)
            .ShouldBe(InvalidationImpact.Render);
        CommandBarSeparatorStyle.Definition.Compare(
                style,
                null,
                style with { Face = style.Face with { Foreground = SemanticColor.Accent } },
                null)
            .ShouldBe(InvalidationImpact.Render);
    }

    /// <summary>Verifies local separator styling round-trips through the typed slot.</summary>
    [Fact]
    public void Style_WhenAssigned_RoundTrips()
    {
        using var separator = new CommandBarSeparator();
        var local = CommandBarSeparatorStyle.Default with
        {
            Glyph = new ControlGlyph(new Rune('|'), new Rune('|'))
        };

        separator.Style = local;

        separator.Style.ShouldBe(local);
        separator.ActualStyle.ShouldBe(local);
    }
}
