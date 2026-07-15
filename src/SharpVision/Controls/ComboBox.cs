// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using SharpVision.Terminal.Input;

/// <summary>Displays one selected list value and opens an owned popup-style list for keyboard or pointer choice.</summary>
public sealed class ComboBox: Pressable
{
    private readonly List _list;
    private readonly Popup _popup;

    #region Construction and properties

    /// <summary>Initializes an empty combo box with a framed popup containing a single-selection list.</summary>
    public ComboBox() : base(capacity: 1)
    {
        _list = new List
        {
            SelectionMode = SelectionMode.Single,
        };
        _list.ItemInvoked += OnItemInvoked;
        _list.SelectionChanged += OnSelectionChanged;
        _popup = new Popup
        {
            Anchor = this,
            Child = _list,
        };
        _popup.Closing += OnPopupClosing;
        _popup.Closed += OnPopupClosed;
        Children.Add(_popup);
    }

    /// <summary>Raised after a selected index commits through direct assignment or the drop-down list.</summary>
    public event EventHandler<ListSelectionChangedEventArgs>? SelectionChanged;

    /// <summary>Gets or sets a copied list of choices displayed by the drop-down.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="ArgumentException">A list template cannot realize the supplied values.</exception>
    /// <exception cref="InvalidOperationException">The attached combo box is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The combo box is disposed.</exception>
    public IReadOnlyList<object?> Items
    {
        get => _list.Items;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            VerifyMutable();
            _list.Items = value;

            if (_list.SelectedIndex < 0 && _list.Items.Count > 0)
            {
                _list.SelectedIndex = 0;
            }

            Invalidate(Invalidation.Measure);
        }
    }

    /// <summary>Gets or sets the selected index, or -1 when no value is selected.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside the item range.</exception>
    /// <exception cref="InvalidOperationException">The attached combo box is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The combo box is disposed.</exception>
    public int SelectedIndex
    {
        get => _list.SelectedIndex;
        set => _list.SelectedIndex = value;
    }

    /// <summary>Gets or sets the maximum visible list height in terminal cells.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero or negative.</exception>
    /// <exception cref="InvalidOperationException">The attached combo box is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The combo box is disposed.</exception>
    public int DropDownHeight
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            _ = Set(ref field, value, Invalidation.Measure);
        }
    } = 8;

    /// <summary>Gets or sets the axes available to the owned drop-down overflow host.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value contains unknown axis flags.</exception>
    /// <exception cref="InvalidOperationException">The attached combo box is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The combo box is disposed.</exception>
    public new ScrollBars ScrollBars
    {
        get => _list.ScrollBars;
        set
        {
            VerifyMutable();

            if (_list.ScrollBars == value)
            {
                return;
            }

            _list.ScrollBars = value;
            NotifyChanged(nameof(ScrollBars), Invalidation.None);
        }
    }

    /// <summary>Gets or sets the drop-down scrollbar reservation policy.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached combo box is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The combo box is disposed.</exception>
    public new ShowScrollBars ShowScrollBars
    {
        get => _list.ShowScrollBars;
        set
        {
            VerifyMutable();

            if (_list.ShowScrollBars == value)
            {
                return;
            }

            _list.ShowScrollBars = value;
            NotifyChanged(nameof(ShowScrollBars), Invalidation.None);
        }
    }

    /// <summary>Gets or sets the compact or full form of the owned drop-down rails.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached combo box is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The combo box is disposed.</exception>
    public new ScrollBarChrome ScrollBarChrome
    {
        get => _list.ScrollBarChrome;
        set
        {
            VerifyMutable();

            if (_list.ScrollBarChrome == value)
            {
                return;
            }

            _list.ScrollBarChrome = value;
            NotifyChanged(nameof(ScrollBarChrome), Invalidation.None);
        }
    }

    /// <summary>Gets or sets the generated line or block glyph treatment for owned drop-down rails.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached combo box is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The combo box is disposed.</exception>
    public new ScrollBarFill ScrollBarFill
    {
        get => _list.ScrollBarFill;
        set
        {
            VerifyMutable();

            if (_list.ScrollBarFill == value)
            {
                return;
            }

            _list.ScrollBarFill = value;
            NotifyChanged(nameof(ScrollBarFill), Invalidation.None);
        }
    }

    /// <summary>Gets or sets whether the owned drop-down list is arranged, rendered, and hit-testable.</summary>
    /// <exception cref="InvalidOperationException">The attached combo box is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The combo box is disposed.</exception>
    public bool IsOpen
    {
        get => _popup.IsOpen;
        set
        {
            VerifyMutable();

            if (_popup.IsOpen == value)
            {
                return;
            }

            _popup.IsOpen = value;
        }
    }

    #endregion

    #region Input, layout, and rendering

    /// <inheritdoc/>
    internal override bool ClipsChildren => false;

    /// <inheritdoc/>
    protected override Rect VisualBounds => IsOpen
        ? Union(Bounds, _popup.SurfaceBounds)
        : Bounds;

    /// <inheritdoc/>
    public override Control? HitTest(Point point)
    {
        return IsDisposed || !IsHitTestVisible || !EffectiveIsVisible || !EffectiveIsEnabled
            ? null
            : (IsOpen ? _popup.HitTest(point) : null) ?? (Bounds.Contains(point) ? this : null);
    }

    /// <inheritdoc/>
    protected override void Activate(ActivationCause cause)
    {
        _ = cause;
        IsOpen = !IsOpen;
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _popup.Measure(new Constraint(constraint.Width, Add(DropDownHeight, 2)));
        var text = SelectedText();
        var width = Add(Terminal.Unicode.Width.Measure(text).Cells, 2);
        return new Size(width, 1);
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        if (!IsOpen)
        {
            return;
        }

        _popup.Arrange(
            RootBounds(bounds),
            widthResolved: true,
            heightResolved: true);
    }

    /// <inheritdoc/>
    protected override void OnRender(TerminalCanvas canvas)
    {
        var style = ResolvedStyle;

        if (ControlAppearance.HasOpaqueFill(this, GetVisualState()))
        {
            canvas.Clear(Bounds, style);
        }

        var label = canvas.Clip(new Rect(Bounds.X, Bounds.Y, Math.Max(0, Bounds.Width - 2), 1));
        _ = label.Draw(SelectedText().AsSpan(), new Point(Bounds.X, Bounds.Y), style, background: BackgroundMode.Transparent);
        _ = canvas.Draw(" ▼".AsSpan(), new Point(Math.Max(Bounds.X, Bounds.Right - 2), Bounds.Y), style, background: BackgroundMode.Transparent);
    }

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        base.OnEvent(eventArgs);

        if (eventArgs.Handled || !IsOpen || eventArgs is not KeyEventArgs { Stroke: { Code: Code.Escape, Action: KeyAction.Press } })
        {
            return;
        }

        IsOpen = false;
        eventArgs.Handled = true;
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);

        if (reason == ReleaseReason.Disposed)
        {
            _list.ItemInvoked -= OnItemInvoked;
            _list.SelectionChanged -= OnSelectionChanged;
            _popup.Closing -= OnPopupClosing;
            _popup.Closed -= OnPopupClosed;
            SelectionChanged = null;
        }
    }

    #endregion

    #region Drop-down coordination

    private void OnItemInvoked(object? sender, ItemInvokedEventArgs eventArgs)
    {
        _ = sender;
        _list.SelectedIndex = eventArgs.Index;
        IsOpen = false;
    }

    private void OnSelectionChanged(object? sender, ListSelectionChangedEventArgs eventArgs)
    {
        _ = sender;
        Invalidate(Invalidation.Render);
        SelectionChanged?.Invoke(this, eventArgs);
    }

    private void OnPopupClosing(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;

        if (IsWithin(_list, FocusOwner?.Focused))
        {
            // Popup invokes Closing while the List remains visible, which lets
            // this field recover focus before the child becomes unavailable.
            _ = FocusOwner!.Focus(this);
        }
    }

    private void OnPopupClosed(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        NotifyChanged(nameof(IsOpen), Invalidation.None);
    }

    #endregion

    #region Geometry

    private string SelectedText()
    {
        var index = _list.SelectedIndex;

        return index < 0 || index >= _list.Items.Count ? string.Empty : _list.Items[index]?.ToString() ?? string.Empty;
    }

    private static int Add(int left, int right)
    {
        Debug.Assert(left >= 0, "ComboBox accumulation uses non-negative extents.");
        Debug.Assert(right >= 0, "ComboBox accumulation uses non-negative extents.");

        return (int) Math.Min(int.MaxValue, (long) left + right);
    }

    private Rect RootBounds(Rect fallback)
    {
        Control root = this;

        while (root.Parent is { } parent)
        {
            root = parent;
        }

        if (!ReferenceEquals(root, this) && root.Bounds.Width != 0 && root.Bounds.Height != 0)
        {
            return root.Bounds;
        }

        // A standalone field may deliberately be one cell high. Its popup is
        // still constrained by the measure viewport, not by that field box.
        var viewport = LastMeasureConstraint;
        return new Rect(
            fallback.X,
            fallback.Y,
            viewport?.Width ?? fallback.Width,
            viewport?.Height ?? fallback.Height);
    }

    private static bool IsWithin(Control ancestor, Control? candidate)
    {
        for (var current = candidate; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    private static Rect Union(Rect left, Rect right)
    {
        var x = Math.Min(left.X, right.X);
        var y = Math.Min(left.Y, right.Y);
        var rightEdge = Math.Max(left.Right, right.Right);
        var bottom = Math.Max(left.Bottom, right.Bottom);

        return new Rect(
            x,
            y,
            (int) Math.Max(0L, (long) rightEdge - x),
            (int) Math.Max(0L, (long) bottom - y));
    }

    #endregion
}
