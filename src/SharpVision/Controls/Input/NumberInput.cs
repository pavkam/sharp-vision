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
    private readonly NumericEditBehavior _editing;

    /// <summary>Initializes a focusable number field with no committed value.</summary>
    public NumberInput()
    {
        TabNavigation = TabNavigation.None;
        _coordinator = new NumericInputCommitCoordinator(
            _buffer,
            VerifyMutable,
            NotifyPropertyChanged,
            ResolveCommitRounding,
            () => IsFocused,
            RefreshBuffer,
            (previous, candidate) => ValueChanged?.Invoke(this, new NumberInputValueChangedEventArgs(previous, candidate)));
#pragma warning disable IDE0200 // A method group would capture the construction-time ContentBounds value.
        _editing = new NumericEditBehavior(
            _buffer,
            _coordinator,
            ConfigureBuffer,
            () => Mode == NumberInputMode.Integer ? 0 : DecimalPlaces,
            () => IsFocused,
            point => ContentBounds.Contains(point),
            RequestEditingFocus,
            ResolveCaretIndex,
            () => Invalidate(InvalidationImpact.Render));
#pragma warning restore IDE0200
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

    #region Layout

    /// <summary>Gets or sets the optional leading edge-pinned decoration, reserved inboard of the
    /// border and outboard of the value's own text.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Affix? StartAffix
    {
        get;
        set => _ = SetProperty(ref field, value, GetAffixChangeImpact(field, value));
    }

    /// <summary>Gets or sets the optional trailing edge-pinned decoration, reserved inboard of the
    /// border and outboard of the value's own text.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Affix? EndAffix
    {
        get;
        set => _ = SetProperty(ref field, value, GetAffixChangeImpact(field, value));
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;
        var minimumText = FormatValue(Minimum);
        var maximumText = FormatValue(Maximum);
        var widest = minimumText.Length >= maximumText.Length ? minimumText : maximumText;
        var affixes = MeasureAffixes(StartAffix, EndAffix, ResolveAffixGap());
        return new Size(MeasureCells(widest) + affixes.StartCells + affixes.EndCells, 1);
    }

    #endregion

    #region Input

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);

        if (!EffectiveIsEnabled || !EffectiveIsVisible)
        {
            base.OnEvent(eventArgs);
            return;
        }

        if (!_editing.HandleEvent(eventArgs))
        {
            base.OnEvent(eventArgs);
        }
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

    private bool RequestEditingFocus()
    {
        var dispatcher = Dispatcher;
        _ = RequestFocus();
        return CanContinueAfterFocus(dispatcher);
    }

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
        var content = ContentBounds;

        if (content.Width == 0 || content.Height == 0)
        {
            return;
        }

        var style = ResolvedStyle;
        var affixes = MeasureAffixes(StartAffix, EndAffix, ResolveAffixGap());

        // Affixes render against the undeflated content box - not the value box below - so a
        // present affix keeps sitting at the true edge even when the value's own box saturates to
        // zero width.
        RenderAffixes(canvas, content, affixes, StartAffix, EndAffix, style);

        var valueBox = DeflateForAffixes(content, affixes);
        var displayText = IsFocused ? _buffer.Text : Value is { } value ? FormatValue(value) : string.Empty;
        var clipped = canvas.Clip(new Rect(valueBox.X, valueBox.Y, valueBox.Width, 1));
        _ = clipped.Draw(displayText.AsSpan(), new Point(valueBox.X, valueBox.Y), style, background: BackgroundMode.Transparent);

        if (!IsFocused)
        {
            return;
        }

        var caretColumn = MeasureCells(displayText.AsSpan(0, Math.Min(_buffer.Selection.Caret, displayText.Length)));
        var position = new Point(valueBox.X + caretColumn, valueBox.Y);

        if (valueBox.Contains(position) && canvas.Bounds.Contains(position))
        {
            canvas.SetCursor(position, visible: true, CursorShape.Block);
        }
    }

    #endregion

    #region Lifecycle

    /// <inheritdoc/>
    protected override void OnFocusChanged(bool focused)
    {
        base.OnFocusChanged(focused);

        _editing.FocusChanged(focused);
    }

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
