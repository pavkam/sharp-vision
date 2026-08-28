// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using SharpVision.Terminal.Input;
using SharpVision.Text;

using TextSelection = Text.Selection;

/// <content>Owns the opt-in semantic text-selection capability shared by every control.</content>
public abstract partial class ControlBase: ISelectableTextSource
{
    private TextSelectionGesture? _textSelectionGesture;
    private int? _textSelectionDesiredColumn;
    private int? _textSelectionDesiredRow;
    private bool _textSelectionCaretEstablished;
    private ulong _textSelectionFingerprint;
    private ulong _textSelectionCapabilityVersion;

    /// <summary>Gets the common pointer-selection phase for behavioral invariant tests.</summary>
    internal TextSelectionGesturePhase TextSelectionPhase =>
        _textSelectionGesture?.Phase ?? TextSelectionGesturePhase.Idle;

    /// <summary>Gets the base-owned range for editor mutation transactions.</summary>
    protected TextSelection CommittedTextSelection { get; private set; }

    /// <summary>Gets the current commit's transition version, for reentrancy detection by an
    /// <see cref="OnTextSelectionStateChanged(TextSelectionChangedEventArgs)"/> override that
    /// publishes more than one dependent notification for a single commit.</summary>
    protected ulong TextSelectionTransitionVersion { get; private set; }

    /// <summary>Raised synchronously after the directional semantic-text selection changes.</summary>
    /// <remarks>
    /// If an earlier notification reenters selection mutation, remaining observers receive only the
    /// newer committed transition; an obsolete outer transition is not published afterward.
    /// </remarks>
    public event EventHandler<TextSelectionChangedEventArgs>? TextSelectionChanged;

    /// <summary>Gets or sets whether this control selects semantic text projected by its subtree.</summary>
    /// <remarks>
    /// The capability is disabled by default. Disabling it clears the committed range after the
    /// property state changes. It does not change focusability or tab-navigation policy.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool IsTextSelectionEnabled
    {
        get;
        set
        {
            VerifyMutable();

            if (field == value)
            {
                return;
            }

            var version = ++_textSelectionCapabilityVersion;

            if (!SetProperty(ref field, value, InvalidationImpact.Render))
            {
                return;
            }

            if (!IsTextSelectionCapabilityCurrent(version, value))
            {
                return;
            }

            if (!value)
            {
                _textSelectionGesture?.Cancel(releaseCapture: true);

                if (!IsTextSelectionCapabilityCurrent(version, value))
                {
                    return;
                }

                _textSelectionCaretEstablished = false;
                _ = CommitTextSelection(default);
            }
            else
            {
                _textSelectionGesture = new TextSelectionGesture(this);
            }

            if (IsTextSelectionCapabilityCurrent(version, value))
            {
                OnTextSelectionEnabledChanged(value);
            }
        }
    }

    /// <summary>Gets the current directional UTF-16 selection over this control's semantic text.</summary>
    public virtual TextSelection TextSelection
    {
        get
        {
            if (!IsTextSelectionEnabled)
            {
                return default;
            }

            VerifyMutable();
            _ = ReconcileTextSelectionMap();
            return CommittedTextSelection;
        }
    }

    /// <summary>Gets an independently owned copy of the selected semantic text.</summary>
    /// <exception cref="InvalidOperationException">The attached control is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public virtual string SelectedText
    {
        get
        {
            VerifyMutable();

            if (!IsTextSelectionEnabled)
            {
                return string.Empty;
            }

            var map = ReconcileTextSelectionMap();
            return CommittedTextSelection.IsEmpty
                ? string.Empty
                : map.Text.Substring(CommittedTextSelection.Start, CommittedTextSelection.Length);
        }
    }

    /// <summary>Creates this control's complete semantic text and visible grapheme geometry.</summary>
    /// <returns>An independently owned snapshot in this control's local cell coordinates.</returns>
    /// <exception cref="InvalidOperationException">The attached control is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public virtual SelectableTextSnapshot GetSelectableTextSnapshot()
    {
        VerifyMutable();
        return CreateSelectableTextSnapshot();
    }

    /// <summary>Creates the concrete snapshot behind the inherited public selection source.</summary>
    internal virtual SelectableTextSnapshot CreateSelectableTextSnapshot()
    {
        var children = new List<ControlBase>();
        return AddSelectableTextChildren(children)
            ? SelectableTextAggregation.Create(this)
            : new SelectableTextSnapshot(string.Empty, [], isAuthoritative: false);
    }

    /// <summary>Replaces the selection with validated UTF-16 grapheme-boundary endpoints.</summary>
    /// <param name="selection">The proposed directional semantic-text selection.</param>
    /// <exception cref="ArgumentOutOfRangeException">An endpoint exceeds the semantic text length.</exception>
    /// <exception cref="ArgumentException">An endpoint splits an extended grapheme cluster.</exception>
    /// <exception cref="InvalidOperationException">
    /// Text selection is disabled, or the attached control is mutated off-dispatcher.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public virtual void SetTextSelection(TextSelection selection)
    {
        VerifyTextSelectionEnabled();
        var map = GetTextSelectionMap();
        Edit.Validate(map.Text, selection);
        _textSelectionDesiredColumn = null;
        _textSelectionDesiredRow = null;
        _textSelectionCaretEstablished = true;
        _textSelectionFingerprint = map.Fingerprint;
        _ = CommitTextSelection(selection);
    }

    /// <summary>Selects the complete current semantic text stream.</summary>
    /// <exception cref="InvalidOperationException">
    /// Text selection is disabled, or the attached control is mutated off-dispatcher.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public virtual void SelectAllText()
    {
        VerifyTextSelectionEnabled();
        var map = GetTextSelectionMap();
        _textSelectionDesiredColumn = null;
        _textSelectionDesiredRow = null;
        _textSelectionCaretEstablished = true;
        _textSelectionFingerprint = map.Fingerprint;
        _ = CommitTextSelection(new TextSelection(0, map.Text.Length));
    }

    /// <summary>Collapses the selection at its current directional caret endpoint.</summary>
    /// <exception cref="InvalidOperationException">
    /// Text selection is disabled, or the attached control is mutated off-dispatcher.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public virtual void ClearTextSelection()
    {
        VerifyTextSelectionEnabled();
        _textSelectionDesiredColumn = null;
        _textSelectionDesiredRow = null;
        _textSelectionCaretEstablished = true;
        _ = CommitTextSelection(new TextSelection(CommittedTextSelection.Caret, CommittedTextSelection.Caret));
    }

    /// <summary>Returns selected semantic text without publishing clipboard or terminal state.</summary>
    /// <returns>An independently owned string, or empty when the selection is collapsed.</returns>
    /// <exception cref="InvalidOperationException">The attached control is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    [Pure]
    public virtual string CopySelectedText() => GetTextSelectionCopyText();

    /// <summary>Gets copied selection text after component disclosure policy is applied.</summary>
    /// <returns>An independently owned string, or empty when disclosure is suppressed.</returns>
    protected virtual string GetTextSelectionCopyText() => SelectedText;

    /// <summary>Commits a validated selection and publishes its common post-commit event.</summary>
    /// <param name="selection">The validated directional selection.</param>
    /// <param name="beforeNotifications">Optional component work after state commit and before selection events.</param>
    /// <returns>True when the committed value changed; otherwise false.</returns>
    protected bool CommitTextSelection(TextSelection selection, Action? beforeNotifications = null)
    {
        var map = GetTextSelectionMap();
        return CommitTextSelection(selection, map.Text, map.Fingerprint, beforeNotifications);
    }

    /// <summary>Commits an editor transaction against authoritative text without building render geometry.</summary>
    /// <param name="selection">The proposed directional selection.</param>
    /// <param name="text">The non-null authoritative semantic text.</param>
    /// <param name="beforeNotifications">Optional component work after state commit and before selection events.</param>
    /// <returns>True when the committed value changed; otherwise false.</returns>
    protected bool CommitTextSelectionForAuthoritativeText(
        TextSelection selection,
        string text,
        Action? beforeNotifications = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        return CommitTextSelection(
            selection,
            text,
            TextSelectionMap.ComputeFingerprint(text, []),
            beforeNotifications);
    }

    private bool CommitTextSelection(
        TextSelection selection,
        string text,
        ulong fingerprint,
        Action? beforeNotifications)
    {
        Edit.Validate(text, selection);
        _textSelectionFingerprint = fingerprint;

        if (CommittedTextSelection == selection)
        {
            return false;
        }

        var previous = CommittedTextSelection;
        CommittedTextSelection = selection;
        unchecked
        {
            TextSelectionTransitionVersion++;
        }
        var transitionVersion = TextSelectionTransitionVersion;
        Invalidate(Invalidation.Render);
        var eventArgs = new TextSelectionChangedEventArgs(previous, selection);
        OnTextSelectionStateChanged(eventArgs);

        if (TextSelectionTransitionVersion != transitionVersion)
        {
            return true;
        }

        beforeNotifications?.Invoke();

        if (TextSelectionTransitionVersion != transitionVersion)
        {
            return true;
        }

        OnTextSelectionCommitted(eventArgs, transitionVersion);

        if (TextSelectionTransitionVersion == transitionVersion)
        {
            RaiseTextSelectionChanged(eventArgs, transitionVersion);
        }

        return true;
    }

    private void RaiseTextSelectionChanged(
        TextSelectionChangedEventArgs eventArgs,
        ulong transitionVersion)
    {
        var handlers = TextSelectionChanged;

        if (handlers is null)
        {
            return;
        }

        foreach (var subscriber in handlers.GetInvocationList())
        {
            if (TextSelectionTransitionVersion != transitionVersion)
            {
                break;
            }

            var handler = (EventHandler<TextSelectionChangedEventArgs>) subscriber;
            handler(this, eventArgs);
        }
    }

    /// <summary>Invokes a component's own compatibility selection-changed event without redelivering
    /// a transition superseded by a subscriber that reenters during delivery.</summary>
    /// <param name="handlers">The subscribed compatibility delegate, or null when unsubscribed.</param>
    /// <param name="sender">The event sender.</param>
    /// <param name="transitionVersion">The transition version captured before <see cref="OnTextSelectionCommitted"/> ran.</param>
    protected void RaiseTextSelectionCompatibilityEvent(
        EventHandler? handlers,
        object? sender,
        ulong transitionVersion)
    {
        if (handlers is null)
        {
            return;
        }

        foreach (var subscriber in handlers.GetInvocationList())
        {
            if (TextSelectionTransitionVersion != transitionVersion)
            {
                break;
            }

            ((EventHandler) subscriber)(sender, EventArgs.Empty);
        }
    }

    /// <summary>Invokes a component's own compatibility selection-changed event without redelivering
    /// a transition superseded by a subscriber that reenters during delivery.</summary>
    /// <typeparam name="TEventArgs">The compatibility event's argument type.</typeparam>
    /// <param name="handlers">The subscribed compatibility delegate, or null when unsubscribed.</param>
    /// <param name="sender">The event sender.</param>
    /// <param name="eventArgs">The immutable compatibility event payload.</param>
    /// <param name="transitionVersion">The transition version captured before <see cref="OnTextSelectionCommitted"/> ran.</param>
    protected void RaiseTextSelectionCompatibilityEvent<TEventArgs>(
        EventHandler<TEventArgs>? handlers,
        object? sender,
        TEventArgs eventArgs,
        ulong transitionVersion)
    {
        if (handlers is null)
        {
            return;
        }

        foreach (var subscriber in handlers.GetInvocationList())
        {
            if (TextSelectionTransitionVersion != transitionVersion)
            {
                break;
            }

            ((EventHandler<TEventArgs>) subscriber)(sender, eventArgs);
        }
    }

    /// <summary>Publishes component compatibility state after base selection commits.</summary>
    /// <param name="eventArgs">The immutable common transition.</param>
    /// <param name="transitionVersion">
    /// The transition version to pass to <see cref="RaiseTextSelectionCompatibilityEvent(EventHandler?, object?, ulong)"/>
    /// or its generic overload so a reentrant commit cannot redeliver this obsolete transition.
    /// </param>
    protected virtual void OnTextSelectionCommitted(TextSelectionChangedEventArgs eventArgs, ulong transitionVersion) =>
        _ = (eventArgs, transitionVersion);

    /// <summary>Synchronizes component state immediately after the base range changes.</summary>
    /// <param name="eventArgs">The immutable common transition.</param>
    protected virtual void OnTextSelectionStateChanged(TextSelectionChangedEventArgs eventArgs) => _ = eventArgs;

    /// <summary>Responds after the opt-in selection capability state changes.</summary>
    /// <param name="enabled">The committed capability state.</param>
    protected virtual void OnTextSelectionEnabledChanged(bool enabled) => _ = enabled;

    [Pure]
    private bool IsTextSelectionCapabilityCurrent(ulong version, bool value) =>
        !IsDisposed && _textSelectionCapabilityVersion == version && IsTextSelectionEnabled == value;

    /// <summary>Gets whether this control's own snapshot replaces retained-child aggregation.</summary>
    protected virtual bool HasAuthoritativeTextSelectionProjection => false;

    /// <summary>Creates the complete geometry projection used by common navigation and adornment.</summary>
    /// <returns>An owned semantic snapshot whose geometry may include cells outside the visible clip.</returns>
    protected virtual SelectableTextSnapshot GetTextSelectionProjection() => GetSelectableTextSnapshot();

    /// <summary>Gets whether this authoritative owner arbitrates drags beginning in selectable descendants.</summary>
    private protected virtual bool OwnsDescendantTextSelectionGestures => HasAuthoritativeTextSelectionProjection;

    /// <summary>Verifies this control may read or mutate its enabled text-selection state.</summary>
    /// <exception cref="InvalidOperationException">Text selection is disabled, or the attached control is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    protected void VerifyTextSelectionEnabled()
    {
        VerifyMutable();

        if (!IsTextSelectionEnabled)
        {
            throw new InvalidOperationException("Semantic text selection is not enabled for this control.");
        }
    }

    /// <summary>Marks a specialized editor's committed caret as eligible for common keyboard navigation.</summary>
    protected void EstablishTextSelectionCaret() => _textSelectionCaretEstablished = true;

    /// <summary>Cancels the common pointer-selection transaction during component projection replacement.</summary>
    protected void CancelTextSelectionGesture(bool releaseCapture) =>
        _textSelectionGesture?.Cancel(releaseCapture);

    /// <summary>Builds the indexed semantic map used by common text-selection behavior.</summary>
    /// <returns>An immutable current map in this control's local cell coordinates.</returns>
    internal virtual TextSelectionMap GetTextSelectionMap()
    {
        var children = new List<ControlBase>();

        if (!HasAuthoritativeTextSelectionProjection && AddSelectableTextChildren(children))
        {
            return SelectableTextAggregation.CreateMap(this);
        }

        var snapshot = GetTextSelectionProjection();
        var glyphs = new TextSelectionGlyph[snapshot.Glyphs.Count];
        var lineCount = Math.Max(0, Bounds.Height);

        for (var index = 0; index < glyphs.Length; index++)
        {
            var glyph = snapshot.Glyphs[index];
            glyphs[index] = new TextSelectionGlyph(glyph.Range, glyph.Bounds);
            lineCount = Math.Max(lineCount, glyph.Bounds.Bottom);
        }

        return new TextSelectionMap(snapshot.Text, glyphs, [], lineCount);
    }

    /// <summary>Gets whether one routed pointer cell may arm the common selection gesture.</summary>
    protected virtual bool IsTextSelectionPointerTarget(ControlBase? originalSource, Point cells)
    {
        _ = originalSource;
        return Bounds.Contains(cells);
    }

    /// <summary>Gets whether the common controller captures immediately to observe drags leaving this owner.</summary>
    protected virtual bool CaptureTextSelectionOnPress => false;

    /// <summary>Gets whether this owner requests immediate potential-gesture capture.</summary>
    internal bool ShouldCaptureTextSelectionOnPress => CaptureTextSelectionOnPress;

    /// <summary>Gets the common final-adornment colors for this owner.</summary>
    protected virtual TerminalStyle ApplyTextSelectionStyle(TerminalStyle current) => new(
        ResolveColor(new ControlColor(SemanticColor.SelectedText)),
        ResolveColor(new ControlColor(SemanticColor.SelectedControl)),
        current.Attributes,
        current.Hyperlink,
        current.Underline,
        current.UnderlineColor);

    /// <summary>Reveals one keyboard-moved caret through an authoritative local viewport.</summary>
    protected virtual void RevealTextSelectionCaret(int caret)
    {
        if ((Dispatcher is not null && !ContainsFocus) ||
            IsDisposed ||
            !EffectiveIsEnabled ||
            !EffectiveIsVisible)
        {
            return;
        }

        var expectedSelection = CommittedTextSelection;
        var map = GetTextSelectionMap();
        var expectedFingerprint = map.Fingerprint;

        _ = map.TryGetCaretGeometry(caret, out _, out var source);

        if (source is { Viewport: { } sourceViewport } && IsTextSelectionSourceEligible(source))
        {
            var localOffset = Math.Clamp(caret - source.Range.Start, 0, source.Text.Length);
            _ = sourceViewport.RevealSelectableTextOffset(localOffset);

            if (!CanContinueTextSelectionReveal(expectedSelection, expectedFingerprint, out map))
            {
                return;
            }
        }

        if (this is ISelectableTextViewport viewport)
        {
            _ = viewport.RevealSelectableTextOffset(caret);

            if (!CanContinueTextSelectionReveal(expectedSelection, expectedFingerprint, out map))
            {
                return;
            }
        }

        if (!map.TryGetCaretGeometry(caret, out var caretBounds, out _))
        {
            return;
        }

        var screenBounds = GetTextSelectionAdornmentBounds(caretBounds);

        for (var current = Parent; current is not null; current = current.Parent)
        {
            if (!AllowsModalAncestor(current))
            {
                break;
            }

            if (current is not Container
                {
                    AutoScroll: true,
                    EffectiveIsEnabled: true,
                    EffectiveIsVisible: true
                } container)
            {
                continue;
            }

            var ancestorViewport = new Rect(
                container.ContentBounds.X,
                container.ContentBounds.Y,
                container.Viewport.Width,
                container.Viewport.Height);
            var horizontal = TextSelectionRevealDelta(
                screenBounds.X,
                screenBounds.Width,
                ancestorViewport.X,
                ancestorViewport.Width);
            var vertical = TextSelectionRevealDelta(
                screenBounds.Y,
                screenBounds.Height,
                ancestorViewport.Y,
                ancestorViewport.Height);
            horizontal = (container.ScrollBars & ScrollBars.Horizontal) != 0 ? horizontal : 0;
            vertical = (container.ScrollBars & ScrollBars.Vertical) != 0 ? vertical : 0;

            if (horizontal == 0 && vertical == 0)
            {
                continue;
            }

            var previousHorizontal = container.HorizontalOffset;
            var previousVertical = container.VerticalOffset;
            _ = container.ScrollBy(horizontal, vertical, ScrollCause.Keyboard);
            screenBounds = new Rect(
                screenBounds.X.SaturatingAdd(previousHorizontal - container.HorizontalOffset),
                screenBounds.Y.SaturatingAdd(previousVertical - container.VerticalOffset),
                screenBounds.Width,
                screenBounds.Height);

            if (!CanContinueTextSelectionReveal(expectedSelection, expectedFingerprint, out _))
            {
                return;
            }
        }
    }

    private bool CanContinueTextSelectionReveal(
        TextSelection expectedSelection,
        ulong expectedFingerprint,
        out TextSelectionMap map)
    {
        map = GetTextSelectionMap();
        return (Dispatcher is null || ContainsFocus) &&
               !IsDisposed &&
               EffectiveIsEnabled &&
               EffectiveIsVisible &&
               CommittedTextSelection == expectedSelection &&
               map.Fingerprint == expectedFingerprint;
    }

    [Pure]
    private static int TextSelectionRevealDelta(int start, int length, int viewportStart, int viewportLength)
    {
        if (viewportLength <= 0 || start < viewportStart)
        {
            return start - viewportStart;
        }

        var end = (long) start + length;
        var viewportEnd = (long) viewportStart + viewportLength;
        return end > viewportEnd ? (int) Math.Clamp(end - viewportEnd, int.MinValue, int.MaxValue) : 0;
    }

    private TextSelectionMap ReconcileTextSelectionMap()
    {
        var map = GetTextSelectionMap();

        if (CommittedTextSelection == default || _textSelectionFingerprint == map.Fingerprint)
        {
            _textSelectionFingerprint = map.Fingerprint;
            return map;
        }

        var previous = CommittedTextSelection;
        CommittedTextSelection = default;
        _textSelectionCaretEstablished = false;
        _textSelectionDesiredColumn = null;
        _textSelectionDesiredRow = null;
        _textSelectionFingerprint = map.Fingerprint;
        unchecked
        {
            TextSelectionTransitionVersion++;
        }
        var transitionVersion = TextSelectionTransitionVersion;
        Invalidate(Invalidation.Render);
        RaiseTextSelectionChanged(new TextSelectionChangedEventArgs(previous, default), transitionVersion);
        return map;
    }

    /// <summary>Resolves one screen cell to the nearest semantic grapheme endpoint.</summary>
    /// <param name="cells">The screen-cell pointer coordinate.</param>
    /// <returns>A grapheme-aligned UTF-16 endpoint.</returns>
    internal int HitTestTextSelection(Point cells) => HitTestTextSelectionCore(cells);

    /// <summary>Resolves one screen cell against a component's authoritative semantic projection.</summary>
    protected virtual int HitTestTextSelectionCore(Point cells)
    {
        var local = new Point(
            cells.X.SaturatingSubtract(Bounds.X),
            cells.Y.SaturatingSubtract(Bounds.Y));
        return GetTextSelectionMap().HitTest(local);
    }

    /// <summary>Maps one semantic glyph from projection coordinates into final screen cells.</summary>
    protected virtual Rect GetTextSelectionAdornmentBounds(Rect bounds) => new(
        Bounds.X.SaturatingAdd(bounds.X),
        Bounds.Y.SaturatingAdd(bounds.Y),
        bounds.Width,
        bounds.Height);

    /// <summary>Runs component click policy after the shared click selection commits.</summary>
    protected virtual void OnTextSelectionClickCompleted(
        ControlBase? originalSource,
        Point pressCells,
        Point releaseCells,
        int clickCount,
        PointerEventArgs eventArgs)
    {
        _ = originalSource;
        _ = pressCells;
        _ = releaseCells;
        _ = clickCount;
        _ = eventArgs;
    }

    /// <summary>Normalizes terminal click counts for component-specific interactive descendants.</summary>
    protected virtual int NormalizeTextSelectionClickCount(ControlBase? originalSource, int clickCount)
    {
        _ = originalSource;
        return clickCount;
    }

    /// <summary>Normalizes one routed click count through component policy.</summary>
    internal int GetTextSelectionClickCount(ControlBase? originalSource, int clickCount) =>
        NormalizeTextSelectionClickCount(originalSource, clickCount);

    /// <summary>Publishes a completed shared click to component policy.</summary>
    internal void CompleteTextSelectionClick(
        ControlBase? originalSource,
        Point pressCells,
        Point releaseCells,
        int clickCount,
        PointerEventArgs eventArgs) =>
        OnTextSelectionClickCompleted(originalSource, pressCells, releaseCells, clickCount, eventArgs);

    /// <summary>Transfers and releases child capture when a handled route closes a potential drag.</summary>
    internal void ReleasePotentialTextSelectionChildCapture(ControlBase? originalSource)
    {
        for (var current = originalSource;
             current is not null && !ReferenceEquals(current, this);
             current = current.Parent)
        {
            if (!current.HasPointerCapture)
            {
                continue;
            }

            if (CaptureTextSelectionPointer())
            {
                ReleaseTextSelectionPointerCapture();
            }

            return;
        }
    }

    /// <summary>Gets the semantic and ordered-source identity of the current common projection.</summary>
    internal ulong TextSelectionFingerprint => GetTextSelectionMap().Fingerprint;

    /// <summary>Finds the innermost eligible nested selectable viewport containing one cell.</summary>
    internal TextSelectionSource? TextSelectionSourceAt(Point cells)
    {
        var map = GetTextSelectionMap();

        for (var index = map.Sources.Count - 1; index >= 0; index--)
        {
            var source = map.Sources[index];

            if (IsTextSelectionSourceEligible(source) &&
                TryGetTextSelectionSourceBounds(source, out var bounds) &&
                bounds.Contains(cells))
            {
                return source;
            }
        }

        return null;
    }

    /// <summary>Associates a press with the nearest authoritative embedded selectable source.</summary>
    internal virtual TextSelectionSource? GetTextSelectionSource(ControlBase? originalSource, Point cells)
    {
        _ = originalSource;
        return TextSelectionSourceAt(cells);
    }

    /// <summary>Reconciles one captured nested source occurrence against the current projection.</summary>
    internal TextSelectionSource? ResolveTextSelectionSource(TextSelectionSource? source, Point cells)
    {
        if (source is null)
        {
            return TextSelectionSourceAt(cells);
        }

        var candidate = GetTextSelectionMap().ResolveSourceOccurrence(source);
        return candidate is not null && IsTextSelectionSourceEligible(candidate)
            ? candidate
            : TextSelectionSourceAt(cells);
    }

    /// <summary>Gets whether an active drag lies beyond a nested, owner, or ancestor viewport.</summary>
    internal bool HasTextSelectionAutoScrollRequest(Point cells, TextSelectionSource? source) =>
        ResolveTextSelectionAutoScroll(cells, source, apply: false, out _);

    /// <summary>Offers one edge-scroll attempt from the innermost viewport outward.</summary>
    internal bool AutoScrollTextSelection(
        Point cells,
        TextSelectionSource? source,
        out Point hitAdjustment) =>
        ResolveTextSelectionAutoScroll(cells, source, apply: true, out hitAdjustment);

    private bool ResolveTextSelectionAutoScroll(
        Point cells,
        TextSelectionSource? source,
        bool apply,
        out Point hitAdjustment)
    {
        hitAdjustment = default;
        var hasPropagatedRequest = false;
        var propagatedHorizontal = 0;
        var propagatedVertical = 0;

        if (source is { Viewport: { } sourceViewport } &&
            TryGetTextSelectionSourceBounds(source, out var sourceBounds))
        {
            var (sourceHorizontal, sourceVertical) = TextSelectionAutoScrollDelta(cells, sourceBounds);
            hasPropagatedRequest = sourceHorizontal != 0 || sourceVertical != 0;
            propagatedHorizontal = sourceHorizontal;
            propagatedVertical = sourceVertical;

            if (hasPropagatedRequest &&
                (!apply || sourceViewport.ScrollSelectableTextViewport(sourceHorizontal, sourceVertical)))
            {
                return true;
            }
        }

        if (this is ISelectableTextViewport ownerViewport)
        {
            var local = ownerViewport.SelectableTextViewport;
            var ownerBounds = new Rect(
                Bounds.X.SaturatingAdd(local.X),
                Bounds.Y.SaturatingAdd(local.Y),
                local.Width,
                local.Height);
            var (ownerHorizontal, ownerVertical) = hasPropagatedRequest
                ? (propagatedHorizontal, propagatedVertical)
                : TextSelectionAutoScrollDelta(cells, ownerBounds);

            if (ownerHorizontal != 0 || ownerVertical != 0)
            {
                hasPropagatedRequest = true;
                propagatedHorizontal = ownerHorizontal;
                propagatedVertical = ownerVertical;

                if (!apply)
                {
                    return true;
                }

                if (ScrollTextSelectionViewport(ownerHorizontal, ownerVertical, out hitAdjustment))
                {
                    return true;
                }
            }
        }

        for (var current = this; current is not null; current = current.Parent)
        {
            if (!ReferenceEquals(current, this) && !AllowsModalAncestor(current))
            {
                break;
            }

            if (current is not Container
                {
                    AutoScroll: true,
                    EffectiveIsEnabled: true,
                    EffectiveIsVisible: true
                } container)
            {
                continue;
            }

            var viewport = new Rect(
                container.ContentBounds.X,
                container.ContentBounds.Y,
                container.Viewport.Width,
                container.Viewport.Height);
            var (horizontal, vertical) = hasPropagatedRequest
                ? (propagatedHorizontal, propagatedVertical)
                : TextSelectionAutoScrollDelta(cells, viewport);
            horizontal = (container.ScrollBars & ScrollBars.Horizontal) != 0 ? horizontal : 0;
            vertical = (container.ScrollBars & ScrollBars.Vertical) != 0 ? vertical : 0;

            if (horizontal == 0 && vertical == 0)
            {
                continue;
            }

            hasPropagatedRequest = true;

            if (!apply)
            {
                return true;
            }

            var previousHorizontal = container.HorizontalOffset;
            var previousVertical = container.VerticalOffset;

            if (container.ScrollBy(horizontal, vertical, ScrollCause.Pointer))
            {
                hitAdjustment = new Point(
                    container.HorizontalOffset.SaturatingSubtract(previousHorizontal),
                    container.VerticalOffset.SaturatingSubtract(previousVertical));
                return true;
            }
        }

        return hasPropagatedRequest && !apply;
    }

    /// <summary>Scrolls this owner's selectable viewport and reports deferred hit-test translation.</summary>
    protected virtual bool ScrollTextSelectionViewport(int horizontal, int vertical, out Point hitAdjustment)
    {
        hitAdjustment = new Point(horizontal, vertical);
        return this is ISelectableTextViewport viewport &&
               viewport.ScrollSelectableTextViewport(horizontal, vertical);
    }

    private bool IsTextSelectionSourceEligible(TextSelectionSource source) =>
        source.Viewport is not null &&
        source.Source is ControlBase
        {
            IsDisposed: false,
            EffectiveIsEnabled: true,
            EffectiveIsVisible: true
        } control &&
        IsTextSelectionDescendant(control, this);

    private static bool IsTextSelectionDescendant(ControlBase control, ControlBase ancestor)
    {
        for (var current = control; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryGetTextSelectionSourceBounds(TextSelectionSource source, out Rect bounds)
    {
        if (source.Viewport is null || source.Source is not ControlBase { IsDisposed: false } control)
        {
            bounds = default;
            return false;
        }

        var local = source.Viewport.SelectableTextViewport;
        var raw = new Rect(
            control.Bounds.X.SaturatingAdd(local.X),
            control.Bounds.Y.SaturatingAdd(local.Y),
            local.Width,
            local.Height);
        bounds = raw.Intersect(GetDescendantSelectableTextInheritedClip(control));
        return bounds.Width > 0 && bounds.Height > 0;
    }

    private static (int Horizontal, int Vertical) TextSelectionAutoScrollDelta(Point cells, Rect viewport) =>
        (TextSelectionAutoScrollDelta(cells.X, viewport.X, viewport.Width),
         TextSelectionAutoScrollDelta(cells.Y, viewport.Y, viewport.Height));

    [Pure]
    private static int TextSelectionAutoScrollDelta(int coordinate, int origin, int length)
    {
        if (length <= 0)
        {
            return 0;
        }

        if (coordinate < origin)
        {
            return -(int) Math.Clamp((long) origin - coordinate, 1, 8);
        }

        var end = (long) origin + length;
        return coordinate >= end
            ? (int) Math.Clamp(coordinate - end + 1, 1, 8)
            : 0;
    }

    /// <summary>Commits one already hit-tested pointer range.</summary>
    /// <param name="anchor">The grapheme-aligned anchor.</param>
    /// <param name="caret">The grapheme-aligned active endpoint.</param>
    internal void CommitPointerTextSelection(int anchor, int caret)
    {
        _textSelectionDesiredColumn = null;
        _textSelectionDesiredRow = null;
        _textSelectionCaretEstablished = true;
        _textSelectionFingerprint = GetTextSelectionMap().Fingerprint;
        _ = CommitTextSelection(new TextSelection(anchor, caret));
    }

    /// <summary>Commits the shared single-, double-, or triple-click selection command.</summary>
    /// <param name="caret">The grapheme-aligned endpoint under the click.</param>
    /// <param name="clickCount">The positive consecutive primary-click count.</param>
    internal void CommitTextSelectionClick(int caret, int clickCount)
    {
        var map = GetTextSelectionMap();
        var selection = clickCount switch
        {
            >= 3 => new TextSelection(
                map.VisualLineBoundary(caret, end: false),
                map.VisualLineBoundary(caret, end: true)),
            2 => Edit.SelectWord(map.Text, caret),
            _ => new TextSelection(caret, caret)
        };

        _textSelectionDesiredColumn = null;
        _textSelectionDesiredRow = null;
        _textSelectionCaretEstablished = true;
        _textSelectionFingerprint = map.Fingerprint;
        _ = CommitTextSelection(selection);
    }

    /// <summary>Transfers pointer capture to this text-selection owner.</summary>
    /// <returns>True when capture is owned.</returns>
    internal bool CaptureTextSelectionPointer() => CapturePointer();

    /// <summary>Releases pointer capture when this text-selection owner holds it.</summary>
    internal void ReleaseTextSelectionPointerCapture()
    {
        if (HasPointerCapture)
        {
            ReleasePointerCapture();
        }
    }

    private void OnTextSelectionPointerRouted(object? sender, PointerEventArgs eventArgs)
    {
        _ = sender;

        if (!IsTextSelectionEnabled ||
            eventArgs.Phase != RoutingPhase.Preview ||
            !EffectiveIsEnabled ||
            !EffectiveIsVisible)
        {
            return;
        }

        if (TextSelectionPhase != TextSelectionGesturePhase.Idle)
        {
            _textSelectionGesture?.Handle(eventArgs);
            return;
        }

        if (!IsNearestTextSelectionOwner(eventArgs.OriginalSource) ||
            eventArgs.Pointer.Cells is not { } cells ||
            !IsTextSelectionPointerTarget(eventArgs.OriginalSource, cells))
        {
            return;
        }

        _textSelectionGesture?.Handle(eventArgs);
    }

    private void OnTextSelectionKeyRouted(object? sender, KeyEventArgs eventArgs)
    {
        _ = sender;

        if (!IsTextSelectionEnabled ||
            eventArgs.IsHandled ||
            eventArgs.Phase != RoutingPhase.Bubble ||
            (Dispatcher is not null && !ContainsFocus) ||
            !EffectiveIsEnabled ||
            !EffectiveIsVisible ||
            !IsNearestTextSelectionOwner(eventArgs.OriginalSource) ||
            !eventArgs.IsKeyDown)
        {
            return;
        }

        var stroke = eventArgs.Stroke;
        var modifiers = stroke.Modifiers & ~(Modifiers.CapsLock | Modifiers.NumLock);

        if (stroke.Code == Code.Character &&
            stroke.Character is { } character &&
            Rune.ToLowerInvariant(character) == new Rune('a') &&
            KeyboardModifierPolicy.MatchesCommand(stroke.Modifiers, Modifiers.Control))
        {
            SelectAllText();
            eventArgs.IsHandled = true;
            return;
        }

        if (!_textSelectionCaretEstablished)
        {
            return;
        }

        var extend = (modifiers & Modifiers.Shift) != 0;
        var word = (modifiers & Modifiers.Control) != 0;

        if ((modifiers & ~(Modifiers.Shift | Modifiers.Control)) != 0 ||
            stroke.Code is not (
                Code.Left or Code.Right or Code.Up or Code.Down or
                Code.Home or Code.End or Code.PageUp or Code.PageDown))
        {
            return;
        }

        var caret = MoveTextSelectionCaret(stroke.Code, extend, word);
        CommitTextSelectionNavigation(extend
            ? new TextSelection(CommittedTextSelection.Anchor, caret)
            : new TextSelection(caret, caret));
        RevealTextSelectionCaret(caret);
        eventArgs.IsHandled = true;
    }

    /// <summary>Commits one keyboard navigation result against the component's authoritative text.</summary>
    /// <param name="selection">The validated directional result.</param>
    protected virtual void CommitTextSelectionNavigation(TextSelection selection) =>
        _ = CommitTextSelection(selection);

    /// <summary>Resolves one common keyboard navigation command against the component projection.</summary>
    /// <param name="code">The supported navigation key.</param>
    /// <param name="extend">Whether the anchor remains fixed.</param>
    /// <param name="word">Whether horizontal movement uses Unicode word boundaries.</param>
    /// <returns>The grapheme-aligned target caret.</returns>
    protected virtual int MoveTextSelectionCaret(Code code, bool extend, bool word)
    {
        var map = GetTextSelectionMap();
        _textSelectionFingerprint = map.Fingerprint;

        if (code is Code.Left or Code.Right)
        {
            _textSelectionDesiredColumn = null;
            _textSelectionDesiredRow = null;

            return !extend && !CommittedTextSelection.IsEmpty
                ? code == Code.Left ? CommittedTextSelection.Start : CommittedTextSelection.End
                : word
                    ? code == Code.Left
                        ? Edit.MovePreviousWord(map.Text, CommittedTextSelection, extend).Selection.Caret
                        : Edit.MoveNextWord(map.Text, CommittedTextSelection, extend).Selection.Caret
                    : code == Code.Left
                        ? map.PreviousBoundary(CommittedTextSelection.Caret)
                        : map.NextBoundary(CommittedTextSelection.Caret);
        }

        if (code is Code.Home or Code.End)
        {
            _textSelectionDesiredColumn = null;
            _textSelectionDesiredRow = null;
            return map.VisualLineBoundary(CommittedTextSelection.Caret, end: code == Code.End);
        }

        if (!map.TryGetVisualPosition(CommittedTextSelection.Caret, out var row, out var column))
        {
            return CommittedTextSelection.Caret;
        }

        _textSelectionDesiredColumn ??= column;
        _textSelectionDesiredRow ??= row;
        var direction = code is Code.Up or Code.PageUp ? -1 : 1;
        var distance = code is Code.PageUp or Code.PageDown
            ? TextSelectionPageDistance()
            : 1;
        _textSelectionDesiredRow = (int) Math.Clamp(
            (long) _textSelectionDesiredRow.Value + (direction * distance),
            0,
            Math.Max(0, map.VisualRowCount - 1));
        return map.OffsetAtVisualColumn(_textSelectionDesiredRow.Value, _textSelectionDesiredColumn.Value);
    }

    /// <summary>Gets the visual-row distance used by common page selection navigation.</summary>
    protected virtual int TextSelectionPageDistance() => this is Container container
        ? (int) Math.Clamp((long) container.Viewport.Height - container.PageOverlap, 1, int.MaxValue)
        : Math.Max(1, Bounds.Height);

    private void OnTextSelectionTerminalFocusRouted(object? sender, TerminalFocusEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.Phase == RoutingPhase.Preview && !eventArgs.Focus.Gained)
        {
            _textSelectionGesture?.Cancel(releaseCapture: false);
        }
    }

    private bool IsNearestTextSelectionOwner(ControlBase? originalSource)
    {
        if (OwnsDescendantTextSelectionGestures &&
            originalSource is not null &&
            !ReferenceEquals(originalSource, this))
        {
            return true;
        }

        for (var current = originalSource; current is not null; current = current.Parent)
        {
            if (current.IsTextSelectionEnabled)
            {
                return ReferenceEquals(current, this);
            }

            if (ReferenceEquals(current, this))
            {
                return true;
            }
        }

        return false;
    }

    private void RenderTextSelectionAdornment(TerminalCanvas canvas)
    {
        if (!IsTextSelectionEnabled || CommittedTextSelection.IsEmpty)
        {
            return;
        }

        var map = ReconcileTextSelectionMap();

        RenderTextSelectionAdornmentCore(canvas, map);
    }

    private void RenderTextSelectionAdornmentCore(TerminalCanvas canvas, TextSelectionMap map)
    {

        foreach (var glyph in map.Glyphs)
        {
            if (glyph.Range.Start < CommittedTextSelection.Start || glyph.Range.End > CommittedTextSelection.End)
            {
                continue;
            }

            canvas.ApplyCellStyle(
                GetTextSelectionAdornmentBounds(glyph.Bounds),
                (_, current) => ApplyTextSelectionStyle(current));
        }
    }

}
