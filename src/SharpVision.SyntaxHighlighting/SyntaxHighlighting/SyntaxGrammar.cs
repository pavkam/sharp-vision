// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting;

using MustUseReturnValue = JetBrains.Annotations.MustUseReturnValueAttribute;

/// <summary>
/// Represents one <see cref="SyntaxDefinition"/> after grammar compilation: every context's
/// <c>IncludeRules</c> spliced away, every context-switch target resolved to a concrete context,
/// and every rule's word-delimiter set, keyword matcher, and regular expression built.
/// </summary>
[PublicAPI]
public sealed class SyntaxGrammar
{
    private readonly SyntaxGrammarContext?[] _contexts;
    private readonly Dictionary<string, int> _indexByName;
    private readonly SyntaxWordDelimiters _baseDelimiters;

    /// <summary>Initializes the shell for one definition's compiled grammar.</summary>
    /// <param name="definition">The non-null source definition.</param>
    internal SyntaxGrammar(SyntaxDefinition definition)
    {
        Definition = definition;
        _contexts = new SyntaxGrammarContext?[definition.Contexts.Count];
        _indexByName = new Dictionary<string, int>(StringComparer.Ordinal);
        _baseDelimiters = SyntaxWordDelimiters.Default.With(definition.General.AdditionalDeliminator, definition.General.WeakDeliminator);

        for (var i = 0; i < definition.Contexts.Count; i++)
        {
            _ = _indexByName.TryAdd(definition.Contexts[i].Name, i);
        }
    }

    /// <summary>Gets the source definition this grammar compiles.</summary>
    public SyntaxDefinition Definition { get; }

    /// <summary>
    /// Gets the compiled contexts, in the source definition's declaration order;
    /// <c>Contexts[0]</c> is the start context a fresh tokenizer session begins in.
    /// </summary>
    public IReadOnlyList<SyntaxGrammarContext> Contexts
    {
        get
        {
            Debug.Assert(
                Array.TrueForAll(_contexts, context => context is not null),
                "SyntaxGrammarCompiler.Compile always resolves every one of a definition's " +
                "contexts - including every other grammar reached transitively through a " +
                "cross-definition reference - before returning, so no element is ever null here.");

            return _contexts!;
        }
    }

    /// <summary>Compiles a definition into a grammar, resolving cross-definition references eagerly.</summary>
    /// <param name="definition">The non-null definition to compile.</param>
    /// <param name="resolveDefinition">
    /// An optional lookup from another definition's <see cref="SyntaxDefinition.Name"/> to that
    /// definition, consulted for a cross-definition <c>IncludeRules</c> or context switch such as
    /// <c>Normal##JavaScript</c>. When null, or when it returns null for a requested name, that
    /// reference resolves to nothing rather than failing the whole compilation, the same graceful
    /// degradation <see cref="SyntaxContextTarget"/> documents.
    /// </param>
    /// <returns>The fully compiled, immutable grammar.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> is null.</exception>
    [MustUseReturnValue]
    public static SyntaxGrammar Compile(SyntaxDefinition definition, Func<string, SyntaxDefinition?>? resolveDefinition = null)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var compiler = new SyntaxGrammarCompiler(resolveDefinition);
        return compiler.Compile(definition);
    }

    /// <summary>Attempts to find one context's index by name.</summary>
    /// <param name="name">The non-null context name.</param>
    /// <param name="index">The resolved index, when found.</param>
    /// <returns>True when a context with that name exists.</returns>
    internal bool TryGetContextIndex(string name, out int index) => _indexByName.TryGetValue(name, out index);

    /// <summary>
    /// Resolves and caches one context by index, recursively resolving any context it includes or
    /// switches to along the way.
    /// </summary>
    /// <param name="index">The context index within this grammar.</param>
    /// <param name="compiler">The shared compilation session, for cross-definition resolution.</param>
    /// <param name="resolving">The call-stack-scoped cycle guard shared across this compilation.</param>
    /// <returns>The resolved context.</returns>
    internal SyntaxGrammarContext GetOrResolveContext(int index, SyntaxGrammarCompiler compiler, HashSet<(SyntaxGrammar Grammar, int Index)> resolving)
    {
        if (_contexts[index] is { } resolved)
        {
            return resolved;
        }

        var key = (this, index);

        if (!resolving.Add(key))
        {
            // A cyclic IncludeRules/context-switch chain: contribute nothing for this occurrence,
            // matching upstream's "Cyclic dependency!" warning instead of recursing forever.
            return SyntaxGrammarContext.Empty(Definition.Contexts[index].Name);
        }

        var source = Definition.Contexts[index];
        var attributeStyle = ResolveStyle(source.AttributeName);
        var rules = new List<SyntaxCompiledRule>();

        foreach (var rule in source.Rules)
        {
            if (rule.Kind == SyntaxRuleKind.IncludeRules)
            {
                Debug.Assert(rule.IncludeContext is not null, "The reader requires an IncludeRules rule's context attribute.");
                var target = ResolveReference(rule.IncludeContext.Value, compiler, resolving);

                if (target is not null)
                {
                    rules.AddRange(target.Rules);

                    if (rule.IncludeAttribute)
                    {
                        attributeStyle = target.AttributeStyle;
                    }
                }

                continue;
            }

            var compiled = CompileRule(rule, compiler);

            if (compiled is not null)
            {
                rules.Add(compiled);
            }
        }

        var context = new SyntaxGrammarContext(
            source.Name,
            attributeStyle,
            rules,
            lineEndTarget: ResolveTarget(source.LineEndContext, compiler),
            lineEmptyTarget: ResolveTarget(source.LineEmptyContext, compiler),
            fallthroughTarget: ResolveTarget(source.FallthroughContext, compiler),
            indentationBasedFoldingEnabled: !source.NoIndentationBasedFolding && Definition.General.Folding.IndentationSensitive,
            stopEmptyLineContextSwitchLoop: source.StopEmptyLineContextSwitchLoop);

        _ = resolving.Remove(key);
        _contexts[index] = context;
        return context;
    }

    private SyntaxDefaultStyle ResolveStyle(string? attributeName) =>
        attributeName is not null && Definition.ItemDataSet.TryGetValue(attributeName, out var itemData)
            ? itemData.DefaultStyle
            : SyntaxDefaultStyle.Normal;

    private SyntaxDefaultStyle? ResolveOptionalStyle(string? attributeName) =>
        attributeName is not null && Definition.ItemDataSet.TryGetValue(attributeName, out var itemData)
            ? itemData.DefaultStyle
            : null;

    private SyntaxContextTargetEntry? ResolveReferenceEntry(SyntaxContextReference reference, SyntaxGrammarCompiler compiler)
    {
        var grammar = reference.DefinitionName is null ? this : compiler.Resolve(reference.DefinitionName);

        if (grammar is null)
        {
            return null;
        }

        var contextName = reference.ContextName.Length == 0 ? grammar.Definition.Contexts[0].Name : reference.ContextName;

        return grammar.TryGetContextIndex(contextName, out var index)
            ? new SyntaxContextTargetEntry(grammar, index)
            : null;
    }

    private SyntaxGrammarContext? ResolveReference(SyntaxContextReference reference, SyntaxGrammarCompiler compiler, HashSet<(SyntaxGrammar Grammar, int Index)> resolving)
    {
        var entry = ResolveReferenceEntry(reference, compiler);
        return entry is { } value ? value.Grammar.GetOrResolveContext(value.ContextIndex, compiler, resolving) : null;
    }

    /// <summary>
    /// Compiles one non-<c>IncludeRules</c> rule, or returns null when a
    /// <see cref="SyntaxRuleKind.Keyword"/> rule names a keyword list this definition does not
    /// declare. Upstream KSyntaxHighlighting's own <c>KeywordListRule::create</c> drops such a
    /// rule from its context entirely (a warning, not a load failure) rather than rejecting the
    /// whole definition, since a stale or externally resolved list name in one rule must not
    /// break every other rule in the file.
    /// </summary>
    private SyntaxCompiledRule? CompileRule(SyntaxRule rule, SyntaxGrammarCompiler compiler)
    {
        var target = ResolveTarget(rule.ContextSwitch, compiler);
        var style = ResolveOptionalStyle(rule.AttributeName);
        var delimiters = _baseDelimiters.With(rule.AdditionalDeliminator, rule.WeakDeliminator);

        SyntaxKeywordMatcher? keywordMatcher = null;

        if (rule.Kind == SyntaxRuleKind.Keyword)
        {
            Debug.Assert(rule.KeywordListName is not null, "The reader requires a Keyword rule's String attribute.");
            var listName = rule.KeywordListName ?? string.Empty;

            if (!Definition.KeywordLists.TryGetValue(listName, out var list))
            {
                return null;
            }

            var words = list.CrossDefinitionIncludes.Count == 0
                ? list.Words
                : ResolveKeywordWords(list, compiler, [$"{Definition.Name}\0{listName}"]);
            var caseSensitive = rule.KeywordCaseSensitiveOverride ?? Definition.General.CaseSensitiveKeywords;
            keywordMatcher = new SyntaxKeywordMatcher(words, caseSensitive, delimiters);
        }

        return new SyntaxCompiledRule(rule, style, target, delimiters, keywordMatcher);
    }

    /// <summary>
    /// Resolves a keyword list's own words together with every word reachable through its
    /// <see cref="SyntaxKeywordList.CrossDefinitionIncludes"/>, recursively, matching upstream
    /// <c>KeywordList::resolveIncludeKeywords</c>. An include naming a definition or list this
    /// compilation cannot resolve, or one that would revisit an already-visited list, is skipped
    /// rather than rejected.
    /// </summary>
    private static List<string> ResolveKeywordWords(SyntaxKeywordList list, SyntaxGrammarCompiler compiler, HashSet<string> visited)
    {
        var words = new List<string>(list.Words);

        foreach (var raw in list.CrossDefinitionIncludes)
        {
            var separatorIndex = raw.IndexOf("##", StringComparison.Ordinal);

            if (separatorIndex < 0)
            {
                continue;
            }

            var listName = raw[..separatorIndex];
            var definitionName = raw[(separatorIndex + 2)..];

            if (!visited.Add($"{definitionName}\0{listName}"))
            {
                continue;
            }

            var targetGrammar = compiler.Resolve(definitionName);

            if (targetGrammar is not null && targetGrammar.Definition.KeywordLists.TryGetValue(listName, out var targetList))
            {
                words.AddRange(ResolveKeywordWords(targetList, compiler, visited));
            }
        }

        return words;
    }

    private SyntaxContextTarget ResolveTarget(SyntaxContextSwitch source, SyntaxGrammarCompiler compiler)
    {
        if (source.IsStay)
        {
            return SyntaxContextTarget.Stay;
        }

        var pushes = new List<SyntaxContextTargetEntry>();

        foreach (var reference in source.Targets)
        {
            if (ResolveReferenceEntry(reference, compiler) is { } entry)
            {
                pushes.Add(entry);
            }
        }

        return new SyntaxContextTarget(source.PopCount, pushes);
    }
}
