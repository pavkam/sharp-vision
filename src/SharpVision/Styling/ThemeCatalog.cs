// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Discovers, loads, validates, and caches immutable SharpVision themes.</summary>
[PublicAPI]
public static class ThemeCatalog
{
    internal const int MaximumDocumentBytes = 64 * 1024;
    private const int _maximumPaletteEntries = 256;
    private const int _maximumMetadataLength = 2048;
    private const string _resourcePrefix = "SharpVision.Styling.Themes.";
    private const string _resourceSuffix = ".theme.json";

    // The complete, closed vocabulary of a theme document's "styles" object. A leaf control style
    // (button, checkBox, ...) resolves entirely from its code-owned default, its declared one-hop
    // fallback to one of these six, and a locally assigned Style - never from a theme section of
    // its own - so admitting any other key, leaf or vendor-dotted, would let a theme author a
    // section that silently does nothing. This used to be a reflectively discovered registry of
    // every leaf style type's own key as well; that registry is gone along with the leaf keys it
    // validated, leaving exactly the six names every bundled theme already authors.
    private static readonly HashSet<string> _knownStyleSections =
        new(StringComparer.Ordinal) { "control", "input", "container", "window", "popup", "tooltip" };
    private static readonly Lock _gate = new();
    private static readonly Dictionary<string, Theme> _cache = new(StringComparer.Ordinal);

    // The full resource scan is deferred behind this Lazy so that touching one theme (including
    // the White/Dark defaults every ControlBase falls back to) never forces every embedded theme
    // to be read and parsed. ExecutionAndPublication is required, not just a nicety:
    // two threads racing to materialize this would otherwise both run BuildCatalog and could
    // observe two different (equally valid) winning instances handed out to concurrent callers.
    private static readonly Lazy<(IReadOnlyList<ThemeCatalogEntry> Entries, IReadOnlyList<string> Slugs, Dictionary<string, byte[]> Documents)> _catalog =
        new(BuildCatalog, LazyThreadSafetyMode.ExecutionAndPublication);

    // No DTO property across ThemeDocument and its nested types is enum-typed - every enum-like
    // field is parsed by hand via Enum.TryParse in the resolve methods below, so a
    // JsonStringEnumConverter here has never converted anything. It was also the one
    // [RequiresDynamicCode] member in this options instance. Internal (not private) so Theme's own
    // reflective fractional overlay reuses this exact configured instance for every
    // nested Deserialize call instead of an implicit, differently-configured default.
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        AllowDuplicateProperties = false,
        AllowTrailingCommas = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        MaxDepth = 8,
        RespectNullableAnnotations = true
    };

    private static readonly Dictionary<string, TerminalAttributes> _attributeNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["bold"] = TerminalAttributes.Bold,
            ["dim"] = TerminalAttributes.Dim,
            ["italic"] = TerminalAttributes.Italic,
            ["underline"] = TerminalAttributes.Underline,
            ["blink"] = TerminalAttributes.Blink,
            ["reverse"] = TerminalAttributes.Reverse,
            ["hidden"] = TerminalAttributes.Hidden,
            ["strike"] = TerminalAttributes.Strike,
            ["strikethrough"] = TerminalAttributes.Strike,
            ["overline"] = TerminalAttributes.Overline
        };

    // Shared by Theme.ResolveSectionDecoration, via the ConvertLeaf unification, and this
    // file's own ParseAttributes below, so the one literal-attribute-name vocabulary is defined
    // exactly once.
    [Pure]
    internal static TerminalAttributes ResolveAttributeName(string name, string source, string context) =>
        _attributeNames.TryGetValue(name, out var attribute)
            ? attribute
            : throw new InvalidDataException($"Theme '{source}' '{context}' has unknown attribute '{name}'.");

    static ThemeCatalog()
    {
        White = Load("default-light");
        Dark = Load("default-dark");
    }

    #region Catalog

    /// <summary>Gets embedded theme metadata ordered by authored order and then slug.</summary>
    public static IReadOnlyList<ThemeCatalogEntry> Entries => _catalog.Value.Entries;

    /// <summary>Gets embedded theme slugs in catalog order.</summary>
    public static IReadOnlyList<string> Slugs => _catalog.Value.Slugs;

    /// <summary>Gets the frozen light standard theme.</summary>
    public static Theme White { get; }

    /// <summary>Gets the frozen dark standard theme.</summary>
    public static Theme Dark { get; }

    /// <summary>Loads and caches one embedded theme by slug.</summary>
    /// <param name="slug">The exact embedded theme slug.</param>
    /// <returns>The cached frozen theme.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="slug"/> is null.</exception>
    /// <exception cref="KeyNotFoundException">The slug is not in the embedded catalog.</exception>
    public static Theme Load(string slug)
    {
        ArgumentNullException.ThrowIfNull(slug);

        lock (_gate)
        {
            if (_cache.TryGetValue(slug, out var cached))
            {
                return cached;
            }

            var theme = TryLoadDirect(slug) ?? LoadFromCatalog(slug);
            _cache[slug] = theme;
            return theme;
        }
    }

    // Reads and parses the one requested embedded resource directly, without scanning or
    // materializing the other 14 - this is what lets White/Dark (forced by every ControlBase
    // fallback) stay cheap. Falls back to null (never throws) so Load can still resolve slugs
    // whose filename doesn't match their internal slug via the full catalog scan.
    private static Theme? TryLoadDirect(string slug)
    {
        var assembly = typeof(ThemeCatalog).Assembly;
        var resourceName = _resourcePrefix + slug + _resourceSuffix;
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return null;
        }

        var theme = ParseUtf8(ReadBounded(stream, slug), slug, embedded: true);
        return string.Equals(theme.Slug, slug, StringComparison.Ordinal) ? theme : null;
    }

    private static Theme LoadFromCatalog(string slug) =>
        _catalog.Value.Documents.TryGetValue(slug, out var document)
            ? ParseUtf8(document, slug, embedded: true)
            : throw new KeyNotFoundException($"The theme catalog does not contain '{slug}'.");

    // Extracted from the former static constructor: only runs when something actually needs the
    // full catalog (Entries/Slugs, or a Load(slug) whose filename doesn't match its own internal
    // slug) rather than unconditionally on every process start.
    private static (IReadOnlyList<ThemeCatalogEntry> Entries, IReadOnlyList<string> Slugs, Dictionary<string, byte[]> Documents) BuildCatalog()
    {
        var assembly = typeof(ThemeCatalog).Assembly;
        var entries = new List<ThemeCatalogEntry>();
        var documents = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var orders = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var resource in assembly.GetManifestResourceNames())
        {
            if (!resource.StartsWith(_resourcePrefix, StringComparison.Ordinal) ||
                !resource.EndsWith(_resourceSuffix, StringComparison.Ordinal))
            {
                continue;
            }

            var document = ReadResource(assembly, resource);
            var entry = ReadCatalogEntry(document, resource, out var order);

            if (!documents.TryAdd(entry.Slug, document))
            {
                throw new InvalidDataException($"Duplicate theme slug '{entry.Slug}'.");
            }

            orders[entry.Slug] = order;
            entries.Add(entry);
        }

        entries.Sort((left, right) =>
        {
            var byOrder = orders[left.Slug].CompareTo(orders[right.Slug]);
            return byOrder != 0 ? byOrder : string.CompareOrdinal(left.Slug, right.Slug);
        });

        return (entries.AsReadOnly(), entries.ConvertAll(static entry => entry.Slug).AsReadOnly(), documents);
    }

    #endregion

    #region External loading

    /// <summary>Parses a frozen theme from bounded JSON text.</summary>
    /// <param name="json">The complete theme JSON document.</param>
    /// <returns>A newly parsed frozen theme.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="json"/> is null.</exception>
    /// <exception cref="InvalidDataException">The document is oversized or invalid.</exception>
    [Pure]
    public static Theme Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return Parse(json, "<parsed>");
    }

    /// <summary>Parses a frozen theme using an internal diagnostic source label.</summary>
    [Pure]
    internal static Theme Parse(string json, string source)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(source);
        return Encoding.UTF8.GetByteCount(json) > MaximumDocumentBytes
            ? throw DocumentTooLarge(source)
            : ParseUtf8(Encoding.UTF8.GetBytes(json), source, embedded: false);
    }

    /// <summary>Loads a frozen theme from the current position of a caller-owned UTF-8 stream.</summary>
    /// <param name="stream">The readable stream, which remains open.</param>
    /// <returns>A newly parsed frozen theme.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="stream"/> is unreadable.</exception>
    /// <exception cref="InvalidDataException">The document is oversized or invalid.</exception>
    public static Theme Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return !stream.CanRead
            ? throw new ArgumentException("The theme stream must be readable.", nameof(stream))
            : ParseUtf8(ReadBounded(stream, "<stream>"), "<stream>", embedded: false);
    }

    /// <summary>Loads a frozen theme from a UTF-8 JSON file.</summary>
    /// <param name="path">The theme file path.</param>
    /// <returns>A newly parsed frozen theme.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="path"/> is empty or malformed.</exception>
    /// <exception cref="UnauthorizedAccessException">Access to <paramref name="path"/> is denied, or it names a directory.</exception>
    /// <exception cref="IOException">The file cannot be read.</exception>
    /// <exception cref="InvalidDataException">The document is oversized or invalid.</exception>
    public static Theme LoadFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        using var stream = File.OpenRead(path);
        return ParseUtf8(ReadBounded(stream, path), path, embedded: false);
    }

    #endregion

    #region Parsing

    // The embedded/external distinction is the one themes.md documents, so it is threaded
    // explicitly rather than inferred from which entry point the host happened to call. Keying it
    // off the method name meant one document could load through Load(slug) claiming Dark and throw
    // the moment anything touched Entries, because BuildCatalog scans every resource strictly.
    private static Theme ParseUtf8(ReadOnlyMemory<byte> utf8, string source, bool embedded)
    {
        var definition = Deserialize(utf8, source);
        ValidateMetadata(definition, source);

        var palette = ResolvePalette(definition.Palette, source);
        return BuildSemanticTheme(definition, palette, source, embedded);
    }

    private static Theme BuildSemanticTheme(
        ThemeDocument definition,
        Dictionary<string, Color> palette,
        string source,
        bool embedded)
    {
        // An external document substitutes for blank as well as missing. Substituting for null
        // alone let "slug": "" reach Theme's constructor, which threw an undeclared
        // ArgumentException naming a paramName that means nothing to a caller who passed one json
        // string - and which a caller catching the documented InvalidDataException did not catch.
        var theme = new Theme(
            palette,
            embedded ? ReadRequiredMetadata(definition.Name, "name", source) : Or(definition.Name, "Custom"),
            ReadSlug(definition.Slug, source, required: embedded),
            ReadColorScheme(definition.ColorScheme, source, required: embedded),
            embedded ? ReadRequiredMetadata(definition.Author, "author", source) : Or(definition.Author, "SharpVision contributors"),
            ReadLicense(definition.License, source, required: embedded),
            ReadSource(definition.Source, source, required: embedded));

        theme.SetDiagnosticSource(source);

        SetSemanticColors(theme, definition.Colors, palette, source);
        SetSemanticAttributes(theme, definition.Attributes, source);
        ApplyStyleSections(theme, definition.Styles, source);
        theme.SetGlyphs(ReadGlyphFamily(definition.Glyphs, source));
        theme.ValidateStyleSections();
        theme.Freeze();
        return theme;
    }

    private static void SetSemanticColors(
        Theme theme,
        ThemeDocumentColors? values,
        Dictionary<string, Color> palette,
        string source)
    {
        if (values is null)
        {
            throw new InvalidDataException($"Theme '{source}' must define every known semantic color exactly once.");
        }

        foreach (var color in Enum.GetValues<SemanticColor>())
        {
            var value = values.Get(color);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidDataException($"Theme '{source}' is missing colors.{JsonName(color)}.");
            }

            theme.SetColor(color, ResolveColor(value, palette, source, $"colors.{JsonName(color)}"));
        }
    }

    private static void SetSemanticAttributes(
        Theme theme,
        ThemeDocumentAttributes? values,
        string source)
    {
        if (values is null)
        {
            throw new InvalidDataException($"Theme '{source}' must define every known semantic decoration exactly once.");
        }

        foreach (var decoration in Enum.GetValues<SemanticDecoration>())
        {
            var value = values.Get(decoration);
            if (!value.HasValue)
            {
                throw new InvalidDataException($"Theme '{source}' is missing attributes.{JsonName(decoration)}.");
            }

            theme.SetAttributes(decoration, ParseAttributes(value.Value, source, $"attributes.{JsonName(decoration)}"));
        }
    }

    private static void ApplyStyleSections(Theme theme, JsonElement? styles, string source)
    {
        if (styles is not { } stylesElement)
        {
            throw new InvalidDataException($"Theme '{source}' must define a 'styles' object, which may be empty.");
        }

        var rawSections = ReadObject(stylesElement, source, "styles");
        theme.SetStyleSections(ReadStyleSections(rawSections, source));
    }

    /// <summary>Reads one required JSON object without exposing serializer shape exceptions.</summary>
    internal static Dictionary<string, JsonElement> ReadObject(JsonElement element, string source, string path)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(path);

        return element.ValueKind != JsonValueKind.Object
            ? throw new InvalidDataException($"Theme '{source}' '{path}' must be an object.")
            : element.EnumerateObject().ToDictionary(
                static property => property.Name,
                static property => property.Value,
                StringComparer.Ordinal);
    }

    // Every key, including a dot-namespaced one, must be one of the six well-known role sections -
    // the dot no longer bypasses validation, since there is no longer any registrable third-party
    // section for it to admit. Anything else is rejected outright: very likely a typo of one of the
    // six (e.g. "buton" instead of "button"), or a leaf/vendor key that named no section even before
    // this closed the vocabulary.
    private static Dictionary<string, JsonElement> ReadStyleSections(
        Dictionary<string, JsonElement>? sections,
        string source)
    {
        if (sections is null)
        {
            return [];
        }

        foreach (var name in sections.Keys)
        {
            if (!_knownStyleSections.Contains(name))
            {
                throw new InvalidDataException(
                    $"Theme '{source}' has unknown styles section '{name}'. Sections are limited to the six well-known styles: control, input, container, window, popup, tooltip.");
            }
        }

        return sections;
    }


    private static string JsonName<T>(T value) where T : struct, Enum =>
        JsonNamingPolicy.CamelCase.ConvertName(value.ToString());

    private static readonly byte[] _utf8Preamble = Encoding.UTF8.GetPreamble();

    private static ThemeDocument Deserialize(ReadOnlyMemory<byte> utf8, string source)
    {
        if (utf8.Length > MaximumDocumentBytes)
        {
            throw DocumentTooLarge(source);
        }

        // The Deserialize(ReadOnlySpan<byte>, ...) overload does not strip a leading UTF-8
        // preamble the way the Stream overload does for free; buffering into a byte[] to enforce
        // the size bound above loses that leniency by accident. RFC 8259 §8.1 permits ignoring a
        // BOM, and a BOM'd file is otherwise well-formed UTF-8.
        var content = utf8.Span.StartsWith(_utf8Preamble) ? utf8[_utf8Preamble.Length..] : utf8;

        try
        {
            return JsonSerializer.Deserialize<ThemeDocument>(content.Span, JsonOptions)
                   ?? throw new InvalidDataException($"Theme '{source}' deserialized to null.");
        }
        catch (JsonException error)
        {
            // error.Path is "$" at the document root, which is not whitespace, so this must test
            // the trimmed value - trimming first and then checking would otherwise interpolate an
            // empty string while the untrimmed guard let it through, leaving "... at ''." on every
            // root-level failure. error.Path also stops descending once it reaches a raw-JsonElement
            // subtree such as "styles", so a syntax error nested inside one reports only the shallow
            // path above it; LineNumber/BytePositionInLine come from the underlying Utf8JsonReader
            // instead and are unaffected by that truncation, so they are appended unconditionally
            // whenever the reader captured them. Both are 0-based in JsonException; +1 them here so
            // the message reads as a human would count lines and columns in an editor.
            var trimmedPath = error.Path?.TrimStart('$', '.') ?? string.Empty;
            var path = string.IsNullOrWhiteSpace(trimmedPath) ? string.Empty : $" at '{trimmedPath}'";
            var position = error.LineNumber is { } line && error.BytePositionInLine is { } column
                ? $" (line {line + 1}, column {column + 1})"
                : string.Empty;
            throw new InvalidDataException($"Theme '{source}' is not valid JSON{path}{position}.", error);
        }
    }

    private static Dictionary<string, Color> ResolvePalette(
        Dictionary<string, string>? entries,
        string source)
    {
        var result = new Dictionary<string, Color>(StringComparer.Ordinal);
        if (entries is null)
        {
            return result;
        }

        if (entries.Count > _maximumPaletteEntries)
        {
            throw new InvalidDataException(
                $"Theme '{source}' palette exceeds {_maximumPaletteEntries} entries.");
        }

        foreach (var (name, value) in entries)
        {
            result[name] = Color.TryFromHex(value, out var color)
                ? color
                : throw new InvalidDataException(
                    $"Theme '{source}' palette entry '{name}' has invalid color '{value}'.");
        }

        return result;
    }

    private static Color ResolveColor(
        string value,
        Dictionary<string, Color> palette,
        string source,
        string context) =>
        palette.TryGetValue(value, out var paletteColor)
            ? paletteColor
            : throw new InvalidDataException(
                $"Theme '{source}' {context} references unknown palette key '{value}'.");

    private static TerminalAttributes ParseAttributes(JsonElement value, string source, string context)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            var name = value.GetString()!;
            return _attributeNames.TryGetValue(name, out var attribute)
                ? attribute
                : throw new InvalidDataException(
                    $"Theme '{source}' {context} has unknown attribute '{name}'.");
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException($"Theme '{source}' {context} must be a string or array.");
        }

        var result = TerminalAttributes.None;
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                throw new InvalidDataException($"Theme '{source}' {context} must contain strings.");
            }

            var name = item.GetString()!;
            if (!_attributeNames.TryGetValue(name, out var attribute))
            {
                throw new InvalidDataException(
                    $"Theme '{source}' {context} has unknown attribute '{name}'.");
            }

            result |= attribute;
        }

        return result;
    }

    #endregion

    #region Metadata and validation

    private static ThemeCatalogEntry ReadCatalogEntry(
        ReadOnlyMemory<byte> utf8,
        string resource,
        out int order)
    {
        var definition = Deserialize(utf8, resource);
        ValidateMetadata(definition, resource);
        order = definition.Order;
        return new ThemeCatalogEntry(
            ReadRequiredMetadata(definition.Name, "name", resource),
            ReadSlug(definition.Slug, resource, required: true),
            ReadColorScheme(definition.ColorScheme, resource, required: true),
            ReadRequiredMetadata(definition.Author, "author", resource),
            ReadLicense(definition.License, resource, required: true),
            ReadSource(definition.Source, resource, required: true),
            definition.Order);
    }

    private static void ValidateMetadata(ThemeDocument definition, string source)
    {
        foreach (var (name, value) in new[]
                 {
                     ("name", definition.Name),
                     ("slug", definition.Slug),
                     ("colorScheme", definition.ColorScheme),
                     ("author", definition.Author),
                     ("license", definition.License),
                     ("source", definition.Source),
                     ("glyphs", definition.Glyphs)
                 })
        {
            if (value is { Length: > _maximumMetadataLength })
            {
                throw new InvalidDataException(
                    $"Theme '{source}' field '{name}' exceeds {_maximumMetadataLength} characters.");
            }
        }
    }

    private static string Or(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;

    private static string ReadRequiredMetadata(string? value, string name, string source) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new InvalidDataException($"Theme '{source}' is missing required field '{name}'.")
            : value;

    private static string ReadSlug(string? value, string source, bool required)
    {
        var slug = required
            ? ReadRequiredMetadata(value, "slug", source)
            : Or(value, "custom");

        return ThemeSlug.IsValid(slug)
            ? slug
            : throw new InvalidDataException($"Theme '{source}' has invalid slug '{slug}'.");
    }

    private static string ReadLicense(string? value, string source, bool required)
    {
        var license = required ? ReadRequiredMetadata(value, "license", source) : Or(value, "MIT");
        return ThemeProvenance.IsLicense(license)
            ? license
            : throw new InvalidDataException($"Theme '{source}' has invalid SPDX license identifier '{license}'.");
    }

    private static string ReadSource(string? value, string source, bool required)
    {
        var provenance = required
            ? ReadRequiredMetadata(value, "source", source)
            : Or(value, "https://github.com/sharpvision/sharpvision");
        return ThemeProvenance.IsSource(provenance)
            ? provenance
            : throw new InvalidDataException($"Theme '{source}' has invalid source URL '{provenance}'.");
    }

    private static ColorScheme ReadColorScheme(string? value, string source, bool required)
    {
        return string.IsNullOrWhiteSpace(value) && !required
            ? ColorScheme.Dark
            : value switch
            {
                "dark" => ColorScheme.Dark,
                "light" => ColorScheme.Light,
                _ => throw new InvalidDataException($"Theme '{source}' has invalid colorScheme '{value}'.")
            };
    }

    // Case-insensitive, matching ResolveSectionBorderGlyphStyle's identical string-to-static-
    // instance leniency rather than ReadColorScheme's exact-case switch: "glyphs" is authored
    // freely by any theme document, not just the fifteen curated ones ReadColorScheme's stricter
    // metadata rules already assume complete, embedded documents for.
    private static GlyphFamily ReadGlyphFamily(string? value, string source) =>
        value is null
            ? GlyphFamily.Default
            : value.ToLowerInvariant() switch
            {
                "dots" => GlyphFamily.Dots,
                "blocks" => GlyphFamily.Blocks,
                "ascii" => GlyphFamily.Ascii,
                "shades" => GlyphFamily.Shades,
                "lines" => GlyphFamily.Lines,
                _ => throw new InvalidDataException($"Theme '{source}' has invalid glyphs '{value}'.")
            };

    #endregion

    #region Bounded input

    private static byte[] ReadResource(Assembly assembly, string name)
    {
        using var stream = assembly.GetManifestResourceStream(name)
                           ?? throw new InvalidDataException($"Embedded theme resource '{name}' is missing.");
        return ReadBounded(stream, name);
    }

    private static byte[] ReadBounded(Stream stream, string source)
    {
        // Seekable streams (files, embedded resources) know their remaining length up front, so
        // the bound can be checked and the buffer sized exactly - avoiding the 64KB+1 scratch
        // allocation the non-seekable path below needs to grow into. This is the common case:
        // every embedded theme resource and every Load(slug)/LoadFile call goes through here.
        // Non-seekable streams (arbitrary caller-supplied Stream implementations)
        // fall back to the original grow-and-check loop, since their length isn't known ahead of
        // the read.
        if (stream.CanSeek)
        {
            var remaining = stream.Length - stream.Position;
            if (remaining < 0)
            {
                throw new InvalidDataException($"Theme '{source}' starts beyond the end of its stream.");
            }

            if (remaining > MaximumDocumentBytes)
            {
                throw DocumentTooLarge(source);
            }

            var sized = new byte[remaining];
            var offset = 0;

            while (offset < sized.Length)
            {
                var read = stream.Read(sized.AsSpan(offset));
                if (read == 0)
                {
                    break;
                }

                offset += read;
            }

            return offset == sized.Length ? sized : sized[..offset];
        }

        var bytes = new byte[MaximumDocumentBytes + 1];
        var length = 0;

        while (length < bytes.Length)
        {
            var read = stream.Read(bytes.AsSpan(length));
            if (read == 0)
            {
                break;
            }

            length += read;
        }

        if (length > MaximumDocumentBytes)
        {
            throw DocumentTooLarge(source);
        }

        Array.Resize(ref bytes, length);
        return bytes;
    }

    private static InvalidDataException DocumentTooLarge(string source) =>
        new($"Theme '{source}' exceeds the {MaximumDocumentBytes}-byte document limit.");

    #endregion
}
