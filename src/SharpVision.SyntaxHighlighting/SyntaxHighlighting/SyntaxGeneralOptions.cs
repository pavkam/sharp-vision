// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting;

/// <summary>
/// Describes a syntax definition's <c>&lt;general&gt;</c> section: folding, comment metadata, and
/// the definition-wide keyword defaults every context and rule inherits.
/// </summary>
[PublicAPI]
public sealed class SyntaxGeneralOptions
{
    /// <summary>Initializes a fully specified, internally validated general-options section.</summary>
    /// <param name="folding">The folding declaration.</param>
    /// <param name="comments">The non-null, possibly empty comment definitions.</param>
    /// <param name="caseSensitiveKeywords">
    /// Whether keyword lists match case sensitively unless a list overrides this.
    /// </param>
    /// <param name="weakDeliminator">
    /// Non-null characters removed from the built-in default word-delimiter set for this whole
    /// definition.
    /// </param>
    /// <param name="additionalDeliminator">
    /// Non-null characters added to the word-delimiter set for this whole definition.
    /// </param>
    /// <param name="emptyLineRules">
    /// The non-null, possibly empty patterns indentation-based folding treats as blank lines.
    /// </param>
    internal SyntaxGeneralOptions(
        SyntaxFoldingOptions folding,
        IReadOnlyList<SyntaxCommentDefinition> comments,
        bool caseSensitiveKeywords,
        string weakDeliminator,
        string additionalDeliminator,
        IReadOnlyList<SyntaxEmptyLineRule> emptyLineRules)
    {
        Folding = folding;
        Comments = new SyntaxReadOnlyList<SyntaxCommentDefinition>(comments);
        CaseSensitiveKeywords = caseSensitiveKeywords;
        WeakDeliminator = weakDeliminator;
        AdditionalDeliminator = additionalDeliminator;
        EmptyLineRules = new SyntaxReadOnlyList<SyntaxEmptyLineRule>(emptyLineRules);
    }

    /// <summary>Gets the definition's default, all-contexts folding behavior.</summary>
    public SyntaxFoldingOptions Folding { get; }

    /// <summary>Gets the declared single-line and multi-line comment shapes.</summary>
    public IReadOnlyList<SyntaxCommentDefinition> Comments { get; }

    /// <summary>
    /// Gets whether keyword lists match case sensitively unless an individual list or rule
    /// overrides this.
    /// </summary>
    public bool CaseSensitiveKeywords { get; }

    /// <summary>
    /// Gets the characters removed from the built-in default word-delimiter set for every rule in
    /// this definition, before any further per-rule override.
    /// </summary>
    public string WeakDeliminator { get; }

    /// <summary>
    /// Gets the characters added to the word-delimiter set for every rule in this definition,
    /// before any further per-rule override.
    /// </summary>
    public string AdditionalDeliminator { get; }

    /// <summary>Gets the patterns indentation-based folding treats as blank lines.</summary>
    public IReadOnlyList<SyntaxEmptyLineRule> EmptyLineRules { get; }
}
