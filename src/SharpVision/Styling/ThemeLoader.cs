// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

using System.Text.Json;

/// <summary>Turns theme JSON and definitions into frozen <see cref="Theme"/> instances.</summary>
internal static class ThemeLoader
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = false,
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
}
