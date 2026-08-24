// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting.Tests;

/// <summary>Verifies <see cref="CodeViewStyle"/>'s own paint and glyph validation wiring.</summary>
/// <remarks>
/// <see cref="ControlColor"/>'s transparent rejection and <c>Rune.ValidateSingleCell</c>'s width
/// rejection are already exhaustively unit-tested in isolation. These tests instead prove the
/// wiring: that <see cref="CodeViewStyle"/>'s own constructor and <c>init</c> accessors actually
/// invoke that shared validation, through a few representative color and glyph properties rather
/// than mechanically repeating the same one-line-different case across every one of its 34 color
/// properties.
/// </remarks>
public sealed class CodeViewStyleTests
{
    /// <summary>Verifies the constructor rejects a transparent color for a representative sample
    /// of color properties spanning the declaration order (first, middle, and last).</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Constructor_WhenAColorIsTransparent_ThrowsArgumentException(int sample)
    {
        var baseline = CodeViewStyle.Default;
        ControlColor transparent = Color.Transparent;

        _ = Should.Throw<ArgumentException>(() => new CodeViewStyle(
            baseline.Face,
            baseline.Border,
            baseline.Shadow,
            sample == 0 ? transparent : baseline.NormalColor,
            baseline.KeywordColor,
            baseline.FunctionColor,
            baseline.VariableColor,
            baseline.ControlFlowColor,
            baseline.OperatorColor,
            baseline.BuiltInColor,
            baseline.ExtensionColor,
            baseline.PreprocessorColor,
            baseline.AttributeColor,
            baseline.CharColor,
            baseline.SpecialCharColor,
            baseline.StringColor,
            baseline.VerbatimStringColor,
            baseline.SpecialStringColor,
            baseline.ImportColor,
            baseline.DataTypeColor,
            baseline.DecimalValueColor,
            baseline.BaseNColor,
            baseline.FloatColor,
            baseline.ConstantColor,
            baseline.CommentColor,
            baseline.DocumentationColor,
            baseline.AnnotationColor,
            baseline.CommentVariableColor,
            baseline.RegionMarkerColor,
            baseline.InformationColor,
            baseline.WarningColor,
            baseline.AlertColor,
            baseline.OthersColor,
            sample == 1 ? transparent : baseline.ErrorColor,
            baseline.SelectedTextColor,
            baseline.SelectedBackground,
            sample == 2 ? transparent : baseline.GutterColor,
            baseline.CollapsedGlyph,
            baseline.ExpandedGlyph));
    }

    /// <summary>Verifies a <c>with</c> expression rejects a transparent color too, since each
    /// color property validates in its own <c>init</c> accessor independent of the constructor.</summary>
    [Fact]
    public void With_WhenErrorColorIsTransparent_ThrowsArgumentException()
    {
        var baseline = CodeViewStyle.Default;
        ControlColor transparent = Color.Transparent;

        _ = Should.Throw<ArgumentException>(() => baseline with { ErrorColor = transparent });
    }

    /// <summary>Verifies the constructor rejects a two-cell-wide glyph for either fold indicator.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Constructor_WhenAFoldGlyphIsTwoCellsWide_ThrowsArgumentException(bool collapsed)
    {
        var baseline = CodeViewStyle.Default;
        var wide = new Rune(0x4E16);

        _ = Should.Throw<ArgumentException>(() => new CodeViewStyle(
            baseline.Face,
            baseline.Border,
            baseline.Shadow,
            baseline.NormalColor,
            baseline.KeywordColor,
            baseline.FunctionColor,
            baseline.VariableColor,
            baseline.ControlFlowColor,
            baseline.OperatorColor,
            baseline.BuiltInColor,
            baseline.ExtensionColor,
            baseline.PreprocessorColor,
            baseline.AttributeColor,
            baseline.CharColor,
            baseline.SpecialCharColor,
            baseline.StringColor,
            baseline.VerbatimStringColor,
            baseline.SpecialStringColor,
            baseline.ImportColor,
            baseline.DataTypeColor,
            baseline.DecimalValueColor,
            baseline.BaseNColor,
            baseline.FloatColor,
            baseline.ConstantColor,
            baseline.CommentColor,
            baseline.DocumentationColor,
            baseline.AnnotationColor,
            baseline.CommentVariableColor,
            baseline.RegionMarkerColor,
            baseline.InformationColor,
            baseline.WarningColor,
            baseline.AlertColor,
            baseline.OthersColor,
            baseline.ErrorColor,
            baseline.SelectedTextColor,
            baseline.SelectedBackground,
            baseline.GutterColor,
            collapsed ? wide : baseline.CollapsedGlyph,
            collapsed ? baseline.ExpandedGlyph : wide));
    }

    /// <summary>Verifies a <c>with</c> expression rejects a two-cell-wide glyph too.</summary>
    [Fact]
    public void With_WhenCollapsedGlyphIsTwoCellsWide_ThrowsArgumentException()
    {
        var baseline = CodeViewStyle.Default;
        var wide = new Rune(0x4E16);

        _ = Should.Throw<ArgumentException>(() => baseline with { CollapsedGlyph = wide });
    }
}
