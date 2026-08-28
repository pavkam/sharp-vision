// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

using System.Runtime.ExceptionServices;

/// <summary>
/// Orchestrates the buffer-then-commit workflow shared by every buffer-then-commit numeric field
/// control (<see cref="Controls.Input.NumberInput"/>, <see cref="Controls.Input.CurrencyInput"/>):
/// parsing and rounding a completed <see cref="NumericEditBuffer"/> into a candidate value on
/// commit, clamping every candidate into range, stepping and jumping to a bound, reverting to the
/// committed value's own formatting, and repairing a stale committed value after a bound tightens -
/// all without each control re-deriving the same shape.
/// </summary>
/// <remarks>
/// <para>
/// Owns the committed nullable value and its range policy. The owning control supplies only its
/// mutation guard, property publication, focused-buffer refresh, and typed value-event adapter,
/// alongside the one formula - how a parsed buffer rounds - that differs between
/// <see cref="Controls.Input.NumberInput"/> and <see cref="Controls.Input.CurrencyInput"/> today
/// (integer-aware truncation plus a fixed <c>DecimalPlaces</c> for the former, a culture-derived
/// <c>EffectiveDecimalPlaces</c> for the latter). Clamping and the step/jump/rounding-toward-range
/// arithmetic built on top of it are otherwise identical between the two controls, so this type owns
/// them outright rather than taking them as delegates.
/// </para>
/// <para>
/// Routed editing and focus transitions are composed separately by <see cref="NumericEditBehavior"/>;
/// this type remains the authoritative numeric model and commit engine.
/// </para>
/// </remarks>
internal sealed class NumericInputCommitCoordinator
{
    private long _commitVersion;
    private readonly NumericEditBuffer _buffer;
    private readonly Action _verifyMutable;
    private readonly Action<string, InvalidationImpact> _notifyPropertyChanged;
    private readonly Func<decimal, decimal> _resolveCommitRounding;
    private readonly Func<bool> _isFocused;
    private readonly Action _refreshBuffer;
    private readonly Action<decimal?, decimal?> _raiseValueChanged;
    private decimal _minimum = decimal.MinValue;
    private decimal _maximum = decimal.MaxValue;

    /// <summary>Initializes a coordinator bound to one control's buffer and control-specific
    /// data and callbacks.</summary>
    /// <param name="buffer">The shared transient edit buffer this control owns.</param>
    /// <param name="verifyMutable">Validates the owning control's dispatcher and lifetime.</param>
    /// <param name="notifyPropertyChanged">Publishes one committed public property and invalidates
    /// its earliest affected phase.</param>
    /// <param name="resolveCommitRounding">Resolves the decimal places and rounding policy a freshly
    /// parsed buffer value commits under.</param>
    /// <param name="isFocused">Reads whether the caller currently holds focus.</param>
    /// <param name="refreshBuffer">Reloads the buffer from the currently committed value.</param>
    /// <param name="raiseValueChanged">Raises the caller's own typed ValueChanged event with the
    /// previous and new value.</param>
    /// <exception cref="ArgumentNullException">Any parameter is null.</exception>
    public NumericInputCommitCoordinator(
        NumericEditBuffer buffer,
        Action verifyMutable,
        Action<string, InvalidationImpact> notifyPropertyChanged,
        Func<decimal, decimal> resolveCommitRounding,
        Func<bool> isFocused,
        Action refreshBuffer,
        Action<decimal?, decimal?> raiseValueChanged)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentNullException.ThrowIfNull(verifyMutable);
        ArgumentNullException.ThrowIfNull(notifyPropertyChanged);
        ArgumentNullException.ThrowIfNull(resolveCommitRounding);
        ArgumentNullException.ThrowIfNull(isFocused);
        ArgumentNullException.ThrowIfNull(refreshBuffer);
        ArgumentNullException.ThrowIfNull(raiseValueChanged);

        _buffer = buffer;
        _verifyMutable = verifyMutable;
        _notifyPropertyChanged = notifyPropertyChanged;
        _resolveCommitRounding = resolveCommitRounding;
        _isFocused = isFocused;
        _refreshBuffer = refreshBuffer;
        _raiseValueChanged = raiseValueChanged;
    }

    /// <summary>Gets the committed nullable value.</summary>
    public decimal? Value { get; private set; }

    /// <summary>Gets whether null is admitted.</summary>
    public bool AllowNull { get; private set; } = true;

    /// <summary>Gets the inclusive lower bound.</summary>
    public decimal Minimum => _minimum;

    /// <summary>Gets the inclusive upper bound.</summary>
    public decimal Maximum => _maximum;

    /// <summary>Gets the positive step increment.</summary>
    public decimal Step { get; private set; } = 1m;

    /// <summary>Validates and commits a caller-supplied nullable value.</summary>
    public bool SetValue(decimal? value)
    {
        _verifyMutable();

        return value.HasValue
            ? CommitValue(ClampToRange(value.Value))
            : AllowNull && CommitValue(null);
    }

    /// <summary>Commits the null-admission policy and performs any required deterministic reseed.</summary>
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
            () => _notifyPropertyChanged(nameof(Controls.Input.NumberInput.AllowNull), InvalidationImpact.None),
            ref failure);
        ExceptionAggregation.Capture(RepairNullPolicy, ref failure);
        failure?.Throw();
        return true;
    }

    /// <summary>Validates and commits the inclusive lower bound, then repairs the live value.</summary>
    public bool SetMinimum(decimal value)
    {
        ArgumentException.ThrowIfAboveMaximum(value, _maximum, nameof(value), "Minimum cannot exceed Maximum.");
        return SetBound(ref _minimum, value, nameof(Controls.Input.NumberInput.Minimum));
    }

    /// <summary>Validates and commits the inclusive upper bound, then repairs the live value.</summary>
    public bool SetMaximum(decimal value)
    {
        ArgumentException.ThrowIfBelowMinimum(value, _minimum, nameof(value), "Maximum cannot be less than Minimum.");
        return SetBound(ref _maximum, value, nameof(Controls.Input.NumberInput.Maximum));
    }

    /// <summary>Validates and commits the positive step increment.</summary>
    public bool SetStep(decimal value)
    {
        ArgumentOutOfRangeException.ThrowIfNotAPositiveStep(value, nameof(value));
        _verifyMutable();

        if (Step == value)
        {
            return false;
        }

        Step = value;
        _notifyPropertyChanged(nameof(Controls.Input.NumberInput.Step), InvalidationImpact.None);
        return true;
    }

    /// <summary>Parses the buffer into a rounded, clamped candidate and commits it - or, for an
    /// empty buffer, clears the value when null is allowed - then reloads the buffer from whatever
    /// value ends up committed.</summary>
    /// <returns>Always true; the buffer-then-commit keys treat Enter as handled regardless of
    /// whether the parse actually changed the committed value.</returns>
    public bool CommitBuffer()
    {
        if (_buffer.IsEmpty)
        {
            if (AllowNull)
            {
                _ = CommitValue(null);
            }
        }
        else if (_buffer.TryCommit(out var parsed))
        {
            var rounded = _resolveCommitRounding(parsed);
            _ = CommitValue(ClampToRange(rounded));
        }

        _refreshBuffer();
        return true;
    }

    /// <summary>Discards any in-progress transient edit by reloading the buffer from the
    /// still-committed value.</summary>
    /// <returns>Always true; Escape is always handled.</returns>
    public bool RevertBuffer()
    {
        _refreshBuffer();
        return true;
    }

    /// <summary>Commits a candidate value: assigns it through the caller's own SetProperty-backed
    /// field, refreshes the buffer while focused, and raises the caller's typed ValueChanged event
    /// on an actual transition.</summary>
    /// <param name="candidate">The already clamped and rounded replacement value, or null.</param>
    /// <returns>True when the candidate differed from the previously committed value.</returns>
    public bool CommitValue(decimal? candidate)
    {
        _verifyMutable();
        var previous = Value;
        var priorVersion = _commitVersion;
        var version = ++_commitVersion;

        if (Value == candidate)
        {
            if (_commitVersion == version)
            {
                _commitVersion = priorVersion;
            }

            return false;
        }

        Value = candidate;
        _notifyPropertyChanged(nameof(Controls.Input.NumberInput.Value), InvalidationImpact.Render);

        if (_isFocused())
        {
            _refreshBuffer();
        }

        if (_commitVersion == version && Value == candidate)
        {
            _raiseValueChanged(previous, candidate);
        }

        return true;
    }

    /// <summary>Re-clamps an already-committed value after Minimum or Maximum tightens, committing
    /// the clamped result only when it actually moved.</summary>
    public void RepairValue()
    {
        if (Value is { } current)
        {
            var clamped = ClampToRange(current);

            if (clamped != current)
            {
                _ = CommitValue(clamped);
            }
        }
    }

    /// <summary>Clamps a value into the caller's current Minimum/Maximum.</summary>
    [Pure]
    public decimal ClampToRange(decimal value) => value.Clamp(_minimum, _maximum);

    /// <summary>Applies one step increment or decrement from the currently committed value (or zero
    /// when unset), rounds and clamps the result through the caller's own commit-rounding formula
    /// and range, and commits it.</summary>
    /// <param name="direction">A positive value steps up by one Step increment; a negative value
    /// steps down.</param>
    /// <returns>Always true; a step key is always handled.</returns>
    public bool ApplyStep(int direction)
    {
        var baseline = Value ?? 0m;
        decimal stepped;

        try
        {
            stepped = baseline + (Step * direction);
        }
        catch (OverflowException)
        {
            stepped = direction > 0 ? decimal.MaxValue : decimal.MinValue;
        }

        var next = ClampToRange(_resolveCommitRounding(stepped));
        _ = CommitValue(next);
        return true;
    }

    /// <summary>Rounds at an accepted non-negative precision without forwarding values above
    /// Decimal's 28-digit limit to <see cref="Math.Round(decimal, int, MidpointRounding)"/>.</summary>
    [Pure]
    public static decimal RoundAtAcceptedPrecision(decimal value, int places, MidpointRounding mode) =>
        places > 28 ? value : Math.Round(value, places, mode);

    /// <summary>Bounds formatting precision to the fractional digits Decimal can represent.</summary>
    [Pure]
    public static int RepresentableDecimalPlaces(int places) => Math.Min(places, 28);

    /// <summary>Commits the caller's Minimum or Maximum directly, rounded toward the interior of the
    /// range at the given precision so a bound configured with more precision than the caller's
    /// display allows still commits a value the control's own typing path can reach.</summary>
    /// <param name="minimum">True to jump to Minimum; false to jump to Maximum.</param>
    /// <param name="places">The decimal places the caller's commit rounding uses - the control-
    /// specific formula (<c>DecimalPlaces</c> or an integer-mode override for
    /// <see cref="Controls.Input.NumberInput"/>, culture-derived <c>EffectiveDecimalPlaces</c> for
    /// <see cref="Controls.Input.CurrencyInput"/>) that the caller resolves and passes in.</param>
    /// <returns>Always true; Home and End are always handled.</returns>
    public bool JumpToBound(bool minimum, int places)
    {
        var target = minimum ? _minimum : _maximum;
        target = ClampToRange(RoundTowardRangeInterior(target, places, minimum));
        _ = CommitValue(target);
        return true;
    }

    // Rounds toward the interior of [Minimum, Maximum] - ceiling for the lower bound, floor for
    // the upper - so a bound configured with more precision than the caller's own decimal-places
    // formula allows still commits a value the control's own typing path can reach. Rounding the
    // bound naively (via the caller's rounding mode) and then clamping back into range would be
    // self-defeating for Minimum specifically: ClampToRange's own floor is that same unrounded
    // Minimum, so a rounded value below it gets pushed straight back, undoing the rounding. The
    // trailing ClampToRange in JumpToBound only guards the degenerate case where the range itself is
    // narrower than one unit at this precision (e.g. Minimum=0.4, Maximum=0.6 at 0 decimal places) -
    // safe here because it clamps against the OTHER bound, never the one this method just rounded
    // toward.
    private static decimal RoundTowardRangeInterior(decimal value, int places, bool isMinimum)
    {
        if (places <= 0)
        {
            return isMinimum ? Math.Ceiling(value) : Math.Floor(value);
        }

        decimal scale;
        decimal scaled;

        try
        {
            scale = 1m;

            for (var index = 0; index < places; index++)
            {
                scale *= 10m;
            }

            scaled = value * scale;
        }
        catch (OverflowException)
        {
            // Home and End reach here with the unbounded decimal.MinValue/MaxValue defaults for
            // Minimum/Maximum, or with a caller-supplied places beyond the 28 significant digits
            // Decimal itself can represent: isolating whole units at the requested precision would
            // need more magnitude than Decimal holds, either while building the scaling power of
            // ten itself or while applying it to the bound. Such a bound already carries no
            // fractional component finer than Decimal's own range, so it stands unrounded rather
            // than crashing the jump a keypress is not expected to ever fail.
            return value;
        }

        scaled = isMinimum ? Math.Ceiling(scaled) : Math.Floor(scaled);
        return scaled / scale;
    }

    private bool SetBound(ref decimal field, decimal value, string propertyName)
    {
        _verifyMutable();

        if (field == value)
        {
            return false;
        }

        field = value;
        ExceptionDispatchInfo? failure = null;
        ExceptionAggregation.Capture(
            () => _notifyPropertyChanged(propertyName, InvalidationImpact.Measure),
            ref failure);
        ExceptionAggregation.Capture(RepairValue, ref failure);
        failure?.Throw();
        return true;
    }

    private void RepairNullPolicy()
    {
        if (!AllowNull && Value is null)
        {
            _ = CommitValue(ClampToRange(0m));
        }
    }
}
