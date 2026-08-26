// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting.Tests;

/// <summary>Verifies KDE syntax-definition XML parsing, validation, and error handling.</summary>
public sealed class SyntaxDefinitionReaderTests
{
    /// <summary>Verifies the required minimum engine version is preserved in parsed metadata.</summary>
    [Fact]
    public void Read_WhenKateVersionIsSupported_PreservesMinimumVersion() =>
        SyntaxDefinitionReader.Read(_minimal).KateVersion.ShouldBe(new Version(5, 0));

    /// <summary>Verifies definitions cannot omit the schema-required engine version.</summary>
    [Fact]
    public void Read_WhenKateVersionIsMissing_ThrowsFormatException()
    {
        var xml = _minimal.Replace(" kateversion=\"5.0\"", string.Empty, StringComparison.Ordinal);

        _ = Should.Throw<FormatException>(() => SyntaxDefinitionReader.Read(xml));
    }

    /// <summary>Verifies definitions requiring a newer engine format are rejected before publication.</summary>
    [Fact]
    public void Read_WhenKateVersionIsNewerThanSupported_ThrowsFormatException()
    {
        var xml = _minimal.Replace("kateversion=\"5.0\"", "kateversion=\"99.0\"", StringComparison.Ordinal);

        _ = Should.Throw<FormatException>(() => SyntaxDefinitionReader.Read(xml));
    }

    private const string _minimal = """
        <?xml version="1.0" encoding="UTF-8"?>
        <!DOCTYPE language>
        <language name="Mini" section="Sources" extensions="*.mini" version="1" kateversion="5.0" author="Test" license="MIT">
          <highlighting>
            <list name="keywords">
              <item>if</item>
              <item>else</item>
            </list>
            <list name="aliasKeywords">
              <include>keywords</include>
              <item>then</item>
            </list>
            <contexts>
              <context name="Normal" attribute="Normal Text" lineEndContext="#stay">
                <keyword attribute="Keyword" context="#stay" String="aliasKeywords"/>
                <DetectChar attribute="String" context="String" char="&quot;"/>
                <RegExpr attribute="Comment" context="#stay" String="//.*$"/>
              </context>
              <context name="String" attribute="String" lineEndContext="#pop">
                <HlCStringChar attribute="SpecialChar" context="#stay"/>
                <DetectChar attribute="String" context="#pop" char="&quot;"/>
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

    /// <summary>Verifies core language metadata and context declaration order are preserved.</summary>
    [Fact]
    public void Read_WhenGivenMinimalDefinition_ParsesMetadata()
    {
        var definition = SyntaxDefinitionReader.Read(_minimal);

        definition.Name.ShouldBe("Mini");
        definition.Section.ShouldBe("Sources");
        definition.Extensions.ShouldBe(["*.mini"]);
        definition.Version.ShouldBe(1);
        definition.Author.ShouldBe("Test");
        definition.License.ShouldBe("MIT");
        definition.Contexts.Count.ShouldBe(2);
        definition.Contexts[0].Name.ShouldBe("Normal");
    }

    /// <summary>Verifies a keyword list's own <c>&lt;include&gt;</c> is flattened at read time.</summary>
    [Fact]
    public void Read_WhenListIncludesAnotherList_FlattensWords()
    {
        var definition = SyntaxDefinitionReader.Read(_minimal);

        definition.KeywordLists["aliasKeywords"].Words.ShouldBe(["if", "else", "then"]);
    }

    /// <summary>Verifies every <c>ds*</c> default style name maps to its <see cref="SyntaxDefaultStyle"/> value.</summary>
    [Fact]
    public void Read_WhenItemDataDeclaresEveryDefaultStyle_MapsAllThirtyOneRoles()
    {
        // DecimalValue and CommentVariable are the two roles whose XML names (dsDecVal,
        // dsCommentVar) do not follow the "ds{EnumName}" pattern every other role uses.
        var abbreviatedNames = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [nameof(SyntaxDefaultStyle.DecimalValue)] = "dsDecVal",
            [nameof(SyntaxDefaultStyle.CommentVariable)] = "dsCommentVar",
        };

        var xmlNameByEnumName = Enum.GetNames<SyntaxDefaultStyle>().ToDictionary(
            name => name,
            name => abbreviatedNames.GetValueOrDefault(name, $"ds{name}"));

        var allStyles = string.Join(
            '\n',
            xmlNameByEnumName.Select(pair => $"<itemData name=\"{pair.Key}\" defStyleNum=\"{pair.Value}\"/>"));

        var xml = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <language name="AllStyles" section="Sources" extensions="*.a" version="1" kateversion="5.0">
              <highlighting>
                <contexts>
                  <context name="Normal" attribute="Normal" lineEndContext="#stay"/>
                </contexts>
                <itemDatas>
                  {allStyles}
                </itemDatas>
              </highlighting>
            </language>
            """;

        var definition = SyntaxDefinitionReader.Read(xml);

        definition.ItemDataSet.Count.ShouldBe(31);
        definition.ItemDataSet["Comment"].DefaultStyle.ShouldBe(SyntaxDefaultStyle.Comment);
        definition.ItemDataSet["ControlFlow"].DefaultStyle.ShouldBe(SyntaxDefaultStyle.ControlFlow);
    }

    /// <summary>Verifies a document whose root element is not <c>&lt;language&gt;</c> is rejected.</summary>
    [Fact]
    public void Read_WhenRootIsNotLanguage_ThrowsFormatException() => _ = Should.Throw<FormatException>(() => SyntaxDefinitionReader.Read("<notLanguage/>"));

    /// <summary>Verifies a context whose <c>attribute</c> names an undeclared item data is rejected.</summary>
    [Fact]
    public void Read_WhenContextReferencesUnknownAttribute_ThrowsFormatException()
    {
        const string xml = """
            <language name="Bad" section="Sources" extensions="*.b" version="1" kateversion="5.0">
              <highlighting>
                <contexts>
                  <context name="Normal" attribute="Missing" lineEndContext="#stay"/>
                </contexts>
                <itemDatas>
                  <itemData name="Normal Text" defStyleNum="dsNormal"/>
                </itemDatas>
              </highlighting>
            </language>
            """;

        _ = Should.Throw<FormatException>(() => SyntaxDefinitionReader.Read(xml));
    }

    /// <summary>Verifies a cyclic keyword-list <c>&lt;include&gt;</c> chain is rejected rather than looping forever.</summary>
    [Fact]
    public void Read_WhenKeywordListIncludeCycleExists_ThrowsFormatException()
    {
        const string xml = """
            <language name="Cyclic" section="Sources" extensions="*.c" version="1" kateversion="5.0">
              <highlighting>
                <list name="a"><include>b</include></list>
                <list name="b"><include>a</include></list>
                <contexts>
                  <context name="Normal" attribute="Normal Text" lineEndContext="#stay">
                    <keyword attribute="Normal Text" context="#stay" String="a"/>
                  </context>
                </contexts>
                <itemDatas>
                  <itemData name="Normal Text" defStyleNum="dsNormal"/>
                </itemDatas>
              </highlighting>
            </language>
            """;

        _ = Should.Throw<FormatException>(() => SyntaxDefinitionReader.Read(xml));
    }

    /// <summary>Verifies internal DTD entity declarations several upstream files rely on are expanded.</summary>
    [Fact]
    public void Read_WhenDocumentUsesInternalDtdEntities_ExpandsThem()
    {
        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE language [
              <!ENTITY greeting "Hello">
            ]>
            <language name="&greeting;World" section="Sources" extensions="*.g" version="1" kateversion="5.0">
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

        var definition = SyntaxDefinitionReader.Read(xml);

        definition.Name.ShouldBe("HelloWorld");
    }

    /// <summary>Verifies an unspecified <c>lineEmptyContext</c> resolves to the context's own <c>lineEndContext</c>.</summary>
    [Fact]
    public void Read_WhenLineEmptyContextIsUnspecified_DefaultsToLineEndContext()
    {
        const string xml = """
            <language name="Defaulting" section="Sources" extensions="*.d" version="1" kateversion="5.0">
              <highlighting>
                <contexts>
                  <context name="Normal" attribute="Normal Text" lineEndContext="#pop"/>
                </contexts>
                <itemDatas>
                  <itemData name="Normal Text" defStyleNum="dsNormal"/>
                </itemDatas>
              </highlighting>
            </language>
            """;

        var definition = SyntaxDefinitionReader.Read(xml);

        definition.Contexts[0].LineEmptyContext.ShouldBe(definition.Contexts[0].LineEndContext);
    }

    /// <summary>Verifies an explicit <c>lineEmptyContext="#stay"</c> - not merely an unspecified
    /// attribute - is still overridden to fall back to <c>lineEndContext</c>, matching upstream
    /// <c>Context::resolveContexts</c>'s special case for skipping past a line-continuation
    /// character on an otherwise-empty line.</summary>
    [Fact]
    public void Read_WhenLineEmptyContextIsExplicitlyStay_FallsBackToLineEndContext()
    {
        const string xml = """
            <language name="ExplicitStay" section="Sources" extensions="*.d" version="1" kateversion="5.0">
              <highlighting>
                <contexts>
                  <context name="Normal" attribute="Normal Text" lineEndContext="#pop" lineEmptyContext="#stay"/>
                </contexts>
                <itemDatas>
                  <itemData name="Normal Text" defStyleNum="dsNormal"/>
                </itemDatas>
              </highlighting>
            </language>
            """;

        var definition = SyntaxDefinitionReader.Read(xml);

        definition.Contexts[0].LineEmptyContext.ShouldBe(definition.Contexts[0].LineEndContext);
    }

    /// <summary>
    /// Verifies an <c>&lt;item&gt;</c> surrounded by incidental whitespace (common when a list is
    /// formatted one entry per line) is trimmed, matching upstream
    /// <c>KeywordList::load</c>'s <c>readElementText().trimmed()</c>.
    /// </summary>
    [Fact]
    public void Read_WhenKeywordItemHasSurroundingWhitespace_TrimsWord()
    {
        const string xml = """
            <language name="Trimming" section="Sources" extensions="*.t" version="1" kateversion="5.0">
              <highlighting>
                <list name="keywords">
                  <item>
                    padded
                  </item>
                </list>
                <contexts>
                  <context name="Normal" attribute="Normal Text" lineEndContext="#stay">
                    <keyword attribute="Normal Text" context="#stay" String="keywords"/>
                  </context>
                </contexts>
                <itemDatas>
                  <itemData name="Normal Text" defStyleNum="dsNormal"/>
                </itemDatas>
              </highlighting>
            </language>
            """;

        var definition = SyntaxDefinitionReader.Read(xml);

        definition.KeywordLists["keywords"].Words.ShouldBe(["padded"]);
    }

    /// <summary>
    /// Verifies a keyword list containing only a cross-definition <c>&lt;include&gt;</c> parses
    /// with an empty <see cref="SyntaxKeywordList.Words"/> rather than requiring same-file content.
    /// </summary>
    [Fact]
    public void Read_WhenKeywordListHasOnlyCrossDefinitionInclude_WordsIsEmpty()
    {
        const string xml = """
            <language name="CrossOnly" section="Sources" extensions="*.co" version="1" kateversion="5.0">
              <highlighting>
                <list name="borrowed">
                  <include>keywords##Other</include>
                </list>
                <contexts>
                  <context name="Normal" attribute="Normal Text" lineEndContext="#stay"/>
                </contexts>
                <itemDatas>
                  <itemData name="Normal Text" defStyleNum="dsNormal"/>
                </itemDatas>
              </highlighting>
            </language>
            """;

        var definition = SyntaxDefinitionReader.Read(xml);

        definition.KeywordLists["borrowed"].Words.ShouldBeEmpty();
        definition.KeywordLists["borrowed"].CrossDefinitionIncludes.ShouldBe(["keywords##Other"]);
    }

    /// <summary>
    /// Verifies a top-level <c>&lt;language casesensitive="…"&gt;</c> attribute supplies the
    /// keyword case-sensitivity default when <c>&lt;general&gt;&lt;keywords&gt;</c> does not
    /// itself declare one, matching upstream <c>DefinitionData::loadLanguage</c> setting the same
    /// field <c>DefinitionData::loadGeneral</c> only conditionally overrides.
    /// </summary>
    [Fact]
    public void Read_WhenLanguageDeclaresCaseSensitivityAndGeneralDoesNot_UsesLanguageLevelDefault()
    {
        const string xml = """
            <language name="LanguageCase" section="Sources" extensions="*.lc" version="1" kateversion="5.0" casesensitive="0">
              <highlighting>
                <contexts>
                  <context name="Normal" attribute="Normal Text" lineEndContext="#stay"/>
                </contexts>
                <itemDatas>
                  <itemData name="Normal Text" defStyleNum="dsNormal"/>
                </itemDatas>
              </highlighting>
              <general>
                <keywords weakDeliminator="-"/>
              </general>
            </language>
            """;

        var definition = SyntaxDefinitionReader.Read(xml);

        definition.General.CaseSensitiveKeywords.ShouldBeFalse();
    }

    /// <summary>
    /// Verifies <c>&lt;general&gt;&lt;keywords casesensitive="…"&gt;</c> still overrides the
    /// top-level <c>&lt;language casesensitive="…"&gt;</c> default when both are present.
    /// </summary>
    [Fact]
    public void Read_WhenBothLanguageAndKeywordsDeclareCaseSensitivity_KeywordsElementWins()
    {
        const string xml = """
            <language name="BothCase" section="Sources" extensions="*.bc" version="1" kateversion="5.0" casesensitive="0">
              <highlighting>
                <contexts>
                  <context name="Normal" attribute="Normal Text" lineEndContext="#stay"/>
                </contexts>
                <itemDatas>
                  <itemData name="Normal Text" defStyleNum="dsNormal"/>
                </itemDatas>
              </highlighting>
              <general>
                <keywords casesensitive="1"/>
              </general>
            </language>
            """;

        var definition = SyntaxDefinitionReader.Read(xml);

        definition.General.CaseSensitiveKeywords.ShouldBeTrue();
    }

    /// <summary>
    /// Verifies a <c>lookAhead</c> rule's own dangling <c>attribute</c> reference is not resolved
    /// or validated, matching upstream <c>HighlightingContextData::load</c>, which only reads a
    /// rule's <c>attribute</c> when <c>lookAhead</c> is false since a lookahead match never styles
    /// any text.
    /// </summary>
    [Fact]
    public void Read_WhenLookAheadRuleReferencesUnknownAttribute_DoesNotThrowAndLeavesAttributeNameNull()
    {
        const string xml = """
            <language name="LookAheadDangling" section="Sources" extensions="*.la" version="1" kateversion="5.0">
              <highlighting>
                <contexts>
                  <context name="Normal" attribute="Normal Text" lineEndContext="#stay">
                    <DetectChar attribute="DoesNotExist" lookAhead="true" context="Other" char="x"/>
                  </context>
                  <context name="Other" attribute="Normal Text" lineEndContext="#pop"/>
                </contexts>
                <itemDatas>
                  <itemData name="Normal Text" defStyleNum="dsNormal"/>
                </itemDatas>
              </highlighting>
            </language>
            """;

        var definition = SyntaxDefinitionReader.Read(xml);

        var rule = definition.Contexts[0].Rules[0];
        rule.LookAhead.ShouldBeTrue();
        rule.AttributeName.ShouldBeNull();
    }

    /// <summary>
    /// Verifies a non-lookahead rule's dangling <c>attribute</c> reference is still rejected,
    /// confirming the lookahead exemption above does not weaken validation for ordinary rules.
    /// </summary>
    [Fact]
    public void Read_WhenNonLookAheadRuleReferencesUnknownAttribute_ThrowsFormatException()
    {
        const string xml = """
            <language name="NonLookAheadDangling" section="Sources" extensions="*.nla" version="1" kateversion="5.0">
              <highlighting>
                <contexts>
                  <context name="Normal" attribute="Normal Text" lineEndContext="#stay">
                    <DetectChar attribute="DoesNotExist" context="#stay" char="x"/>
                  </context>
                </contexts>
                <itemDatas>
                  <itemData name="Normal Text" defStyleNum="dsNormal"/>
                </itemDatas>
              </highlighting>
            </language>
            """;

        _ = Should.Throw<FormatException>(() => SyntaxDefinitionReader.Read(xml));
    }

    /// <summary>
    /// Verifies a boolean attribute spelled with different capitalization (<c>"True"</c>) is still
    /// accepted, matching upstream <c>Xml::attrToBool</c>'s case-insensitive comparison against
    /// <c>"true"</c>.
    /// </summary>
    [Fact]
    public void Read_WhenBooleanAttributeUsesMixedCapitalization_TreatsCaseInsensitiveTrueAsTrue()
    {
        const string xml = """
            <language name="MixedCaseBool" section="Sources" extensions="*.mc" version="1" kateversion="5.0" hidden="True">
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

        var definition = SyntaxDefinitionReader.Read(xml);

        definition.Hidden.ShouldBeTrue();
    }

    /// <summary>
    /// Verifies an unrecognized boolean attribute value is treated as false rather than rejected,
    /// matching upstream <c>Xml::attrToBool</c>, which never fails an xs:boolean attribute and
    /// instead treats anything other than <c>"1"</c> or a case-insensitive <c>"true"</c> as false.
    /// </summary>
    [Fact]
    public void Read_WhenBooleanAttributeIsUnrecognized_TreatsAsFalseWithoutThrowing()
    {
        const string xml = """
            <language name="GarbageBool" section="Sources" extensions="*.gb" version="1" kateversion="5.0" hidden="yes">
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

        var definition = SyntaxDefinitionReader.Read(xml);

        definition.Hidden.ShouldBeFalse();
    }

    /// <summary>
    /// Verifies an empty <c>&lt;contexts&gt;</c> element is rejected as a <see cref="FormatException"/>
    /// rather than letting <see cref="SyntaxDefinition"/>'s own internal <see cref="ArgumentException"/>
    /// escape this reader's documented exception contract.
    /// </summary>
    [Fact]
    public void Read_WhenContextsElementIsEmpty_ThrowsFormatException()
    {
        const string xml = """
            <language name="NoContexts" section="Sources" extensions="*.nc" version="1" kateversion="5.0">
              <highlighting>
                <contexts/>
                <itemDatas>
                  <itemData name="Normal Text" defStyleNum="dsNormal"/>
                </itemDatas>
              </highlighting>
            </language>
            """;

        _ = Should.Throw<FormatException>(() => SyntaxDefinitionReader.Read(xml));
    }

    /// <summary>
    /// Verifies a numeric attribute too large for <see cref="int"/> is rejected as the documented
    /// <see cref="FormatException"/> rather than an undocumented <see cref="OverflowException"/>.
    /// </summary>
    [Fact]
    public void Read_WhenVersionOverflowsInt32_ThrowsFormatException()
    {
        const string xml = """
            <language name="Overflow" section="Sources" extensions="*.ov" version="99999999999999999999" kateversion="5.0">
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

        _ = Should.Throw<FormatException>(() => SyntaxDefinitionReader.Read(xml));
    }
}
