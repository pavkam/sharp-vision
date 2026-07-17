// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;


using SharpVision.Terminal.Input;
using SharpVision.Text;

using UnicodeWidth = Width;

/// <summary>Defines a focusable grapheme-safe single- or multiline text editor.</summary>
public sealed class TextInput: Control
{
    private readonly List<EditResult> _undo = [];
    private readonly List<EditResult> _redo = [];
    private string _text = string.Empty;
    private Selection _selection;
    private bool _pointerSelecting;
    private int _pointerAnchor;
    private int _contentWidth;
    private int _contentHeight = 1;
    private readonly OwnedControlSlot _chrome;
    private readonly ScrollBar _horizontal;
    private readonly ScrollBar _vertical;
    private Rect _editorBounds;

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
                ChangeImpact.Arrange),
            capacity: 2);
        _horizontal = new ScrollBar { Orientation = Orientation.Horizontal };
        _vertical = new ScrollBar { Orientation = Orientation.Vertical };
        _horizontal.ValueChanged += OnHorizontalChanged;
        _vertical.ValueChanged += OnVerticalChanged;
        _chrome.Add(_horizontal);
        _chrome.Add(_vertical);
        BorderThickness = new Thickness(1);
        BorderGlyphs = Glyphs.Light;
        Focusable = true;
        TabStop = true;
    }

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
        set => _ = SetProperty(ref field, value, ChangeImpact.Render);
    }

    /// <summary>Gets or sets whether user input may mutate text.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool IsReadOnly
    {
        get;
        set => _ = SetProperty(ref field, value, ChangeImpact.Render);
    }

    /// <summary>Gets or sets whether inserted CR or LF values are accepted.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool AcceptsReturn
    {
        get;
        set => _ = SetProperty(ref field, value, ChangeImpact.Measure);
    }

    /// <summary>Gets or sets whether inserted tab values are accepted.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool AcceptsTab
    {
        get;
        set => _ = SetProperty(ref field, value, ChangeImpact.Measure);
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

            _ = SetProperty(ref field, value, ChangeImpact.Measure);
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

            _ = SetProperty(ref field, value, ChangeImpact.None);
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
                throw new ArgumentOutOfRangeException(nameof(value), value, "The scrollbar axes contain unknown flags.");
            }

            if (SetProperty(ref field, value, ChangeImpact.Arrange))
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
                throw new ArgumentOutOfRangeException(nameof(value), value, "The scrollbar visibility policy is unknown.");
            }

            if (SetProperty(ref field, value, ChangeImpact.Arrange))
            {
                ArrangeChrome();
            }
        }
    } = ShowScrollBars.WhenNeeded;

    /// <summary>Gets or sets the compact or full form requested for editor rails.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public ScrollBarChrome ScrollBarChrome
    {
        get;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The scrollbar chrome is unknown.");
            }

            _ = SetProperty(ref field, value, ChangeImpact.Arrange);
            _horizontal.Chrome = ScrollBarChrome;
            _vertical.Chrome = ScrollBarChrome;
        }
    }

    /// <summary>Gets or sets the generated line or block glyph treatment requested for editor rails.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public ScrollBarFill ScrollBarFill
    {
        get;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The scrollbar fill is unknown.");
            }

            _ = SetProperty(ref field, value, ChangeImpact.Render);
            _horizontal.Fill = ScrollBarFill;
            _vertical.Fill = ScrollBarFill;
        }
    }

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
            _ = SetProperty(ref field, value, ChangeImpact.None);
            Trim(_undo);
        }
    } = 100;

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

    /// <summary>Inserts application-owned clipboard text through the normal edit transaction.</summary>
    /// <param name="value">The non-null clipboard text.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The attached control is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    internal void PasteClipboard(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        VerifyMutable();
        Insert(value);
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
        _ = constraint.Width;
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
    public override Control? HitTest(Point point)
    {
        return IsDisposed || !IsHitTestVisible || !EffectiveIsVisible || !EffectiveIsEnabled || !Bounds.Contains(point)
            ? null
            : _vertical.HitTest(point) ?? _horizontal.HitTest(point) ?? this;
    }

    /// <inheritdoc/>
    internal override void RenderChildren(TerminalCanvas canvas)
    {
        _horizontal.Render(canvas);
        _vertical.Render(canvas);
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
                canvas.SetCursor(position, visible: true);
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
                Insert(text.Text.Value.ToString());
                text.Handled = true;
                break;
            case PasteEventArgs paste:
                Insert(Encoding.UTF8.GetString(paste.Paste.Utf8.Span));
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
        EnsureCaretVisible(_editorBounds);

        if (textChanged)
        {
            NotifyPropertyChanged(nameof(Text), ChangeImpact.Measure);
        }

        if (previousSelection != _selection)
        {
            NotifyPropertyChanged(nameof(CaretIndex), ChangeImpact.Render);
            NotifyPropertyChanged(nameof(SelectionStart), ChangeImpact.Render);
            NotifyPropertyChanged(nameof(SelectionLength), ChangeImpact.Render);
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

    private void Insert(string value)
    {
        if (IsReadOnly)
        {
            return;
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
            return;
        }

        // Observer exceptions happen after a valid proposal and must propagate;
        // they are never edit-policy rejections.
        _ = Commit(result, true);
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
                : Edit.MovePrevious(Text, _selection, extend);
        }
        else if (eventArgs.Stroke.Code == Code.Right)
        {
            result = word
                ? Edit.MoveNextWord(Text, _selection, extend)
                : Edit.MoveNext(Text, _selection, extend);
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
                Insert("\n");
            }
            else
            {
                Submitted?.Invoke(this, new SubmittedEventArgs(Text));
            }

            eventArgs.Handled = true;
        }
        else if (eventArgs.Stroke.Code == Code.Tab && AcceptsTab && !IsReadOnly)
        {
            Insert("\t");
            eventArgs.Handled = true;
        }
    }

    private void Handle(PointerEventArgs eventArgs)
    {
        var pointer = eventArgs.Pointer;

        if (pointer.Action == PointerAction.Wheel)
        {
            eventArgs.Handled = ScrollBy(
                Negate(pointer.WheelX),
                Negate(pointer.WheelY));
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
        var targetX = Math.Max(0, point.X - _editorBounds.X + HorizontalOffset);
        var targetY = Math.Max(0, point.Y - _editorBounds.Y + VerticalOffset);
        var x = 0;
        var y = 0;

        foreach (var grapheme in Graphemes.Enumerate(Text))
        {
            var cluster = Text.AsSpan(grapheme.Offset, grapheme.Length);

            if (IsLineBreak(cluster))
            {
                if (y == targetY)
                {
                    return grapheme.Offset;
                }

                x = 0;
                y++;
                continue;
            }

            if (y > targetY)
            {
                return grapheme.Offset;
            }

            var width = ClusterWidth(cluster, x);

            if (y == targetY && targetX < x + width)
            {
                return targetX <= x + (width / 2)
                    ? grapheme.Offset
                    : grapheme.Offset + grapheme.Length;
            }

            x += width;
        }

        return Text.Length;
    }

    private EditResult MoveVertical(int delta, bool extend)
    {
        Position(_selection.Caret, out var x, out var y);
        var targetY = Math.Max(0, y + delta);
        var caret = IndexAt(new Point(
            _editorBounds.X + x - HorizontalOffset,
            _editorBounds.Y + targetY - VerticalOffset));
        var selection = extend
            ? new Selection(_selection.Anchor, caret)
            : new Selection(caret, caret);
        return new EditResult(Text, selection, selection != _selection);
    }

    private void EnsureCaretVisible(Rect bounds)
    {
        MeasureText(out _contentWidth, out _contentHeight);
        Position(_selection.Caret, out var x, out var y);

        HorizontalOffset = Offset(HorizontalOffset, x, bounds.Width, _contentWidth);
        VerticalOffset = Offset(VerticalOffset, y, bounds.Height, _contentHeight);
    }

    private bool ScrollBy(int horizontal, int vertical)
    {
        MeasureText(out _contentWidth, out _contentHeight);
        var bounds = _editorBounds;
        var nextHorizontal = Move(
            HorizontalOffset,
            horizontal,
            bounds.Width,
            _contentWidth);
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
        _horizontal.Chrome = ScrollBarChrome;
        _vertical.Chrome = ScrollBarChrome;
        _horizontal.Fill = ScrollBarFill;
        _vertical.Fill = ScrollBarFill;
        MeasureText(out _contentWidth, out _contentHeight);
        var bounds = ContentBounds;
        var horizontal = (ScrollBars & ScrollBars.Horizontal) != 0 &&
            ShowScrollBars == ShowScrollBars.Always;
        var vertical = (ScrollBars & ScrollBars.Vertical) != 0 &&
            ShowScrollBars == ShowScrollBars.Always;
        var viewport = new Rect(bounds.X, bounds.Y, Math.Max(0, bounds.Width - (vertical ? 1 : 0)), Math.Max(0, bounds.Height - (horizontal ? 1 : 0)));

        if (ShowScrollBars == ShowScrollBars.WhenNeeded)
        {
            horizontal = (ScrollBars & ScrollBars.Horizontal) != 0 && _contentWidth > viewport.Width;
            vertical = (ScrollBars & ScrollBars.Vertical) != 0 && _contentHeight > viewport.Height;
            viewport = new Rect(bounds.X, bounds.Y, Math.Max(0, bounds.Width - (vertical ? 1 : 0)), Math.Max(0, bounds.Height - (horizontal ? 1 : 0)));
            horizontal |= (ScrollBars & ScrollBars.Horizontal) != 0 && _contentWidth > viewport.Width;
            vertical |= (ScrollBars & ScrollBars.Vertical) != 0 && _contentHeight > viewport.Height;
            viewport = new Rect(bounds.X, bounds.Y, Math.Max(0, bounds.Width - (vertical ? 1 : 0)), Math.Max(0, bounds.Height - (horizontal ? 1 : 0)));
        }

        _editorBounds = viewport;
        _horizontal.Visibility = horizontal ? Visibility.Visible : Visibility.Collapsed;
        _vertical.Visibility = vertical ? Visibility.Visible : Visibility.Collapsed;
        ArrangeChild(
            _horizontal,
            new Rect(bounds.X, bounds.Y + viewport.Height, viewport.Width, horizontal ? 1 : 0),
            ResolvedAxes.Both);
        ArrangeChild(
            _vertical,
            new Rect(bounds.X + viewport.Width, bounds.Y, vertical ? 1 : 0, viewport.Height),
            ResolvedAxes.Both);
        Configure(_horizontal, Math.Max(0, _contentWidth - viewport.Width + 1), viewport.Width, HorizontalOffset);
        Configure(_vertical, Math.Max(0, _contentHeight - viewport.Height + 1), viewport.Height, VerticalOffset);
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
        x = 0;
        y = 0;

        foreach (var grapheme in Graphemes.Enumerate(Text.AsSpan(0, index)))
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
        }
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

    private static int Negate(int value) =>
        (int) Math.Clamp(-(long) value, int.MinValue, int.MaxValue);

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
}
