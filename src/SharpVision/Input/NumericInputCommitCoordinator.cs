// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

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
/// Deliberately does not own the committed value, the invalidation plumbing that guards it, or the
/// caller's typed <c>ValueChanged</c> event: those stay with the owning control, the only party that
/// can call its own <c>SetProperty</c>-backed field and raise its own event-args type. Every one of
/// those control-owned steps is threaded through as a constructor delegate instead of assumed here,
/// alongside <see cref="Controls.Input.NumberInput.Minimum"/>/<see cref="Controls.Input.NumberInput.Maximum"/>/<see cref="Controls.Input.NumberInput.Step"/>
/// themselves and the one formula - how a parsed buffer rounds - that differs between
/// <see cref="Controls.Input.NumberInput"/> and <see cref="Controls.Input.CurrencyInput"/> today
/// (integer-aware truncation plus a fixed <c>DecimalPlaces</c> for the former, a culture-derived
/// <c>EffectiveDecimalPlaces</c> for the latter). Clamping and the step/jump/rounding-toward-range
/// arithmetic built on top of it are otherwise identical between the two controls, so this type owns
/// them outright rather than taking them as delegates.
/// </para>
/// <para>
/// Key dispatch (recognizing Home/End/Up/Down and the buffer's own editing keys) deliberately stays
/// on each control instead of moving here: it needs the protected static step-delta helper and the
/// protected invalidation call both controls inherit from their shared base, neither of which this
/// internal type - living outside that inheritance chain - can reach. Only the resulting
/// step/jump/commit/revert computations route through this type.
/// </para>
/// </remarks>
internal sealed class NumericInputCommitCoordinator
{
    private readonly NumericEditBuffer _buffer;
    private readonly Func<decimal?> _getValue;
    private readonly Func<decimal?, bool> _trySetValue;
    private readonly Func<decimal> _getMinimum;
    private readonly Func<decimal> _getMaximum;
    private readonly Func<decimal> _getStep;
    private readonly Func<decimal, decimal> _resolveCommitRounding;
    private readonly Func<bool> _isAllowNull;
    private readonly Func<bool> _isFocused;
    private readonly Action _refreshBuffer;
    private readonly Action<decimal?, decimal?> _raiseValueChanged;

    /// <summary>Initializes a coordinator bound to one control's buffer and control-specific
    /// data and callbacks.</summary>
    /// <param name="buffer">The shared transient edit buffer this control owns.</param>
    /// <param name="getValue">Reads the currently committed value.</param>
    /// <param name="trySetValue">Assigns a candidate through the caller's own SetProperty-backed
    /// field, returning whether it actually changed.</param>
    /// <param name="getMinimum">Reads the caller's current inclusive lower bound.</param>
    /// <param name="getMaximum">Reads the caller's current inclusive upper bound.</param>
    /// <param name="getStep">Reads the caller's current positive step increment.</param>
    /// <param name="resolveCommitRounding">Resolves the decimal places and rounding policy a freshly
    /// parsed buffer value commits under.</param>
    /// <param name="isAllowNull">Reads whether an empty buffer may commit null.</param>
    /// <param name="isFocused">Reads whether the caller currently holds focus.</param>
    /// <param name="refreshBuffer">Reloads the buffer from the currently committed value.</param>
    /// <param name="raiseValueChanged">Raises the caller's own typed ValueChanged event with the
    /// previous and new value.</param>
    /// <exception cref="ArgumentNullException">Any parameter is null.</exception>
    public NumericInputCommitCoordinator(
        NumericEditBuffer buffer,
        Func<decimal?> getValue,
        Func<decimal?, bool> trySetValue,
        Func<decimal> getMinimum,
        Func<decimal> getMaximum,
        Func<decimal> getStep,
        Func<decimal, decimal> resolveCommitRounding,
        Func<bool> isAllowNull,
        Func<bool> isFocused,
        Action refreshBuffer,
        Action<decimal?, decimal?> raiseValueChanged)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentNullException.ThrowIfNull(getValue);
        ArgumentNullException.ThrowIfNull(trySetValue);
        ArgumentNullException.ThrowIfNull(getMinimum);
        ArgumentNullException.ThrowIfNull(getMaximum);
        ArgumentNullException.ThrowIfNull(getStep);
        ArgumentNullException.ThrowIfNull(resolveCommitRounding);
        ArgumentNullException.ThrowIfNull(isAllowNull);
        ArgumentNullException.ThrowIfNull(isFocused);
        ArgumentNullException.ThrowIfNull(refreshBuffer);
        ArgumentNullException.ThrowIfNull(raiseValueChanged);

        _buffer = buffer;
        _getValue = getValue;
        _trySetValue = trySetValue;
        _getMinimum = getMinimum;
        _getMaximum = getMaximum;
        _getStep = getStep;
        _resolveCommitRounding = resolveCommitRounding;
        _isAllowNull = isAllowNull;
        _isFocused = isFocused;
        _refreshBuffer = refreshBuffer;
        _raiseValueChanged = raiseValueChanged;
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
            if (_isAllowNull())
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
        var previous = _getValue();

        if (!_trySetValue(candidate))
        {
            return false;
        }

        if (_isFocused())
        {
            _refreshBuffer();
        }

        _raiseValueChanged(previous, candidate);
        return true;
    }

    /// <summary>Re-clamps an already-committed value after Minimum or Maximum tightens, committing
    /// the clamped result only when it actually moved.</summary>
    public void RepairValue()
    {
        if (_getValue() is { } current)
        {
            var clamped = ClampToRange(current);

            if (clamped != current)
            {
                _ = CommitValue(clamped);
            }
        }
    }

    /// <summary>Clamps a value into the caller's current Minimum/Maximum.</summary>
    public decimal ClampToRange(decimal value) => value.Clamp(_getMinimum(), _getMaximum());

    /// <summary>Applies one step increment or decrement from the currently committed value (or zero
    /// when unset), rounds and clamps the result through the caller's own commit-rounding formula
    /// and range, and commits it.</summary>
    /// <param name="direction">A positive value steps up by one Step increment; a negative value
    /// steps down.</param>
    /// <returns>Always true; a step key is always handled.</returns>
    public bool ApplyStep(int direction)
    {
        var baseline = _getValue() ?? 0m;
        decimal stepped;

        try
        {
            stepped = baseline + (_getStep() * direction);
        }
        catch (OverflowException)
        {
            stepped = direction > 0 ? decimal.MaxValue : decimal.MinValue;
        }

        var next = ClampToRange(_resolveCommitRounding(stepped));
        _ = CommitValue(next);
        return true;
    }

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
        var target = minimum ? _getMinimum() : _getMaximum();
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
}
