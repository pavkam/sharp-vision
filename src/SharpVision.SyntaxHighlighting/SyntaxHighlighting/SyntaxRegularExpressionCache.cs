// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting;

/// <summary>Retains a bounded least-recently-used set of capture-substituted regular expressions.
/// Compilation occurs outside the cache lock so untrusted patterns never hold shared state.</summary>
internal sealed class SyntaxRegularExpressionCache
{
    private readonly int _capacity;
    private readonly Dictionary<string, LinkedListNode<KeyValuePair<string, PcreRegex>>> _entries = new(StringComparer.Ordinal);
    private readonly LinkedList<KeyValuePair<string, PcreRegex>> _recency = new();
    private readonly Lock _gate = new();

    /// <summary>Initializes a cache with a positive maximum entry count.</summary>
    /// <param name="capacity">The maximum number of compiled expressions retained.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is not positive.</exception>
    internal SyntaxRegularExpressionCache(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _capacity = capacity;
    }

    /// <summary>Gets the current retained expression count.</summary>
    internal int Count
    {
        get
        {
            lock (_gate)
            {
                return _entries.Count;
            }
        }
    }

    /// <summary>Returns the cached expression or compiles and inserts it, evicting the least
    /// recently used entry when the capacity is reached.</summary>
    /// <param name="pattern">The effective capture-substituted pattern.</param>
    /// <param name="factory">The compiler invoked outside the cache lock on a miss.</param>
    internal PcreRegex GetOrAdd(string pattern, Func<string, PcreRegex> factory)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(factory);

        lock (_gate)
        {
            if (_entries.TryGetValue(pattern, out var cached))
            {
                _recency.Remove(cached);
                _recency.AddFirst(cached);
                return cached.Value.Value;
            }
        }

        var compiled = factory(pattern);

        lock (_gate)
        {
            if (_entries.TryGetValue(pattern, out var raced))
            {
                _recency.Remove(raced);
                _recency.AddFirst(raced);
                return raced.Value.Value;
            }

            var added = _recency.AddFirst(new KeyValuePair<string, PcreRegex>(pattern, compiled));
            _entries.Add(pattern, added);

            if (_entries.Count > _capacity)
            {
                var evicted = _recency.Last!;
                _recency.RemoveLast();
                _ = _entries.Remove(evicted.Value.Key);
            }

            return compiled;
        }
    }
}
