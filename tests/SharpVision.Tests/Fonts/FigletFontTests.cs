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

    /// <summary>Verifies the 95 mandatory glyphs alone are checked against the configured glyph
    /// count limit, instead of only the optional trailing extension/German-block content: a
    /// minimal font that never reaches any of the later limit-aware loops must still be rejected
    /// when even its mandatory block alone exceeds the configured limit.</summary>
    [Fact]
    public void Load_WhenMandatoryGlyphsAloneExceedGlyphLimit_ThrowsInvalidDataException()
    {
        using var stream = Stream(CreateFontWithoutGermanBlock());
        var limits = new FigletLimits(maxGlyphs: 1);

        _ = Should.Throw<InvalidDataException>(() => FigletFont.Load(stream, "too-few-glyphs", limits));
    }

    /// <summary>Verifies a minimal font carrying exactly the 95 mandatory glyphs still loads
    /// successfully when the configured glyph count limit is high enough to admit them - the
    /// mandatory-block limit check must not reject a font it should accept.</summary>
    [Fact]
    public void Load_WhenMandatoryGlyphsFitWithinGlyphLimit_LoadsSuccessfully()
    {
        using var stream = Stream(CreateFontWithoutGermanBlock());
        var limits = new FigletLimits(maxGlyphs: 95);

        var font = FigletFont.Load(stream, "exactly-enough-glyphs", limits);

        font.Render("A").ShouldBe("A");
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

    /// <summary>Verifies a font that omits the optional legacy German-umlaut block, but still has
    /// enough trailing code-tagged extension glyphs to reach the block's remaining-line-count
    /// heuristic, parses those extension glyphs correctly instead of having its cursor
    /// desynchronized by their tag/data lines being mistaken for untagged German rows.</summary>
    [Fact]
    public void Load_WhenGermanBlockIsAbsentButExtensionGlyphsReachThreshold_ParsesExtensionGlyphsCorrectly()
    {
        var builder = new StringBuilder(CreateFontWithoutGermanBlock());

        for (var code = 9732; code <= 9735; code++)
        {
            _ = builder.Append(code).Append(" extra\n").Append(RuneFor(code)).Append("@@\n");
        }

        using var stream = Stream(builder.ToString());

        var font = FigletFont.Load(stream, "no-german-block");

        for (var code = 9732; code <= 9735; code++)
        {
            font.Render(RuneFor(code)).ShouldBe(RuneFor(code));
        }

        foreach (var code in new[] { 196, 214, 220, 228, 246, 252, 223 })
        {
            font.Render(RuneFor(code)).ShouldBe("?");
        }
    }

    /// <summary>Verifies the same absent-German-block scenario at the exact edge of the
    /// remaining-line-count heuristic - trailing extension content totaling precisely, not merely
    /// at least, the block's threshold - still yields to the extension-glyph loop instead of
    /// misreading the line past the end of that content as a phantom final German row.</summary>
    [Fact]
    public void Load_WhenExtensionContentExactlyMeetsGermanBlockThreshold_StillParsesExtensionGlyphs()
    {
        var builder = new StringBuilder(CreateFontWithoutGermanBlock());

        for (var code = 9732; code <= 9734; code++)
        {
            _ = builder.Append(code).Append(" extra\n").Append(RuneFor(code)).Append("@@\n");
        }

        using var stream = Stream(builder.ToString());

        var font = FigletFont.Load(stream, "boundary-exact");

        for (var code = 9732; code <= 9734; code++)
        {
            font.Render(RuneFor(code)).ShouldBe(RuneFor(code));
        }

        foreach (var code in new[] { 196, 214, 220, 228, 246, 252, 223 })
        {
            font.Render(RuneFor(code)).ShouldBe("?");
        }
    }

    /// <summary>Verifies the adversarial regression case where a font has no German-umlaut block but
    /// its trailing code-tagged extension glyphs total exactly the block's remaining-line-count
    /// threshold (height 6, 6 real extension glyphs each spanning a tag line plus 6 data rows = 7
    /// lines, totaling 42 = 7 x height) immediately followed by a 7th extension glyph. Under the
    /// previous tentative-read-plus-one-line-boundary-peek approach, the peek saw the 7th glyph's
    /// own tag line right after the swallowed 42-line candidate and wrongly concluded it was a
    /// genuine German block, silently corrupting the first six extension glyphs. Parsing the
    /// remainder as extension glyphs first must instead recognize the whole remainder for what it
    /// is and publish all seven glyphs with their real, uncorrupted content.</summary>
    [Fact]
    public void Load_WhenNoGermanBlockButExtensionGlyphsAlignExactlyWithTheThreshold_ParsesAllExtensionGlyphsCorrectly()
    {
        const int height = 6;
        var builder = new StringBuilder(CreateFontWithoutGermanBlock(height));

        for (var code = 9732; code <= 9738; code++)
        {
            _ = builder.Append(code).Append(" extra\n");
            _ = AppendGlyphRows(builder, RuneFor(code), height);
        }

        using var stream = Stream(builder.ToString());

        var font = FigletFont.Load(stream, "no-german-block-exact-threshold");

        for (var code = 9732; code <= 9738; code++)
        {
            var expected = string.Join('\n', Enumerable.Repeat(RuneFor(code), height));
            font.Render(RuneFor(code)).ShouldBe(expected);
        }

        var expectedFallback = string.Join('\n', Enumerable.Repeat("?", height));

        foreach (var code in new[] { 196, 214, 220, 228, 246, 252, 223 })
        {
            font.Render(RuneFor(code)).ShouldBe(expectedFallback);
        }
    }

    /// <summary>Verifies a trailing endmark run longer than the standard one-character
    /// (non-final row) or two-character (final row) convention is stripped in its entirety, per
    /// the FIGfont spec's "sequence of ... identical characters" rule, instead of only the
    /// conventional count being removed and leaving stray endmark characters embedded in the
    /// parsed glyph.</summary>
    [Fact]
    public void Load_WhenGlyphRowHasLongerThanConventionalEndmarkRun_StripsEntireRun()
    {
        const int height = 2;
        var builder = new StringBuilder(CreateFontWithoutGermanBlock(height));

        // A hand-authored glyph whose rows carry a longer-than-conventional run of the endmark
        // character: 3 on the non-final row (instead of the usual 1) and 4 on the final row
        // (instead of the usual 2).
        _ = builder.Append("9732 extra\n").Append("X@@@\n").Append("X@@@@\n");

        using var stream = Stream(builder.ToString());

        var font = FigletFont.Load(stream, "long-endmark-run");

        font.Render(RuneFor(9732)).ShouldBe("X\nX");
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

    /// <summary>Builds a minimal height-1 font's header, comment, and 95 required glyphs only - no
    /// optional legacy German-umlaut block - so callers can append their own trailing content and
    /// control exactly how many lines follow the required glyphs.</summary>
    private static string CreateFontWithoutGermanBlock()
    {
        var builder = new StringBuilder("flf2a$ 1 1 80 -1 1 0\nTest font by SharpVision\n");

        for (var code = 32; code <= 126; code++)
        {
            _ = builder.Append(RuneFor(code)).Append("@@\n");
        }

        return builder.ToString();
    }

    /// <summary>Builds a minimal <paramref name="height"/>-row font's header, comment, and 95
    /// required glyphs only - no optional legacy German-umlaut block - mirroring
    /// <see cref="CreateFontWithoutGermanBlock()"/> but for a caller-specified height, so
    /// multi-row extension-glyph regression scenarios can be constructed.</summary>
    private static string CreateFontWithoutGermanBlock(int height)
    {
        var builder = new StringBuilder($"flf2a$ {height} {height} 80 -1 1 0\nTest font by SharpVision\n");

        for (var code = 32; code <= 126; code++)
        {
            _ = AppendGlyphRows(builder, RuneFor(code), height);
        }

        return builder.ToString();
    }

    /// <summary>Appends one glyph's <paramref name="height"/> encoded data rows, each repeating
    /// <paramref name="content"/> and terminated with the FIG-font endmark convention: a single
    /// mark on every row but the last, and a doubled mark on the last row to signal the glyph's
    /// end.</summary>
    private static StringBuilder AppendGlyphRows(StringBuilder builder, string content, int height)
    {
        for (var row = 0; row < height; row++)
        {
            _ = builder.Append(content).Append(row == height - 1 ? "@@\n" : "@\n");
        }

        return builder;
    }

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
