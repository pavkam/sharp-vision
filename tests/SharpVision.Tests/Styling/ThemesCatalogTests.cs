// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

/// <summary>Verifies the embedded theme catalog discovers, orders, loads, and caches themes.</summary>
public sealed class ThemesCatalogTests
{
    /// <summary>Verifies the default catalog includes both built-in theme slugs.</summary>
    [Fact]
    public void Default_ContainsBuiltInDefaults()
    {
        Themes.Slugs.ShouldContain("default-dark");
        Themes.Slugs.ShouldContain("default-light");
    }

    /// <summary>Verifies catalog entries are ordered by (order, slug).</summary>
    [Fact]
    public void Entries_AreOrderedByOrderThenSlug()
    {
        var entries = Themes.Entries;

        entries[0].Slug.ShouldBe("default-dark"); // order 0
        entries[1].Slug.ShouldBe("default-light"); // order 1
    }

    /// <summary>Verifies loading the default dark theme produces complete normal control colors.</summary>
    [Fact]
    public void Load_WhenDefaultDark_ProducesCompleteNormalControlColors()
    {
        var theme = Themes.Load("default-dark");

        theme.IsFrozen.ShouldBeTrue();
        theme.Resolve(theme.Control.Normal.Face.Foreground).IsRgb.ShouldBeTrue();
        theme.Resolve(theme.Control.Normal.Face.Background).IsRgb.ShouldBeTrue();
    }

    /// <summary>Verifies repeated loads of the same slug return the same cached instance.</summary>
    [Fact]
    public void Load_WhenCalledTwice_ReturnsSameInstance()
    {
        var first = Themes.Load("default-dark");
        var second = Themes.Load("default-dark");

        ReferenceEquals(first, second).ShouldBeTrue();
    }

    /// <summary>Verifies loading an unknown slug throws <see cref="KeyNotFoundException"/>.</summary>
    [Fact]
    public void Load_WhenUnknownSlug_Throws() =>
        Should.Throw<KeyNotFoundException>(() => Themes.Load("nope"));

    /// <summary>Verifies no two embedded themes share a slug (see #250).</summary>
    [Fact]
    public void Slugs_AreUnique() =>
        Themes.Slugs.Distinct(StringComparer.Ordinal).Count().ShouldBe(Themes.Slugs.Count);

    /// <summary>Verifies theme loading has one public entry point backed by a JSON definition model.</summary>
    [Fact]
    public void PublicSurface_WhenInspected_ExposesOnlyThemesLoader()
    {
        _ = typeof(Themes).GetMethod(nameof(Themes.Parse), [typeof(string)]).ShouldNotBeNull();
        _ = typeof(Themes).Assembly.GetType("SharpVision.Styling.ThemeDefinition").ShouldNotBeNull();
        typeof(Themes).Assembly.GetType("SharpVision.Styling.ThemeCatalog").ShouldBeNull();
        typeof(Themes).Assembly.GetType("SharpVision.Styling.ThemeFile").ShouldBeNull();
    }
}
