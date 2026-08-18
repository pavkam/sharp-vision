// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>Caches JSON-key-to-property lookups for <see cref="Theme.Overlay"/>.</summary>
internal static class ThemeStyleFragment
{
    // A plain Dictionary populated from multiple threads here would be a real corruption
    // hazard: Theme.Parse/Theme.Load(Stream)/Theme.LoadFile have no lock of their own and are
    // directly reachable concurrently through public API, unlike the built-in catalog's
    // ThemeCatalog.Load(slug), which the Lazy<T> in ThemeCatalog.cs already serializes.
    private static readonly ConcurrentDictionary<(Type Type, string JsonKey), PropertyInfo?> _propertyCache = new();

    /// <summary>Resolves the public instance property whose JSON name matches the given key -
    /// an explicit <see cref="JsonPropertyNameAttribute"/> wins when present (e.g.
    /// <c>Shadow.IsVisible</c> maps to "visible", not the camelCase-of-property-name "isVisible"),
    /// otherwise the camelCase-of-property-name convention <c>ThemeCatalog.cs</c>'s fixed DTOs also use.</summary>
    /// <param name="type">The fragment type to search.</param>
    /// <param name="jsonKey">The exact JSON member name.</param>
    /// <returns>The matching property, or null when no public property maps to that key.</returns>
    internal static PropertyInfo? ResolveProperty(Type type, string jsonKey) =>
        _propertyCache.GetOrAdd((type, jsonKey), static key =>
            key.Type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                // Settable only. A get-only computed property - CheckBoxStyle.MarkWidth,
                // RadioButtonStyle's MarkWidth/UncheckedText/CheckedText - otherwise resolved by
                // name, passed the "is not a known property" guard, converted cleanly, and then
                // threw a raw ArgumentException from SetValue. Such a member is derived from an
                // authorable one, so refusing it by name is the honest answer.
                .Where(static property => property.CanWrite)
                .FirstOrDefault(property =>
                    (property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ??
                        JsonNamingPolicy.CamelCase.ConvertName(property.Name)) == key.JsonKey));
}
