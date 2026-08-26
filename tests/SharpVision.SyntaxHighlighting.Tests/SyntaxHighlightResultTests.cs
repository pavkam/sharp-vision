// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting.Tests;

/// <summary>Verifies tokenization results retain immutable public collections.</summary>
public sealed class SyntaxHighlightResultTests
{
    /// <summary>Verifies document, line-token, and fold-range collections reject mutation.</summary>
    [Fact]
    public void Collections_WhenTokenized_RejectMutationAtEveryResultBoundary()
    {
        const string xml = """
            <language name="Result" section="Sources" extensions="*.result" version="1" kateversion="5.0">
              <highlighting>
                <contexts>
                  <context name="Normal" attribute="Normal Text" lineEndContext="#stay">
                    <StringDetect attribute="Normal Text" context="#stay" String="open" beginRegion="R"/>
                    <StringDetect attribute="Normal Text" context="#stay" String="close" endRegion="R"/>
                  </context>
                </contexts>
                <itemDatas><itemData name="Normal Text" defStyleNum="dsNormal"/></itemDatas>
              </highlighting>
            </language>
            """;
        var grammar = SyntaxGrammar.Compile(SyntaxDefinitionReader.Read(xml));
        var result = SyntaxTokenizer.Tokenize(grammar, "open\nclose");

        ShouldRejectMutation(result.Lines);
        ShouldRejectMutation(result.Lines[0].Tokens);
        ShouldRejectMutation(result.FoldRanges);
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
