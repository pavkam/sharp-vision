// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

using SharpVision.Styling;
using SharpVision.Terminal.Protocols;

using Shouldly;

/// <summary>Verifies every embedded theme loads and the curated set is complete.</summary>
public sealed class CuratedThemesTests
{
    private static readonly string[] _expected =
    [
        "default-dark", "default-light", "tokyo-night", "tokyo-night-storm",
        "tokyo-night-day", "catppuccin-mocha", "catppuccin-latte", "gruvbox-dark",
        "gruvbox-light", "dracula", "nord", "monokai", "solarized-dark",
        "solarized-light", "one-dark",
    ];

    /// <summary>Verifies the catalog contains exactly the curated slug set plus the two built-in defaults.</summary>
    [Fact]
    public void Catalog_ContainsExactlyTheCuratedSet()
    {
        ThemeCatalog.Default.Slugs.OrderBy(static s => s, StringComparer.Ordinal)
            .ShouldBe(_expected.OrderBy(static s => s, StringComparer.Ordinal));
    }

    /// <summary>Verifies every catalog theme loads frozen and exposes all twelve color roles.</summary>
    [Fact]
    public void EveryTheme_LoadsFrozenWithAllRoles()
    {
        foreach (string slug in ThemeCatalog.Default.Slugs)
        {
            Theme theme = ThemeCatalog.Default.Load(slug);
            theme.IsFrozen.ShouldBeTrue();

            foreach (ColorRole role in Enum.GetValues<ColorRole>())
            {
                theme.TryGetColor(role, out _).ShouldBeTrue($"{slug} missing {role}");
            }
        }
    }

    /// <summary>Verifies editor themes resolve their accent to an absolute RGB color.</summary>
    [Fact]
    public void EditorThemes_UseRgbAccents()
    {
        ThemeCatalog.Default.Load("dracula").TryGetColor(ColorRole.Accent, out Color accent).ShouldBeTrue();
        accent.Kind.ShouldBe(ColorKind.Rgb);
    }
}
