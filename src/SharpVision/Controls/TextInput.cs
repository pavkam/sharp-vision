using System.Diagnostics;
using System.Text;

using SharpVision.Input;
using SharpVision.Layout;
using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Input;
using SharpVision.Terminal.Unicode;
using SharpVision.Text;

using KeyAction = SharpVision.Terminal.Input.Action;
using TerminalAttributes = SharpVision.Terminal.Rendering.Attributes;
using TerminalCanvas = SharpVision.Terminal.Rendering.Canvas;
using TerminalStyle = SharpVision.Terminal.Rendering.Style;
using UnicodeWidth = SharpVision.Terminal.Unicode.Width;

namespace SharpVision.Controls;

/// <summary>Defines a focusable grapheme-safe single- or multiline text editor.</summary>
public sealed class TextInput: Control
{
    private readonly List<EditResult> _undo = [];
    private readonly List<EditResult> _redo = [];
    private string _text = string.Empty;
    private Selection _selection;
    private bool _pointerSelecting;
    private int _pointerAnchor;
    private CaptureManager? _subscribedCapture;
    private int _contentWidth;
    private int _contentHeight = 1;

    /// <summary>Initializes an empty focusable single-line editor.</summary>
    public TextInput() => CanFocus = true;

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

    /// <summary>Gets or sets whether user input may mutate text.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool IsReadOnly
    {
        get;
        set => _ = Set(ref field, value, Invalidation.Render);
    }

    /// <summary>Gets or sets whether inserted CR or LF values are accepted.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool AcceptsReturn
    {
        get;
        set => _ = Set(ref field, value, Invalidation.Measure);
    }

    /// <summary>Gets or sets whether inserted tab values are accepted.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool AcceptsTab
    {
        get;
        set => _ = Set(ref field, value, Invalidation.Measure);
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

            _ = Set(ref field, value, Invalidation.Measure);
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

            _ = Set(ref field, value, Invalidation.None);
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
            _ = Set(ref field, value, Invalidation.None);
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
    protected override Size MeasureCore(Constraint constraint)
    {
        _ = constraint.Width;
        MeasureText(out _contentWidth, out _contentHeight);
        return new Size(Math.Max(1, _contentWidth), Math.Max(1, _contentHeight));
    }

    /// <inheritdoc/>
    protected override void ArrangeCore(Rect bounds) => EnsureCaretVisible(bounds);

    /// <inheritdoc/>
    protected override void RenderCore(TerminalCanvas canvas)
    {
        var bounds = ContentBounds;

        if (bounds.Width == 0 || bounds.Height == 0)
        {
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
            TextChanging = null;
            TextChanged = null;
            SelectionChanged = null;
            Submitted = null;
            _undo.Clear();
            _redo.Clear();
        }
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
        EnsureCaretVisible(ContentBounds);

        if (textChanged)
        {
            NotifyChanged(nameof(Text), Invalidation.Measure);
        }

        if (previousSelection != _selection)
        {
            NotifyChanged(nameof(CaretIndex), Invalidation.Render);
            NotifyChanged(nameof(SelectionStart), Invalidation.Render);
            NotifyChanged(nameof(SelectionLength), Invalidation.Render);
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

        if (pointer.Action == PointerAction.Press &&
            (pointer.Buttons & Buttons.Primary) != 0 &&
            Bounds.Contains(pointer.Cells))
        {
            var capture = CaptureOwner;

            if (capture is null || !capture.Capture(this))
            {
                return;
            }

            _ = FocusOwner?.Focus(this);
            _pointerAnchor = IndexAt(pointer.Cells);
            _pointerSelecting = true;
            SubscribeCapture(capture);
            SetSelection(new Selection(_pointerAnchor, _pointerAnchor));
            eventArgs.Handled = true;
            return;
        }

        if (!_pointerSelecting)
        {
            return;
        }

        SetSelection(new Selection(_pointerAnchor, IndexAt(pointer.Cells)));
        eventArgs.Handled = true;

        if (pointer.Action is PointerAction.Release or PointerAction.Leave)
        {
            CancelPointer(releaseCapture: true);
        }
    }

    private int IndexAt(Point point)
    {
        var targetX = Math.Max(0, point.X - ContentBounds.X + HorizontalOffset);
        var targetY = Math.Max(0, point.Y - ContentBounds.Y + VerticalOffset);
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
            ContentBounds.X + x - HorizontalOffset,
            ContentBounds.Y + targetY - VerticalOffset));
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
        return PasswordCharacter.HasValue
            ? 1
            : cluster.Length == 1 && cluster[0] == '\t'
            ? 4 - (x % 4)
            : UnicodeWidth.Measure(cluster).Cells;
    }

    private TerminalStyle SelectedStyle()
    {
        var style = ResolvedStyle;
        return new TerminalStyle(
            style.Foreground,
            style.Background,
            style.Attributes | TerminalAttributes.Reverse,
            style.Hyperlink);
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

    private void OnCaptureCancelled(object? sender, CaptureCancelledEventArgs eventArgs)
    {
        if (ReferenceEquals(eventArgs.Control, this))
        {
            Debug.Assert(ReferenceEquals(sender, _subscribedCapture), "Capture owner is stable.");
            CancelPointer(releaseCapture: false);
        }
    }

    private void SubscribeCapture(CaptureManager capture)
    {
        if (_subscribedCapture is { } previous)
        {
            previous.Cancelled -= OnCaptureCancelled;
        }

        _subscribedCapture = capture;
        capture.Cancelled += OnCaptureCancelled;
    }

    private void CancelPointer(bool releaseCapture)
    {
        _pointerSelecting = false;

        if (_subscribedCapture is { } capture)
        {
            capture.Cancelled -= OnCaptureCancelled;
            _subscribedCapture = null;
        }

        if (releaseCapture && CaptureOwner?.Captured is { } captured && ReferenceEquals(captured, this))
        {
            CaptureOwner.Release();
        }
    }
}
