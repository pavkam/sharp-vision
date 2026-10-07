// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting.Tests;

/// <summary>Verifies the embedded and directory-backed syntax-definition catalogs.</summary>
public sealed class SyntaxDefinitionCatalogTests
{
    /// <summary>Verifies the embedded catalog contains exactly the audited definition count.</summary>
    [Fact]
    public void Default_WhenInspected_ContainsExactlyTheAuditedDefinitionCount() => SyntaxDefinitionCatalog.Default.Names.Count.ShouldBe(160);

    /// <summary>Verifies unstated-license and copyleft upstream languages are excluded from the embedded catalog.</summary>
    [Fact]
    public void Default_WhenInspected_ExcludesUnstatedAndCopyleftLanguages()
    {
        var names = SyntaxDefinitionCatalog.Default.Names;

        // C, Python, and JSON carry no stated or a copyleft upstream license and must never be
        // embedded from upstream by this package; see extern/kde-syntax-highlighting/README.md.
        // C# is present (see the dedicated first-party test below), but as this project's own
        // original definition, not upstream's unlicensed one.
        names.ShouldNotContain("C");
        names.ShouldNotContain("Python");
        names.ShouldNotContain("JSON");
    }

    /// <summary>Verifies the one first-party definition carries this repository's own provenance
    /// instead of the upstream KDE pin every other embedded definition carries, since upstream's
    /// own C# definition has no stated license and cannot be redistributed.</summary>
    [Fact]
    public void GetInfo_WhenNameIsCSharp_ReportsFirstPartyProvenance()
    {
        var info = SyntaxDefinitionCatalog.Default.GetInfo("C#");

        info.License.ShouldBe("MIT");
        info.SourceRepository.ShouldNotBeNullOrWhiteSpace();
        info.SourceRepository.ShouldNotBe("https://github.com/KDE/syntax-highlighting");
        info.SourceCommit.ShouldBeEmpty();
    }

    /// <summary>Verifies lazy inventory metadata exposes every hidden definition without parsing them.</summary>
    [Fact]
    public void GetInfo_WhenEmbeddedInventoryContainsHiddenDefinitions_ReportsAllWithoutParsingDefinitions()
    {
        var catalog = SyntaxDefinitionCatalog.CreateEmbedded();
        var before = catalog.DefinitionParseCount;

        var hidden = catalog.Names.Where(name => catalog.GetInfo(name).Hidden).ToArray();

        hidden.ShouldBe(["Alerts", "Comments", "Modelines", "SPDX-Comments"]);
        catalog.DefinitionParseCount.ShouldBe(before);
    }

    /// <summary>Verifies every embedded generalized style is available from lazy inventory metadata.</summary>
    [Fact]
    public void GetInfo_WhenEmbeddedDefinitionsDeclareStyles_ReportsAllWithoutParsingDefinitions()
    {
        var catalog = SyntaxDefinitionCatalog.CreateEmbedded();
        var before = catalog.DefinitionParseCount;

        var styles = catalog.Names
            .Select(name => (Name: name, catalog.GetInfo(name).Style))
            .Where(static entry => entry.Style.Length > 0)
            .ToArray();

        styles.ShouldBe(
        [
            ("Elixir/EEx", "eex"),
            ("Elixir/HEEx", "heex"),
            ("Elm", "elm"),
            ("GTK Blueprint", "gtkblueprint"),
            ("Julia", "julia"),
            ("PureScript", "haskell"),
            ("Snakemake", "snakemake"),
            ("Swift", "cstyle"),
        ]);
        catalog.DefinitionParseCount.ShouldBe(before);
    }

    /// <summary>Verifies parsed style metadata reaches directory-backed inventory records.</summary>
    [Fact]
    public void GetInfo_WhenDirectoryDefinitionDeclaresStyleAndHidden_PreservesBothValues()
    {
        var directory = Directory.CreateTempSubdirectory("sharpvision-syntax-catalog-metadata-");

        try
        {
            File.WriteAllText(
                Path.Combine(directory.FullName, "metadata.xml"),
                CreateLanguage("Metadata", "*.meta").Replace(
                    "kateversion=\"5.0\"",
                    "kateversion=\"5.0\" style=\"haskell\" hidden=\"true\"",
                    StringComparison.Ordinal));

            var info = SyntaxDefinitionCatalog.FromDirectory(directory.FullName).GetInfo("Metadata");

            info.Style.ShouldBe("haskell");
            info.Hidden.ShouldBeTrue();
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>Verifies loading the same definition twice reads its embedded resource only once.</summary>
    [Fact]
    public void GetDefinition_WhenCalledTwiceForTheSameName_ReadsTheEmbeddedResourceOnlyOnce()
    {
        // Default is a process-wide singleton another test may have already warmed up, so this
        // asserts the more robust invariant directly: the second call never reads again, whatever
        // the first call's own effect on the shared cache was.
        var catalog = SyntaxDefinitionCatalog.Default;

        _ = catalog.GetDefinition("Rust");
        var afterFirstCall = catalog.EmbeddedResourceReadCount;

        _ = catalog.GetDefinition("Rust");

        catalog.EmbeddedResourceReadCount.ShouldBe(afterFirstCall);
    }

    /// <summary>Verifies an unknown name is rejected with KeyNotFoundException.</summary>
    [Fact]
    public void GetDefinition_WhenNameIsUnknown_ThrowsKeyNotFoundException() => _ = Should.Throw<KeyNotFoundException>(() => SyntaxDefinitionCatalog.Default.GetDefinition("NoSuchLanguage"));

    /// <summary>Verifies an unknown name is rejected with KeyNotFoundException.</summary>
    [Fact]
    public void GetGrammar_WhenNameIsUnknown_ThrowsKeyNotFoundException() => _ = Should.Throw<KeyNotFoundException>(() => SyntaxDefinitionCatalog.Default.GetGrammar("NoSuchLanguage"));

    /// <summary>Verifies a null name is rejected on every lookup member.</summary>
    [Fact]
    public void Lookups_WhenNameIsNull_ThrowArgumentNullException()
    {
        var catalog = SyntaxDefinitionCatalog.Default;

        _ = Should.Throw<ArgumentNullException>(() => catalog.GetInfo(null!));
        _ = Should.Throw<ArgumentNullException>(() => catalog.FindNameForFile(null!));
        _ = Should.Throw<ArgumentNullException>(() => catalog.GetDefinition(null!));
        _ = Should.Throw<ArgumentNullException>(() => catalog.GetGrammar(null!));
        _ = Should.Throw<ArgumentNullException>(() => SyntaxDefinitionCatalog.FromDirectory(null!));
        _ = Should.Throw<ArgumentNullException>(() => catalog.Overlay(null!));
    }

    /// <summary>
    /// Verifies repeated calls for the same name return the exact same cached grammar instance
    /// rather than silently recompiling it, so every caller observes one consistent compilation.
    /// </summary>
    [Fact]
    public void GetGrammar_WhenCalledTwiceForTheSameName_ReturnsTheSameCachedInstance()
    {
        var catalog = SyntaxDefinitionCatalog.Default;

        var first = catalog.GetGrammar("Rust");
        var second = catalog.GetGrammar("Rust");

        second.ShouldBeSameAs(first);
    }

    /// <summary>Verifies the Rust grammar tokenizes a representative snippet.</summary>
    [Fact]
    public void GetGrammar_WhenLanguageIsRust_TokenizesARepresentativeSnippet()
    {
        var grammar = SyntaxDefinitionCatalog.Default.GetGrammar("Rust");
        var result = SyntaxTokenizer.Tokenize(grammar, "fn main() {\n    let x = 1;\n}\n");

        result.Lines.Count.ShouldBe(4);
        result.Lines[0].Tokens.ShouldContain(token => token.Style == SyntaxDefaultStyle.Keyword);
    }

    /// <summary>Verifies a ".rs" file name resolves to the Rust definition.</summary>
    [Fact]
    public void FindNameForFile_WhenExtensionMatchesRust_ReturnsRust() => SyntaxDefinitionCatalog.Default.FindNameForFile("main.rs").ShouldBe("Rust");

    /// <summary>Verifies overlapping embedded globs select the definition with the greatest KDE
    /// priority rather than whichever language name sorts first.</summary>
    [Fact]
    public void FindNameForFile_WhenSeveralDefinitionsMatch_UsesHighestPriority() =>
        SyntaxDefinitionCatalog.Default.FindNameForFile("service.log").ShouldBe("Log File (simplified) Selector");

    /// <summary>Verifies equal-priority overlapping definitions retain an ordinal-name tie-break,
    /// so filesystem enumeration order cannot make detection nondeterministic.</summary>
    [Fact]
    public void FindNameForFile_WhenPrioritiesTie_UsesOrdinalNameAsDeterministicTieBreak()
    {
        var directory = Directory.CreateTempSubdirectory("sharpvision-syntax-priority-");

        try
        {
            File.WriteAllText(Path.Combine(directory.FullName, "z.xml"), CreateLanguage("Zulu", "*.same", priority: 4));
            File.WriteAllText(Path.Combine(directory.FullName, "a.xml"), CreateLanguage("Alpha", "*.same", priority: 4));

            var catalog = SyntaxDefinitionCatalog.FromDirectory(directory.FullName);

            catalog.FindNameForFile("file.same").ShouldBe("Alpha");
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>Verifies an unrecognized extension resolves to null.</summary>
    [Fact]
    public void FindNameForFile_WhenNoExtensionMatches_ReturnsNull() => SyntaxDefinitionCatalog.Default.FindNameForFile("file.not-a-real-extension").ShouldBeNull();

    /// <summary>Verifies wildcards consume a complete file name, including newline code units, and treat other punctuation literally.</summary>
    [Theory]
    [InlineData("*.ext", "file.ext\n", false)]
    [InlineData("*.ext", "fi\nle.ext", true)]
    [InlineData("?.ext", "\n.ext", true)]
    [InlineData("?.ext", "a.ext\n", false)]
    [InlineData("file.ext", "file.ext\n", false)]
    [InlineData("*.ext", "FILE.EXT", true)]
    [InlineData("a?b*?.ext", "a\nbxy.ext", true)]
    [InlineData("x[ab].ext", "xa.ext", false)]
    [InlineData("x[ab].ext", "x[ab].ext", true)]
    [InlineData("?.ext", "ab.ext", false)]
    [InlineData("*?*.ext", ".ext", false)]
    [InlineData("*", "", true)]
    [InlineData("a**b?.ext", "abx.ext", true)]
    [InlineData("a*bc.ext", "axbbbcbc.ext", true)]
    [InlineData("Δ?.ext", "δx.EXT", true)]
    [InlineData("\u13A0?.ext", "\uAB70x.EXT", true)]
    [InlineData("\uAB70?.ext", "\u13A0x.EXT", true)]
    public void FindNameForFile_WhenWildcardHasBoundaryCases_MatchesWholeName(
        string pattern, string fileName, bool expectedMatch)
    {
        var directory = Directory.CreateTempSubdirectory("sharpvision-syntax-glob-");

        try
        {
            File.WriteAllText(Path.Combine(directory.FullName, "pattern.xml"), CreateLanguage("Pattern", pattern));
            var catalog = SyntaxDefinitionCatalog.FromDirectory(directory.FullName);

            var name = catalog.FindNameForFile(fileName);

            name.ShouldBe(expectedMatch ? "Pattern" : null);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>Verifies multiple star candidates cannot trigger regex backtracking failure during detection.</summary>
    [Fact]
    public void FindNameForFile_WhenManyStarsCannotMatch_ReturnsNullWithoutTimeout()
    {
        var directory = Directory.CreateTempSubdirectory("sharpvision-syntax-glob-");
        var pattern = string.Concat(Enumerable.Repeat("*a", 24)) + "b.ext";
        var fileName = new string('a', 128) + ".ext";

        try
        {
            File.WriteAllText(Path.Combine(directory.FullName, "pattern.xml"), CreateLanguage("Pattern", pattern));
            var catalog = SyntaxDefinitionCatalog.FromDirectory(directory.FullName);

            catalog.FindNameForFile(fileName).ShouldBeNull();
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>Verifies seeded wildcard combinations agree with an independent dynamic-programming matcher.</summary>
    [Fact]
    public void FindNameForFile_WhenPatternsAreRandomized_MatchesWildcardOracle()
    {
        const int seed = 0x61b012;
        const string patternAlphabet = "abAB.*?[]";
        const string nameAlphabet = "abAB.\n[]";
        var random = new Random(seed);
        var directory = Directory.CreateTempSubdirectory("sharpvision-syntax-glob-");

        try
        {
            for (var iteration = 0; iteration < 64; iteration++)
            {
                var pattern = new string([.. Enumerable.Range(0, random.Next(1, 13))
                    .Select(_ => patternAlphabet[random.Next(patternAlphabet.Length)])]);
                File.WriteAllText(Path.Combine(directory.FullName, "pattern.xml"), CreateLanguage("Pattern", pattern));
                var catalog = SyntaxDefinitionCatalog.FromDirectory(directory.FullName);

                for (var sample = 0; sample < 32; sample++)
                {
                    var fileName = new string([.. Enumerable.Range(0, random.Next(17))
                        .Select(_ => nameAlphabet[random.Next(nameAlphabet.Length)])]);
                    var expected = MatchesWildcardOracle(pattern, fileName) ? "Pattern" : null;

                    catalog.FindNameForFile(fileName).ShouldBe(expected,
                        $"Seed {seed}, pattern {iteration}, sample {sample}.");
                }
            }
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>Verifies an external directory's definitions load and compile.</summary>
    [Fact]
    public void FromDirectory_WhenGivenExternalDefinitions_LoadsAndCompilesThem()
    {
        var directory = Directory.CreateTempSubdirectory("sharpvision-syntax-catalog-");

        try
        {
            File.WriteAllText(
                Path.Combine(directory.FullName, "external.xml"),
                """
                <language name="ExternalTest" section="Sources" extensions="*.ext" version="1" kateversion="5.0">
                  <highlighting>
                    <contexts>
                      <context name="Normal" attribute="Normal Text" lineEndContext="#stay"/>
                    </contexts>
                    <itemDatas>
                      <itemData name="Normal Text" defStyleNum="dsNormal"/>
                    </itemDatas>
                  </highlighting>
                </language>
                """);

            var catalog = SyntaxDefinitionCatalog.FromDirectory(directory.FullName);

            catalog.Names.ShouldBe(["ExternalTest"]);
            catalog.GetInfo("ExternalTest").License.ShouldBe(string.Empty);
            _ = catalog.GetGrammar("ExternalTest");
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>Verifies one external catalog augments the embedded inventory and can resolve a
    /// cross-definition target through the combined compilation session.</summary>
    [Fact]
    public void Overlay_WhenExternalDefinitionReferencesBuiltIn_ResolvesBothCatalogs()
    {
        var directory = Directory.CreateTempSubdirectory("sharpvision-syntax-overlay-");

        try
        {
            File.WriteAllText(
                Path.Combine(directory.FullName, "external.xml"),
                """
                <language name="ExternalTest" section="Sources" extensions="*.ext" version="1" kateversion="5.0">
                  <highlighting>
                    <contexts>
                      <context name="Normal" attribute="Normal Text" lineEndContext="#stay">
                        <DetectChar attribute="Normal Text" context="Normal##Rust" char="x"/>
                      </context>
                    </contexts>
                    <itemDatas>
                      <itemData name="Normal Text" defStyleNum="dsNormal"/>
                    </itemDatas>
                  </highlighting>
                </language>
                """);
            var external = SyntaxDefinitionCatalog.FromDirectory(directory.FullName);

            var catalog = SyntaxDefinitionCatalog.Default.Overlay(external);

            catalog.Names.ShouldContain("Rust");
            catalog.Names.ShouldContain("ExternalTest");
            var target = catalog.GetGrammar("ExternalTest").Contexts[0].Rules[0].ResolvedTarget.Pushes.ShouldHaveSingleItem();
            target.Grammar.ShouldBeSameAs(catalog.GetGrammar("Rust"));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>Verifies overlay definitions replace same-name base entries deterministically.</summary>
    [Fact]
    public void Overlay_WhenAdditionHasSameName_AdditionTakesPrecedence()
    {
        var directory = Directory.CreateTempSubdirectory("sharpvision-syntax-overlay-precedence-");

        try
        {
            File.WriteAllText(Path.Combine(directory.FullName, "rust.xml"), CreateLanguage("Rust", "*.replacement"));
            var additions = SyntaxDefinitionCatalog.FromDirectory(directory.FullName);

            var catalog = SyntaxDefinitionCatalog.Default.Overlay(additions);

            catalog.GetDefinition("Rust").Extensions.ShouldBe(["*.replacement"]);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>Verifies directory construction retains the already-validated definitions, so a
    /// later lookup does not parse the same complete XML document a second time.</summary>
    [Fact]
    public void FromDirectory_WhenDefinitionIsRetrieved_DoesNotReparseItsXml()
    {
        var directory = Directory.CreateTempSubdirectory("sharpvision-syntax-retained-");

        try
        {
            File.WriteAllText(Path.Combine(directory.FullName, "retained.xml"), CreateLanguage("Retained", "*.retained"));
            var catalog = SyntaxDefinitionCatalog.FromDirectory(directory.FullName);

            catalog.DefinitionParseCount.ShouldBe(1);

            _ = catalog.GetDefinition("Retained");

            catalog.DefinitionParseCount.ShouldBe(1);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>Verifies a grammar reached through a cross-definition context target is the exact
    /// catalog-owned instance returned by direct lookup of that target definition.</summary>
    [Fact]
    public void GetGrammar_WhenCrossDefinitionTargetIsCompiled_SharesTheCatalogGrammarInstance()
    {
        var directory = Directory.CreateTempSubdirectory("sharpvision-syntax-shared-grammar-");

        try
        {
            File.WriteAllText(Path.Combine(directory.FullName, "b.xml"), CreateLanguage("Target", "*.target"));
            File.WriteAllText(
                Path.Combine(directory.FullName, "a.xml"),
                """
                <language name="Host" section="Sources" extensions="*.host" version="1" kateversion="5.0">
                  <highlighting>
                    <contexts>
                      <context name="Normal" attribute="Normal Text" lineEndContext="#stay">
                        <DetectChar attribute="Normal Text" context="Normal##Target" char="x"/>
                      </context>
                    </contexts>
                    <itemDatas>
                      <itemData name="Normal Text" defStyleNum="dsNormal"/>
                    </itemDatas>
                  </highlighting>
                </language>
                """);
            var catalog = SyntaxDefinitionCatalog.FromDirectory(directory.FullName);

            var host = catalog.GetGrammar("Host");
            var targetFromReference = host.Contexts[0].Rules[0].ResolvedTarget.Pushes.ShouldHaveSingleItem().Grammar;
            var targetFromLookup = catalog.GetGrammar("Target");

            targetFromReference.ShouldBeSameAs(targetFromLookup);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>Verifies racing first lookups publish one definition parse and one grammar
    /// compilation rather than merely returning one winner after duplicating the expensive work.</summary>
    [Fact]
    public async Task GetGrammar_WhenFirstLookupIsConcurrent_CompilesExactlyOnceAsync()
    {
        var embeddedCatalog = SyntaxDefinitionCatalog.CreateEmbedded();
        using var start = new ManualResetEventSlim(initialState: false);
        var definitionTasks = Enumerable.Range(0, 64).Select(_ => Task.Run(() =>
        {
            start.Wait();
            return embeddedCatalog.GetDefinition("C#");
        })).ToArray();

        start.Set();
        var definitions = await Task.WhenAll(definitionTasks);

        definitions.ShouldAllBe(definition => ReferenceEquals(definition, definitions[0]));
        embeddedCatalog.DefinitionParseCount.ShouldBe(1);

        var directory = Directory.CreateTempSubdirectory("sharpvision-syntax-concurrent-grammar-");

        try
        {
            File.WriteAllText(Path.Combine(directory.FullName, "single.xml"), CreateLanguage("Single", "*.single"));
            var catalog = SyntaxDefinitionCatalog.FromDirectory(directory.FullName);
            using var grammarStart = new ManualResetEventSlim(initialState: false);
            var grammarTasks = Enumerable.Range(0, 64).Select(_ => Task.Run(() =>
            {
                grammarStart.Wait();
                return catalog.GetGrammar("Single");
            })).ToArray();

            grammarStart.Set();
            var grammars = await Task.WhenAll(grammarTasks);

            grammars.ShouldAllBe(grammar => ReferenceEquals(grammar, grammars[0]));
            catalog.GrammarCompilationCount.ShouldBe(1);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>Verifies a directory with no XML files is rejected with ArgumentException.</summary>
    [Fact]
    public void FromDirectory_WhenGivenNoXmlFiles_ThrowsArgumentException()
    {
        var directory = Directory.CreateTempSubdirectory("sharpvision-syntax-catalog-empty-");

        try
        {
            _ = Should.Throw<ArgumentException>(() => SyntaxDefinitionCatalog.FromDirectory(directory.FullName));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>Verifies a directory that does not exist is rejected with DirectoryNotFoundException.</summary>
    [Fact]
    public void FromDirectory_WhenDirectoryDoesNotExist_ThrowsDirectoryNotFoundException()
    {
        var missing = Path.Combine(Path.GetTempPath(), "sharpvision-syntax-catalog-missing-" + Guid.NewGuid().ToString("N"));

        _ = Should.Throw<DirectoryNotFoundException>(() => SyntaxDefinitionCatalog.FromDirectory(missing));
    }

    /// <summary>Verifies a malformed XML file is rejected with FormatException instead of a raw XML exception.</summary>
    [Fact]
    public void FromDirectory_WhenAFileIsMalformedXml_ThrowsFormatException()
    {
        var directory = Directory.CreateTempSubdirectory("sharpvision-syntax-catalog-malformed-");

        try
        {
            File.WriteAllText(Path.Combine(directory.FullName, "broken.xml"), "<language name=\"Broken\"><unclosed>");

            _ = Should.Throw<FormatException>(() => SyntaxDefinitionCatalog.FromDirectory(directory.FullName));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>Verifies directory loading applies the reader's document-size bound while parsing
    /// the file stream instead of accepting an arbitrarily large authored document.</summary>
    [Fact]
    public void FromDirectory_WhenAFileExceedsTheDocumentBound_ThrowsFormatException()
    {
        var directory = Directory.CreateTempSubdirectory("sharpvision-syntax-catalog-oversized-");

        try
        {
            var oversizedComment = new string('x', 16_000_001);
            File.WriteAllText(
                Path.Combine(directory.FullName, "oversized.xml"),
                $"<language name=\"Oversized\" section=\"Sources\" extensions=\"*.huge\" version=\"1\"><!--{oversizedComment}--></language>");

            _ = Should.Throw<FormatException>(() => SyntaxDefinitionCatalog.FromDirectory(directory.FullName));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>Verifies two files declaring the same language name are rejected with ArgumentException.</summary>
    [Fact]
    public void FromDirectory_WhenTwoFilesDeclareTheSameLanguageName_ThrowsArgumentException()
    {
        var directory = Directory.CreateTempSubdirectory("sharpvision-syntax-catalog-duplicate-");

        const string language = """
            <language name="DuplicateTest" section="Sources" extensions="*.dup" version="1" kateversion="5.0">
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

        try
        {
            File.WriteAllText(Path.Combine(directory.FullName, "first.xml"), language);
            File.WriteAllText(Path.Combine(directory.FullName, "second.xml"), language);

            _ = Should.Throw<ArgumentException>(() => SyntaxDefinitionCatalog.FromDirectory(directory.FullName));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
    /// <summary>Evaluates every prefix pair independently so greedy-star decisions cannot bias the expected match.</summary>
    private static bool MatchesWildcardOracle(string pattern, string name)
    {
        var prefixes = new bool[pattern.Length + 1, name.Length + 1];
        prefixes[0, 0] = true;

        for (var patternLength = 1; patternLength <= pattern.Length; patternLength++)
        {
            var token = pattern[patternLength - 1];
            prefixes[patternLength, 0] = token == '*' && prefixes[patternLength - 1, 0];

            for (var nameLength = 1; nameLength <= name.Length; nameLength++)
            {
                prefixes[patternLength, nameLength] = token == '*'
                    ? prefixes[patternLength - 1, nameLength] || prefixes[patternLength, nameLength - 1]
                    : prefixes[patternLength - 1, nameLength - 1] &&
                      (token == '?' || char.ToUpperInvariant(token) == char.ToUpperInvariant(name[nameLength - 1]));
            }
        }

        return prefixes[pattern.Length, name.Length];
    }

    private static string CreateLanguage(string name, string extension, int? priority = null)
    {
        var priorityAttribute = priority is null ? string.Empty : $" priority=\"{priority.Value}\"";
        return $"""
            <language name="{name}" section="Sources" extensions="{extension}" version="1" kateversion="5.0"{priorityAttribute}>
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
    }
}
