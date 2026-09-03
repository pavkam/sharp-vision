// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using NonNegativeValue = JetBrains.Annotations.NonNegativeValueAttribute;

/// <summary>Edits an integer or decimal value through a transient typed buffer committed on Enter
/// or focus loss.</summary>
/// <remarks>
/// Unlike the segmented temporal fields (<see cref="DateInput"/>, <see cref="TimeInput"/>,
/// <see cref="DateTimeInput"/>), which commit <see cref="Value"/> on every keystroke,
/// <see cref="NumberInput"/> edits a transient text buffer while typing and only parses and
/// commits it on Enter or when focus leaves the control. <see cref="NumericEditBehavior"/> owns
/// the shared routed editing lifecycle, while <see cref="NumericInputCommitCoordinator"/> owns the
/// nullable value and range state. Up/Down and Home/End bypass the buffer and commit immediately,
/// matching <see cref="Slider"/>.
/// </remarks>
[PublicAPI]
public sealed class NumberInput: InputBase
{
    private readonly NumericEditBuffer _buffer = new();
    private readonly NumericInputCommitCoordinator _coordinator;

    /// <summary>Initializes a focusable number field with no committed value.</summary>
    public NumberInput()
    {
        _coordinator = new NumericInputCommitCoordinator(
            _buffer,
            VerifyMutable,
            NotifyPropertyChanged,
            ResolveCommitRounding,
            () => IsFocused,
            RefreshBuffer,
            (previous, candidate) => ValueChanged?.Invoke(this, new NumberInputValueChangedEventArgs(previous, candidate)));
        EnableNumericEditing(
            _buffer,
            _coordinator,
            ConfigureBuffer,
            () => Mode == NumberInputMode.Integer ? 0 : DecimalPlaces,
            ResolveCaretIndex);
    }

    /// <summary>Raised after a committed value transition.</summary>
    public event EventHandler<NumberInputValueChangedEventArgs>? ValueChanged;

    /// <summary>Gets or sets the current value, or null when cleared. Assignment clamps silently
    /// into <see cref="Minimum"/> and <see cref="Maximum"/>; a null assignment is a no-op unless
    /// <see cref="AllowNull"/> is set.</summary>
    /// <exception cref="ArgumentException">The value has a fractional component while <see cref="Mode"/> is <see cref="NumberInputMode.Integer"/>.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public decimal? Value
    {
        get => _coordinator.Value;
        set
        {
            VerifyMutable();

            if (!value.HasValue)
            {
                _ = _coordinator.SetValue(null);
                return;
            }

            if (Mode == NumberInputMode.Integer && value.Value != decimal.Truncate(value.Value))
            {
                throw new ArgumentException(
                    "A fractional value cannot be assigned while Mode is Integer.",
                    nameof(value));
            }

            _ = _coordinator.SetValue(value.Value);
        }
    }

    /// <summary>Gets or sets whether the value may be cleared to null. Default is true.</summary>
    /// <remarks>Disabling this while the value is already null eagerly reseeds it to zero, clamped
    /// into <see cref="Minimum"/> and <see cref="Maximum"/>, raising <see cref="ValueChanged"/> - the
    /// deterministic numeric analog of the temporal input family's eager reseed to the current
    /// clock.</remarks>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool AllowNull
    {
        get => _coordinator.AllowNull;
        set => _ = _coordinator.SetAllowNull(value);
    }

    /// <summary>Gets or sets the inclusive lower bound. Default is <see cref="decimal.MinValue"/>.</summary>
    /// <remarks>Endpoints may be equal.</remarks>
    /// <exception cref="ArgumentException">The minimum exceeds <see cref="Maximum"/>.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public decimal Minimum
    {
        get => _coordinator.Minimum;
        set => _ = _coordinator.SetMinimum(value);
    }

    /// <summary>Gets or sets the inclusive upper bound. Default is <see cref="decimal.MaxValue"/>.</summary>
    /// <remarks>Endpoints may be equal.</remarks>
    /// <exception cref="ArgumentException">The maximum is below <see cref="Minimum"/>.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public decimal Maximum
    {
        get => _coordinator.Maximum;
        set => _ = _coordinator.SetMaximum(value);
    }

    /// <summary>Gets or sets the positive increment Up and Down apply, and the jump Home and End
    /// commit to <see cref="Minimum"/> and <see cref="Maximum"/> land on directly. Default is
    /// <c>1</c>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero or negative.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public decimal Step
    {
        get => _coordinator.Step;
        set => _ = _coordinator.SetStep(value);
    }

    /// <summary>Gets or sets whether editing is restricted to whole numbers. Default is
    /// <see cref="NumberInputMode.Decimal"/>.</summary>
    /// <remarks>
    /// Switching to <see cref="NumberInputMode.Integer"/> while a fractional value is already
    /// committed repairs it by rounding to zero places with <see cref="RoundingMode"/>, raising
    /// <see cref="ValueChanged"/> - the same bounds-repair philosophy <see cref="Minimum"/> and
    /// <see cref="Maximum"/> already apply. Switching modes mid-edit also discards any in-progress
    /// transient buffer back to the committed value's formatting under the new mode; no half-parsed
    /// state migrates across the switch.
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

    /// <summary>Gets or sets whether the idle and freshly focused display groups digits under
    /// <see cref="Culture"/>. Purely a display concern: a typed or pasted group separator is always
    /// accepted and stripped while parsing, regardless of this setting. Default is true.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool AllowGrouping
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.Measure);
    } = true;

    /// <summary>Gets or sets the rounding applied when a typed value commits. Accepted precision
    /// above Decimal's 28-digit rounding limit preserves the already-representable value rather
    /// than forwarding an invalid digit count to <see cref="Math.Round(decimal, int, MidpointRounding)"/>. Default is
    /// <see cref="MidpointRounding.AwayFromZero"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public MidpointRounding RoundingMode
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNotDefined(value, nameof(value), "The rounding mode is unknown.");
            _ = SetProperty(ref field, value, InvalidationImpact.None);
        }
    } = MidpointRounding.AwayFromZero;

    /// <summary>Gets or sets the culture whose decimal separator, group separator, sign, and digit
    /// grouping govern display and parsing. Default is <see cref="CultureInfo.InvariantCulture"/>,
    /// unlike <see cref="DateInput.Culture"/>, so out-of-the-box rendering never depends on the host
    /// operating system's locale.</summary>
    /// <remarks>Changing this mid-edit discards any in-progress transient buffer back to the
    /// committed value's formatting under the new culture; no half-parsed state migrates across the
    /// switch.</remarks>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public CultureInfo Culture
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (!SetPropertyWithComparer(
                ref field,
                value,
                InvalidationImpact.Measure,
                ReferenceEqualityComparer.Instance))
            {
                return;
            }

            if (IsFocused)
            {
                RefreshBuffer();
            }
        }
    } = CultureInfo.InvariantCulture;

    /// <summary>Gets or sets optional hint text shown while the value and transient edit buffer are
    /// empty.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public string? Placeholder
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.Render);
    }

    /// <summary>Gets or sets the protocol-neutral cursor shape requested while this field has
    /// focus.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public CursorShape CursorShape
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNotDefined(value, nameof(value), "The cursor shape is unknown.");
            _ = SetProperty(ref field, value, InvalidationImpact.Render);
        }
    }

    #region Layout

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;
        var minimumText = FormatValue(Minimum);
        var maximumText = FormatValue(Maximum);
        var widest = minimumText.Length >= maximumText.Length ? minimumText : maximumText;
        var affixes = MeasureAffixes(StartAffix, EndAffix, ResolveAffixGap());

        // Reserve one cell beyond the widest formatted bound for the end-of-buffer caret. The
        // caret only paints inside the value box, so without the reservation an auto-sized field
        // whose committed value is as wide as its widest bound hides the cursor the moment the
        // caret rests past the last digit - exactly where every focus gain and commit places it.
        return new Size(MeasureCells(widest) + 1 + affixes.StartCells + affixes.EndCells, 1);
    }

    #endregion

    #region Commit and buffer synchronization

    /// <summary>Resolves the decimal places and rounding policy a freshly parsed buffer value
    /// commits under, for <see cref="NumericInputCommitCoordinator"/>.</summary>
    [Pure]
    private decimal ResolveCommitRounding(decimal parsed)
    {
        var places = Mode == NumberInputMode.Integer ? 0 : DecimalPlaces;
        return NumericInputCommitCoordinator.RoundAtAcceptedPrecision(
            Mode == NumberInputMode.Integer ? decimal.Truncate(parsed) : parsed,
            places,
            RoundingMode);
    }

    private void RefreshBuffer()
    {
        ConfigureBuffer();
        _buffer.Load(Value is { } value ? FormatValue(value) : string.Empty);
    }

    private void ConfigureBuffer() =>
        _buffer.Configure(Culture.NumberFormat, Mode == NumberInputMode.Integer);

    private int ResolveCaretIndex(Point cells)
    {
        var content = ContentBounds;
        var valueBox = DeflateForAffixes(content, MeasureAffixes(StartAffix, EndAffix, ResolveAffixGap()));
        return _buffer.IndexAtColumn(cells.X - valueBox.X, CellPolicy.AmbiguousWidth);
    }

    [Pure]
    private string FormatValue(decimal value)
    {
        var places = Mode == NumberInputMode.Integer ? 0 : DecimalPlaces;
        var displayPlaces = NumericInputCommitCoordinator.RepresentableDecimalPlaces(places);
        var specifier = (AllowGrouping ? "N" : "F") + displayPlaces.ToString(CultureInfo.InvariantCulture);
        return value.ToString(specifier, Culture);
    }

    #endregion

    #region Rendering

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        var displayText = IsFocused ? _buffer.Text : Value is { } value ? FormatValue(value) : string.Empty;
        RenderNumericInputContent(
            canvas,
            displayText,
            IsFocused ? _buffer.Selection : default,
            IsFocused ? _buffer.Selection.Caret : 0,
            StartAffix,
            EndAffix,
            Placeholder,
            CursorShape);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Canvas.CopyFromPrevious already restored this control's affixes and value-text cells;
    /// <see cref="TerminalCanvas.SetCursor(Point, bool, CursorShape)"/> is the one thing a cell copy can never replay, since
    /// cursor placement lives outside the frame's cell arena exactly like Image's DrawImage
    /// placement. An unset render bit already proves the buffer text and caret are unchanged since
    /// the last real paint, so recomputing the caret column from this control's own CURRENT state
    /// here is provably identical to what that paint recorded.
    /// </remarks>
    internal override void OnReuseCleanRender(TerminalCanvas canvas)
    {
        ReplayNumericInputCursor(
            canvas,
            _buffer.Text,
            _buffer.Selection.Caret,
            StartAffix,
            EndAffix,
            CursorShape);
    }

    #endregion

    #region Lifecycle

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);

        if (reason == ReleaseReason.Disposed)
        {
            ValueChanged = null;
        }
    }

    #endregion
}
