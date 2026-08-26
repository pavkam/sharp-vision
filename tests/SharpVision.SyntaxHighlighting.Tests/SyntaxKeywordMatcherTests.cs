// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting.Tests;

/// <summary>Verifies <see cref="SyntaxKeywordMatcher"/>'s boundary handling and case sensitivity.</summary>
public sealed class SyntaxKeywordMatcherTests
{
    /// <summary>
    /// Verifies a negative offset throws the documented <see cref="ArgumentOutOfRangeException"/>
    /// instead of an undocumented <see cref="IndexOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void Match_WhenOffsetIsNegative_ThrowsArgumentOutOfRangeException()
    {
        var matcher = new SyntaxKeywordMatcher(["if"], caseSensitive: true, SyntaxWordDelimiters.Default);

        _ = Should.Throw<ArgumentOutOfRangeException>(() => matcher.Match("if", -1));
    }

    /// <summary>
    /// Verifies an offset past the end of the line throws the documented
    /// <see cref="ArgumentOutOfRangeException"/> instead of an undocumented
    /// <see cref="IndexOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void Match_WhenOffsetExceedsLineLength_ThrowsArgumentOutOfRangeException()
    {
        var matcher = new SyntaxKeywordMatcher(["if"], caseSensitive: true, SyntaxWordDelimiters.Default);

        _ = Should.Throw<ArgumentOutOfRangeException>(() => matcher.Match("if", 5));
    }

    /// <summary>Verifies an offset exactly at the end of the line is a valid, non-throwing "no match".</summary>
    [Fact]
    public void Match_WhenOffsetEqualsLineLength_ReturnsZeroWithoutThrowing()
    {
        var matcher = new SyntaxKeywordMatcher(["if"], caseSensitive: true, SyntaxWordDelimiters.Default);

        matcher.Match("if", 2).ShouldBe(0);
    }

    /// <summary>
    /// Verifies a keyword that is only a prefix of a longer identifier does not match: the scan
    /// always runs to the next delimiter before comparing the whole scanned word against the list.
    /// </summary>
    [Fact]
    public void Match_WhenKeywordIsPrefixOfLongerIdentifier_DoesNotMatch()
    {
        var matcher = new SyntaxKeywordMatcher(["if"], caseSensitive: true, SyntaxWordDelimiters.Default);

        matcher.Match("iffy", 0).ShouldBe(0);
    }

    /// <summary>Verifies a keyword bounded by a delimiter matches for its exact word length.</summary>
    [Fact]
    public void Match_WhenKeywordIsBoundedByDelimiter_MatchesFullWordLength()
    {
        var matcher = new SyntaxKeywordMatcher(["if"], caseSensitive: true, SyntaxWordDelimiters.Default);

        matcher.Match("if(x)", 0).ShouldBe(2);
    }

    /// <summary>Verifies case-sensitive matching rejects a differently cased word.</summary>
    [Fact]
    public void Match_WhenCaseSensitiveAndCaseDiffers_DoesNotMatch()
    {
        var matcher = new SyntaxKeywordMatcher(["if"], caseSensitive: true, SyntaxWordDelimiters.Default);

        matcher.Match("IF", 0).ShouldBe(0);
    }

    /// <summary>Verifies case-insensitive matching accepts a differently cased word.</summary>
    [Fact]
    public void Match_WhenCaseInsensitiveAndCaseDiffers_Matches()
    {
        var matcher = new SyntaxKeywordMatcher(["if"], caseSensitive: false, SyntaxWordDelimiters.Default);

        matcher.Match("IF", 0).ShouldBe(2);
    }

    /// <summary>Verifies case-insensitive keyword lookup uses Qt-compatible Unicode folding.</summary>
    [Theory]
    [InlineData("K", "K")]
    [InlineData("S", "ſ")]
    public void Match_WhenCaseInsensitiveUnicodeFoldDiffersFromOrdinal_Matches(string keyword, string text)
    {
        var matcher = new SyntaxKeywordMatcher([keyword], caseSensitive: false, SyntaxWordDelimiters.Default);

        matcher.Match(text, 0).ShouldBe(text.Length);
    }

    /// <summary>Verifies candidate lookup compares the source span without allocating a temporary string.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Match_WhenRepeatedAfterWarmup_AllocatesNoCandidateStrings(bool caseSensitive)
    {
        var matcher = new SyntaxKeywordMatcher(["keyword"], caseSensitive, SyntaxWordDelimiters.Default);
        _ = matcher.Match("identifier", 0);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var matches = 0;

        for (var index = 0; index < 1_000; index++)
        {
            matches += matcher.Match("identifier", 0);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        matches.ShouldBe(0);
        allocated.ShouldBe(0);
    }
}
