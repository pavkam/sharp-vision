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
    [Fact]
    public void Mutate_WhenOperationsAreRandomized_PreservesOwnership()
    {
        Random random = new(_seed);
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
            (Source: "🏽", Presentation: "�"),
        ];
        using Frame frame = new(new Size(20, 5));

        for (int operation = 0; operation < 1_000; operation++)
        {
            Point point = new(random.Next(frame.Size.Width), random.Next(frame.Size.Height));

            if (random.Next(4) == 0)
            {
                frame.Canvas.Clear(new Rect(point.X, point.Y, 1, 1));
            }
            else
            {
                (string? source, string? presentation) = values[random.Next(values.Length)];
                Edge edge = (Edge) random.Next(3);
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
        for (int y = 0; y < frame.Size.Height; y++)
        {
            for (int x = 0; x < frame.Size.Width; x++)
            {
                Point point = new(x, y);
                CellInfo cell = frame.GetCell(point);
                string message = $"Seed {_seed}, operation {operation}, cell ({x},{y}).";

                if (cell.IsContinuation)
                {
                    cell.Lead.Y.ShouldBe(y, message);
                    cell.Lead.X.ShouldBe(x - 1, message);
                    CellInfo lead = frame.GetCell(cell.Lead);
                    lead.IsContinuation.ShouldBeFalse(message);
                    lead.Width.ShouldBe(2, message);
                }
                else if (cell.Width == 2)
                {
                    x.ShouldBeLessThan(frame.Size.Width - 1, message);
                    CellInfo continuation = frame.GetCell(new Point(x + 1, y));
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
