// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting;

/// <summary>
/// Represents one fully parsed KDE syntax-definition XML document: its metadata, keyword lists,
/// item-data style roles, and contexts, exactly as <see cref="SyntaxDefinitionReader"/> read them.
/// </summary>
/// <remarks>
/// This is the raw parsed shape: cross-definition <c>IncludeRules</c> and context-switch targets
/// are recorded as unresolved <see cref="SyntaxContextReference"/> values. <see cref="SyntaxGrammar"/>
/// performs that resolution, given a way to look up other definitions by name.
/// </remarks>
[PublicAPI]
public sealed class SyntaxDefinition
{
    /// <summary>Initializes a fully specified, internally validated syntax definition.</summary>
    /// <param name="name">The non-null, non-empty language name.</param>
    /// <param name="alternativeNames">Non-null additional names this definition also matches under.</param>
    /// <param name="section">The non-null logical grouping, such as <c>Sources</c> or <c>Markup</c>.</param>
    /// <param name="extensions">Non-null file-glob patterns identifying documents of this language.</param>
    /// <param name="mimeTypes">Non-null MIME types identifying documents of this language.</param>
    /// <param name="version">The definition's own non-negative revision number.</param>
    /// <param name="kateVersion">The minimum Kate syntax engine version required by the definition.</param>
    /// <param name="priority">The relative priority among multiple usable definitions for one document, or null.</param>
    /// <param name="author">The non-null declared author.</param>
    /// <param name="license">The non-null declared license.</param>
    /// <param name="indenter">The suggested indenter name, or null.</param>
    /// <param name="hidden">Whether this definition should stay out of a language-selection menu.</param>
    /// <param name="general">The parsed <c>&lt;general&gt;</c> section.</param>
    /// <param name="keywordLists">The non-null keyword lists, keyed by name.</param>
    /// <param name="itemDataSet">The non-null item-data style roles, keyed by name.</param>
    /// <param name="contexts">The non-null, non-empty contexts; the first is the start context.</param>
    /// <exception cref="ArgumentException"><paramref name="contexts"/> is empty.</exception>
    internal SyntaxDefinition(
        string name,
        IReadOnlyList<string> alternativeNames,
        string section,
        IReadOnlyList<string> extensions,
        IReadOnlyList<string> mimeTypes,
        int version,
        Version kateVersion,
        int? priority,
        string author,
        string license,
        string? indenter,
        bool hidden,
        SyntaxGeneralOptions general,
        IReadOnlyDictionary<string, SyntaxKeywordList> keywordLists,
        IReadOnlyDictionary<string, SyntaxItemData> itemDataSet,
        IReadOnlyList<SyntaxContext> contexts)
    {
        if (contexts.Count == 0)
        {
            throw new ArgumentException("A syntax definition must declare at least one context.", nameof(contexts));
        }

        Name = name;
        AlternativeNames = new SyntaxReadOnlyList<string>(alternativeNames);
        Section = section;
        Extensions = new SyntaxReadOnlyList<string>(extensions);
        MimeTypes = new SyntaxReadOnlyList<string>(mimeTypes);
        Version = version;
        KateVersion = kateVersion;
        Priority = priority;
        Author = author;
        License = license;
        Indenter = indenter;
        Hidden = hidden;
        General = general;
        KeywordLists = new SyntaxReadOnlyDictionary<string, SyntaxKeywordList>(keywordLists);
        ItemDataSet = new SyntaxReadOnlyDictionary<string, SyntaxItemData>(itemDataSet);
        Contexts = new SyntaxReadOnlyList<SyntaxContext>(contexts);
    }

    /// <summary>Gets the language name, referenced by cross-definition includes and switches.</summary>
    public string Name { get; }

    /// <summary>Gets additional names this definition also matches under.</summary>
    public IReadOnlyList<string> AlternativeNames { get; }

    /// <summary>Gets the logical grouping, such as <c>Sources</c> or <c>Markup</c>.</summary>
    public string Section { get; }

    /// <summary>Gets the file-glob patterns identifying documents of this language.</summary>
    public IReadOnlyList<string> Extensions { get; }

    /// <summary>Gets the MIME types identifying documents of this language.</summary>
    public IReadOnlyList<string> MimeTypes { get; }

    /// <summary>Gets the definition's own revision number.</summary>
    public int Version { get; }

    /// <summary>Gets the minimum Kate syntax engine version required by this definition.</summary>
    public Version KateVersion { get; }

    /// <summary>Gets the relative priority among multiple usable definitions for one document, or null.</summary>
    public int? Priority { get; }

    /// <summary>Gets the declared author.</summary>
    public string Author { get; }

    /// <summary>Gets the declared license.</summary>
    public string License { get; }

    /// <summary>Gets the suggested indenter name, or null.</summary>
    public string? Indenter { get; }

    /// <summary>Gets whether this definition should stay out of a language-selection menu.</summary>
    public bool Hidden { get; }

    /// <summary>Gets the parsed <c>&lt;general&gt;</c> section.</summary>
    public SyntaxGeneralOptions General { get; }

    /// <summary>Gets the keyword lists, keyed by name.</summary>
    public IReadOnlyDictionary<string, SyntaxKeywordList> KeywordLists { get; }

    /// <summary>Gets the item-data style roles, keyed by name.</summary>
    public IReadOnlyDictionary<string, SyntaxItemData> ItemDataSet { get; }

    /// <summary>Gets the contexts; <see cref="Contexts"/>[0] is the start context.</summary>
    public IReadOnlyList<SyntaxContext> Contexts { get; }
}
