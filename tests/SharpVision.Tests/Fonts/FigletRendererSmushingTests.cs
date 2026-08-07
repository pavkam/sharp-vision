// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Fonts;

using System.Text;

using SharpVision.Fonts;

/// <summary>Verifies FigletRenderer's horizontal smushing rules, including hardblank collisions.</summary>
public sealed class FigletRendererSmushingTests
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

    private static FigletFont CreateFont(int fullLayout, Dictionary<int, string> overrides)
    {
        var builder = new StringBuilder($"flf2a$ 1 1 80 -1 0 0 {fullLayout}\n");

        for (var code = 32; code <= 126; code++)
        {
            var content = overrides.TryGetValue(code, out var value) ? value : char.ConvertFromUtf32(code);
            _ = builder.Append(content).Append("@@\n");
        }

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(builder.ToString()));
        return FigletFont.Load(stream, "test");
    }
}
