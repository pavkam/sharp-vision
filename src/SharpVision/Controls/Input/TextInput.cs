// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using Scrolling;

using SharpVision.Terminal.Input;

using Terminal.Rendering;

using Text;

using UnicodeWidth = Width;

/// <summary>Defines a focusable grapheme-safe single- or multiline text editor.</summary>
[PublicAPI]
public sealed class TextInput: Control
{
    private readonly List<EditResult> _undo = [];
    private readonly List<EditResult> _redo = [];
    private string _text = string.Empty;
    private Selection _selection;
    private int[]? _boundaryRowCache;
    private int[]? _boundaryColumnCache;
    private string? _boundaryCacheSource;
    private Rune? _boundaryCachePasswordCharacter;
    private Policy? _boundaryCacheCellPolicy;
    private bool _pointerSelecting;
    private int _pointerAnchor;
    private int _contentWidth;
    private int _contentHeight = 1;
    private readonly OwnedControlSlot _chrome;
    private readonly ScrollBar _horizontal;
    private readonly ScrollBar _vertical;
    private Rect _editorBounds;
    private VisualLine[] _visualLines = [];

    /// <summary>Initializes an empty focusable single-line editor with a light one-cell border.</summary>
    public TextInput()
    {
        _chrome = RegisterOwnedSlot(
            new OwnedControlOptions(
                OwnedControlRole.FrameworkPart,
                OwnedControlLayer.Normal,
                participatesInHitTesting: true,
                participatesInNavigation: false,
                partKey: "editor-scroll-bars",
                InvalidationImpact.Arrange),
            capacity: 2);
        _horizontal = new ScrollBar { Orientation = Orientation.Horizontal };
        _vertical = new ScrollBar { Orientation = Orientation.Vertical };
        _horizontal.ValueChanged += OnHorizontalChanged;
        _vertical.ValueChanged += OnVerticalChanged;
        _chrome.Add(_horizontal);
        _chrome.Add(_vertical);
        Focusable = true;
        TabStop = true;
        ContextMenu = new TextInputContextMenu(this);
    }

    /// <inheritdoc/>
    protected override ThemeRole ThemeRole => ThemeRole.Input;

    /// <inheritdoc/>
    protected override InvalidationImpact GetThemeChangeImpact(
        Theme? previous,
        Theme? current,
        Face? previousParentAmbientFace,
        Face? currentParentAmbientFace)
    {
        var appearanceImpact = base.GetThemeChangeImpact(
            previous,
            current,
            previousParentAmbientFace,
            currentParentAmbientFace);
        var previousStyle = ScrollBar.ResolveStyle(ScrollBarStyle, previous);
        var currentStyle = ScrollBar.ResolveStyle(ScrollBarStyle, current);
        var styleImpact = previousStyle.Chrome != currentStyle.Chrome
            ? InvalidationImpact.Measure
            : previousStyle == currentStyle
                ? InvalidationImpact.None
                : InvalidationImpact.Render;

        return MaximumImpact(appearanceImpact, styleImpact);
    }

    /// <inheritdoc/>
    protected override string? GetThemeResolvedStylePropertyName(Theme? previous, Theme? current) =>
        ScrollBarStyle is null &&
        ScrollBar.ResolveStyle(ScrollBarStyle, previous) != ScrollBar.ResolveStyle(ScrollBarStyle, current)
            ? nameof(ActualScrollBarStyle)
            : null;

    /// <inheritdoc/>
    /// <summary>Raised before a text mutation and cancellable before commit.</summary>
    public event EventHandler<TextChangingEventArgs>? TextChanging;

    /// <summary>Raised after text and selection commit atomically.</summary>
    public event EventHandler<TextChangedEventArgs>? TextChanged;

    /// <summary>Raised after a changed directional selection commits.</summary>
    public event EventHandler<InputSelectionChangedEventArgs>? SelectionChanged;

    /// <summary>Raised when Enter submits a single-line editor.</summary>
    public event EventHandler<SubmittedEventArgs>? Submitted;

    /// <summary>Gets or sets non-null owned text under current return/tab/maximum policy.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="ArgumentException">The value violates policy or maximum length.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public string Text
    {
        get => _text;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            var validated = Edit.Replace(
                string.Empty,
                default,
                value,
                MaxLength,
                AcceptsReturn,
                AcceptsTab);

            if (!string.Equals(validated.Text, value, StringComparison.Ordinal))
            {
                throw new ArgumentException("Text exceeds MaxLength.", nameof(value));
            }

            _ = Commit(new EditResult(value, new Selection(value.Length, value.Length), true), true);
        }
    }

    /// <summary>Gets or sets optional placeholder text shown when the input is empty.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public string? Placeholder
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.Render);
    }

    /// <summary>Gets or sets whether user input may mutate text.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool IsReadOnly
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.Render);
    }

    /// <summary>Gets or sets whether inserted CR or LF values are accepted.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool AcceptsReturn
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.Measure);
    }

    /// <summary>Gets or sets whether inserted tab values are accepted.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool AcceptsTab
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.Measure);
    }

    /// <summary>Gets or sets the optional printable narrow display mask.</summary>
    /// <exception cref="ArgumentException">The value is a control or not one cell wide.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Rune? PasswordCharacter
    {
        get;
        set
        {
            if (value is { } mask)
            {
                _ = Edit.ProjectPassword(string.Empty, mask);
            }

            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
        }
    }

    /// <summary>Gets or sets zero for unlimited or a positive maximum grapheme count.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="ArgumentException">The current text exceeds a non-zero value.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public int MaxLength
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);

            if (value > 0 && Edit.GraphemeCount(Text) > value)
            {
                throw new ArgumentException("MaxLength cannot exclude current text.", nameof(value));
            }

            _ = SetProperty(ref field, value, InvalidationImpact.None);
        }
    }

    /// <summary>Gets or sets the collapsed caret at a grapheme boundary.</summary>
    /// <exception cref="ArgumentException">The value splits a grapheme.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The value exceeds text.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public int CaretIndex
    {
        get => _selection.Caret;
        set => SetSelection(new Selection(value, value));
    }

    /// <summary>Gets or sets the normalized selection start.</summary>
    /// <exception cref="ArgumentException">An endpoint splits a grapheme.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The resulting range exceeds text.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public int SelectionStart
    {
        get => _selection.Start;
        set => Select(value, SelectionLength);
    }

    /// <summary>Gets or sets the normalized selection length with caret at the range end.</summary>
    /// <exception cref="ArgumentException">An endpoint splits a grapheme.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The resulting range exceeds text.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public int SelectionLength
    {
        get => _selection.Length;
        set => Select(SelectionStart, value);
    }

    /// <summary>Gets selected source text as a new owned string.</summary>
    public string SelectedText => Text.Substring(SelectionStart, SelectionLength);

    /// <summary>Gets the current horizontal cell offset.</summary>
    public int HorizontalOffset { get; private set; }

    /// <summary>Gets the current vertical line offset.</summary>
    public int VerticalOffset { get; private set; }

    /// <summary>Gets or sets the protocol-neutral cursor shape requested while this editor has focus.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public CursorShape CursorShape
    {
        get;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The cursor shape is unknown.");
            }

            _ = SetProperty(ref field, value, InvalidationImpact.Render);
        }
    }

    /// <summary>Gets or sets the axes eligible for editor overflow scrolling.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value contains unknown axis flags.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public ScrollBars ScrollBars
    {
        get;
        set
        {
            if ((value & ~ScrollBars.Both) != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    "The scrollbar axes contain unknown flags.");
            }

            if (SetProperty(ref field, value, InvalidationImpact.Arrange))
            {
                ArrangeChrome();
            }
        }
    } = ScrollBars.Both;

    /// <summary>Gets or sets the scrollbar reservation policy for enabled editor axes.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public ShowScrollBars ShowScrollBars
    {
        get;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    "The scrollbar visibility policy is unknown.");
            }

            if (SetProperty(ref field, value, InvalidationImpact.Arrange))
            {
                ArrangeChrome();
            }
        }
    } = ShowScrollBars.WhenNeeded;

    /// <summary>Gets or sets the complete local style requested for editor rails.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public ScrollBarStyle? ScrollBarStyle
    {
        get;
        set
        {
            VerifyMutable();
            if (field == value)
            {
                return;
            }

            var previous = ActualScrollBarStyle;
            field = value;
            var current = ActualScrollBarStyle;
            _horizontal.Style = value;
            _vertical.Style = value;
            NotifyPropertyChanged(
                nameof(ScrollBarStyle),
                previous.Chrome != current.Chrome
                    ? InvalidationImpact.Arrange
                    : previous == current
                        ? InvalidationImpact.None
                        : InvalidationImpact.Render);
            if (previous != current)
            {
                NotifyPropertyChanged(nameof(ActualScrollBarStyle), InvalidationImpact.None);
            }
        }
    }

    /// <summary>Gets the resolved editor-rail style.</summary>
    public ScrollBarStyle ActualScrollBarStyle => ScrollBar.ResolveStyle(ScrollBarStyle, Theme);

    /// <summary>Gets or sets the maximum retained undo snapshots.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public int UndoLimit
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _ = SetProperty(ref field, value, InvalidationImpact.None);
            Trim(_undo);
        }
    } = 100;

    /// <summary>Gets or sets whether long lines are visually wrapped at word boundaries.</summary>
    public bool WordWrap
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.Measure);
    }

    /// <summary>Gets whether one undo snapshot is available.</summary>
    public bool CanUndo => _undo.Count > 0;

    /// <summary>Gets whether one redo snapshot is available.</summary>
    public bool CanRedo => _redo.Count > 0;

    /// <summary>Selects a normalized grapheme-aligned range with caret at its end.</summary>
    /// <param name="start">The UTF-16 start boundary.</param>
    /// <param name="length">The non-negative UTF-16 range length.</param>
    /// <exception cref="ArgumentOutOfRangeException">The range overflows or exceeds text.</exception>
    /// <exception cref="ArgumentException">An endpoint splits a grapheme.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public void Select(int start, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        var end = checked(start + length);
        SetSelection(new Selection(start, end));
    }

    /// <summary>Copies selected text unless password policy suppresses source disclosure.</summary>
    /// <returns>An owned selected string, or empty when no selection or password masking is active.</returns>
    /// <exception cref="InvalidOperationException">The attached control is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public string CopySelection()
    {
        VerifyMutable();
        return PasswordCharacter.HasValue ? string.Empty : SelectedText;
    }

    /// <summary>Copies and deletes selection unless read-only or password policy suppresses cutting.</summary>
    /// <returns>The owned copied text, or empty when password masking is active.</returns>
    /// <exception cref="InvalidOperationException">The attached control is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public string CutSelection()
    {
        var copied = CopySelection();

        if (copied.Length > 0 && !IsReadOnly)
        {
            _ = Commit(Edit.Delete(Text, _selection), true);
        }

        return copied;
    }

    /// <summary>Replaces the current selection, or inserts at the caret when there is none, through
    /// the same edit transaction every other edit path uses.</summary>
    /// <param name="value">The non-null replacement text.</param>
    /// <returns>
    /// True when the edit committed; false when read-only, rejected by policy (a disallowed control
    /// character, or retained length exceeding <see cref="MaxLength"/>), or cancelled by
    /// <see cref="TextChanging"/>.
    /// </returns>
    /// <remarks>
    /// Reuses the control's ordinary validation, <see cref="MaxLength"/> truncation, grapheme-safe
    /// boundaries, undo recording, <see cref="TextChanging"/>/<see cref="TextChanged"/> sequencing,
    /// and scroll repair — the same primitive keyboard input, bracketed paste, context-menu paste,
    /// and cut already route through. This is the composition seam for virtual keyboards, clipboard
    /// adapters, input-method components, and find/replace UI that need to edit content without
    /// reconstructing <see cref="Text"/> externally and bypassing those guarantees.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool ReplaceSelection(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        VerifyMutable();
        return Insert(value);
    }

    /// <summary>Inserts application-owned clipboard text through the normal edit transaction.</summary>
    /// <param name="value">The non-null clipboard text.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The attached control is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    internal void PasteClipboard(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        VerifyMutable();
        _ = Insert(value);
    }

    /// <summary>Restores the newest retained undo snapshot.</summary>
    /// <returns>True when a snapshot committed.</returns>
    /// <exception cref="InvalidOperationException">The attached control is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool Undo() => Restore(_undo, _redo);

    /// <summary>Restores the newest retained redo snapshot.</summary>
    /// <returns>True when a snapshot committed.</returns>
    /// <exception cref="InvalidOperationException">The attached control is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool Redo() => Restore(_redo, _undo);

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        if (WordWrap && constraint.Width is { } maxWidth)
        {
            BuildVisualLines(maxWidth);
            _contentHeight = _visualLines.Length;
            _contentWidth = 0;

            foreach (var line in _visualLines)
            {
                _contentWidth = Math.Max(_contentWidth, line.Cells);
            }

            return new Size(maxWidth, Math.Max(1, _contentHeight));
        }

        MeasureText(out _contentWidth, out _contentHeight);
        return new Size(Math.Max(1, _contentWidth), Math.Max(1, _contentHeight));
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        _editorBounds = bounds;
        ArrangeChrome();
        EnsureCaretVisible(_editorBounds);
    }

    /// <inheritdoc/>
    internal override Control? HitTest(Point point)
    {
        return IsDisposed || !IsHitTestVisible || !EffectiveIsVisible || !EffectiveIsEnabled || !Bounds.Contains(point)
            ? null
            : _vertical.HitTest(point) ?? _horizontal.HitTest(point) ?? this;
    }

    /// <inheritdoc/>
    internal override void RenderChildren(TerminalCanvas canvas, Rect contentClip)
    {
        _horizontal.Render(canvas, contentClip);
        _vertical.Render(canvas, contentClip);
    }

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        var bounds = _editorBounds;

        if (bounds.Width == 0 || bounds.Height == 0)
        {
            return;
        }

        // An editor owns its complete visible surface so configured backgrounds
        // remain continuous beyond short text, selection, and the caret.
        canvas.Clear(bounds, ResolvedStyle);

        if (Text.Length == 0 && !IsFocused && Placeholder is { Length: > 0 } placeholder)
        {
            var placeholderStyle = PlaceholderStyle();
            var px = 0;

            foreach (var grapheme in Graphemes.Enumerate(placeholder))
            {
                var cluster = placeholder.AsSpan(grapheme.Offset, grapheme.Length);

                if (IsLineBreak(cluster))
                {
                    break;
                }

                var width = UnicodeWidth.Measure(cluster, CellPolicy.AmbiguousWidth).Cells;
                var point = new Point(bounds.X + px, bounds.Y);

                if (point.X + width > bounds.X + bounds.Width)
                {
                    break;
                }

                _ = canvas.Draw(cluster, point, placeholderStyle);
                px += width;
            }

            return;
        }

        if (WordWrap && _visualLines.Length > 0)
        {
            RenderWrapped(canvas, bounds);
        }
        else
        {
            RenderUnwrapped(canvas, bounds);
        }
    }

    private void RenderWrapped(TerminalCanvas canvas, Rect bounds)
    {
        for (var i = 0; i < _visualLines.Length; i++)
        {
            var screenY = bounds.Y + i - VerticalOffset;

            if (screenY < bounds.Y || screenY >= bounds.Y + bounds.Height)
            {
                continue;
            }

            var line = _visualLines[i];
            var span = Text.AsSpan(line.Offset, line.Length);
            var x = 0;

            foreach (var g in Graphemes.Enumerate(span))
            {
                var c = span.Slice(g.Offset, g.Length);

                if (IsLineBreak(c))
                {
                    continue;
                }

                var width = ClusterWidth(c, x);
                var sourceOffset = line.Offset + g.Offset;
                var point = new Point(bounds.X + x, screenY);
                var selected = sourceOffset < _selection.End &&
                               sourceOffset + g.Length > _selection.Start;
                var style = selected ? SelectedStyle() : ResolvedStyle;

                if (PasswordCharacter is { } mask)
                {
                    Draw(canvas, point, mask, style);
                }
                else if (c.Length == 1 && c[0] == '\t')
                {
                    DrawSpaces(canvas, point, width, style);
                }
                else
                {
                    _ = canvas.Draw(c, point, style);
                }

                x += width;
            }
        }

        if (IsFocused)
        {
            Position(_selection.Caret, out var caretX, out var caretY);
            var position = new Point(
                bounds.X + caretX,
                bounds.Y + caretY - VerticalOffset);

            if (bounds.Contains(position) && canvas.Bounds.Contains(position))
            {
                canvas.SetCursor(position, visible: true, CursorShape);
            }
        }
    }

    private void RenderUnwrapped(TerminalCanvas canvas, Rect bounds)
    {
        var x = 0;
        var y = 0;

        foreach (var grapheme in Graphemes.Enumerate(Text))
        {
            var cluster = Text.AsSpan(grapheme.Offset, grapheme.Length);

            if (IsLineBreak(cluster))
            {
                x = 0;
                y++;
                continue;
            }

            var width = ClusterWidth(cluster, x);
            var point = new Point(
                bounds.X + x - HorizontalOffset,
                bounds.Y + y - VerticalOffset);
            var selected = grapheme.Offset < _selection.End &&
                           grapheme.Offset + grapheme.Length > _selection.Start;
            var style = selected ? SelectedStyle() : ResolvedStyle;

            if (PasswordCharacter is { } mask)
            {
                Draw(canvas, point, mask, style);
            }
            else if (cluster.Length == 1 && cluster[0] == '\t')
            {
                DrawSpaces(canvas, point, width, style);
            }
            else
            {
                _ = canvas.Draw(cluster, point, style);
            }

            x += width;
        }

        if (IsFocused)
        {
            Position(_selection.Caret, out var caretX, out var caretY);
            var position = new Point(
                bounds.X + caretX - HorizontalOffset,
                bounds.Y + caretY - VerticalOffset);

            if (bounds.Contains(position) && canvas.Bounds.Contains(position))
            {
                canvas.SetCursor(position, visible: true, CursorShape);
            }
        }
    }

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);

        if (!EffectiveIsEnabled || !EffectiveIsVisible)
        {
            return;
        }

        switch (eventArgs)
        {
            case KeyEventArgs key:
                Handle(key);
                break;
            case TextEventArgs text:
                _ = Insert(text.Text.Value.ToString());
                text.Handled = true;
                break;
            case PasteEventArgs paste:
                _ = Insert(Encoding.UTF8.GetString(paste.Paste.Utf8.Span));
                paste.Handled = true;
                break;
            case PointerEventArgs pointer:
                Handle(pointer);
                break;
            default:
                break;
        }
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);
        CancelPointer(releaseCapture: false);

        if (reason == ReleaseReason.Disposed)
        {
            _horizontal.ValueChanged -= OnHorizontalChanged;
            _vertical.ValueChanged -= OnVerticalChanged;
            TextChanging = null;
            TextChanged = null;
            SelectionChanged = null;
            Submitted = null;
            _undo.Clear();
            _redo.Clear();
        }
    }

    /// <inheritdoc/>
    protected override void OnLostPointerCapture(PointerCaptureLossReason reason)
    {
        base.OnLostPointerCapture(reason);
        CancelPointer(releaseCapture: false);
    }

    /// <inheritdoc/>
    protected override void OnFocusChanged(bool focused)
    {
        base.OnFocusChanged(focused);

        if (!focused)
        {
            CancelPointer(releaseCapture: true);
        }
    }

    private bool Commit(EditResult proposal, bool recordHistory)
    {
        VerifyMutable();
        var textChanged = !string.Equals(Text, proposal.Text, StringComparison.Ordinal);

        if (!textChanged && _selection == proposal.Selection)
        {
            return false;
        }

        if (textChanged)
        {
            var changing = new TextChangingEventArgs(proposal);
            TextChanging?.Invoke(this, changing);

            if (changing.Cancel)
            {
                return false;
            }
        }

        var previousText = Text;
        var previousSelection = _selection;

        if (recordHistory && textChanged)
        {
            Push(_undo, new EditResult(previousText, previousSelection, false));
            _redo.Clear();
        }

        _text = proposal.Text;
        _selection = proposal.Selection;

        if (textChanged && WordWrap && _editorBounds.Width > 0)
        {
            BuildVisualLines(_editorBounds.Width);
        }

        EnsureCaretVisible(_editorBounds, remeasure: textChanged);

        if (textChanged)
        {
            NotifyPropertyChanged(nameof(Text), InvalidationImpact.Measure);
        }

        if (previousSelection != _selection)
        {
            NotifyPropertyChanged(nameof(CaretIndex), InvalidationImpact.Render);
            NotifyPropertyChanged(nameof(SelectionStart), InvalidationImpact.Render);
            NotifyPropertyChanged(nameof(SelectionLength), InvalidationImpact.Render);
        }

        if (textChanged)
        {
            TextChanged?.Invoke(this, new TextChangedEventArgs(previousText, Text));
        }

        if (previousSelection != _selection)
        {
            SelectionChanged?.Invoke(
                this,
                new InputSelectionChangedEventArgs(previousSelection, _selection));
        }

        return true;
    }

    private void SetSelection(Selection selection)
    {
        Edit.Validate(Text, selection);
        _ = Commit(new EditResult(Text, selection, selection != _selection), false);
    }

    /// <summary>Moves or extends the caret to the previous complete grapheme in O(log n) instead of
    /// Edit.MovePreviousUnchecked's O(n) full-prefix rescan, using a lazily built and reference-
    /// invalidated boundary offset cache. Holding Left through a large document previously cost
    /// O(n) per keystroke - O(n^2) total across n keystrokes (see #42).</summary>
    private EditResult MoveCaretPrevious(bool extend)
    {
        var caret = !extend && !_selection.IsEmpty
            ? _selection.Start
            : PreviousBoundaryFast(_selection.Caret);
        var selection = extend ? new Selection(_selection.Anchor, caret) : new Selection(caret, caret);
        return selection == _selection
            ? new EditResult(Text, _selection, changed: false)
            : new EditResult(Text, selection, changed: true);
    }

    /// <summary>Finds the largest cached boundary offset strictly less than <paramref name="index"/>
    /// via binary search.</summary>
    private int PreviousBoundaryFast(int index)
    {
        var (offsets, _, _) = BoundaryCache();
        var position = Array.BinarySearch(offsets, index);
        Debug.Assert(position >= 0, "The caret is always a valid cached boundary.");
        return position > 0 ? offsets[position - 1] : 0;
    }

    /// <summary>Looks up the non-word-wrap caret cell position for <paramref name="index"/> in
    /// O(log n) via the cached boundary/row/column arrays, replacing <see cref="Position"/>'s O(n)
    /// full-prefix rescan on every keystroke (see #42).</summary>
    private void PositionFast(int index, out int x, out int y)
    {
        var (offsets, rows, columns) = BoundaryCache();
        var position = Array.BinarySearch(offsets, index);
        Debug.Assert(position >= 0, "The queried index is always a valid cached boundary.");
        x = columns[position];
        y = rows[position];
    }

    /// <summary>Gets the cached grapheme boundary offsets backing <see cref="MoveCaretPrevious"/>
    /// and <see cref="PositionFast"/>. Also exposed internally so regression coverage can assert the
    /// same array instance is reused, not rebuilt, across repeated navigation (see #42).</summary>
    internal int[]? BoundaryOffsets { get; private set; }

    /// <summary>Gets every grapheme boundary offset in <see cref="Text"/> (including 0 and the
    /// source length) paired with the non-word-wrap cell column and row at that offset, computed
    /// with one forward pass and reused while the source text and every other <see cref="ClusterWidth"/>
    /// input - <see cref="PasswordCharacter"/> and <see cref="Control.CellPolicy"/> - are unchanged.
    /// The <see cref="Text"/> check is a reference comparison, safe because every assignment routes
    /// through <see cref="Commit"/>, which either keeps the same string reference or replaces it
    /// wholesale. Holding Left, or repeatedly committing a selection-only change that must
    /// reposition the caret, previously cost O(n) per keystroke twice over - once in
    /// <c>Edit.MovePreviousUnchecked</c> and again in <see cref="Position"/> - for O(n^2) total
    /// across n keystrokes (see #42).</summary>
    private (int[] Offsets, int[] Rows, int[] Columns) BoundaryCache()
    {
        if (ReferenceEquals(_boundaryCacheSource, Text) &&
            _boundaryCachePasswordCharacter == PasswordCharacter &&
            _boundaryCacheCellPolicy == CellPolicy)
        {
            return (BoundaryOffsets!, _boundaryRowCache!, _boundaryColumnCache!);
        }

        var offsets = new List<int> { 0 };
        var rows = new List<int> { 0 };
        var columns = new List<int> { 0 };
        var x = 0;
        var y = 0;

        foreach (var grapheme in Graphemes.Enumerate(Text))
        {
            var cluster = Text.AsSpan(grapheme.Offset, grapheme.Length);

            if (IsLineBreak(cluster))
            {
                x = 0;
                y++;
            }
            else
            {
                x += ClusterWidth(cluster, x);
            }

            offsets.Add(grapheme.Offset + grapheme.Length);
            rows.Add(y);
            columns.Add(x);
        }

        BoundaryOffsets = [.. offsets];
        _boundaryRowCache = [.. rows];
        _boundaryColumnCache = [.. columns];
        _boundaryCacheSource = Text;
        _boundaryCachePasswordCharacter = PasswordCharacter;
        _boundaryCacheCellPolicy = CellPolicy;
        return (BoundaryOffsets, _boundaryRowCache, _boundaryColumnCache);
    }

    private bool Insert(string value)
    {
        if (IsReadOnly)
        {
            return false;
        }

        EditResult result;

        try
        {
            result = Edit.Replace(
                Text,
                _selection,
                value,
                MaxLength,
                AcceptsReturn,
                AcceptsTab);
        }
        catch (ArgumentException)
        {
            // Terminal text that policy rejects is ignored as one complete input transaction.
            return false;
        }

        // Observer exceptions happen after a valid proposal and must propagate;
        // they are never edit-policy rejections.
        return Commit(result, true);
    }

    private void Handle(KeyEventArgs eventArgs)
    {
        if (eventArgs.Stroke.Action is not (KeyAction.Press or KeyAction.Repeat))
        {
            return;
        }

        var extend = (eventArgs.Stroke.Modifiers & Modifiers.Shift) != 0;
        var word = (eventArgs.Stroke.Modifiers & Modifiers.Control) != 0;

        if (word && eventArgs.Stroke is { Code: Code.Character, Character: { } character })
        {
            var value = Rune.ToLowerInvariant(character);

            if (value == new Rune('a'))
            {
                SetSelection(new Selection(0, Text.Length));
                eventArgs.Handled = true;
                return;
            }

            if (value == new Rune('z'))
            {
                _ = Undo();
                eventArgs.Handled = true;
                return;
            }

            if (value == new Rune('y'))
            {
                _ = Redo();
                eventArgs.Handled = true;
                return;
            }
        }

        EditResult? result = null;

        if (eventArgs.Stroke.Code == Code.Left)
        {
            result = word
                ? Edit.MovePreviousWord(Text, _selection, extend)
                : MoveCaretPrevious(extend);
        }
        else if (eventArgs.Stroke.Code == Code.Right)
        {
            result = word
                ? Edit.MoveNextWord(Text, _selection, extend)
                : Edit.MoveNextUnchecked(Text, _selection, extend);
        }
        else if (eventArgs.Stroke.Code == Code.Home)
        {
            result = Edit.MoveHome(Text, _selection, extend);
        }
        else if (eventArgs.Stroke.Code == Code.End)
        {
            result = Edit.MoveEnd(Text, _selection, extend);
        }
        else if (eventArgs.Stroke.Code == Code.Up)
        {
            result = MoveVertical(-1, extend);
        }
        else if (eventArgs.Stroke.Code == Code.Down)
        {
            result = MoveVertical(1, extend);
        }
        else if (eventArgs.Stroke.Code == Code.Backspace && !IsReadOnly)
        {
            result = Edit.Backspace(Text, _selection);
        }
        else if (eventArgs.Stroke.Code == Code.Delete && !IsReadOnly)
        {
            result = Edit.Delete(Text, _selection);
        }

        if (result.HasValue)
        {
            _ = Commit(result.Value, result.Value.Text != Text);
            eventArgs.Handled = true;
            return;
        }

        if (eventArgs.Stroke.Code == Code.Enter)
        {
            if (AcceptsReturn && !IsReadOnly)
            {
                _ = Insert("\n");
            }
            else
            {
                Submitted?.Invoke(this, new SubmittedEventArgs(Text));
            }

            eventArgs.Handled = true;
        }
        else if (eventArgs.Stroke.Code == Code.Tab && AcceptsTab && !IsReadOnly)
        {
            _ = Insert("\t");
            eventArgs.Handled = true;
        }
    }

    private void Handle(PointerEventArgs eventArgs)
    {
        var pointer = eventArgs.Pointer;

        if (pointer.Action == PointerAction.Wheel)
        {
            eventArgs.Handled = ScrollBy(
                pointer.WheelX,
                pointer.WheelY.Negate());
            return;
        }

        if (pointer.Action == PointerAction.Press &&
            (pointer.Buttons & Buttons.Primary) != 0 &&
            pointer.Cells is { } pressedCells &&
            Bounds.Contains(pressedCells))
        {
            _ = RequestFocus();

            if (eventArgs.ClickCount == 2)
            {
                SetSelection(Edit.SelectWord(Text, IndexAt(pressedCells)));
                eventArgs.Handled = true;
                return;
            }

            var capture = CaptureOwner;

            if (capture is null || !capture.Capture(this))
            {
                return;
            }

            _pointerAnchor = IndexAt(pressedCells);
            _pointerSelecting = true;
            SetSelection(new Selection(_pointerAnchor, _pointerAnchor));
            eventArgs.Handled = true;
            return;
        }

        if (!_pointerSelecting)
        {
            return;
        }

        if (pointer.Cells is not { } cells)
        {
            eventArgs.Handled = true;

            if (pointer.Action is PointerAction.Release or PointerAction.Leave)
            {
                CancelPointer(releaseCapture: true);
            }

            return;
        }

        SetSelection(new Selection(_pointerAnchor, IndexAt(cells)));
        eventArgs.Handled = true;

        if (pointer.Action is PointerAction.Release or PointerAction.Leave)
        {
            CancelPointer(releaseCapture: true);
        }
    }

    private int IndexAt(Point point)
    {
        if (WordWrap && _visualLines.Length > 0)
        {
            var targetX = Math.Max(0, point.X - _editorBounds.X);
            var targetY = Math.Clamp(
                point.Y - _editorBounds.Y + VerticalOffset,
                0,
                _visualLines.Length - 1);
            var line = _visualLines[targetY];

            if (line.Length == 0)
            {
                return line.Offset;
            }

            var span = Text.AsSpan(line.Offset, line.Length);
            var x = 0;

            foreach (var g in Graphemes.Enumerate(span))
            {
                var c = span.Slice(g.Offset, g.Length);

                if (IsLineBreak(c))
                {
                    return line.Offset + g.Offset;
                }

                var width = ClusterWidth(c, x);

                if (targetX < x + width)
                {
                    return targetX < x + ((width + 1) / 2)
                        ? line.Offset + g.Offset
                        : line.Offset + g.Offset + g.Length;
                }

                x += width;
            }

            return line.Offset + line.Length;
        }

        var targetXOrig = Math.Max(0, point.X - _editorBounds.X + HorizontalOffset);
        var targetYOrig = Math.Max(0, point.Y - _editorBounds.Y + VerticalOffset);
        var xOrig = 0;
        var yOrig = 0;

        foreach (var grapheme in Graphemes.Enumerate(Text))
        {
            var cluster = Text.AsSpan(grapheme.Offset, grapheme.Length);

            if (IsLineBreak(cluster))
            {
                if (yOrig == targetYOrig)
                {
                    return grapheme.Offset;
                }

                xOrig = 0;
                yOrig++;
                continue;
            }

            if (yOrig > targetYOrig)
            {
                return grapheme.Offset;
            }

            var width = ClusterWidth(cluster, xOrig);

            if (yOrig == targetYOrig && targetXOrig < xOrig + width)
            {
                return targetXOrig < xOrig + ((width + 1) / 2)
                    ? grapheme.Offset
                    : grapheme.Offset + grapheme.Length;
            }

            xOrig += width;
        }

        return Text.Length;
    }

    private EditResult MoveVertical(int delta, bool extend)
    {
        Position(_selection.Caret, out var x, out var y);
        var targetY = Math.Max(0, y + delta);
        var caret = IndexAt(new Point(
            _editorBounds.X + x - (WordWrap && _visualLines.Length > 0 ? 0 : HorizontalOffset),
            _editorBounds.Y + targetY - VerticalOffset));
        var selection = extend
            ? new Selection(_selection.Anchor, caret)
            : new Selection(caret, caret);
        return new EditResult(Text, selection, selection != _selection);
    }

    private void EnsureCaretVisible(Rect bounds, bool remeasure = true)
    {
        if (WordWrap && _visualLines.Length > 0)
        {
            _contentHeight = _visualLines.Length;
            Position(_selection.Caret, out _, out var y);
            VerticalOffset = Offset(VerticalOffset, y, bounds.Height, _contentHeight);
            return;
        }

        if (remeasure)
        {
            MeasureText(out _contentWidth, out _contentHeight);
        }

        Position(_selection.Caret, out var x, out var caretY);

        HorizontalOffset = AlignToClusterStart(
            Offset(HorizontalOffset, x, bounds.Width, _contentWidth),
            caretY);
        VerticalOffset = Offset(VerticalOffset, caretY, bounds.Height, _contentHeight);
    }

    /// <summary>Snaps a rightward-scrolled horizontal offset down to the start of whichever cluster
    /// it lands inside, on the given row.</summary>
    /// <remarks>
    /// The leftward branch of <see cref="Offset(int, int, int, int)"/> always assigns the caret's own
    /// cell x, which is inherently a cluster start; only the rightward branch's viewport-relative
    /// arithmetic (<c>caret - viewport + 1</c>) can land mid-cluster. Re-snapping unconditionally is
    /// therefore a no-op for every already-aligned value and only changes the one case that needs it
    /// (see #217). Looks the target row up in the same cached boundary/row/column arrays
    /// <see cref="PositionFast"/> uses instead of rescanning <see cref="Text"/> from its start on
    /// every call - the prior linear scan cost O(document) per navigation event that calls
    /// <see cref="EnsureCaretVisible"/>, the other half of the O(n^2) total this cache was built to
    /// eliminate (see #42).
    /// </remarks>
    private int AlignToClusterStart(int offset, int row)
    {
        if (offset <= 0)
        {
            return offset;
        }

        var (_, rows, columns) = BoundaryCache();

        for (var index = LowerBoundByRow(rows, row); index < rows.Length && rows[index] == row; index++)
        {
            if (index == 0 || rows[index - 1] != row)
            {
                continue;
            }

            var before = columns[index - 1];
            var after = columns[index];

            if (offset > before && offset < after)
            {
                return before;
            }
        }

        return offset;
    }

    /// <summary>Finds the first index whose cached row is at least <paramref name="row"/> via binary
    /// search over the non-decreasing row array, so <see cref="AlignToClusterStart"/> starts scanning
    /// at the target row instead of the document start.</summary>
    private static int LowerBoundByRow(int[] rows, int row)
    {
        var low = 0;
        var high = rows.Length;

        while (low < high)
        {
            var mid = low + ((high - low) / 2);

            if (rows[mid] < row)
            {
                low = mid + 1;
            }
            else
            {
                high = mid;
            }
        }

        return low;
    }

    private bool ScrollBy(int horizontal, int vertical)
    {
        if (WordWrap && _visualLines.Length > 0)
        {
            _contentHeight = _visualLines.Length;
        }
        else
        {
            MeasureText(out _contentWidth, out _contentHeight);
        }

        var bounds = _editorBounds;
        var nextHorizontal = WordWrap
            ? HorizontalOffset
            : Move(HorizontalOffset, horizontal, bounds.Width, _contentWidth);
        var nextVertical = Move(
            VerticalOffset,
            vertical,
            bounds.Height,
            _contentHeight);

        if (nextHorizontal == HorizontalOffset && nextVertical == VerticalOffset)
        {
            return false;
        }

        // Only consume a wheel event while this editor has moved. At an edge,
        // the untouched event continues through bubble routing to an ancestor viewport.
        HorizontalOffset = nextHorizontal;
        VerticalOffset = nextVertical;
        Invalidate(Invalidation.Render);
        return true;
    }

    private void ArrangeChrome()
    {
        _horizontal.Style = ScrollBarStyle;
        _vertical.Style = ScrollBarStyle;
        var bounds = ContentBounds;

        if (WordWrap)
        {
            // In word-wrap mode, first determine vertical scrollbar presence to get final viewport width.
            var vertical = (ScrollBars & ScrollBars.Vertical) != 0 &&
                           ShowScrollBars == ShowScrollBars.Always;
            var viewport = new Rect(bounds.X, bounds.Y, Math.Max(0, bounds.Width - (vertical ? 1 : 0)),
                bounds.Height);

            BuildVisualLines(viewport.Width);
            _contentHeight = _visualLines.Length;
            _contentWidth = viewport.Width;

            if (!vertical && ShowScrollBars == ShowScrollBars.WhenNeeded)
            {
                vertical = (ScrollBars & ScrollBars.Vertical) != 0 && _contentHeight > viewport.Height;
                viewport = new Rect(bounds.X, bounds.Y, Math.Max(0, bounds.Width - (vertical ? 1 : 0)),
                    bounds.Height);

                // Rebuild with potentially narrower width after vertical scrollbar appeared.
                BuildVisualLines(viewport.Width);
                _contentHeight = _visualLines.Length;
                _contentWidth = viewport.Width;
            }

            _editorBounds = viewport;
            _horizontal.Visibility = Visibility.Collapsed;
            _vertical.Visibility = vertical ? Visibility.Visible : Visibility.Collapsed;
            ArrangeChild(
                _horizontal,
                new Rect(bounds.X, bounds.Y + viewport.Height, viewport.Width, 0),
                ResolvedAxes.Both);
            ArrangeChild(
                _vertical,
                new Rect(bounds.X + viewport.Width, bounds.Y, vertical ? 1 : 0, viewport.Height),
                ResolvedAxes.Both);
            HorizontalOffset = 0;
            Configure(_horizontal, 0, viewport.Width, 0);
            Configure(_vertical, Math.Max(0, _contentHeight - viewport.Height + 1), viewport.Height, VerticalOffset);
            return;
        }

        MeasureText(out _contentWidth, out _contentHeight);
        var horizontal = (ScrollBars & ScrollBars.Horizontal) != 0 &&
                         ShowScrollBars == ShowScrollBars.Always;
        var vert = (ScrollBars & ScrollBars.Vertical) != 0 &&
                   ShowScrollBars == ShowScrollBars.Always;
        var vp = new Rect(bounds.X, bounds.Y, Math.Max(0, bounds.Width - (vert ? 1 : 0)),
            Math.Max(0, bounds.Height - (horizontal ? 1 : 0)));

        if (ShowScrollBars == ShowScrollBars.WhenNeeded)
        {
            horizontal = (ScrollBars & ScrollBars.Horizontal) != 0 && _contentWidth > vp.Width;
            vert = (ScrollBars & ScrollBars.Vertical) != 0 && _contentHeight > vp.Height;
            vp = new Rect(bounds.X, bounds.Y, Math.Max(0, bounds.Width - (vert ? 1 : 0)),
                Math.Max(0, bounds.Height - (horizontal ? 1 : 0)));
            horizontal |= (ScrollBars & ScrollBars.Horizontal) != 0 && _contentWidth > vp.Width;
            vert |= (ScrollBars & ScrollBars.Vertical) != 0 && _contentHeight > vp.Height;
            vp = new Rect(bounds.X, bounds.Y, Math.Max(0, bounds.Width - (vert ? 1 : 0)),
                Math.Max(0, bounds.Height - (horizontal ? 1 : 0)));
        }

        _editorBounds = vp;
        _horizontal.Visibility = horizontal ? Visibility.Visible : Visibility.Collapsed;
        _vertical.Visibility = vert ? Visibility.Visible : Visibility.Collapsed;
        ArrangeChild(
            _horizontal,
            new Rect(bounds.X, bounds.Y + vp.Height, vp.Width, horizontal ? 1 : 0),
            ResolvedAxes.Both);
        ArrangeChild(
            _vertical,
            new Rect(bounds.X + vp.Width, bounds.Y, vert ? 1 : 0, vp.Height),
            ResolvedAxes.Both);
        Configure(_horizontal, Math.Max(0, _contentWidth - vp.Width + 1), vp.Width, HorizontalOffset);
        Configure(_vertical, Math.Max(0, _contentHeight - vp.Height + 1), vp.Height, VerticalOffset);
    }

    private void OnHorizontalChanged(object? sender, ScrollEventArgs eventArgs)
    {
        _ = sender;
        HorizontalOffset = eventArgs.Value;
        Invalidate(Invalidation.Render);
    }

    private void OnVerticalChanged(object? sender, ScrollEventArgs eventArgs)
    {
        _ = sender;
        VerticalOffset = eventArgs.Value;
        Invalidate(Invalidation.Render);
    }

    private static void Configure(ScrollBar bar, int maximum, int viewport, int value)
    {
        if (bar.Value > maximum)
        {
            bar.Value = maximum;
        }

        bar.Maximum = maximum;
        bar.ViewportSize = viewport;
        bar.LargeChange = viewport;
        bar.Value = Math.Min(value, maximum);
    }

    private void Position(int index, out int x, out int y)
    {
        if (WordWrap && _visualLines.Length > 0)
        {
            for (var i = _visualLines.Length - 1; i >= 0; i--)
            {
                var line = _visualLines[i];

                if (index >= line.Offset)
                {
                    y = i;
                    x = 0;
                    var end = Math.Min(index, line.Offset + line.Length);
                    var span = Text.AsSpan(line.Offset, end - line.Offset);

                    foreach (var g in Graphemes.Enumerate(span))
                    {
                        var c = span.Slice(g.Offset, g.Length);

                        if (!IsLineBreak(c))
                        {
                            x += ClusterWidth(c, x);
                        }
                    }

                    return;
                }
            }

            x = 0;
            y = 0;
            return;
        }

        PositionFast(index, out x, out y);
    }

    private void MeasureText(out int width, out int height)
    {
        var x = 0;
        width = 0;
        height = 1;

        foreach (var grapheme in Graphemes.Enumerate(Text))
        {
            var cluster = Text.AsSpan(grapheme.Offset, grapheme.Length);

            if (IsLineBreak(cluster))
            {
                width = Math.Max(width, x);
                x = 0;
                height++;
            }
            else
            {
                x += ClusterWidth(cluster, x);
            }
        }

        width = Math.Max(width, x);
    }

    private void BuildVisualLines(int viewportWidth)
    {
        if (Text.Length == 0)
        {
            _visualLines = [new VisualLine(0, 0, 0)];

            return;
        }

        if (viewportWidth <= 0)
        {
            viewportWidth = int.MaxValue;
        }

        var lines = new List<VisualLine>();
        var lineStart = 0;
        var x = 0;
        var lastBreakOffset = -1;
        var lastBreakCells = 0;

        foreach (var grapheme in Graphemes.Enumerate(Text))
        {
            var cluster = Text.AsSpan(grapheme.Offset, grapheme.Length);

            if (IsLineBreak(cluster))
            {
                lines.Add(new VisualLine(lineStart, grapheme.Offset - lineStart, x));
                lineStart = grapheme.Offset + grapheme.Length;
                x = 0;
                lastBreakOffset = -1;
                lastBreakCells = 0;
                continue;
            }

            var width = ClusterWidth(cluster, x);

            // Would this grapheme exceed the viewport?
            if (x + width > viewportWidth && lineStart < grapheme.Offset)
            {
                if (lastBreakOffset > lineStart)
                {
                    lines.Add(new VisualLine(lineStart, lastBreakOffset - lineStart, lastBreakCells));
                    lineStart = lastBreakOffset;
                }
                else
                {
                    lines.Add(new VisualLine(lineStart, grapheme.Offset - lineStart, x));
                    lineStart = grapheme.Offset;
                }

                x = 0;
                lastBreakOffset = -1;
                lastBreakCells = 0;
                var remaining = Text.AsSpan(lineStart, grapheme.Offset + grapheme.Length - lineStart);

                foreach (var g in Graphemes.Enumerate(remaining))
                {
                    var c = remaining.Slice(g.Offset, g.Length);

                    if (!IsLineBreak(c))
                    {
                        var w = ClusterWidth(c, x);

                        if (IsWordBreak(c))
                        {
                            lastBreakOffset = lineStart + g.Offset + g.Length;
                            lastBreakCells = x + w;
                        }

                        x += w;
                    }
                }

                continue;
            }

            // Track whitespace as potential break point (after the whitespace).
            if (IsWordBreak(cluster))
            {
                lastBreakOffset = grapheme.Offset + grapheme.Length;
                lastBreakCells = x + width;
            }

            x += width;
        }

        lines.Add(new VisualLine(lineStart, Text.Length - lineStart, x));
        _visualLines = [.. lines];
    }

    private static bool IsWordBreak(ReadOnlySpan<char> cluster) =>
        cluster.Length == 1 && char.IsWhiteSpace(cluster[0]) && cluster[0] is not ('\r' or '\n');

    private int ClusterWidth(ReadOnlySpan<char> cluster, int x)
    {
        return PasswordCharacter is { } mask
            ? PasswordWidth(mask)
            : cluster.Length == 1 && cluster[0] == '\t'
                ? 4 - (x % 4)
                : UnicodeWidth.Measure(cluster, CellPolicy.AmbiguousWidth).Cells;
    }

    private int PasswordWidth(Rune value)
    {
        Span<char> buffer = stackalloc char[2];
        var length = value.EncodeToUtf16(buffer);
        return UnicodeWidth.Measure(buffer[..length], CellPolicy.AmbiguousWidth).Cells;
    }

    private TerminalStyle SelectedStyle()
    {
        var style = ResolvedStyle;
        return new TerminalStyle(
            style.Foreground,
            style.Background,
            style.Attributes | TerminalAttributes.Reverse,
            style.Hyperlink,
            style.Underline,
            style.UnderlineColor);
    }

    private TerminalStyle PlaceholderStyle()
    {
        var style = ResolvedStyle;
        return new TerminalStyle(
            style.Foreground,
            style.Background,
            style.Attributes | TerminalAttributes.Dim,
            style.Hyperlink,
            style.Underline,
            style.UnderlineColor);
    }

    private static bool IsLineBreak(ReadOnlySpan<char> cluster) =>
        cluster.IndexOfAny('\r', '\n') >= 0;

    private static int Offset(int current, int caret, int viewport, int content)
    {
        if (viewport <= 0)
        {
            return 0;
        }

        var next = caret < current
            ? caret
            : caret >= current + viewport
                ? caret - viewport + 1
                : current;
        return Math.Clamp(next, 0, Math.Max(0, content - viewport + 1));
    }

    private static int Move(int current, int delta, int viewport, int content)
    {
        if (viewport <= 0)
        {
            return 0;
        }

        var next = (int) Math.Clamp((long) current + delta, 0, int.MaxValue);
        return Math.Clamp(next, 0, Math.Max(0, content - viewport + 1));
    }

    private static void Draw(
        TerminalCanvas canvas,
        Point point,
        Rune rune,
        TerminalStyle style)
    {
        Span<char> buffer = stackalloc char[2];
        var length = rune.EncodeToUtf16(buffer);
        _ = canvas.Draw(buffer[..length], point, style);
    }

    private static void DrawSpaces(
        TerminalCanvas canvas,
        Point point,
        int count,
        TerminalStyle style)
    {
        for (var index = 0; index < count; index++)
        {
            _ = canvas.Draw(" ", new Point(point.X + index, point.Y), style);
        }
    }

    private bool Restore(List<EditResult> source, List<EditResult> destination)
    {
        VerifyMutable();

        if (source.Count == 0)
        {
            return false;
        }

        var snapshot = source[^1];
        var current = new EditResult(Text, _selection, false);

        if (!Commit(new EditResult(snapshot.Text, snapshot.Selection, true), false))
        {
            return false;
        }

        source.RemoveAt(source.Count - 1);
        Push(destination, current);
        return true;
    }

    private void Push(List<EditResult> history, EditResult value)
    {
        if (UndoLimit == 0)
        {
            return;
        }

        history.Add(value);
        Trim(history);
    }

    private void Trim(List<EditResult> history)
    {
        var remove = history.Count - UndoLimit;

        if (remove > 0)
        {
            history.RemoveRange(0, remove);
        }
    }

    private void CancelPointer(bool releaseCapture)
    {
        _pointerSelecting = false;

        if (releaseCapture && CaptureOwner?.Captured is { } captured && ReferenceEquals(captured, this))
        {
            CaptureOwner.Release();
        }
    }

    private readonly record struct VisualLine(int Offset, int Length, int Cells);
}
