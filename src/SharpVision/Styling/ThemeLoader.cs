// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Turns theme JSON and definitions into frozen <see cref="Theme"/> instances.</summary>
internal static class ThemeLoader
{
    /// <summary>Gets the maximum encoded bytes accepted from one theme document.</summary>
    internal const int MaximumDocumentBytes = 64 * 1024;

    /// <summary>Gets the maximum characters accepted in a palette or role key.</summary>
    internal const int MaximumKeyCharacters = 64;

    private const int _maximumDepth = 6;
    private const int _maximumPaletteEntries = 256;
    private const int _maximumRoleEntries = 12;
    private const int _maximumStringCharacters = 2048;
    private const int _maximumDecodedCharacters = 32 * 1024;
    private static readonly UTF8Encoding _utf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private static readonly Dictionary<string, ColorRole> _roleNames = new(StringComparer.Ordinal)
    {
        ["foreground"] = ColorRole.Foreground,
        ["background"] = ColorRole.Background,
        ["surface"] = ColorRole.Surface,
        ["border"] = ColorRole.Border,
        ["accent"] = ColorRole.Accent,
        ["muted"] = ColorRole.Muted,
        ["selectionBackground"] = ColorRole.SelectionBackground,
        ["selectionForeground"] = ColorRole.SelectionForeground,
        ["error"] = ColorRole.Error,
        ["warning"] = ColorRole.Warning,
        ["success"] = ColorRole.Success,
        ["info"] = ColorRole.Info,
    };

    #region Document parsing and validation

    /// <summary>Parses one bounded theme JSON string into a validated definition.</summary>
    /// <param name="json">The theme JSON text.</param>
    /// <param name="source">A label (slug or path) used in error messages.</param>
    /// <returns>The deserialized definition.</returns>
    /// <exception cref="InvalidDataException">The JSON is malformed or empty.</exception>
    public static ThemeDefinition Deserialize(string json, string source)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        try
        {
            var byteCount = _utf8.GetByteCount(json);

            return byteCount > MaximumDocumentBytes ? throw TooLarge(source) : Deserialize(_utf8.GetBytes(json), source);
        }
        catch (EncoderFallbackException error)
        {
            throw new InvalidDataException($"Theme '{source}' contains invalid Unicode text.", error);
        }
    }

    /// <summary>Reads and parses one bounded UTF-8 theme stream from its current position.</summary>
    /// <param name="stream">The readable caller-owned stream.</param>
    /// <param name="source">A non-empty label used in diagnostics.</param>
    /// <returns>The validated definition.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
    /// <exception cref="ArgumentException">The stream is unreadable or <paramref name="source"/> is empty.</exception>
    /// <exception cref="InvalidDataException">The content exceeds a limit or violates the schema.</exception>
    internal static ThemeDefinition Deserialize(Stream stream, string source)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        return !stream.CanRead
            ? throw new ArgumentException("The theme stream must be readable.", nameof(stream))
            : Deserialize(ReadBounded(stream, source), source);
    }

    /// <summary>Reads at most one bounded theme document without closing the source stream.</summary>
    /// <param name="stream">The readable caller-owned stream at its desired starting position.</param>
    /// <param name="source">A non-empty label used in diagnostics.</param>
    /// <returns>The exact retained bytes, never exceeding <see cref="MaximumDocumentBytes"/>.</returns>
    /// <exception cref="InvalidDataException">The document exceeds the encoded byte limit.</exception>
    internal static byte[] ReadBounded(Stream stream, string source)
    {
        Debug.Assert(stream is not null && stream.CanRead, "A bounded theme read requires a readable stream.");
        Debug.Assert(!string.IsNullOrWhiteSpace(source), "A bounded theme read requires a diagnostic source.");
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
            throw TooLarge(source);
        }

        Array.Resize(ref bytes, length);
        return bytes;
    }

    /// <summary>Parses one already bounded UTF-8 theme document.</summary>
    /// <param name="utf8">The complete borrowed UTF-8 document.</param>
    /// <param name="source">A non-empty label used in diagnostics.</param>
    /// <returns>The validated definition.</returns>
    /// <exception cref="InvalidDataException">The document exceeds a limit or violates schema version 2.</exception>
    internal static ThemeDefinition Deserialize(ReadOnlyMemory<byte> utf8, string source)
    {
        if (utf8.Length > MaximumDocumentBytes)
        {
            throw TooLarge(source);
        }

        try
        {
            using var document = JsonDocument.Parse(
                utf8,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = _maximumDepth,
                });
            return ParseDefinition(document.RootElement, source);
        }
        catch (JsonException error)
        {
            throw new InvalidDataException($"Theme '{source}' is not valid JSON.", error);
        }
    }

    /// <summary>Deserializes and builds a frozen theme from JSON.</summary>
    /// <param name="json">The theme JSON text.</param>
    /// <param name="source">A label (slug or path) used in error messages.</param>
    /// <returns>The frozen theme.</returns>
    /// <exception cref="InvalidDataException">
    /// The JSON is malformed, or the deserialized definition is invalid (see <see cref="FromDefinition"/>).
    /// </exception>
    public static Theme FromJson(string json, string source) =>
        FromDefinition(Deserialize(json, source), source);

    /// <summary>Builds a frozen theme from one bounded UTF-8 document.</summary>
    /// <param name="utf8">The complete borrowed UTF-8 document.</param>
    /// <param name="source">A non-empty label used in diagnostics.</param>
    /// <returns>The frozen resolved theme.</returns>
    /// <exception cref="InvalidDataException">The document or resolved theme is invalid.</exception>
    internal static Theme FromUtf8(ReadOnlyMemory<byte> utf8, string source) =>
        FromDefinition(Deserialize(utf8, source), source);

    /// <summary>Resolves a definition's palette and roles, fills fallbacks, and builds a frozen theme.</summary>
    /// <param name="definition">The deserialized definition.</param>
    /// <param name="source">A label (slug or path) used in error messages.</param>
    /// <returns>The frozen theme.</returns>
    /// <exception cref="InvalidDataException">
    /// A palette or role entry has a null, malformed, or out-of-range color value; a role references an
    /// unknown palette key or names an unknown role; or the required <c>background</c>/<c>foreground</c>
    /// roles are not both resolved.
    /// </exception>
    public static Theme FromDefinition(ThemeDefinition definition, string source)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (definition.Version != 2)
        {
            throw new InvalidDataException(
                $"Theme '{source}' uses unsupported schema version {definition.Version}.");
        }

        var palette = ResolvePalette(definition, source);
        var roles = ResolveRoles(definition, palette, source);
        FillFallbacks(roles, source);
        return ThemeBuilder.Build(
            roles,
            definition.Glyphs ?? throw new InvalidDataException($"Theme '{source}' is missing required property 'glyphs'."),
            definition);
    }

    private static ThemeDefinition ParseDefinition(JsonElement root, string source)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"Theme '{source}' root must be an object.");
        }

        var definition = new ThemeDefinition();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var decodedCharacters = 0;

        foreach (var property in root.EnumerateObject())
        {
            if (!seen.Add(property.Name))
            {
                throw Duplicate(source, property.Name, "root");
            }

            AddDecoded(property.Name, source, ref decodedCharacters);

            switch (property.Name)
            {
                case "version":
                    definition.Version = ReadInteger(property.Value, source, property.Name, nonNegative: true);
                    break;
                case "name":
                    definition.Name = ReadString(property.Value, source, property.Name, ref decodedCharacters);
                    break;
                case "slug":
                    definition.Slug = ReadString(property.Value, source, property.Name, ref decodedCharacters);
                    break;
                case "colorScheme":
                    definition.ColorScheme = ReadString(property.Value, source, property.Name, ref decodedCharacters);
                    break;
                case "order":
                    definition.Order = ReadInteger(property.Value, source, property.Name, nonNegative: true);
                    break;
                case "author":
                    definition.Author = ReadString(property.Value, source, property.Name, ref decodedCharacters);
                    break;
                case "license":
                    definition.License = ReadString(property.Value, source, property.Name, ref decodedCharacters);
                    break;
                case "source":
                    definition.Source = ReadString(property.Value, source, property.Name, ref decodedCharacters);
                    break;
                case "palette":
                    definition.Palette = ReadMap(
                        property.Value,
                        source,
                        property.Name,
                        _maximumPaletteEntries,
                        ref decodedCharacters);
                    break;
                case "roles":
                    definition.Roles = ReadMap(
                        property.Value,
                        source,
                        property.Name,
                        _maximumRoleEntries,
                        ref decodedCharacters);
                    break;
                case "glyphs":
                    definition.Glyphs = ReadThemeGlyphs(
                        property.Value,
                        source,
                        ref decodedCharacters);
                    break;
                default:
                    throw new InvalidDataException(
                        $"Theme '{source}' has unknown root property '{property.Name}'.");
            }
        }

        if (!seen.Contains("version"))
        {
            throw new InvalidDataException($"Theme '{source}' is missing required property 'version'.");
        }

        if (definition.Version != 2)
        {
            throw new InvalidDataException(
                $"Theme '{source}' uses unsupported schema version {definition.Version}.");
        }

        if (!seen.Contains("roles"))
        {
            throw new InvalidDataException($"Theme '{source}' is missing required property 'roles'.");
        }

        if (!seen.Contains("glyphs"))
        {
            throw new InvalidDataException($"Theme '{source}' is missing required property 'glyphs'.");
        }

        ValidateOptionalMetadata(definition, seen, source);
        return definition;
    }

    private static void ValidateOptionalMetadata(
        ThemeDefinition definition,
        HashSet<string> seen,
        string source)
    {
        ValidateNonEmpty(definition.Name, "name");
        ValidateNonEmpty(definition.Author, "author");
        ValidateNonEmpty(definition.License, "license");
        ValidateNonEmpty(definition.Source, "source");

        if (seen.Contains("slug"))
        {
            ValidateNonEmpty(definition.Slug, "slug");

            if (definition.Slug!.Length > MaximumKeyCharacters)
            {
                throw new InvalidDataException(
                    $"Theme '{source}' field 'slug' exceeds {MaximumKeyCharacters} characters.");
            }
        }

        if (seen.Contains("colorScheme") && definition.ColorScheme is not ("dark" or "light"))
        {
            throw new InvalidDataException(
                $"Theme '{source}' field 'colorScheme' must be 'dark' or 'light'.");
        }

        void ValidateNonEmpty(string? value, string field)
        {
            if (seen.Contains(field) && string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidDataException(
                    $"Theme '{source}' field '{field}' must contain non-whitespace text.");
            }
        }
    }

    #endregion

    #region Semantic glyph parsing

    private static ThemeGlyphs ReadThemeGlyphs(
        JsonElement element,
        string source,
        ref int decodedCharacters)
    {
        var root = ReadObject(
            element,
            source,
            "glyphs",
            ["chrome", "progress", "disclosure", "selection", "navigation", "scrollBars", "separators", "text"],
            ref decodedCharacters);
        var chrome = ReadObject(
            root["chrome"],
            source,
            "glyphs.chrome",
            ["topLeft", "top", "topRight", "right", "bottomRight", "bottom", "bottomLeft", "left", "shadow", "windowClose"],
            ref decodedCharacters);
        var progress = ReadObject(
            root["progress"],
            source,
            "glyphs.progress",
            ["empty", "full", "indeterminate", "horizontalFractions", "verticalFractions"],
            ref decodedCharacters);
        var disclosure = ReadObject(
            root["disclosure"],
            source,
            "glyphs.disclosure",
            ["collapsed", "expanded", "dropDown"],
            ref decodedCharacters);
        var selection = ReadObject(
            root["selection"],
            source,
            "glyphs.selection",
            [
                "checkBoxBracketUnchecked", "checkBoxBracketChecked", "checkBoxBracketIndeterminate",
                "checkBoxTickUnchecked", "checkBoxTickChecked", "checkBoxTickIndeterminate",
                "checkBoxSquareUnchecked", "checkBoxSquareChecked", "checkBoxSquareIndeterminate",
                "radioUnchecked", "radioChecked", "menuCheckUnchecked", "menuCheckChecked",
                "menuRadioUnchecked", "menuRadioChecked",
            ],
            ref decodedCharacters);
        var navigation = ReadObject(
            root["navigation"],
            source,
            "glyphs.navigation",
            ["itemIdle", "itemCurrent", "groupCollapsed", "groupExpanded", "separator"],
            ref decodedCharacters);
        var scrollBars = ReadObject(
            root["scrollBars"],
            source,
            "glyphs.scrollBars",
            [
                "verticalDecrement", "verticalIncrement", "horizontalDecrement", "horizontalIncrement",
                "blockTrack", "blockThumb", "horizontalLineTrack", "horizontalLineThumb",
                "verticalLineTrack", "verticalLineThumb",
            ],
            ref decodedCharacters);
        var separators = ReadObject(
            root["separators"],
            source,
            "glyphs.separators",
            ["horizontal", "vertical", "menu", "tableHorizontal", "tableVertical", "tableCross", "tabDivider", "tabUnderline"],
            ref decodedCharacters);
        var text = ReadObject(
            root["text"],
            source,
            "glyphs.text",
            ["ellipsis"],
            ref decodedCharacters);

        var empty = ReadGlyph(progress["empty"], source, "glyphs.progress.empty", ref decodedCharacters);
        var full = ReadGlyph(progress["full"], source, "glyphs.progress.full", ref decodedCharacters);
        ProgressGlyphs progressGlyphs;

        try
        {
            progressGlyphs = new ProgressGlyphs(
                empty,
                full,
                ReadGlyph(progress["indeterminate"], source, "glyphs.progress.indeterminate", ref decodedCharacters),
                ReadGlyphArray(
                    progress["horizontalFractions"],
                    source,
                    "glyphs.progress.horizontalFractions",
                    ref decodedCharacters),
                ReadGlyphArray(
                    progress["verticalFractions"],
                    source,
                    "glyphs.progress.verticalFractions",
                    ref decodedCharacters));
        }
        catch (ArgumentException error)
        {
            throw new InvalidDataException(
                $"Theme '{source}' progress fraction endpoints must match its empty and full glyphs.",
                error);
        }

        return new ThemeGlyphs(
            new ChromeGlyphs(
                ReadGlyph(chrome["topLeft"], source, "glyphs.chrome.topLeft", ref decodedCharacters),
                ReadGlyph(chrome["top"], source, "glyphs.chrome.top", ref decodedCharacters),
                ReadGlyph(chrome["topRight"], source, "glyphs.chrome.topRight", ref decodedCharacters),
                ReadGlyph(chrome["right"], source, "glyphs.chrome.right", ref decodedCharacters),
                ReadGlyph(chrome["bottomRight"], source, "glyphs.chrome.bottomRight", ref decodedCharacters),
                ReadGlyph(chrome["bottom"], source, "glyphs.chrome.bottom", ref decodedCharacters),
                ReadGlyph(chrome["bottomLeft"], source, "glyphs.chrome.bottomLeft", ref decodedCharacters),
                ReadGlyph(chrome["left"], source, "glyphs.chrome.left", ref decodedCharacters),
                ReadGlyph(chrome["shadow"], source, "glyphs.chrome.shadow", ref decodedCharacters),
                ReadGlyph(chrome["windowClose"], source, "glyphs.chrome.windowClose", ref decodedCharacters)),
            progressGlyphs,
            new DisclosureGlyphs(
                ReadGlyph(disclosure["collapsed"], source, "glyphs.disclosure.collapsed", ref decodedCharacters),
                ReadGlyph(disclosure["expanded"], source, "glyphs.disclosure.expanded", ref decodedCharacters),
                ReadGlyph(disclosure["dropDown"], source, "glyphs.disclosure.dropDown", ref decodedCharacters)),
            new SelectionGlyphs(
                ReadGlyph(selection["checkBoxBracketUnchecked"], source, "glyphs.selection.checkBoxBracketUnchecked", ref decodedCharacters),
                ReadGlyph(selection["checkBoxBracketChecked"], source, "glyphs.selection.checkBoxBracketChecked", ref decodedCharacters),
                ReadGlyph(selection["checkBoxBracketIndeterminate"], source, "glyphs.selection.checkBoxBracketIndeterminate", ref decodedCharacters),
                ReadGlyph(selection["checkBoxTickUnchecked"], source, "glyphs.selection.checkBoxTickUnchecked", ref decodedCharacters),
                ReadGlyph(selection["checkBoxTickChecked"], source, "glyphs.selection.checkBoxTickChecked", ref decodedCharacters),
                ReadGlyph(selection["checkBoxTickIndeterminate"], source, "glyphs.selection.checkBoxTickIndeterminate", ref decodedCharacters),
                ReadGlyph(selection["checkBoxSquareUnchecked"], source, "glyphs.selection.checkBoxSquareUnchecked", ref decodedCharacters),
                ReadGlyph(selection["checkBoxSquareChecked"], source, "glyphs.selection.checkBoxSquareChecked", ref decodedCharacters),
                ReadGlyph(selection["checkBoxSquareIndeterminate"], source, "glyphs.selection.checkBoxSquareIndeterminate", ref decodedCharacters),
                ReadGlyph(selection["radioUnchecked"], source, "glyphs.selection.radioUnchecked", ref decodedCharacters),
                ReadGlyph(selection["radioChecked"], source, "glyphs.selection.radioChecked", ref decodedCharacters),
                ReadGlyph(selection["menuCheckUnchecked"], source, "glyphs.selection.menuCheckUnchecked", ref decodedCharacters),
                ReadGlyph(selection["menuCheckChecked"], source, "glyphs.selection.menuCheckChecked", ref decodedCharacters),
                ReadGlyph(selection["menuRadioUnchecked"], source, "glyphs.selection.menuRadioUnchecked", ref decodedCharacters),
                ReadGlyph(selection["menuRadioChecked"], source, "glyphs.selection.menuRadioChecked", ref decodedCharacters)),
            new NavigationGlyphs(
                ReadGlyph(navigation["itemIdle"], source, "glyphs.navigation.itemIdle", ref decodedCharacters),
                ReadGlyph(navigation["itemCurrent"], source, "glyphs.navigation.itemCurrent", ref decodedCharacters),
                ReadGlyph(navigation["groupCollapsed"], source, "glyphs.navigation.groupCollapsed", ref decodedCharacters),
                ReadGlyph(navigation["groupExpanded"], source, "glyphs.navigation.groupExpanded", ref decodedCharacters),
                ReadGlyph(navigation["separator"], source, "glyphs.navigation.separator", ref decodedCharacters)),
            new ScrollBarGlyphs(
                ReadGlyph(scrollBars["verticalDecrement"], source, "glyphs.scrollBars.verticalDecrement", ref decodedCharacters),
                ReadGlyph(scrollBars["verticalIncrement"], source, "glyphs.scrollBars.verticalIncrement", ref decodedCharacters),
                ReadGlyph(scrollBars["horizontalDecrement"], source, "glyphs.scrollBars.horizontalDecrement", ref decodedCharacters),
                ReadGlyph(scrollBars["horizontalIncrement"], source, "glyphs.scrollBars.horizontalIncrement", ref decodedCharacters),
                ReadGlyph(scrollBars["blockTrack"], source, "glyphs.scrollBars.blockTrack", ref decodedCharacters),
                ReadGlyph(scrollBars["blockThumb"], source, "glyphs.scrollBars.blockThumb", ref decodedCharacters),
                ReadGlyph(scrollBars["horizontalLineTrack"], source, "glyphs.scrollBars.horizontalLineTrack", ref decodedCharacters),
                ReadGlyph(scrollBars["horizontalLineThumb"], source, "glyphs.scrollBars.horizontalLineThumb", ref decodedCharacters),
                ReadGlyph(scrollBars["verticalLineTrack"], source, "glyphs.scrollBars.verticalLineTrack", ref decodedCharacters),
                ReadGlyph(scrollBars["verticalLineThumb"], source, "glyphs.scrollBars.verticalLineThumb", ref decodedCharacters)),
            new SeparatorGlyphs(
                ReadGlyph(separators["horizontal"], source, "glyphs.separators.horizontal", ref decodedCharacters),
                ReadGlyph(separators["vertical"], source, "glyphs.separators.vertical", ref decodedCharacters),
                ReadGlyph(separators["menu"], source, "glyphs.separators.menu", ref decodedCharacters),
                ReadGlyph(separators["tableHorizontal"], source, "glyphs.separators.tableHorizontal", ref decodedCharacters),
                ReadGlyph(separators["tableVertical"], source, "glyphs.separators.tableVertical", ref decodedCharacters),
                ReadGlyph(separators["tableCross"], source, "glyphs.separators.tableCross", ref decodedCharacters),
                ReadGlyph(separators["tabDivider"], source, "glyphs.separators.tabDivider", ref decodedCharacters),
                ReadGlyph(separators["tabUnderline"], source, "glyphs.separators.tabUnderline", ref decodedCharacters)),
            new TextGlyphs(
                ReadGlyph(text["ellipsis"], source, "glyphs.text.ellipsis", ref decodedCharacters)));
    }

    private static Dictionary<string, JsonElement> ReadObject(
        JsonElement element,
        string source,
        string field,
        string[] members,
        ref int decodedCharacters)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw WrongType(source, field, "an object");
        }

        var expected = new HashSet<string>(members, StringComparer.Ordinal);
        var values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);

        foreach (var property in element.EnumerateObject())
        {
            if (!expected.Contains(property.Name))
            {
                throw new InvalidDataException($"Theme '{source}' has unknown field '{field}.{property.Name}'.");
            }

            if (!values.TryAdd(property.Name, property.Value))
            {
                throw Duplicate(source, property.Name, field);
            }

            AddDecoded(property.Name, source, ref decodedCharacters);
        }

        foreach (var member in members)
        {
            if (!values.ContainsKey(member))
            {
                throw new InvalidDataException($"Theme '{source}' is missing required field '{field}.{member}'.");
            }
        }

        return values;
    }

    private static ThemedGlyph ReadGlyph(
        JsonElement element,
        string source,
        string field,
        ref int decodedCharacters)
    {
        var value = ReadObject(
            element,
            source,
            field,
            ["value", "fallback"],
            ref decodedCharacters);
        var primary = ReadRune(value["value"], source, $"{field}.value", ref decodedCharacters);
        var fallback = ReadRune(value["fallback"], source, $"{field}.fallback", ref decodedCharacters);

        try
        {
            return new ThemedGlyph(primary, fallback);
        }
        catch (ArgumentException error)
        {
            var member = error.ParamName == "fallback" ? "fallback" : "value";
            throw new InvalidDataException(
                $"Theme '{source}' field '{field}.{member}' must contain a printable one-cell glyph.",
                error);
        }
    }

    private static ThemedGlyph[] ReadGlyphArray(
        JsonElement element,
        string source,
        string field,
        ref int decodedCharacters)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw WrongType(source, field, "an array");
        }

        var values = element.EnumerateArray().ToArray();

        if (values.Length != 9)
        {
            throw new InvalidDataException($"Theme '{source}' field '{field}' must contain exactly nine glyphs.");
        }

        var glyphs = new ThemedGlyph[values.Length];

        for (var index = 0; index < values.Length; index++)
        {
            glyphs[index] = ReadGlyph(
                values[index],
                source,
                $"{field}[{index}]",
                ref decodedCharacters);
        }

        return glyphs;
    }

    private static Rune ReadRune(
        JsonElement element,
        string source,
        string field,
        ref int decodedCharacters)
    {
        var value = ReadString(element, source, field, ref decodedCharacters);
        var enumerator = value.EnumerateRunes();

        if (!enumerator.MoveNext())
        {
            throw new InvalidDataException($"Theme '{source}' field '{field}' must contain exactly one Unicode scalar.");
        }

        var rune = enumerator.Current;

        return enumerator.MoveNext()
            ? throw new InvalidDataException($"Theme '{source}' field '{field}' must contain exactly one Unicode scalar.")
            : rune;
    }

    #endregion

    #region Primitive value parsing

    private static Dictionary<string, string> ReadMap(
        JsonElement element,
        string source,
        string field,
        int maximumEntries,
        ref int decodedCharacters)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw WrongType(source, field, "an object");
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var property in element.EnumerateObject())
        {
            if (values.Count == maximumEntries)
            {
                throw new InvalidDataException(
                    $"Theme '{source}' field '{field}' exceeds {maximumEntries} entries.");
            }

            if (string.IsNullOrWhiteSpace(property.Name) || property.Name.Length > MaximumKeyCharacters)
            {
                throw new InvalidDataException(
                    $"Theme '{source}' field '{field}' contains an invalid key.");
            }

            if (values.ContainsKey(property.Name))
            {
                throw Duplicate(source, property.Name, field);
            }

            AddDecoded(property.Name, source, ref decodedCharacters);
            values.Add(
                property.Name,
                ReadString(
                    property.Value,
                    source,
                    $"{field}.{property.Name}",
                    ref decodedCharacters));
        }

        return values;
    }

    private static string ReadString(
        JsonElement element,
        string source,
        string field,
        ref int decodedCharacters)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            throw WrongType(source, field, "a string");
        }

        var value = element.GetString()!;

        if (value.Length > _maximumStringCharacters)
        {
            throw new InvalidDataException(
                $"Theme '{source}' field '{field}' exceeds {_maximumStringCharacters} characters.");
        }

        AddDecoded(value, source, ref decodedCharacters);
        return value;
    }

    private static int ReadInteger(JsonElement element, string source, string field, bool nonNegative)
    {
        return element.ValueKind != JsonValueKind.Number || !element.TryGetInt32(out var value)
            ? throw WrongType(source, field, "a 32-bit integer")
            : nonNegative && value < 0 ? throw new InvalidDataException($"Theme '{source}' field '{field}' cannot be negative.") : value;
    }

    private static void AddDecoded(string value, string source, ref int decodedCharacters)
    {
        var total = (long) decodedCharacters + value.Length;

        if (total > _maximumDecodedCharacters)
        {
            throw new InvalidDataException(
                $"Theme '{source}' exceeds {_maximumDecodedCharacters} decoded characters.");
        }

        decodedCharacters = (int) total;
    }

    private static InvalidDataException Duplicate(string source, string property, string container) =>
        new($"Theme '{source}' field '{container}' contains duplicate property '{property}'.");

    private static InvalidDataException WrongType(string source, string field, string expected) =>
        new($"Theme '{source}' field '{field}' must be {expected}.");

    private static InvalidDataException TooLarge(string source) =>
        new($"Theme '{source}' exceeds the {MaximumDocumentBytes}-byte document limit.");

    #endregion

    #region Color resolution

    private static Dictionary<string, Color> ResolvePalette(ThemeDefinition definition, string source)
    {
        var palette = new Dictionary<string, Color>(StringComparer.Ordinal);

        if (definition.Palette is null)
        {
            return palette;
        }

        foreach (var entry in definition.Palette)
        {
            if (entry.Value is null)
            {
                throw new InvalidDataException(
                    $"Theme '{source}' palette entry '{entry.Key}' is null.");
            }

            if (!ThemeColorValue.IsLiteral(entry.Value))
            {
                throw new InvalidDataException(
                    $"Theme '{source}' palette entry '{entry.Key}' must be a #hex or idx:N value.");
            }

            palette[entry.Key] = ParseOrThrow(entry.Value, source, $"palette entry '{entry.Key}'");
        }

        return palette;
    }

    private static Dictionary<ColorRole, Color> ResolveRoles(
        ThemeDefinition definition,
        Dictionary<string, Color> palette,
        string source)
    {
        Dictionary<ColorRole, Color> roles = [];

        if (definition.Roles is null)
        {
            return roles;
        }

        foreach (var entry in definition.Roles)
        {
            if (!_roleNames.TryGetValue(entry.Key, out var role))
            {
                throw new InvalidDataException($"Theme '{source}' has unknown role '{entry.Key}'.");
            }

            if (entry.Value is null)
            {
                throw new InvalidDataException($"Theme '{source}' role '{entry.Key}' is null.");
            }

            roles[role] = ThemeColorValue.IsLiteral(entry.Value)
                ? ParseOrThrow(entry.Value, source, $"role '{entry.Key}'")
                : palette.TryGetValue(entry.Value, out var color)
                    ? color
                    : throw new InvalidDataException(
                        $"Theme '{source}' role '{entry.Key}' references unknown palette key '{entry.Value}'.");
        }

        return roles;
    }

    private static void FillFallbacks(Dictionary<ColorRole, Color> roles, string source)
    {
        if (!roles.ContainsKey(ColorRole.Background) || !roles.ContainsKey(ColorRole.Foreground))
        {
            throw new InvalidDataException(
                $"Theme '{source}' must define both 'background' and 'foreground'.");
        }

        // Fixed order so the Border/Muted cross-reference terminates at a required role.
        Fallback(roles, ColorRole.Accent, ColorRole.Foreground);

        // Muted first: takes explicit Border if present, else Foreground.
        if (!roles.ContainsKey(ColorRole.Muted))
        {
            roles[ColorRole.Muted] = roles.TryGetValue(ColorRole.Border, out var border)
                ? border
                : roles[ColorRole.Foreground];
        }

        // Border then resolves to the (now-present) Muted.
        Fallback(roles, ColorRole.Border, ColorRole.Muted);

        Fallback(roles, ColorRole.Surface, ColorRole.Background);
        Fallback(roles, ColorRole.SelectionBackground, ColorRole.Accent);
        Fallback(roles, ColorRole.SelectionForeground, ColorRole.Foreground);
        Fallback(roles, ColorRole.Error, ColorRole.Accent);
        Fallback(roles, ColorRole.Warning, ColorRole.Accent);
        Fallback(roles, ColorRole.Success, ColorRole.Accent);
        Fallback(roles, ColorRole.Info, ColorRole.Accent);
    }

    private static void Fallback(Dictionary<ColorRole, Color> roles, ColorRole target, ColorRole source)
    {
        if (!roles.ContainsKey(target))
        {
            roles[target] = roles[source];
        }
    }

    private static Color ParseOrThrow(string value, string source, string where)
    {
        try
        {
            return ThemeColorValue.ParseLiteral(value);
        }
        catch (FormatException error)
        {
            throw new InvalidDataException($"Theme '{source}' {where} has invalid color '{value}'.", error);
        }
    }

    #endregion
}
