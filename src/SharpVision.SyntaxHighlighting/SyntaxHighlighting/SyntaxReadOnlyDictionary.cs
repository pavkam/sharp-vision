// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting;

using System.Collections;

/// <summary>Owns a dictionary copy exposed only through read-only lookup and enumeration.</summary>
/// <typeparam name="TKey">The key type.</typeparam>
/// <typeparam name="TValue">The value type.</typeparam>
internal sealed class SyntaxReadOnlyDictionary<TKey, TValue>: IReadOnlyDictionary<TKey, TValue>
    where TKey : notnull
{
    private readonly Dictionary<TKey, TValue> _items;

    /// <summary>Initializes an owned snapshot of <paramref name="items"/>.</summary>
    /// <param name="items">The non-null entries to copy.</param>
    public SyntaxReadOnlyDictionary(IEnumerable<KeyValuePair<TKey, TValue>> items) => _items = new Dictionary<TKey, TValue>(items);

    /// <inheritdoc/>
    public int Count => _items.Count;

    /// <inheritdoc/>
    public IEnumerable<TKey> Keys => _items.Keys;

    /// <inheritdoc/>
    public IEnumerable<TValue> Values => _items.Values;

    /// <inheritdoc/>
    public TValue this[TKey key] => _items[key];

    /// <inheritdoc/>
    public bool ContainsKey(TKey key) => _items.ContainsKey(key);

    /// <inheritdoc/>
    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value) => _items.TryGetValue(key, out value);

    /// <inheritdoc/>
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _items.GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();
}
