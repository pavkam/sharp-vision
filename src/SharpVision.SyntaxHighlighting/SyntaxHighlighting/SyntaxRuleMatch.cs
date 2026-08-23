// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting;

/// <summary>Represents the outcome of attempting to match one <see cref="SyntaxCompiledRule"/>.</summary>
[PublicAPI]
public readonly record struct SyntaxRuleMatch
{
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
        Captures = captures;
    }

    /// <summary>Gets whether the rule matched at the attempted offset.</summary>
    public bool Success { get; }

    /// <summary>Gets the number of characters consumed by a successful match.</summary>
    public int Length { get; }

    /// <summary>Gets the captured groups available to propagate as dynamic arguments.</summary>
    public IReadOnlyList<string> Captures { get; } = [];
}
