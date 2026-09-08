// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using SharpVision.Text;

/// <summary>Defines the shared buffer-then-commit editing model, nullable range state, and
/// transient-buffer rendering every decimal field control built on
/// <see cref="InputBase.EnableNumericEditing(NumericEditBuffer, NumericInputCommitCoordinator, Action, Func{int}, Func{Point, int})"/>
/// composes: <see cref="NumberInput"/> and <see cref="CurrencyInput"/> today, and any in-assembly or
/// third-party derivative that needs the same transient-typed-buffer-committed-on-Enter-or-focus-loss
/// contract.</summary>
/// <remarks>
/// A concrete derivative supplies only the seams that genuinely differ between numeric fields: how a
/// value formats for idle display versus the transient buffer
/// (<see cref="FormatValue(decimal)"/>, <see cref="FormatBufferValue(decimal)"/>), what
/// <see cref="NumberFormatInfo"/> and integer-only policy the buffer parses against
/// (<see cref="BuildBufferFormat"/>, <see cref="IsIntegerOnly"/>), how many fractional digits a
/// commit rounds to and under what policy (<see cref="EffectiveDecimalPlaces"/>,
/// <see cref="ResolveCommitRounding(decimal)"/>), and optionally how a candidate
/// <see cref="Value"/> or <see cref="Culture"/> is validated before it commits
/// (<see cref="ValidateValueAssignment(decimal)"/>, <see cref="ValidateCulture(CultureInfo)"/>), or
/// how the buffer's own text projects into a richer focused display
/// (<see cref="ProjectFocusedDisplay"/>). Everything else - the coordinator wiring, the nullable
/// range state, measurement, buffer refresh, pointer-to-buffer projection, affix-aware rendering,
/// cursor replay, and the typed <see cref="ValueChanged"/> event - lives here exactly once.
/// </remarks>
[PublicAPI]
public abstract class NumericInputBase: InputBase
{
    private protected readonly NumericEditBuffer _buffer = new();
    private protected readonly NumericInputCommitCoordinator _coordinator;

    /// <summary>Initializes a focusable numeric field with no committed value.</summary>
    protected NumericInputBase()
    {
        _coordinator = new NumericInputCommitCoordinator(
            _buffer,
            VerifyMutable,
            NotifyPropertyChanged,
            ResolveCommitRounding,
            () => IsFocused,
            RefreshBuffer,
            (previous, candidate) => ValueChanged?.Invoke(this, new NumericValueChangedEventArgs(previous, candidate)));
        EnableNumericEditing(
            _buffer,
            _coordinator,
            ConfigureBuffer,
            () => EffectiveDecimalPlaces,
            ResolveCaretIndex);
    }

    /// <summary>Raised after a committed value transition.</summary>
    public event EventHandler<NumericValueChangedEventArgs>? ValueChanged;

    /// <summary>Gets or sets the current value, or null when cleared. Assignment clamps silently
    /// into <see cref="Minimum"/> and <see cref="Maximum"/>; a null assignment is a no-op unless
    /// <see cref="AllowNull"/> is set.</summary>
    /// <exception cref="ArgumentException">A derived control's
    /// <see cref="ValidateValueAssignment(decimal)"/> rejects the candidate.</exception>
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

            ValidateValueAssignment(value.Value);
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

    /// <summary>Gets or sets the culture whose formatting and parsing data governs this field's
    /// display and parsing. Default is <see cref="CultureInfo.InvariantCulture"/>, so out-of-the-box
    /// rendering never depends on the host operating system's locale.</summary>
    /// <remarks>Changing this mid-edit discards any in-progress transient buffer back to the
    /// committed value's formatting under the new culture; no half-parsed state migrates across the
    /// switch. Culture changes use reference identity because <see cref="CultureInfo"/> is mutable
    /// configuration: a distinct same-name clone refreshes formatting and the edit buffer, while
    /// reassigning the identical instance raises no notification or invalidation. A derived control
    /// may reject a candidate through <see cref="ValidateCulture(CultureInfo)"/> before it
    /// commits.</remarks>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher, or
    /// a derived control's <see cref="ValidateCulture(CultureInfo)"/> rejects the candidate.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public CultureInfo Culture
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            ValidateCulture(value);

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

    private protected void RefreshBuffer()
    {
        ConfigureBuffer();
        _buffer.Load(Value is { } value ? FormatBufferValue(value) : string.Empty);
    }

    private void ConfigureBuffer() => _buffer.Configure(BuildBufferFormat(), IsIntegerOnly);

    private int ResolveCaretIndex(Point cells)
    {
        var content = ContentBounds;
        var valueBox = DeflateForAffixes(content, MeasureAffixes(StartAffix, EndAffix, ResolveAffixGap()));
        return ResolveBufferIndexAtColumn(cells.X - valueBox.X);
    }

    /// <summary>Maps a pointer column, relative to the value box's left edge, to a UTF-16 index in
    /// the transient edit buffer's own text. The base implementation resolves directly against the
    /// buffer, matching a control whose focused rendering is the buffer's text unchanged; a control
    /// whose focused rendering composes additional decoration around the buffer's core text
    /// overrides this to map back through that composition instead.</summary>
    /// <param name="column">The zero-based column within the value box.</param>
    /// <returns>The UTF-16 index in the transient edit buffer's own text.</returns>
    [Pure]
    protected virtual int ResolveBufferIndexAtColumn(int column) =>
        _buffer.IndexAtColumn(column, CellPolicy.AmbiguousWidth);

    #endregion

    #region Derived-class formatting and validation seams

    /// <summary>Formats a value the way this control's idle, unfocused display presents it - also
    /// used by <see cref="MeasureOverride(Constraint)"/> to size the widest bound.</summary>
    /// <param name="value">The value to format.</param>
    /// <returns>The formatted text.</returns>
    [Pure]
    protected abstract string FormatValue(decimal value);

    /// <summary>Formats a value for loading into the transient edit buffer while focused. The base
    /// implementation delegates to <see cref="FormatValue(decimal)"/>; a derived control whose
    /// buffer excludes decoration <see cref="FormatValue(decimal)"/> includes - such as a currency
    /// symbol - overrides this to format only the buffer-native core text.</summary>
    /// <param name="value">The value to format.</param>
    /// <returns>The buffer-native formatted text.</returns>
    [Pure]
    protected virtual string FormatBufferValue(decimal value) => FormatValue(value);

    /// <summary>Builds the <see cref="NumberFormatInfo"/> the transient edit buffer parses and
    /// formats against.</summary>
    /// <returns>The buffer's separator, group, and sign token source.</returns>
    [Pure]
    protected abstract NumberFormatInfo BuildBufferFormat();

    /// <summary>Gets the fractional digit count a freshly parsed buffer value rounds to at commit,
    /// and that a bound jump (Home or End) rounds toward.</summary>
    protected abstract int EffectiveDecimalPlaces { get; }

    /// <summary>Gets whether the transient edit buffer rejects the decimal-separator keystroke
    /// outright, admitting only whole digits.</summary>
    protected abstract bool IsIntegerOnly { get; }

    /// <summary>Resolves the decimal places and rounding policy a freshly parsed buffer value
    /// commits under.</summary>
    /// <param name="parsed">The value the buffer parsed, before rounding.</param>
    /// <returns>The rounded commit candidate.</returns>
    [Pure]
    protected abstract decimal ResolveCommitRounding(decimal parsed);

    /// <summary>Validates a non-null candidate <see cref="Value"/> assignment before it is clamped
    /// and committed. The base implementation performs no validation; a derived control overrides
    /// this to reject a candidate its own editing mode does not admit.</summary>
    /// <param name="value">The non-null candidate value.</param>
    protected virtual void ValidateValueAssignment(decimal value)
    {
    }

    /// <summary>Validates a non-null candidate <see cref="Culture"/> assignment before it is
    /// committed. The base implementation performs no validation; a derived control overrides this
    /// to reject a culture whose combination with other settings cannot resolve a required
    /// identity.</summary>
    /// <param name="culture">The non-null candidate culture.</param>
    protected virtual void ValidateCulture(CultureInfo culture)
    {
    }

    /// <summary>Projects the transient edit buffer into this control's focused rendered text,
    /// selection, and caret. The base implementation is the identity projection - the buffer's own
    /// text, selection, and caret unchanged - matching a control with no additional focused
    /// decoration; a control whose focused rendering composes decoration around the buffer's core
    /// text overrides this to project through that composition instead.</summary>
    /// <returns>The focused rendered text, the buffer selection projected into it, and the buffer
    /// caret projected into it.</returns>
    [Pure]
    protected virtual NumericFocusedDisplay ProjectFocusedDisplay() =>
        new(_buffer.Text, _buffer.Selection, _buffer.Selection.Caret);

    #endregion

    #region Rendering

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        string displayText;
        var displaySelection = default(Selection);
        var caretIndex = 0;

        if (IsFocused)
        {
            var focused = ProjectFocusedDisplay();
            displayText = focused.Text;
            displaySelection = focused.Selection;
            caretIndex = focused.Caret;
        }
        else
        {
            displayText = Value is { } value ? FormatValue(value) : string.Empty;
        }

        RenderNumericInputContent(
            canvas,
            displayText,
            displaySelection,
            caretIndex,
            StartAffix,
            EndAffix,
            Placeholder,
            CursorShape);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Canvas.CopyFromPrevious already restored this control's affixes and value-text cells;
    /// <see cref="TerminalCanvas.SetCursor(Point, bool, CursorShape)"/> is the one thing a cell copy
    /// can never replay, since cursor placement lives outside the frame's cell arena exactly like
    /// Image's DrawImage placement. An unset render bit already proves the buffer text and caret are
    /// unchanged since the last real paint, so recomputing the caret column from this control's own
    /// CURRENT state - through the same <see cref="ProjectFocusedDisplay"/> projection
    /// <see cref="OnRenderContent(TerminalCanvas)"/> uses - here is provably identical to what that
    /// paint recorded.
    /// </remarks>
    protected internal override void OnReuseCleanRender(TerminalCanvas canvas)
    {
        var focused = ProjectFocusedDisplay();
        ReplayNumericInputCursor(canvas, focused.Text, focused.Caret, StartAffix, EndAffix, CursorShape);
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
