// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Text.Json;


/// <summary>Represents immutable global semantic values, appearance profiles, status colors, and provenance after freezing.</summary>
[PublicAPI]
public sealed class Theme
{
    private readonly Dictionary<string, Color> _palette;
    private readonly Dictionary<StatusColor, Color> _statusColors = [];
    private readonly Color[] _colors = new Color[Enum.GetValues<ThemeColor>().Length];
    private readonly TerminalAttributes[] _attributes =
        new TerminalAttributes[Enum.GetValues<ThemeDecoration>().Length];
    private readonly ConcurrentDictionary<string, object> _boundStyleSections = new(StringComparer.Ordinal);
    private Dictionary<string, JsonElement> _styleSections = [];

    /// <summary>Initializes an unfrozen theme and copies its optional named palette.</summary>
    /// <param name="palette">Optional named concrete colors.</param>
    /// <param name="name">The display name.</param>
    /// <param name="slug">The stable catalog slug.</param>
    /// <param name="colorScheme">The intended light or dark color scheme.</param>
    /// <param name="author">The attribution author.</param>
    /// <param name="license">The palette license identifier.</param>
    /// <param name="source">The palette source URL.</param>
    /// <exception cref="ArgumentNullException">Required identity or provenance metadata is null.</exception>
    /// <exception cref="ArgumentException">Required metadata is blank or a palette value is transparent.</exception>
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
        slug = RequireMetadata(slug, nameof(slug));
        author = RequireMetadata(author, nameof(author));
        license = RequireMetadata(license, nameof(license));
        source = RequireMetadata(source, nameof(source));

        if (!Enum.IsDefined(colorScheme))
        {
            throw new ArgumentOutOfRangeException(
                nameof(colorScheme),
                colorScheme,
                "The theme color scheme is unknown.");
        }

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

        Control = new ThemeProfile(CreateDefaultAppearance(ThemeRole.Control));
        Input = new ThemeProfile(CreateDefaultAppearance(ThemeRole.Input));
        Container = new ThemeProfile(CreateDefaultAppearance(ThemeRole.Container));
        Window = new ThemeProfile(CreateDefaultAppearance(ThemeRole.Window));
        Popup = new ThemeProfile(CreateDefaultAppearance(ThemeRole.Popup));
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

    /// <summary>Gets the passive base-control semantic profile.</summary>
    public ThemeProfile Control { get; private set; }

    /// <summary>Gets the editable or selectable input semantic profile.</summary>
    public ThemeProfile Input { get; private set; }

    /// <summary>Gets the framed grouping or collection semantic profile.</summary>
    public ThemeProfile Container { get; private set; }

    /// <summary>Gets the top-level window semantic profile.</summary>
    public ThemeProfile Window { get; private set; }

    /// <summary>Gets the transient popup semantic profile.</summary>
    public ThemeProfile Popup { get; private set; }

    /// <summary>Resolves one known global color to a concrete terminal color.</summary>
    /// <param name="color">The known color role.</param>
    /// <returns>The configured concrete terminal color.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="color"/> is unknown.</exception>
    public Color ResolveColor(ThemeColor color) => Enum.IsDefined(color)
        ? _colors[(int) color]
        : throw new ArgumentOutOfRangeException(nameof(color), color, "The theme color is unknown.");

    /// <summary>Resolves one known global attribute role to concrete terminal attributes.</summary>
    /// <param name="decoration">The known attribute role.</param>
    /// <returns>The configured complete terminal attributes.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="decoration"/> is unknown.</exception>
    public TerminalAttributes ResolveAttributes(ThemeDecoration decoration) => Enum.IsDefined(decoration)
        ? _attributes[(int) decoration]
        : throw new ArgumentOutOfRangeException(
            nameof(decoration),
            decoration,
            "The theme decoration is unknown.");

    internal void SetColor(ThemeColor color, Color value)
    {
        if (IsFrozen)
        {
            throw new InvalidOperationException("A frozen theme cannot be changed.");
        }

        ValidateConcrete(value, nameof(value));
        _colors[(int) color] = value;
    }

    internal void SetAttributes(ThemeDecoration decoration, TerminalAttributes value)
    {
        if (IsFrozen)
        {
            throw new InvalidOperationException("A frozen theme cannot be changed.");
        }

        ((TerminalAttributes?) value).Validate(null, null);
        _attributes[(int) decoration] = value;
    }

    internal void SetProfiles(
        ThemeProfile control,
        ThemeProfile input,
        ThemeProfile container,
        ThemeProfile window,
        ThemeProfile popup)
    {
        if (IsFrozen)
        {
            throw new InvalidOperationException("A frozen theme cannot be changed.");
        }

        Control = control;
        Input = input;
        Container = container;
        Window = window;
        Popup = popup;
    }

    internal void SetStyleSections(Dictionary<string, JsonElement> sections)
    {
        if (IsFrozen)
        {
            throw new InvalidOperationException("A frozen theme cannot be changed.");
        }

        _styleSections = sections;
    }

    /// <summary>Resolves one registrable style section, deferred-parsed and memoized on first access.</summary>
    /// <typeparam name="TSection">The section's JSON definition type.</typeparam>
    /// <param name="sectionName">The exact styles.* section name this theme document may author (e.g. "vendor.control").</param>
    /// <returns>The parsed section, or null when this theme did not author it.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="sectionName"/> is null.</exception>
    /// <exception cref="InvalidDataException">The section's JSON does not match <typeparamref name="TSection"/>.</exception>
    public TSection? GetStyleSection<TSection>(string sectionName)
        where TSection : class
    {
        ArgumentNullException.ThrowIfNull(sectionName);

        if (_boundStyleSections.TryGetValue(sectionName, out var cached))
        {
            return (TSection) cached;
        }

        if (!_styleSections.TryGetValue(sectionName, out var element))
        {
            return null;
        }

        var parsed = Themes.ParseSection<TSection>(element, sectionName, Slug);
        return (TSection) _boundStyleSections.GetOrAdd(sectionName, parsed);
    }

    /// <summary>Resolves one known semantic role to its concrete appearance profile.</summary>
    /// <param name="role">The known semantic role.</param>
    /// <returns>The configured appearance profile.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="role"/> is unknown.</exception>
    public ThemeProfile GetProfile(ThemeRole role) => role switch
    {
        ThemeRole.Control => Control,
        ThemeRole.Input => Input,
        ThemeRole.Container => Container,
        ThemeRole.Window => Window,
        ThemeRole.Popup => Popup,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "The theme role is unknown.")
    };

    internal Color Resolve(ColorValue value) => value.IsLiteral
        ? value.Literal
        : ResolveColor(value.ThemeColor);

    internal TerminalAttributes Resolve(AttributeValue value) => value.IsLiteral
        ? value.Literal
        : ResolveAttributes(value.ThemeDecoration);

    internal void SetStatusColors(Dictionary<StatusColor, Color> statusColors)
    {
        foreach (var (key, value) in statusColors)
        {
            _statusColors[key] = value;
        }
    }

    /// <summary>Resolves one cross-cutting status color.</summary>
    public Color ResolveStatusColor(StatusColor status) =>
        _statusColors.TryGetValue(status, out var color) ? color : Color.Default;

    /// <summary>Gets the error status color.</summary>
    public Color Error => ResolveStatusColor(StatusColor.Error);

    /// <summary>Gets the warning status color.</summary>
    public Color Warning => ResolveStatusColor(StatusColor.Warning);

    /// <summary>Gets the success status color.</summary>
    public Color Success => ResolveStatusColor(StatusColor.Success);

    /// <summary>Gets the info status color.</summary>
    public Color Info => ResolveStatusColor(StatusColor.Info);

    /// <summary>Gets the muted status color.</summary>
    public Color Muted => ResolveStatusColor(StatusColor.Muted);

    /// <summary>Gets the hotkey/access-key status color.</summary>
    public Color Hotkey => ResolveStatusColor(StatusColor.Hotkey);

    /// <summary>Prevents further mutation.</summary>
    public void Freeze() => IsFrozen = true;

    private static void ValidateConcrete(Color color, string parameterName)
    {
        if (color.IsTransparent)
        {
            throw new ArgumentException("A theme color must be a concrete terminal color.", parameterName);
        }
    }

    internal static ThemeAppearance CreateDefaultAppearance() => new(
        new Face(
            Color.Default,
            Color.Transparent,
            TerminalAttributes.None,
            Underline.None,
            Color.Default),
        new Border(
            BorderSide.None,
            BorderGlyphStyle.Default,
            Color.Default,
            Color.Transparent,
            TerminalAttributes.None),
        new Shadow(
            false,
            ShadowMode.Composite,
            default,
            ControlGlyphs.Chrome.Shadow.Value,
            Color.Default,
            Color.Transparent,
            TerminalAttributes.None));

    internal static ThemeAppearance CreateDefaultAppearance(ThemeRole role)
    {
        var appearance = CreateDefaultAppearance();
        var border = role switch
        {
            ThemeRole.Input => new Border(
                BorderSide.All,
                BorderGlyphStyle.Heavy,
                Color.Default,
                Color.Transparent,
                TerminalAttributes.None),
            ThemeRole.Container => new Border(
                BorderSide.All,
                BorderGlyphStyle.Light,
                Color.Default,
                Color.Transparent,
                TerminalAttributes.None),
            ThemeRole.Window => new Border(
                BorderSide.All,
                BorderGlyphStyle.Paired,
                Color.Default,
                Color.Transparent,
                TerminalAttributes.None),
            ThemeRole.Popup => new Border(
                BorderSide.All,
                BorderGlyphStyle.Rounded,
                Color.Default,
                Color.Transparent,
                TerminalAttributes.None),
            ThemeRole.Control => appearance.Border,
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "The theme role is unknown.")
        };
        var shadow = role == ThemeRole.Window
            ? new Shadow(
                true,
                ShadowMode.Composite,
                new Point(2, 1),
                ControlGlyphs.Chrome.Shadow.Value,
                Color.Default,
                Color.Transparent,
                TerminalAttributes.Dim)
            : appearance.Shadow;
        return new ThemeAppearance(appearance.Face, border, shadow);
    }
}
