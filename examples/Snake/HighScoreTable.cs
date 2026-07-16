// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Snake;

/// <summary>Manages the top-10 high scores with three-letter initials.</summary>
public sealed class HighScoreTable
{
    private readonly List<(string Name, int Score)> _entries = [];

    /// <summary>Initializes the table with seed entries.</summary>
    public HighScoreTable()
    {
        _entries.Add(("ACE", 500));
        _entries.Add(("PRO", 350));
        _entries.Add(("TUI", 200));
        _entries.Add(("DOT", 100));
        _entries.Add(("NET", 50));
    }

    /// <summary>Gets the top entries.</summary>
    public IReadOnlyList<(string Name, int Score)> Entries => _entries;

    /// <summary>Returns whether the score qualifies for the table.</summary>
    public bool Qualifies(int score) => _entries.Count < 10 || score > _entries[^1].Score;

    /// <summary>Inserts a score and returns the rank (1-based).</summary>
    public int Insert(string name, int score)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var entry = (name.ToUpperInvariant()[..Math.Min(3, name.Length)].PadRight(3), score);
        var index = 0;

        while (index < _entries.Count && _entries[index].Score >= score)
        {
            index++;
        }

        _entries.Insert(index, entry);

        if (_entries.Count > 10)
        {
            _entries.RemoveAt(10);
        }

        return index + 1;
    }
}
