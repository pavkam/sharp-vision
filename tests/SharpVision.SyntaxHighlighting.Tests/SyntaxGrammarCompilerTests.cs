// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting.Tests;

/// <summary>
/// Verifies <see cref="SyntaxGrammarCompiler"/>'s <c>IncludeRules</c> splicing and cross-definition
/// resolution: the most bug-prone area of the KDE grammar-compilation model, per
/// <c>ContextSwitch::resolve</c> and <c>Context::resolveIncludes</c> upstream.
/// </summary>
public sealed class SyntaxGrammarCompilerTests
{
    /// <summary>Verifies a null definition is rejected with ArgumentNullException.</summary>
    [Fact]
    public void Compile_WhenDefinitionIsNull_ThrowsArgumentNullException() =>
        _ = Should.Throw<ArgumentNullException>(static () => SyntaxGrammar.Compile(null!));

    private const string _baseLanguage = """
        <language name="Base" section="Sources" extensions="*.base" version="1" kateversion="5.0">
          <highlighting>
            <contexts>
              <context name="Start" attribute="Normal Text" lineEndContext="#stay">
                <DetectChar attribute="Normal Text" context="Middle" char="a"/>
              </context>
              <context name="Middle" attribute="BaseHighlight" lineEndContext="#stay">
                <DetectChar attribute="Normal Text" context="#pop" char="b"/>
              </context>
            </contexts>
            <itemDatas>
              <itemData name="Normal Text" defStyleNum="dsNormal"/>
              <itemData name="BaseHighlight" defStyleNum="dsChar"/>
            </itemDatas>
          </highlighting>
        </language>
        """;

    private const string _hostLanguage = """
        <language name="Host" section="Sources" extensions="*.host" version="1" kateversion="5.0">
          <highlighting>
            <contexts>
              <context name="Normal" attribute="Normal Text" lineEndContext="#stay">
                <IncludeRules context="Start##Base"/>
                <DetectChar attribute="AfterPop" context="#stay" char="!"/>
              </context>
              <context name="Middle" attribute="HostHighlight" lineEndContext="#stay"/>
            </contexts>
            <itemDatas>
              <itemData name="Normal Text" defStyleNum="dsNormal"/>
              <itemData name="HostHighlight" defStyleNum="dsComment"/>
              <itemData name="AfterPop" defStyleNum="dsAlert"/>
            </itemDatas>
          </highlighting>
        </language>
        """;

    /// <summary>
    /// Verifies that when a context spliced in by a cross-definition <c>IncludeRules</c> carries
    /// its own local context-switch to a context of the same name as one the *including*
    /// definition happens to also declare, the switch still resolves to the *included*
    /// definition's context, not the including definition's same-named one. Upstream resolves
    /// every context switch while a context is still owned by its original definition, before
    /// splicing its already-resolved rules elsewhere, specifically to avoid this trap.
    /// </summary>
    [Fact]
    public void Compile_WhenIncludedContextSwitchesToASiblingWithACollidingName_ResolvesRelativeToTheIncludedDefinition()
    {
        var host = SyntaxDefinitionReader.Read(_hostLanguage);
        var baseDefinition = SyntaxDefinitionReader.Read(_baseLanguage);

        var grammar = SyntaxGrammar.Compile(host, name => name == "Base" ? baseDefinition : null);

        // "a" enters Base's own "Middle" (BaseHighlight) via the spliced rule, "b" pops back out
        // via Base's own rule, and the trailing "!" only matches Host's own Normal-context rule if
        // the stack is truly back at Host's Normal - proving "a" never pushed Host's unrelated,
        // rule-less "Middle" context of the same name.
        var result = SyntaxTokenizer.Tokenize(grammar, "ab!");

        var tokens = result.Lines[0].Tokens;
        tokens[^1].Style.ShouldBe(SyntaxDefaultStyle.Alert);
    }

    /// <summary>
    /// Verifies that <c>includeAttrib="true"</c> makes the including context adopt the included
    /// context's own resolved attribute.
    /// </summary>
    [Fact]
    public void Compile_WhenIncludeRulesSetsIncludeAttribute_AdoptsTheIncludedContextsAttribute()
    {
        const string host = """
            <language name="AttribHost" section="Sources" extensions="*.ah" version="1" kateversion="5.0">
              <highlighting>
                <contexts>
                  <context name="Normal" attribute="Normal Text" lineEndContext="#stay">
                    <IncludeRules context="Start##Base" includeAttrib="true"/>
                  </context>
                </contexts>
                <itemDatas>
                  <itemData name="Normal Text" defStyleNum="dsNormal"/>
                </itemDatas>
              </highlighting>
            </language>
            """;

        var hostDefinition = SyntaxDefinitionReader.Read(host);
        var baseDefinition = SyntaxDefinitionReader.Read(_baseLanguage);

        var grammar = SyntaxGrammar.Compile(hostDefinition, name => name == "Base" ? baseDefinition : null);

        grammar.Contexts[0].AttributeStyle.ShouldBe(SyntaxDefaultStyle.Normal);
    }

    /// <summary>
    /// Verifies a context whose own <c>IncludeRules</c> names itself, directly, drops the cyclic
    /// occurrence instead of recursing forever or duplicating its own rules.
    /// </summary>
    [Fact]
    public void Compile_WhenContextIncludesItselfDirectly_DropsTheCycleWithoutInfiniteRecursion()
    {
        const string language = """
            <language name="SelfRef" section="Sources" extensions="*.sr" version="1" kateversion="5.0">
              <highlighting>
                <contexts>
                  <context name="A" attribute="Normal Text" lineEndContext="#stay">
                    <IncludeRules context="A"/>
                    <DetectChar attribute="Keyword" context="#stay" char="x"/>
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

        _ = grammar.Contexts[0].Rules.ShouldHaveSingleItem();
    }

    /// <summary>
    /// Verifies two contexts that <c>IncludeRules</c> each other across definitions - a diamond
    /// that never bottoms out on its own - still compile to a finite, correct rule set instead of
    /// recursing forever.
    /// </summary>
    [Fact]
    public void Compile_WhenTwoContextsIncludeEachOtherAcrossDefinitions_DropsTheCycleWithoutInfiniteRecursion()
    {
        const string language = """
            <language name="Cycle" section="Sources" extensions="*.cy" version="1" kateversion="5.0">
              <highlighting>
                <contexts>
                  <context name="A" attribute="Normal Text" lineEndContext="#stay">
                    <IncludeRules context="B"/>
                    <DetectChar attribute="Keyword" context="#stay" char="x"/>
                  </context>
                  <context name="B" attribute="Normal Text" lineEndContext="#stay">
                    <IncludeRules context="A"/>
                    <DetectChar attribute="String" context="#stay" char="y"/>
                  </context>
                </contexts>
                <itemDatas>
                  <itemData name="Normal Text" defStyleNum="dsNormal"/>
                  <itemData name="Keyword" defStyleNum="dsKeyword"/>
                  <itemData name="String" defStyleNum="dsString"/>
                </itemDatas>
              </highlighting>
            </language>
            """;

        var grammar = SyntaxGrammar.Compile(SyntaxDefinitionReader.Read(language));
        var result = SyntaxTokenizer.Tokenize(grammar, "xy");

        var tokens = result.Lines[0].Tokens;
        tokens[0].Style.ShouldBe(SyntaxDefaultStyle.Keyword);
        tokens[1].Style.ShouldBe(SyntaxDefaultStyle.String);
    }

    /// <summary>
    /// Verifies a keyword list's cross-definition <c>&lt;include&gt;</c> that cycles back to the
    /// originating list still resolves every reachable word exactly once instead of recursing
    /// forever.
    /// </summary>
    [Fact]
    public void Compile_WhenKeywordListCrossDefinitionIncludeIsCyclic_ResolvesWithoutInfiniteRecursion()
    {
        const string languageA = """
            <language name="LangA" section="Sources" extensions="*.a" version="1" kateversion="5.0">
              <highlighting>
                <list name="words">
                  <item>alpha</item>
                  <include>words##LangB</include>
                </list>
                <contexts>
                  <context name="Normal" attribute="Normal Text" lineEndContext="#stay">
                    <keyword attribute="Keyword" context="#stay" String="words"/>
                  </context>
                </contexts>
                <itemDatas>
                  <itemData name="Normal Text" defStyleNum="dsNormal"/>
                  <itemData name="Keyword" defStyleNum="dsKeyword"/>
                </itemDatas>
              </highlighting>
            </language>
            """;

        const string languageB = """
            <language name="LangB" section="Sources" extensions="*.b" version="1" kateversion="5.0">
              <highlighting>
                <list name="words">
                  <item>beta</item>
                  <include>words##LangA</include>
                </list>
                <contexts>
                  <context name="Normal" attribute="Normal Text" lineEndContext="#stay">
                    <keyword attribute="Keyword" context="#stay" String="words"/>
                  </context>
                </contexts>
                <itemDatas>
                  <itemData name="Normal Text" defStyleNum="dsNormal"/>
                  <itemData name="Keyword" defStyleNum="dsKeyword"/>
                </itemDatas>
              </highlighting>
            </language>
            """;

        var definitionA = SyntaxDefinitionReader.Read(languageA);
        var definitionB = SyntaxDefinitionReader.Read(languageB);

        var grammar = SyntaxGrammar.Compile(definitionA, name => name switch
        {
            "LangB" => definitionB,
            "LangA" => definitionA,
            _ => null,
        });

        var result = SyntaxTokenizer.Tokenize(grammar, "alpha beta");

        var tokens = result.Lines[0].Tokens;
        tokens[0].Style.ShouldBe(SyntaxDefaultStyle.Keyword);
        tokens[^1].Style.ShouldBe(SyntaxDefaultStyle.Keyword);
    }

    private const string _lookAheadUnresolvableTargetLanguage = """
        <language name="LookAheadUnresolvable" section="Sources" extensions="*.lau" version="1" kateversion="5.0">
          <highlighting>
            <contexts>
              <context name="Normal" attribute="Normal Text" lineEndContext="#stay">
                <RegExpr attribute="Normal Text" lookAhead="true" context="Other##Missing" String="x"/>
                <DetectChar attribute="Marker" context="#stay" char="x"/>
              </context>
            </contexts>
            <itemDatas>
              <itemData name="Normal Text" defStyleNum="dsNormal"/>
              <itemData name="Marker" defStyleNum="dsAlert"/>
            </itemDatas>
          </highlighting>
        </language>
        """;

    /// <summary>
    /// Verifies a <c>lookAhead</c> rule whose context reference names a definition the catalog
    /// cannot resolve - syntactically valid, but unresolvable only after cross-definition lookup
    /// runs - is dropped from its context entirely, rather than compiling into a rule whose
    /// resolved target is <see cref="SyntaxContextTarget.Stay"/>. A lookAhead rule that resolves to
    /// Stay would match at the same offset on every attempt without ever making progress, since a
    /// lookAhead match never consumes text of its own: unlike every other rule kind, for which a
    /// dangling cross-definition reference degrading to a no-op is harmless, here it would stall
    /// the tokenizer for the rest of the line.
    /// </summary>
    [Fact]
    public void Compile_WhenLookAheadRuleTargetsAnUnresolvableDefinition_DropsTheRuleEntirely()
    {
        var grammar = SyntaxGrammar.Compile(
            SyntaxDefinitionReader.Read(_lookAheadUnresolvableTargetLanguage),
            resolveDefinition: _ => null);

        grammar.Contexts[0].Rules.ShouldHaveSingleItem().Source.Kind.ShouldBe(SyntaxRuleKind.Character);

        var result = SyntaxTokenizer.Tokenize(grammar, "x");
        result.Lines[0].Tokens.ShouldHaveSingleItem().Style.ShouldBe(SyntaxDefaultStyle.Alert);
    }

    private const string _popWithUnresolvedPushLanguage = """
        <language name="PopWithUnresolvedPush" section="Sources" extensions="*.pup" version="1" kateversion="5.0">
          <highlighting>
            <contexts>
              <context name="Normal" attribute="Normal Text" lineEndContext="#stay">
                <DetectChar attribute="Normal Text" context="Inner" char="("/>
              </context>
              <context name="Inner" attribute="Bracket" lineEndContext="#stay">
                <DetectChar attribute="Normal Text" context="#pop!Missing##Unresolvable" char=")"/>
              </context>
            </contexts>
            <itemDatas>
              <itemData name="Normal Text" defStyleNum="dsNormal"/>
              <itemData name="Bracket" defStyleNum="dsChar"/>
            </itemDatas>
          </highlighting>
        </language>
        """;

    /// <summary>
    /// Pins SharpVision's own, deliberately upstream-diverging resolution contract for a
    /// <c>#pop!Name##Missing</c> switch whose push target cannot be resolved (see
    /// <see cref="SyntaxContextTarget"/>'s remarks): the pop still takes effect and the tokenizer
    /// correctly lands back in the popped-to context, rather than the whole switch collapsing to a
    /// no-op the way upstream's own resolution step would leave the tokenizer stranded one context
    /// deeper than intended.
    /// </summary>
    [Fact]
    public void Compile_WhenPopSwitchPushTargetIsUnresolvable_StillPopsBackToTheParentContext()
    {
        var grammar = SyntaxGrammar.Compile(
            SyntaxDefinitionReader.Read(_popWithUnresolvedPushLanguage),
            resolveDefinition: _ => null);

        // If the pop had been dropped along with the unresolvable push, offset 2's trailing "("
        // would still be evaluated inside "Inner" - which declares no rule for "(" at all - and
        // fall back to Inner's own Bracket attribute, a second, differently styled token. With the
        // pop correctly taking effect, offset 2 is back in "Normal", where "(" matches the same
        // push rule as offset 0 did, merging the whole line into one Normal-styled token.
        var result = SyntaxTokenizer.Tokenize(grammar, "()(");

        result.Lines[0].Tokens.ShouldHaveSingleItem().Style.ShouldBe(SyntaxDefaultStyle.Normal);
    }
}
