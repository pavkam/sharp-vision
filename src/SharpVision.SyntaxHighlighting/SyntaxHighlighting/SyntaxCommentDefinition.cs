// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting;

/// <summary>
/// Describes one single-line or multi-line comment shape a syntax definition's <c>&lt;general&gt;</c>
/// section declares, informational metadata a host application can use for comment-toggling
/// commands.
/// </summary>
[PublicAPI]
public sealed class SyntaxCommentDefinition
{
    /// <summary>Initializes a fully specified, internally validated comment definition.</summary>
    /// <param name="kind">Whether this is a single-line or multi-line comment.</param>
    /// <param name="start">The non-null, non-empty marker that starts the comment.</param>
    /// <param name="end">
    /// The marker that ends a multi-line comment, or null for a single-line comment.
    /// </param>
    /// <param name="region">
    /// The optional fold-region name shared with the <c>beginRegion</c>/<c>endRegion</c> rules
    /// that actually delimit this comment in its contexts, or null when the definition does not
    /// name one.
    /// </param>
    /// <param name="matchAfterWhitespace">
    /// Whether a single-line comment is conventionally inserted after leading whitespace rather
    /// than at column zero.
    /// </param>
    internal SyntaxCommentDefinition(
        SyntaxCommentKind kind,
        string start,
        string? end,
        string? region,
        bool matchAfterWhitespace)
    {
        Kind = kind;
        Start = start;
        End = end;
        Region = region;
        MatchAfterWhitespace = matchAfterWhitespace;
    }

    /// <summary>Gets whether this is a single-line or multi-line comment.</summary>
    public SyntaxCommentKind Kind { get; }

    /// <summary>Gets the marker that starts the comment.</summary>
    public string Start { get; }

    /// <summary>Gets the marker that ends a multi-line comment, or null for a single-line comment.</summary>
    public string? End { get; }

    /// <summary>
    /// Gets the fold-region name shared with this comment's delimiting rules, or null when the
    /// definition does not name one.
    /// </summary>
    public string? Region { get; }

    /// <summary>
    /// Gets whether a single-line comment is conventionally inserted after leading whitespace
    /// rather than at column zero.
    /// </summary>
    public bool MatchAfterWhitespace { get; }
}
