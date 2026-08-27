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

    /// <summary>Verifies a stray <c>endRegion</c> with no matching open <c>beginRegion</c> - the
    /// mirror image of an unterminated <c>beginRegion</c> - is silently ignored rather than
    /// throwing or producing a nonsensical fold range.</summary>
    [Fact]
    public void Tokenize_WhenEndRegionHasNoMatchingOpenRegion_ProducesNoFoldRange()
    {
        var result = SyntaxTokenizer.Tokenize(CompileMini(), "}");

        result.FoldRanges.ShouldBeEmpty();
    }

    /// <summary>Verifies an end marker closes the most recent open region with the same name,
    /// leaving differently named interleaved regions open for their own markers.</summary>
    [Fact]
    public void Tokenize_WhenNamedRegionsAreInterleaved_ClosesEachMatchingRegion()
    {
        const string language = """
            <language name="InterleavedRegions" section="Sources" extensions="*.regions" version="1" kateversion="5.0">
              <highlighting>
                <contexts>
                  <context name="Normal" attribute="Normal Text" lineEndContext="#stay">
                    <StringDetect attribute="Normal Text" context="#stay" String="openA" beginRegion="A"/>
                    <StringDetect attribute="Normal Text" context="#stay" String="openB" beginRegion="B"/>
                    <StringDetect attribute="Normal Text" context="#stay" String="closeA" endRegion="A"/>
                    <StringDetect attribute="Normal Text" context="#stay" String="closeB" endRegion="B"/>
                  </context>
                </contexts>
                <itemDatas>
                  <itemData name="Normal Text" defStyleNum="dsNormal"/>
                </itemDatas>
              </highlighting>
            </language>
            """;
        var grammar = SyntaxGrammar.Compile(SyntaxDefinitionReader.Read(language));

        var result = SyntaxTokenizer.Tokenize(grammar, "openA\nopenB\ncloseA\ncloseB");

        result.FoldRanges.Count.ShouldBe(2);
        result.FoldRanges[0].RegionName.ShouldBe("A");
        result.FoldRanges[0].StartLine.ShouldBe(0);
        result.FoldRanges[0].EndLine.ShouldBe(2);
        result.FoldRanges[1].RegionName.ShouldBe("B");
        result.FoldRanges[1].StartLine.ShouldBe(1);
        result.FoldRanges[1].EndLine.ShouldBe(3);
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

    /// <summary>
    /// Verifies the embedded ChangeLog definition's author-name pattern (<c>(\w\s*)+</c>) matches a
    /// complete accented name as one run instead of stopping at the first non-ASCII letter, the
    /// same PCRE2_UCP-backed Unicode property classification Qt's QRegularExpression - and
    /// therefore upstream Kate - always applies to <c>\w</c>.
    /// </summary>
    [Fact]
    public void Tokenize_WhenChangeLogEntryHasAccentedAuthorName_KeepsTheCompleteNameAsOneRun()
    {
        var grammar = SyntaxDefinitionCatalog.Default.GetGrammar("ChangeLog");
        var source = "2024-01-01  José García <jose@example.com>\n";

        var result = SyntaxTokenizer.Tokenize(grammar, source);

        var nameToken = result.Lines[0].Tokens.Single(token =>
            source.Substring(token.Start, token.Length).Contains("José", StringComparison.Ordinal));
        source.Substring(nameToken.Start, nameToken.Length).ShouldBe("José García ");
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
          <highlighting>
            <contexts>
              <context name="Normal" attribute="Normal Text" lineEndContext="#stay"/>
            </contexts>
            <itemDatas>
              <itemData name="Normal Text" defStyleNum="dsNormal"/>
            </itemDatas>
          </highlighting>
          <general>
            <folding indentationsensitive="true"/>
          </general>
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

    /// <summary>Verifies tab indentation follows the tokenizer consumer's documented one-cell
    /// geometry, so a visibly deeper two-space child folds beneath a one-tab parent.</summary>
    [Fact]
    public void Tokenize_WhenTabParentHasTwoSpaceChild_UsesOneCellTabIndentation()
    {
        var grammar = SyntaxGrammar.Compile(SyntaxDefinitionReader.Read(_indentationLanguage));

        var result = SyntaxTokenizer.Tokenize(grammar, "\tparent\n  child\nroot");

        var range = result.FoldRanges.ShouldHaveSingleItem();
        range.StartLine.ShouldBe(0);
        range.EndLine.ShouldBe(1);
    }

    /// <summary>Verifies a one-tab child is not treated as deeper than a two-space parent when a
    /// tab is displayed as one cell.</summary>
    [Fact]
    public void Tokenize_WhenTwoSpaceParentHasTabChild_DoesNotInventAFold()
    {
        var grammar = SyntaxGrammar.Compile(SyntaxDefinitionReader.Read(_indentationLanguage));

        var result = SyntaxTokenizer.Tokenize(grammar, "  parent\n\tchild\nroot");

        result.FoldRanges.ShouldBeEmpty();
    }

    /// <summary>Verifies a same-offset look-ahead cycle degrades to a complete fallback token
    /// instead of returning an uncovered non-empty line after its safety bound.</summary>
    [Fact]
    public void Tokenize_WhenLookAheadContextsCycleAtOneOffset_CoversTheRemainingLine()
    {
        const string language = """
            <language name="LookAheadCycle" section="Sources" extensions="*.cycle" version="1" kateversion="5.0">
              <highlighting>
                <contexts>
                  <context name="A" attribute="Normal Text" lineEndContext="#stay">
                    <DetectChar attribute="Normal Text" context="B" char="x" lookAhead="true"/>
                  </context>
                  <context name="B" attribute="Keyword" lineEndContext="#stay">
                    <DetectChar attribute="Keyword" context="#pop" char="x" lookAhead="true"/>
                  </context>
                </contexts>
                <itemDatas>
                  <itemData name="Normal Text" defStyleNum="dsNormal"/>
                  <itemData name="Keyword" defStyleNum="dsKeyword"/>
                </itemDatas>
              </highlighting>
            </language>
            """;
        var grammar = SyntaxGrammar.Compile(SyntaxDefinitionReader.Read(language));

        var result = SyntaxTokenizer.Tokenize(grammar, "xyz");

        var token = result.Lines[0].Tokens.ShouldHaveSingleItem();
        token.Start.ShouldBe(0);
        token.Length.ShouldBe(3);
    }

    private const string _keywordOnlyLanguage = """
        <language name="KeywordOnly" section="Sources" extensions="*.k" version="1" kateversion="5.0">
          <highlighting>
            <list name="keywords">
              <item>if</item>
              <item>else</item>
            </list>
            <contexts>
              <context name="Normal" attribute="Normal Text" lineEndContext="#stay">
                <keyword attribute="Keyword" context="#stay" String="keywords"/>
              </context>
            </contexts>
            <itemDatas>
              <itemData name="Normal Text" defStyleNum="dsNormal"/>
              <itemData name="Keyword" defStyleNum="dsKeyword"/>
            </itemDatas>
          </highlighting>
        </language>
        """;

    /// <summary>
    /// Verifies tokenizing one very long non-keyword, undelimited run under a context whose only
    /// rule is a <c>keyword</c> list - deliberately omitting a catch-all identifier-consuming rule
    /// real-world grammars normally pair a keyword rule with - completes in time roughly
    /// proportional to the input, not quadratic in it.
    /// </summary>
    /// <remarks>
    /// Before the per-line skip-offset cache, the tokenizer's one-character-at-a-time fallback (no
    /// rule matches at any offset, since the run is never a keyword) re-invoked the keyword rule at
    /// every offset, and each invocation independently rescanned forward to rediscover the same
    /// delimiter boundary its previous invocation already found - quadratic in the run length.
    /// Empirically, that made this exact input take several seconds; this asserts a generous bound
    /// no plausible machine load reaches under the current linear behavior, while remaining
    /// impossible for the quadratic behavior to meet.
    /// </remarks>
    [Fact]
    public void Tokenize_WhenOneLongNonKeywordRunHasNoOtherMatchingRule_CompletesInBoundedTime()
    {
        var grammar = SyntaxGrammar.Compile(SyntaxDefinitionReader.Read(_keywordOnlyLanguage));
        var line = new string('a', 200_000);
        var stopwatch = Stopwatch.StartNew();

        var result = SyntaxTokenizer.Tokenize(grammar, line);

        stopwatch.Stop();
        result.Lines.ShouldHaveSingleItem().Tokens.ShouldHaveSingleItem().Style.ShouldBe(SyntaxDefaultStyle.Normal);
        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(5));
    }

    private const string _catastrophicRegularExpressionLanguage = """
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

    /// <summary>Verifies one pathological rule is suppressed after exhausting its bounded match
    /// budget instead of paying that budget again at every remaining source offset.</summary>
    [Fact]
    public void Tokenize_WhenRegularExpressionExhaustsMatchBudget_DoesNotRetryAtEveryOffset()
    {
        var grammar = SyntaxGrammar.Compile(SyntaxDefinitionReader.Read(_catastrophicRegularExpressionLanguage));
        var adversarialLine = new string('a', 40);
        var stopwatch = Stopwatch.StartNew();

        var result = SyntaxTokenizer.Tokenize(grammar, adversarialLine);

        stopwatch.Stop();
        result.Lines.ShouldHaveSingleItem().Tokens.ShouldHaveSingleItem().Length.ShouldBe(adversarialLine.Length);
        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(2));
    }

    private const string _lineEndPopBeyondRootLanguage = """
        <language name="LineEndPopBeyondRoot" section="Sources" extensions="*.p" version="1" kateversion="5.0">
          <highlighting>
            <contexts>
              <context name="Normal" attribute="Normal Text" lineEndContext="#pop!Normal"/>
            </contexts>
            <itemDatas>
              <itemData name="Normal Text" defStyleNum="dsNormal"/>
            </itemDatas>
          </highlighting>
        </language>
        """;

    /// <summary>
    /// Verifies a <c>lineEndContext</c> that pops-and-pushes even while already at the root
    /// context - a malformed grammar authoring mistake, since a well-formed root context does not
    /// try to pop past itself - stays bounded by the per-boundary context-switch ceiling on every
    /// single line instead of hanging, and that every following line still tokenizes correctly
    /// regardless of how many frames the pattern has already stacked up.
    /// </summary>
    [Fact]
    public void Tokenize_WhenLineEndContextPopsBeyondTheRootEveryLine_RemainsBoundedAndContinuesTokenizing()
    {
        var grammar = SyntaxGrammar.Compile(SyntaxDefinitionReader.Read(_lineEndPopBeyondRootLanguage));

        var result = SyntaxTokenizer.Tokenize(grammar, "a\nb\nc\nd\ne");

        result.Lines.Count.ShouldBe(5);

        foreach (var line in result.Lines)
        {
            line.Tokens.ShouldHaveSingleItem().Style.ShouldBe(SyntaxDefaultStyle.Normal);
        }
    }

    private const string _coincidentFoldRangesLanguage = """
        <language name="CoincidentFolds" section="Sources" extensions="*.c" version="1" kateversion="5.0">
          <highlighting>
            <contexts>
              <context name="Normal" attribute="Normal Text" lineEndContext="#stay">
                <StringDetect attribute="Normal Text" context="#stay" String="open" beginRegion="R"/>
                <StringDetect attribute="Normal Text" context="#stay" String="close" endRegion="R"/>
              </context>
            </contexts>
            <itemDatas>
              <itemData name="Normal Text" defStyleNum="dsNormal"/>
            </itemDatas>
          </highlighting>
          <general>
            <folding indentationsensitive="true"/>
          </general>
        </language>
        """;

    /// <summary>Verifies a region fold and an indentation fold that share the exact same start and
    /// end line sort in a fixed, deterministic order (region before indentation) rather than in
    /// the unspecified order an unstable sort's tie-breaking would otherwise permit, matching this
    /// repository's deterministic-UI-state requirement.</summary>
    [Fact]
    public void Tokenize_WhenRegionAndIndentationFoldsShareTheSameLines_SortsDeterministically()
    {
        var grammar = SyntaxGrammar.Compile(SyntaxDefinitionReader.Read(_coincidentFoldRangesLanguage));

        var result = SyntaxTokenizer.Tokenize(grammar, "open\n    close\nz");

        result.FoldRanges.Count.ShouldBe(2);
        result.FoldRanges[0].StartLine.ShouldBe(0);
        result.FoldRanges[0].EndLine.ShouldBe(1);
        result.FoldRanges[0].Kind.ShouldBe(SyntaxFoldRangeKind.Region);
        result.FoldRanges[1].StartLine.ShouldBe(0);
        result.FoldRanges[1].EndLine.ShouldBe(1);
        result.FoldRanges[1].Kind.ShouldBe(SyntaxFoldRangeKind.Indentation);
    }

    private const string _invalidEmptyLinePatternLanguage = """
        <language name="InvalidEmptyLine" section="Sources" extensions="*.i" version="1" kateversion="5.0">
          <highlighting>
            <contexts>
              <context name="Normal" attribute="Normal Text" lineEndContext="#stay"/>
            </contexts>
            <itemDatas>
              <itemData name="Normal Text" defStyleNum="dsNormal"/>
            </itemDatas>
          </highlighting>
          <general>
            <folding indentationsensitive="true"/>
            <emptyLines>
              <emptyLine regexpr="(unterminated"/>
            </emptyLines>
          </general>
        </language>
        """;

    /// <summary>Verifies an unparsable <c>&lt;emptyLine regexpr&gt;</c> pattern - plausible in a
    /// hand-authored or third-party definition - is simply excluded from indentation-folding's
    /// blank-line classification instead of throwing out of compilation or tokenization.</summary>
    [Fact]
    public void Tokenize_WhenEmptyLinePatternIsUnparsable_FoldsAsIfThatRuleWereAbsent()
    {
        var grammar = SyntaxGrammar.Compile(SyntaxDefinitionReader.Read(_invalidEmptyLinePatternLanguage));

        var result = SyntaxTokenizer.Tokenize(grammar, "def foo():\n    x = 1\n    y = 2\nz = 3");

        var range = result.FoldRanges.ShouldHaveSingleItem();
        range.Kind.ShouldBe(SyntaxFoldRangeKind.Indentation);
        range.StartLine.ShouldBe(0);
        range.EndLine.ShouldBe(2);
    }

    private const string _pcreEmptyLinePatternLanguage = """
        <language name="PcreEmptyLine" section="Sources" extensions="*.p" version="1" kateversion="5.0">
          <highlighting>
            <contexts>
              <context name="Normal" attribute="Normal Text" lineEndContext="#stay"/>
            </contexts>
            <itemDatas>
              <itemData name="Normal Text" defStyleNum="dsNormal"/>
            </itemDatas>
          </highlighting>
          <general>
            <folding indentationsensitive="true"/>
            <emptyLines>
              <emptyLine regexpr="^a++$"/>
            </emptyLines>
          </general>
        </language>
        """;

    /// <summary>Verifies indentation folding interprets empty-line patterns with the same PCRE2
    /// dialect as ordinary KDE regular-expression rules.</summary>
    [Fact]
    public void Tokenize_WhenEmptyLinePatternUsesPcreConstruct_ClassifiesMatchingLineAsEmpty()
    {
        var grammar = SyntaxGrammar.Compile(SyntaxDefinitionReader.Read(_pcreEmptyLinePatternLanguage));

        var result = SyntaxTokenizer.Tokenize(grammar, "root\n    child\naaaa\n    grandchild\nnext");

        var range = result.FoldRanges.ShouldHaveSingleItem();
        range.StartLine.ShouldBe(0);
        range.EndLine.ShouldBe(3);
    }

    private const string _catastrophicEmptyLinePatternLanguage = """
        <language name="CatastrophicEmptyLine" section="Sources" extensions="*.c" version="1" kateversion="5.0">
          <highlighting>
            <contexts>
              <context name="Normal" attribute="Normal Text" lineEndContext="#stay"/>
            </contexts>
            <itemDatas>
              <itemData name="Normal Text" defStyleNum="dsNormal"/>
            </itemDatas>
          </highlighting>
          <general>
            <folding indentationsensitive="true"/>
            <emptyLines>
              <emptyLine regexpr="^(a+)+b$"/>
            </emptyLines>
          </general>
        </language>
        """;

    /// <summary>Verifies a catastrophically-backtracking <c>&lt;emptyLine regexpr&gt;</c> pattern
    /// exhausts its bounded match budget to "this line is not blank" rather than blocking
    /// tokenization of the rest of the document.</summary>
    [Fact]
    public void Tokenize_WhenEmptyLinePatternCausesCatastrophicBacktracking_TimesOutAndContinuesTokenizing()
    {
        var grammar = SyntaxGrammar.Compile(SyntaxDefinitionReader.Read(_catastrophicEmptyLinePatternLanguage));
        var adversarialLine = new string('a', 40);

        var result = SyntaxTokenizer.Tokenize(grammar, $"def foo():\n    {adversarialLine}\nz = 3");

        result.Lines.Count.ShouldBe(3);
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

    private const string _lookAheadSwitchLanguage = """
        <language name="LookAheadSwitch" section="Sources" extensions="*.la" version="1" kateversion="5.0">
          <highlighting>
            <contexts>
              <context name="Normal" attribute="Normal Text" lineEndContext="#stay">
                <DetectChar attribute="Normal Text" lookAhead="true" context="Marked" char="x"/>
                <AnyChar attribute="Normal Text" context="#stay" String="x"/>
              </context>
              <context name="Marked" attribute="Alert" lineEndContext="#pop"/>
            </contexts>
            <itemDatas>
              <itemData name="Normal Text" defStyleNum="dsNormal"/>
              <itemData name="Alert" defStyleNum="dsAlert"/>
            </itemDatas>
          </highlighting>
        </language>
        """;

    /// <summary>
    /// Verifies a matching <c>lookAhead</c> rule applies its context switch without consuming any
    /// text: the matched character is styled by the newly active context's own attribute (reached
    /// through the ordinary "no rule in this context matched" fallback, since "Marked" declares no
    /// rules of its own), proving the switch actually took effect rather than merely not throwing.
    /// </summary>
    [Fact]
    public void Tokenize_WhenLookAheadRuleMatches_SwitchesContextWithoutConsumingText()
    {
        var grammar = SyntaxGrammar.Compile(SyntaxDefinitionReader.Read(_lookAheadSwitchLanguage));

        var result = SyntaxTokenizer.Tokenize(grammar, "x");

        result.Lines[0].Tokens.ShouldHaveSingleItem().Style.ShouldBe(SyntaxDefaultStyle.Alert);
    }

    private const string _columnAndFirstNonSpaceLanguage = """
        <language name="ColumnConstraints" section="Sources" extensions="*.c" version="1" kateversion="5.0">
          <highlighting>
            <contexts>
              <context name="Normal" attribute="Normal Text" lineEndContext="#stay">
                <DetectChar attribute="Shebang" context="#stay" char="#" column="0"/>
                <DetectChar attribute="Marker" context="#stay" char="%" firstNonSpace="true"/>
              </context>
            </contexts>
            <itemDatas>
              <itemData name="Normal Text" defStyleNum="dsNormal"/>
              <itemData name="Shebang" defStyleNum="dsPreprocessor"/>
              <itemData name="Marker" defStyleNum="dsAlert"/>
            </itemDatas>
          </highlighting>
        </language>
        """;

    /// <summary>Verifies a rule's <c>column</c> constraint restricts it to that exact offset,
    /// falling through to the context's own default styling everywhere else on the line.</summary>
    [Fact]
    public void Tokenize_WhenRuleRequiresAnExactColumn_MatchesOnlyThere()
    {
        var grammar = SyntaxGrammar.Compile(SyntaxDefinitionReader.Read(_columnAndFirstNonSpaceLanguage));

        var atColumnZero = SyntaxTokenizer.Tokenize(grammar, "#a");
        var elsewhere = SyntaxTokenizer.Tokenize(grammar, "a#");

        atColumnZero.Lines[0].Tokens[0].Style.ShouldBe(SyntaxDefaultStyle.Preprocessor);

        // "#" at offset 1 never satisfies column="0", so the whole line stays one token under the
        // context's own default style - proving the Shebang style never appears here.
        elsewhere.Lines[0].Tokens.ShouldHaveSingleItem().Style.ShouldBe(SyntaxDefaultStyle.Normal);
    }

    /// <summary>Verifies a rule's <c>firstNonSpace</c> constraint restricts it to offsets at or
    /// before the line's first non-whitespace character, falling through to the context's own
    /// default styling once that point has passed.</summary>
    [Fact]
    public void Tokenize_WhenRuleRequiresFirstNonSpace_MatchesOnlyBeforeItPasses()
    {
        var grammar = SyntaxGrammar.Compile(SyntaxDefinitionReader.Read(_columnAndFirstNonSpaceLanguage));

        // "%" is still the line's first non-space character, so the firstNonSpace rule applies.
        var beforeFirstNonSpace = SyntaxTokenizer.Tokenize(grammar, "  %a");

        // "a" is the first non-space character; by the time "%" is reached, offset already
        // exceeds firstNonSpace, so the rule never applies and the whole line stays one token.
        var afterFirstNonSpace = SyntaxTokenizer.Tokenize(grammar, "a %");

        beforeFirstNonSpace.Lines[0].Tokens[1].Style.ShouldBe(SyntaxDefaultStyle.Alert);
        afterFirstNonSpace.Lines[0].Tokens.ShouldHaveSingleItem().Style.ShouldBe(SyntaxDefaultStyle.Normal);
    }

    private const string _perRuleWeakDelimiterLanguage = """
        <language name="PerRuleWeakDelimiter" section="Sources" extensions="*.w" version="1" kateversion="5.0">
          <highlighting>
            <list name="words">
              <item>foo.bar</item>
            </list>
            <contexts>
              <context name="Normal" attribute="Normal Text" lineEndContext="#stay">
                <keyword attribute="Keyword" context="#stay" String="words" weakDeliminator="."/>
              </context>
            </contexts>
            <itemDatas>
              <itemData name="Normal Text" defStyleNum="dsNormal"/>
              <itemData name="Keyword" defStyleNum="dsKeyword"/>
            </itemDatas>
          </highlighting>
        </language>
        """;

    /// <summary>Verifies a rule's own <c>weakDeliminator</c> override actually changes match
    /// behavior: removing "." from the effective delimiter set lets a keyword list entry that
    /// itself contains a "." match as one word instead of splitting at the dot.</summary>
    [Fact]
    public void Tokenize_WhenRuleOverridesWeakDeliminator_MatchesAcrossTheRemovedDelimiter()
    {
        var grammar = SyntaxGrammar.Compile(SyntaxDefinitionReader.Read(_perRuleWeakDelimiterLanguage));

        var result = SyntaxTokenizer.Tokenize(grammar, "foo.bar");

        result.Lines[0].Tokens.ShouldHaveSingleItem().Style.ShouldBe(SyntaxDefaultStyle.Keyword);
    }
}
