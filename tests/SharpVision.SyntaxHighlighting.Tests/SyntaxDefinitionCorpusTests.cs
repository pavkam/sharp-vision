// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting.Tests;

/// <summary>
/// Exercises every embedded definition end to end: parsing, grammar compilation (including
/// cross-definition references that resolve to an excluded, non-embedded language, such as the
/// many definitions that embed C, C++, or JavaScript), and tokenizing a small representative
/// snippet, all without throwing.
/// </summary>
public sealed class SyntaxDefinitionCorpusTests
{
    /// <summary>Freezes the explicitly supported partial-definition inventory so embedded
    /// dependency loss cannot grow or change without a reviewed catalog decision.</summary>
    [Fact]
    public void Default_WhenDependenciesAreUnavailable_MatchesPublishedPartialSupportInventory()
    {
        var catalog = SyntaxDefinitionCatalog.Default;
        var diagnostics = catalog.Names
            .SelectMany(name => catalog.GetGrammar(name).Diagnostics)
            .Where(static diagnostic => diagnostic.Kind == SyntaxGrammarDiagnosticKind.MissingDefinition)
            .ToArray();
        var affected = diagnostics.Select(static diagnostic => diagnostic.SourceDefinition).Distinct().Order().ToArray();

        diagnostics.Length.ShouldBe(192);
        affected.ShouldBe(
        [
            "Cabal", "COBOL", "CoffeeScript", "D2", "Dockerfile", "Earthfile", "Elixir/EEx",
            "Elixir/HEEx", "Elvish", "Expect", "InnoSetup", "Jam", "Java Module",
            "JavaScript React (JSX)", "Mermaid", "Mustache/Handlebars (HTML)", "OORS",
            "Org Mode", "PIO Assembler", "PureScript", "QML", "R documentation", "Raku",
            "RenPy", "RPM Spec", "SAS", "SASS", "SubRip Subtitles", "TypeScript",
            "TypeScript React (TSX)", "Web Video Text Tracks", "XHTML", "YARA", "Zsh",
        ]);
    }

    /// <summary>Gets every embedded definition's name as a theory data source.</summary>
    public static TheoryData<string> AllEmbeddedNames() =>
        [.. SyntaxDefinitionCatalog.Default.Names];

    /// <summary>Verifies one embedded definition compiles and tokenizes a representative snippet without throwing.</summary>
    [Theory]
    [MemberData(nameof(AllEmbeddedNames))]
    public void GetGrammar_ForEveryEmbeddedDefinition_CompilesAndTokenizesWithoutThrowing(string name)
    {
        var catalog = SyntaxDefinitionCatalog.Default;
        var grammar = catalog.GetGrammar(name);

        grammar.Contexts.ShouldNotBeEmpty();

        foreach (var context in grammar.Contexts)
        {
            foreach (var rule in context.Rules.Where(static rule => rule.Source.Kind == SyntaxRuleKind.RegularExpression))
            {
                rule.RegularExpressionIsValid.ShouldBeTrue(
                    $"Embedded language '{name}', context '{context.Name}', has an invalid PCRE2 pattern: {rule.Source.Text}");
            }
        }

        var sample = string.Join('\n', ["// a comment", "identifier 123 \"a string\" 0x1F", "{ }", string.Empty]);
        var result = SyntaxTokenizer.Tokenize(grammar, sample);

        result.Lines.Count.ShouldBe(4);
    }
}
