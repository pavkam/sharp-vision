// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Fonts;

using System.Text;

using SharpVision.Fonts;

using Shouldly;

/// <summary>Verifies bounded FIGfont parsing and deterministic text rendering.</summary>
public sealed class FigletFontTests
{
    #region Parsing

    /// <summary>Verifies a complete FIGfont publishes validated header metadata.</summary>
    [Fact]
    public void Load_WhenFontIsValid_ExposesHeaderAndGlyphs()
    {
        using MemoryStream stream = Stream(CreateFont());

        FigletFont font = FigletFont.Load(stream, "test");

        font.Name.ShouldBe("test");
        font.Height.ShouldBe(1);
        font.Baseline.ShouldBe(1);
        font.Direction.ShouldBe(FigletDirection.LeftToRight);
        font.Render("AB").ShouldBe("AB");
    }

    /// <summary>Verifies tagged Unicode scalar glyphs are parsed after standard glyphs.</summary>
    [Fact]
    public void Load_WhenFontHasCodeTag_RendersUnicodeScalar()
    {
        using MemoryStream stream = Stream($"{CreateFont()}9731 snowman\n☃@@\n");

        FigletFont font = FigletFont.Load(stream, "unicode");

        font.Render("☃").ShouldBe("☃");
    }

    /// <summary>Verifies malformed input fails without publishing a partial font.</summary>
    [Fact]
    public void Load_WhenSignatureIsInvalid_ThrowsFormatException()
    {
        using MemoryStream stream = Stream("not-a-font\n");

        _ = Should.Throw<FormatException>(() => FigletFont.Load(stream, "bad"));
    }

    /// <summary>Verifies configured byte limits are enforced before parsing.</summary>
    [Fact]
    public void Load_WhenInputExceedsLimit_ThrowsInvalidDataException()
    {
        using MemoryStream stream = Stream(CreateFont());
        FigletLimits limits = new FigletLimits(maxInputBytes: 16);

        _ = Should.Throw<InvalidDataException>(() => FigletFont.Load(stream, "large", limits));
    }

    #endregion

    #region Rendering

    /// <summary>Verifies an explicit right-to-left override reverses scalar order.</summary>
    [Fact]
    public void Render_WhenDirectionIsRightToLeft_ReversesGlyphOrder()
    {
        using MemoryStream stream = Stream(CreateFont());
        FigletFont font = FigletFont.Load(stream, "test");
        FigletOptions options = new FigletOptions(FigletDirection.RightToLeft);

        font.Render("ABC", options).ShouldBe("CBA");
    }

    /// <summary>Verifies missing scalars use the font's question-mark glyph.</summary>
    [Fact]
    public void Render_WhenGlyphIsMissing_UsesQuestionMarkFallback()
    {
        using MemoryStream stream = Stream(CreateFont());
        FigletFont font = FigletFont.Load(stream, "test");

        font.Render("😀").ShouldBe("?");
    }

    /// <summary>Verifies hardblanks become visible spaces only after composition.</summary>
    [Fact]
    public void Render_WhenGlyphContainsHardblank_ReplacesItAfterComposition()
    {
        using MemoryStream stream = Stream(CreateFont(code => code == 'A' ? "$A" : RuneFor(code)));
        FigletFont font = FigletFont.Load(stream, "hardblank");

        font.Render("A").ShouldBe(" A");
    }

    /// <summary>Verifies full-layout vertical fitting moves complete rendered lines together.</summary>
    [Fact]
    public void Render_WhenVerticalFittingIsEnabled_OverlapsBlankBoundaryRows()
    {
        using MemoryStream stream = Stream(CreateTallFont());
        FigletFont font = FigletFont.Load(stream, "vertical");

        font.Render("A\nA").ShouldBe("A\nA\n ");
    }

    /// <summary>Verifies Big-X smushing distinguishes rising and falling slash pairs.</summary>
    [Theory]
    [InlineData("AB", "|")]
    [InlineData("BA", "Y")]
    public void Render_WhenBigXRuleCombinesSlashes_UsesSpecifiedGlyph(
        string content,
        string expected)
    {
        using MemoryStream stream = Stream(CreateFont(code => code switch
        {
            'A' => "/",
            'B' => "\\",
            _ => RuneFor(code),
        }));
        FigletFont font = FigletFont.Load(stream, "big-x");
        FigletOptions options = new FigletOptions(
            layout: FigletLayout.HorizontalSmushing | FigletLayout.BigX);

        font.Render(content, options).ShouldBe(expected);
    }

    #endregion

    private static string CreateFont(Func<int, string>? glyph = null)
    {
        glyph ??= RuneFor;
        StringBuilder builder = new StringBuilder("flf2a$ 1 1 80 -1 1 0\nTest font by SharpVision\n");

        for (var code = 32; code <= 126; code++)
        {
            _ = builder.Append(glyph(code)).Append("@@\n");
        }

        foreach (var code in new[] { 196, 214, 220, 228, 246, 252, 223 })
        {
            _ = builder.Append(glyph(code)).Append("@@\n");
        }

        return builder.ToString();
    }

    private static string RuneFor(int code) => char.ConvertFromUtf32(code);

    private static string CreateTallFont()
    {
        StringBuilder builder = new StringBuilder(
            "flf2a$ 2 2 80 -1 1 0 8192\nTest font by SharpVision\n");

        for (var code = 32; code <= 126; code++)
        {
            _ = builder.Append(code == 'A' ? "A@\n" : " @\n").Append(" @@\n");
        }

        for (var index = 0; index < 7; index++)
        {
            _ = builder.Append(" @\n @@\n");
        }

        return builder.ToString();
    }

    private static MemoryStream Stream(string value) =>
        new(Encoding.UTF8.GetBytes(value), writable: false);
}
