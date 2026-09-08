// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using NonNegativeValue = JetBrains.Annotations.NonNegativeValueAttribute;

/// <summary>Edits an integer or decimal value through a transient typed buffer committed on Enter
/// or focus loss.</summary>
/// <remarks>
/// Unlike the segmented temporal fields (<see cref="DateInput"/>, <see cref="TimeInput"/>,
/// <see cref="DateTimeInput"/>), which commit <see cref="NumericInputBase.Value"/> on every keystroke,
/// <see cref="NumberInput"/> edits a transient text buffer while typing and only parses and
/// commits it on Enter or when focus leaves the control. <see cref="NumericInputBase"/> owns
/// the shared routed editing lifecycle, nullable value, and range state; this type owns only its
/// integer/decimal mode policy and formatting. Up/Down and Home/End bypass the buffer and commit
/// immediately, matching <see cref="Slider"/>.
/// </remarks>
[PublicAPI]
public sealed class NumberInput: NumericInputBase
{
    /// <summary>Initializes a focusable number field with no committed value.</summary>
    public NumberInput()
    {
    }

    /// <summary>Gets or sets whether editing is restricted to whole numbers. Default is
    /// <see cref="NumberInputMode.Decimal"/>.</summary>
    /// <remarks>
    /// Switching to <see cref="NumberInputMode.Integer"/> while a fractional value is already
    /// committed repairs it by rounding to zero places with <see cref="NumericInputBase.RoundingMode"/>, raising
    /// <see cref="NumericInputBase.ValueChanged"/> - the same bounds-repair philosophy
    /// <see cref="NumericInputBase.Minimum"/> and <see cref="NumericInputBase.Maximum"/> already
    /// apply. Switching modes mid-edit also discards any in-progress transient buffer back to the
    /// committed value's formatting under the new mode; no half-parsed state migrates across the
    /// switch.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public NumberInputMode Mode
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNotDefined(value, nameof(value), "The mode is unknown.");

            _ = SetPropertyAndContinue(ref field, value, InvalidationImpact.Measure, ApplyModePolicy);
        }
    } = NumberInputMode.Decimal;

    private void ApplyModePolicy()
    {
        if (Mode == NumberInputMode.Integer &&
            Value is { } current &&
            current != decimal.Truncate(current))
        {
            _ = _coordinator.CommitValue(_coordinator.ClampToRange(Math.Round(current, 0, RoundingMode)));
        }

        if (IsFocused)
        {
            RefreshBuffer();
        }
    }

    /// <summary>Gets or sets the number of fractional digits displayed and accepted while
    /// <see cref="Mode"/> is <see cref="NumberInputMode.Decimal"/>. Treated as zero while
    /// <see cref="Mode"/> is <see cref="NumberInputMode.Integer"/>. Default is <c>2</c>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    [NonNegativeValue]
    public int DecimalPlaces
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
        }
    } = 2;

    #region Numeric editing seams

    /// <inheritdoc/>
    protected override int EffectiveDecimalPlaces => Mode == NumberInputMode.Integer ? 0 : DecimalPlaces;

    /// <inheritdoc/>
    protected override bool IsIntegerOnly => Mode == NumberInputMode.Integer;

    /// <inheritdoc/>
    protected override void ValidateValueAssignment(decimal value)
    {
        if (Mode == NumberInputMode.Integer && value != decimal.Truncate(value))
        {
            throw new ArgumentException(
                "A fractional value cannot be assigned while Mode is Integer.",
                nameof(value));
        }
    }

    /// <inheritdoc/>
    [Pure]
    protected override decimal ResolveCommitRounding(decimal parsed)
    {
        var places = Mode == NumberInputMode.Integer ? 0 : DecimalPlaces;
        return NumericInputCommitCoordinator.RoundAtAcceptedPrecision(
            Mode == NumberInputMode.Integer ? decimal.Truncate(parsed) : parsed,
            places,
            RoundingMode);
    }

    /// <inheritdoc/>
    [Pure]
    protected override NumberFormatInfo BuildBufferFormat() => Culture.NumberFormat;

    /// <inheritdoc/>
    [Pure]
    protected override string FormatValue(decimal value)
    {
        var places = Mode == NumberInputMode.Integer ? 0 : DecimalPlaces;
        var displayPlaces = NumericInputCommitCoordinator.RepresentableDecimalPlaces(places);
        var specifier = (AllowGrouping ? "N" : "F") + displayPlaces.ToString(CultureInfo.InvariantCulture);
        return value.ToString(specifier, Culture);
    }

    #endregion
}
