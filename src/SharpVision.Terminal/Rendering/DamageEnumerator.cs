// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Rendering;

using System.Diagnostics;

/// <summary>Finds deterministic row-major semantic damage without retaining frame memory.</summary>
public ref struct DamageEnumerator
{
    private readonly Frame? _front;
    private readonly Frame _back;
    private readonly bool _full;
    private int _row;
    private int _column;

    /// <summary>Initializes an enumerator positioned before the first run.</summary>
    /// <param name="front">The optional committed frame.</param>
    /// <param name="back">The target frame.</param>
    /// <param name="full">Whether complete damage is required.</param>
    internal DamageEnumerator(Frame? front, Frame back, bool full)
    {
        _front = front;
        _back = back;
        _full = full || front is null || front.Size != back.Size;
        _row = 0;
        _column = 0;
        Current = default;
    }

    /// <summary>Gets the current changed run.</summary>
    public DamageSpan Current { get; private set; }

    /// <summary>Advances to the next changed run.</summary>
    /// <returns><see langword="true"/> when a run is available.</returns>
    public bool MoveNext()
    {
        int width = _back.Size.Width;
        int height = _back.Size.Height;

        while (_row < height)
        {
            if (width == 0)
            {
                _row++;
                _column = 0;
                continue;
            }

            if (_full)
            {
                Current = new DamageSpan(_row++, 0, width);
                _column = 0;
                return true;
            }

            Debug.Assert(_front is not null, "Incremental damage requires a front frame.");

            while (_column < width && CellsEqual(_row, _column))
            {
                _column++;
            }

            if (_column == width)
            {
                _row++;
                _column = 0;
                continue;
            }

            int start = Math.Min(
                _front.GetLeadColumn(_row, _column),
                _back.GetLeadColumn(_row, _column));
            int end = _column + 1;

            while (end < width && !CellsEqual(_row, end))
            {
                end++;
            }

            int expanded = Math.Max(
                _front.GetOwnedEnd(_row, end - 1),
                _back.GetOwnedEnd(_row, end - 1));
            end = Math.Max(end, expanded);
            _column = end;
            Current = new DamageSpan(_row, start, end - start);
            return true;
        }

        return false;
    }

    private readonly bool CellsEqual(int row, int column)
    {
        Debug.Assert(_front is not null, "Incremental comparison requires a front frame.");
        int index = checked((row * _back.Size.Width) + column);
        return _front.SemanticallyEquals(_back, index);
    }
}
