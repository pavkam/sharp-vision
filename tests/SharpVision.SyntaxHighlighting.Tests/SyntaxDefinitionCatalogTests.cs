// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting.Tests;

/// <summary>Verifies the embedded and directory-backed syntax-definition catalogs.</summary>
public sealed class SyntaxDefinitionCatalogTests
{
    /// <summary>Verifies the embedded catalog contains exactly the audited definition count.</summary>
    [Fact]
    public void Default_WhenInspected_ContainsExactlyTheAuditedDefinitionCount() => SyntaxDefinitionCatalog.Default.Names.Count.ShouldBe(159);

    /// <summary>Verifies unstated-license and copyleft upstream languages are excluded from the embedded catalog.</summary>
    [Fact]
    public void Default_WhenInspected_ExcludesUnstatedAndCopyleftLanguages()
    {
        var names = SyntaxDefinitionCatalog.Default.Names;

        // C, C#, Python, and JSON carry no stated or a copyleft upstream license and must never
        // be embedded by this package; see extern/kde-syntax-highlighting/README.md.
        names.ShouldNotContain("C");
        names.ShouldNotContain("C#");
        names.ShouldNotContain("Python");
        names.ShouldNotContain("JSON");
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

    /// <summary>Verifies an unrecognized extension resolves to null.</summary>
    [Fact]
    public void FindNameForFile_WhenNoExtensionMatches_ReturnsNull() => SyntaxDefinitionCatalog.Default.FindNameForFile("file.not-a-real-extension").ShouldBeNull();

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
}
