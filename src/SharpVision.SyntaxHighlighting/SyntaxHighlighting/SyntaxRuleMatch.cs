// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting;

/// <summary>Represents the outcome of attempting to match one <see cref="SyntaxCompiledRule"/>.</summary>
[PublicAPI]
public readonly record struct SyntaxRuleMatch
{
#pragma warning disable IDE0032 // Default structs need null-coalescing getters over nullable backing storage.
    private readonly IReadOnlyList<string>? _captures;
#pragma warning restore IDE0032

    /// <summary>The result for a rule that did not match.</summary>
    public static readonly SyntaxRuleMatch None;

    /// <summary>Initializes a successful match.</summary>
    /// <param name="length">The non-negative number of characters consumed.</param>
    /// <param name="captures">
    /// The non-null, possibly empty regular-expression capture groups 1-9, present only for a
    /// successful <see cref="SyntaxRuleKind.RegularExpression"/> match; used to populate the
    /// dynamic arguments of any context this rule's match pushes.
    /// </param>
    internal SyntaxRuleMatch(int length, IReadOnlyList<string> captures)
    {
        Success = true;
        Length = length;
        _captures = new SyntaxReadOnlyList<string>(captures);
        SkipOffset = 0;
    }

    /// <summary>
    /// Initializes a failed match that still reports how far forward a caller may safely skip
    /// re-invoking a <see cref="SyntaxRuleKind.Keyword"/> or <see cref="SyntaxRuleKind.RegularExpression"/>
    /// rule within the same line, mirroring upstream KSyntaxHighlighting's own skip-offset
    /// optimization (<c>KeywordListRule::doMatch</c>, <c>regexMatch</c>). Retrying either rule kind
    /// at an intermediate offset always re-derives the exact same boundary - a keyword rule scans
    /// forward to the identical next delimiter regardless of where within an undelimited run it
    /// starts, and a non-anchored regex search from an earlier offset already reports where its
    /// next possible match begins - so caching this bound turns what would otherwise be quadratic
    /// rescanning of one long token into linear work.
    /// </summary>
    /// <param name="skipOffset">
    /// The offset before which this rule cannot possibly match again on the current line, or a
    /// negative value when it can never match again on this line at all.
    /// </param>
    internal SyntaxRuleMatch(int skipOffset)
    {
        Success = false;
        Length = 0;
        _captures = SyntaxReadOnlyList<string>.Empty;
        SkipOffset = skipOffset;
    }

    /// <summary>Gets whether the rule matched at the attempted offset.</summary>
    public bool Success { get; }

    /// <summary>Gets the number of characters consumed by a successful match.</summary>
    public int Length { get; }

    /// <summary>Gets the captured groups available to propagate as dynamic arguments.</summary>
    public IReadOnlyList<string> Captures => _captures ?? SyntaxReadOnlyList<string>.Empty;

    /// <summary>
    /// Gets the offset before which this rule is known not to match again on the current line, a
    /// negative value when it is known never to match again at all, or zero when this rule kind
    /// reports no such hint. <see cref="SyntaxTokenizer"/> is the only reader of this value.
    /// </summary>
    internal int SkipOffset { get; }
}
