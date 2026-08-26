// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting;

/// <summary>
/// Owns one <see cref="SyntaxGrammar.Compile"/> call's shared state: the cache of grammars already
/// compiled or in progress, keyed by <see cref="SyntaxDefinition.Name"/>, so a diamond or cyclic
/// cross-definition reference compiles each definition at most once.
/// </summary>
internal sealed class SyntaxGrammarCompiler
{
    private readonly Func<string, SyntaxDefinition?>? _resolveDefinition;
    private readonly Dictionary<string, SyntaxGrammar> _grammars = new(StringComparer.Ordinal);

    /// <summary>
    /// The call-stack-scoped cycle guard shared across every grammar this session resolves,
    /// including ones reached indirectly through <see cref="Resolve"/>. A single shared set is
    /// required (rather than one per grammar) so a cycle that spans two different definitions -
    /// definition A including a context of B which in turn includes a context back in A - is
    /// still detected.
    /// </summary>
    private readonly HashSet<(SyntaxGrammar Grammar, int Index)> _resolving = [];

    /// <summary>Initializes a compilation session.</summary>
    /// <param name="resolveDefinition">The optional cross-definition lookup by name.</param>
    public SyntaxGrammarCompiler(Func<string, SyntaxDefinition?>? resolveDefinition) => _resolveDefinition = resolveDefinition;

    /// <summary>Compiles one definition, fully resolving every one of its contexts.</summary>
    /// <param name="definition">The non-null definition to compile.</param>
    /// <returns>The fully resolved grammar.</returns>
    public SyntaxGrammar Compile(SyntaxDefinition definition)
    {
        var grammar = GetOrCreateShell(definition, out var created);

        try
        {
            for (var i = 0; i < definition.Contexts.Count; i++)
            {
                _ = grammar.GetOrResolveContext(i, this, _resolving);
            }

            return grammar;
        }
        catch
        {
            if (created)
            {
                _ = _grammars.Remove(definition.Name);
            }

            throw;
        }
    }

    /// <summary>Attempts to retrieve a grammar already compiled or reached by this catalog-owned
    /// compiler session.</summary>
    /// <param name="definitionName">The exact definition name.</param>
    /// <param name="grammar">The existing grammar when found.</param>
    /// <returns>True when the session already owns the grammar.</returns>
    internal bool TryGetGrammar(string definitionName, out SyntaxGrammar grammar) =>
        _grammars.TryGetValue(definitionName, out grammar!);

    /// <summary>Resolves another definition by name and compiles it, reusing an in-progress grammar.</summary>
    /// <param name="definitionName">The non-null definition name.</param>
    /// <returns>The compiled grammar, or null when the name cannot be resolved.</returns>
    public SyntaxGrammar? Resolve(string definitionName)
    {
        if (_grammars.TryGetValue(definitionName, out var existing))
        {
            return existing;
        }

        var definition = _resolveDefinition?.Invoke(definitionName);
        return definition is null ? null : Compile(definition);
    }

    private SyntaxGrammar GetOrCreateShell(SyntaxDefinition definition, out bool created)
    {
        if (_grammars.TryGetValue(definition.Name, out var existing))
        {
            created = false;
            return existing;
        }

        var grammar = new SyntaxGrammar(definition);
        _grammars[definition.Name] = grammar;
        created = true;
        return grammar;
    }
}
