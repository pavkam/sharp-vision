// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Verifies the immutable CheckMark layout and glyph family, including the Square preset
/// that no consuming control (Brackets and Tick are already proven through CheckBox and TreeView)
/// otherwise exercises.</summary>
public sealed class CheckMarkTests
{
    /// <summary>Verifies the Square preset resolves the documented one-cell layout and default
    /// glyph family.</summary>
    [Fact]
    public void Square_ResolvesOneCellLayoutWithDefaultGlyphs()
    {
        CheckMark.Square.MarkStyle.ShouldBe(CheckBoxMarkStyle.Square);
        CheckMark.Square.Glyphs.ShouldBe(CheckBoxGlyphs.Default);
        CheckMark.Square.Width.ShouldBe(1);
    }

    /// <summary>Verifies the constructor rejects an undefined mark style.</summary>
    [Fact]
    public void Constructor_WhenMarkStyleIsUndefined_Throws() =>
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            _ = new CheckMark((CheckBoxMarkStyle) 99, CheckBoxGlyphs.Default));

    /// <summary>Verifies the constructor round-trips a validated layout and glyph family.</summary>
    [Fact]
    public void Constructor_WhenValid_RoundTripsMarkStyleAndGlyphs()
    {
        var glyphs = new CheckBoxGlyphs(new Rune('-'), new Rune('x'), new Rune('~'));

        var mark = new CheckMark(CheckBoxMarkStyle.Tick, glyphs);

        mark.MarkStyle.ShouldBe(CheckBoxMarkStyle.Tick);
        mark.Glyphs.ShouldBe(glyphs);
        mark.Width.ShouldBe(1);
    }
}
