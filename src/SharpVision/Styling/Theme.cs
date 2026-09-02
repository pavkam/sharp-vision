// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;

/// <summary>Represents immutable global semantic values, appearance states, and provenance after freezing.</summary>
[PublicAPI]
public sealed class Theme
{
    // A leaf style's own code-owned static preset (e.g. CheckBoxStyle.Brackets) calls its Complete
    // method directly, outside of any real resolution, to build a value with no live Theme in
    // scope. ThemeCatalog.Dark is deliberately NOT used for that. It once guarded against a
    // genuine static-initializer reentrancy - ThemeCatalog's own static constructor parsed the
    // embedded "default-dark" document by reflectively scanning and touching every leaf style's
    // own registered "Definition" property, which reentered THIS type's static constructor before
    // ThemeCatalog's Dark/White fields were assigned - but that reflective registry is gone along
    // with per-leaf theme sections, so that specific trigger no longer exists.
    //
    // The dependency stays worth avoiding on its own terms. Routing a preset through
    // ThemeCatalog.Dark would make every leaf style's static preset initializer depend on parsing
    // an embedded theme resource - I/O plus the full parse/validate/cascade catalog machinery
    // running at type-init time, purely to hand back a Theme - and a resource failure there would
    // cascade into every preset's own type initializer instead of staying local to ThemeCatalog. A
    // bare, unauthored Theme carries the same GlyphFamily.Default (and every other code-owned
    // default) as ThemeCatalog.Dark without ever touching ThemeCatalog: every completion only
    // reads Glyphs, and Unthemed already carries GlyphFamily.Default for it, the same as Dark's
    // own zero-config document resolves to.
    internal static readonly Theme Unthemed = new();

    private readonly Dictionary<string, Color> _palette;
    private readonly Color[] _colors = new Color[Enum.GetValues<SemanticColor>().Length];
    private readonly TerminalAttributes[] _attributes =
        new TerminalAttributes[Enum.GetValues<SemanticDecoration>().Length];
    private Dictionary<string, JsonElement> _styleSections = [];
    private string? DiagnosticSourceOverride { get; set; }

    /// <summary>Gets the loader source retained for delayed style diagnostics.</summary>
    private string DiagnosticSource => DiagnosticSourceOverride ?? Slug;

    /// <summary>Retains the exact loader source before any style leaf is compiled.</summary>
    internal void SetDiagnosticSource(string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        DiagnosticSourceOverride = source;
    }

    /// <summary>Initializes an unfrozen theme and copies its optional named palette.</summary>
    /// <param name="palette">Optional named concrete colors.</param>
    /// <param name="name">The display name.</param>
    /// <param name="slug">The stable catalog slug.</param>
    /// <param name="colorScheme">The intended light or dark color scheme.</param>
    /// <param name="author">The attribution author.</param>
    /// <param name="license">The palette license identifier.</param>
    /// <param name="source">The palette source URL.</param>
    /// <exception cref="ArgumentNullException">Required identity or provenance metadata is null.</exception>
    /// <exception cref="ArgumentException">Required metadata is blank, provenance is invalid, or a palette value is transparent.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="colorScheme"/> is undefined.</exception>
    public Theme(
        IReadOnlyDictionary<string, Color>? palette = null,
        string name = "Custom",
        string slug = "custom",
        ColorScheme colorScheme = ColorScheme.Dark,
        string author = "SharpVision contributors",
        string license = "MIT",
        string source = "https://github.com/sharpvision/sharpvision")
    {
        name = RequireMetadata(name, nameof(name));
        slug = ThemeSlug.Validate(RequireMetadata(slug, nameof(slug)), nameof(slug));
        author = RequireMetadata(author, nameof(author));
        license = ThemeProvenance.ValidateLicense(RequireMetadata(license, nameof(license)), nameof(license));
        source = ThemeProvenance.ValidateSource(RequireMetadata(source, nameof(source)), nameof(source));

        ArgumentOutOfRangeException.ThrowIfNotDefined(colorScheme, nameof(colorScheme), "The theme color scheme is unknown.");

        _palette = palette is null
            ? []
            : new Dictionary<string, Color>(palette, StringComparer.Ordinal);

        foreach (var entry in _palette)
        {
            ValidateConcrete(entry.Value, nameof(palette));
        }

        Palette = new ReadOnlyDictionary<string, Color>(_palette);
        Name = name;
        Slug = slug;
        ColorScheme = colorScheme;
        Author = author;
        License = license;
        Source = source;
    }

    private static string RequireMetadata(string value, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);

        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Theme metadata must not be blank.", parameterName)
            : value;
    }

    /// <summary>Gets the human-readable theme name shown in theme pickers and diagnostics (e.g. "Tokyo Night Storm").</summary>
    public string Name { get; }

    /// <summary>Gets the URL-safe identifier used for theme file names and programmatic lookup (e.g. "tokyo-night-storm").</summary>
    public string Slug { get; }

    /// <summary>Gets whether this theme targets dark or light terminal backgrounds.</summary>
    public ColorScheme ColorScheme { get; }

    /// <summary>Gets the theme author's name or handle for attribution in theme catalogs.</summary>
    public string Author { get; }

    /// <summary>Gets the SPDX license identifier governing redistribution of the theme's color palette (e.g. "MIT").</summary>
    public string License { get; }

    /// <summary>Gets the upstream URL where the original color palette is maintained.</summary>
    public string Source { get; }

    /// <summary>Gets whether mutation has been disabled.</summary>
    public bool IsFrozen { get; private set; }

    /// <summary>Gets the retained immutable named concrete palette.</summary>
    public IReadOnlyDictionary<string, Color> Palette { get; }

    /// <summary>Gets the passive base-control semantic appearance.</summary>
    public AppearanceStates Control => GetAppearanceStates("control", static theme => theme.GetStyleSet(ControlStyle.Default));

    /// <summary>Gets the editable or selectable input semantic appearance.</summary>
    public AppearanceStates Input => GetAppearanceStates("input", static theme => theme.GetStyleSet(InputStyle.Default));

    /// <summary>Gets the framed grouping or collection semantic appearance.</summary>
    public AppearanceStates Container => GetAppearanceStates("container", static theme => theme.GetStyleSet(ContainerStyle.Default));

    /// <summary>Gets the top-level window semantic appearance.</summary>
    public AppearanceStates Window => GetAppearanceStates("window", static theme => theme.GetWindowStyleSet());

    /// <summary>Gets the transient popup semantic appearance.</summary>
    public AppearanceStates Popup => GetAppearanceStates("popup", static theme => theme.GetStyleSet(PopupStyle.Default));

    /// <summary>Gets the passive, non-interactive hint semantic appearance.</summary>
    public AppearanceStates Tooltip => GetAppearanceStates("tooltip", static theme => theme.GetStyleSet(TooltipStyle.Default));

    /// <summary>Resolves one known global color to a concrete terminal color.</summary>
    /// <param name="color">The known semantic color.</param>
    /// <returns>The configured concrete terminal color.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="color"/> is unknown.</exception>
    public Color ResolveColor(SemanticColor color)
    {
        ArgumentOutOfRangeException.ThrowIfNotDefined(color, nameof(color), "The theme color is unknown.");
        return _colors[(int) color];
    }

    /// <summary>Resolves one known global semantic decoration to concrete terminal attributes.</summary>
    /// <param name="decoration">The known semantic decoration.</param>
    /// <returns>The configured complete terminal attributes.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="decoration"/> is unknown.</exception>
    public TerminalAttributes ResolveAttributes(SemanticDecoration decoration)
    {
        ArgumentOutOfRangeException.ThrowIfNotDefined(decoration, nameof(decoration), "The theme decoration is unknown.");
        return _attributes[(int) decoration];
    }

    /// <summary>Configures one global semantic color before this Theme is frozen.</summary>
    /// <param name="color">The semantic color to configure.</param>
    /// <param name="value">The concrete terminal color.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="color"/> is undefined.</exception>
    /// <exception cref="ArgumentException"><paramref name="value"/> is transparent.</exception>
    /// <exception cref="InvalidOperationException">This Theme is frozen.</exception>
    public void SetColor(SemanticColor color, Color value)
    {
        if (IsFrozen)
        {
            throw new InvalidOperationException("A frozen theme cannot be changed.");
        }

        ArgumentOutOfRangeException.ThrowIfNotDefined(color);
        ValidateConcrete(value, nameof(value));
        _colors[(int) color] = value;
    }

    /// <summary>Configures one global semantic decoration before this Theme is frozen.</summary>
    /// <param name="decoration">The semantic decoration to configure.</param>
    /// <param name="value">The concrete terminal attributes.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="decoration"/> is undefined.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> contains unknown flags.</exception>
    /// <exception cref="InvalidOperationException">This Theme is frozen.</exception>
    public void SetAttributes(SemanticDecoration decoration, TerminalAttributes value)
    {
        if (IsFrozen)
        {
            throw new InvalidOperationException("A frozen theme cannot be changed.");
        }

        ArgumentOutOfRangeException.ThrowIfNotDefined(decoration);
        ((TerminalAttributes?) value).Validate(null, null);
        _attributes[(int) decoration] = value;
    }

    /// <summary>Configures one well-known root style's complete state set before this Theme is frozen.</summary>
    /// <typeparam name="TStyle">One of the six exact well-known root style types.</typeparam>
    /// <param name="styles">The complete normal style and optional state contributions.</param>
    /// <exception cref="ArgumentNullException"><paramref name="styles"/> is null.</exception>
    /// <exception cref="ArgumentException"><typeparamref name="TStyle"/> is not an exact well-known root style type.</exception>
    /// <exception cref="InvalidOperationException">This Theme is frozen.</exception>
    public void SetStyleSet<TStyle>(StyleStates<TStyle> styles)
        where TStyle : ControlStyle
    {
        if (IsFrozen)
        {
            throw new InvalidOperationException("A frozen theme cannot be changed.");
        }

        ArgumentNullException.ThrowIfNull(styles);
        var styleType = typeof(TStyle);
        _ = GetRootStyleKey(styleType, nameof(styles));
        if (styles.Normal.GetType() != styleType)
        {
            throw new ArgumentException("The normal style must have the exact well-known root style type.", nameof(styles));
        }

        _programmaticStyleSets[styleType] = styles;
        _styleSets.Clear();
        _appearanceSets.Clear();
    }

    internal void SetStyleSections(Dictionary<string, JsonElement> sections)
    {
        if (IsFrozen)
        {
            throw new InvalidOperationException("A frozen theme cannot be changed.");
        }

        _styleSections = sections;
    }

    /// <summary>Gets the raw "styles" document this theme was parsed from, keyed by top-level
    /// section name - the same source <see cref="SetStyleSections"/> installs, exposed read-only
    /// so a derived scenario theme can replicate another theme's complete style customization.</summary>
    internal IReadOnlyDictionary<string, JsonElement> StyleSections => _styleSections;

    /// <summary>Gets the theme-wide glyph family shaping CheckBox, RadioButton, ScrollBar,
    /// Spinner, ProgressBar, and ChaseIndicator's code-owned presentation. Defaults to
    /// <see cref="GlyphFamily.Default"/> - the exact code-owned presentation each of those six
    /// styles carried before this property existed - until a parsed theme's own root-level
    /// "glyphs" field selects a different family.</summary>
    public GlyphFamily Glyphs { get; private set; } = GlyphFamily.Default;

    /// <summary>Configures the theme-wide glyph family before this Theme is frozen.</summary>
    /// <param name="value">The complete glyph family.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    /// <exception cref="InvalidOperationException">This Theme is frozen.</exception>
    public void SetGlyphs(GlyphFamily value)
    {
        if (IsFrozen)
        {
            throw new InvalidOperationException("A frozen theme cannot be changed.");
        }

        ArgumentNullException.ThrowIfNull(value);
        Glyphs = value;
        _styleSets.Clear();
        _appearanceSets.Clear();
    }

    // Mirrors ThemeCatalog.ResolveColorValue/ResolveColor exactly, but reads this Theme's own public
    // Palette instead of the private dictionary the eager document parse builds - a role
    // section's color members are resolved lazily, long after that parse-time dictionary is gone.
    internal ControlColor ResolveSectionColor(string value, string context)
    {
        ArgumentNullException.ThrowIfNull(value);

        return Palette.TryGetValue(value, out var paletteColor)
            ? paletteColor
            : string.Equals(value, "transparent", StringComparison.OrdinalIgnoreCase)
                ? Color.Transparent
                : string.Equals(value, "default", StringComparison.OrdinalIgnoreCase)
                    ? Color.Default
                    : TryParseNamedEnum(value, out SemanticColor semantic)
                        ? semantic
                        : throw new InvalidDataException(
                            $"Theme '{DiagnosticSource}' {context} references unknown palette key '{value}'.");
    }

    // Shared by every style section's glyph members - previously
    // hand-copied verbatim into six controls' own ParseGlyph helpers.
    internal Rune? ParseSectionGlyph(string? value, string context)
    {
        if (value is null)
        {
            return null;
        }

        var enumerator = value.EnumerateRunes();

        if (!enumerator.MoveNext())
        {
            throw new InvalidDataException($"Theme '{DiagnosticSource}' {context} must contain one Rune.");
        }

        var result = enumerator.Current;

        if (enumerator.MoveNext())
        {
            throw new InvalidDataException($"Theme '{DiagnosticSource}' {context} must contain one Rune.");
        }

        // Counting Runes is only half of what themes.md promises: it also states a value measuring
        // wider than one cell is rejected the same way a hand-authored glyph is. Measuring here
        // means the theme slug and the dotted styles.* path are still in hand; the alternative is
        // the same rejection arriving from inside the render loop with neither.
        try
        {
            return result.ValidateSingleCell(nameof(value));
        }
        catch (ArgumentException error)
        {
            throw new InvalidDataException($"Theme '{DiagnosticSource}' {context} must be one cell wide.", error);
        }
    }

    // Shared by every style section's named-enum members -
    // previously hand-copied verbatim into four controls' own ParseMarkStyle/ParseChrome/
    // ParseFill helpers.
    internal TEnum? ParseSectionEnum<TEnum>(string? value, string context) where TEnum : struct, Enum =>
        value is null
            ? null
            : TryParseNamedEnum(value, out TEnum result)
                ? result
                : throw new InvalidDataException($"Theme '{DiagnosticSource}' {context} has unknown value '{value}'.");

    private static bool TryParseNamedEnum<TEnum>(string value, out TEnum result)
        where TEnum : struct, Enum
    {
        var names = Enum.GetNames<TEnum>();
        var tokens = value.Split(',', StringSplitOptions.TrimEntries);
        if (tokens.Length == 0 || tokens.Any(token =>
                token.Length == 0 || !names.Contains(token, StringComparer.OrdinalIgnoreCase)))
        {
            result = default;
            return false;
        }

        return Enum.TryParse(value, ignoreCase: true, out result) && IsRepresentable(result);
    }

    /// <summary>Reports whether one parsed enum value is expressible in its type.</summary>
    /// <remarks>
    /// <see cref="Enum.IsDefined{TEnum}(TEnum)"/> is false for any <c>[Flags]</c> combination that is
    /// not itself a declared member, which made <c>"top, bottom"</c> - a well-formed, type-legal
    /// <see cref="BorderSide"/> value that every other layer accepts - fail with a message asserting
    /// it was unknown. A flags value is instead checked for bits outside the declared set, exactly
    /// as <c>Border</c>'s and <c>BorderOverlay</c>'s own constructors do. Plain enums keep the strict
    /// membership test.
    /// </remarks>
    /// <typeparam name="TEnum">The parsed enum type.</typeparam>
    /// <param name="value">The parsed value.</param>
    /// <returns>Whether the value is representable.</returns>
    private static bool IsRepresentable<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        if (!typeof(TEnum).IsDefined(typeof(FlagsAttribute), inherit: false))
        {
            return Enum.IsDefined(value);
        }

        var declared = 0UL;

        foreach (var member in Enum.GetValues<TEnum>())
        {
            declared |= Convert.ToUInt64(member, CultureInfo.InvariantCulture);
        }

        var bits = Convert.ToUInt64(value, CultureInfo.InvariantCulture);
        return (bits & ~declared) == 0;
    }

    private static readonly MethodInfo _parseSectionEnumDefinition =
        typeof(Theme).GetMethod(nameof(ParseSectionEnum), BindingFlags.NonPublic | BindingFlags.Instance)!;
    private static readonly ConcurrentDictionary<Type, MethodInfo> _parseSectionEnumMethods = new();

    /// <summary>Patches a fragment's properties from a JSON overrides object, recursing into any
    /// property whose type is itself a fragment and replacing every other (leaf) property
    /// outright - the reflective mechanism every control style's JSON section resolves through,
    /// replacing the six/four-way hand-copied glyph/enum parsers already unified
    /// into <see cref="ParseSectionGlyph"/>/<see cref="ParseSectionEnum{TEnum}"/> above, which this
    /// now calls as its leaf dispatch for those two member kinds.</summary>
    /// <param name="current">The fragment instance overrides are patched onto. Cloned before any
    /// property is written, so the original instance is never mutated.</param>
    /// <param name="overrides">The JSON object's own decoded member set.</param>
    /// <param name="context">The dotted path so far, for diagnostics (e.g. "styles.control.normal").</param>
    /// <param name="restrictToChrome">Whether only <c>Face</c>/<c>Border</c>/<c>Shadow</c> - the
    /// three members every state besides <c>normal</c> is actually read back from - may be authored
    /// at this level. A structural member (padding, mark style, a glyph family) is otherwise parsed,
    /// validated, and then never read by anything: <see cref="AppearanceOverlay"/> carries only
    /// Face/Border/Shadow, and every style resolution always completes its structural members from
    /// <c>normal</c>. Recursing into an already-admitted Face/Border/Shadow fragment keeps this
    /// false, since every member below that point is legitimate chrome regardless of state.</param>
    /// <returns>A new, patched fragment instance of the same runtime type as <paramref name="current"/>.</returns>
    /// <exception cref="InvalidDataException">
    /// An override key does not map to any public property, a value does not convert to its
    /// property's type, or <paramref name="restrictToChrome"/> is true and the key names a
    /// structural member.
    /// </exception>
    internal object Overlay(
        object current,
        Dictionary<string, JsonElement> overrides,
        string context,
        bool restrictToChrome = false)
    {
        var patched = ((IAppearanceFragment) current).Clone();
        var type = patched.GetType();

        foreach (var (key, value) in overrides)
        {
            var property = ThemeStyleFragment.ResolveProperty(type, key)
                ?? throw new InvalidDataException($"Theme '{DiagnosticSource}' '{context}.{key}' is not a known property.");

            if (restrictToChrome && property.DeclaringType != typeof(ControlStyle))
            {
                throw new InvalidDataException(
                    $"Theme '{DiagnosticSource}' '{context}.{key}' is a structural member and only takes effect under 'normal'.");
            }

            object? updated;

            try
            {
                updated = typeof(IAppearanceFragment).IsAssignableFrom(property.PropertyType)
                    ? Overlay(
                        property.GetValue(patched)!,
                        value.Deserialize<Dictionary<string, JsonElement>>(ThemeCatalog.JsonOptions)
                            ?? throw new InvalidDataException($"Theme '{DiagnosticSource}' '{context}.{key}' must be an object."),
                        $"{context}.{key}")
                    : ConvertLeaf(property.PropertyType, value, $"{context}.{key}");

                // Inside the try, not below it. A validating init accessor - separator's glyphs,
                // spinner's frames, window's close chrome, every ControlColor paint channel - is
                // reached through reflection, which wraps whatever it throws in
                // TargetInvocationException, so the write is exactly where a labelled failure is
                // most likely and was the one step outside the handler.
                property.SetValue(patched, updated);
            }
            catch (Exception error) when (error is JsonException or InvalidCastException or FormatException
                or ArgumentException or TargetInvocationException)
            {
                // Every failure inside the merge - wrong JSON shape, unconvertible leaf value, a
                // member rejecting the value it was handed - surfaces as the same source-labelled
                // InvalidDataException every other theme parsing failure does today, never a raw
                // reflection or conversion exception. The accessor's own exception is unwrapped so
                // the inner cause names the real problem rather than "an exception was thrown by
                // the target of an invocation", the same unwrapping ConvertLeaf's enum branch
                // already does.
                throw new InvalidDataException(
                    $"Theme '{DiagnosticSource}' '{context}.{key}' is invalid.",
                    error is TargetInvocationException { InnerException: { } inner } ? inner : error);
            }
        }

        // Every per-member check has already run inside its own init accessor above. What is left
        // is the invariants that span members - Face's attribute/underline conflict - which no
        // accessor can enforce while the rest of the fragment is still half-written.
        try
        {
            patched.Validate();
        }
        catch (ArgumentException error)
        {
            throw new InvalidDataException($"Theme '{DiagnosticSource}' '{context}' is invalid.", error);
        }

        return patched;
    }

    // Dispatches purely on the target property's declared type. ControlColor and Rune route through
    // the same theme-aware parsers every style section already uses; an enum invokes
    // the generic ParseSectionEnum<TEnum> via a per-type-cached MethodInfo, since the concrete
    // TEnum is only known at this point as a runtime Type; everything else (primitives, and any
    // other JSON-convertible leaf shape such as Thickness or Point) deserializes directly through
    // the shared, depth-limited JsonSerializerOptions.
    private object? ConvertLeaf(Type propertyType, JsonElement value, string context)
    {
        // A nullable leaf is a real style shape, not an oversight: TableStyle's three part colors
        // use null to mean "inherit the control's own resolved face", which no fixed ControlColor
        // can express and which also gates whether the header row is filled at all. Without this
        // the member would fall through to plain JSON deserialization and silently mis-convert.
        if (Nullable.GetUnderlyingType(propertyType) is { } underlying)
        {
            return value.ValueKind == JsonValueKind.Null ? null : ConvertLeaf(underlying, value, context);
        }

        if (propertyType == typeof(ControlColor))
        {
            return ResolveSectionColor(RequireString(value, context), context);
        }

        if (propertyType == typeof(Rune))
        {
            return ParseSectionGlyph(RequireString(value, context), context)
                ?? throw new InvalidDataException($"Theme '{DiagnosticSource}' '{context}' must contain one Rune.");
        }

        if (propertyType == typeof(ControlDecoration))
        {
            return ResolveSectionDecoration(value, context);
        }

        if (propertyType == typeof(BorderGlyphStyle))
        {
            return ResolveSectionBorderGlyphStyle(value, context);
        }

        if (propertyType == typeof(ImmutableArray<Rune>))
        {
            return ResolveSectionGlyphSequence(value, context);
        }

        if (propertyType == typeof(Thickness))
        {
            return ResolveSectionThickness(value, context);
        }

        if (propertyType == typeof(Point))
        {
            return ResolveSectionPoint(value, context);
        }

        if (propertyType.IsEnum)
        {
            var method = _parseSectionEnumMethods.GetOrAdd(
                propertyType,
                static (type, definition) => definition.MakeGenericMethod(type),
                _parseSectionEnumDefinition);

            try
            {
                return method.Invoke(this, [RequireString(value, context), context])
                    ?? throw new InvalidDataException($"Theme '{DiagnosticSource}' '{context}' has an unknown value.");
            }
            catch (TargetInvocationException error) when (error.InnerException is not null)
            {
                // Invoke wraps whatever the invoked method threw - here always the
                // InvalidDataException ParseSectionEnum<TEnum> already raises for an unknown
                // value - so unwrap it instead of letting the wrapper reach the catch below,
                // which does not recognize TargetInvocationException as one of the exception
                // kinds it already normalizes.
                ExceptionDispatchInfo.Throw(error.InnerException);
                throw; // Unreachable; ExceptionDispatchInfo.Throw always throws.
            }
        }

        return value.Deserialize(propertyType, ThemeCatalog.JsonOptions)
            ?? throw new InvalidDataException($"Theme '{DiagnosticSource}' '{context}' must not be null.");
    }

    private string RequireString(JsonElement value, string context) =>
        value.ValueKind == JsonValueKind.String
            ? value.GetString()!
            : throw new InvalidDataException($"Theme '{DiagnosticSource}' '{context}' must be a string.");

    // Mirrors ThemeCatalog.ParseAttributes exactly, via the shared ConvertLeaf unification: a string
    // first tries a semantic SemanticDecoration name, then falls back to one literal named
    // attribute; an array combines one or more literal named attributes.
    internal ControlDecoration ResolveSectionDecoration(JsonElement value, string context)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            var name = value.GetString()!;
            return Enum.TryParse<SemanticDecoration>(name, ignoreCase: true, out var semantic) && Enum.IsDefined(semantic)
                ? semantic
                : ThemeCatalog.ResolveAttributeName(name, DiagnosticSource, context);
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException($"Theme '{DiagnosticSource}' '{context}' must be a string or array.");
        }

        var result = TerminalAttributes.None;
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                throw new InvalidDataException($"Theme '{DiagnosticSource}' '{context}' must contain strings.");
            }

            result |= ThemeCatalog.ResolveAttributeName(item.GetString()!, DiagnosticSource, context);
        }

        return result;
    }

    // Resolves a string or eight-element array border glyph style, structured like the
    // section-color and section-attribute resolvers above: a string names one of the ten
    // standard box-drawing families; an eight-element array of one-Rune strings defines a
    // custom glyph set corner-then-edge, matching BorderGlyphStyle's own constructor order.
    internal BorderGlyphStyle ResolveSectionBorderGlyphStyle(JsonElement value, string context)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            var name = value.GetString()!;
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
                _ => throw new InvalidDataException($"Theme '{DiagnosticSource}' '{context}' has unknown glyph style '{name}'.")
            };
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException($"Theme '{DiagnosticSource}' '{context}' must be a string or an eight-Rune array.");
        }

        var runes = new Rune[8];
        var index = 0;
        foreach (var item in value.EnumerateArray())
        {
            if (index >= runes.Length || item.ValueKind != JsonValueKind.String)
            {
                throw new InvalidDataException(
                    $"Theme '{DiagnosticSource}' '{context}' array must contain exactly eight one-Rune strings.");
            }

            runes[index] = ParseSectionGlyph(item.GetString(), $"{context}[{index}]")!.Value;
            index++;
        }

        return index != runes.Length
            ? throw new InvalidDataException($"Theme '{DiagnosticSource}' '{context}' array must contain exactly eight Runes.")
            : new BorderGlyphStyle(runes[0], runes[1], runes[2], runes[3], runes[4], runes[5], runes[6], runes[7]);
    }

    // Backs SpinnerStyle.Frames - the one property in the codebase typed as a Rune sequence
    // rather than a single Rune. The bound below intentionally mirrors
    // SpinnerStyle.MaximumFrameCount (Styling cannot reference Controls.Display, so this is kept
    // in sync by convention, not by a shared constant).
    private const int _maximumGlyphSequenceLength = 256;

    internal ImmutableArray<Rune> ResolveSectionGlyphSequence(JsonElement value, string context)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException($"Theme '{DiagnosticSource}' '{context}' must be an array.");
        }

        var builder = ImmutableArray.CreateBuilder<Rune>();
        foreach (var item in value.EnumerateArray())
        {
            if (builder.Count == _maximumGlyphSequenceLength)
            {
                throw new InvalidDataException(
                    $"Theme '{DiagnosticSource}' '{context}' cannot contain more than {_maximumGlyphSequenceLength} entries.");
            }

            builder.Add(ParseSectionGlyph(RequireString(item, context), context)!.Value);
        }

        return builder.Count == 0
            ? throw new InvalidDataException($"Theme '{DiagnosticSource}' '{context}' must contain at least one entry.")
            : builder.ToImmutable();
    }

    // Thickness has no public parameterless constructor with settable properties (Left/Top/Right/
    // Bottom are get-only), so the generic reflective JsonSerializer.Deserialize fallback silently
    // produces an all-zero value instead of erroring - a dedicated leaf conversion is required, the
    // same way ControlColor/Rune/ControlDecoration/BorderGlyphStyle/ImmutableArray<Rune> already are.
    internal Thickness ResolveSectionThickness(JsonElement value, string context)
    {
        if (value.ValueKind != JsonValueKind.Object ||
            !HasExactGeometryMembers(value) ||
            !value.TryGetProperty("x", out var x) ||
            !value.TryGetProperty("y", out var y) ||
            x.ValueKind != JsonValueKind.Number ||
            y.ValueKind != JsonValueKind.Number)
        {
            throw new InvalidDataException($"Theme '{DiagnosticSource}' '{context}' must be an object with numeric 'x' and 'y'.");
        }

        var horizontal = x.GetInt32();
        var vertical = y.GetInt32();

        return horizontal < 0 || vertical < 0
            ? throw new InvalidDataException($"Theme '{DiagnosticSource}' '{context}' must not be negative.")
            : new Thickness(horizontal, vertical);
    }

    // Point has the same reflective-deserialization pitfall as Thickness above (an explicit
    // constructor alongside the struct's implicit parameterless one), but unlike Thickness its X/Y
    // are signed and a negative offset is legitimate (e.g. a shadow drawn up-and-left), so no
    // negative-value rejection here.
    internal Point ResolveSectionPoint(JsonElement value, string context) =>
        value.ValueKind != JsonValueKind.Object ||
        !HasExactGeometryMembers(value) ||
        !value.TryGetProperty("x", out var x) ||
        !value.TryGetProperty("y", out var y) ||
        x.ValueKind != JsonValueKind.Number ||
        y.ValueKind != JsonValueKind.Number
            ? throw new InvalidDataException($"Theme '{DiagnosticSource}' '{context}' must be an object with numeric 'x' and 'y'.")
            : new Point(x.GetInt32(), y.GetInt32());

    private static bool HasExactGeometryMembers(JsonElement value)
    {
        var count = 0;

        foreach (var property in value.EnumerateObject())
        {
            if (property.Name is not ("x" or "y") || ++count > 2)
            {
                return false;
            }
        }

        return count == 2;
    }

    private static readonly string[] _visualStateJsonNames =
    [
        "normal", "pointerOver", "focusWithin", "focused", "current", "selected", "checked",
        "indeterminate", "pressed", "disabled"
    ];

    private readonly ConcurrentDictionary<string, Dictionary<string, Dictionary<string, JsonElement>>?> _rawStyleSections = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<(Type, string, ControlStyle), Lazy<object>> _styleSets = new();
    private readonly ConcurrentDictionary<string, Lazy<AppearanceStates>> _appearanceSets = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<Type, object> _programmaticStyleSets = new();

    // Raw per-state JSON override dictionaries for one styles.* key, with no code-owned default
    // baked in and no fallback awareness - the primitive both GetStyleSet (root types) and a leaf
    // control's own fallback-aware Appearance resolution patch onto their own,
    // differently-sourced base value. Memoized: the same (theme, key) pair is read at least twice
    // per CommitStyle/GetStyleThemeImpact call (previous and current resolved style).
    internal Dictionary<string, Dictionary<string, JsonElement>>? GetRawStyleSection(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        return _rawStyleSections.GetOrAdd(key, static (sectionKey, theme) => theme.ReadRawStyleSection(sectionKey), this);
    }

    private Dictionary<string, Dictionary<string, JsonElement>>? ReadRawStyleSection(string key)
    {
        if (!_styleSections.TryGetValue(key, out var element))
        {
            return null;
        }

        var byState = ThemeCatalog.ReadObject(element, DiagnosticSource, $"styles.{key}");

        var result = new Dictionary<string, Dictionary<string, JsonElement>>(StringComparer.Ordinal);
        foreach (var (state, value) in byState)
        {
            if (!Array.Exists(_visualStateJsonNames, name => string.Equals(name, state, StringComparison.Ordinal)))
            {
                throw new InvalidDataException($"Theme '{DiagnosticSource}' 'styles.{key}' has unknown state '{state}'.");
            }

            result[state] = ThemeCatalog.ReadObject(value, DiagnosticSource, $"styles.{key}.{state}");
        }

        return result;
    }

    /// <summary>Resolves one root well-known style's complete per-state set from the section
    /// <typeparamref name="TStyle"/> itself owns (see <see cref="StyleKey"/>), so no caller
    /// repeats the key as a literal.</summary>
    /// <typeparam name="TStyle">The style type that owns the section.</typeparam>
    /// <param name="codeOwnedDefault">The code-owned default this type falls back to.</param>
    /// <returns>The complete per-state set.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="codeOwnedDefault"/> is null.</exception>
    /// <exception cref="ArgumentException"><typeparamref name="TStyle"/> is not one of the six well-known root style types.</exception>
    public StyleStates<TStyle> GetStyleSet<TStyle>(TStyle codeOwnedDefault)
        where TStyle : ControlStyle
    {
        ArgumentNullException.ThrowIfNull(codeOwnedDefault);

        var styleType = typeof(TStyle);
        if (codeOwnedDefault.GetType() != styleType)
        {
            throw new ArgumentException("The default must have the exact well-known root style type.", nameof(codeOwnedDefault));
        }

        var key = GetRootStyleKey(styleType, nameof(codeOwnedDefault));

        return _programmaticStyleSets.TryGetValue(styleType, out var configured)
            ? (StyleStates<TStyle>) configured
            : GetStyleSet(key, codeOwnedDefault);
    }

    private static string GetRootStyleKey(Type styleType, string parameterName) =>
        styleType == typeof(ControlStyle) ? StyleKey.Of<ControlStyle>() :
        styleType == typeof(InputStyle) ? StyleKey.Of<InputStyle>() :
        styleType == typeof(ContainerStyle) ? StyleKey.Of<ContainerStyle>() :
        styleType == typeof(WindowStyle) ? StyleKey.Of<WindowStyle>() :
        styleType == typeof(PopupStyle) ? StyleKey.Of<PopupStyle>() :
        styleType == typeof(TooltipStyle) ? StyleKey.Of<TooltipStyle>() :
        throw new ArgumentException("Only the six well-known root style types own theme sections.", parameterName);

    /// <summary>Resolves one root style's complete per-state set from an explicit key, memoized
    /// per theme, style type, key, and code-owned default.</summary>
    /// <typeparam name="TStyle">The complete style type to resolve.</typeparam>
    /// <param name="key">The validated theme-section key.</param>
    /// <param name="codeOwnedDefault">The code-owned default patched by the theme section.</param>
    /// <returns>The complete normal style and sparse state contributions.</returns>
    internal StyleStates<TStyle> GetStyleSet<TStyle>(string key, TStyle codeOwnedDefault)
        where TStyle : ControlStyle
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(codeOwnedDefault);

        return (StyleStates<TStyle>) _styleSets.GetOrAdd(
            (typeof(TStyle), key, codeOwnedDefault),
            static (_, state) => new Lazy<object>(
                () => state.theme.BuildRootStyleSet(state.key, state.codeOwnedDefault),
                LazyThreadSafetyMode.ExecutionAndPublication),
            (theme: this, key, codeOwnedDefault)).Value;
    }

    /// <summary>Compiles every closed root style section before a loader publishes this Theme.</summary>
    internal void ValidateStyleSections()
    {
        _ = GetStyleSet(ControlStyle.Default);
        _ = GetStyleSet(InputStyle.Default);
        _ = GetStyleSet(ContainerStyle.Default);
        _ = GetWindowStyleSet();
        _ = GetStyleSet(PopupStyle.Default);
        _ = GetStyleSet(TooltipStyle.Default);
    }

    /// <summary>Gets Input's interaction state deltas rebased onto the passive control's own
    /// borderless Normal geometry: every interactive state - hover, focus, press, selection, and
    /// the rest - reacts. Use this as a leaf style's <c>StyleDefinitions.Control</c> fallback
    /// target when the control is borderless and owns direct interaction outright, rather than
    /// adopting input-field geometry - a slider, a scroll bar, or any other plain clickable
    /// control.</summary>
    /// <remarks>
    /// <see cref="GetInteractiveRowStyleSet"/> is the row-flavored sibling: identical except
    /// pointer hover keeps this geometry's own background instead of adopting Input's hover fill,
    /// for a selectable row whose selection - not mere pointer membership - owns the highlighted
    /// fill. <see cref="GetFocusableControlStyleSet"/> is the narrower sibling for a borderless
    /// control that is a direct focus target but already owns hover, press, and selection more
    /// specifically through its own content, and so rebases only Focused/FocusWithin.
    ///
    /// <para>Every theme bundled with SharpVision signals focus through a border color change
    /// alone, which this borderless geometry has none of. When Focused or FocusWithin would
    /// otherwise resolve to exactly Normal's own foreground and background, this forces the
    /// terminal's reverse-video attribute on top as a safety net so focus stays visible; a custom
    /// theme that already authors genuinely different focus colors is used exactly as authored,
    /// with nothing forced on top of it.</para>
    /// </remarks>
    /// <returns>The cached complete per-state control-style set.</returns>
    public StyleStates<ControlStyle> GetInteractiveControlStyleSet() =>
        (StyleStates<ControlStyle>) _styleSets.GetOrAdd(
            (typeof(ControlStyle), "$interactiveControl", ControlStyle.Default),
            static (_, theme) => new Lazy<object>(
                () => theme.BuildInteractiveStyleSet(theme.GetStyleSet(ControlStyle.Default), preservePointerBackground: false),
                LazyThreadSafetyMode.ExecutionAndPublication),
            this).Value;

    /// <summary>Gets Input's interaction state deltas rebased onto the passive control's own
    /// borderless Normal geometry, the same as <see cref="GetInteractiveControlStyleSet"/> except
    /// pointer hover retains this geometry's own background instead of adopting Input's hover
    /// fill. Use this as a leaf style's <c>StyleDefinitions.Control</c> fallback target for a
    /// selectable borderless row whose selection - rather than mere pointer membership - owns the
    /// highlighted fill, such as a list, tree, or combo-box item.</summary>
    /// <remarks>
    /// See <see cref="GetInteractiveControlStyleSet"/> for every other interactive state,
    /// including the reverse-video safety net Focused/FocusWithin share with it, and
    /// <see cref="GetFocusableControlStyleSet"/> for the narrower sibling that rebases only
    /// Focused/FocusWithin.
    /// </remarks>
    /// <returns>The cached complete per-state control-style set.</returns>
    public StyleStates<ControlStyle> GetInteractiveRowStyleSet() =>
        (StyleStates<ControlStyle>) _styleSets.GetOrAdd(
            (typeof(ControlStyle), "$interactiveRow", ControlStyle.Default),
            static (_, theme) => new Lazy<object>(
                () => theme.BuildInteractiveStyleSet(theme.GetStyleSet(ControlStyle.Default), preservePointerBackground: true),
                LazyThreadSafetyMode.ExecutionAndPublication),
            this).Value;

    /// <summary>Gets every interactive state rebased onto passive borderless geometry without the
    /// reverse-video safety net used by a generic borderless focus target.</summary>
    /// <remarks>
    /// A targeted control paints independently meaningful targets with semantic foregrounds.
    /// Reversing the owner would turn those target colors into unrelated background blocks and
    /// reverse any unused owner cells. Authored focus colors and text decoration remain intact,
    /// along with hover, press, selection, current, and disabled state resolution.
    /// </remarks>
    /// <returns>The cached complete per-state control-style set for targeted surfaces.</returns>
    internal StyleStates<ControlStyle> GetTargetedInteractiveControlStyleSet() =>
        (StyleStates<ControlStyle>) _styleSets.GetOrAdd(
            (typeof(ControlStyle), "$targetedInteractiveControl", ControlStyle.Default),
            static (_, theme) => new Lazy<object>(
                () => theme.BuildInteractiveStyleSet(
                    theme.GetStyleSet(ControlStyle.Default),
                    preservePointerBackground: false,
                    applyBorderlessFocusFallback: false),
                LazyThreadSafetyMode.ExecutionAndPublication),
            this).Value;

    private StyleStates<TGeometry> BuildInteractiveStyleSet<TGeometry>(
        StyleStates<TGeometry> geometry,
        bool preservePointerBackground,
        bool applyBorderlessFocusFallback = true)
        where TGeometry : ControlStyle
    {
        var input = GetStyleSet(InputStyle.Default);

        TGeometry? Rebase(InputStyle? state, string stateName, bool preserveBackground = false)
        {
            if (state is null)
            {
                return null;
            }

            var rebased = Cascade(
                geometry.Normal,
                StyleStatesExtensions.Diff(input.Normal, state, input.AuthoredFor(stateName)));

            if (applyBorderlessFocusFallback && (stateName is "focused" or "focusWithin"))
            {
                rebased = ApplyBorderlessFocusFallback(rebased, geometry.Normal);
            }

            return preserveBackground
                ? rebased with { Face = rebased.Face with { Background = geometry.Normal.Face.Background } }
                : rebased;
        }

        return new StyleStates<TGeometry>
        {
            Normal = geometry.Normal,
            IsPointerOver = Rebase(input.IsPointerOver, "pointerOver", preservePointerBackground),
            FocusWithin = Rebase(input.FocusWithin, "focusWithin"),
            Focused = Rebase(input.Focused, "focused"),
            Current = Rebase(input.Current, "current"),
            Selected = Rebase(input.Selected, "selected"),
            Checked = Rebase(input.Checked, "checked"),
            Indeterminate = Rebase(input.Indeterminate, "indeterminate"),
            Pressed = Rebase(input.Pressed, "pressed"),
            Disabled = Rebase(input.Disabled, "disabled"),
            Authored = input.Authored
        };
    }

    // Every bundled theme maps "focusedControl" and "focusedText" to the exact same literal color
    // as "control"/"controlText" (unlike "activeControl", which genuinely differs from "control"
    // in every one of them) - a deliberate choice documented in themes.md: "Focus ... remains
    // visible without introducing an alarm-like fill or text color", relying entirely on the
    // border switching to focusedBorder/activeBorder instead. That signal does not exist for a
    // borderless control - Slider, ScrollBar, Table, and every other consumer of
    // GetInteractiveControlStyleSet/GetInteractiveRowStyleSet/GetFocusableControlStyleSet that
    // never enables a border - which left the SemanticDecoration.FocusedText attribute (bold in
    // every bundled theme) as the only surviving cue. Bold alone is frequently imperceptible for
    // line-drawing and block glyphs (a Slider's own ◆/━ track has no distinct bold glyph in most
    // terminal fonts), which is exactly the gap this closes: forcing Reverse - universally
    // rendered as a literal foreground/background swap by every terminal, unlike a font-dependent
    // bold weight - onto Focused/FocusWithin specifically, but ONLY as a last-resort safety net.
    //
    // A custom theme that already authors a genuinely different foreground or background for
    // "input.focused" - as opposed to the bundled themes' focusedControl/focusedText, which
    // resolve to the exact same literal color as control/controlText in every one of them - has
    // already solved the visibility problem on its own terms and must not have Reverse silently
    // forced on top of a deliberate choice to omit it. Comparing resolved literal colors rather
    // than ControlColor identity is load-bearing: SemanticColor.FocusedControl and
    // SemanticColor.Control are always different tokens even when a theme maps both to the same
    // RGB value, so a token-identity comparison would never detect the bundled themes' collapse.
    //
    // A bordered geometry (Container's all-sides light border, adopted by TreeView/JsonView) is
    // left untouched entirely: its border color change already works regardless of this
    // comparison, and forcing Reverse there too would be the redundant, alarm-like double signal
    // the bundled themes deliberately avoid.
    private TStyle ApplyBorderlessFocusFallback<TStyle>(TStyle rebased, ControlStyle geometryNormal)
        where TStyle : ControlStyle
    {
        if (geometryNormal.Border.Sides != BorderSide.None)
        {
            return rebased;
        }

        if (ResolveColorValue(rebased.Face.Foreground) != ResolveColorValue(geometryNormal.Face.Foreground) ||
            ResolveColorValue(rebased.Face.Background) != ResolveColorValue(geometryNormal.Face.Background))
        {
            return rebased;
        }

        var attributes = rebased.Face.Attributes;
        var literalAttributes = attributes.IsLiteral ? attributes.Literal : ResolveAttributes(attributes.SemanticDecoration);

        var normalAttributes = geometryNormal.Face.Attributes;
        var literalNormalAttributes = normalAttributes.IsLiteral
            ? normalAttributes.Literal
            : ResolveAttributes(normalAttributes.SemanticDecoration);

        // Key off Normal's own resolved Reverse bit rather than blindly OR-ing: a custom theme may
        // already author Reverse on this borderless control's Normal face (independently of the
        // color collapse checked above, which only compares Foreground/Background), in which case
        // OR-ing Reverse again onto Focused would be a no-op and leave Focused byte-identical to
        // Normal - defeating this fallback entirely. Flipping the bit relative to Normal guarantees
        // Focused always differs from Normal regardless of which direction Normal already points.
        var finalAttributes = literalNormalAttributes.HasFlag(TerminalAttributes.Reverse)
            ? literalAttributes & ~TerminalAttributes.Reverse
            : literalAttributes | TerminalAttributes.Reverse;

        return rebased with { Face = rebased.Face with { Attributes = finalAttributes } };
    }

    private Color ResolveColorValue(ControlColor value) => value.IsLiteral ? value.Literal : ResolveColor(value.SemanticColor);

    /// <summary>Gets only the Focused/FocusWithin state deltas rebased onto the passive
    /// container's own all-sides-bordered Normal geometry, leaving every other state exactly as
    /// the passive "container" key resolves it. Use this as a leaf style's
    /// <c>StyleDefinitions.Control</c> fallback target for a directly focusable container-shaped
    /// control whose own content already owns hover, press, selection, and current-item cues more
    /// specifically - a TreeView or JsonView. Rebasing every interactive state the way
    /// <see cref="GetInteractiveControlStyleSet"/> does would light up a data-dense tree's entire
    /// frame on mere mouse-over instead of leaving that to its rows.</summary>
    /// <remarks>
    /// <see cref="GetFocusableControlStyleSet"/> is the borderless-geometry sibling for the same
    /// kind of control shape without the container's own border.
    ///
    /// <para>The rebased border keeps its own sides and glyph style; only its color reacts, so
    /// focus can never change the container's measured size. Unlike the borderless sets above, no
    /// reverse-video fallback applies here - the border's own color change is already a visible
    /// cue on its own.</para>
    /// </remarks>
    /// <returns>The cached complete per-state container-style set.</returns>
    public StyleStates<ContainerStyle> GetFocusableContainerStyleSet() =>
        (StyleStates<ContainerStyle>) _styleSets.GetOrAdd(
            (typeof(ContainerStyle), "$focusableContainer", ContainerStyle.Default),
            static (_, theme) => new Lazy<object>(
                () => theme.BuildFocusableStyleSet(theme.GetStyleSet(ContainerStyle.Default)),
                LazyThreadSafetyMode.ExecutionAndPublication),
            this).Value;

    /// <summary>Gets only the Focused/FocusWithin state deltas rebased onto the passive control's
    /// own borderless Normal geometry, leaving every other state exactly as the passive "control"
    /// key resolves it. Use this as a leaf style's <c>StyleDefinitions.Control</c> fallback target
    /// for a directly focusable borderless control whose own content already owns hover, press,
    /// selection, and current-item cues more specifically - a Table.</summary>
    /// <remarks>
    /// See <see cref="GetFocusableContainerStyleSet"/> for the bordered-geometry sibling and for
    /// why both are narrower than
    /// <see cref="GetInteractiveControlStyleSet"/>/<see cref="GetInteractiveRowStyleSet"/>.
    ///
    /// <para>Every theme bundled with SharpVision signals focus through a border color change
    /// alone, which this borderless geometry has none of. When Focused or FocusWithin would
    /// otherwise resolve to exactly Normal's own foreground and background, this forces the
    /// terminal's reverse-video attribute on top as a safety net so focus stays visible; a custom
    /// theme that already authors genuinely different focus colors is used exactly as authored,
    /// with nothing forced on top of it.</para>
    /// </remarks>
    /// <returns>The cached complete per-state control-style set.</returns>
    public StyleStates<ControlStyle> GetFocusableControlStyleSet() =>
        (StyleStates<ControlStyle>) _styleSets.GetOrAdd(
            (typeof(ControlStyle), "$focusableControl", ControlStyle.Default),
            static (_, theme) => new Lazy<object>(
                () => theme.BuildFocusableStyleSet(theme.GetStyleSet(ControlStyle.Default)),
                LazyThreadSafetyMode.ExecutionAndPublication),
            this).Value;

    /// <summary>Gets only the Focused/FocusWithin state deltas rebased onto the passive control's
    /// own borderless Normal geometry, leaving every other state exactly as the passive "control"
    /// key resolves it, and without the reverse-video safety net that
    /// <see cref="GetFocusableControlStyleSet"/> applies. Use this as a leaf style's
    /// <c>StyleDefinitions.Control</c> fallback target for a directly focusable borderless control
    /// that paints one large owner surface behind independently colored content it does not fully
    /// own the coloring of - a Table or Document.</summary>
    /// <remarks>
    /// <see cref="GetFocusableControlStyleSet"/> is the same borderless-geometry rebasing with the
    /// reverse-video fallback included; prefer that one unless the fallback would actively hurt
    /// readability for this control's shape.
    ///
    /// <para>A table or document paints one large owner surface behind independently styled cells
    /// or blocks. Reversing that surface on focus produces a solid focus slab with normal-background
    /// islands wherever the content paints its own faces. This style set retains the theme's
    /// focused text decoration and authored colors; it merely omits the synthetic reverse fallback
    /// while row, cell, or selection styling owns the strong filled cue instead.</para>
    /// </remarks>
    /// <returns>The cached complete per-state control-style set for tabular, owner-surface
    /// controls.</returns>
    public StyleStates<ControlStyle> GetTabularControlStyleSet() =>
        (StyleStates<ControlStyle>) _styleSets.GetOrAdd(
            (typeof(ControlStyle), "$tabularControl", ControlStyle.Default),
            static (_, theme) => new Lazy<object>(
                () => theme.BuildFocusableStyleSet(
                    theme.GetStyleSet(ControlStyle.Default),
                    applyBorderlessFocusFallback: false),
                LazyThreadSafetyMode.ExecutionAndPublication),
            this).Value;

    private StyleStates<TGeometry> BuildFocusableStyleSet<TGeometry>(
        StyleStates<TGeometry> geometry,
        bool applyBorderlessFocusFallback = true)
        where TGeometry : ControlStyle
    {
        var input = GetStyleSet(InputStyle.Default);

        TGeometry? Rebase(InputStyle? state, string stateName)
        {
            if (state is null)
            {
                return null;
            }

            var rebased = Cascade(geometry.Normal, StyleStatesExtensions.Diff(input.Normal, state, input.AuthoredFor(stateName)));
            return applyBorderlessFocusFallback
                ? ApplyBorderlessFocusFallback(rebased, geometry.Normal)
                : rebased;
        }

        // Every other slot passes through geometry's own resolution untouched - most notably
        // Disabled, which "control"/"container" already author in every bundled theme (themes.md:
        // "Bundled themes reserve control for passive normal and disabled defaults"). Only
        // Focused/FocusWithin were genuinely missing a visual cue; replacing the whole StyleStates
        // instead of copying it through would have silently dropped that already-working Disabled
        // appearance for every caller of this method.
        return new StyleStates<TGeometry>
        {
            Normal = geometry.Normal,
            IsPointerOver = geometry.IsPointerOver,
            FocusWithin = Rebase(input.FocusWithin, "focusWithin"),
            Focused = Rebase(input.Focused, "focused"),
            Current = geometry.Current,
            Selected = geometry.Selected,
            Checked = geometry.Checked,
            Indeterminate = geometry.Indeterminate,
            Pressed = geometry.Pressed,
            Disabled = geometry.Disabled,
            Authored = geometry.Authored
        };
    }

    // Only these five well-known style keys cascade unset members from "control"'s own resolved
    // set, exactly mirroring the prior ThemeCatalog.BuildProfile(key, definition, inherited:
    // control, ...) behavior every bundled theme document's "input"/"container"/"window"/"popup"/
    // "tooltip" JSON already assumes (most of those sections only ever author a border-sides/
    // glyphStyle delta, relying on inheriting "control"'s face/border colors for everything else).
    // "control" itself and every other key (a leaf control's
    // own key, or a synthetic/test key with no special meaning) are terminal roots with no
    // inheritance - GetStyleSet's generic contract for an arbitrary key must stay a pure
    // codeOwnedDefault-plus-own-JSON resolution, independent of which specific key is passed.
    private static readonly HashSet<string> _controlInheritingKeys =
        new(StringComparer.Ordinal) { "input", "container", "window", "popup", "tooltip" };

    // Of those five, only "input" also inherits explicitly authored "control" per-state deltas.
    // Bundled themes keep interaction cues on "input" and reserve "control" for passive defaults,
    // while custom themes may still place a shared state contribution on "control" deliberately.
    // The other four styles are passive chrome and do not inherit either source. That is an
    // asserted contract, not an accident - a container must ignore hover
    // (CuratedThemesTests.EveryTheme_WhenPointerIsOver_PreservesPassiveSurfaces,
    // GroupBoxSurfaceTests.Pointer_WhenContentIsHovered_PreservesPassiveSurfaceAsync), and a Window
    // must answer activation alone, keeping its normal face while it contains focus
    // (CuratedThemesTests.EveryTheme_WhenWindowContainsFocus_UsesOnlyActiveBorder,
    // WindowSurfaceTests.Theme_WhenWindowHoveredAndActivated_RespondsOnlyToActivationAsync).
    // Cascading states into them tints every panel and window border on hover.
    private static readonly HashSet<string> _controlStateInheritingKeys =
        new(StringComparer.Ordinal) { "input" };

    private StyleStates<TStyle> BuildRootStyleSet<TStyle>(string key, TStyle codeOwnedDefault)
        where TStyle : ControlStyle
    {
        var raw = GetRawStyleSection(key);

        var authored = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal);

        if (!_controlInheritingKeys.Contains(key))
        {
            var rootNormal = ResolveRawState(raw, "normal", codeOwnedDefault, key, authored) ?? codeOwnedDefault;

            return new StyleStates<TStyle>
            {
                Normal = rootNormal,
                IsPointerOver = ResolveRawState(raw, "pointerOver", rootNormal, key, authored),
                FocusWithin = ResolveRawState(raw, "focusWithin", rootNormal, key, authored),
                Focused = ResolveRawState(raw, "focused", rootNormal, key, authored),
                Current = ResolveRawState(raw, "current", rootNormal, key, authored),
                Selected = ResolveRawState(raw, "selected", rootNormal, key, authored),
                Checked = ResolveRawState(raw, "checked", rootNormal, key, authored),
                Indeterminate = ResolveRawState(raw, "indeterminate", rootNormal, key, authored),
                Pressed = ResolveRawState(raw, "pressed", rootNormal, key, authored),
                Disabled = ResolveRawState(raw, "disabled", rootNormal, key, authored),
                Authored = authored
            };
        }

        // Every state, Normal included, cascades "control"'s DELTA rather than its whole resolved
        // value - what the theme changed about "control", applied onto this style's own code-owned
        // default, with this style's own JSON winning on top.
        //
        // Normal used to copy Face/Border/Shadow wholesale, guarded only by "this theme has no
        // control section at all". A theme that authored styles.control and omitted styles.input -
        // entirely legal, since no styles.* key is required - therefore had InputStyle's Heavy/All
        // border replaced by ControlStyle.Default's borderless one, ContainerStyle's Light border
        // dropped, and WindowStyle's Paired border and composite shadow dropped. Since Sides
        // reserves layout space, adding one control section silently moved measured widths
        // application-wide. The escape hatch - re-author border on every sibling you want to keep -
        // is what all fifteen bundled themes do, which is why it never bit in-tree and also why it
        // looks like ordinary redundancy rather than a load-bearing workaround. It now is
        // redundant.
        //
        // Cascading the delta was already the rule for every non-Normal state, for the same reason
        // stated the other way round: copying Border wholesale into a state replaces this style's
        // own Sides/GlyphStyle (input's Heavy All, popup's Rounded All) with "control"'s borderless
        // Rounded None, moving measured widths by whole cells and shifting popup and submenu
        // layout. That was tried and reverted twice before. The two branches now agree.
        //
        // Cascading explicit control state deltas remains supported for custom themes. Bundled
        // interaction cues live directly on "input"; control's disabled delta and any deliberate
        // shared override still reach input without replacing its code-owned geometry.
        //
        // Still gated on this theme having authored a "control" section: a programmatically
        // constructed Theme that never went through ThemeCatalog.Parse has nothing real to cascade.
        var controlRoot = GetRawStyleSection("control") is null ? null : GetStyleSet(ControlStyle.Default);
        var normalBase = controlRoot is null
            ? codeOwnedDefault
            : Cascade(
                codeOwnedDefault,
                StyleStatesExtensions.Diff(
                    ControlStyle.Default,
                    controlRoot.Normal,
                    controlRoot.AuthoredFor("normal")));
        var normal = ResolveRawState(raw, "normal", normalBase, key, authored) ?? normalBase;
        var controlSet = _controlStateInheritingKeys.Contains(key) ? controlRoot : null;

        // Patches "control"'s contribution for one state onto this style's resolved Normal, then lets
        // this style's own JSON for that state win on top. Returns null (meaning "no such state",
        // which the appearance-states fold resolves as Normal) only when neither side says anything.
        TStyle? InheritState(string stateName, Func<StyleStates<ControlStyle>, ControlStyle?> select)
        {
            var controlState = controlSet is null ? null : select(controlSet);
            var basis = normal;

            if (controlState is not null)
            {
                // "control"'s own provenance travels with its delta. Without it the cascade drops
                // exactly what the leaf's own diff would - a member "control" authored back to its
                // own Normal - one level earlier and just as silently.
                var delta = StyleStatesExtensions.Diff(
                    controlSet!.Normal,
                    controlState,
                    controlSet.AuthoredFor(stateName));
                basis = Cascade(normal, delta);

                if (controlSet.AuthoredFor(stateName) is { Count: > 0 } inherited)
                {
                    authored[stateName] = inherited;
                }
            }

            return ResolveRawState(raw, stateName, basis, key, authored) ??
                (controlState is null ? null : basis);
        }

        return new StyleStates<TStyle>
        {
            Normal = normal,
            IsPointerOver = InheritState("pointerOver", static s => s.IsPointerOver),
            FocusWithin = InheritState("focusWithin", static s => s.FocusWithin),
            Focused = InheritState("focused", static s => s.Focused),
            Current = InheritState("current", static s => s.Current),
            Selected = InheritState("selected", static s => s.Selected),
            Checked = InheritState("checked", static s => s.Checked),
            Indeterminate = InheritState("indeterminate", static s => s.Indeterminate),
            Pressed = InheritState("pressed", static s => s.Pressed),
            Disabled = InheritState("disabled", static s => s.Disabled),
            Authored = authored
        };
    }

    // Applies one partial contribution onto a style's own Face/Border/Shadow, leaving every other
    // member - padding, glyph families, mark styles - exactly as the style already had it. That is
    // the whole difference between cascading a delta and copying a value: the latter also carries
    // across chrome the source never meant to speak for.
    private static TStyle Cascade<TStyle>(TStyle style, AppearanceOverlay delta)
        where TStyle : ControlStyle
    {
        var patched = new ControlAppearance(style.Face, style.Border, style.Shadow).Apply(delta);
        return style with { Face = patched.Face, Border = patched.Border, Shadow = patched.Shadow };
    }

    private TStyle? ResolveRawState<TStyle>(
        Dictionary<string, Dictionary<string, JsonElement>>? raw,
        string state,
        TStyle basis,
        string key,
        Dictionary<string, IReadOnlySet<string>>? authored = null)
        where TStyle : ControlStyle
    {
        if (raw?.TryGetValue(state, out var overrides) != true)
        {
            return null;
        }

        // Recorded before the overlay runs, from the JSON alone. Afterwards the information is
        // gone: patching onto the resolved Normal makes a member written back to Normal's value
        // indistinguishable from one never written, and StyleStatesExtensions.Diff needs to tell
        // those apart to keep an earlier state from winning a member this one claimed.
        if (authored is not null)
        {
            var members = CollectAuthoredChrome(typeof(TStyle), overrides!);

            if (members.Count > 0)
            {
                // Unioned, never replaced. A cascading key records what "control" authored for this
                // state before calling here, and this style's own JSON adds to that rather than
                // standing in for it.
                authored[state] = authored.TryGetValue(state, out var inherited)
                    ? new HashSet<string>(inherited.Union(members), StringComparer.Ordinal)
                    : members;
            }
        }

        return (TStyle) Overlay(basis, overrides!, $"styles.{key}.{state}", restrictToChrome: state != "normal");
    }

    // One level deep is exactly right: Face/Border/Shadow are the only fragment-typed members of
    // ControlStyle, and they are also the only members the per-state overlay carries at all.
    private static HashSet<string> CollectAuthoredChrome(
        Type styleType,
        Dictionary<string, JsonElement> overrides)
    {
        var members = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (key, value) in overrides)
        {
            if (ThemeStyleFragment.ResolveProperty(styleType, key) is not { } fragment ||
                !typeof(IAppearanceFragment).IsAssignableFrom(fragment.PropertyType) ||
                value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            foreach (var member in value.EnumerateObject())
            {
                if (ThemeStyleFragment.ResolveProperty(fragment.PropertyType, member.Name) is { } resolved)
                {
                    _ = members.Add($"{fragment.Name}.{resolved.Name}");
                }
            }
        }

        return members;
    }

    /// <summary>Resolves one leaf control style's complete per-state set against its declared
    /// one-hop fallback: every state borrows the fallback type's own resolved per-state DELTA (not
    /// its whole resolved value) and re-applies just that delta onto
    /// <paramref name="resolvedNormal"/> - preserving whatever <paramref name="resolvedNormal"/>
    /// itself already carries (a locally-assigned style's own Face/Border/Shadow customization,
    /// and every structural member like Padding, which <paramref name="complete"/> only ever
    /// completes from the fallback and would otherwise silently discard for every non-Normal
    /// state. The delta is isolated by running <paramref name="complete"/> twice (once against the
    /// fallback's Normal, once against its per-state contribution) and value-diffing the two
    /// results, so any type-specific per-state special-casing <paramref name="complete"/> itself
    /// performs (e.g. RadioButton's Checked-state accent color) still participates in the delta
    /// exactly as before. A leaf declares no theme section of its own, so the fallback's delta is
    /// the only per-state contribution there is - nothing here overlays this style's own JSON,
    /// because it has none. Converted to a <see cref="AppearanceStates"/> so the unchanged
    /// <see cref="AppearanceResolver"/>/<see cref="AppearanceStates.ApplyStates"/> fold logic can
    /// consume a leaf control's style exactly as it consumes one of the six well-known base
    /// types.</summary>
    internal AppearanceStates BuildFallbackAwareStates<TStyle, TFallback>(
        TStyle resolvedNormal,
        Func<Theme, StyleStates<TFallback>> fallbackTo,
        Func<TFallback, VisualState, Theme, TStyle> complete)
        where TStyle : ControlStyle
        where TFallback : ControlStyle
    {
        var fallbackSet = fallbackTo(this);
        var completedFallbackNormal = complete(fallbackSet.Normal, VisualState.Normal, this);
        var authored = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal);

        TStyle ResolveState(VisualState state, string stateName, Func<StyleStates<TFallback>, TFallback?> fallbackSelector)
        {
            // A state the fallback set leaves unpopulated must STILL run `complete` for that state.
            // `complete` is the only place a style type can express a per-state code-owned default
            // (today just RadioButton's Checked accent foreground), and returning resolvedNormal
            // early here made that branch dead code whenever the fallback did not author the state -
            // which is the common case, since no bundled theme authors "input.checked".
            //
            // Completing from the fallback's Normal leaves every other style type byte-identical:
            // RadioButtonStyle.Complete and HyperlinkButtonStyle.Complete are the only
            // implementations that read their state parameter, so for all the others
            // complete(Normal, state) equals completedFallbackNormal and the diff comes out empty,
            // yielding resolvedNormal exactly as before. A per-state branch is safe here only when
            // it yields a semantic constant (RadioButtonStyle's Checked accent) rather than a
            // pass-through of the fallback's own face - HyperlinkButtonStyle's non-Normal branches
            // pass control.Face.Foreground/Attributes through unchanged, so on this
            // (Theme-fallback-aware) path the diff still comes out empty for those members, but the
            // same pass-through is why HyperlinkButtonStyle cannot use this path's frozen-fallback
            // sibling (BuildCodeOwnedStates) for its local-style resolution without losing a local
            // style's colors in every non-Normal state.
            var completedFallbackState = complete(fallbackSelector(fallbackSet) ?? fallbackSet.Normal, state, this);
            var inheritedMembers = fallbackSet.AuthoredFor(stateName);
            var delta = StyleStatesExtensions.Diff(
                completedFallbackNormal,
                completedFallbackState,
                inheritedMembers);
            var basis = Cascade(resolvedNormal, delta);

            if (inheritedMembers is { Count: > 0 })
            {
                authored[stateName] = inheritedMembers;
            }

            return basis;
        }

        var set = new StyleStates<TStyle>
        {
            Normal = resolvedNormal,
            IsPointerOver = ResolveState(VisualState.IsPointerOver, "pointerOver", static s => s.IsPointerOver),
            FocusWithin = ResolveState(VisualState.FocusWithin, "focusWithin", static s => s.FocusWithin),
            Focused = ResolveState(VisualState.Focused, "focused", static s => s.Focused),
            Current = ResolveState(VisualState.Current, "current", static s => s.Current),
            Selected = ResolveState(VisualState.Selected, "selected", static s => s.Selected),
            Checked = ResolveState(VisualState.Checked, "checked", static s => s.Checked),
            Indeterminate = ResolveState(VisualState.Indeterminate, "indeterminate", static s => s.Indeterminate),
            Pressed = ResolveState(VisualState.Pressed, "pressed", static s => s.Pressed),
            Disabled = ResolveState(VisualState.Disabled, "disabled", static s => s.Disabled),
            Authored = authored
        };

        return set.ToAppearanceStates();
    }

    /// <summary>Builds only the per-state deltas authored by a leaf style's completion logic,
    /// excluding every state supplied by its Theme fallback.</summary>
    internal AppearanceStates BuildCodeOwnedStates<TStyle, TFallback>(
        TStyle resolvedNormal,
        TFallback fallbackNormal,
        Func<TFallback, VisualState, Theme, TStyle> complete)
        where TStyle : ControlStyle
        where TFallback : ControlStyle
    {
        var completedFallbackNormal = complete(fallbackNormal, VisualState.Normal, this);
        TStyle ResolveState(VisualState state) => complete(fallbackNormal, state, this);

        return new AppearanceStates(
            new ControlAppearance(resolvedNormal.Face, resolvedNormal.Border, resolvedNormal.Shadow),
            StyleStatesExtensions.Diff(completedFallbackNormal, ResolveState(VisualState.IsPointerOver)),
            StyleStatesExtensions.Diff(completedFallbackNormal, ResolveState(VisualState.FocusWithin)),
            StyleStatesExtensions.Diff(completedFallbackNormal, ResolveState(VisualState.Focused)),
            StyleStatesExtensions.Diff(completedFallbackNormal, ResolveState(VisualState.Current)),
            StyleStatesExtensions.Diff(completedFallbackNormal, ResolveState(VisualState.Selected)),
            StyleStatesExtensions.Diff(completedFallbackNormal, ResolveState(VisualState.Checked)),
            StyleStatesExtensions.Diff(completedFallbackNormal, ResolveState(VisualState.Indeterminate)),
            StyleStatesExtensions.Diff(completedFallbackNormal, ResolveState(VisualState.Pressed)),
            StyleStatesExtensions.Diff(completedFallbackNormal, ResolveState(VisualState.Disabled)));
    }

    // An active Window's border has always distinguished itself from an inactive one, but no
    // bundled or custom theme has ever authored styles.window.focusWithin - unlike every other
    // per-state slot (which simply falls back to Normal when unauthored), so this one code-owned
    // default is preserved here as a shared primitive, rather than duplicated between this Theme's
    // own Window property and Window.GetDefaultAppearanceStates (both need it identically).
    // A theme JSON that DOES author "window.focusWithin" still wins
    // outright - this only fills in when the raw section is entirely absent.
    internal StyleStates<WindowStyle> GetWindowStyleSet() =>
        (StyleStates<WindowStyle>) _styleSets.GetOrAdd(
            (typeof(WindowStyle), "$windowWithFocusWithin", WindowStyle.Default),
            static (_, theme) => new Lazy<object>(
                () => theme.BuildWindowStyleSet(),
                LazyThreadSafetyMode.ExecutionAndPublication),
            this).Value;

    private StyleStates<WindowStyle> BuildWindowStyleSet()
    {
        var styleSet = GetStyleSet(WindowStyle.Default);
        return styleSet.FocusWithin is not null
            ? styleSet
            : new StyleStates<WindowStyle>
            {
                Normal = styleSet.Normal,
                IsPointerOver = styleSet.IsPointerOver,
                FocusWithin = styleSet.Normal with
                {
                    Border = styleSet.Normal.Border with { Foreground = SemanticColor.ActiveBorder }
                },
                Focused = styleSet.Focused,
                Current = styleSet.Current,
                Selected = styleSet.Selected,
                Checked = styleSet.Checked,
                Indeterminate = styleSet.Indeterminate,
                Pressed = styleSet.Pressed,
                Disabled = styleSet.Disabled,
                Authored = styleSet.Authored
            };
    }

    private AppearanceStates GetAppearanceStates<TStyle>(
        string key,
        Func<Theme, StyleStates<TStyle>> resolve)
        where TStyle : ControlStyle =>
        _appearanceSets.GetOrAdd(
            key,
            static (_, state) => new Lazy<AppearanceStates>(
                () => state.resolve(state.theme).ToAppearanceStates(),
                LazyThreadSafetyMode.ExecutionAndPublication),
            (theme: this, resolve)).Value;

    internal Color Resolve(ControlColor value) => value.IsLiteral
        ? value.Literal
        : ResolveColor(value.SemanticColor);

    internal TerminalAttributes Resolve(ControlDecoration value) => value.IsLiteral
        ? value.Literal
        : ResolveAttributes(value.SemanticDecoration);

    // These six are named shortcuts onto ResolveColor, not a second color table. A theme used to
    // author them twice - once under "colors" and again under a parallel "status" section - a
    // duplication that was collapsed when SemanticColor subsumed the retired StatusColor enum.

    /// <summary>Gets the error color.</summary>
    public Color Error => ResolveColor(SemanticColor.Error);

    /// <summary>Gets the warning color.</summary>
    public Color Warning => ResolveColor(SemanticColor.Warning);

    /// <summary>Gets the success color.</summary>
    public Color Success => ResolveColor(SemanticColor.Success);

    /// <summary>Gets the info color.</summary>
    public Color Info => ResolveColor(SemanticColor.Info);

    /// <summary>Gets the muted color.</summary>
    public Color Muted => ResolveColor(SemanticColor.Muted);

    /// <summary>Gets the hotkey/access-key color.</summary>
    public Color Hotkey => ResolveColor(SemanticColor.Hotkey);

    /// <summary>Prevents further mutation.</summary>
    public void Freeze() => IsFrozen = true;

    private static void ValidateConcrete(Color color, string parameterName)
    {
        if (color.IsTransparent)
        {
            throw new ArgumentException("A theme color must be a concrete terminal color.", parameterName);
        }
    }

}
