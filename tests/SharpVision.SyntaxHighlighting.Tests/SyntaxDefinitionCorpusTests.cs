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

        var sample = string.Join('\n', ["// a comment", "identifier 123 \"a string\" 0x1F", "{ }", string.Empty]);
        var result = SyntaxTokenizer.Tokenize(grammar, sample);

        result.Lines.Count.ShouldBe(4);
    }
}
