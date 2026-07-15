// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Text;

using SharpVision.Text;

/// <summary>Verifies deterministic markup invariants over fixed-seed hostile input.</summary>
public sealed class RandomizedMarkupTests
{
    /// <summary>Verifies escaped randomized text parses back to the exact original source.</summary>
    [Fact]
    public void Escape_WhenRandomTextIsParsed_RoundTripsExactly()
    {
        var random = new Random(20260715);
        const string alphabet = "abc<>\\= /\r\n\u0301界";

        for (var trial = 0; trial < 2000; trial++)
        {
            var builder = new StringBuilder();
            var length = random.Next(0, 80);

            for (var index = 0; index < length; index++)
            {
                _ = builder.Append(alphabet[random.Next(alphabet.Length)]);
            }

            var original = builder.ToString();
            _ = Markup.Parse(Markup.Escape(original), out var display);

            display.ShouldBe(original, $"seed 20260715 trial {trial}");
        }
    }

    /// <summary>Verifies arbitrary fragment combinations produce contiguous, non-overlapping spans.</summary>
    [Fact]
    public void Parse_WhenRandomFragmentsAreCombined_TilesDisplayContiguously()
    {
        var random = new Random(20260716);
        string[] fragments =
        [
            "<b>", "</b>", "<red>", "</red>", "<u>", "<u=curly>", "</>",
            "<fg=#0f0>", "<bg=2>", "x", "<link=https://example.test>", "</link>",
            "\\<", "\\\\", "<unknown>", "<unknown <b>", "<link=bad\u0007>", "=", "界",
        ];

        for (var trial = 0; trial < 2000; trial++)
        {
            var builder = new StringBuilder();
            var length = random.Next(0, 40);

            for (var index = 0; index < length; index++)
            {
                _ = builder.Append(fragments[random.Next(fragments.Length)]);
            }

            var spans = Markup.Parse(builder.ToString(), out var display);
            var cursor = 0;

            foreach (var span in spans)
            {
                span.Offset.ShouldBe(cursor, $"seed 20260716 trial {trial}");
                span.Length.ShouldBeGreaterThan(0, $"seed 20260716 trial {trial}");
                cursor += span.Length;
            }

            cursor.ShouldBe(display.Length, $"seed 20260716 trial {trial}");
        }
    }
}
