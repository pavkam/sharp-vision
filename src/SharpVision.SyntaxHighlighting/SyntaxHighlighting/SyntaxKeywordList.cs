// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting;

/// <summary>
/// Describes one named <c>&lt;list&gt;</c> of keyword literals a <c>&lt;keyword&gt;</c> rule
/// matches against. Any <c>&lt;include&gt;</c> of another list is already flattened into
/// <see cref="Words"/> by the time <see cref="SyntaxDefinitionReader"/> returns this instance.
/// </summary>
/// <remarks>
/// The KDE schema gives <c>&lt;list&gt;</c> only a name: case sensitivity and delimiter overrides
/// belong to a specific <c>&lt;keyword&gt;</c> rule usage (see
/// <see cref="SyntaxRule.KeywordCaseSensitiveOverride"/>, <see cref="SyntaxRule.WeakDeliminator"/>,
/// and <see cref="SyntaxRule.AdditionalDeliminator"/>), not to the list itself; two different rules
/// may reference the same list with different overrides.
/// </remarks>
[PublicAPI]
public sealed class SyntaxKeywordList
{
    /// <summary>Initializes a fully specified, internally validated keyword list.</summary>
    /// <param name="name">The non-null, non-empty list name.</param>
    /// <param name="words">
    /// The non-null, possibly empty flattened literal words from same-file includes. A list whose
    /// only content is a cross-definition <c>&lt;include&gt;</c> - unresolvable until grammar
    /// compilation - has no same-file words of its own and is legitimately empty here.
    /// </param>
    /// <param name="crossDefinitionIncludes">
    /// The non-null, raw <c>"ListName##DefinitionName"</c> texts of any <c>&lt;include&gt;</c> this
    /// list could not resolve within its own file, left for grammar compilation to resolve since
    /// only that phase has access to other definitions.
    /// </param>
    internal SyntaxKeywordList(string name, IReadOnlyList<string> words, IReadOnlyList<string> crossDefinitionIncludes)
    {
        Name = name;
        Words = new SyntaxReadOnlyList<string>(words);
        CrossDefinitionIncludes = new SyntaxReadOnlyList<string>(crossDefinitionIncludes);
    }

    /// <summary>Gets the list name, as referenced by a <c>&lt;keyword String="…"&gt;</c> rule.</summary>
    public string Name { get; }

    /// <summary>Gets the flattened literal words from every same-file include.</summary>
    public IReadOnlyList<string> Words { get; }

    /// <summary>
    /// Gets the raw <c>"ListName##DefinitionName"</c> texts of any cross-definition
    /// <c>&lt;include&gt;</c>, unresolved until grammar compilation.
    /// </summary>
    public IReadOnlyList<string> CrossDefinitionIncludes { get; }
}
