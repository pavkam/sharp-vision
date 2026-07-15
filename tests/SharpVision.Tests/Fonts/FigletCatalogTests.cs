// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Fonts;



/// <summary>Verifies the embedded audited 400-font catalog.</summary>
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
}
