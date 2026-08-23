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
}
