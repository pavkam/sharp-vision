// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting.Tests;

/// <summary>Verifies compiled grammar collections are immutable.</summary>
public sealed class SyntaxGrammarTests
{
    /// <summary>Verifies compiled contexts, rules, resolved pushes, and captures reject mutation.</summary>
    [Fact]
    public void Collections_WhenCompiled_RejectMutationAtEveryGrammarBoundary()
    {
        const string xml = """
            <language name="Grammar" section="Sources" extensions="*.grammar" version="1" kateversion="5.0">
              <highlighting>
                <contexts>
                  <context name="Normal" attribute="Normal Text" lineEndContext="#stay">
                    <RegExpr attribute="Normal Text" context="Other" String="(x)"/>
                  </context>
                  <context name="Other" attribute="Normal Text" lineEndContext="#stay"/>
                </contexts>
                <itemDatas><itemData name="Normal Text" defStyleNum="dsNormal"/></itemDatas>
              </highlighting>
            </language>
            """;
        var grammar = SyntaxGrammar.Compile(SyntaxDefinitionReader.Read(xml));
        var rule = grammar.Contexts[0].Rules.ShouldHaveSingleItem();
        var match = rule.TryMatch("x", 0, []);

        ShouldRejectMutation(grammar.Contexts);
        ShouldRejectMutation(grammar.Contexts[0].Rules);
        ShouldRejectMutation(rule.ResolvedTarget.Pushes);
        ShouldRejectMutation(match.Captures);
    }

    private static void ShouldRejectMutation<T>(IReadOnlyList<T> values)
    {
        if (values is IList<T> mutable)
        {
            _ = Should.Throw<NotSupportedException>(mutable.Clear);
        }

        values.ShouldNotBeOfType<List<T>>();
        values.ShouldNotBeOfType<T[]>();
    }
}
