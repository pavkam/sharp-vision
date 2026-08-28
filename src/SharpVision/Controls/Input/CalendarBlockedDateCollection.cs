// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

/// <summary>Owns normalized inclusive date ranges that a <see cref="Calendar"/> cannot select.</summary>
[PublicAPI]
public sealed class CalendarBlockedDateCollection: IReadOnlyCollection<DateInterval>
{
    private readonly Calendar _owner;
    private List<DateInterval> _ranges = [];

    /// <summary>Initializes an empty collection owned by one calendar.</summary>
    /// <param name="owner">The calendar that validates mutation and consumes changes.</param>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> is null.</exception>
    internal CalendarBlockedDateCollection(Calendar owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
    }

    /// <summary>Gets the number of normalized non-touching ranges.</summary>
    public int Count => _ranges.Count;

    /// <summary>Blocks one date.</summary>
    /// <param name="date">The date to make unavailable.</param>
    /// <exception cref="InvalidOperationException">The range would block the complete selectable domain, or the attached owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The owner is disposed.</exception>
    public void Block(DateOnly date) => Block(new DateInterval(date, date));

    /// <summary>Blocks one inclusive range and coalesces overlapping or adjacent ranges.</summary>
    /// <param name="interval">The inclusive range to make unavailable.</param>
    /// <exception cref="InvalidOperationException">The range would block the complete selectable domain, or the attached owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The owner is disposed.</exception>
    public void Block(DateInterval interval)
    {
        _owner.VerifyCalendarMutable();
        var start = interval.Start.DayNumber;
        var end = interval.End.DayNumber;
        var result = new List<DateInterval>(_ranges.Count + 1);
        var inserted = false;

        foreach (var current in _ranges)
        {
            if (current.End.DayNumber + 1 < start)
            {
                result.Add(current);
                continue;
            }

            if (end + 1 < current.Start.DayNumber)
            {
                if (!inserted)
                {
                    result.Add(Create(start, end));
                    inserted = true;
                }

                result.Add(current);
                continue;
            }

            start = Math.Min(start, current.Start.DayNumber);
            end = Math.Max(end, current.End.DayNumber);
        }

        if (!inserted)
        {
            result.Add(Create(start, end));
        }

        ReplaceIfChanged(result);
    }

    /// <summary>Makes one date selectable again.</summary>
    /// <param name="date">The date to remove from blocked ranges.</param>
    /// <exception cref="InvalidOperationException">The attached owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The owner is disposed.</exception>
    public void Unblock(DateOnly date) => Unblock(new DateInterval(date, date));

    /// <summary>Makes one inclusive range selectable again, splitting blocked ranges when required.</summary>
    /// <param name="interval">The inclusive range to remove.</param>
    /// <exception cref="InvalidOperationException">The attached owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The owner is disposed.</exception>
    public void Unblock(DateInterval interval)
    {
        _owner.VerifyCalendarMutable();
        var result = new List<DateInterval>(_ranges.Count + 1);

        foreach (var current in _ranges)
        {
            if (!current.Intersects(interval))
            {
                result.Add(current);
                continue;
            }

            if (current.Start < interval.Start)
            {
                result.Add(new DateInterval(current.Start, interval.Start.AddDays(-1)));
            }

            if (current.End > interval.End)
            {
                result.Add(new DateInterval(interval.End.AddDays(1), current.End));
            }
        }

        ReplaceIfChanged(result);
    }

    /// <summary>Determines whether one date is blocked.</summary>
    /// <param name="date">The date to search for.</param>
    /// <returns>True when a normalized range contains <paramref name="date"/>.</returns>
    [Pure]
    public bool Contains(DateOnly date)
    {
        var low = 0;
        var high = _ranges.Count - 1;

        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            var interval = _ranges[middle];

            if (date < interval.Start)
            {
                high = middle - 1;
            }
            else if (date > interval.End)
            {
                low = middle + 1;
            }
            else
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Determines whether any blocked range intersects an interval.</summary>
    /// <param name="interval">The inclusive interval to test.</param>
    /// <returns>True when at least one date in <paramref name="interval"/> is blocked.</returns>
    [Pure]
    internal bool Intersects(DateInterval interval)
    {
        foreach (var blocked in _ranges)
        {
            if (blocked.Start > interval.End)
            {
                return false;
            }

            if (blocked.Intersects(interval))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Removes every blocked range.</summary>
    /// <exception cref="InvalidOperationException">The attached owner is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The owner is disposed.</exception>
    public void Clear()
    {
        _owner.VerifyCalendarMutable();

        if (_ranges.Count == 0)
        {
            return;
        }

        _ranges = [];
        _owner.OnBlockedDatesChanged();
    }

    /// <summary>Gets a stable enumerator over the current normalized ranges.</summary>
    /// <returns>An enumerator over ranges in ascending date order.</returns>
    public IEnumerator<DateInterval> GetEnumerator() => _ranges.GetEnumerator();

    /// <summary>Gets a stable non-generic enumerator over normalized ranges.</summary>
    /// <returns>An enumerator over ranges in ascending date order.</returns>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private static DateInterval Create(int start, int end) =>
        new(DateOnly.FromDayNumber(start), DateOnly.FromDayNumber(end));

    private void ReplaceIfChanged(List<DateInterval> result)
    {
        if (_ranges.SequenceEqual(result))
        {
            return;
        }

        Calendar.ValidateSelectableDomain(_owner.MinimumDate, _owner.MaximumDate, result);
        _ranges = result;
        _owner.OnBlockedDatesChanged();
    }
}
