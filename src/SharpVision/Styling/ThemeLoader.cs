// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

using System.Text.Json;

using SharpVision.Terminal.Protocols;

/// <summary>Turns theme JSON and definitions into frozen <see cref="Theme"/> instances.</summary>
internal static class ThemeLoader
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = false,
    };

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

    /// <summary>Deserializes theme JSON into a definition.</summary>
    /// <param name="json">The theme JSON text.</param>
    /// <param name="source">A label (slug or path) used in error messages.</param>
    /// <returns>The deserialized definition.</returns>
    /// <exception cref="InvalidDataException">The JSON is malformed or empty.</exception>
    public static ThemeDefinition Deserialize(string json, string source)
    {
        try
        {
            return JsonSerializer.Deserialize<ThemeDefinition>(json, _options)
                ?? throw new InvalidDataException($"Theme '{source}' deserialized to null.");
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

        Dictionary<string, Color> palette = ResolvePalette(definition, source);
        Dictionary<ColorRole, Color> roles = ResolveRoles(definition, palette, source);
        FillFallbacks(roles, source);
        return ThemeBuilder.Build(roles);
    }

    private static Dictionary<string, Color> ResolvePalette(ThemeDefinition definition, string source)
    {
        Dictionary<string, Color> palette = new(StringComparer.Ordinal);

        if (definition.Palette is null)
        {
            return palette;
        }

        foreach (KeyValuePair<string, string> entry in definition.Palette)
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

        foreach (KeyValuePair<string, string> entry in definition.Roles)
        {
            if (!_roleNames.TryGetValue(entry.Key, out ColorRole role))
            {
                throw new InvalidDataException($"Theme '{source}' has unknown role '{entry.Key}'.");
            }

            if (entry.Value is null)
            {
                throw new InvalidDataException($"Theme '{source}' role '{entry.Key}' is null.");
            }

            roles[role] = ThemeColorValue.IsLiteral(entry.Value)
                ? ParseOrThrow(entry.Value, source, $"role '{entry.Key}'")
                : palette.TryGetValue(entry.Value, out Color color)
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
            roles[ColorRole.Muted] = roles.TryGetValue(ColorRole.Border, out Color border)
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
}
