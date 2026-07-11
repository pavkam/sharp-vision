using SharpVision.Terminal.Unicode;
using SharpVision.Text;

using Shouldly;

using TextLayout = SharpVision.Text.Layout;

namespace SharpVision.Tests.Text;

/// <summary>Proves seeded text formatting invariants over mixed hostile UTF-16.</summary>
public sealed class RandomizedLayoutTests
{
    private const int _caseCount = 5_000;
    private const int _seed = 0x007E_875A;

    /// <summary>Verifies every emitted slice is deterministic, bounded, and grapheme-safe.</summary>
    [Fact]
    public void Format_WhenInputsAreRandomized_PreservesGraphemeAndCellInvariants()
    {
        var random = new Random(_seed);

        for (var sample = 0; sample < _caseCount; sample++)
        {
            var content = Content(random);
            var width = random.Next(0, 21);
            var wrapping = (Wrapping) random.Next(0, 3);
            var trimming = (Trimming) random.Next(0, 4);
            var alignment = (Alignment) random.Next(0, 3);
            var ambiguous = (Ambiguous) random.Next(0, 2);
            var context = $"seed=0x{_seed:X8}, case={sample}, width={width}, " +
                $"wrapping={wrapping}, trimming={trimming}, alignment={alignment}, " +
                $"utf16={Convert.ToHexString(System.Text.Encoding.Unicode.GetBytes(content))}";
            var first = Format(content, width, wrapping, trimming, alignment, ambiguous);
            var second = Format(content, width, wrapping, trimming, alignment, ambiguous);
            var boundaries = Boundaries(content);
            var previous = 0;

            second.ShouldBe(first, context);
            first.ShouldNotBeEmpty(context);

            foreach (var line in first)
            {
                line.Offset.ShouldBeGreaterThanOrEqualTo(previous, context);
                line.Offset.ShouldBeLessThanOrEqualTo(content.Length, context);
                line.Length.ShouldBeGreaterThanOrEqualTo(0, context);
                (line.Offset + line.Length).ShouldBeLessThanOrEqualTo(content.Length, context);
                boundaries.ShouldContain(line.Offset, context);
                boundaries.ShouldContain(line.Offset + line.Length, context);
                line.Cells.ShouldBe(Cells(content.AsSpan(line.Offset, line.Length), ambiguous) +
                    (line.HasEllipsis ? 1 : 0), context);
                line.Leading.ShouldBeGreaterThanOrEqualTo(0, context);

                if (width > 0 && (wrapping != Wrapping.None || trimming != Trimming.None))
                {
                    line.Cells.ShouldBeLessThanOrEqualTo(width, context);
                }

                previous = line.Offset + line.Length;
            }
        }
    }

    private static HashSet<int> Boundaries(string content)
    {
        var result = new HashSet<int> { 0, content.Length };

        foreach (var grapheme in Graphemes.Enumerate(content))
        {
            _ = result.Add(grapheme.Offset);
            _ = result.Add(grapheme.Offset + grapheme.Length);
        }

        return result;
    }

    private static int Cells(ReadOnlySpan<char> value, Ambiguous ambiguous)
    {
        var result = 0;

        foreach (var grapheme in Graphemes.Enumerate(value))
        {
            var cluster = value.Slice(grapheme.Offset, grapheme.Length);
            result += cluster.Length == 1 && cluster[0] == '\t'
                ? 4 - (result % 4)
                : Width.Measure(cluster, ambiguous).Cells;
        }

        return result;
    }

    private static string Content(Random random)
    {
        ReadOnlySpan<string> tokens =
        [
            "a", " ", "\t", "\r", "\n", "\r\n", "e\u0301", "界", "·", "👩‍💻", "\uFE0F",
            "\uD800", "\uDC00",
        ];
        var result = new System.Text.StringBuilder();
        var count = random.Next(0, 25);

        for (var index = 0; index < count; index++)
        {
            _ = result.Append(tokens[random.Next(tokens.Length)]);
        }

        return result.ToString();
    }

    private static Line[] Format(
        string content,
        int width,
        Wrapping wrapping,
        Trimming trimming,
        Alignment alignment,
        Ambiguous ambiguous)
    {
        var required = TextLayout.Format(
            content,
            width,
            wrapping,
            trimming,
            alignment,
            ambiguous,
            []);
        var result = new Line[required];
        _ = TextLayout.Format(
            content,
            width,
            wrapping,
            trimming,
            alignment,
            ambiguous,
            result);
        return result;
    }
}
