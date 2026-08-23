// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting;

/// <summary>
/// Represents one parsed KDE context rule, one of the 17 leaf match productions the syntax XML
/// schema defines.
/// </summary>
/// <remarks>
/// The 17 productions differ only in which one or two literal parameters they carry alongside a
/// shared set of common attributes; none has behavior beyond "match, then apply the common
/// attributes." Modeling them as 17 near-identical subclasses of an abstract base would add a
/// parallel type hierarchy purely for a discriminator the tokenizer already needs to switch on for
/// performance, so <see cref="Kind"/> plays that role directly on one flat type instead. Every
/// property below documents exactly which <see cref="SyntaxRuleKind"/> values populate it; a
/// property left at its default for a given kind is simply unused by that kind.
/// </remarks>
[PublicAPI]
public sealed class SyntaxRule
{
    /// <summary>Initializes a fully specified, internally validated rule.</summary>
    /// <param name="kind">Which context-rule element this instance represents.</param>
    /// <param name="attributeName">
    /// The item-data name this rule's own matched text is styled with, or null to inherit the
    /// owning context's attribute.
    /// </param>
    /// <param name="contextSwitch">The context change to apply after a successful match.</param>
    /// <param name="beginRegion">The fold-region name this rule begins, or null.</param>
    /// <param name="endRegion">The fold-region name this rule ends, or null.</param>
    /// <param name="lookAhead">Whether a match applies its context switch without consuming text.</param>
    /// <param name="firstNonSpace">Whether this rule only matches at the line's first non-space column.</param>
    /// <param name="column">The exact zero-based column this rule requires, or null for any column.</param>
    /// <param name="weakDeliminator">Characters this rule removes from the word-delimiter set.</param>
    /// <param name="additionalDeliminator">Characters this rule adds to the word-delimiter set.</param>
    /// <param name="keywordListName"><see cref="SyntaxRuleKind.Keyword"/>: the referenced list name.</param>
    /// <param name="keywordCaseSensitiveOverride">
    /// <see cref="SyntaxRuleKind.Keyword"/>: this rule's own case-sensitivity override (true means
    /// case sensitive, false means case insensitive), taking precedence over the keyword list's
    /// own override, or null to use the list's resolved case sensitivity unchanged.
    /// </param>
    /// <param name="text">
    /// The literal or pattern text for <see cref="SyntaxRuleKind.StringMatch"/>,
    /// <see cref="SyntaxRuleKind.WordMatch"/>, <see cref="SyntaxRuleKind.RegularExpression"/>, and
    /// the character set for <see cref="SyntaxRuleKind.AnyCharacter"/>.
    /// </param>
    /// <param name="char1">
    /// The first character for <see cref="SyntaxRuleKind.Character"/>,
    /// <see cref="SyntaxRuleKind.TwoCharacter"/>, <see cref="SyntaxRuleKind.Range"/>, and
    /// <see cref="SyntaxRuleKind.LineContinuation"/> (defaulting to <c>\</c> for the last one).
    /// </param>
    /// <param name="char2">
    /// The second character for <see cref="SyntaxRuleKind.TwoCharacter"/> and
    /// <see cref="SyntaxRuleKind.Range"/>.
    /// </param>
    /// <param name="insensitive">
    /// Whether <see cref="SyntaxRuleKind.StringMatch"/>, <see cref="SyntaxRuleKind.WordMatch"/>, or
    /// <see cref="SyntaxRuleKind.RegularExpression"/> matches case insensitively.
    /// </param>
    /// <param name="dynamic">
    /// Whether this rule's own literal parameters contain <c>%1</c>-<c>%9</c> placeholders resolved
    /// against the owning context's captured dynamic arguments.
    /// </param>
    /// <param name="minimal">
    /// Whether a <see cref="SyntaxRuleKind.RegularExpression"/> pattern's quantifiers prefer the
    /// shortest match.
    /// </param>
    /// <param name="includeContext">
    /// <see cref="SyntaxRuleKind.IncludeRules"/>: the context whose rules this one splices in place.
    /// </param>
    /// <param name="includeAttribute">
    /// <see cref="SyntaxRuleKind.IncludeRules"/>: whether the host context also adopts the included
    /// context's own attribute.
    /// </param>
    internal SyntaxRule(
        SyntaxRuleKind kind,
        string? attributeName,
        SyntaxContextSwitch contextSwitch,
        string? beginRegion,
        string? endRegion,
        bool lookAhead,
        bool firstNonSpace,
        int? column,
        string weakDeliminator,
        string additionalDeliminator,
        string? keywordListName = null,
        bool? keywordCaseSensitiveOverride = null,
        string? text = null,
        char char1 = '\0',
        char char2 = '\0',
        bool insensitive = false,
        bool dynamic = false,
        bool minimal = false,
        SyntaxContextReference? includeContext = null,
        bool includeAttribute = false)
    {
        Kind = kind;
        AttributeName = attributeName;
        ContextSwitch = contextSwitch;
        BeginRegion = beginRegion;
        EndRegion = endRegion;
        LookAhead = lookAhead;
        FirstNonSpace = firstNonSpace;
        Column = column;
        WeakDeliminator = weakDeliminator;
        AdditionalDeliminator = additionalDeliminator;
        KeywordListName = keywordListName;
        KeywordCaseSensitiveOverride = keywordCaseSensitiveOverride;
        Text = text;
        Char1 = char1;
        Char2 = char2;
        Insensitive = insensitive;
        Dynamic = dynamic;
        Minimal = minimal;
        IncludeContext = includeContext;
        IncludeAttribute = includeAttribute;
    }

    /// <summary>Gets which context-rule element this instance represents.</summary>
    public SyntaxRuleKind Kind { get; }

    /// <summary>
    /// Gets the item-data name this rule's own matched text is styled with, or null to inherit the
    /// owning context's attribute. Always null when <see cref="LookAhead"/> is set: a lookahead
    /// match never consumes or styles any text, so upstream KSyntaxHighlighting never even reads
    /// this rule's own <c>attribute</c> value in that case.
    /// </summary>
    public string? AttributeName { get; }

    /// <summary>
    /// Gets the context change to apply after a successful match, whether or not the match is a
    /// <see cref="LookAhead"/> match; only whether the matched text is consumed differs between
    /// the two.
    /// </summary>
    public SyntaxContextSwitch ContextSwitch { get; }

    /// <summary>Gets the fold-region name this rule begins, or null.</summary>
    public string? BeginRegion { get; }

    /// <summary>Gets the fold-region name this rule ends, or null.</summary>
    public string? EndRegion { get; }

    /// <summary>Gets whether a match applies its context switch without consuming any text.</summary>
    public bool LookAhead { get; }

    /// <summary>Gets whether this rule only matches at the line's first non-space column.</summary>
    public bool FirstNonSpace { get; }

    /// <summary>Gets the exact zero-based column this rule requires, or null for any column.</summary>
    public int? Column { get; }

    /// <summary>Gets the characters this rule removes from the word-delimiter set.</summary>
    public string WeakDeliminator { get; }

    /// <summary>Gets the characters this rule adds to the word-delimiter set.</summary>
    public string AdditionalDeliminator { get; }

    /// <summary>Gets the keyword-list name for a <see cref="SyntaxRuleKind.Keyword"/> rule.</summary>
    public string? KeywordListName { get; }

    /// <summary>
    /// Gets this <see cref="SyntaxRuleKind.Keyword"/> rule's own case-sensitivity override, taking
    /// precedence over the keyword list's own override, or null to leave the list's resolved case
    /// sensitivity unchanged.
    /// </summary>
    public bool? KeywordCaseSensitiveOverride { get; }

    /// <summary>
    /// Gets the literal or pattern text for a string, word, regular-expression, or any-character
    /// rule.
    /// </summary>
    public string? Text { get; }

    /// <summary>
    /// Gets the first character for a character, two-character, range, or line-continuation rule.
    /// When <see cref="Dynamic"/> is set on a <see cref="SyntaxRuleKind.Character"/> rule, this
    /// instead holds a decimal digit naming which captured dynamic argument to compare against.
    /// </summary>
    public char Char1 { get; }

    /// <summary>Gets the second character for a two-character or range rule.</summary>
    public char Char2 { get; }

    /// <summary>
    /// Gets whether a string, word, or regular-expression rule matches case insensitively.
    /// </summary>
    public bool Insensitive { get; }

    /// <summary>
    /// Gets whether this rule's own literal parameters are resolved against the owning context's
    /// captured dynamic arguments before matching.
    /// </summary>
    public bool Dynamic { get; }

    /// <summary>
    /// Gets whether a regular-expression rule's quantifiers prefer the shortest match.
    /// </summary>
    public bool Minimal { get; }

    /// <summary>
    /// Gets the context an <see cref="SyntaxRuleKind.IncludeRules"/> rule splices in place.
    /// </summary>
    public SyntaxContextReference? IncludeContext { get; }

    /// <summary>
    /// Gets whether an <see cref="SyntaxRuleKind.IncludeRules"/> rule also adopts the included
    /// context's own attribute.
    /// </summary>
    public bool IncludeAttribute { get; }
}
