// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

using System.Runtime.ExceptionServices;

/// <summary>Owns the nullable, bounded, lazily seeded, versioned value contract shared by the
/// date, time, and date-time segmented input controls.</summary>
/// <typeparam name="T">The immutable temporal value type.</typeparam>
/// <remarks>The owning control supplies dispatcher/lifetime validation, property publication,
/// its dispatcher-selected clock projection, and its typed value-event adapter. Optional
/// synchronization callbacks let a date-bearing owner keep a connected calendar current without
/// duplicating the model's commit and reentrancy rules.</remarks>
internal sealed class TemporalValueState<T>
    where T : struct, IComparable<T>
{
    private readonly Action _verifyMutable;
    private readonly Action<string, InvalidationImpact> _notifyPropertyChanged;
    private readonly Func<T> _resolveSeed;
    private readonly Action<T?, T?> _raiseValueChanged;
    private readonly Action<T?>? _synchronizeValue;
    private readonly Action? _synchronizeBounds;
    private T _minimum;
    private T _maximum;

    /// <summary>Initializes shared temporal state with its full representable range and owner callbacks.</summary>
    /// <param name="minimum">The initial inclusive lower bound.</param>
    /// <param name="maximum">The initial inclusive upper bound.</param>
    /// <param name="verifyMutable">Validates the owning control's dispatcher and lifetime.</param>
    /// <param name="notifyPropertyChanged">Publishes a committed public property and invalidates its affected phase.</param>
    /// <param name="resolveSeed">Projects the owning dispatcher's current clock into <typeparamref name="T"/>.</param>
    /// <param name="raiseValueChanged">Raises the owning control's typed value event.</param>
    /// <param name="synchronizeValue">Optionally synchronizes a connected presentation after a value commit.</param>
    /// <param name="synchronizeBounds">Optionally synchronizes connected range presentation after a bound commit.</param>
    /// <exception cref="ArgumentException"><paramref name="minimum"/> exceeds <paramref name="maximum"/>.</exception>
    /// <exception cref="ArgumentNullException">Any required callback is null.</exception>
    public TemporalValueState(
        T minimum,
        T maximum,
        Action verifyMutable,
        Action<string, InvalidationImpact> notifyPropertyChanged,
        Func<T> resolveSeed,
        Action<T?, T?> raiseValueChanged,
        Action<T?>? synchronizeValue = null,
        Action? synchronizeBounds = null)
    {
        if (minimum.CompareTo(maximum) > 0)
        {
            throw new ArgumentException("Minimum cannot exceed maximum.", nameof(minimum));
        }

        ArgumentNullException.ThrowIfNull(verifyMutable);
        ArgumentNullException.ThrowIfNull(notifyPropertyChanged);
        ArgumentNullException.ThrowIfNull(resolveSeed);
        ArgumentNullException.ThrowIfNull(raiseValueChanged);

        _minimum = minimum;
        _maximum = maximum;
        _verifyMutable = verifyMutable;
        _notifyPropertyChanged = notifyPropertyChanged;
        _resolveSeed = resolveSeed;
        _raiseValueChanged = raiseValueChanged;
        _synchronizeValue = synchronizeValue;
        _synchronizeBounds = synchronizeBounds;
    }

    /// <summary>Gets the current value without forcing lazy clock seeding.</summary>
    public T? Value { get; private set; }

    /// <summary>Gets whether null is admitted.</summary>
    public bool AllowNull { get; private set; } = true;

    /// <summary>Gets the inclusive lower bound.</summary>
    public T Minimum => _minimum;

    /// <summary>Gets the inclusive upper bound.</summary>
    public T Maximum => _maximum;

    /// <summary>Gets whether the dispatcher clock seed has been resolved.</summary>
    public bool IsSeeded { get; private set; }

    /// <summary>Gets the monotonically increasing committed-value version used to reject stale popup sessions.</summary>
    public long ValueVersion { get; private set; }

    /// <summary>Gets the monotonically increasing bounds version used to reject stale popup sessions.</summary>
    public long BoundsVersion { get; private set; }

    /// <summary>Resolves and clamps the dispatcher-selected clock seed exactly once.</summary>
    /// <returns>The current nullable value.</returns>
    public T? EnsureSeeded()
    {
        if (IsSeeded)
        {
            return Value;
        }

        IsSeeded = true;
        Value = Clamp(_resolveSeed());
        _synchronizeValue?.Invoke(Value);
        return Value;
    }

    /// <summary>Validates and commits a caller-supplied nullable value.</summary>
    /// <returns>True when the committed value changed.</returns>
    public bool SetValue(T? value)
    {
        _verifyMutable();
        _ = EnsureSeeded();

        var candidate = value.HasValue
            ? Clamp(value.Value)
            : AllowNull ? null : Value;

        if (EqualityComparer<T?>.Default.Equals(Value, candidate))
        {
            return false;
        }

        var previous = Value;
        Value = candidate;
        var version = ++ValueVersion;
        ExceptionDispatchInfo? failure = null;
        ExceptionAggregation.Capture(
            () => _notifyPropertyChanged("Value", InvalidationImpact.Render),
            ref failure);

        if (IsCurrent(candidate, version))
        {
            ExceptionAggregation.Capture(() => _synchronizeValue?.Invoke(candidate), ref failure);
        }

        if (IsCurrent(candidate, version))
        {
            ExceptionAggregation.Capture(() => _raiseValueChanged(previous, candidate), ref failure);
        }

        failure?.Throw();
        return true;
    }

    /// <summary>Commits null admission and repairs an already-seeded null when admission closes.</summary>
    /// <returns>True when the policy changed.</returns>
    public bool SetAllowNull(bool value)
    {
        _verifyMutable();

        if (AllowNull == value)
        {
            return false;
        }

        AllowNull = value;
        ExceptionDispatchInfo? failure = null;
        ExceptionAggregation.Capture(
            () => _notifyPropertyChanged("AllowNull", InvalidationImpact.None),
            ref failure);

        if (!AllowNull && IsSeeded && Value is null)
        {
            ExceptionAggregation.Capture(() => _ = SetValue(Clamp(_resolveSeed())), ref failure);
        }

        failure?.Throw();
        return true;
    }

    /// <summary>Commits the inclusive lower bound and repairs the current value.</summary>
    /// <returns>True when the bound changed.</returns>
    /// <exception cref="ArgumentException"><paramref name="value"/> exceeds <see cref="Maximum"/>.</exception>
    public bool SetMinimum(T value)
    {
        ArgumentException.ThrowIfAboveMaximum(
            value, _maximum, nameof(value), "Minimum cannot exceed Maximum.");

        return SetBound(ref _minimum, value, nameof(Minimum));
    }

    /// <summary>Commits the inclusive upper bound and repairs the current value.</summary>
    /// <returns>True when the bound changed.</returns>
    /// <exception cref="ArgumentException"><paramref name="value"/> is below <see cref="Minimum"/>.</exception>
    public bool SetMaximum(T value)
    {
        ArgumentException.ThrowIfBelowMinimum(
            value, _minimum, nameof(value), "Maximum cannot be less than Minimum.");

        return SetBound(ref _maximum, value, nameof(Maximum));
    }

    /// <summary>Clamps a candidate into the current inclusive range.</summary>
    /// <param name="value">The candidate value.</param>
    /// <returns>The candidate or the nearest endpoint.</returns>
    [Pure]
    public T Clamp(T value) => value.CompareTo(_minimum) < 0
        ? _minimum
        : value.CompareTo(_maximum) > 0 ? _maximum : value;

    private bool SetBound(ref T storage, T value, string propertyName)
    {
        _verifyMutable();

        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return false;
        }

        storage = value;
        BoundsVersion++;
        ExceptionDispatchInfo? failure = null;
        ExceptionAggregation.Capture(
            () => _notifyPropertyChanged(propertyName, InvalidationImpact.Render),
            ref failure);
        ExceptionAggregation.Capture(() => _synchronizeBounds?.Invoke(), ref failure);
        ExceptionAggregation.Capture(RepairValue, ref failure);
        failure?.Throw();
        return true;
    }

    private void RepairValue()
    {
        if (Value is { } current)
        {
            _ = SetValue(Clamp(current));
        }
    }

    [Pure]
    private bool IsCurrent(T? candidate, long version) =>
        ValueVersion == version && EqualityComparer<T?>.Default.Equals(Value, candidate);
}
