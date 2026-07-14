// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

using SharpVision.Styling;
using SharpVision.Terminal.Protocols;

using Shouldly;

/// <summary>Verifies the embedded theme catalog discovers, orders, loads, and caches themes.</summary>
public sealed class ThemeCatalogTests
{
    /// <summary>Verifies the default catalog includes both built-in theme slugs.</summary>
    [Fact]
    public void Default_ContainsBuiltInDefaults()
    {
        ThemeCatalog catalog = ThemeCatalog.Default;

        catalog.Slugs.ShouldContain("default-dark");
        catalog.Slugs.ShouldContain("default-light");
    }

    /// <summary>Verifies catalog entries are ordered by (order, slug).</summary>
    [Fact]
    public void Entries_AreOrderedByOrderThenSlug()
    {
        IReadOnlyList<ThemeCatalogEntry> entries = ThemeCatalog.Default.Entries;

        entries[0].Slug.ShouldBe("default-dark"); // order 0
        entries[1].Slug.ShouldBe("default-light"); // order 1
    }

    /// <summary>Verifies loading the default dark theme reproduces its indexed role colors.</summary>
    [Fact]
    public void Load_WhenDefaultDark_ReproducesIndexedRoles()
    {
        Theme theme = ThemeCatalog.Default.Load("default-dark");

        theme.IsFrozen.ShouldBeTrue();
        theme.TryGetColor(ColorRole.Foreground, out Color fg).ShouldBeTrue();
        fg.ShouldBe(Color.Indexed(15));
        theme.TryGetColor(ColorRole.Background, out Color bg).ShouldBeTrue();
        bg.ShouldBe(Color.Indexed(0));
    }

    /// <summary>Verifies repeated loads of the same slug return the same cached instance.</summary>
    [Fact]
    public void Load_WhenCalledTwice_ReturnsSameInstance()
    {
        Theme first = ThemeCatalog.Default.Load("default-dark");
        Theme second = ThemeCatalog.Default.Load("default-dark");

        ReferenceEquals(first, second).ShouldBeTrue();
    }

    /// <summary>Verifies loading an unknown slug throws <see cref="KeyNotFoundException"/>.</summary>
    [Fact]
    public void Load_WhenUnknownSlug_Throws() =>
        Should.Throw<KeyNotFoundException>(() => ThemeCatalog.Default.Load("nope"));
}
