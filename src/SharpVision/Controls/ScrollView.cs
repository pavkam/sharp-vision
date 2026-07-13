// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;


using SharpVision.Scrolling;
using SharpVision.Terminal.Input;

/// <summary>Defines a clipped one-child viewport with independent automatic scrollbars.</summary>
public sealed class ScrollView: Container
{
    private readonly Children _chrome;
    private readonly ScrollBar _horizontal;
    private readonly ScrollBar _vertical;
    private Size _extent;
    private Size _viewport;
    private int _horizontalOffset;
    private int _verticalOffset;
    private Rect _viewportBounds;
    private Size _measuredExtent;
    private bool _syncing;

    /// <summary>Initializes an empty viewport with automatic bars on both axes.</summary>
    public ScrollView() : base(capacity: 1)
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        _chrome = new Children(this, capacity: 2);
        _horizontal = new ScrollBar
        {
            Orientation = Orientation.Horizontal,
            Chrome = ScrollBarStyle.Full,
            Fill = ScrollBarFill.Block,
        };
        _vertical = new ScrollBar
        {
            Orientation = Orientation.Vertical,
            Chrome = ScrollBarStyle.Full,
            Fill = ScrollBarFill.Block,
        };
        _horizontal.ValueChanged += OnHorizontalChanged;
        _vertical.ValueChanged += OnVerticalChanged;
        _chrome.Add(_horizontal);
        _chrome.Add(_vertical);
    }

    /// <summary>Raised after one or both offsets commit.</summary>
    public event EventHandler<ScrollChangedEventArgs>? ScrollChanged;

    /// <summary>Gets or atomically sets the optional owned content.</summary>
    /// <exception cref="ArgumentException">The value cannot be owned by this viewport.</exception>
    /// <exception cref="InvalidOperationException">The attached viewport is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The viewport or value is disposed.</exception>
    public Control? Content
    {
        get => Children.Count == 0 ? null : Children[0];
        set => Children.SetOnly(value);
    }

    /// <summary>Gets or sets horizontal bar reservation policy.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached viewport is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The viewport is disposed.</exception>
    public ScrollBarVisibility HorizontalBarVisibility
    {
        get;
        set
        {
            Validate(value);
            _ = Set(ref field, value, Invalidation.Measure);
        }
    } = ScrollBarVisibility.Auto;

    /// <summary>Gets or sets vertical bar reservation policy.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached viewport is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The viewport is disposed.</exception>
    public ScrollBarVisibility VerticalBarVisibility
    {
        get;
        set
        {
            Validate(value);
            _ = Set(ref field, value, Invalidation.Measure);
        }
    } = ScrollBarVisibility.Auto;

    /// <summary>Gets or sets the axes that may scroll within this viewport.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value contains unknown axis flags.</exception>
    /// <exception cref="InvalidOperationException">The attached viewport is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The viewport is disposed.</exception>
    public ScrollBars ScrollBars
    {
        get;
        set
        {
            if ((value & ~ScrollBars.Both) != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The scrollbar axes contain unknown flags.");
            }

            _ = Set(ref field, value, Invalidation.Measure);
        }
    } = ScrollBars.Both;

    /// <summary>Gets or sets the common chrome reservation policy for enabled scroll axes.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached viewport is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The viewport is disposed.</exception>
    public ShowScrollBars ShowScrollBars
    {
        get;
        set
        {
            Validate(value);

            if (!Set(ref field, value, Invalidation.Measure))
            {
                return;
            }

            ScrollBarVisibility visibility = value switch
            {
                ShowScrollBars.Never => ScrollBarVisibility.Hidden,
                ShowScrollBars.WhenNeeded => ScrollBarVisibility.Auto,
                ShowScrollBars.Always => ScrollBarVisibility.Always,
                _ => throw new UnreachableException(),
            };
            HorizontalBarVisibility = visibility;
            VerticalBarVisibility = visibility;
        }
    } = ShowScrollBars.WhenNeeded;

    /// <summary>Gets or sets the shared chrome form used by both owned bars.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached viewport is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The viewport is disposed.</exception>
    public ScrollBarStyle ScrollBarChrome
    {
        get;
        set
        {
            Validate(value);

            if (Set(ref field, value, Invalidation.Measure))
            {
                _horizontal.Chrome = value;
                _vertical.Chrome = value;
            }
        }
    } = ScrollBarStyle.Full;

    /// <summary>Gets or sets the shared generated glyph treatment used by both owned bars.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached viewport is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The viewport is disposed.</exception>
    public ScrollBarFill ScrollBarFill
    {
        get;
        set
        {
            Validate(value);

            if (Set(ref field, value, Invalidation.Render))
            {
                _horizontal.Fill = value;
                _vertical.Fill = value;
            }
        }
    } = ScrollBarFill.Block;

    /// <summary>Gets or sets whether content receives the finite viewport width while being measured.</summary>
    /// <remarks>
    /// The default preserves intrinsic horizontal extent and hidden-bar scrolling. Enable this for
    /// reading panes whose word-wrapping content should reflow instead of growing a horizontal extent.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The attached viewport is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The viewport is disposed.</exception>
    public bool ConstrainContentToViewport
    {
        get;
        set => _ = Set(ref field, value, Invalidation.Measure);
    }

    /// <summary>Gets or sets the valid horizontal content offset.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside the current extent.</exception>
    /// <exception cref="InvalidOperationException">The attached viewport is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The viewport is disposed.</exception>
    public int HorizontalOffset
    {
        get => _horizontalOffset;
        set
        {
            ValidateOffset(value, MaximumX(), nameof(value));
            _ = Apply(value, VerticalOffset, Cause.Programmatic);
        }
    }

    /// <summary>Gets or sets the valid vertical content offset.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside the current extent.</exception>
    /// <exception cref="InvalidOperationException">The attached viewport is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The viewport is disposed.</exception>
    public int VerticalOffset
    {
        get => _verticalOffset;
        set
        {
            ValidateOffset(value, MaximumY(), nameof(value));
            _ = Apply(HorizontalOffset, value, Cause.Programmatic);
        }
    }

    /// <summary>Gets the committed non-negative content extent.</summary>
    public Size Extent => _extent;

    /// <summary>Gets the committed non-negative visible extent.</summary>
    public Size Viewport => _viewport;

    /// <summary>Gets or sets the non-negative arrow and wheel change in cells.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached viewport is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The viewport is disposed.</exception>
    public int LineSize
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _ = Set(ref field, value, Invalidation.None);
        }
    } = 1;

    /// <summary>Gets or sets the non-negative cells retained between page commands.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached viewport is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The viewport is disposed.</exception>
    public int PageOverlap
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _ = Set(ref field, value, Invalidation.None);
        }
    }

    /// <summary>Adds signed axis deltas with saturation and endpoint clamping.</summary>
    /// <param name="x">The requested horizontal delta.</param>
    /// <param name="y">The requested vertical delta.</param>
    /// <param name="cause">The defined input path.</param>
    /// <returns>True when at least one offset changed.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cause"/> is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached viewport is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The viewport is disposed.</exception>
    public bool ScrollBy(int x, int y, Cause cause = Cause.Programmatic)
    {
        Validate(cause);
        VerifyMutable();
        return Apply(Add(HorizontalOffset, x), Add(VerticalOffset, y), cause);
    }

    /// <summary>Scrolls minimally to expose one owned descendant.</summary>
    /// <param name="descendant">The non-null content descendant.</param>
    /// <returns>True when at least one offset changed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="descendant"/> is null.</exception>
    /// <exception cref="ArgumentException">The control is not inside Content.</exception>
    /// <exception cref="InvalidOperationException">The attached viewport is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The viewport is disposed.</exception>
    public bool BringIntoView(Control descendant)
    {
        ArgumentNullException.ThrowIfNull(descendant);
        VerifyMutable();

        if (!IsContentDescendant(descendant))
        {
            throw new ArgumentException("The control must be inside Content.", nameof(descendant));
        }

        int logicalX = Add(Difference(descendant.Bounds.X, _viewportBounds.X), HorizontalOffset);
        int logicalY = Add(Difference(descendant.Bounds.Y, _viewportBounds.Y), VerticalOffset);
        int x = Reveal(HorizontalOffset, Viewport.Width, logicalX, descendant.Bounds.Width);
        int y = Reveal(VerticalOffset, Viewport.Height, logicalY, descendant.Bounds.Height);
        return Apply(x, y, Cause.BringIntoView);
    }

    /// <inheritdoc/>
    public override Control? HitTest(Point point)
    {
        return IsDisposed || !IsHitTestVisible || !EffectiveIsVisible || !EffectiveIsEnabled ||
            !Bounds.Contains(point)
            ? null
            : HitTestPopup(point) ??
            _vertical.HitTest(point) ??
            _horizontal.HitTest(point) ??
            (_viewportBounds.Contains(point) ? Content?.HitTest(point) : null) ??
            this;
    }

    /// <inheritdoc/>
    internal override int NavigationCount => Children.Count + _chrome.Count;

    /// <inheritdoc/>
    internal override Control NavigationAt(int index) => index < Children.Count
        ? Children[index]
        : _chrome[index - Children.Count];

    /// <inheritdoc/>
    internal override void VisitChildren(Action<Control> visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);

        foreach (Control child in Children)
        {
            visitor(child);
        }

        foreach (Control child in _chrome)
        {
            visitor(child);
        }
    }

    /// <inheritdoc/>
    internal override void DisposeChildren()
    {
        while (Children.Count > 0)
        {
            Children[^1].Dispose();
        }

        while (_chrome.Count > 0)
        {
            Control child = _chrome[^1];
            _chrome.RemoveAt(_chrome.Count - 1);
            child.Dispose();
        }
    }

    /// <inheritdoc/>
    internal override void RenderChildren(TerminalCanvas canvas)
    {
        Content?.Render(canvas.Clip(_viewportBounds));
        _horizontal.Render(canvas);
        _vertical.Render(canvas);
        if (Parent is null)
        {
            RenderPopupLayer(canvas);
        }
    }

    /// <inheritdoc/>
    internal override Control? HitTestPopup(Point point) => Content?.HitTestPopup(point);

    /// <inheritdoc/>
    internal override void RenderPopupLayer(TerminalCanvas canvas) => Content?.RenderPopupLayer(canvas);

    /// <inheritdoc/>
    protected override Size MeasureCore(Constraint constraint)
    {
        Control? content = Content;

        if (content is null)
        {
            _measuredExtent = default;
        }
        else
        {
            // Reading panes can opt into a finite width while the default
            // preserves intrinsic extent for hidden-bar horizontal scrolling.
            content.Measure(new Constraint(
                ConstrainContentToViewport ? constraint.Width : null,
                height: null));
            _measuredExtent = new Size(
                Add(content.DesiredSize.Width, content.Margin.Horizontal),
                Add(content.DesiredSize.Height, content.Margin.Vertical));
        }

        Size available = new(
            constraint.Width ?? _measuredExtent.Width,
            constraint.Height ?? _measuredExtent.Height);
        Resolve(available, _measuredExtent, out bool horizontal, out bool vertical, out _);
        return new Size(
            Add(_measuredExtent.Width, vertical ? 1 : 0),
            Add(_measuredExtent.Height, horizontal ? 1 : 0));
    }

    /// <inheritdoc/>
    protected override void ArrangeCore(Rect bounds)
    {
        Resolve(
            new Size(bounds.Width, bounds.Height),
            _measuredExtent,
            out bool horizontal,
            out bool vertical,
            out Size viewport);
        _viewportBounds = new Rect(bounds.X, bounds.Y, viewport.Width, viewport.Height);
        bool extentChanged = _extent != _measuredExtent;
        bool viewportChanged = _viewport != viewport;
        _ = Set(ref _extent, _measuredExtent, Invalidation.None, nameof(Extent));
        _ = Set(ref _viewport, viewport, Invalidation.None, nameof(Viewport));
        _ = Apply(
            Math.Min(HorizontalOffset, MaximumX()),
            Math.Min(VerticalOffset, MaximumY()),
            extentChanged ? Cause.Content : Cause.Resize);

        Content?.Arrange(
            new Rect(
                Difference(bounds.X, HorizontalOffset),
                Difference(bounds.Y, VerticalOffset),
                Math.Max(Extent.Width, Viewport.Width),
                Math.Max(Extent.Height, Viewport.Height)),
            widthResolved: true,
            heightResolved: true);

        SetVisibility(_horizontal, horizontal);
        SetVisibility(_vertical, vertical);
        _horizontal.Arrange(
            new Rect(bounds.X, bounds.Y + viewport.Height, viewport.Width, horizontal ? 1 : 0),
            widthResolved: true,
            heightResolved: true);
        _vertical.Arrange(
            new Rect(bounds.X + viewport.Width, bounds.Y, vertical ? 1 : 0, viewport.Height),
            widthResolved: true,
            heightResolved: true);
        Synchronize();
        Debug.Assert(!viewportChanged || Viewport == viewport, "Arranged viewport commits atomically.");
    }

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);

        if (!EffectiveIsEnabled || !EffectiveIsVisible)
        {
            return;
        }

        if (eventArgs is KeyEventArgs key)
        {
            Handle(key);
        }
        else if (eventArgs is PointerEventArgs pointer)
        {
            Handle(pointer);
        }
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);

        if (reason == ReleaseReason.Disposed)
        {
            ScrollChanged = null;
        }
    }

    private bool Apply(int x, int y, Cause cause)
    {
        x = Math.Clamp(x, 0, MaximumX());
        y = Math.Clamp(y, 0, MaximumY());
        Point previous = new(HorizontalOffset, VerticalOffset);
        bool changedX = Set(ref _horizontalOffset, x, Invalidation.Arrange, nameof(HorizontalOffset));
        bool changedY = Set(ref _verticalOffset, y, Invalidation.Arrange, nameof(VerticalOffset));

        if (!changedX && !changedY)
        {
            return false;
        }

        Synchronize();
        ScrollChanged?.Invoke(
            this,
            new ScrollChangedEventArgs(previous, new Point(x, y), Extent, Viewport, cause));
        return true;
    }

    private void Synchronize()
    {
        if (_syncing)
        {
            return;
        }

        _syncing = true;

        try
        {
            Configure(_horizontal, MaximumX(), Viewport.Width, HorizontalOffset);
            Configure(_vertical, MaximumY(), Viewport.Height, VerticalOffset);
        }
        finally
        {
            _syncing = false;
        }
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
        bar.Value = value;
    }

    private void OnHorizontalChanged(object? sender, ScrollEventArgs eventArgs)
    {
        _ = sender;

        if (!_syncing)
        {
            _ = Apply(eventArgs.Value, VerticalOffset, eventArgs.Cause);
        }
    }

    private void OnVerticalChanged(object? sender, ScrollEventArgs eventArgs)
    {
        _ = sender;

        if (!_syncing)
        {
            _ = Apply(HorizontalOffset, eventArgs.Value, eventArgs.Cause);
        }
    }

    private void Handle(KeyEventArgs eventArgs)
    {
        if (eventArgs.Stroke.Action is not (KeyAction.Press or KeyAction.Repeat))
        {
            return;
        }

        int page = Math.Max(0, Viewport.Height - Math.Min(PageOverlap, Viewport.Height));
        Code code = eventArgs.Stroke.Code;

        if (code == Code.Left)
        {
            _ = ScrollBy(-LineSize, 0, Cause.Keyboard);
        }
        else if (code == Code.Right)
        {
            _ = ScrollBy(LineSize, 0, Cause.Keyboard);
        }
        else if (code == Code.Up)
        {
            _ = ScrollBy(0, -LineSize, Cause.Keyboard);
        }
        else if (code == Code.Down)
        {
            _ = ScrollBy(0, LineSize, Cause.Keyboard);
        }
        else if (code == Code.PageUp)
        {
            _ = ScrollBy(0, -page, Cause.Keyboard);
        }
        else if (code == Code.PageDown)
        {
            _ = ScrollBy(0, page, Cause.Keyboard);
        }
        else if (code == Code.Home)
        {
            _ = Apply(HorizontalOffset, 0, Cause.Keyboard);
        }
        else if (code == Code.End)
        {
            _ = Apply(HorizontalOffset, MaximumY(), Cause.Keyboard);
        }
        else
        {
            return;
        }

        eventArgs.Handled = true;
    }

    private void Handle(PointerEventArgs eventArgs)
    {
        Pointer pointer = eventArgs.Pointer;

        if (pointer.Action != PointerAction.Wheel)
        {
            return;
        }

        int x = MultiplyNegative(pointer.WheelX, LineSize);
        int y = MultiplyNegative(pointer.WheelY, LineSize);
        int remainingX = x;
        int remainingY = y;

        for (ScrollView? current = this; current is not null && (remainingX != 0 || remainingY != 0);
            current = Ancestor(current))
        {
            int previousX = current.HorizontalOffset;
            int previousY = current.VerticalOffset;
            _ = current.ScrollBy(remainingX, remainingY, Cause.Wheel);
            remainingX = Difference(remainingX, current.HorizontalOffset - previousX);
            remainingY = Difference(remainingY, current.VerticalOffset - previousY);
        }

        eventArgs.Handled = x != 0 || y != 0;
    }

    private static ScrollView? Ancestor(Control control)
    {
        for (Container? current = control.Parent; current is not null; current = current.Parent)
        {
            if (current is ScrollView view)
            {
                return view;
            }
        }

        return null;
    }

    private bool IsContentDescendant(Control value)
    {
        for (Control? current = value; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, Content))
            {
                return true;
            }
        }

        return false;
    }

    private int MaximumX() => (ScrollBars & ScrollBars.Horizontal) != 0
        ? Math.Max(0, Extent.Width - Viewport.Width)
        : 0;

    private int MaximumY() => (ScrollBars & ScrollBars.Vertical) != 0
        ? Math.Max(0, Extent.Height - Viewport.Height)
        : 0;

    private static int Reveal(int current, int viewport, int start, int length)
    {
        if (start < current)
        {
            return start;
        }

        int end = Add(start, length);
        return end > Add(current, viewport) ? Math.Max(0, end - viewport) : current;
    }

    private void Resolve(
        Size available,
        Size extent,
        out bool horizontal,
        out bool vertical,
        out Size viewport)
    {
        horizontal = (ScrollBars & ScrollBars.Horizontal) != 0 &&
            HorizontalBarVisibility == ScrollBarVisibility.Always;
        vertical = (ScrollBars & ScrollBars.Vertical) != 0 &&
            VerticalBarVisibility == ScrollBarVisibility.Always;

        // Automatic bars are added monotonically because one reserved axis can
        // induce overflow on the other. Two additions are the finite maximum.
        for (int probe = 0; probe < 2; probe++)
        {
            viewport = new Size(
                Math.Max(0, available.Width - (vertical ? 1 : 0)),
                Math.Max(0, available.Height - (horizontal ? 1 : 0)));
            bool addHorizontal = (ScrollBars & ScrollBars.Horizontal) != 0 &&
                HorizontalBarVisibility == ScrollBarVisibility.Auto &&
                extent.Width > viewport.Width;
            bool addVertical = (ScrollBars & ScrollBars.Vertical) != 0 &&
                VerticalBarVisibility == ScrollBarVisibility.Auto &&
                extent.Height > viewport.Height;
            bool nextHorizontal = horizontal || addHorizontal;
            bool nextVertical = vertical || addVertical;

            if (nextHorizontal == horizontal && nextVertical == vertical)
            {
                return;
            }

            horizontal = nextHorizontal;
            vertical = nextVertical;
        }

        viewport = new Size(
            Math.Max(0, available.Width - (vertical ? 1 : 0)),
            Math.Max(0, available.Height - (horizontal ? 1 : 0)));
    }

    private static void SetVisibility(Control control, bool visible) =>
        control.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;

    private static void ValidateOffset(int value, int maximum, string name)
    {
        if (value < 0 || value > maximum)
        {
            throw new ArgumentOutOfRangeException(name, value, "Offset must be inside the current extent.");
        }
    }

    private static void Validate<T>(T value) where T : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "The enum value is unknown.");
        }
    }

    private static int Add(int left, int right) =>
        (int) Math.Clamp((long) left + right, 0, int.MaxValue);

    private static int Difference(int left, int right) =>
        (int) Math.Clamp((long) left - right, int.MinValue, int.MaxValue);

    private static int MultiplyNegative(int left, int right) =>
        (int) Math.Clamp(-(long) left * right, int.MinValue, int.MaxValue);
}
