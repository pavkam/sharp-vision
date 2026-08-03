// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Discovers, loads, validates, and caches immutable SharpVision themes.</summary>
[PublicAPI]
public static class Themes
{
    internal const int MaximumDocumentBytes = 64 * 1024;
    private const int _maximumPaletteEntries = 256;
    private const int _maximumMetadataLength = 2048;
    private const string _resourcePrefix = "SharpVision.Styling.Themes.";
    private const string _resourceSuffix = ".theme.json";

    // The closed set of library-owned registrable style sections (see #155), analogous to the
    // five fixed profile names - grows as a built-in control registers a section, unlike the open
    // "vendor.control" namespace any third-party control can use without a registry entry here.
    private static readonly HashSet<string> _registeredStyleSections =
        new(StringComparer.Ordinal)
        {
            "scrollBar", "checkBox", "radioButton", "button", "chaseIndicator", "slider", "progressBar"
        };
    private static readonly Lock _gate = new();
    private static readonly Dictionary<string, Theme> _cache = new(StringComparer.Ordinal);

    // The full resource scan is deferred behind this Lazy so that touching one theme (including
    // the White/Dark defaults every ControlBase falls back to) never forces every embedded theme
    // to be read and parsed - see #250. ExecutionAndPublication is required, not just a nicety:
    // two threads racing to materialize this would otherwise both run BuildCatalog and could
    // observe two different (equally valid) winning instances handed out to concurrent callers.
    private static readonly Lazy<(IReadOnlyList<ThemeCatalogEntry> Entries, IReadOnlyList<string> Slugs, Dictionary<string, byte[]> Documents)> _catalog =
        new(BuildCatalog, LazyThreadSafetyMode.ExecutionAndPublication);

    // No DTO property across ThemeDefinition and its nested types is enum-typed - every enum-like
    // field is parsed by hand via Enum.TryParse in the resolve methods below, so a
    // JsonStringEnumConverter here has never converted anything (see #250). It was also the one
    // [RequiresDynamicCode] member in this options instance.
    private static readonly JsonSerializerOptions _jsonOptions = new()
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

    private static readonly Dictionary<string, StatusColor> _statusColorNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["error"] = StatusColor.Error,
            ["warning"] = StatusColor.Warning,
            ["success"] = StatusColor.Success,
            ["info"] = StatusColor.Info,
            ["muted"] = StatusColor.Muted,
            ["hotkey"] = StatusColor.Hotkey
        };

    static Themes()
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
        var assembly = typeof(Themes).Assembly;
        var resourceName = _resourcePrefix + slug + _resourceSuffix;
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return null;
        }

        var theme = ParseUtf8(ReadBounded(stream, slug), slug);
        return string.Equals(theme.Slug, slug, StringComparison.Ordinal) ? theme : null;
    }

    private static Theme LoadFromCatalog(string slug) =>
        _catalog.Value.Documents.TryGetValue(slug, out var document)
            ? ParseUtf8(document, slug)
            : throw new KeyNotFoundException($"The theme catalog does not contain '{slug}'.");

    // Extracted from the former static constructor: only runs when something actually needs the
    // full catalog (Entries/Slugs, or a Load(slug) whose filename doesn't match its own internal
    // slug) rather than unconditionally on every process start - see #250.
    private static (IReadOnlyList<ThemeCatalogEntry> Entries, IReadOnlyList<string> Slugs, Dictionary<string, byte[]> Documents) BuildCatalog()
    {
        var assembly = typeof(Themes).Assembly;
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
    public static Theme Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return Parse(json, "<parsed>");
    }

    /// <summary>Parses a frozen theme using an internal diagnostic source label.</summary>
    internal static Theme Parse(string json, string source)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(source);
        return Encoding.UTF8.GetByteCount(json) > MaximumDocumentBytes
            ? throw DocumentTooLarge(source)
            : ParseUtf8(Encoding.UTF8.GetBytes(json), source);
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
            : ParseUtf8(ReadBounded(stream, "<stream>"), "<stream>");
    }

    /// <summary>Loads a frozen theme from a UTF-8 JSON file.</summary>
    /// <param name="path">The theme file path.</param>
    /// <returns>A newly parsed frozen theme.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    /// <exception cref="IOException">The file cannot be read.</exception>
    /// <exception cref="InvalidDataException">The document is oversized or invalid.</exception>
    public static Theme LoadFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        using var stream = File.OpenRead(path);
        return ParseUtf8(ReadBounded(stream, path), path);
    }

    #endregion

    #region Parsing

    private static Theme ParseUtf8(ReadOnlyMemory<byte> utf8, string source)
    {
        var definition = Deserialize(utf8, source);
        ValidateMetadata(definition, source);

        var palette = ResolvePalette(definition.Palette, source);
        return BuildSemanticTheme(definition, palette, source);
    }

    private static Theme BuildSemanticTheme(
        ThemeDefinition definition,
        Dictionary<string, Color> palette,
        string source)
    {
        var theme = new Theme(
            palette,
            definition.Name ?? "Custom",
            definition.Slug ?? "custom",
            ReadColorScheme(definition.ColorScheme, source, required: false),
            definition.Author ?? "SharpVision contributors",
            definition.License ?? "MIT",
            definition.Source ?? "https://github.com/sharpvision/sharpvision");

        SetSemanticColors(theme, definition.Colors, palette, source);
        SetSemanticAttributes(theme, definition.Attributes, source);
        SetSemanticProfiles(theme, definition.Styles, palette, source);
        var status = ResolveStatusColors(definition.Status, palette, source);
        SetDefaultStatusColor(status, StatusColor.Error, theme.ResolveColor(ThemeColor.Error));
        SetDefaultStatusColor(status, StatusColor.Warning, theme.ResolveColor(ThemeColor.Warning));
        SetDefaultStatusColor(status, StatusColor.Success, theme.ResolveColor(ThemeColor.Success));
        SetDefaultStatusColor(status, StatusColor.Info, theme.ResolveColor(ThemeColor.Info));
        SetDefaultStatusColor(status, StatusColor.Muted, theme.ResolveColor(ThemeColor.Muted));
        SetDefaultStatusColor(status, StatusColor.Hotkey, theme.ResolveColor(ThemeColor.Hotkey));
        theme.SetStatusColors(status);
        theme.Freeze();
        return theme;
    }

    private static void SetSemanticColors(
        Theme theme,
        ThemeColorsDefinition? values,
        Dictionary<string, Color> palette,
        string source)
    {
        if (values is null)
        {
            throw new InvalidDataException($"Theme '{source}' must define every known color role exactly once.");
        }

        foreach (var role in Enum.GetValues<ThemeColor>())
        {
            var value = values.Get(role);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidDataException($"Theme '{source}' is missing colors.{JsonName(role)}.");
            }

            theme.SetColor(role, ResolveColor(value, palette, source, $"colors.{JsonName(role)}"));
        }
    }

    private static void SetSemanticAttributes(
        Theme theme,
        ThemeAttributesDefinition? values,
        string source)
    {
        if (values is null)
        {
            throw new InvalidDataException($"Theme '{source}' must define every known attribute role exactly once.");
        }

        foreach (var role in Enum.GetValues<ThemeDecoration>())
        {
            var value = values.Get(role);
            if (!value.HasValue)
            {
                throw new InvalidDataException($"Theme '{source}' is missing attributes.{JsonName(role)}.");
            }

            theme.SetAttributes(role, ParseAttributes(value.Value, source, $"attributes.{JsonName(role)}"));
        }
    }

    private static void SetSemanticProfiles(
        Theme theme,
        ThemeStylesDefinition? definitions,
        Dictionary<string, Color> palette,
        string source)
    {
        if (definitions is null)
        {
            throw new InvalidDataException($"Theme '{source}' must define every semantic style role exactly once.");
        }

        var control = BuildProfile(
            ThemeRole.Control,
            ReadProfile(definitions.Control, ThemeRole.Control, source),
            inherited: null,
            palette,
            source,
            "styles.control");
        var input = BuildProfile(
            ThemeRole.Input,
            ReadProfile(definitions.Input, ThemeRole.Input, source), control, palette, source, "styles.input");
        var container = BuildProfile(
            ThemeRole.Container,
            ReadProfile(definitions.Container, ThemeRole.Container, source), control, palette, source, "styles.container");
        var window = BuildProfile(
            ThemeRole.Window,
            ReadProfile(definitions.Window, ThemeRole.Window, source), control, palette, source, "styles.window");
        var popup = BuildProfile(
            ThemeRole.Popup,
            ReadProfile(definitions.Popup, ThemeRole.Popup, source), control, palette, source, "styles.popup");

        theme.SetProfiles(control, input, container, window, popup);
        theme.SetStyleSections(ReadStyleSections(definitions.Sections, source));
    }

    // A namespaced key ("vendor.control") is a registrable third-party style section and is
    // retained raw for later lazy binding via Theme.GetStyleSection, with no registry entry
    // needed here. An unqualified key must be one of the five fixed profile names or a
    // library-registered section name (_registeredStyleSections) - anything else is very likely a
    // typo (e.g. "buton" instead of "button"), so it is rejected rather than silently retained
    // (see #155).
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
            if (!name.Contains('.', StringComparison.Ordinal) && !_registeredStyleSections.Contains(name))
            {
                throw new InvalidDataException(
                    $"Theme '{source}' has unknown styles section '{name}'. Third-party sections must be namespaced as 'vendor.control'.");
            }
        }

        return sections;
    }


    private static T ConvertStyleMember<T>(
        Func<T> convert,
        string source,
        string context,
        bool appendParameterName = true)
    {
        try
        {
            return convert();
        }
        catch (ArgumentException error)
        {
            var errorContext = ResolveStyleErrorContext(context, error.ParamName, appendParameterName);
            throw new InvalidDataException($"Theme '{source}' {errorContext} is invalid.", error);
        }
        catch (OverflowException error)
        {
            throw new InvalidDataException($"Theme '{source}' {context} is invalid.", error);
        }
    }

    private static string ResolveStyleErrorContext(
        string context,
        string? parameterName,
        bool appendParameterName) =>
        !appendParameterName || string.IsNullOrWhiteSpace(parameterName)
            ? context
            : string.Equals(parameterName, context, StringComparison.Ordinal) ||
              context.EndsWith($".{parameterName}", StringComparison.Ordinal)
            ? context
            : parameterName.StartsWith("styles.", StringComparison.Ordinal)
                ? parameterName
                : $"{context}.{parameterName}";

    private static ThemeProfileDefinition ReadProfile(
        ThemeProfileDefinition? definition,
        ThemeRole role,
        string source) => definition ??
        throw new InvalidDataException($"Theme '{source}' is missing styles.{JsonName(role)}.");

    private static ThemeProfile BuildProfile(
        ThemeRole role,
        ThemeProfileDefinition definition,
        ThemeProfile? inherited,
        Dictionary<string, Color> palette,
        string source,
        string context)
    {
        var normalBase = inherited?.Normal ?? Theme.CreateDefaultAppearance();
        var normal = normalBase.Apply(ResolveAppearanceSet(definition.Normal, palette, source, $"{context}.normal"));
        return new ThemeProfile(
            normal,
            Merge(InheritedState(role, inherited, VisualState.PointerOver), definition.PointerOver, palette, source, $"{context}.pointerOver"),
            Merge(InheritedState(role, inherited, VisualState.FocusWithin), definition.FocusWithin, palette, source, $"{context}.focusWithin"),
            Merge(InheritedState(role, inherited, VisualState.Focused), definition.Focused, palette, source, $"{context}.focused"),
            Merge(inherited?.Current, definition.Current, palette, source, $"{context}.current"),
            Merge(inherited?.Selected, definition.Selected, palette, source, $"{context}.selected"),
            Merge(inherited?.Checked, definition.Checked, palette, source, $"{context}.checked"),
            Merge(inherited?.Indeterminate, definition.Indeterminate, palette, source, $"{context}.indeterminate"),
            Merge(inherited?.Pressed, definition.Pressed, palette, source, $"{context}.pressed"),
            Merge(inherited?.Disabled, definition.Disabled, palette, source, $"{context}.disabled"));
    }

    private static AppearanceSet? InheritedState(ThemeRole role, ThemeProfile? inherited, VisualState state) =>
        (role, state) switch
        {
            (ThemeRole.Container or ThemeRole.Window, VisualState.PointerOver) => AppearanceSet.Empty,
            (ThemeRole.Window, VisualState.FocusWithin) => new AppearanceSet(
                border: new BorderSet(foreground: ThemeColor.ActiveBorder)),
            (ThemeRole.Window, VisualState.Focused) => AppearanceSet.Empty,
            (_, VisualState.PointerOver) => inherited?.PointerOver,
            (_, VisualState.FocusWithin) => inherited?.FocusWithin,
            (_, VisualState.Focused) => inherited?.Focused,
            _ => throw new UnreachableException()
        };

    private static AppearanceSet Merge(
        AppearanceSet? inherited,
        ThemeAppearanceDefinition? definition,
        Dictionary<string, Color> palette,
        string source,
        string context)
    {
        var own = ResolveAppearanceSet(definition, palette, source, context);
        return inherited?.Overlay(own) ?? own;
    }

    private static AppearanceSet ResolveAppearanceSet(
        ThemeAppearanceDefinition? definition,
        Dictionary<string, Color> palette,
        string source,
        string context) => definition is null
        ? AppearanceSet.Empty
        : new AppearanceSet(
            ResolveFaceSet(definition.Face, palette, source, $"{context}.face"),
            ResolveBorderSet(definition.Border, palette, source, $"{context}.border"),
            ResolveShadowSet(definition.Shadow, palette, source, $"{context}.shadow"));

    private static FaceSet? ResolveFaceSet(
        FaceDefinition? definition,
        Dictionary<string, Color> palette,
        string source,
        string context) => definition is null
        ? null
        : ConvertStyleMember(
            () => new FaceSet(
                ResolveOptionalColorValue(definition.Foreground, palette, source, $"{context}.foreground"),
                ResolveOptionalColorValue(definition.Background, palette, source, $"{context}.background"),
                ResolveOptionalAttributeValue(definition.Attributes, source, $"{context}.attributes"),
                ResolveOptionalUnderline(definition.Underline, source, $"{context}.underline"),
                ResolveOptionalColorValue(definition.UnderlineColor, palette, source, $"{context}.underlineColor")),
            source,
            context);

    private static BorderSet? ResolveBorderSet(
        BorderDefinition? definition,
        Dictionary<string, Color> palette,
        string source,
        string context) => definition is null
        ? null
        : ConvertStyleMember(
            () => new BorderSet(
                ResolveOptionalBorderSide(definition.Sides, source, $"{context}.sides"),
                ResolveOptionalBorderGlyphStyle(definition.GlyphStyle, source, $"{context}.glyphStyle"),
                ResolveOptionalColorValue(definition.Foreground, palette, source, $"{context}.foreground"),
                ResolveOptionalColorValue(definition.Background, palette, source, $"{context}.background"),
                ResolveOptionalAttributeValue(definition.Attributes, source, $"{context}.attributes")),
            source,
            context);

    private static ShadowSet? ResolveShadowSet(
        ShadowDefinition? definition,
        Dictionary<string, Color> palette,
        string source,
        string context) => definition is null
        ? null
        : ConvertStyleMember(
            () => new ShadowSet(
                definition.Visible,
                ResolveOptionalShadowMode(definition.Mode, source, $"{context}.mode"),
                definition.Offset is null ? null : new Point(definition.Offset.X, definition.Offset.Y),
                ResolveOptionalRune(definition.Glyph, source, $"{context}.glyph"),
                ResolveOptionalColorValue(definition.Foreground, palette, source, $"{context}.foreground"),
                ResolveOptionalColorValue(definition.Background, palette, source, $"{context}.background"),
                ResolveOptionalAttributeValue(definition.Attributes, source, $"{context}.attributes")),
            source,
            context);

    private static ColorValue? ResolveOptionalColorValue(
        string? value,
        Dictionary<string, Color> palette,
        string source,
        string context) => value is null ? null : ResolveColorValue(value, palette, source, context);

    private static ColorValue ResolveColorValue(
        string value,
        Dictionary<string, Color> palette,
        string source,
        string context) => string.Equals(value, "transparent", StringComparison.OrdinalIgnoreCase)
        ? Color.Transparent
        : string.Equals(value, "default", StringComparison.OrdinalIgnoreCase)
            ? Color.Default
            : Enum.TryParse<ThemeColor>(value, ignoreCase: true, out var role) && Enum.IsDefined(role)
                ? role
                : ResolveColor(value, palette, source, context);

    private static AttributeValue? ResolveOptionalAttributeValue(
        JsonElement? value,
        string source,
        string context)
    {
        if (!value.HasValue)
        {
            return null;
        }

        if (value.Value.ValueKind == JsonValueKind.String)
        {
            var name = value.Value.GetString();
            if (Enum.TryParse<ThemeDecoration>(name, ignoreCase: true, out var role) && Enum.IsDefined(role))
            {
                return role;
            }
        }

        return ParseAttributes(value.Value, source, context);
    }

    private static Underline? ResolveOptionalUnderline(string? value, string source, string context) => value is null
        ? null
        : Enum.TryParse<Underline>(value, ignoreCase: true, out var result) && Enum.IsDefined(result)
            ? result
            : throw new InvalidDataException($"Theme '{source}' {context} has unknown underline '{value}'.");

    private static BorderSide? ResolveOptionalBorderSide(string? value, string source, string context) => value is null
        ? null
        : value.ToLowerInvariant() switch
        {
            "none" => BorderSide.None,
            "left" => BorderSide.Left,
            "top" => BorderSide.Top,
            "right" => BorderSide.Right,
            "bottom" => BorderSide.Bottom,
            "all" => BorderSide.All,
            _ => throw new InvalidDataException($"Theme '{source}' {context} has unknown sides '{value}'.")
        };

    private static BorderGlyphStyle? ResolveOptionalBorderGlyphStyle(
        JsonElement? value,
        string source,
        string context)
    {
        if (!value.HasValue)
        {
            return null;
        }

        if (value.Value.ValueKind == JsonValueKind.String)
        {
            var name = value.Value.GetString()!;
            return name.ToLowerInvariant() switch
            {
                "light" => BorderGlyphStyle.Light,
                "heavy" => BorderGlyphStyle.Heavy,
                "paired" => BorderGlyphStyle.Paired,
                "rounded" => BorderGlyphStyle.Rounded,
                "ascii" => BorderGlyphStyle.Ascii,
                "solid" => BorderGlyphStyle.Solid,
                "halfblock" => BorderGlyphStyle.HalfBlock,
                "lightshade" => BorderGlyphStyle.LightShade,
                "mediumshade" => BorderGlyphStyle.MediumShade,
                "darkshade" => BorderGlyphStyle.DarkShade,
                _ => throw new InvalidDataException($"Theme '{source}' {context} has unknown glyph style '{name}'.")
            };
        }

        if (value.Value.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException($"Theme '{source}' {context} must be a string or an eight-Rune array.");
        }

        var runes = new Rune[8];
        var index = 0;
        foreach (var item in value.Value.EnumerateArray())
        {
            if (index >= runes.Length)
            {
                throw new InvalidDataException(
                    $"Theme '{source}' {context} array must contain exactly eight Runes.");
            }

            if (item.ValueKind != JsonValueKind.String)
            {
                throw new InvalidDataException($"Theme '{source}' {context} array must contain strings.");
            }

            runes[index] = ParseRune(item.GetString()!, source, $"{context}[{index}]");
            index++;
        }

        return index != runes.Length
            ? throw new InvalidDataException($"Theme '{source}' {context} array must contain exactly eight Runes.")
            : new BorderGlyphStyle(
                runes[0],
                runes[1],
                runes[2],
                runes[3],
                runes[4],
                runes[5],
                runes[6],
                runes[7]);
    }

    private static ShadowMode? ResolveOptionalShadowMode(string? value, string source, string context) => value is null
        ? null
        : Enum.TryParse<ShadowMode>(value, ignoreCase: true, out var result) && Enum.IsDefined(result)
            ? result
            : throw new InvalidDataException($"Theme '{source}' {context} has unknown shadow mode '{value}'.");

    private static Rune? ResolveOptionalRune(string? value, string source, string context) =>
        value is null ? null : ParseRune(value, source, context);

    private static Rune ParseRune(string value, string source, string context)
    {
        var runes = value.EnumerateRunes();
        var enumerator = runes.GetEnumerator();
        if (!enumerator.MoveNext())
        {
            throw new InvalidDataException($"Theme '{source}' {context} must contain one Rune.");
        }

        var result = enumerator.Current;
        return enumerator.MoveNext()
            ? throw new InvalidDataException($"Theme '{source}' {context} must contain one Rune.")
            : result;
    }

    private static void SetDefaultStatusColor(
        Dictionary<StatusColor, Color> status,
        StatusColor role,
        Color value) => status.TryAdd(role, value);

    private static string JsonName<T>(T value) where T : struct, Enum =>
        JsonNamingPolicy.CamelCase.ConvertName(value.ToString());

    private static readonly byte[] _utf8Preamble = Encoding.UTF8.GetPreamble();

    private static ThemeDefinition Deserialize(ReadOnlyMemory<byte> utf8, string source)
    {
        if (utf8.Length > MaximumDocumentBytes)
        {
            throw DocumentTooLarge(source);
        }

        // The Deserialize(ReadOnlySpan<byte>, ...) overload does not strip a leading UTF-8
        // preamble the way the Stream overload does for free; buffering into a byte[] to enforce
        // the size bound above loses that leniency by accident. RFC 8259 §8.1 permits ignoring a
        // BOM, and a BOM'd file is otherwise well-formed UTF-8 (see #202).
        var content = utf8.Span.StartsWith(_utf8Preamble) ? utf8[_utf8Preamble.Length..] : utf8;

        try
        {
            return JsonSerializer.Deserialize<ThemeDefinition>(content.Span, _jsonOptions)
                   ?? throw new InvalidDataException($"Theme '{source}' deserialized to null.");
        }
        catch (JsonException error)
        {
            // error.Path is "$" at the document root, which is not whitespace, so this must test
            // the trimmed value - trimming first and then checking would otherwise interpolate an
            // empty string while the untrimmed guard let it through, leaving "... at ''." on every
            // root-level failure (see #202).
            var trimmedPath = error.Path?.TrimStart('$', '.') ?? string.Empty;
            var path = string.IsNullOrWhiteSpace(trimmedPath) ? string.Empty : $" at '{trimmedPath}'";
            throw new InvalidDataException($"Theme '{source}' is not valid JSON{path}.", error);
        }
    }

    // Deserializes one retained styles.* section element on demand, called from
    // Theme.GetStyleSection - the element was already read within the whole document's
    // MaxDepth=8 budget during the original Deserialize call above, so no separate depth check is
    // needed here (see #155).
    internal static TSection ParseSection<TSection>(JsonElement element, string sectionName, string source)
        where TSection : class
    {
        try
        {
            return JsonSerializer.Deserialize<TSection>(element, _jsonOptions)
                   ?? throw new InvalidDataException($"Theme '{source}' section '{sectionName}' deserialized to null.");
        }
        catch (JsonException error)
        {
            throw new InvalidDataException($"Theme '{source}' section '{sectionName}' is not valid.", error);
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

    private static Dictionary<StatusColor, Color> ResolveStatusColors(
        Dictionary<string, string>? status,
        Dictionary<string, Color> palette,
        string source)
    {
        var result = new Dictionary<StatusColor, Color>();
        if (status is null)
        {
            return result;
        }

        if (status.Count > _statusColorNames.Count)
        {
            throw new InvalidDataException(
                $"Theme '{source}' status section exceeds {_statusColorNames.Count} entries.");
        }

        foreach (var (name, value) in status)
        {
            if (!_statusColorNames.TryGetValue(name, out var statusColor))
            {
                throw new InvalidDataException($"Theme '{source}' has unknown status color '{name}'.");
            }

            result[statusColor] = ResolveColor(value, palette, source, $"status.{name}");
        }

        return result;
    }

    private static Color ResolveColor(
        string value,
        Dictionary<string, Color> palette,
        string source,
        string context) =>
        value.StartsWith('#')
            ? Color.TryFromHex(value, out var color)
                ? color
                : throw new InvalidDataException($"Theme '{source}' {context} has invalid color '{value}'.")
            : palette.TryGetValue(value, out var paletteColor)
                ? paletteColor
                : throw new InvalidDataException(
                    $"Theme '{source}' {context} references unknown palette key '{value}'.");

    private static TerminalAttributes ParseAttributes(JsonElement value, string source, string context)
    {
        if (value.ValueKind == JsonValueKind.Null)
        {
            return TerminalAttributes.None;
        }

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
            ReadRequiredMetadata(definition.Slug, "slug", resource),
            ReadColorScheme(definition.ColorScheme, resource, required: true),
            ReadRequiredMetadata(definition.Author, "author", resource),
            ReadRequiredMetadata(definition.License, "license", resource),
            ReadRequiredMetadata(definition.Source, "source", resource));
    }

    private static void ValidateMetadata(ThemeDefinition definition, string source)
    {
        foreach (var (name, value) in new[]
                 {
                     ("name", definition.Name),
                     ("slug", definition.Slug),
                     ("colorScheme", definition.ColorScheme),
                     ("author", definition.Author),
                     ("license", definition.License),
                     ("source", definition.Source)
                 })
        {
            if (value is { Length: > _maximumMetadataLength })
            {
                throw new InvalidDataException(
                    $"Theme '{source}' field '{name}' exceeds {_maximumMetadataLength} characters.");
            }
        }
    }

    private static string ReadRequiredMetadata(string? value, string name, string source) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new InvalidDataException($"Theme '{source}' is missing required field '{name}'.")
            : value;

    private static ColorScheme ReadColorScheme(string? value, string source, bool required)
    {
        return value is null && !required
            ? ColorScheme.Dark
            : value switch
            {
                "dark" => ColorScheme.Dark,
                "light" => ColorScheme.Light,
                _ => throw new InvalidDataException($"Theme '{source}' has invalid colorScheme '{value}'.")
            };
    }

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
        // every embedded theme resource and every Load(slug)/LoadFile call goes through here
        // (see #250). Non-seekable streams (arbitrary caller-supplied Stream implementations)
        // fall back to the original grow-and-check loop, since their length isn't known ahead of
        // the read.
        if (stream.CanSeek)
        {
            var remaining = stream.Length - stream.Position;
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
