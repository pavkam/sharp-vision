using System.Globalization;
using System.Text;

using SharpVision.Terminal.Unicode;

using Shouldly;

namespace SharpVision.Terminal.Tests.Unicode;

/// <summary>
/// Verifies every official Unicode 17 extended-grapheme conformance case.
/// </summary>
public sealed class ConformanceTests
{
    /// <summary>
    /// Verifies segmentation matches every boundary in GraphemeBreakTest.
    /// </summary>
    [Fact]
    public void Enumerate_WhenUnicodeConformanceCasesAreRead_MatchesEveryBoundary()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Unicode",
            "Data",
            "GraphemeBreakTest-17.0.0.txt");
        var lineNumber = 0;

        foreach (var rawLine in File.ReadLines(path))
        {
            lineNumber++;
            var content = rawLine.Split('#', 2)[0].Trim();

            if (content.Length == 0)
            {
                continue;
            }

            var test = Parse(content);
            var actual = new List<int> { 0 };

            foreach (var segment in Graphemes.Enumerate(test.Value.AsSpan()))
            {
                actual.Add(segment.Offset + segment.Length);
            }

            actual.SequenceEqual(test.Boundaries).ShouldBeTrue(
                $"Unicode 17 GraphemeBreakTest line {lineNumber}: {content}");
        }
    }

    private static Case Parse(string content)
    {
        var tokens = content.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var value = new StringBuilder();
        var boundaries = new List<int>();

        for (var index = 0; index < tokens.Length; index += 2)
        {
            tokens[index].ShouldBeOneOf("÷", "×");

            if (tokens[index] == "÷")
            {
                boundaries.Add(value.Length);
            }

            if (index + 1 < tokens.Length)
            {
                var scalar = int.Parse(tokens[index + 1], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                _ = value.Append(new Rune(scalar));
            }
        }

        return new Case(value.ToString(), [.. boundaries]);
    }

}
