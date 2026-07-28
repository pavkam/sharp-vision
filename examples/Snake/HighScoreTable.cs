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

    /// <summary>Returns whether a non-negative score qualifies for the table.</summary>
    /// <param name="score">The non-negative score to compare.</param>
    /// <returns><see langword="true"/> when the score has a place in the table; otherwise, false.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="score"/> is negative.</exception>
    public bool Qualifies(int score)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(score);
        return _entries.Count < 10 || score > _entries[^1].Score;
    }

    /// <summary>Inserts a score with up to three Unicode initials and returns the rank (1-based).</summary>
    /// <param name="name">The non-blank initials; only the first three Unicode scalars are stored.</param>
    /// <param name="score">The non-negative score.</param>
    /// <returns>The inserted entry's one-based rank.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is empty, whitespace, or contains a non-letter Unicode scalar.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="score"/> is negative.</exception>
    public int Insert(string name, int score)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegative(score);

        var initials = new StringBuilder();
        var runeCount = 0;

        foreach (var rune in name.EnumerateRunes())
        {
            if (!Rune.IsLetter(rune))
            {
                throw new ArgumentException("Initials may contain Unicode letters only.", nameof(name));
            }

            if (runeCount < 3)
            {
                _ = initials.Append(Rune.ToUpperInvariant(rune));
                runeCount++;
            }
        }

        while (runeCount < 3)
        {
            _ = initials.Append(' ');
            runeCount++;
        }

        var entry = (initials.ToString(), score);
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
