// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;


using SharpVision.Scrolling;
using SharpVision.Terminal.Input;

/// <summary>Defines a mutable control that owns an ordered child collection.</summary>
public abstract class Container: Control
{
    /// <summary>Initializes an empty ordered child collection.</summary>
    protected Container() : this(int.MaxValue)
    {
    }

    /// <summary>Initializes an empty ordered child collection with a finite capacity.</summary>
    /// <param name="capacity">The non-negative maximum child count.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is negative.</exception>
    protected Container(int capacity) => Children = new Children(this, capacity);

    /// <summary>Gets the owned ordered children.</summary>
    public Children Children { get; }

    /// <inheritdoc/>
    public override Control? HitTest(Point point)
    {
        if (!CanHitTestSelf(point, requireContainment: false))
        {
            return null;
        }

        if (HitTestPopup(point) is { } popup)
        {
            return popup;
        }

        var contains = Bounds.Contains(point);

        if (!contains && (AutoScroll || ClipsChildren))
        {
            return null;
        }

        if (AutoScroll)
        {
            var bar = _bars is not null
                ? _vertical!.HitTest(point) ?? _horizontal!.HitTest(point)
                : null;

            return bar ?? (_viewportBounds.Contains(point) ? HitTestChildren(point) : null) ?? this;
        }

        return HitTestChildren(point) ?? (contains ? this : null);
    }

    private Control? HitTestChildren(Point point)
    {
        for (var index = Children.Count - 1; index >= 0; index--)
        {
            if (Children[index].HitTest(point) is { } child)
            {
                return child;
            }
        }

        return null;
    }

    /// <inheritdoc/>
    internal override void RenderChildren(TerminalCanvas canvas)
    {
        if (!AutoScroll)
        {
            RenderContent(canvas);
            return;
        }

        RenderContent(canvas.Clip(_viewportBounds));
        _horizontal?.Render(canvas);
        _vertical?.Render(canvas);
    }

    /// <inheritdoc/>
    internal override void RenderContent(TerminalCanvas canvas)
    {
        foreach (var child in Children)
        {
            if (child.RendersInNormalLayer)
            {
                child.Render(canvas);
            }
        }
    }

    /// <inheritdoc/>
    internal override void RenderPopupLayer(TerminalCanvas canvas) => base.RenderPopupLayer(canvas);

    #region Grow and shrink

    /// <summary>Gets or sets whether this container sizes its border box to its content, overriding stretch and star sizing.</summary>
    /// <remarks>Honors <see cref="Control.MinWidth"/>/<see cref="Control.MaxWidth"/> and the height equivalents. See <see cref="AutoSizeMode"/>.</remarks>
    /// <exception cref="InvalidOperationException">The attached container is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The container is disposed.</exception>
    public bool AutoSize
    {
        get;
        set => _ = SetProperty(ref field, value, ChangeImpact.Measure);
    }

    /// <summary>Gets or sets whether an auto-sizing axis may shrink below its explicit fixed-cell size.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached container is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The container is disposed.</exception>
    public AutoSizeMode AutoSizeMode
    {
        get;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The auto-size mode is unknown.");
            }

            _ = SetProperty(ref field, value, ChangeImpact.Measure);
        }
    } = AutoSizeMode.GrowAndShrink;

    /// <inheritdoc/>
    internal override bool ShrinkWrapsWidth => AutoSize;

    /// <inheritdoc/>
    internal override bool ShrinkWrapsHeight => AutoSize;

    // AutoSize sizes to content on both axes, so content is measured unbounded
    // (unclamped by an explicit Width/Height) to discover its natural size.
    /// <inheritdoc/>
    internal override Constraint OnMeasuringContent(Constraint content)
    {
        if (AutoSize)
        {
            return new Constraint(null, null);
        }

        if (!AutoScroll)
        {
            return content;
        }

        // Eligible axes measure unbounded so children report natural extent
        // (ResolveMeasureAxis clamps DesiredSize, which would otherwise hide overflow).
        var width = (ScrollBars & ScrollBars.Horizontal) != 0 ? null : content.Width;
        var height = (ScrollBars & ScrollBars.Vertical) != 0 ? null : content.Height;
        return new Constraint(width, height);
    }

    /// <inheritdoc/>
    internal override Size OnMeasuredDesired(Size desired) => !AutoSize
        ? desired
        : new Size(
            AutoSizeAxis(
                ContentExtent.Width,
                Add(Padding.Horizontal, BorderThickness.Horizontal),
                Width,
                MinWidth,
                MaxWidth),
            AutoSizeAxis(
                ContentExtent.Height,
                Add(Padding.Vertical, BorderThickness.Vertical),
                Height,
                MinHeight,
                MaxHeight));

    // GrowAndShrink fits content exactly; GrowOnly never shrinks below an explicit
    // fixed-cell size. Both honor Min/Max.
    private int AutoSizeAxis(int contentExtent, int inset, Length length, int minimum, int maximum)
    {
        Debug.Assert(contentExtent >= 0 && inset >= 0, "Auto-size inputs are non-negative cell extents.");
        Debug.Assert(minimum >= 0 && maximum >= minimum, "Auto-size limits are validated and ordered.");

        var content = (long) contentExtent + inset;
        var floor = AutoSizeMode == AutoSizeMode.GrowOnly && length.Kind == Kind.Cells
            ? (int) length.Value
            : 0;
        var requested = Math.Max(content, floor);
        return (int) Math.Clamp(requested, minimum, maximum);
    }

    #endregion

    #region Scrolling

    private Size _extent;
    private Size _viewport;
    private int _horizontalOffset;
    private int _verticalOffset;
    private protected Rect _viewportBounds;
    private bool _reserveHorizontal;
    private bool _reserveVertical;
    private protected Children? _bars;
    private ScrollBar? _horizontal;
    private ScrollBar? _vertical;
    private bool _syncing;

    /// <summary>Raised after one or both offsets commit.</summary>
    public event EventHandler<ScrollChangedEventArgs>? ScrollChanged;

    /// <summary>Gets or sets whether this container clips and offsets overflowing content along enabled axes.</summary>
    /// <exception cref="InvalidOperationException">The attached container is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The container is disposed.</exception>
    public bool AutoScroll
    {
        get;
        set
        {
            if (!SetProperty(ref field, value, ChangeImpact.Measure))
            {
                return;
            }

            // Bars are created when scrolling is armed rather than lazily in
            // ResolveContentSlot. Lazy creation there added children
            // mid-arrange, which invalidates this container's own measure and
            // can prevent nested armed containers from ever converging to a
            // settled layout.
            if (value)
            {
                EnsureBars();
            }
            else
            {
                _horizontalOffset = 0;
                _verticalOffset = 0;

                if (_bars is not null)
                {
                    SetVisibility(_horizontal!, visible: false);
                    SetVisibility(_vertical!, visible: false);
                }
            }
        }
    }

    /// <summary>Gets or sets the axes that may scroll within this container.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value contains unknown axis flags.</exception>
    /// <exception cref="InvalidOperationException">The attached container is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The container is disposed.</exception>
    public ScrollBars ScrollBars
    {
        get;
        set
        {
            if ((value & ~ScrollBars.Both) != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The scrollbar axes contain unknown flags.");
            }

            _ = SetProperty(ref field, value, ChangeImpact.Measure);
        }
    } = ScrollBars.Vertical;

    /// <summary>Gets or sets the common chrome reservation policy for enabled scroll axes.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached container is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The container is disposed.</exception>
    public ShowScrollBars ShowScrollBars
    {
        get;
        set
        {
            Validate(value);

            if (!SetProperty(ref field, value, ChangeImpact.Measure))
            {
                return;
            }

            var visibility = value switch
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

    /// <summary>Gets or sets horizontal bar reservation policy.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached container is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The container is disposed.</exception>
    public ScrollBarVisibility HorizontalBarVisibility
    {
        get;
        set
        {
            Validate(value);
            _ = SetProperty(ref field, value, ChangeImpact.Measure);
        }
    } = ScrollBarVisibility.Auto;

    /// <summary>Gets or sets vertical bar reservation policy.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached container is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The container is disposed.</exception>
    public ScrollBarVisibility VerticalBarVisibility
    {
        get;
        set
        {
            Validate(value);
            _ = SetProperty(ref field, value, ChangeImpact.Measure);
        }
    } = ScrollBarVisibility.Auto;

    /// <summary>Gets or sets the shared chrome form used by both owned bars.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached container is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The container is disposed.</exception>
    public ScrollBarChrome ScrollBarChrome
    {
        get;
        set
        {
            Validate(value);

            if (!SetProperty(ref field, value, ChangeImpact.Measure))
            {
                return;
            }

            _ = _horizontal?.Chrome = value;
            _ = _vertical?.Chrome = value;
        }
    } = ScrollBarChrome.Full;

    /// <summary>Gets or sets the shared generated glyph treatment used by both owned bars.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached container is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The container is disposed.</exception>
    public ScrollBarFill ScrollBarFill
    {
        get;
        set
        {
            Validate(value);

            if (!SetProperty(ref field, value, ChangeImpact.Render))
            {
                return;
            }

            _ = _horizontal?.Fill = value;
            _ = _vertical?.Fill = value;
        }
    } = ScrollBarFill.Block;

    /// <summary>Gets or sets the non-negative arrow and wheel change in cells.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached container is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The container is disposed.</exception>
    public int LineSize
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _ = SetProperty(ref field, value, ChangeImpact.None);
        }
    } = 1;

    /// <summary>Gets or sets the non-negative cells retained between page commands.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached container is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The container is disposed.</exception>
    public int PageOverlap
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _ = SetProperty(ref field, value, ChangeImpact.None);
        }
    }

    /// <summary>Gets the committed non-negative content extent.</summary>
    public Size Extent => _extent;

    /// <summary>Gets the committed non-negative visible extent.</summary>
    public Size Viewport => _viewport;

    /// <summary>Gets or sets the valid horizontal content offset.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside the current extent.</exception>
    /// <exception cref="InvalidOperationException">The attached container is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The container is disposed.</exception>
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
    /// <exception cref="InvalidOperationException">The attached container is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The container is disposed.</exception>
    public int VerticalOffset
    {
        get => _verticalOffset;
        set
        {
            ValidateOffset(value, MaximumY(), nameof(value));
            _ = Apply(HorizontalOffset, value, Cause.Programmatic);
        }
    }

    /// <summary>Adds signed axis deltas with saturation and endpoint clamping.</summary>
    /// <param name="x">The requested horizontal delta.</param>
    /// <param name="y">The requested vertical delta.</param>
    /// <param name="cause">The defined input path.</param>
    /// <returns>True when at least one offset changed.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cause"/> is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached container is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The container is disposed.</exception>
    public bool ScrollBy(int x, int y, Cause cause = Cause.Programmatic)
    {
        Validate(cause);
        VerifyMutable();
        return Apply(Add(HorizontalOffset, x), Add(VerticalOffset, y), cause);
    }

    /// <summary>Scrolls minimally to expose one descendant of this container.</summary>
    /// <param name="descendant">The non-null descendant control.</param>
    /// <returns>True when at least one offset changed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="descendant"/> is null.</exception>
    /// <exception cref="ArgumentException">The control is not a descendant of this container.</exception>
    /// <exception cref="InvalidOperationException">The attached container is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The container is disposed.</exception>
    public bool BringIntoView(Control descendant)
    {
        ArgumentNullException.ThrowIfNull(descendant);
        VerifyMutable();

        if (!IsContentDescendant(descendant))
        {
            throw new ArgumentException("The control must be a descendant of this container.", nameof(descendant));
        }

        var logicalX = Add(Difference(descendant.Bounds.X, _viewportBounds.X), HorizontalOffset);
        var logicalY = Add(Difference(descendant.Bounds.Y, _viewportBounds.Y), VerticalOffset);
        var x = Reveal(HorizontalOffset, Viewport.Width, logicalX, descendant.Bounds.Width);
        var y = Reveal(VerticalOffset, Viewport.Height, logicalY, descendant.Bounds.Height);
        return Apply(x, y, Cause.BringIntoView);
    }

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);

        if (!AutoScroll || !EffectiveIsEnabled || !EffectiveIsVisible)
        {
            base.OnEvent(eventArgs);
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

        if (!eventArgs.Handled)
        {
            base.OnEvent(eventArgs);
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

    private void Handle(KeyEventArgs eventArgs)
    {
        if (eventArgs.Stroke.Action is not (KeyAction.Press or KeyAction.Repeat))
        {
            return;
        }

        var page = Math.Max(0, Viewport.Height - Math.Min(PageOverlap, Viewport.Height));
        var code = eventArgs.Stroke.Code;

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
        var pointer = eventArgs.Pointer;

        if (pointer.Action != PointerAction.Wheel)
        {
            return;
        }

        var x = MultiplyNegative(pointer.WheelX, LineSize);
        var y = MultiplyNegative(pointer.WheelY, LineSize);
        var remainingX = x;
        var remainingY = y;

        for (var current = this; current is not null && (remainingX != 0 || remainingY != 0);
            current = Ancestor(current))
        {
            var previousX = current.HorizontalOffset;
            var previousY = current.VerticalOffset;
            _ = current.ScrollBy(remainingX, remainingY, Cause.Wheel);
            remainingX = Difference(remainingX, current.HorizontalOffset - previousX);
            remainingY = Difference(remainingY, current.VerticalOffset - previousY);
        }

        eventArgs.Handled = x != 0 || y != 0;
    }

    private static Container? Ancestor(Control control)
    {
        Debug.Assert(control is not null, "Scrollable ancestor lookup requires a control.");

        for (var current = control.Parent; current is not null; current = current.Parent)
        {
            if (current is Container { AutoScroll: true } container)
            {
                return container;
            }
        }

        return null;
    }

    /// <inheritdoc/>
    internal override Rect ResolveContentSlot(Rect padded)
    {
        if (!AutoScroll)
        {
            var box = new Size(padded.Width, padded.Height);
            _ = SetProperty(ref _extent, box, ChangeImpact.None, nameof(Extent));
            _ = SetProperty(ref _viewport, box, ChangeImpact.None, nameof(Viewport));
            _viewportBounds = padded;
            _horizontalOffset = 0;
            _verticalOffset = 0;
            return padded;
        }

        if (HorizontalBarVisibility != ScrollBarVisibility.Hidden || VerticalBarVisibility != ScrollBarVisibility.Hidden)
        {
            EnsureBars();
        }

        Resolve(new Size(padded.Width, padded.Height), ContentExtent, out var horizontal, out var vertical, out var viewport);
        _viewportBounds = new Rect(padded.X, padded.Y, viewport.Width, viewport.Height);
        var extentChanged = _extent != ContentExtent;
        _ = SetProperty(ref _extent, ContentExtent, ChangeImpact.None, nameof(Extent));
        _ = SetProperty(ref _viewport, viewport, ChangeImpact.None, nameof(Viewport));
        _reserveHorizontal = horizontal;
        _reserveVertical = vertical;
        _ = Apply(
            Math.Min(HorizontalOffset, MaximumX()),
            Math.Min(VerticalOffset, MaximumY()),
            extentChanged ? Cause.Content : Cause.Resize);

        return new Rect(
            Difference(padded.X, HorizontalOffset),
            Difference(padded.Y, VerticalOffset),
            Math.Max(Extent.Width, viewport.Width),
            Math.Max(Extent.Height, viewport.Height));
    }

    /// <inheritdoc/>
    internal override void ArrangeOverlays(Rect padded)
    {
        if (!AutoScroll || _bars is null)
        {
            return;
        }

        Debug.Assert(_horizontal is not null && _vertical is not null, "Created scrollbar chrome owns both axes.");

        SetVisibility(_horizontal, _reserveHorizontal);
        SetVisibility(_vertical, _reserveVertical);
        _horizontal.Arrange(
            new Rect(padded.X, padded.Y + _viewportBounds.Height, _viewportBounds.Width, _reserveHorizontal ? 1 : 0),
            widthResolved: true,
            heightResolved: true);
        _vertical.Arrange(
            new Rect(padded.X + _viewportBounds.Width, padded.Y, _reserveVertical ? 1 : 0, _viewportBounds.Height),
            widthResolved: true,
            heightResolved: true);
        Synchronize();
    }

    private void EnsureBars()
    {
        if (_bars is not null)
        {
            return;
        }

        _horizontal = new ScrollBar
        {
            Orientation = Orientation.Horizontal,
            Chrome = ScrollBarChrome,
            Fill = ScrollBarFill,
        };
        _vertical = new ScrollBar
        {
            Orientation = Orientation.Vertical,
            Chrome = ScrollBarChrome,
            Fill = ScrollBarFill,
        };
        _horizontal.ValueChanged += OnHorizontalChanged;
        _vertical.ValueChanged += OnVerticalChanged;
        _bars = new Children(
            this,
            capacity: 2,
            new OwnedControlOptions(
                OwnedControlRole.FrameworkPart,
                OwnedControlLayer.Normal,
                participatesInHitTesting: true,
                participatesInNavigation: true,
                partKey: "scroll-bars",
                ChangeImpact.Measure))
        {
            _horizontal,
            _vertical,
        };

        Debug.Assert(_bars.Count == 2, "Scrollbar chrome owns exactly one control per axis.");
    }

    private void Synchronize()
    {
        if (_syncing || _bars is null)
        {
            return;
        }

        Debug.Assert(_horizontal is not null && _vertical is not null, "Scrollbar synchronization requires both axes.");

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
        Debug.Assert(bar is not null, "Scrollbar configuration requires an owned bar.");
        Debug.Assert(maximum >= 0 && viewport >= 0, "Scrollbar geometry is non-negative.");
        Debug.Assert(value >= 0 && value <= maximum, "Scrollbar value is clamped before synchronization.");

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

    private static void SetVisibility(Control control, bool visible) =>
        control.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;

    private bool Apply(int x, int y, Cause cause)
    {
        Debug.Assert(Enum.IsDefined(cause), "Scroll changes require a defined cause.");

        x = Math.Clamp(x, 0, MaximumX());
        y = Math.Clamp(y, 0, MaximumY());
        var previous = new Point(HorizontalOffset, VerticalOffset);
        var changedX = SetProperty(ref _horizontalOffset, x, ChangeImpact.Arrange, nameof(HorizontalOffset));
        var changedY = SetProperty(ref _verticalOffset, y, ChangeImpact.Arrange, nameof(VerticalOffset));

        if (!changedX && !changedY)
        {
            return false;
        }

        Synchronize();
        ScrollChanged?.Invoke(this, new ScrollChangedEventArgs(previous, new Point(x, y), Extent, Viewport, cause));
        return true;
    }

    private bool IsContentDescendant(Control value)
    {
        Debug.Assert(value is not null, "Descendant checks require a control.");

        for (var current = value; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, this))
            {
                return true;
            }
        }

        return false;
    }

    private static int Reveal(int current, int viewport, int start, int length)
    {
        Debug.Assert(current >= 0 && viewport >= 0, "Reveal uses a non-negative viewport.");
        Debug.Assert(start >= 0 && length >= 0, "Reveal uses non-negative content geometry.");

        if (start < current)
        {
            return start;
        }

        var end = Add(start, length);
        return end > Add(current, viewport) ? Math.Max(0, end - viewport) : current;
    }

    private int MaximumX() => AutoScroll && (ScrollBars & ScrollBars.Horizontal) != 0
        ? Math.Max(0, Extent.Width - Viewport.Width)
        : 0;

    private int MaximumY() => AutoScroll && (ScrollBars & ScrollBars.Vertical) != 0
        ? Math.Max(0, Extent.Height - Viewport.Height)
        : 0;

    private void Resolve(
        Size available,
        Size extent,
        out bool horizontal,
        out bool vertical,
        out Size viewport)
    {
        Debug.Assert(available.Width >= 0 && available.Height >= 0, "Scrollbar resolution uses available cell extents.");
        Debug.Assert(extent.Width >= 0 && extent.Height >= 0, "Scrollbar resolution uses non-negative content extents.");

        horizontal = (ScrollBars & ScrollBars.Horizontal) != 0 &&
            HorizontalBarVisibility == ScrollBarVisibility.Always;
        vertical = (ScrollBars & ScrollBars.Vertical) != 0 &&
            VerticalBarVisibility == ScrollBarVisibility.Always;

        // Automatic bars are added monotonically because one reserved axis can
        // induce overflow on the other. Two additions are the finite maximum.
        for (var probe = 0; probe < 2; probe++)
        {
            viewport = new Size(
                Math.Max(0, available.Width - (vertical ? 1 : 0)),
                Math.Max(0, available.Height - (horizontal ? 1 : 0)));
            var addHorizontal = (ScrollBars & ScrollBars.Horizontal) != 0 &&
                HorizontalBarVisibility == ScrollBarVisibility.Auto &&
                extent.Width > viewport.Width;
            var addVertical = (ScrollBars & ScrollBars.Vertical) != 0 &&
                VerticalBarVisibility == ScrollBarVisibility.Auto &&
                extent.Height > viewport.Height;
            var nextHorizontal = horizontal || addHorizontal;
            var nextVertical = vertical || addVertical;

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

    private static void ValidateOffset(int value, int maximum, string name)
    {
        Debug.Assert(maximum >= 0, "Offset validation uses a non-negative maximum.");
        Debug.Assert(!string.IsNullOrWhiteSpace(name), "Offset validation identifies its public argument.");

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

    #endregion
}
