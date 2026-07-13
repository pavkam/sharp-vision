// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Unicode;

using System.Globalization;

using SharpVision.Terminal.Unicode;


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
        string path = Path.Combine(
            AppContext.BaseDirectory,
            "Unicode",
            "Data",
            "GraphemeBreakTest-17.0.0.txt");
        int lineNumber = 0;

        foreach (string rawLine in File.ReadLines(path))
        {
            lineNumber++;
            string content = rawLine.Split('#', 2)[0].Trim();

            if (content.Length == 0)
            {
                continue;
            }

            Case test = Parse(content);
            List<int> actual = [0];

            foreach (Grapheme segment in Graphemes.Enumerate(test.Value.AsSpan()))
            {
                actual.Add(segment.Offset + segment.Length);
            }

            actual.SequenceEqual(test.Boundaries).ShouldBeTrue(
                $"Unicode 17 GraphemeBreakTest line {lineNumber}: {content}");
        }
    }

    private static Case Parse(string content)
    {
        string[] tokens = content.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        StringBuilder value = new();
        List<int> boundaries = [];

        for (int index = 0; index < tokens.Length; index += 2)
        {
            tokens[index].ShouldBeOneOf("÷", "×");

            if (tokens[index] == "÷")
            {
                boundaries.Add(value.Length);
            }

            if (index + 1 < tokens.Length)
            {
                int scalar = int.Parse(tokens[index + 1], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                _ = value.Append(new Rune(scalar));
            }
        }

        return new Case(value.ToString(), [.. boundaries]);
    }

}
