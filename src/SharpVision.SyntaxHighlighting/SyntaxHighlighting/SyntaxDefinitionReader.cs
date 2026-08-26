// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting;

using System.Xml;

using MustUseReturnValue = JetBrains.Annotations.MustUseReturnValueAttribute;

/// <summary>
/// Parses a complete KDE syntax-definition XML document (the format documented at
/// <see href="https://docs.kde.org/?application=katepart&amp;branch=stable5&amp;path=highlight.html"/>)
/// into an immutable <see cref="SyntaxDefinition"/>.
/// </summary>
/// <remarks>
/// A syntax-definition file is authored content, not end-user input: this reader fails fast with
/// a descriptive <see cref="FormatException"/> on the first structural problem rather than
/// collecting tolerant diagnostics, the same contract <c>FigletFont.Load</c> and
/// <see cref="System.Text.Json.JsonDocument"/> use for other authored-format loaders in
/// SharpVision. Every file this type reads still runs through bounded, defensive XML parsing:
/// DTD internal entities (several upstream definitions use them for repeated literals) are
/// expanded, but external DTD subsets and unbounded entity expansion are both rejected.
/// </remarks>
[PublicAPI]
public static class SyntaxDefinitionReader
{
    private static readonly Version _supportedKateVersion = new(6, 22);
    private const int _maxCharactersFromEntities = 4_000_000;
    private const int _maxCharactersInDocument = 16_000_000;

    /// <summary>Parses one complete syntax-definition XML document.</summary>
    /// <param name="xml">The non-null complete XML document text.</param>
    /// <returns>The immutable parsed definition.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="xml"/> is null.</exception>
    /// <exception cref="FormatException">
    /// <paramref name="xml"/> is not well-formed, or violates a structural requirement of the
    /// syntax-definition schema (a missing required attribute, an unknown default style, a
    /// dangling keyword-list reference, and so on).
    /// </exception>
    [MustUseReturnValue]
    public static SyntaxDefinition Read(string xml)
    {
        ArgumentNullException.ThrowIfNull(xml);

        using var stringReader = new StringReader(xml);
        using var xmlReader = XmlReader.Create(stringReader, CreateSettings());
        return Read(xmlReader);
    }

    /// <summary>Parses one complete definition directly from a bounded stream. This internal path
    /// lets directory catalogs avoid allocating and retaining a second full XML string.</summary>
    /// <param name="stream">The non-null readable definition stream.</param>
    /// <returns>The immutable parsed definition.</returns>
    internal static SyntaxDefinition Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var xmlReader = XmlReader.Create(stream, CreateSettings());
        return Read(xmlReader);
    }

    private static SyntaxDefinition Read(XmlReader xmlReader)
    {
        try
        {
            var document = XDocument.Load(xmlReader, LoadOptions.None);
            var language = document.Root ?? throw new FormatException("The document has no root element.");

            if (language.Name.LocalName != "language")
            {
                throw new FormatException($"Expected a root <language> element, found <{language.Name.LocalName}>.");
            }

            ValidateLanguageStructure(language);
            var highlighting = RequiredChild(language, "highlighting");
            ValidateHighlightingStructure(highlighting);
            var keywordLists = ReadKeywordLists(highlighting);
            var itemDataSet = ReadItemDataSet(highlighting);
            var contexts = ReadContexts(highlighting, itemDataSet);
            var general = ReadGeneral(language.Element("general"), language);
            var kateVersion = ParseKateVersion(language);

            // SyntaxDefinition's own constructor also enforces this, but only as internal
            // defense in depth: this reader is the sole caller, and it must surface every
            // structural problem as the documented FormatException, not let an internal
            // ArgumentException from that constructor escape this public API undocumented.
            return contexts.Count == 0
                ? throw new FormatException("A syntax definition must declare at least one <context>.")
                : new SyntaxDefinition(
                    name: RequiredNonWhitespaceAttribute(language, "name"),
                    alternativeNames: SplitList(Attribute(language, "alternativeNames")),
                    section: RequiredAttribute(language, "section"),
                    extensions: SplitList(RequiredAttribute(language, "extensions")),
                    mimeTypes: SplitList(Attribute(language, "mimetype")),
                    version: ParseRequiredNonNegativeInt(language, "version"),
                    kateVersion: kateVersion,
                    priority: TryParseInt(Attribute(language, "priority")),
                    author: Attribute(language, "author") ?? string.Empty,
                    license: Attribute(language, "license") ?? string.Empty,
                    indenter: Attribute(language, "indenter"),
                    style: Attribute(language, "style") ?? string.Empty,
                    hidden: ParseBool(Attribute(language, "hidden")),
                    general: general,
                    keywordLists: keywordLists,
                    itemDataSet: itemDataSet,
                    contexts: contexts);
        }
        catch (XmlException exception)
        {
            throw new FormatException($"The syntax definition is not well-formed XML: {exception.Message}", exception);
        }
    }

    private static Version ParseKateVersion(XElement language)
    {
        var raw = RequiredAttribute(language, "kateversion");

        var version = Version.TryParse(raw, out var parsed) && parsed.Major >= 0 && parsed.Minor >= 0
            ? parsed
            : throw new FormatException($"Attribute 'kateversion' value '{raw}' is not a valid version.");

        return version <= _supportedKateVersion
            ? version
            : throw new FormatException(
                $"Syntax definition requires Kate format {version}, newer than supported {_supportedKateVersion}.");
    }

    #region Document loading

    private static XmlReaderSettings CreateSettings() =>
        new()
        {
            DtdProcessing = DtdProcessing.Parse,
            XmlResolver = null,
            MaxCharactersFromEntities = _maxCharactersFromEntities,
            MaxCharactersInDocument = _maxCharactersInDocument,
        };

    #endregion

    #region Keyword lists

    private static Dictionary<string, SyntaxKeywordList> ReadKeywordLists(XElement highlighting)
    {
        var rawChildren = new Dictionary<string, List<(bool IsInclude, string Value)>>(StringComparer.Ordinal);

        foreach (var list in highlighting.Elements("list"))
        {
            var name = RequiredNonWhitespaceAttribute(list, "name");
            var children = new List<(bool IsInclude, string Value)>();

            foreach (var child in list.Elements())
            {
                switch (child.Name.LocalName)
                {
                    case "item":
                        // Upstream KeywordList::load trims each <item>'s element text
                        // (readElementText().trimmed()) before storing it: several embedded
                        // definitions indent keyword lists one entry per line, and an untrimmed
                        // entry would never match any real identifier in a document.
                        children.Add((false, child.Value.Trim()));
                        break;
                    case "include":
                        children.Add((true, child.Value.Trim()));
                        break;
                    default:
                        throw new FormatException($"Unknown <list> child <{child.Name.LocalName}> in list '{name}'.");
                }
            }

            rawChildren[name] = children;
        }

        var resolved = new Dictionary<string, SyntaxKeywordList>(StringComparer.Ordinal);

        foreach (var name in rawChildren.Keys)
        {
            ResolveKeywordList(name, rawChildren, resolved, new HashSet<string>(StringComparer.Ordinal));
        }

        return resolved;
    }

    private static void ResolveKeywordList(
        string name,
        Dictionary<string, List<(bool IsInclude, string Value)>> rawChildren,
        Dictionary<string, SyntaxKeywordList> resolved,
        HashSet<string> inProgress)
    {
        if (resolved.ContainsKey(name))
        {
            return;
        }

        if (!inProgress.Add(name))
        {
            throw new FormatException($"Keyword list '{name}' has a cyclic <include>.");
        }

        if (!rawChildren.TryGetValue(name, out var children))
        {
            throw new FormatException($"Keyword list '{name}' is referenced but not declared.");
        }

        var words = new List<string>();
        var crossDefinitionIncludes = new List<string>();

        foreach (var (isInclude, value) in children)
        {
            if (!isInclude)
            {
                words.Add(value);
                continue;
            }

            // A "ListName##OtherDefinition" include can only be resolved once other definitions
            // are available, which is not until grammar compilation; see SyntaxGrammar. Its
            // position relative to same-file items and includes is not preserved across that
            // deferral, since the grammar-compile phase always appends resolved cross-definition
            // words after this list's own same-file content.
            if (value.Contains("##", StringComparison.Ordinal))
            {
                crossDefinitionIncludes.Add(value);
                continue;
            }

            ResolveKeywordList(value, rawChildren, resolved, inProgress);

            // ResolveKeywordList only returns without throwing once "value" is in resolved:
            // either it already was (the early-return branch above), or this call just added it
            // (the assignment below runs on every non-throwing path). A cyclic or undeclared
            // reference always throws before reaching here instead.
            Debug.Assert(resolved.ContainsKey(value), "A non-throwing ResolveKeywordList call always leaves its target resolved.");

            words.AddRange(resolved[value].Words);
            crossDefinitionIncludes.AddRange(resolved[value].CrossDefinitionIncludes);
        }

        resolved[name] = new SyntaxKeywordList(name, words, crossDefinitionIncludes);
        _ = inProgress.Remove(name);
    }

    #endregion

    #region Item data

    private static Dictionary<string, SyntaxItemData> ReadItemDataSet(XElement highlighting)
    {
        var itemDatas = RequiredChild(highlighting, "itemDatas");
        var result = new Dictionary<string, SyntaxItemData>(StringComparer.Ordinal);

        foreach (var itemData in itemDatas.Elements("itemData"))
        {
            var name = RequiredNonWhitespaceAttribute(itemData, "name");
            var defaultStyle = ParseDefaultStyle(RequiredAttribute(itemData, "defStyleNum"));
            result[name] = new SyntaxItemData(name, defaultStyle);
        }

        return result;
    }

    private static SyntaxDefaultStyle ParseDefaultStyle(string value) => value switch
    {
        "dsNormal" => SyntaxDefaultStyle.Normal,
        "dsKeyword" => SyntaxDefaultStyle.Keyword,
        "dsFunction" => SyntaxDefaultStyle.Function,
        "dsVariable" => SyntaxDefaultStyle.Variable,
        "dsControlFlow" => SyntaxDefaultStyle.ControlFlow,
        "dsOperator" => SyntaxDefaultStyle.Operator,
        "dsBuiltIn" => SyntaxDefaultStyle.BuiltIn,
        "dsExtension" => SyntaxDefaultStyle.Extension,
        "dsPreprocessor" => SyntaxDefaultStyle.Preprocessor,
        "dsAttribute" => SyntaxDefaultStyle.Attribute,
        "dsChar" => SyntaxDefaultStyle.Char,
        "dsSpecialChar" => SyntaxDefaultStyle.SpecialChar,
        "dsString" => SyntaxDefaultStyle.String,
        "dsVerbatimString" => SyntaxDefaultStyle.VerbatimString,
        "dsSpecialString" => SyntaxDefaultStyle.SpecialString,
        "dsImport" => SyntaxDefaultStyle.Import,
        "dsDataType" => SyntaxDefaultStyle.DataType,
        "dsDecVal" => SyntaxDefaultStyle.DecimalValue,
        "dsBaseN" => SyntaxDefaultStyle.BaseN,
        "dsFloat" => SyntaxDefaultStyle.Float,
        "dsConstant" => SyntaxDefaultStyle.Constant,
        "dsComment" => SyntaxDefaultStyle.Comment,
        "dsDocumentation" => SyntaxDefaultStyle.Documentation,
        "dsAnnotation" => SyntaxDefaultStyle.Annotation,
        "dsCommentVar" => SyntaxDefaultStyle.CommentVariable,
        "dsRegionMarker" => SyntaxDefaultStyle.RegionMarker,
        "dsInformation" => SyntaxDefaultStyle.Information,
        "dsWarning" => SyntaxDefaultStyle.Warning,
        "dsAlert" => SyntaxDefaultStyle.Alert,
        "dsOthers" => SyntaxDefaultStyle.Others,
        "dsError" => SyntaxDefaultStyle.Error,
        _ => throw new FormatException($"Unknown default style '{value}'."),
    };

    #endregion

    #region Contexts and rules

    private static List<SyntaxContext> ReadContexts(XElement highlighting, IReadOnlyDictionary<string, SyntaxItemData> itemDataSet)
    {
        var contextsElement = RequiredChild(highlighting, "contexts");
        var contexts = new List<SyntaxContext>();

        foreach (var context in contextsElement.Elements("context"))
        {
            var name = RequiredNonWhitespaceAttribute(context, "name");
            var attributeName = ResolveAttributeName(context, itemDataSet, name);
            var lineEndContext = SyntaxContextSwitch.Parse(Attribute(context, "lineEndContext"));
            var lineEmptyAttribute = Attribute(context, "lineEmptyContext");
            var lineEmptyContext = lineEmptyAttribute is null
                ? lineEndContext
                : SyntaxContextSwitch.Parse(lineEmptyAttribute);

            // Only a lineEmptyContext that ultimately stays falls back further to lineEndContext;
            // this mirrors Context::resolveContexts, which special-cases this to avoid skipping
            // empty lines after a line-continuation character.
            if (lineEmptyContext.IsStay)
            {
                lineEmptyContext = lineEndContext;
            }

            contexts.Add(
                new SyntaxContext(
                    name: name,
                    attributeName: attributeName,
                    lineEndContext: lineEndContext,
                    lineEmptyContext: lineEmptyContext,
                    fallthroughContext: SyntaxContextSwitch.Parse(Attribute(context, "fallthroughContext")),
                    noIndentationBasedFolding: ParseBool(Attribute(context, "noIndentationBasedFolding")),
                    stopEmptyLineContextSwitchLoop: ParseBool(Attribute(context, "stopEmptyLineContextSwitchLoop")),
                    rules: ReadRules(context, itemDataSet, name)));
        }

        return contexts;
    }

    private static List<SyntaxRule> ReadRules(XElement context, IReadOnlyDictionary<string, SyntaxItemData> itemDataSet, string contextName)
    {
        var rules = new List<SyntaxRule>();

        foreach (var element in context.Elements())
        {
            rules.Add(ReadRule(element, itemDataSet, contextName));
        }

        return rules;
    }

    private static SyntaxRule ReadRule(XElement element, IReadOnlyDictionary<string, SyntaxItemData> itemDataSet, string contextName)
    {
        var kind = element.Name.LocalName switch
        {
            "keyword" => SyntaxRuleKind.Keyword,
            "Float" => SyntaxRuleKind.Float,
            "HlCOct" => SyntaxRuleKind.Octal,
            "HlCHex" => SyntaxRuleKind.Hex,
            "Int" => SyntaxRuleKind.Integer,
            "DetectChar" => SyntaxRuleKind.Character,
            "Detect2Chars" => SyntaxRuleKind.TwoCharacter,
            "AnyChar" => SyntaxRuleKind.AnyCharacter,
            "StringDetect" => SyntaxRuleKind.StringMatch,
            "WordDetect" => SyntaxRuleKind.WordMatch,
            "RegExpr" => SyntaxRuleKind.RegularExpression,
            "LineContinue" => SyntaxRuleKind.LineContinuation,
            "HlCStringChar" => SyntaxRuleKind.EscapedCharacter,
            "RangeDetect" => SyntaxRuleKind.Range,
            "HlCChar" => SyntaxRuleKind.QuotedCharacter,
            "IncludeRules" => SyntaxRuleKind.IncludeRules,
            "DetectSpaces" => SyntaxRuleKind.DetectSpaces,
            "DetectIdentifier" => SyntaxRuleKind.DetectIdentifier,
            var other => throw new FormatException($"Unknown rule <{other}> in context '{contextName}'."),
        };

        var contextSwitch = SyntaxContextSwitch.Parse(Attribute(element, "context"));
        var lookAhead = ParseBool(Attribute(element, "lookAhead"));

        if (lookAhead && contextSwitch.IsStay)
        {
            throw new FormatException($"A lookAhead rule in context '{contextName}' must specify a context to switch to.");
        }

        // Upstream HighlightingContextData::load only reads (and therefore only validates) the
        // "attribute" value when lookAhead is false: a lookAhead match never produces a styled
        // token, so its own attribute is never consulted at all, and a dangling reference there
        // is not a real structural problem in the file. Resolving it unconditionally would reject
        // an otherwise valid third-party definition purely over dead data.
        var attributeName = lookAhead ? null : ResolveAttributeName(element, itemDataSet, contextName);

        var (attributeNameValue, contextSwitchValue, beginRegionValue, endRegionValue, lookAheadValue, firstNonSpaceValue, columnValue, weakDeliminatorValue, additionalDeliminatorValue) = (
            attributeName,
            contextSwitch,
            Attribute(element, "beginRegion"),
            Attribute(element, "endRegion"),
            lookAhead,
            ParseBool(Attribute(element, "firstNonSpace")),
            TryParseInt(Attribute(element, "column")),
            Attribute(element, "weakDeliminator") ?? string.Empty,
            Attribute(element, "additionalDeliminator") ?? string.Empty);

        return kind switch
        {
            SyntaxRuleKind.Keyword => new SyntaxRule(
                kind, attributeNameValue, contextSwitchValue, beginRegionValue, endRegionValue,
                lookAheadValue, firstNonSpaceValue, columnValue, weakDeliminatorValue, additionalDeliminatorValue,
                keywordListName: RequiredAttribute(element, "String"),
                keywordCaseSensitiveOverride: TryParseBool(Attribute(element, "insensitive")) is { } insensitive ? !insensitive : null),
            SyntaxRuleKind.Character => new SyntaxRule(
                kind, attributeNameValue, contextSwitchValue, beginRegionValue, endRegionValue,
                lookAheadValue, firstNonSpaceValue, columnValue, weakDeliminatorValue, additionalDeliminatorValue,
                char1: RequiredChar(element, "char"), dynamic: ParseBool(Attribute(element, "dynamic"))),
            SyntaxRuleKind.TwoCharacter => new SyntaxRule(
                kind, attributeNameValue, contextSwitchValue, beginRegionValue, endRegionValue,
                lookAheadValue, firstNonSpaceValue, columnValue, weakDeliminatorValue, additionalDeliminatorValue,
                char1: RequiredChar(element, "char"), char2: RequiredChar(element, "char1")),
            SyntaxRuleKind.AnyCharacter => new SyntaxRule(
                kind, attributeNameValue, contextSwitchValue, beginRegionValue, endRegionValue,
                lookAheadValue, firstNonSpaceValue, columnValue, weakDeliminatorValue, additionalDeliminatorValue,
                text: RequiredAttribute(element, "String")),
            SyntaxRuleKind.StringMatch => new SyntaxRule(
                kind, attributeNameValue, contextSwitchValue, beginRegionValue, endRegionValue,
                lookAheadValue, firstNonSpaceValue, columnValue, weakDeliminatorValue, additionalDeliminatorValue,
                text: RequiredAttribute(element, "String"), insensitive: ParseBool(Attribute(element, "insensitive")),
                dynamic: ParseBool(Attribute(element, "dynamic"))),
            SyntaxRuleKind.WordMatch => new SyntaxRule(
                kind, attributeNameValue, contextSwitchValue, beginRegionValue, endRegionValue,
                lookAheadValue, firstNonSpaceValue, columnValue, weakDeliminatorValue, additionalDeliminatorValue,
                text: RequiredAttribute(element, "String"), insensitive: ParseBool(Attribute(element, "insensitive"))),
            SyntaxRuleKind.RegularExpression => new SyntaxRule(
                kind, attributeNameValue, contextSwitchValue, beginRegionValue, endRegionValue,
                lookAheadValue, firstNonSpaceValue, columnValue, weakDeliminatorValue, additionalDeliminatorValue,
                text: RequiredAttribute(element, "String"), insensitive: ParseBool(Attribute(element, "insensitive")),
                dynamic: ParseBool(Attribute(element, "dynamic")), minimal: ParseBool(Attribute(element, "minimal"))),
            SyntaxRuleKind.LineContinuation => new SyntaxRule(
                kind, attributeNameValue, contextSwitchValue, beginRegionValue, endRegionValue,
                lookAheadValue, firstNonSpaceValue, columnValue, weakDeliminatorValue, additionalDeliminatorValue,
                char1: OptionalChar(element, "char") ?? '\\'),
            SyntaxRuleKind.Range => new SyntaxRule(
                kind, attributeNameValue, contextSwitchValue, beginRegionValue, endRegionValue,
                lookAheadValue, firstNonSpaceValue, columnValue, weakDeliminatorValue, additionalDeliminatorValue,
                char1: RequiredChar(element, "char"), char2: RequiredChar(element, "char1")),
            SyntaxRuleKind.IncludeRules => new SyntaxRule(
                kind, attributeNameValue, contextSwitchValue, beginRegionValue, endRegionValue,
                lookAheadValue, firstNonSpaceValue, columnValue, weakDeliminatorValue, additionalDeliminatorValue,
                includeContext: ParseContextReference(RequiredAttribute(element, "context")),
                includeAttribute: ParseBool(Attribute(element, "includeAttrib"))),

            // Float, Octal, Hex, Integer, EscapedCharacter, QuotedCharacter, DetectSpaces, and
            // DetectIdentifier carry no kind-specific parameters beyond the common attributes
            // already captured above.
            SyntaxRuleKind.Float or SyntaxRuleKind.Octal or SyntaxRuleKind.Hex or SyntaxRuleKind.Integer or
                SyntaxRuleKind.EscapedCharacter or SyntaxRuleKind.QuotedCharacter or
                SyntaxRuleKind.DetectSpaces or SyntaxRuleKind.DetectIdentifier => new SyntaxRule(
                    kind, attributeNameValue, contextSwitchValue, beginRegionValue, endRegionValue,
                    lookAheadValue, firstNonSpaceValue, columnValue, weakDeliminatorValue, additionalDeliminatorValue),

            _ => throw new UnreachableException(),
        };
    }

    private static SyntaxContextReference ParseContextReference(string value)
    {
        var separatorIndex = value.IndexOf("##", StringComparison.Ordinal);
        return separatorIndex < 0
            ? new SyntaxContextReference(value, null)
            : new SyntaxContextReference(value[..separatorIndex], value[(separatorIndex + 2)..]);
    }

    private static string? ResolveAttributeName(XElement element, IReadOnlyDictionary<string, SyntaxItemData> itemDataSet, string owner)
    {
        var name = Attribute(element, "attribute");

        return string.IsNullOrEmpty(name)
            ? null
            : !itemDataSet.ContainsKey(name)
                ? throw new FormatException($"'{owner}' references unknown item data '{name}'.")
                : name;
    }

    #endregion

    #region General options

    private static SyntaxGeneralOptions ReadGeneral(XElement? general, XElement language)
    {
        // Upstream DefinitionData::loadLanguage reads a "casesensitive" attribute directly on
        // <language> as the initial keyword case-sensitivity default; DefinitionData::loadGeneral
        // then only overrides that default when <general><keywords casesensitive="…"> is itself
        // present. Several embedded definitions (for example gtk-blueprint.xml) declare
        // casesensitive only on <language>, so resolving <keywords> in isolation and defaulting
        // straight to "true" would silently discard that declaration whenever <keywords> omits it.
        var languageCaseSensitiveDefault = ParseBool(Attribute(language, "casesensitive"), defaultValue: true);

        if (general is null)
        {
            return new SyntaxGeneralOptions(
                folding: new SyntaxFoldingOptions(indentationSensitive: false),
                comments: [],
                caseSensitiveKeywords: languageCaseSensitiveDefault,
                weakDeliminator: string.Empty,
                additionalDeliminator: string.Empty,
                emptyLineRules: []);
        }

        if (general.HasAttributes)
        {
            throw new FormatException("<general> does not accept attributes.");
        }

        var indentationSensitive = false;
        var caseSensitiveKeywords = languageCaseSensitiveDefault;
        var delimiters = SyntaxWordDelimiters.Default;
        var delimiterCandidates = new List<char>();
        var comments = new List<SyntaxCommentDefinition>();
        var emptyLineRules = new List<SyntaxEmptyLineRule>();

        foreach (var section in general.Elements())
        {
            switch (section.Name.LocalName)
            {
                case "folding":
                    indentationSensitive = ParseBool(Attribute(section, "indentationsensitive"));
                    break;
                case "keywords":
                    if (Attribute(section, "casesensitive") is { } rawCaseSensitive)
                    {
                        caseSensitiveKeywords = ParseBool(rawCaseSensitive);
                    }

                    var weakDeliminator = Attribute(section, "weakDeliminator") ?? string.Empty;
                    var additionalDeliminator = Attribute(section, "additionalDeliminator") ?? string.Empty;

                    // KDE applies each pair immediately, so a later addition can restore a
                    // delimiter weakened by an earlier section. Preserve that encounter order,
                    // then encode the final set as one public definition-level delta.
                    foreach (var candidate in additionalDeliminator.Concat(weakDeliminator))
                    {
                        if (!delimiterCandidates.Contains(candidate))
                        {
                            delimiterCandidates.Add(candidate);
                        }
                    }

                    delimiters = delimiters.With(additionalDeliminator, weakDeliminator);
                    break;
                case "comments":
                    foreach (var comment in section.Elements("comment"))
                    {
                        var kind = RequiredAttribute(comment, "name") switch
                        {
                            "singleLine" => SyntaxCommentKind.SingleLine,
                            "multiLine" => SyntaxCommentKind.MultiLine,
                            var other => throw new FormatException($"Unknown comment kind '{other}'."),
                        };

                        comments.Add(
                            new SyntaxCommentDefinition(
                                kind,
                                RequiredNonWhitespaceAttribute(comment, "start"),
                                kind == SyntaxCommentKind.MultiLine
                                    ? RequiredNonWhitespaceAttribute(comment, "end")
                                    : Attribute(comment, "end"),
                                Attribute(comment, "region"),
                                Attribute(comment, "position") == "afterwhitespace"));
                    }

                    break;
                case "emptyLines":
                    foreach (var emptyLine in section.Elements("emptyLine"))
                    {
                        emptyLineRules.Add(
                            new SyntaxEmptyLineRule(
                                RequiredAttribute(emptyLine, "regexpr"),
                                ParseBool(Attribute(emptyLine, "casesensitive"), defaultValue: true)));
                    }

                    break;
                case "spellchecking":
                    // Spellchecking metadata is part of the KDE schema but outside this
                    // syntax-tokenization package's public model.
                    break;
                default:
                    throw new FormatException($"Unknown <general> child <{section.Name.LocalName}>.");
            }
        }

        return new SyntaxGeneralOptions(
            folding: new SyntaxFoldingOptions(indentationSensitive),
            comments: comments,
            caseSensitiveKeywords: caseSensitiveKeywords,
            weakDeliminator: string.Concat(delimiterCandidates.Where(
                candidate => SyntaxWordDelimiters.Default.Contains(candidate) && !delimiters.Contains(candidate))),
            additionalDeliminator: string.Concat(delimiterCandidates.Where(
                candidate => !SyntaxWordDelimiters.Default.Contains(candidate) && delimiters.Contains(candidate))),
            emptyLineRules: emptyLineRules);
    }

    #endregion

    #region XML helpers

    private static void ValidateLanguageStructure(XElement language)
    {
        string[] allowedAttributes =
        [
            "name", "alternativeNames", "section", "extensions", "version", "kateversion",
            "style", "mimetype", "casesensitive", "priority", "author", "license", "indenter",
            "hidden", "generated",
        ];

        foreach (var attribute in language.Attributes().Where(static attribute => !attribute.IsNamespaceDeclaration))
        {
            if (attribute.Name is { LocalName: "noNamespaceSchemaLocation", NamespaceName: "http://www.w3.org/2001/XMLSchema-instance" })
            {
                continue;
            }

            if (attribute.Name.NamespaceName.Length != 0 ||
                !allowedAttributes.Contains(attribute.Name.LocalName, StringComparer.Ordinal))
            {
                throw new FormatException($"Unknown <language> attribute '{attribute.Name.LocalName}'.");
            }
        }

        var children = language.Elements().ToArray();

        if (children.Length is < 1 or > 2 ||
            children[0].Name.LocalName != "highlighting" ||
            (children.Length == 2 && children[1].Name.LocalName != "general"))
        {
            throw new FormatException("<language> must contain exactly one <highlighting> followed by at most one <general>.");
        }
    }

    private static void ValidateHighlightingStructure(XElement highlighting)
    {
        if (highlighting.HasAttributes)
        {
            throw new FormatException("<highlighting> does not accept attributes.");
        }

        var children = highlighting.Elements().ToArray();
        var index = 0;

        while (index < children.Length && children[index].Name.LocalName == "list")
        {
            index++;
        }

        if (index >= children.Length || children[index++].Name.LocalName != "contexts" ||
            index >= children.Length || children[index++].Name.LocalName != "itemDatas" ||
            index != children.Length)
        {
            throw new FormatException("<highlighting> must contain zero or more <list> elements, one <contexts>, then one <itemDatas>.");
        }
    }

    private static XElement RequiredChild(XElement parent, string name) =>
        parent.Element(name) ?? throw new FormatException($"'{parent.Name.LocalName}' is missing required child <{name}>.");

    private static string? Attribute(XElement? element, string name) => element?.Attribute(name)?.Value;

    private static string RequiredAttribute(XElement element, string name) =>
        Attribute(element, name) ?? throw new FormatException($"'{element.Name.LocalName}' is missing required attribute '{name}'.");

    private static string RequiredNonWhitespaceAttribute(XElement element, string name)
    {
        var value = RequiredAttribute(element, name);
        return !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new FormatException($"'{element.Name.LocalName}' attribute '{name}' must not be empty or whitespace.");
    }

    private static char RequiredChar(XElement element, string name)
    {
        var value = RequiredAttribute(element, name);
        return value.Length == 1
            ? value[0]
            : throw new FormatException($"'{element.Name.LocalName}' attribute '{name}' must be exactly one character.");
    }

    private static char? OptionalChar(XElement element, string name)
    {
        var value = Attribute(element, name);
        return value is null ? null : RequiredChar(element, name);
    }

    // Matches upstream Xml::attrToBool exactly: "1" or a case-insensitive "true" is true, and
    // every other value - including "0", "false", empty text, or outright garbage - is false.
    // Upstream never rejects a malformed xs:boolean attribute; it silently treats it as false, so
    // this reader must not fail an otherwise well-formed third-party definition merely because one
    // boolean attribute was capitalized differently or misspelled.
    private static bool ParseBool(string? value, bool defaultValue = false) =>
        value is null
            ? defaultValue
            : value == "1" || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

    private static bool? TryParseBool(string? value) => value is null ? null : ParseBool(value);

    private static int ParseRequiredInt(XElement element, string name)
    {
        var value = RequiredAttribute(element, name);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new FormatException($"'{element.Name.LocalName}' attribute '{name}' is not a valid integer: '{value}'.");
    }

    private static int ParseRequiredNonNegativeInt(XElement element, string name)
    {
        var value = ParseRequiredInt(element, name);
        return value >= 0
            ? value
            : throw new FormatException($"'{element.Name.LocalName}' attribute '{name}' must not be negative.");
    }

    private static int? TryParseInt(string? value) =>
        value is null
            ? null
            : int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : throw new FormatException($"'{value}' is not a valid integer.");

    internal static IReadOnlyList<string> SplitList(string? value) =>
        string.IsNullOrEmpty(value)
            ? []
            : value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    #endregion
}
