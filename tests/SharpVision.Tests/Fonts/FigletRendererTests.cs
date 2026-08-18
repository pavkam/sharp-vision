// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Fonts;

using System.Text;

using SharpVision.Fonts;

/// <summary>Verifies FigletRenderer's horizontal smushing rules, including hardblank collisions.</summary>
public sealed class FigletRendererTests
{
    /// <summary>
    /// Verifies universal horizontal smushing (the <see cref="FigletLayout.HorizontalSmushing"/>
    /// bit set with none of the specific rule bits, exactly as three of the bundled catalog
    /// fonts - shadow.flf, smshadow.flf, and mini.flf - declare) keeps the visible glyph pixel
    /// when it collides with the other glyph's hardblank, instead of letting the hardblank win
    /// and erase the visible pixel. This matches the reference FIGlet <c>smushem</c> algorithm's
    /// hardblank special case, which the "no rule bits" branch must still honor.
    /// </summary>
    [Fact]
    public void Render_WhenUniversalSmushingCollidesWithHardBlank_KeepsTheVisiblePixel()
    {
        var font = CreateFont(
            (int) FigletLayout.HorizontalSmushing,
            new Dictionary<int, string>
            {
                ['A'] = "X",
                ['B'] = "$",
            });

        var rendered = FigletRenderer.Render(font, "AB", default);

        rendered.ShouldBe("X");
    }

    /// <summary>
    /// Verifies a single huge logical line (no line breaks) trips <see cref="FigletLimits.MaxOutputChars"/>
    /// long before the whole input would be composed. Prior to bounding <c>RenderLine</c>'s per-rune
    /// merge loop, a single logical line was fully built into unbounded <see cref="StringBuilder"/>
    /// rows with no limit check anywhere in the composition path, so the only existing check - in the
    /// trailing row-joining loop - never ran until the entire oversized line had already been
    /// allocated. This uses a repeat count far larger than the configured limit; if the fix regressed
    /// back to checking only after full composition, this test would take unreasonably long or exhaust
    /// memory instead of failing fast.
    /// </summary>
    [Fact]
    public void Render_WhenOneHugeLogicalLineExceedsTheLimit_ThrowsWithoutComposingTheWholeLine()
    {
        var font = CreateFont(
            (int) FigletLayout.None,
            [],
            new FigletLimits(maxOutputChars: 1_000));
        var text = new string('A', 5_000_000);

        _ = Should.Throw<InvalidOperationException>(() => FigletRenderer.Render(font, text, default));
    }

    /// <summary>
    /// Verifies many short newline-separated logical lines also trip
    /// <see cref="FigletLimits.MaxOutputChars"/> long before every line has been individually
    /// composed and appended. Prior to bounding <c>AppendVertical</c>, the accumulated <c>composed</c>
    /// output grew without any check across the whole outer per-line loop, so the only existing check
    /// - in the trailing row-joining loop - never ran until every one of the many lines had already
    /// been merged into the accumulated output.
    /// </summary>
    [Fact]
    public void Render_WhenManyShortLogicalLinesExceedTheLimit_ThrowsWithoutComposingEveryLine()
    {
        var font = CreateFont(
            (int) FigletLayout.None,
            [],
            new FigletLimits(maxOutputChars: 50));
        var text = string.Join('\n', Enumerable.Repeat("A", 200_000));

        _ = Should.Throw<InvalidOperationException>(() => FigletRenderer.Render(font, text, default));
    }

    /// <summary>
    /// Verifies ordinary output that fits within the configured limit - including the exact boundary,
    /// where the rendered length equals <see cref="FigletLimits.MaxOutputChars"/> rather than falling
    /// under it - still renders correctly and identically to rendering with a generous limit. This
    /// guards against the incremental per-rune and per-line checks added to bound memory usage being
    /// off-by-one or otherwise over-eager for normal-sized input.
    /// </summary>
    [Fact]
    public void Render_WhenOutputFitsExactlyWithinTheLimit_RendersUnchanged()
    {
        var constrainedFont = CreateFont(
            (int) FigletLayout.None,
            [],
            new FigletLimits(maxOutputChars: 2));
        var generousFont = CreateFont((int) FigletLayout.None, [], FigletLimits.Default);

        var constrained = FigletRenderer.Render(constrainedFont, "AB", default);
        var generous = FigletRenderer.Render(generousFont, "AB", default);

        constrained.ShouldBe("AB");
        constrained.ShouldBe(generous);
    }

    /// <summary>
    /// Verifies a glyph's leading blank margin - ordinary in real bundled fonts, where many
    /// glyph rows carry a left-hand column or two of padding - is not counted against
    /// <see cref="FigletLimits.MaxOutputChars"/> during composition, even though <c>RenderLine</c>
    /// checks the limit before <c>TrimLeading</c> ever removes that margin. A naive incremental
    /// check against the raw, untrimmed row length would throw here despite the final trimmed
    /// output ("AB") landing exactly at the configured limit, rejecting legitimate input purely
    /// because of columns the renderer always discards before returning.
    /// </summary>
    [Fact]
    public void Render_WhenGlyphsCarryALeadingBlankMargin_DoesNotCountItAgainstTheLimit()
    {
        var font = CreateFont(
            (int) FigletLayout.None,
            new Dictionary<int, string>
            {
                ['A'] = " A",
                ['B'] = "B",
            },
            new FigletLimits(maxOutputChars: 2));

        var rendered = FigletRenderer.Render(font, "AB", default);

        rendered.ShouldBe("AB");
    }

    private static FigletFont CreateFont(
        int fullLayout,
        Dictionary<int, string> overrides,
        FigletLimits? limits = null)
    {
        var builder = new StringBuilder($"flf2a$ 1 1 80 -1 0 0 {fullLayout}\n");

        for (var code = 32; code <= 126; code++)
        {
            var content = overrides.TryGetValue(code, out var value) ? value : char.ConvertFromUtf32(code);
            _ = builder.Append(content).Append("@@\n");
        }

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(builder.ToString()));
        return FigletFont.Load(stream, "test", limits ?? FigletLimits.Default);
    }
}
