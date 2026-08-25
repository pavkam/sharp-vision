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
    private TextSelection _textSelection;
    private int? _textSelectionDesiredColumn;
    private ulong _textSelectionFingerprint;

    /// <summary>Raised synchronously after the directional semantic-text selection changes.</summary>
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
            if (!SetProperty(ref field, value, InvalidationImpact.Render))
            {
                return;
            }

            if (!value)
            {
                _textSelectionGesture?.Cancel(releaseCapture: true);
                _ = CommitTextSelection(default);
            }
            else
            {
                _textSelectionGesture = new TextSelectionGesture(this);
            }

            OnTextSelectionEnabledChanged(value);
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
            return _textSelection;
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
            return _textSelection.IsEmpty
                ? string.Empty
                : map.Text.Substring(_textSelection.Start, _textSelection.Length);
        }
    }

    /// <summary>Creates this control's complete semantic text and visible grapheme geometry.</summary>
    /// <returns>An independently owned snapshot in this control's local cell coordinates.</returns>
    /// <exception cref="InvalidOperationException">The attached control is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public virtual SelectableTextSnapshot GetSelectableTextSnapshot()
    {
        VerifyMutable();
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
        _ = CommitTextSelection(new TextSelection(_textSelection.Caret, _textSelection.Caret));
    }

    /// <summary>Returns selected semantic text without publishing clipboard or terminal state.</summary>
    /// <returns>An independently owned string, or empty when the selection is collapsed.</returns>
    /// <exception cref="InvalidOperationException">The attached control is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    [Pure]
    public virtual string CopySelectedText() => SelectedText;

    /// <summary>Commits a validated selection and publishes its common post-commit event.</summary>
    /// <param name="selection">The validated directional selection.</param>
    /// <returns>True when the committed value changed; otherwise false.</returns>
    protected bool CommitTextSelection(TextSelection selection)
    {
        if (_textSelection == selection)
        {
            return false;
        }

        var previous = _textSelection;
        _textSelection = selection;
        Invalidate(Invalidation.Render);
        TextSelectionChanged?.Invoke(this, new TextSelectionChangedEventArgs(previous, selection));
        return true;
    }

    /// <summary>Publishes a specialized control's already committed text-selection transition.</summary>
    /// <param name="previous">The previous validated directional selection.</param>
    /// <param name="selection">The current validated directional selection.</param>
    protected void PublishTextSelectionChanged(TextSelection previous, TextSelection selection)
    {
        if (previous != selection)
        {
            TextSelectionChanged?.Invoke(this, new TextSelectionChangedEventArgs(previous, selection));
        }
    }

    /// <summary>Responds after the opt-in selection capability state changes.</summary>
    /// <param name="enabled">The committed capability state.</param>
    protected virtual void OnTextSelectionEnabledChanged(bool enabled) => _ = enabled;

    /// <summary>Gets whether this control uses the common gesture and adornment controller.</summary>
    /// <remarks>
    /// Specialized text controls may retain an editing or semantic-projection controller while
    /// adapting its state to the inherited public contract.
    /// </remarks>
    protected virtual bool UsesTextSelectionController => true;

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

    /// <summary>Builds the indexed semantic map used by common text-selection behavior.</summary>
    /// <returns>An immutable current map in this control's local cell coordinates.</returns>
    internal virtual TextSelectionMap GetTextSelectionMap()
    {
        var children = new List<ControlBase>();

        if (AddSelectableTextChildren(children))
        {
            return SelectableTextAggregation.CreateMap(this);
        }

        var snapshot = GetSelectableTextSnapshot();
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

    private TextSelectionMap ReconcileTextSelectionMap()
    {
        var map = GetTextSelectionMap();

        if (_textSelection == default || _textSelectionFingerprint == map.Fingerprint)
        {
            _textSelectionFingerprint = map.Fingerprint;
            return map;
        }

        var previous = _textSelection;
        _textSelection = default;
        _textSelectionDesiredColumn = null;
        _textSelectionFingerprint = map.Fingerprint;
        Invalidate(Invalidation.Render);
        TextSelectionChanged?.Invoke(this, new TextSelectionChangedEventArgs(previous, default));
        return map;
    }

    /// <summary>Resolves one screen cell to the nearest semantic grapheme endpoint.</summary>
    /// <param name="cells">The screen-cell pointer coordinate.</param>
    /// <returns>A grapheme-aligned UTF-16 endpoint.</returns>
    internal int HitTestTextSelection(Point cells)
    {
        var local = new Point(
            SaturatingCoordinateDifference(cells.X, Bounds.X),
            SaturatingCoordinateDifference(cells.Y, Bounds.Y));
        return GetTextSelectionMap().HitTest(local);
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

        if (source is { Viewport: { } sourceViewport } &&
            TryGetTextSelectionSourceBounds(source, out var sourceBounds))
        {
            var (sourceHorizontal, sourceVertical) = TextSelectionAutoScrollDelta(cells, sourceBounds);
            hasPropagatedRequest = sourceHorizontal != 0 || sourceVertical != 0;

            if (hasPropagatedRequest &&
                (!apply || sourceViewport.ScrollSelectableTextViewport(sourceHorizontal, sourceVertical)))
            {
                return true;
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
            var (horizontal, vertical) = TextSelectionAutoScrollDelta(cells, viewport);
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
                    SaturatingCoordinateDifference(container.HorizontalOffset, previousHorizontal),
                    SaturatingCoordinateDifference(container.VerticalOffset, previousVertical));
                return true;
            }
        }

        return hasPropagatedRequest && !apply;
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
            SaturatingCoordinateSum(control.Bounds.X, local.X),
            SaturatingCoordinateSum(control.Bounds.Y, local.Y),
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
        _textSelectionFingerprint = GetTextSelectionMap().Fingerprint;
        _ = CommitTextSelection(new TextSelection(anchor, caret));
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
            !UsesTextSelectionController ||
            eventArgs.Phase != RoutingPhase.Preview ||
            !EffectiveIsEnabled ||
            !EffectiveIsVisible ||
            !IsNearestTextSelectionOwner(eventArgs.OriginalSource))
        {
            return;
        }

        _textSelectionGesture?.Handle(eventArgs);
    }

    private void OnTextSelectionKeyRouted(object? sender, KeyEventArgs eventArgs)
    {
        _ = sender;

        if (!IsTextSelectionEnabled ||
            !UsesTextSelectionController ||
            eventArgs.IsHandled ||
            eventArgs.Phase != RoutingPhase.Bubble ||
            !ContainsFocus ||
            !EffectiveIsEnabled ||
            !EffectiveIsVisible ||
            !IsNearestTextSelectionOwner(eventArgs.OriginalSource) ||
            eventArgs.Stroke.Action is not (KeyAction.Press or KeyAction.Repeat))
        {
            return;
        }

        var stroke = eventArgs.Stroke;
        var modifiers = stroke.Modifiers & ~(Modifiers.CapsLock | Modifiers.NumLock);

        if (stroke.Code == Code.Character &&
            stroke.Character is { } character &&
            Rune.ToLowerInvariant(character) == new Rune('a') &&
            modifiers == Modifiers.Control)
        {
            SelectAllText();
            eventArgs.IsHandled = true;
            return;
        }

        if (modifiers != Modifiers.Shift || stroke.Code is not (
                Code.Left or Code.Right or Code.Up or Code.Down or
                Code.Home or Code.End or Code.PageUp or Code.PageDown))
        {
            return;
        }

        var map = GetTextSelectionMap();
        var caret = MoveTextSelectionCaret(map, stroke.Code);
        _textSelectionFingerprint = map.Fingerprint;
        _ = CommitTextSelection(new TextSelection(_textSelection.Anchor, caret));
        eventArgs.IsHandled = true;
    }

    private int MoveTextSelectionCaret(TextSelectionMap map, Code code)
    {
        if (code is Code.Left or Code.Right)
        {
            _textSelectionDesiredColumn = null;
            return code == Code.Left
                ? map.PreviousBoundary(_textSelection.Caret)
                : map.NextBoundary(_textSelection.Caret);
        }

        if (code is Code.Home or Code.End)
        {
            _textSelectionDesiredColumn = null;
            return map.VisualLineBoundary(_textSelection.Caret, end: code == Code.End);
        }

        if (!map.TryGetVisualPosition(_textSelection.Caret, out var row, out var column))
        {
            return _textSelection.Caret;
        }

        _textSelectionDesiredColumn ??= column;
        var direction = code is Code.Up or Code.PageUp ? -1 : 1;
        var distance = code is Code.PageUp or Code.PageDown
            ? TextSelectionPageDistance()
            : 1;
        return map.OffsetAtVisualColumn(row + (direction * distance), _textSelectionDesiredColumn.Value);
    }

    private int TextSelectionPageDistance() => this is Container container
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
        if (!IsTextSelectionEnabled || !UsesTextSelectionController)
        {
            return;
        }

        var map = ReconcileTextSelectionMap();

        if (_textSelection.IsEmpty)
        {
            return;
        }

        RenderTextSelectionAdornmentCore(canvas, map);
    }

    private void RenderTextSelectionAdornmentCore(TerminalCanvas canvas, TextSelectionMap map)
    {

        var foreground = ResolveColor(new ControlColor(SemanticColor.SelectedText));
        var background = ResolveColor(new ControlColor(SemanticColor.SelectedControl));

        foreach (var glyph in map.Glyphs)
        {
            if (glyph.Range.Start < _textSelection.Start || glyph.Range.End > _textSelection.End)
            {
                continue;
            }

            canvas.ApplyCellStyle(
                new Rect(
                    SaturatingCoordinateSum(Bounds.X, glyph.Bounds.X),
                    SaturatingCoordinateSum(Bounds.Y, glyph.Bounds.Y),
                    glyph.Bounds.Width,
                    glyph.Bounds.Height),
                (_, current) => new TerminalStyle(
                    foreground,
                    background,
                    current.Attributes,
                    current.Hyperlink,
                    current.Underline,
                    current.UnderlineColor));
        }
    }

    [Pure]
    private static int SaturatingCoordinateDifference(int left, int right) =>
        (int) Math.Clamp((long) left - right, int.MinValue, int.MaxValue);

    [Pure]
    private static int SaturatingCoordinateSum(int left, int right) =>
        (int) Math.Clamp((long) left + right, int.MinValue, int.MaxValue);
}
