// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

/// <summary>Verifies the embedded theme catalog discovers, orders, loads, and caches themes.</summary>
public sealed class ThemeCatalogTests
{
    /// <summary>Verifies the default catalog includes both built-in theme slugs.</summary>
    [Fact]
    public void Default_ContainsBuiltInDefaults()
    {
        ThemeCatalog.Slugs.ShouldContain("default-dark");
        ThemeCatalog.Slugs.ShouldContain("default-light");
    }

    /// <summary>Verifies catalog entries are ordered by (order, slug).</summary>
    [Fact]
    public void Entries_AreOrderedByOrderThenSlug()
    {
        var entries = ThemeCatalog.Entries;

        entries[0].Slug.ShouldBe("default-dark"); // order 0
        entries[1].Slug.ShouldBe("default-light"); // order 1
    }

    /// <summary>Verifies loading the default dark theme produces complete normal control colors.</summary>
    [Fact]
    public void Load_WhenDefaultDark_ProducesCompleteNormalControlColors()
    {
        var theme = ThemeCatalog.Load("default-dark");

        theme.Frozen.ShouldBeTrue();
        theme.Resolve(theme.Control.Normal.Face.Foreground).IsRgb.ShouldBeTrue();
        theme.Resolve(theme.Control.Normal.Face.Background).IsRgb.ShouldBeTrue();
    }

    /// <summary>Verifies repeated loads of the same slug return the same cached instance.</summary>
    [Fact]
    public void Load_WhenCalledTwice_ReturnsSameInstance()
    {
        var first = ThemeCatalog.Load("default-dark");
        var second = ThemeCatalog.Load("default-dark");

        ReferenceEquals(first, second).ShouldBeTrue();
    }

    /// <summary>Verifies loading an unknown slug throws <see cref="KeyNotFoundException"/>.</summary>
    [Fact]
    public void Load_WhenUnknownSlug_Throws() =>
        Should.Throw<KeyNotFoundException>(() => ThemeCatalog.Load("nope"));

    /// <summary>Verifies no two embedded themes share a slug.</summary>
    [Fact]
    public void Slugs_AreUnique() =>
        ThemeCatalog.Slugs.Distinct(StringComparer.Ordinal).Count().ShouldBe(ThemeCatalog.Slugs.Count);

    /// <summary>Verifies theme loading has one public entry point backed by a JSON definition model.</summary>
    [Fact]
    public void PublicSurface_WhenInspected_ExposesOnlyTheCatalogLoader()
    {
        _ = typeof(ThemeCatalog).GetMethod(nameof(ThemeCatalog.Parse), [typeof(string)]).ShouldNotBeNull();
        _ = typeof(ThemeCatalog).Assembly.GetType("SharpVision.Styling.ThemeDocument").ShouldNotBeNull();

        // The DTO stays internal - a theme document is parsed through ThemeCatalog, never handed to
        // a caller as a model. The alternative entry points below have never existed and must not
        // appear: one loader, one document shape.
        typeof(ThemeCatalog).Assembly.GetType("SharpVision.Styling.ThemeDocument")!.IsPublic.ShouldBeFalse();
        typeof(ThemeCatalog).Assembly.GetType("SharpVision.Styling.ThemeFile").ShouldBeNull();
        typeof(ThemeCatalog).Assembly.GetType("SharpVision.Styling.ThemeLoader").ShouldBeNull();
    }
}
