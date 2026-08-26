// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting.Tests;

/// <summary>Verifies <see cref="SyntaxWordDelimiters"/>'s default set and override composition.</summary>
public sealed class SyntaxWordDelimitersTests
{
    /// <summary>Verifies the default struct is a valid empty delimiter set whose query, derivation,
    /// equality, and hashing members are all safe.</summary>
    [Fact]
    public void DefaultValue_WhenUsed_BehavesAsAnEmptyDelimiterSet()
    {
        var delimiters = default(SyntaxWordDelimiters);

        delimiters.Contains('a').ShouldBeFalse();
        delimiters.With("@", string.Empty).Contains('@').ShouldBeTrue();
        delimiters.Equals(default).ShouldBeTrue();
        delimiters.GetHashCode().ShouldBe(default(SyntaxWordDelimiters).GetHashCode());
    }
    /// <summary>
    /// Verifies <see cref="SyntaxWordDelimiters.Default"/> contains exactly the built-in upstream
    /// KSyntaxHighlighting <c>WordDelimiters::WordDelimiters()</c> character set - the tab and
    /// space characters plus <c>!%&amp;()*+,-./:;&lt;=&gt;?[\]^{|}~</c> - character-for-character, no
    /// more and no fewer.
    /// </summary>
    [Fact]
    public void Default_WhenInspected_ContainsExactUpstreamCharacterSet()
    {
        const string expected = "\t !%&()*+,-./:;<=>?[\\]^{|}~";
        var delimiters = SyntaxWordDelimiters.Default;

        foreach (var c in expected)
        {
            delimiters.Contains(c).ShouldBeTrue($"'{c}' (U+{(int) c:X4}) should be a default delimiter.");
        }

        for (var c = (char) 0; c < 128; c++)
        {
            var shouldBeDelimiter = expected.Contains(c);
            delimiters.Contains(c).ShouldBe(shouldBeDelimiter, $"'{c}' (U+{(int) c:X4}) delimiter membership mismatch.");
        }
    }

    /// <summary>
    /// Verifies <c>additionalDeliminator</c> adds characters to the effective set without removing
    /// any of the built-in defaults.
    /// </summary>
    [Fact]
    public void With_WhenAdditionalSpecifiesACharacter_AddsItWithoutRemovingDefaults()
    {
        var delimiters = SyntaxWordDelimiters.Default.With("@", string.Empty);

        delimiters.Contains('@').ShouldBeTrue();
        delimiters.Contains('.').ShouldBeTrue();
    }

    /// <summary>
    /// Verifies <c>weakDeliminator</c> removes a character from the built-in default set rather
    /// than adding it - the opposite direction of <c>additionalDeliminator</c>.
    /// </summary>
    [Fact]
    public void With_WhenWeakSpecifiesADefaultDelimiter_RemovesItFromTheEffectiveSet()
    {
        var delimiters = SyntaxWordDelimiters.Default.With(string.Empty, ".");

        delimiters.Contains('.').ShouldBeFalse();
        delimiters.Contains(',').ShouldBeTrue();
    }

    /// <summary>Verifies a null <c>additional</c> argument throws <see cref="ArgumentNullException"/>.</summary>
    [Fact]
    public void With_WhenAdditionalIsNull_ThrowsArgumentNullException() =>
        _ = Should.Throw<ArgumentNullException>(() => SyntaxWordDelimiters.Default.With(null!, string.Empty));

    /// <summary>Verifies a null <c>weak</c> argument throws <see cref="ArgumentNullException"/>.</summary>
    [Fact]
    public void With_WhenWeakIsNull_ThrowsArgumentNullException() =>
        _ = Should.Throw<ArgumentNullException>(() => SyntaxWordDelimiters.Default.With(string.Empty, null!));

    /// <summary>
    /// Verifies a non-ASCII additional delimiter is honored: the delimiter set stores non-ASCII
    /// characters separately from the ASCII fast-path table.
    /// </summary>
    [Fact]
    public void With_WhenAdditionalSpecifiesANonAsciiCharacter_AddsItToTheEffectiveSet()
    {
        var delimiters = SyntaxWordDelimiters.Default.With("\u00a7", string.Empty);

        delimiters.Contains('\u00a7').ShouldBeTrue();
    }
}
