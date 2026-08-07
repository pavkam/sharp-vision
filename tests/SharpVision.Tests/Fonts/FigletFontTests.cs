// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Fonts;

/// <summary>Verifies bounded FIGfont parsing and deterministic text rendering.</summary>
public sealed class FigletFontTests
{
    #region Parsing

    /// <summary>Verifies a complete FIGfont publishes validated header metadata.</summary>
    [Fact]
    public void Load_WhenFontIsValid_ExposesHeaderAndGlyphs()
    {
        using var stream = Stream(CreateFont());

        var font = FigletFont.Load(stream, "test");

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
        using var stream = Stream($"{CreateFont()}9731 snowman\n☃@@\n");

        var font = FigletFont.Load(stream, "unicode");

        font.Render("☃").ShouldBe("☃");
    }

    /// <summary>Verifies malformed input fails without publishing a partial font.</summary>
    [Fact]
    public void Load_WhenSignatureIsInvalid_ThrowsFormatException()
    {
        using var stream = Stream("not-a-font\n");

        _ = Should.Throw<FormatException>(() => FigletFont.Load(stream, "bad"));
    }

    /// <summary>Verifies an unknown numeric direction falls back to left-to-right instead of
    /// rejecting an otherwise complete font.</summary>
    [Theory]
    [InlineData("2")]
    [InlineData("-1")]
    public void Load_WhenDirectionIsUnknownNumericValue_FallsBackToLeftToRight(string direction)
    {
        using var stream = Stream(CreateFont(directionField: direction));

        var font = FigletFont.Load(stream, "test");

        font.Direction.ShouldBe(FigletDirection.LeftToRight);
    }

    /// <summary>Verifies configured byte limits are enforced before parsing.</summary>
    [Fact]
    public void Load_WhenInputExceedsLimit_ThrowsInvalidDataException()
    {
        using var stream = Stream(CreateFont());
        var limits = new FigletLimits(maxInputBytes: 16);

        _ = Should.Throw<InvalidDataException>(() => FigletFont.Load(stream, "large", limits));
    }

    /// <summary>Verifies an understated legacy header remains compatible when the encoded row is
    /// still inside the caller's hard safety limit.</summary>
    [Fact]
    public void Load_WhenGlyphRowExceedsDeclaredMaxLength_LoadsWithinConfiguredLimit()
    {
        using var stream = Stream(CreateFont(code => code == 'A' ? new string('#', 100) : RuneFor(code)));

        var font = FigletFont.Load(stream, "understated-max");

        font.Render("A").ShouldBe(new string('#', 100));
    }

    /// <summary>Verifies encoded rows remain bounded by the caller's configured hard limit.</summary>
    [Fact]
    public void Load_WhenGlyphRowExceedsConfiguredLimit_ThrowsFormatException()
    {
        using var stream = Stream(CreateFont(code => code == 'A' ? new string('#', 100) : RuneFor(code)));
        var limits = new FigletLimits(maxRowWidth: 101);

        _ = Should.Throw<FormatException>(() => FigletFont.Load(stream, "over-configured-max", limits));
    }

    #endregion

    #region Rendering

    /// <summary>Verifies an explicit right-to-left override reverses scalar order.</summary>
    [Fact]
    public void Render_WhenDirectionIsRightToLeft_ReversesGlyphOrder()
    {
        using var stream = Stream(CreateFont());
        var font = FigletFont.Load(stream, "test");
        var options = new FigletOptions(FigletDirection.RightToLeft);

        font.Render("ABC", options).ShouldBe("CBA");
    }

    /// <summary>Verifies missing scalars use the font's question-mark glyph.</summary>
    [Fact]
    public void Render_WhenGlyphIsMissing_UsesQuestionMarkFallback()
    {
        using var stream = Stream(CreateFont());
        var font = FigletFont.Load(stream, "test");

        font.Render("😀").ShouldBe("?");
    }

    /// <summary>Verifies hardblanks become visible spaces only after composition.</summary>
    [Fact]
    public void Render_WhenGlyphContainsHardblank_ReplacesItAfterComposition()
    {
        using var stream = Stream(CreateFont(code => code == 'A' ? "$A" : RuneFor(code)));
        var font = FigletFont.Load(stream, "hardblank");

        font.Render("A").ShouldBe(" A");
    }

    /// <summary>Verifies an explicit <c>full_layout</c> value of zero publishes full width instead
    /// of being reinterpreted as horizontal fitting the way an <c>old_layout</c> of zero is (see
    /// the FIGfont v2 spec's distinct treatment of the two header fields).</summary>
    [Fact]
    public void Load_WhenFullLayoutIsZero_PublishesNoneInsteadOfHorizontalFitting()
    {
        using var stream = Stream(CreateFont(fullLayoutField: "0"));

        var font = FigletFont.Load(stream, "full-layout-zero");

        font.Layout.ShouldBe(FigletLayout.None);
    }

    /// <summary>Verifies a <c>full_layout</c> value in the legacy smush-rule-sum range is used as a
    /// literal bit mask and does not automatically gain <see cref="FigletLayout.HorizontalSmushing"/>
    /// the way the equivalent <c>old_layout</c> value would.</summary>
    [Fact]
    public void Load_WhenFullLayoutIsLowValue_DoesNotInjectHorizontalSmushing()
    {
        using var stream = Stream(CreateFont(fullLayoutField: "1"));

        var font = FigletFont.Load(stream, "full-layout-equal");

        font.Layout.ShouldBe(FigletLayout.Equal);
    }

    /// <summary>Verifies full-layout vertical fitting moves complete rendered lines together.</summary>
    [Fact]
    public void Render_WhenVerticalFittingIsEnabled_OverlapsBlankBoundaryRows()
    {
        using var stream = Stream(CreateTallFont());
        var font = FigletFont.Load(stream, "vertical");

        font.Render("A\nA").ShouldBe("A\nA\n ");
    }

    /// <summary>Verifies full horizontal smushing across every printable pair never leaks the
    /// internal NUL sentinel FigletRenderer.Merge uses to mean "no smush rule matched" into the
    /// rendered text - GetOverlap's per-row-minimum overlap must never expose an un-smushable
    /// column pair to Merge, matching the invariant MergeVertical already asserts explicitly.</summary>
    [Fact]
    public void Render_WhenFullSmushingCombinesEveryPrintablePair_NeverEmitsNulSentinel()
    {
        using var stream = Stream(CreateFont(code => "##"));
        var font = FigletFont.Load(stream, "full-smush");
        var options = new FigletOptions(layout: (FigletLayout) 63 | FigletLayout.HorizontalSmushing);

        var text = new StringBuilder();

        for (var first = 33; first <= 126; first++)
        {
            _ = text.Append(font.Render($"{(char) first}{(char) first}", options));
        }

        text.ToString().ShouldNotContain('\0');
    }

    /// <summary>Verifies Big-X smushing distinguishes rising and falling slash pairs.</summary>
    [Theory]
    [InlineData("AB", "|")]
    [InlineData("BA", "Y")]
    public void Render_WhenBigXRuleCombinesSlashes_UsesSpecifiedGlyph(
        string content,
        string expected)
    {
        using var stream = Stream(CreateFont(code => code switch
        {
            'A' => "/",
            'B' => "\\",
            _ => RuneFor(code)
        }));
        var font = FigletFont.Load(stream, "big-x");
        var options = new FigletOptions(
            layout: FigletLayout.HorizontalSmushing | FigletLayout.BigX);

        font.Render(content, options).ShouldBe(expected);
    }

    #endregion

    private static string CreateFont(
        Func<int, string>? glyph = null,
        string directionField = "0",
        string? fullLayoutField = null)
    {
        glyph ??= RuneFor;
        var header = fullLayoutField is null
            ? $"flf2a$ 1 1 80 -1 1 {directionField}"
            : $"flf2a$ 1 1 80 -1 1 {directionField} {fullLayoutField}";
        var builder = new StringBuilder($"{header}\nTest font by SharpVision\n");

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
        var builder = new StringBuilder(
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
