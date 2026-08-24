// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting.Tests;

/// <summary>Verifies <see cref="SyntaxCompiledRule"/>'s argument validation and match algorithms.</summary>
public sealed class SyntaxCompiledRuleTests
{
    private const string _integerLanguage = """
        <language name="Probe" section="Sources" extensions="*.p" version="1" kateversion="5.0">
          <highlighting>
            <contexts>
              <context name="Normal" attribute="Normal Text" lineEndContext="#stay">
                <Int attribute="Number" context="#stay"/>
              </context>
            </contexts>
            <itemDatas>
              <itemData name="Normal Text" defStyleNum="dsNormal"/>
              <itemData name="Number" defStyleNum="dsDecVal"/>
            </itemDatas>
          </highlighting>
        </language>
        """;

    private static SyntaxCompiledRule IntegerRule() =>
        SyntaxGrammar.Compile(SyntaxDefinitionReader.Read(_integerLanguage)).Contexts[0].Rules[0];

    /// <summary>Verifies a null line throws <see cref="ArgumentNullException"/> rather than crashing deeper.</summary>
    [Fact]
    public void TryMatch_WhenLineIsNull_ThrowsArgumentNullException() =>
        _ = Should.Throw<ArgumentNullException>(() => IntegerRule().TryMatch(null!, 0, []));

    /// <summary>Verifies null captures throws <see cref="ArgumentNullException"/>.</summary>
    [Fact]
    public void TryMatch_WhenCapturesIsNull_ThrowsArgumentNullException() =>
        _ = Should.Throw<ArgumentNullException>(() => IntegerRule().TryMatch("1", 0, null!));

    /// <summary>
    /// Verifies a negative offset throws the documented <see cref="ArgumentOutOfRangeException"/>
    /// instead of an undocumented <see cref="IndexOutOfRangeException"/> from deep inside a
    /// matcher's own character indexing.
    /// </summary>
    [Fact]
    public void TryMatch_WhenOffsetIsNegative_ThrowsArgumentOutOfRangeException() =>
        _ = Should.Throw<ArgumentOutOfRangeException>(() => IntegerRule().TryMatch("abc", -1, []));

    /// <summary>
    /// Verifies an offset past the end of the line throws the documented
    /// <see cref="ArgumentOutOfRangeException"/> instead of an undocumented
    /// <see cref="IndexOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void TryMatch_WhenOffsetExceedsLineLength_ThrowsArgumentOutOfRangeException() =>
        _ = Should.Throw<ArgumentOutOfRangeException>(() => IntegerRule().TryMatch("abc", 10, []));

    /// <summary>Verifies an offset exactly at the end of the line is a valid, non-throwing "no match".</summary>
    [Fact]
    public void TryMatch_WhenOffsetEqualsLineLength_ReturnsNoMatchWithoutThrowing()
    {
        var result = IntegerRule().TryMatch("abc", 3, []);

        result.Success.ShouldBeFalse();
    }

    private const string _capturingGroupLanguage = """
        <language name="Captures" section="Sources" extensions="*.c" version="1" kateversion="5.0">
          <highlighting>
            <contexts>
              <context name="Normal" attribute="Normal Text" lineEndContext="#stay">
                <RegExpr attribute="Normal Text" context="Body" String="(\w+)=(\w+);(\w+)" dynamic="false"/>
              </context>
              <context name="Body" attribute="Normal Text" lineEndContext="#stay"/>
            </contexts>
            <itemDatas>
              <itemData name="Normal Text" defStyleNum="dsNormal"/>
            </itemDatas>
          </highlighting>
        </language>
        """;

    /// <summary>
    /// Verifies a <c>RegExpr</c> match's captured groups are exposed 1-based, in left-to-right
    /// declaration order, ignoring the implicit whole-match group 0 - the numbering a
    /// <c>dynamic="true"</c> rule's <c>%1</c>-<c>%9</c> placeholders in the pushed context rely on.
    /// </summary>
    [Fact]
    public void TryMatch_WhenRegularExpressionHasThreeCapturingGroups_ExposesThemInOrderStartingAtOne()
    {
        var grammar = SyntaxGrammar.Compile(SyntaxDefinitionReader.Read(_capturingGroupLanguage));
        var rule = grammar.Contexts[0].Rules[0];

        var match = rule.TryMatch("key=value;tail", 0, []);

        match.Success.ShouldBeTrue();
        match.Captures.Count.ShouldBe(3);
        match.Captures[0].ShouldBe("key");
        match.Captures[1].ShouldBe("value");
        match.Captures[2].ShouldBe("tail");
    }

    private const string _manyGroupsLanguage = """
        <language name="ManyGroups" section="Sources" extensions="*.m" version="1" kateversion="5.0">
          <highlighting>
            <contexts>
              <context name="Normal" attribute="Normal Text" lineEndContext="#stay">
                <RegExpr attribute="Normal Text" context="#stay" String="(a)(b)(c)(d)(e)(f)(g)(h)(i)(j)"/>
              </context>
            </contexts>
            <itemDatas>
              <itemData name="Normal Text" defStyleNum="dsNormal"/>
            </itemDatas>
          </highlighting>
        </language>
        """;

    /// <summary>
    /// Verifies a pattern with more than nine capturing groups still only exposes the first nine,
    /// matching the KDE format's own <c>%1</c>-<c>%9</c> ceiling.
    /// </summary>
    [Fact]
    public void TryMatch_WhenRegularExpressionHasMoreThanNineGroups_CapsCapturesAtNine()
    {
        var grammar = SyntaxGrammar.Compile(SyntaxDefinitionReader.Read(_manyGroupsLanguage));
        var rule = grammar.Contexts[0].Rules[0];

        var match = rule.TryMatch("abcdefghij", 0, []);

        match.Success.ShouldBeTrue();
        match.Captures.Count.ShouldBe(9);
        match.Captures[8].ShouldBe("i");
    }

    private const string _invalidPatternLanguage = """
        <language name="Invalid" section="Sources" extensions="*.i" version="1" kateversion="5.0">
          <highlighting>
            <contexts>
              <context name="Normal" attribute="Normal Text" lineEndContext="#stay">
                <RegExpr attribute="Normal Text" context="#stay" String="(unterminated"/>
              </context>
            </contexts>
            <itemDatas>
              <itemData name="Normal Text" defStyleNum="dsNormal"/>
            </itemDatas>
          </highlighting>
        </language>
        """;

    /// <summary>
    /// Verifies an unparsable <c>RegExpr</c> pattern - plausible in a hand-authored or third-party
    /// definition - degrades to a rule that compiles successfully but never matches, rather than
    /// throwing out of grammar compilation or out of every subsequent match attempt.
    /// </summary>
    [Fact]
    public void TryMatch_WhenPatternIsUnparsable_NeverMatchesWithoutThrowing()
    {
        var grammar = SyntaxGrammar.Compile(SyntaxDefinitionReader.Read(_invalidPatternLanguage));
        var rule = grammar.Contexts[0].Rules[0];

        var match = rule.TryMatch("anything", 0, []);

        match.Success.ShouldBeFalse();
    }

    private const string _catastrophicPatternLanguage = """
        <language name="Catastrophic" section="Sources" extensions="*.c" version="1" kateversion="5.0">
          <highlighting>
            <contexts>
              <context name="Normal" attribute="Normal Text" lineEndContext="#stay">
                <RegExpr attribute="Normal Text" context="#stay" String="(a+)+b"/>
              </context>
            </contexts>
            <itemDatas>
              <itemData name="Normal Text" defStyleNum="dsNormal"/>
            </itemDatas>
          </highlighting>
        </language>
        """;

    /// <summary>
    /// Verifies a pattern prone to catastrophic backtracking - the classic <c>(a+)+b</c> shape,
    /// evaluated against adversarial input with no trailing <c>b</c> - degrades to a timed-out
    /// non-match instead of blocking the calling thread indefinitely. This deliberately runs the
    /// match engine long enough for its bounded <c>matchTimeout</c> to actually fire.
    /// </summary>
    [Fact]
    public void TryMatch_WhenPatternCausesCatastrophicBacktracking_TimesOutToANonMatch()
    {
        var grammar = SyntaxGrammar.Compile(SyntaxDefinitionReader.Read(_catastrophicPatternLanguage));
        var rule = grammar.Contexts[0].Rules[0];
        var adversarialInput = new string('a', 40);

        var match = rule.TryMatch(adversarialInput, 0, []);

        match.Success.ShouldBeFalse();
    }
}
