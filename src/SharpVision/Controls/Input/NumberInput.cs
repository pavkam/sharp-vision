// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using SharpVision.Terminal.Input;

using NonNegativeValue = JetBrains.Annotations.NonNegativeValueAttribute;

/// <summary>Edits an integer or decimal value through a transient typed buffer committed on Enter
/// or focus loss.</summary>
/// <remarks>
/// Unlike the segmented temporal fields (<see cref="DateInput"/>, <see cref="TimeInput"/>,
/// <see cref="DateTimeInput"/>), which commit <see cref="Value"/> on every keystroke,
/// <see cref="NumberInput"/> edits a transient text buffer while typing and only parses and
/// commits it on Enter or when focus leaves the control - the shared editing primitive is
/// <see cref="NumericEditBuffer"/>. Up/Down and Home/End bypass the buffer entirely and commit
/// immediately, matching <see cref="Slider"/>.
/// </remarks>
[PublicAPI]
public sealed class NumberInput: InputBase
{
    /// <inheritdoc/>
    protected override AppearanceStates GetDefaultAppearanceStates(Theme? theme) =>
        (theme ?? ThemeCatalog.Dark).GetStyleSet(InputStyle.Default).ToAppearanceStates();

    private readonly NumericEditBuffer _buffer = new();
    private readonly NumericInputCommitCoordinator _coordinator;
    private decimal? _value;

    /// <summary>Initializes a focusable number field with no committed value.</summary>
    public NumberInput()
    {
        TabNavigation = TabNavigation.None;
        _coordinator = new NumericInputCommitCoordinator(
            _buffer,
            () => _value,
            candidate => SetProperty(ref _value, candidate, InvalidationImpact.Render, nameof(Value)),
            () => Minimum,
            () => Maximum,
            () => Step,
            ResolveCommitRounding,
            () => AllowNull,
            () => IsFocused,
            RefreshBuffer,
            (previous, candidate) => ValueChanged?.Invoke(this, new NumberInputValueChangedEventArgs(previous, candidate)));
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
        get => _value;
        set
        {
            VerifyMutable();

            if (!value.HasValue)
            {
                if (AllowNull)
                {
                    _ = _coordinator.CommitValue(null);
                }

                return;
            }

            if (Mode == NumberInputMode.Integer && value.Value != decimal.Truncate(value.Value))
            {
                throw new ArgumentException(
                    "A fractional value cannot be assigned while Mode is Integer.",
                    nameof(value));
            }

            _ = _coordinator.CommitValue(_coordinator.ClampToRange(value.Value));
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
        get;
        set
        {
            if (SetProperty(ref field, value, InvalidationImpact.None) && !value && _value is null)
            {
                _ = _coordinator.CommitValue(_coordinator.ClampToRange(0m));
            }
        }
    } = true;

    /// <summary>Gets or sets the inclusive lower bound. Default is <see cref="decimal.MinValue"/>.</summary>
    /// <remarks>Endpoints may be equal.</remarks>
    /// <exception cref="ArgumentException">The minimum exceeds <see cref="Maximum"/>.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public decimal Minimum
    {
        get;
        set
        {
            ArgumentException.ThrowIfAboveMaximum(value, Maximum, nameof(value), "Minimum cannot exceed Maximum.");

            if (SetProperty(ref field, value, InvalidationImpact.Measure))
            {
                _coordinator.RepairValue();
            }
        }
    } = decimal.MinValue;

    /// <summary>Gets or sets the inclusive upper bound. Default is <see cref="decimal.MaxValue"/>.</summary>
    /// <remarks>Endpoints may be equal.</remarks>
    /// <exception cref="ArgumentException">The maximum is below <see cref="Minimum"/>.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public decimal Maximum
    {
        get;
        set
        {
            ArgumentException.ThrowIfBelowMinimum(value, Minimum, nameof(value), "Maximum cannot be less than Minimum.");

            if (SetProperty(ref field, value, InvalidationImpact.Measure))
            {
                _coordinator.RepairValue();
            }
        }
    } = decimal.MaxValue;

    /// <summary>Gets or sets the positive increment Up and Down apply, and the jump Home and End
    /// commit to <see cref="Minimum"/> and <see cref="Maximum"/> land on directly. Default is
    /// <c>1</c>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero or negative.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public decimal Step
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNotAPositiveStep(value, nameof(value));

            _ = SetProperty(ref field, value, InvalidationImpact.None);
        }
    } = 1m;

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

            if (!SetProperty(ref field, value, InvalidationImpact.Measure))
            {
                return;
            }

            if (value == NumberInputMode.Integer &&
                _value is { } current &&
                current != decimal.Truncate(current))
            {
                _ = _coordinator.CommitValue(_coordinator.ClampToRange(Math.Round(current, 0, RoundingMode)));
            }

            if (IsFocused)
            {
                RefreshBuffer();
            }
        }
    } = NumberInputMode.Decimal;

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

    /// <summary>Gets or sets the rounding applied when a typed value commits, using the three-argument
    /// <see cref="Math.Round(decimal, int, MidpointRounding)"/> overload exclusively - never the
    /// two-argument overload, which silently rounds to even. Default is
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

            if (!SetProperty(ref field, value, InvalidationImpact.Measure))
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

        // Keeps the buffer's separator/sign tokens and integer-only policy in lockstep with the
        // current Mode and Culture even if a caller drives keyboard input without ever having
        // routed an actual focus transition through this control - RefreshBuffer's own Configure
        // call (on focus gain, commit, or a mid-edit Mode/Culture change) makes this redundant on
        // the ordinary focused path, but costs nothing to repeat here.
        _buffer.Configure(Culture.NumberFormat, Mode == NumberInputMode.Integer);

        switch (eventArgs)
        {
            case KeyEventArgs key:
                HandleKey(key);
                break;
            case TextEventArgs text:
                _ = _buffer.Insert(text.Text.Value.ToString());
                text.IsHandled = true;
                Invalidate(InvalidationImpact.Render);
                break;
            case PasteEventArgs paste:
                _ = _buffer.Insert(Encoding.UTF8.GetString(paste.Paste.Utf8.Span));
                paste.IsHandled = true;
                Invalidate(InvalidationImpact.Render);
                break;
            case PointerEventArgs pointer:
                HandlePointer(pointer);
                break;
            default:
                break;
        }

        if (!eventArgs.IsHandled)
        {
            base.OnEvent(eventArgs);
        }
    }

    private void HandleKey(KeyEventArgs eventArgs)
    {
        var stroke = eventArgs.Stroke;

        if (stroke.Action is not (KeyAction.Press or KeyAction.Repeat))
        {
            return;
        }

        if (TryGetStepDelta(eventArgs, out var delta))
        {
            _ = _coordinator.ApplyStep(delta);
            eventArgs.IsHandled = true;
            return;
        }

        var places = Mode == NumberInputMode.Integer ? 0 : DecimalPlaces;

#pragma warning disable IDE0072 // Every unmatched key intentionally remains unhandled.
        var handled = stroke.Code switch
        {
            Code.Home => _coordinator.JumpToBound(minimum: true, places),
            Code.End => _coordinator.JumpToBound(minimum: false, places),
            Code.Enter => _coordinator.CommitBuffer(),
            Code.Escape => _coordinator.RevertBuffer(),
            Code.Backspace => _buffer.Backspace(),
            Code.Delete => _buffer.Delete(),
            Code.Left => _buffer.MovePrevious(extend: false),
            Code.Right => _buffer.MoveNext(extend: false),
            Code.Character when stroke.Character is { } ch => _buffer.Insert(ch.ToString()),
            _ => false
        };
#pragma warning restore IDE0072

        if (handled)
        {
            eventArgs.IsHandled = true;
            Invalidate(InvalidationImpact.Render);
        }
    }

    private void HandlePointer(PointerEventArgs eventArgs)
    {
        var pointer = eventArgs.Pointer;

        if (pointer.Action != PointerAction.Press || (pointer.Buttons & Buttons.Primary) == 0)
        {
            return;
        }

        if (pointer.Cells is not { } cells)
        {
            return;
        }

        var content = ContentBounds;

        if (!content.Contains(cells))
        {
            return;
        }

        if (!IsFocused)
        {
            _ = RequestFocus();
        }

        // A click that lands on an affix column (outside the value's own deflated box) still
        // focuses the control; IndexAtColumn clamps a negative or overflowing local column to the
        // nearest valid caret boundary, so no separate affix-column rejection is needed here.
        var valueBox = DeflateForAffixes(content, MeasureAffixes(StartAffix, EndAffix, ResolveAffixGap()));
        var localX = cells.X - valueBox.X;
        _buffer.SetCaret(_buffer.IndexAtColumn(localX, CellPolicy.AmbiguousWidth));
        Invalidate(InvalidationImpact.Render);
        eventArgs.IsHandled = true;
    }

    #endregion

    #region Commit and buffer synchronization

    /// <summary>Resolves the decimal places and rounding policy a freshly parsed buffer value
    /// commits under, for <see cref="NumericInputCommitCoordinator"/>.</summary>
    [Pure]
    private decimal ResolveCommitRounding(decimal parsed)
    {
        var places = Mode == NumberInputMode.Integer ? 0 : DecimalPlaces;
        return Math.Round(
            Mode == NumberInputMode.Integer ? decimal.Truncate(parsed) : parsed,
            places,
            RoundingMode);
    }

    private void RefreshBuffer()
    {
        _buffer.Configure(Culture.NumberFormat, Mode == NumberInputMode.Integer);
        _buffer.Load(_value is { } value ? FormatValue(value) : string.Empty);
    }

    [Pure]
    private string FormatValue(decimal value)
    {
        var places = Mode == NumberInputMode.Integer ? 0 : DecimalPlaces;
        var specifier = (AllowGrouping ? "N" : "F") + places.ToString(CultureInfo.InvariantCulture);
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
        var displayText = IsFocused ? _buffer.Text : _value is { } value ? FormatValue(value) : string.Empty;
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

        if (focused)
        {
            RefreshBuffer();
        }
        else
        {
            _ = _coordinator.CommitBuffer();
        }

        Invalidate(InvalidationImpact.Render);
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
