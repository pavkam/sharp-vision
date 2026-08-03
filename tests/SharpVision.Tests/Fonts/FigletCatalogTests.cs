// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Fonts;

using System.IO.Compression;

/// <summary>Verifies the embedded audited 400-font catalog and the caller-supplied constructors.</summary>
public sealed class FigletCatalogTests
{
    #region Inventory

    /// <summary>Verifies the full audited collection is present and ordinally sorted.</summary>
    [Fact]
    public void Names_WhenCatalogLoads_ContainsFourHundredSortedFonts()
    {
        var names = FigletCatalog.Default.Names;

        names.Count.ShouldBe(400);
        names.ShouldBe(names.Order(StringComparer.Ordinal).ToArray());
        names.ShouldContain("Standard");
    }

    /// <summary>Verifies manifest provenance and hashes are exposed unchanged.</summary>
    [Fact]
    public void GetInfo_WhenFontExists_ReturnsAuditedMetadata()
    {
        var info = FigletCatalog.Default.GetInfo("Standard");

        info.File.ShouldBe("Standard.flf");
        info.Sha256.Length.ShouldBe(64);
        info.Bytes.ShouldBeGreaterThan(0);
        info.Notice.ShouldContain("Glenn Chappell");
    }

    /// <summary>Verifies lookup is exact and path-like input cannot address archive entries.</summary>
    [Theory]
    [InlineData("standard")]
    [InlineData("../Standard")]
    [InlineData("Standard.flf")]
    public void Load_WhenNameIsNotExact_ThrowsKeyNotFoundException(string name) =>
        _ = Should.Throw<KeyNotFoundException>(() => FigletCatalog.Default.Load(name));

    #endregion

    #region Parsing

    /// <summary>Verifies a representative catalog font renders through the public API.</summary>
    [Fact]
    public void Load_WhenStandardIsSelected_RendersText()
    {
        var font = FigletCatalog.Default.Load("Standard");

        var output = font.Render("Hi");

        output.ShouldBe(
            " _   _ _ \n" +
            "| | | (_)\n" +
            "| |_| | |\n" +
            "|  _  | |\n" +
            "|_| |_|_|\n" +
            "         ");
    }

    /// <summary>Verifies caller-supplied limits reach the loaded font and are re-enforced on render,
    /// letting callers tighten bounds for untrusted caller-supplied text (see #136).</summary>
    [Fact]
    public void Load_WhenLimitsAreExplicit_RetainsThemAndEnforcesThemOnRender()
    {
        var limits = new FigletLimits(maxOutputChars: 4);

        var font = FigletCatalog.Default.Load("Standard", limits);

        font.Limits.ShouldBe(limits);
        _ = Should.Throw<InvalidOperationException>(() => font.Render("Hi"));
    }

    /// <summary>Verifies every archived font can be opened and parsed safely.</summary>
    [Fact]
    public void Load_WhenEveryCatalogEntryIsSelected_ParsesAllFonts()
    {
        var catalog = FigletCatalog.Default;

        foreach (var name in catalog.Names)
        {
            _ = Should.NotThrow(() => catalog.Load(name), name);
        }
    }

    #endregion

    #region FromFonts

    /// <summary>Verifies a catalog built from already-parsed fonts exposes them by their own name (#13).</summary>
    [Fact]
    public void FromFonts_WhenGivenParsedFonts_ExposesThemByOwnName()
    {
        var one = FigletFont.Load(Stream(CreateFontText()), "One");
        var two = FigletFont.Load(Stream(CreateFontText()), "Two");

        var catalog = FigletCatalog.FromFonts([one, two]);

        catalog.Names.ShouldBe(["One", "Two"]);
        catalog.Load("One").ShouldBeSameAs(one);
        catalog.Load("Two").ShouldBeSameAs(two);
    }

    /// <summary>Verifies the public names view cannot rewrite the catalog's published inventory.</summary>
    [Fact]
    public void Names_WhenReadOnlyViewIsMutated_PreservesCatalogSnapshot()
    {
        var one = FigletFont.Load(Stream(CreateFontText()), "One");
        var two = FigletFont.Load(Stream(CreateFontText()), "Two");
        var catalog = FigletCatalog.FromFonts([one, two]);

        if (catalog.Names is string[] mutableNames)
        {
            mutableNames[0] = "Corrupted";
        }

        catalog.Names.ShouldBe(["One", "Two"]);
        catalog.Load("One").ShouldBeSameAs(one);
    }

    /// <summary>Verifies placeholder metadata is reported for an already-parsed font with no source file.</summary>
    [Fact]
    public void FromFonts_WhenInfoIsRequested_ReportsUnauditedPlaceholderMetadata()
    {
        var font = FigletFont.Load(Stream(CreateFontText()), "One");

        var catalog = FigletCatalog.FromFonts([font]);
        var info = catalog.GetInfo("One");

        info.License.ShouldBe("unaudited");
        info.Sha256.ShouldBeEmpty();
        info.Bytes.ShouldBe(0);
    }

    /// <summary>Verifies an empty sequence is rejected rather than producing an unusable catalog.</summary>
    [Fact]
    public void FromFonts_WhenSequenceIsEmpty_ThrowsArgumentException() =>
        _ = Should.Throw<ArgumentException>(() => FigletCatalog.FromFonts([]));

    /// <summary>Verifies a null element is rejected rather than silently skipped.</summary>
    [Fact]
    public void FromFonts_WhenSequenceContainsNull_ThrowsArgumentNullException() =>
        _ = Should.Throw<ArgumentNullException>(() => FigletCatalog.FromFonts([null!]));

    /// <summary>Verifies two fonts sharing a name are rejected rather than silently overwriting.</summary>
    [Fact]
    public void FromFonts_WhenTwoFontsShareAName_ThrowsArgumentException()
    {
        var first = FigletFont.Load(Stream(CreateFontText()), "Same");
        var second = FigletFont.Load(Stream(CreateFontText()), "Same");

        _ = Should.Throw<ArgumentException>(() => FigletCatalog.FromFonts([first, second]));
    }

    #endregion

    #region FromDirectory

    /// <summary>Verifies a catalog built from a directory names each font by its file stem (#13).</summary>
    [Fact]
    public void FromDirectory_WhenDirectoryContainsFontFiles_LoadsThemByFileNameStem()
    {
        var directory = CreateTempDirectory();

        try
        {
            File.WriteAllText(Path.Combine(directory, "Alpha.flf"), CreateFontText());
            File.WriteAllText(Path.Combine(directory, "Beta.tlf"), CreateFontText());
            File.WriteAllText(Path.Combine(directory, "ignored.txt"), "not a font");

            var catalog = FigletCatalog.FromDirectory(directory);

            catalog.Names.ShouldBe(["Alpha", "Beta"]);
            catalog.GetInfo("Alpha").Format.ShouldBe("flf");
            catalog.GetInfo("Beta").Format.ShouldBe("tlf");
            catalog.Load("Alpha").Name.ShouldBe("Alpha");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Verifies a directory with no font files is rejected rather than producing an empty catalog.</summary>
    [Fact]
    public void FromDirectory_WhenDirectoryHasNoFontFiles_ThrowsArgumentException()
    {
        var directory = CreateTempDirectory();

        try
        {
            _ = Should.Throw<ArgumentException>(() => FigletCatalog.FromDirectory(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    #endregion

    #region FromZip

    /// <summary>Verifies a catalog built from a Zip archive names each font by its entry stem (#13).</summary>
    [Fact]
    public void FromZip_WhenArchiveContainsFontEntries_LoadsThemByEntryNameStem()
    {
        using var archive = CreateZip(("Alpha.flf", CreateFontText()), ("Beta.tlf", CreateFontText()));

        var catalog = FigletCatalog.FromZip(archive);

        catalog.Names.ShouldBe(["Alpha", "Beta"]);
        catalog.Load("Alpha").Name.ShouldBe("Alpha");
        catalog.Load("Beta").Name.ShouldBe("Beta");
    }

    /// <summary>Verifies an archive with no font entries is rejected rather than producing an empty catalog.</summary>
    [Fact]
    public void FromZip_WhenArchiveHasNoFontEntries_ThrowsArgumentException()
    {
        using var archive = CreateZip(("readme.txt", "not a font"));

        _ = Should.Throw<ArgumentException>(() => FigletCatalog.FromZip(archive));
    }

    #endregion

    private static MemoryStream Stream(string content) => new(Encoding.UTF8.GetBytes(content));

    private static string CreateFontText()
    {
        var builder = new StringBuilder("flf2a$ 1 1 80 -1 1 0\nTest font by SharpVision\n");

        for (var code = 32; code <= 126; code++)
        {
            _ = builder.Append(char.ConvertFromUtf32(code)).Append("@@\n");
        }

        foreach (var code in new[] { 196, 214, 220, 228, 246, 252, 223 })
        {
            _ = builder.Append(char.ConvertFromUtf32(code)).Append("@@\n");
        }

        return builder.ToString();
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "sharp-vision-figlet-tests-" + Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(path);
        return path;
    }

    private static MemoryStream CreateZip(params (string EntryName, string Content)[] entries)
    {
        MemoryStream buffer = new();

        using (ZipArchive zip = new(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (entryName, content) in entries)
            {
                var entry = zip.CreateEntry(entryName);
                using var writer = new StreamWriter(entry.Open());
                writer.Write(content);
            }
        }

        buffer.Position = 0;
        return buffer;
    }
}
