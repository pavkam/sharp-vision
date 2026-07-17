// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Represents an immutable semantic UI palette and its provenance.</summary>
public sealed class Theme
{
    private readonly Dictionary<ColorRole, Color> _colors = [];

    /// <summary>Initializes a complete glyph palette and theme metadata.</summary>
    /// <param name="glyphs">The complete immutable semantic glyph palette.</param>
    /// <param name="version">The theme schema version.</param>
    /// <param name="name">The display name.</param>
    /// <param name="slug">The stable catalog slug.</param>
    /// <param name="colorScheme">The intended light or dark color scheme.</param>
    /// <param name="author">The attribution author.</param>
    /// <param name="license">The palette license identifier.</param>
    /// <param name="source">The palette source URL.</param>
    /// <exception cref="ArgumentNullException"><paramref name="glyphs"/> is null.</exception>
    public Theme(
        ThemeGlyphs glyphs,
        int version = 2,
        string name = "Custom",
        string slug = "custom",
        ColorScheme colorScheme = ColorScheme.Dark,
        string author = "SharpVision contributors",
        string license = "MIT",
        string source = "https://github.com/sharpvision/sharpvision")
    {
        ArgumentNullException.ThrowIfNull(glyphs);
        Glyphs = glyphs;
        SchemaVersion = version;
        Name = name;
        Slug = slug;
        ColorScheme = colorScheme;
        Author = author;
        License = license;
        Source = source;
    }

    /// <summary>Gets the document schema version.</summary>
    public int SchemaVersion { get; }

    /// <summary>Gets the display name.</summary>
    public string Name { get; }

    /// <summary>Gets the stable slug.</summary>
    public string Slug { get; }

    /// <summary>Gets the intended colour scheme.</summary>
    public ColorScheme ColorScheme { get; }

    /// <summary>Gets author attribution.</summary>
    public string Author { get; }

    /// <summary>Gets the license identifier.</summary>
    public string License { get; }

    /// <summary>Gets the source URL.</summary>
    public string Source { get; }

    /// <summary>Gets whether the palette is immutable.</summary>
    public bool IsFrozen { get; private set; }

    /// <summary>Gets the palette revision.</summary>
    public int Version { get; private set; }

    /// <summary>Gets a complete immutable palette snapshot.</summary>
    public ThemePalette Palette => new(_colors);

    /// <summary>Gets the complete immutable semantic glyph palette.</summary>
    public ThemeGlyphs Glyphs { get; }

    /// <summary>Sets one concrete colour role before the theme freezes.</summary>
    public void SetColor(ColorRole role, Color color)
    {
        if (IsFrozen)
        {
            throw new InvalidOperationException("Theme is frozen.");
        }

        _colors[role] = color;
        Version++;
    }

    /// <summary>Attempts to resolve a semantic role.</summary>
    public bool TryGetColor(ColorRole role, out Color color) => _colors.TryGetValue(role, out color);

    /// <summary>Resolves a UI colour token to its concrete terminal colour.</summary>
    public Color Resolve(ThemeColor color)
    {
        return color.TryGetColor(out var concrete)
            ? concrete
            : color.TryGetRole(out var role) && TryGetColor(role, out var resolved)
            ? resolved
            : Color.Default;
    }

    /// <summary>Prevents further palette mutation.</summary>
    public void Freeze() => IsFrozen = true;
}
