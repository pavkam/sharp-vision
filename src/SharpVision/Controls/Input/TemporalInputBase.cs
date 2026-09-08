// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

/// <summary>Defines the shared nullable bounded value state, lazy dispatcher-clock seeding,
/// culture-driven segmented pattern layout, and per-segment digit/increment/clear dispatch every
/// segmented temporal field control built on
/// <see cref="InputBase.EnableSegmentEditing(Func{IReadOnlyList{SegmentDescriptor}}, Func{SegmentDescriptor, int, bool}, Func{SegmentDescriptor, int, bool}, Func{SegmentDescriptor, bool}, SegmentFieldKeyOptions, bool, bool, Action?)"/>
/// composes: <see cref="DateInput"/>, <see cref="TimeInput"/>, and <see cref="DateTimeInput"/>
/// today, and any in-assembly or third-party derivative editing a different immutable temporal
/// value (a duration field, a month picker) that needs the same lazily seeded,
/// clamped-to-range, segment-at-a-time editing contract.</summary>
/// <typeparam name="TValue">The immutable temporal value type this field edits.</typeparam>
/// <remarks>
/// A concrete derivative supplies only the seams that genuinely differ between temporal fields:
/// which pattern letters map to which <see cref="TemporalSegmentKind"/>
/// (<see cref="TokenKinds"/>), which format pattern is currently active
/// (<see cref="ResolvePattern"/>), how a value renders against a resolved pattern
/// (<see cref="FormatValue(TValue, string, CultureInfo)"/>), the largest value one editable
/// segment can hold (<see cref="MaxValueFor(TemporalSegmentKind, bool, int)"/>), and the three
/// per-segment arithmetic operations a routed key ultimately performs
/// (<see cref="Increment(TValue, TemporalSegmentKind, int, int)"/>,
/// <see cref="ApplyDigit(TValue, TemporalSegmentKind, int, int)"/>,
/// <see cref="ClearSegment(TValue, TemporalSegmentKind)"/>). Everything else - the nullable
/// bounded value state, the lazy dispatcher-clock seed, the shared segment layout skeleton, the
/// null-value seeding a routed digit or increment performs first, culture validation dispatch,
/// and the typed <see cref="ValueChanged"/> event - lives here exactly once.
/// </remarks>
[PublicAPI]
public abstract class TemporalInputBase<TValue>: InputBase
    where TValue : struct, IComparable<TValue>
{
    private protected readonly TemporalValueState<TValue> _state;
    private protected readonly SegmentFieldBehavior _segments;
    private CultureInfo _culture;

    /// <summary>Initializes shared temporal field state with its inclusive range and initial
    /// culture, and opts into the shared active-segment navigation, digit-entry buffering, and
    /// pointer hit-testing engine wired to this base's own <see cref="BuildSegments()"/> layout
    /// and <see cref="ApplyDigit(TValue, TemporalSegmentKind, int, int)"/>/
    /// <see cref="Increment(TValue, TemporalSegmentKind, int, int)"/>/
    /// <see cref="ClearSegment(TValue, TemporalSegmentKind)"/> dispatch - the same
    /// wire-it-once-in-the-base shape <see cref="NumericInputBase"/> uses for its own transient
    /// buffer, so a derivative (in this assembly or any other) gets the complete segmented-editing
    /// contract by implementing only the public and protected seams below, without itself needing
    /// access to the in-assembly segmented-field engine or its friend-only option type.</summary>
    /// <param name="minimum">The initial inclusive lower bound.</param>
    /// <param name="maximum">The initial inclusive upper bound.</param>
    /// <param name="initialCulture">The initial <see cref="Culture"/>.</param>
    /// <exception cref="ArgumentException"><paramref name="minimum"/> exceeds <paramref name="maximum"/>.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="initialCulture"/> is null.</exception>
    protected TemporalInputBase(TValue minimum, TValue maximum, CultureInfo initialCulture)
    {
        ArgumentNullException.ThrowIfNull(initialCulture);
        _culture = initialCulture;
        _state = new TemporalValueState<TValue>(
            minimum,
            maximum,
            this,
            VerifyMutable,
            NotifyPropertyChanged,
            ResolveClockSeed,
            PublishValueChangedCore,
            SynchronizeValue,
            SynchronizeBounds,
            ResolveValueImpactCore);
        _segments = EnableSegmentEditing(
            BuildSegments,
            ApplySegmentDigit,
            ApplySegmentIncrement,
            ApplySegmentClear,
            new SegmentFieldKeyOptions(
                ResolveSegmentStepDelta,
                ClearValue,
                ResolveCharacterCommand(),
                ResolvePopupCommand(),
                handleRecognizedWithoutChange: true),
            ReservesDropDownIndicator,
            ActivateFirstSegmentOnFocus,
            EnsureSeeded);
    }

    /// <summary>Raised after a committed value transition.</summary>
    public event EventHandler<TemporalValueChangedEventArgs<TValue>>? ValueChanged;

    /// <summary>Gets or sets the current value, or null when cleared. Assignment clamps silently
    /// into <see cref="Minimum"/> and <see cref="Maximum"/>.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public TValue? Value
    {
        get
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            return _state.EnsureSeeded();
        }
        set => _ = _state.SetValue(value);
    }

    /// <summary>Gets or sets whether the value may be cleared to null. Default is true.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool AllowNull
    {
        get => _state.AllowNull;
        set => _ = _state.SetAllowNull(value);
    }

    /// <summary>Gets or sets the inclusive lower bound that repairs the current value.</summary>
    /// <exception cref="ArgumentException">The value exceeds <see cref="Maximum"/>.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public TValue Minimum
    {
        get => _state.Minimum;
        set => _ = _state.SetMinimum(value);
    }

    /// <summary>Gets or sets the inclusive upper bound that repairs the current value.</summary>
    /// <exception cref="ArgumentException">The value precedes <see cref="Minimum"/>.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public TValue Maximum
    {
        get => _state.Maximum;
        set => _ = _state.SetMaximum(value);
    }

    /// <summary>Gets or sets the culture governing this field's segment order, separators,
    /// designator text, and digit glyphs. A derived control validates a candidate through
    /// <see cref="ValidateCulture(CultureInfo)"/> before it commits.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="ArgumentException"><see cref="ValidateCulture(CultureInfo)"/> rejects the candidate.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public CultureInfo Culture
    {
        get => _culture;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            ValidateCulture(value);
            _ = SetPropertyAndSynchronize(
                ref _culture,
                value,
                InvalidationImpact.Measure,
                () =>
                {
                    SynchronizeCulture(value);
                    InvalidateSegmentLayout();
                },
                ReferenceEqualityComparer.Instance);
        }
    }

    #region Derived-class seams

    /// <summary>Projects the owning dispatcher's current clock into <typeparamref name="TValue"/>,
    /// used both to lazily seed <see cref="Value"/> on first read and to seed a null value that a
    /// routed segment increment reaches.</summary>
    /// <returns>The current local instant, projected into <typeparamref name="TValue"/>.</returns>
    [Pure]
    protected abstract TValue ResolveClockSeed();

    /// <summary>Resolves the seed a routed digit entry commits first when it reaches a null value.
    /// The base implementation defers to <see cref="ResolveClockSeed"/>, matching every derivative
    /// except one that deliberately seeds a different starting point for typed digit entry than for
    /// Up/Down.</summary>
    /// <returns>The seed value a null-value digit entry commits before applying the typed digit.</returns>
    [Pure]
    protected virtual TValue ResolveDigitEntrySeed() => ResolveClockSeed();

    /// <summary>Gets the map from a recognized custom format pattern letter to the
    /// <see cref="TemporalSegmentKind"/> it produces, passed to the internal pattern segmenter
    /// when parsing <see cref="ResolvePattern"/>.</summary>
    protected abstract IReadOnlyDictionary<char, TemporalSegmentKind> TokenKinds { get; }

    /// <summary>Resolves the currently active custom format pattern - an explicit override or a
    /// structurally derived default.</summary>
    /// <returns>The non-null, non-empty pattern to segment and format against.</returns>
    [Pure]
    protected abstract string ResolvePattern();

    /// <summary>Rewrites a parsed pattern immediately before it formats a non-null value, letting a
    /// derivative normalize a token the parsed layout already committed to (for example rewriting a
    /// designator-less 12-hour token to its 24-hour equivalent). The base implementation is the
    /// identity transform.</summary>
    /// <param name="pattern">The pattern <see cref="ResolvePattern"/> returned.</param>
    /// <param name="hasAmPmDesignator">Whether the parsed layout includes an AM/PM designator segment.</param>
    /// <returns>The pattern to format the current value against.</returns>
    [Pure]
    protected virtual string AdjustRenderingPattern(string pattern, bool hasAmPmDesignator) => pattern;

    /// <summary>Resolves the culture a non-null value formats under, distinct from <see cref="Culture"/>
    /// only for a derivative whose pattern occasionally demands invariant rendering (a round-trip
    /// specifier). The base implementation returns <see cref="Culture"/>.</summary>
    /// <returns>The culture <see cref="FormatValue(TValue, string, CultureInfo)"/> renders under.</returns>
    [Pure]
    protected virtual CultureInfo ResolveRenderingCulture() => Culture;

    /// <summary>Formats a non-null value against a resolved pattern and culture, typically
    /// <c>value.ToString(format, culture)</c>.</summary>
    /// <param name="value">The non-null value to format.</param>
    /// <param name="format">The pattern segment or run to format against.</param>
    /// <param name="culture">The culture resolved by <see cref="ResolveRenderingCulture"/>.</param>
    /// <returns>The formatted text.</returns>
    [Pure]
    protected abstract string FormatValue(TValue value, string format, CultureInfo culture);

    /// <summary>Reports whether a non-null value falls in the PM half of the day, used only to
    /// resolve an editable AM/PM designator segment's rendered text. The base implementation
    /// returns false, matching a derivative whose value has no time-of-day component.</summary>
    /// <param name="value">The non-null value to classify.</param>
    [Pure]
    protected virtual bool IsPm(TValue value) => false;

    /// <summary>Resolves the largest numeric value one editable segment can hold, used only to
    /// compute the single-digit entry that should auto-commit immediately instead of buffering for
    /// a second digit.</summary>
    /// <param name="kind">The segment's semantic kind.</param>
    /// <param name="hasAmPmDesignator">Whether the current layout has a 12-hour AM/PM designator segment.</param>
    /// <param name="runLength">The pattern run length backing the segment.</param>
    /// <returns>The segment's maximum admitted value.</returns>
    [Pure]
    protected abstract int MaxValueFor(TemporalSegmentKind kind, bool hasAmPmDesignator, int runLength);

    /// <summary>Applies a one-step increment (positive or negative <paramref name="delta"/>) to one
    /// segment of a non-null value.</summary>
    /// <param name="value">The current non-null value.</param>
    /// <param name="kind">The active segment's semantic kind.</param>
    /// <param name="delta">The signed step to apply.</param>
    /// <param name="digitCapacity">The active segment's digit capacity, needed only to resolve a
    /// fractional-second segment's unit tick size.</param>
    /// <returns>The incremented candidate, or null when the increment cannot apply (an overflow
    /// beyond the value type's own representable range).</returns>
    [Pure]
    protected abstract TValue? Increment(TValue value, TemporalSegmentKind kind, int delta, int digitCapacity);

    /// <summary>Applies a fully or partially typed numeric value to one segment of a non-null
    /// value.</summary>
    /// <param name="value">The current non-null value.</param>
    /// <param name="kind">The active segment's semantic kind.</param>
    /// <param name="digitValue">The typed numeric value, already clamped by the caller only where a
    /// shared clamp helper applies.</param>
    /// <param name="digitCapacity">The active segment's digit capacity, needed only to resolve a
    /// fractional-second segment's tick precision.</param>
    /// <returns>The candidate with the segment replaced, or null when the typed value cannot apply.</returns>
    [Pure]
    protected abstract TValue? ApplyDigit(TValue value, TemporalSegmentKind kind, int digitValue, int digitCapacity);

    /// <summary>Resets one segment of a non-null value to its lowest representable value.</summary>
    /// <param name="value">The current non-null value.</param>
    /// <param name="kind">The active segment's semantic kind.</param>
    /// <returns>The candidate with the segment cleared, or null when the segment cannot be cleared.</returns>
    [Pure]
    protected abstract TValue? ClearSegment(TValue value, TemporalSegmentKind kind);

    /// <summary>Validates a non-null candidate <see cref="Culture"/> before it commits.</summary>
    /// <param name="culture">The non-null candidate culture.</param>
    /// <exception cref="ArgumentException">The candidate is unsuitable for this field.</exception>
    protected abstract void ValidateCulture(CultureInfo culture);

    /// <summary>Synchronizes a connected presentation - such as an owned Calendar popup - after a
    /// value commit. The base implementation does nothing, matching a derivative with no connected
    /// presentation.</summary>
    /// <param name="value">The newly committed nullable value.</param>
    protected virtual void SynchronizeValue(TValue? value)
    {
    }

    /// <summary>Synchronizes a connected presentation's range after a bound commit. The base
    /// implementation does nothing, matching a derivative with no connected presentation.</summary>
    protected virtual void SynchronizeBounds()
    {
    }

    /// <summary>Synchronizes a connected presentation's culture after a committed <see cref="Culture"/>
    /// transition. The base implementation does nothing, matching a derivative with no connected
    /// presentation.</summary>
    /// <param name="culture">The newly committed culture.</param>
    protected virtual void SynchronizeCulture(CultureInfo culture)
    {
    }

    /// <summary>Clears the complete value to null when <see cref="AllowNull"/> allows it. The base
    /// implementation additionally requires a present value, matching every derivative except one
    /// whose own clearing policy admits an already-empty value as a no-op through this same
    /// call.</summary>
    /// <returns>True when the value actually changed.</returns>
    protected virtual bool ClearValue() => AllowNull && _state.Value.HasValue && _state.SetValue(null);

    /// <summary>Resolves the optional non-digit character command (such as an AM/PM selection
    /// shortcut) the constructor wires into the segmented-editing engine. The base implementation
    /// returns null, matching a derivative whose value has no such shortcut.</summary>
    /// <returns>The character command, or null to admit none.</returns>
    [Pure]
    protected virtual Func<Rune, bool>? ResolveCharacterCommand() => null;

    /// <summary>Resolves the optional popup-opening command (such as an owned Calendar's
    /// disclosure key) the constructor wires into the segmented-editing engine. The base
    /// implementation returns null, matching a derivative with no owned popup.</summary>
    /// <returns>The popup command, or null to admit none.</returns>
    [Pure]
    protected virtual Func<KeyEventArgs, bool?>? ResolvePopupCommand() => null;

    /// <summary>Gets whether the field reserves a column for an owned drop-down indicator. The
    /// base implementation returns false, matching a derivative with no owned popup.</summary>
    protected virtual bool ReservesDropDownIndicator => false;

    /// <summary>Gets whether each focus entry returns to the first editable segment. The base
    /// implementation returns false, matching a derivative whose focus entry keeps whichever
    /// segment was last active.</summary>
    protected virtual bool ActivateFirstSegmentOnFocus => false;

    #endregion

    #region Segment layout and dispatch

    /// <summary>Latches <see cref="Value"/> to <see cref="ResolveClockSeed"/> on first read, so a
    /// control mounted under a dispatcher observes that dispatcher's clock instead of the clock
    /// current at construction. A value already committed - including an explicit null under
    /// <see cref="AllowNull"/> - is left untouched.</summary>
    protected void EnsureSeeded() => _ = _state.EnsureSeeded();

    /// <summary>Builds the current segment layout for the currently committed value, the
    /// zero-argument shape <see cref="InputBase.EnableSegmentEditing"/>'s segments provider
    /// requires.</summary>
    /// <returns>The ordered literal and editable segments for the current value.</returns>
    private protected SegmentDescriptor[] BuildSegments() => BuildSegments(_state.Value);

    /// <summary>Builds the ordered literal and editable segment layout for an arbitrary candidate
    /// value: parses <see cref="ResolvePattern"/> against <see cref="TokenKinds"/>, formats each
    /// editable run (or a null placeholder), and resolves each segment's digit capacity and maximum
    /// value.</summary>
    /// <param name="value">The candidate value to render, or null for the placeholder layout.</param>
    /// <returns>The ordered literal and editable segments.</returns>
    private protected SegmentDescriptor[] BuildSegments(TValue? value)
    {
        var pattern = ResolvePattern();
        var tokenKinds = TokenKinds;
        var tokens = TemporalPatternSegmenter.ParseTokens(pattern, tokenKinds, Culture);
        var hasAmPmDesignator = false;

        foreach (var token in tokens)
        {
            if (token.Kind == TemporalSegmentKind.AmPmDesignator)
            {
                hasAmPmDesignator = true;
                break;
            }
        }

        IReadOnlyList<string> text;

        if (value is { } current)
        {
            var renderingPattern = AdjustRenderingPattern(pattern, hasAmPmDesignator);
            var renderingCulture = ResolveRenderingCulture();
            text = TemporalPatternSegmenter.FormatSegments(
                renderingPattern,
                tokens,
                tokenKinds,
                format => FormatValue(current, format, renderingCulture));
        }
        else
        {
            var placeholder = new string[tokens.Count];

            for (var index = 0; index < tokens.Count; index++)
            {
                placeholder[index] = TemporalSegmentClassification.Placeholder(tokens[index]);
            }

            text = placeholder;
        }

        var descriptors = new SegmentDescriptor[tokens.Count];

        for (var index = 0; index < tokens.Count; index++)
        {
            var token = tokens[index];
            var segmentText = TemporalSegmentClassification.ReserveOptionalFractionCells(token, text[index]);

            // A weekday (dddd) or month-name (MMMM) run of length >= 3 is a name, not a
            // zero-padded number: rendering it as an ordinary editable segment would let a typed
            // digit be misinterpreted as a day-of-month or month-number and corrupt the value.
            // Building it as a literal instead makes it inert for digit entry, tab/arrow
            // traversal, and Increment alike, since SegmentFieldBehavior gates all three purely
            // on SegmentDescriptor.IsEditable.
            var isNameLiteral = token.Kind is TemporalSegmentKind.Month or TemporalSegmentKind.Day &&
                token.RunLength >= 3;

            descriptors[index] = token.Kind is not { } kind || isNameLiteral
                ? new SegmentDescriptor(segmentText)
                : new SegmentDescriptor(
                    kind == TemporalSegmentKind.AmPmDesignator
                        ? TemporalSegmentClassification.ResolveDesignatorText(
                            segmentText,
                            value is { } pmValue && IsPm(pmValue))
                        : segmentText,
                    kind,
                    TemporalSegmentClassification.DigitCapacity(token),
                    MaxValueFor(kind, hasAmPmDesignator, token.RunLength));
        }

        return descriptors;
    }

    private bool ApplySegmentDigit(SegmentDescriptor segment, int digitValue)
    {
        var kind = segment.Kind!.Value;

        if (_state.Value is not { } current)
        {
            _ = _state.SetValue(_state.Clamp(ResolveDigitEntrySeed()));

            if (_state.Value is not { } seeded)
            {
                return false;
            }

            current = seeded;
        }

        var result = ApplyDigit(current, kind, digitValue, segment.DigitCapacity);
        return result is { } value && _state.SetValue(value);
    }

    private bool ApplySegmentIncrement(SegmentDescriptor segment, int delta)
    {
        var kind = segment.Kind!.Value;

        if (_state.Value is not { } current)
        {
            return _state.SetValue(_state.Clamp(ResolveClockSeed()));
        }

        var result = Increment(current, kind, delta, segment.DigitCapacity);
        return result is { } value && _state.SetValue(value);
    }

    private bool ApplySegmentClear(SegmentDescriptor segment)
    {
        var kind = segment.Kind!.Value;

        if (_state.Value is not { } current)
        {
            return false;
        }

        var result = ClearSegment(current, kind);
        return result is { } value && _state.SetValue(value);
    }

    private InvalidationImpact ResolveValueImpactCore(TValue? previous, TValue? candidate) =>
        ResolveSegmentWidthImpact(BuildSegments(previous), BuildSegments(candidate));

    private void PublishValueChangedCore(
        ref CallbackTransitionTransaction transition,
        TValue? previous,
        TValue? current) =>
        transition.PublishCurrent(
            ValueChanged,
            this,
            new TemporalValueChangedEventArgs<TValue>(previous, current));

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
