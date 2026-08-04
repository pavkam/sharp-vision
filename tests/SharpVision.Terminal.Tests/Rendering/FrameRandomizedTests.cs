// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Rendering;

/// <summary>
/// Verifies seeded frame mutations preserve wide-cell ownership invariants.
/// </summary>
public sealed class FrameRandomizedTests
{
    private const int _seed = 0xC311;

    /// <summary>
    /// Verifies random draw and clear operations never orphan a continuation.
    /// </summary>
    /// <remarks>
    /// This test was previously skipped on .NET 10 x64 Linux because the process died with an
    /// AccessViolationException blamed on <c>Frame.get_Size()</c>, a plain auto-property that
    /// cannot fault. That crash is not a frame or pooling defect: it is the code-coverage
    /// instrumentation probe store documented in #32, which is attributed to whichever managed
    /// method happens to be executing. The invariant itself is platform-independent, so the
    /// quarantine is removed and every platform executes it.
    /// </remarks>
    [Fact]
    public void Mutate_WhenOperationsAreRandomized_PreservesOwnership()
    {
        var random = new Random(_seed);
        (string Source, string Presentation)[] values =
        [
            (Source: "a", Presentation: "a"),
            (Source: "界", Presentation: "界"),
            (Source: "e\u0301", Presentation: "e\u0301"),
            (Source: "👩‍💻", Presentation: "👩‍💻"),
            (Source: "🇵🇹", Presentation: "🇵🇹"),
            (Source: "\u0301", Presentation: "�"),
            (Source: "\u200d", Presentation: "�"),
            (Source: "\ufe0f", Presentation: "�"),
            (Source: "🏽", Presentation: "�")
        ];
        using Frame frame = new(new Size(20, 5));

        for (var operation = 0; operation < 1_000; operation++)
        {
            var point = new Point(random.Next(frame.Size.Width), random.Next(frame.Size.Height));

            if (random.Next(4) == 0)
            {
                frame.Canvas.Clear(new Rect(point.X, point.Y, 1, 1));
            }
            else
            {
                var (source, presentation) = values[random.Next(values.Length)];
                var edge = (Edge) random.Next(3);
                _ = frame.Canvas.Draw(source.AsSpan(), point, edge: edge);

                if (presentation == "�")
                {
                    FrameTests.GetText(frame, point).ShouldBe(
                        presentation,
                        $"Seed {_seed}, operation {operation}, orphan presentation.");
                }
            }

            AssertOwnership(frame, operation);
        }
    }

    private static void AssertOwnership(Frame frame, int operation)
    {
        for (var y = 0; y < frame.Size.Height; y++)
        {
            for (var x = 0; x < frame.Size.Width; x++)
            {
                var point = new Point(x, y);
                var cell = frame.GetCell(point);
                var message = $"Seed {_seed}, operation {operation}, cell ({x},{y}).";

                if (cell.IsContinuation)
                {
                    cell.Lead.Y.ShouldBe(y, message);
                    cell.Lead.X.ShouldBe(x - 1, message);
                    var lead = frame.GetCell(cell.Lead);
                    lead.IsContinuation.ShouldBeFalse(message);
                    lead.Width.ShouldBe(2, message);
                }
                else if (cell.Width == 2)
                {
                    x.ShouldBeLessThan(frame.Size.Width - 1, message);
                    var continuation = frame.GetCell(new Point(x + 1, y));
                    continuation.IsContinuation.ShouldBeTrue(message);
                    continuation.Lead.ShouldBe(point, message);
                }
                else
                {
                    cell.Width.ShouldBe(1, message);
                }
            }
        }
    }
}
