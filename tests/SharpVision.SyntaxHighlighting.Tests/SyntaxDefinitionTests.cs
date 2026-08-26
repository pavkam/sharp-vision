// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting.Tests;

/// <summary>Verifies parsed syntax definitions expose immutable model collections.</summary>
public sealed class SyntaxDefinitionTests
{
    /// <summary>Verifies every collection reachable from a parsed definition rejects mutation,
    /// including empty collections whose current contents cannot reveal a successful clear.</summary>
    [Fact]
    public void Collections_WhenParsed_RejectMutationAtEveryModelBoundary()
    {
        const string xml = """
            <language name="Immutable" alternativeNames="Alias" section="Sources" extensions="*.immutable" mimetype="text/x-immutable" version="1" kateversion="5.0">
              <highlighting>
                <list name="words"><item>alpha</item><include>words##Other</include></list>
                <contexts>
                  <context name="Normal" attribute="Normal Text" lineEndContext="Other">
                    <keyword attribute="Keyword" context="#stay" String="words"/>
                  </context>
                  <context name="Other" attribute="Normal Text" lineEndContext="#stay"/>
                </contexts>
                <itemDatas>
                  <itemData name="Normal Text" defStyleNum="dsNormal"/>
                  <itemData name="Keyword" defStyleNum="dsKeyword"/>
                </itemDatas>
              </highlighting>
            </language>
            """;
        var definition = SyntaxDefinitionReader.Read(xml);

        ShouldRejectMutation(definition.AlternativeNames);
        ShouldRejectMutation(definition.Extensions);
        ShouldRejectMutation(definition.MimeTypes);
        ShouldRejectMutation(definition.Contexts);
        ShouldRejectMutation(definition.Contexts[0].Rules);
        ShouldRejectMutation(definition.Contexts[0].LineEndContext.Targets);
        ShouldRejectMutation(definition.General.Comments);
        ShouldRejectMutation(definition.General.EmptyLineRules);
        ShouldRejectMutation(definition.KeywordLists["words"].Words);
        ShouldRejectMutation(definition.KeywordLists["words"].CrossDefinitionIncludes);
        ShouldRejectMutation(definition.KeywordLists);
        ShouldRejectMutation(definition.ItemDataSet);
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

    private static void ShouldRejectMutation<TKey, TValue>(IReadOnlyDictionary<TKey, TValue> values)
        where TKey : notnull
    {
        if (values is IDictionary<TKey, TValue> mutable)
        {
            _ = Should.Throw<NotSupportedException>(mutable.Clear);
        }

        values.ShouldNotBeOfType<Dictionary<TKey, TValue>>();
    }
}
