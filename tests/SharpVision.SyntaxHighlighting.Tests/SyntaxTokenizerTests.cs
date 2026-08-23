// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting.Tests;

/// <summary>Verifies the context-stack tokenization algorithm: matching, folding, dynamic captures, and fallthrough.</summary>
public sealed class SyntaxTokenizerTests
{
    private const string _miniLanguage = """
        <language name="Mini" section="Sources" extensions="*.mini" version="1" kateversion="5.0">
          <highlighting>
            <list name="keywords">
              <item>if</item>
              <item>else</item>
            </list>
            <contexts>
              <context name="Normal" attribute="Normal Text" lineEndContext="#stay">
                <keyword attribute="Keyword" context="#stay" String="keywords"/>
                <DetectChar attribute="String" context="String" char="&quot;" beginRegion="String"/>
                <RegExpr attribute="Comment" context="#stay" String="//.*$"/>
                <DetectChar attribute="Normal Text" context="Braced" char="{" beginRegion="Brace"/>
                <DetectChar attribute="Normal Text" context="#pop" char="}" endRegion="Brace"/>
              </context>
              <context name="Braced" attribute="Normal Text" lineEndContext="#stay">
                <keyword attribute="Keyword" context="#stay" String="keywords"/>
                <DetectChar attribute="Normal Text" context="#pop" char="}" endRegion="Brace"/>
              </context>
              <context name="String" attribute="String" lineEndContext="#stay">
                <HlCStringChar attribute="SpecialChar" context="#stay"/>
                <DetectChar attribute="String" context="#pop" char="&quot;" endRegion="String"/>
              </context>
            </contexts>
            <itemDatas>
              <itemData name="Normal Text" defStyleNum="dsNormal"/>
              <itemData name="Keyword" defStyleNum="dsKeyword"/>
              <itemData name="String" defStyleNum="dsString"/>
              <itemData name="SpecialChar" defStyleNum="dsSpecialChar"/>
              <itemData name="Comment" defStyleNum="dsComment"/>
            </itemDatas>
          </highlighting>
        </language>
        """;

    private static SyntaxGrammar CompileMini() => SyntaxGrammar.Compile(SyntaxDefinitionReader.Read(_miniLanguage));

    /// <summary>Verifies a null grammar is rejected with ArgumentNullException.</summary>
    [Fact]
    public void Tokenize_WhenGrammarIsNull_ThrowsArgumentNullException() => _ = Should.Throw<ArgumentNullException>(() => SyntaxTokenizer.Tokenize(null!, "x"));

    /// <summary>Verifies null text is rejected with ArgumentNullException.</summary>
    [Fact]
    public void Tokenize_WhenTextIsNull_ThrowsArgumentNullException() => _ = Should.Throw<ArgumentNullException>(() => SyntaxTokenizer.Tokenize(CompileMini(), null!));

    /// <summary>Verifies a line containing a keyword styles it as Keyword.</summary>
    [Fact]
    public void Tokenize_WhenLineContainsKeyword_StylesItAsKeyword()
    {
        var result = SyntaxTokenizer.Tokenize(CompileMini(), "if x");

        var tokens = result.Lines[0].Tokens;
        tokens[0].Start.ShouldBe(0);
        tokens[0].Length.ShouldBe(2);
        tokens[0].Style.ShouldBe(SyntaxDefaultStyle.Keyword);
    }

    /// <summary>Verifies unmatched text falls back to the owning context's own attribute style.</summary>
    [Fact]
    public void Tokenize_WhenLineHasNoMatchingRule_UsesContextAttributeForUnmatchedText()
    {
        var result = SyntaxTokenizer.Tokenize(CompileMini(), "x");

        var tokens = result.Lines[0].Tokens;
        _ = tokens.ShouldHaveSingleItem();
        tokens[0].Style.ShouldBe(SyntaxDefaultStyle.Normal);
    }

    /// <summary>Verifies an escaped quote inside a string literal does not end the string context.</summary>
    [Fact]
    public void Tokenize_WhenStringContainsEscapedQuote_StaysInsideStringContext()
    {
        var result = SyntaxTokenizer.Tokenize(CompileMini(), "\"a\\\"b\"");

        var tokens = result.Lines[0].Tokens;

        // The whole run from the opening quote through the closing quote is String/SpecialChar,
        // never falling back to Normal Text in between.
        tokens.ShouldAllBe(token => token.Style == SyntaxDefaultStyle.String || token.Style == SyntaxDefaultStyle.SpecialChar);
        (tokens[^1].Start + tokens[^1].Length).ShouldBe(6);
    }

    /// <summary>Verifies an unterminated string carries its context across a line boundary.</summary>
    [Fact]
    public void Tokenize_WhenLineEndsInsideString_CarriesContextToNextLine()
    {
        var result = SyntaxTokenizer.Tokenize(CompileMini(), "\"unterminated\nstill inside\"");

        result.Lines[1].Tokens.ShouldAllBe(token => token.Style == SyntaxDefaultStyle.String);
    }

    /// <summary>Verifies a region that begins and ends on one line still produces a fold range.</summary>
    [Fact]
    public void Tokenize_WhenRegionBeginsAndEndsOnSameLine_ProducesSingleLineFoldRange()
    {
        var result = SyntaxTokenizer.Tokenize(CompileMini(), "\"hi\"");

        var range = result.FoldRanges.ShouldHaveSingleItem();
        range.StartLine.ShouldBe(0);
        range.EndLine.ShouldBe(0);
        range.Kind.ShouldBe(SyntaxFoldRangeKind.Region);
    }

    /// <summary>Verifies a region spanning multiple lines produces a matching fold range.</summary>
    [Fact]
    public void Tokenize_WhenRegionSpansMultipleLines_ProducesMultiLineFoldRange()
    {
        var result = SyntaxTokenizer.Tokenize(CompileMini(), "if {\nelse\n}");

        var range = result.FoldRanges.ShouldHaveSingleItem();
        range.StartLine.ShouldBe(0);
        range.EndLine.ShouldBe(2);
    }

    /// <summary>Verifies an unterminated region folds through the end of the document.</summary>
    [Fact]
    public void Tokenize_WhenBeginRegionIsNeverClosed_FoldsToTheEndOfTheDocument()
    {
        var result = SyntaxTokenizer.Tokenize(CompileMini(), "if {\nelse");

        var range = result.FoldRanges.ShouldHaveSingleItem();
        range.StartLine.ShouldBe(0);
        range.EndLine.ShouldBe(1);
    }

    /// <summary>Verifies an empty document tokenizes to one empty line with no fold ranges.</summary>
    [Fact]
    public void Tokenize_WhenGivenEmptyDocument_ProducesOneEmptyLineAndNoFoldRanges()
    {
        var result = SyntaxTokenizer.Tokenize(CompileMini(), string.Empty);

        _ = result.Lines.ShouldHaveSingleItem();
        result.Lines[0].Tokens.ShouldBeEmpty();
        result.FoldRanges.ShouldBeEmpty();
    }

    private const string _dynamicHeredocLanguage = """
        <language name="Heredoc" section="Sources" extensions="*.h" version="1" kateversion="5.0">
          <highlighting>
            <contexts>
              <context name="Normal" attribute="Normal Text" lineEndContext="#stay">
                <RegExpr attribute="Normal Text" context="Body" String="&lt;&lt;(\w+)"/>
              </context>
              <context name="Body" attribute="VerbatimString" lineEndContext="#stay">
                <StringDetect attribute="Normal Text" context="#pop" String="%1" dynamic="true"/>
              </context>
            </contexts>
            <itemDatas>
              <itemData name="Normal Text" defStyleNum="dsNormal"/>
              <itemData name="VerbatimString" defStyleNum="dsVerbatimString"/>
            </itemDatas>
          </highlighting>
        </language>
        """;

    /// <summary>Verifies a RegExpr rule's captured group propagates as a dynamic argument to the context it pushes.</summary>
    [Fact]
    public void Tokenize_WhenRegExprCapturesGroup_PropagatesItAsDynamicArgumentToPushedContext()
    {
        var grammar = SyntaxGrammar.Compile(SyntaxDefinitionReader.Read(_dynamicHeredocLanguage));
        var result = SyntaxTokenizer.Tokenize(grammar, "<<EOF\nbody text\nEOF\nafter");

        // Line 0 after the marker, and line 1's body, are inside the heredoc (VerbatimString);
        // "EOF" on line 2 closes it via the dynamic back-reference, and line 3 is Normal again.
        result.Lines[1].Tokens.ShouldAllBe(t => t.Style == SyntaxDefaultStyle.VerbatimString);
        result.Lines[3].Tokens.ShouldAllBe(t => t.Style == SyntaxDefaultStyle.Normal);
    }

    private const string _fallthroughLanguage = """
        <language name="Fallthrough" section="Sources" extensions="*.f" version="1" kateversion="5.0">
          <highlighting>
            <contexts>
              <context name="Normal" attribute="Normal Text" lineEndContext="#stay" fallthroughContext="Fell">
                <DetectChar attribute="Keyword" context="#stay" char="k"/>
              </context>
              <context name="Fell" attribute="Comment" lineEndContext="#pop"/>
            </contexts>
            <itemDatas>
              <itemData name="Normal Text" defStyleNum="dsNormal"/>
              <itemData name="Keyword" defStyleNum="dsKeyword"/>
              <itemData name="Comment" defStyleNum="dsComment"/>
            </itemDatas>
          </highlighting>
        </language>
        """;

    /// <summary>Verifies a context's fallthroughContext switches without consuming a character when no rule matches.</summary>
    [Fact]
    public void Tokenize_WhenNoRuleMatchesAndFallthroughIsSet_SwitchesContextWithoutConsuming()
    {
        var grammar = SyntaxGrammar.Compile(SyntaxDefinitionReader.Read(_fallthroughLanguage));
        var result = SyntaxTokenizer.Tokenize(grammar, "x");

        result.Lines[0].Tokens.ShouldHaveSingleItem().Style.ShouldBe(SyntaxDefaultStyle.Comment);
    }

    private const string _crossDefinitionLanguage = """
        <language name="Host" section="Sources" extensions="*.host" version="1" kateversion="5.0">
          <highlighting>
            <contexts>
              <context name="Normal" attribute="Normal Text" lineEndContext="#stay">
                <IncludeRules context="Normal##Embedded"/>
                <IncludeRules context="Normal##Missing"/>
              </context>
            </contexts>
            <itemDatas>
              <itemData name="Normal Text" defStyleNum="dsNormal"/>
            </itemDatas>
          </highlighting>
        </language>
        """;

    private const string _embeddedLanguage = """
        <language name="Embedded" section="Sources" extensions="*.embed" version="1" kateversion="5.0">
          <highlighting>
            <contexts>
              <context name="Normal" attribute="Marker" lineEndContext="#stay">
                <DetectChar attribute="Marker" context="#stay" char="!"/>
              </context>
            </contexts>
            <itemDatas>
              <itemData name="Marker" defStyleNum="dsAlert"/>
            </itemDatas>
          </highlighting>
        </language>
        """;

    /// <summary>Verifies IncludeRules referencing another definition splices that definition's rules in place.</summary>
    [Fact]
    public void Compile_WhenIncludeRulesReferencesAnotherDefinition_SplicesItsRules()
    {
        var host = SyntaxDefinitionReader.Read(_crossDefinitionLanguage);
        var embedded = SyntaxDefinitionReader.Read(_embeddedLanguage);

        var grammar = SyntaxGrammar.Compile(host, name => name == "Embedded" ? embedded : null);
        var result = SyntaxTokenizer.Tokenize(grammar, "!");

        result.Lines[0].Tokens.ShouldHaveSingleItem().Style.ShouldBe(SyntaxDefaultStyle.Alert);
    }

    /// <summary>Verifies IncludeRules referencing an unresolvable definition degrades gracefully instead of throwing.</summary>
    [Fact]
    public void Compile_WhenIncludeRulesReferencesUnresolvableDefinition_DegradesGracefullyInsteadOfThrowing()
    {
        var host = SyntaxDefinitionReader.Read(_crossDefinitionLanguage);

        var grammar = SyntaxGrammar.Compile(host, _ => null);
        var result = SyntaxTokenizer.Tokenize(grammar, "x");

        result.Lines[0].Tokens.ShouldHaveSingleItem().Style.ShouldBe(SyntaxDefaultStyle.Normal);
    }

    private const string _indentationLanguage = """
        <language name="Indented" section="Sources" extensions="*.i" version="1" kateversion="5.0">
          <general>
            <folding indentationsensitive="true"/>
          </general>
          <highlighting>
            <contexts>
              <context name="Normal" attribute="Normal Text" lineEndContext="#stay"/>
            </contexts>
            <itemDatas>
              <itemData name="Normal Text" defStyleNum="dsNormal"/>
            </itemDatas>
          </highlighting>
        </language>
        """;

    /// <summary>Verifies an indentation increase followed by a decrease produces an indentation fold range.</summary>
    [Fact]
    public void Tokenize_WhenIndentationIncreasesThenDecreases_ProducesIndentationFoldRange()
    {
        var grammar = SyntaxGrammar.Compile(SyntaxDefinitionReader.Read(_indentationLanguage));
        var result = SyntaxTokenizer.Tokenize(grammar, "def foo():\n    x = 1\n    y = 2\nz = 3");

        var range = result.FoldRanges.ShouldHaveSingleItem();
        range.Kind.ShouldBe(SyntaxFoldRangeKind.Indentation);
        range.StartLine.ShouldBe(0);
        range.EndLine.ShouldBe(2);
    }

    private const string _emptyLineStopLanguage = """
        <language name="EmptyLineStop" section="Sources" extensions="*.els" version="1" kateversion="5.0">
          <highlighting>
            <contexts>
              <context name="Normal" attribute="Normal Text" lineEndContext="#stay" lineEmptyContext="Terminal"/>
              <context name="Terminal" attribute="Comment" lineEndContext="#stay" lineEmptyContext="ShouldNotReach" stopEmptyLineContextSwitchLoop="true"/>
              <context name="ShouldNotReach" attribute="Alert" lineEndContext="#stay"/>
            </contexts>
            <itemDatas>
              <itemData name="Normal Text" defStyleNum="dsNormal"/>
              <itemData name="Comment" defStyleNum="dsComment"/>
              <itemData name="Alert" defStyleNum="dsAlert"/>
            </itemDatas>
          </highlighting>
        </language>
        """;

    /// <summary>
    /// Verifies that repeated empty-line context switching stops upon reaching a context whose own
    /// <c>stopEmptyLineContextSwitchLoop</c> is set, rather than the context being left - matching
    /// upstream's own check of the just-entered context, not the one the switch left.
    /// </summary>
    [Fact]
    public void Tokenize_WhenSwitchedContextStopsEmptyLineLoop_DoesNotChaseFurtherEmptyLineSwitches()
    {
        var grammar = SyntaxGrammar.Compile(SyntaxDefinitionReader.Read(_emptyLineStopLanguage));
        var result = SyntaxTokenizer.Tokenize(grammar, "\nx");

        // The empty first line should switch Normal -> Terminal and then stop, since Terminal sets
        // stopEmptyLineContextSwitchLoop. It must never reach ShouldNotReach.
        result.Lines[1].Tokens.ShouldHaveSingleItem().Style.ShouldBe(SyntaxDefaultStyle.Comment);
    }

    private const string _emptyLineRepeatedPopLanguage = """
        <language name="EmptyLineRepeatedPop" section="Sources" extensions="*.elp" version="1" kateversion="5.0">
          <highlighting>
            <contexts>
              <context name="Normal" attribute="Normal Text" lineEndContext="#stay">
                <DetectChar attribute="Normal Text" context="Bracket" char="("/>
                <DetectChar attribute="Stray" context="#stay" char=")"/>
              </context>
              <context name="Bracket" attribute="Bracket Text" lineEndContext="#stay" lineEmptyContext="#pop">
                <DetectChar attribute="Normal Text" context="Bracket" char="("/>
                <DetectChar attribute="Normal Text" context="#pop" char=")"/>
              </context>
            </contexts>
            <itemDatas>
              <itemData name="Normal Text" defStyleNum="dsNormal"/>
              <itemData name="Bracket Text" defStyleNum="dsChar"/>
              <itemData name="Stray" defStyleNum="dsError"/>
            </itemDatas>
          </highlighting>
        </language>
        """;

    /// <summary>
    /// Verifies that an empty line's repeated <c>#pop</c> keeps unwinding the context stack across
    /// multiple stack frames that happen to share the same compiled context (nested pushes of the
    /// same named context), rather than stopping as soon as the newly active context object
    /// happens to reference-equal the one two frames pushed.
    /// </summary>
    [Fact]
    public void Tokenize_WhenEmptyLinePopsThroughRepeatedSameContextFrames_UnwindsEveryFrame()
    {
        var grammar = SyntaxGrammar.Compile(SyntaxDefinitionReader.Read(_emptyLineRepeatedPopLanguage));

        // Line 0 pushes "Bracket" twice (two nested "(" characters, same compiled context both
        // times). Line 1 is empty and should pop both, fully unwinding back to "Normal". Line 2's
        // leading ")" then hits Normal's own #pop-less rule and is styled Stray, proving the stack
        // was already back at Normal instead of one Bracket frame short.
        var result = SyntaxTokenizer.Tokenize(grammar, "((\n\n))");

        result.Lines[2].Tokens[0].Style.ShouldBe(SyntaxDefaultStyle.Error);
    }
}
