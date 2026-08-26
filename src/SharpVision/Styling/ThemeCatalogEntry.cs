// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Immutable metadata for one embedded theme, independent of loading it.</summary>
[PublicAPI]
public sealed class ThemeCatalogEntry
{
    /// <summary>Initializes a catalog entry.</summary>
    /// <param name="name">The display name.</param>
    /// <param name="slug">The stable catalog key.</param>
    /// <param name="colorScheme">The dark/light color scheme.</param>
    /// <param name="author">The attribution author.</param>
    /// <param name="license">The license identifier.</param>
    /// <param name="order">The declared catalog sort order.</param>
    /// <param name="source">The source URL.</param>
    /// <exception cref="ArgumentException">A required string is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="colorScheme"/> is undefined.</exception>
    public ThemeCatalogEntry(string name, string slug, ColorScheme colorScheme, string author, string license,
        string source, int order = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentException.ThrowIfNullOrWhiteSpace(author);
        ArgumentException.ThrowIfNullOrWhiteSpace(license);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        _ = ThemeSlug.Validate(slug, nameof(slug));

        ArgumentOutOfRangeException.ThrowIfNotDefined(colorScheme, nameof(colorScheme), "The theme catalog color scheme is unknown.");

        Name = name;
        Slug = slug;
        ColorScheme = colorScheme;
        Author = author;
        License = license;
        Source = source;
        Order = order;
    }

    /// <summary>Gets the display name.</summary>
    public string Name { get; }

    /// <summary>Gets the stable catalog key.</summary>
    public string Slug { get; }

    /// <summary>Gets the dark/light color scheme.</summary>
    public ColorScheme ColorScheme { get; }

    /// <summary>Gets the attribution author.</summary>
    public string Author { get; }

    /// <summary>Gets the declared catalog sort order.</summary>
    public int Order { get; }

    /// <summary>Gets the license identifier.</summary>
    public string License { get; }

    /// <summary>Gets the source URL.</summary>
    public string Source { get; }
}
