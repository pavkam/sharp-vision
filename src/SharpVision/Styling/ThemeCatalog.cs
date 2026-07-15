// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;


/// <summary>Discovers and loads the embedded theme resources shipped with SharpVision.</summary>
public sealed class ThemeCatalog
{
    private const string _prefix = "SharpVision.Styling.Themes.";
    private const string _suffix = ".theme.json";
    private readonly Lock _gate = new();
    private readonly Dictionary<string, string> _json = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Theme> _cache = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _orders = new(StringComparer.Ordinal);

    private ThemeCatalog()
    {
        var assembly = typeof(ThemeCatalog).Assembly;
        List<ThemeCatalogEntry> entries = [];

        foreach (var resource in assembly.GetManifestResourceNames())
        {
            if (!resource.StartsWith(_prefix, StringComparison.Ordinal) ||
                !resource.EndsWith(_suffix, StringComparison.Ordinal))
            {
                continue;
            }

            var json = ReadResource(assembly, resource);
            var definition = ThemeLoader.Deserialize(json, resource);
            var entry = ToEntry(definition, resource);

            if (!_json.TryAdd(entry.Slug, json))
            {
                throw new InvalidDataException($"Duplicate theme slug '{entry.Slug}'.");
            }

            entries.Add(entry);
        }

        entries.Sort((left, right) =>
        {
            var byOrder = ByOrder(left.Slug).CompareTo(ByOrder(right.Slug));
            return byOrder != 0 ? byOrder : string.CompareOrdinal(left.Slug, right.Slug);
        });

        Entries = entries;
        Slugs = entries.ConvertAll(static e => e.Slug);

        int ByOrder(string slug) => _orders[slug];
    }

    /// <summary>Gets the process-wide embedded theme catalog.</summary>
    public static ThemeCatalog Default { get; } = new();

    /// <summary>Gets the theme metadata entries ordered by (order, slug).</summary>
    public IReadOnlyList<ThemeCatalogEntry> Entries { get; }

    /// <summary>Gets the ordered theme slugs.</summary>
    public IReadOnlyList<string> Slugs { get; }

    /// <summary>Loads and freezes one theme by slug, caching the result.</summary>
    /// <param name="slug">The catalog slug.</param>
    /// <returns>The frozen theme; the same instance on repeated calls.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="slug"/> is null.</exception>
    /// <exception cref="KeyNotFoundException">The slug is not in the catalog.</exception>
    public Theme Load(string slug)
    {
        ArgumentNullException.ThrowIfNull(slug);

        lock (_gate)
        {
            if (_cache.TryGetValue(slug, out var cached))
            {
                return cached;
            }

            if (!_json.TryGetValue(slug, out var json))
            {
                throw new KeyNotFoundException($"The theme catalog does not contain '{slug}'.");
            }

            var theme = ThemeLoader.FromJson(json, slug);
            _cache[slug] = theme;
            return theme;
        }
    }

    private ThemeCatalogEntry ToEntry(ThemeDefinition definition, string resource)
    {
        var slug = Require(definition.Slug, resource, "slug");
        _orders[slug] = definition.Order;
        return new ThemeCatalogEntry(
            Require(definition.Name, resource, "name"),
            slug,
            ParseColorScheme(definition.ColorScheme, resource),
            Require(definition.Author, resource, "author"),
            Require(definition.License, resource, "license"),
            Require(definition.Source, resource, "source"));
    }

    private static ColorScheme ParseColorScheme(string? value, string resource) => value switch
    {
        "dark" => ColorScheme.Dark,
        "light" => ColorScheme.Light,
        _ => throw new InvalidDataException($"Theme '{resource}' has invalid colorScheme '{value}'."),
    };

    private static string Require(string? value, string resource, string field) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new InvalidDataException($"Theme '{resource}' is missing required field '{field}'.")
            : value;

    private static string ReadResource(Assembly assembly, string name)
    {
        using var stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidDataException($"Embedded theme resource '{name}' is missing.");
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }
}
