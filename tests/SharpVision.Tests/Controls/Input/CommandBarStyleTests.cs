// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Verifies the immutable CommandBar presentation and its invalidation policy.</summary>
public sealed class CommandBarStyleTests
{
    /// <summary>Verifies the default is a passive borderless strip with a one-cell overflow glyph.</summary>
    [Fact]
    public void Default_WhenRead_UsesDocumentedPresentation()
    {
        var style = CommandBarStyle.Default;

        style.Face.ShouldBe(ControlStyle.Default.Face);
        style.Border.ShouldBe(ControlStyle.Default.Border);
        style.Shadow.ShouldBe(ControlStyle.Default.Shadow);
        style.Padding.ShouldBe(default);
        style.OverflowGlyph.ShouldBe(ControlGlyphs.Text.Ellipsis);
        style.OverflowColor.ShouldBe((ControlColor) SemanticColor.ControlText);
    }

    /// <summary>Verifies constructor and with-expression glyph replacement both reject invalid defaults.</summary>
    [Fact]
    public void OverflowGlyph_WhenNotOnePrintableCell_RejectsBeforeConstruction()
    {
        var valid = CommandBarStyle.Default;

        _ = Should.Throw<ArgumentException>(() => new CommandBarStyle(
            valid.Face,
            valid.Border,
            valid.Shadow,
            valid.Padding,
            default,
            valid.OverflowColor));
        _ = Should.Throw<ArgumentException>(() => valid with { OverflowGlyph = default });
    }

    /// <summary>Verifies a transparent overflow foreground is invalid through every authoring route.</summary>
    [Fact]
    public void OverflowColor_WhenTransparent_RejectsBeforeConstruction()
    {
        var valid = CommandBarStyle.Default;
        var transparent = new ControlColor(Color.Transparent);

        _ = Should.Throw<ArgumentException>(() => new CommandBarStyle(
            valid.Face,
            valid.Border,
            valid.Shadow,
            valid.Padding,
            valid.OverflowGlyph,
            transparent));
        _ = Should.Throw<ArgumentException>(() => valid with { OverflowColor = transparent });
    }

    /// <summary>Verifies structural and color changes report their earliest affected phase.</summary>
    [Fact]
    public void Definition_Compare_WhenMembersChange_ReturnsExpectedImpact()
    {
        var style = CommandBarStyle.Default;

        CommandBarStyle.Definition.Compare(
            style,
            null,
            style with { Padding = new Thickness(1) },
            null)
            .ShouldBe(InvalidationImpact.Measure);
        CommandBarStyle.Definition.Compare(
            style,
            null,
            style with { OverflowGlyph = new ControlGlyph(new Rune('>'), new Rune('>')) },
            null)
            .ShouldBe(InvalidationImpact.Render);
        CommandBarStyle.Definition.Compare(
                style,
                null,
                style with { OverflowColor = SemanticColor.Accent },
                null)
            .ShouldBe(InvalidationImpact.Render);
    }

    /// <summary>Verifies local assignment is authoritative and clearing restores theme ownership.</summary>
    [Fact]
    public void Style_WhenAssignedThenCleared_RestoresResolvedDefault()
    {
        using var bar = new CommandBar();
        var local = CommandBarStyle.Default with { OverflowColor = SemanticColor.Warning };

        bar.Style = local;
        bar.ActualStyle.ShouldBe(local);

        bar.Style = null;
        bar.ActualStyle.ShouldBe(CommandBarStyle.Definition.Resolve(null, ThemeCatalog.Dark));
    }
}
