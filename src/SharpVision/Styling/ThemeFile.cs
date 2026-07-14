// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Loads themes from JSON text, streams, or files at runtime.</summary>
public static class ThemeFile
{
    /// <summary>Parses a theme from JSON text.</summary>
    /// <param name="json">The theme JSON.</param>
    /// <returns>The frozen theme.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="json"/> is null.</exception>
    /// <exception cref="InvalidDataException">The JSON is malformed or invalid.</exception>
    public static Theme Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return ThemeLoader.FromJson(json, "<parsed>");
    }

    /// <summary>Loads a theme from a UTF-8 JSON stream. The caller owns the stream.</summary>
    /// <param name="stream">The readable JSON stream.</param>
    /// <returns>The frozen theme.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
    /// <exception cref="InvalidDataException">The content is malformed or invalid.</exception>
    public static Theme Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using StreamReader reader = new(stream, leaveOpen: true);
        return ThemeLoader.FromJson(reader.ReadToEnd(), "<stream>");
    }

    /// <summary>Loads a theme from a JSON file path.</summary>
    /// <param name="path">The file path.</param>
    /// <returns>The frozen theme.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="IOException">The file could not be read.</exception>
    /// <exception cref="InvalidDataException">The content is malformed or invalid.</exception>
    public static Theme LoadFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return ThemeLoader.FromJson(File.ReadAllText(path), path);
    }
}
