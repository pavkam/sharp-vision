// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Performance;

/// <summary>Gates vertical-merge cost during multi-line FIGlet rendering.</summary>
[Collection(PerformanceGroup.Name)]
public sealed class FigletRendererPerformanceTests
{
    /// <summary>Verifies many vertically merged lines render in time proportional to their count
    /// rather than its square — the vertical-merge overlap scan previously rescanned the entire
    /// accumulated output on every line, making K lines cost O(K^2) instead of O(K) (see #127).</summary>
    [Fact]
    public void Render_WhenManyLinesUseVerticalFitting_ScalesLinearlyWithLineCount()
    {
        using var stream = Stream(CreateOneRowFont());
        var font = FigletFont.Load(stream, "one-row");
        var options = new FigletOptions(layout: FigletLayout.VerticalFitting);

        // A single sample on a shared CI runner is noisy enough to false-fail even a correct
        // linear implementation; taking the minimum of several trials for each size keeps a
        // true O(K^2) regression clearly visible (it still grows ~256x at best case) while
        // filtering out scheduler-jitter outliers that a single measurement cannot distinguish
        // from a real regression.
        var small = MinimumElapsed(font, options, lineCount: 500, samples: 5);
        var large = MinimumElapsed(font, options, lineCount: 8_000, samples: 5);

        // A quadratic scan grows by ~256x (16^2) for a 16x larger line count; a linear scan
        // grows by ~16x. A 40x budget comfortably clears linear noise while still rejecting
        // the O(K^2) shape well before it reaches 256x.
        large.TotalMilliseconds.ShouldBeLessThan((small.TotalMilliseconds * 40) + 20);
    }

    private static TimeSpan MinimumElapsed(FigletFont font, FigletOptions options, int lineCount, int samples)
    {
        var minimum = TimeSpan.MaxValue;

        for (var sample = 0; sample < samples; sample++)
        {
            var elapsed = Elapsed(font, options, lineCount);

            if (elapsed < minimum)
            {
                minimum = elapsed;
            }
        }

        return minimum;
    }

    private static TimeSpan Elapsed(FigletFont font, FigletOptions options, int lineCount)
    {
        var text = string.Join('\n', Enumerable.Repeat("A", lineCount));
        _ = font.Render(text, options);
        var watch = Stopwatch.StartNew();
        _ = font.Render(text, options);
        watch.Stop();
        return watch.Elapsed;
    }

    private static string CreateOneRowFont()
    {
        var builder = new StringBuilder("flf2a$ 1 1 80 -1 1 0\nOne-row font by SharpVision\n");

        for (var code = 32; code <= 126; code++)
        {
            _ = builder.Append(char.ConvertFromUtf32(code)).Append("@@\n");
        }

        foreach (var code in new[] { 196, 214, 220, 228, 246, 252, 223 })
        {
            _ = builder.Append(char.ConvertFromUtf32(code)).Append("@@\n");
        }

        return builder.ToString();
    }

    private static MemoryStream Stream(string content) =>
        new(Encoding.UTF8.GetBytes(content), writable: false);
}
