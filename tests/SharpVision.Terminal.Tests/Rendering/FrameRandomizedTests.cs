using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Rendering;

using Shouldly;

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
        var random = new Random(_seed);
        var values = new[] { "a", "界", "e\u0301", "👩‍💻", "🇵🇹" };
        using var frame = new Frame(new Size(20, 5));

        for (var operation = 0; operation < 1_000; operation++)
        {
            var point = new Point(random.Next(frame.Size.Width), random.Next(frame.Size.Height));

            if (random.Next(4) == 0)
            {
                frame.Canvas.Clear(new Rect(point.X, point.Y, 1, 1));
            }
            else
            {
                var value = values[random.Next(values.Length)];
                var edge = (Edge) random.Next(3);
                _ = frame.Canvas.Draw(value.AsSpan(), point, edge: edge);
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
