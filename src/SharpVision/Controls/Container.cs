// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;


using SharpVision.Scrolling;

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

    /// <summary>Gets the number of children participating in default navigation.</summary>
    internal virtual int NavigationCount => Children.Count;

    /// <summary>Gets one child in default navigation order.</summary>
    /// <param name="index">The zero-based navigation index.</param>
    /// <returns>The child at the requested navigation position.</returns>
    internal virtual Control NavigationAt(int index) => Children[index];

    /// <inheritdoc/>
    public override Control? HitTest(Point point)
    {
        if (HitTestPopup(point) is { } popup)
        {
            return popup;
        }

        Control? hit = base.HitTest(point);

        if (hit is null)
        {
            return null;
        }

        for (int index = Children.Count - 1; index >= 0; index--)
        {
            if (Children[index].HitTest(point) is { } child)
            {
                return child;
            }
        }

        return this;
    }

    /// <inheritdoc/>
    internal override void VisitChildren(Action<Control> visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);

        foreach (Control child in Children)
        {
            visitor(child);
        }
    }

    /// <inheritdoc/>
    internal override Control? HitTestPopup(Point point)
    {
        for (int index = Children.Count - 1; index >= 0; index--)
        {
            if (Children[index].HitTestPopup(point) is { } popup)
            {
                return popup;
            }
        }

        return null;
    }

    /// <inheritdoc/>
    internal override void DisposeChildren()
    {
        while (Children.Count > 0)
        {
            Children[^1].Dispose();
        }
    }

    /// <inheritdoc/>
    internal override void RenderChildren(TerminalCanvas canvas)
    {
        foreach (Control child in Children)
        {
            child.Render(canvas);
        }

        if (Parent is null)
        {
            RenderOwnedPopupLayer(canvas);
        }
    }

    /// <inheritdoc/>
    internal override void RenderPopupLayer(TerminalCanvas canvas)
        => RenderOwnedPopupLayer(canvas);

    private void RenderOwnedPopupLayer(TerminalCanvas canvas)
    {
        foreach (Control child in Children)
        {
            child.RenderPopupLayer(canvas);
        }
    }

    #region Grow and shrink

    /// <summary>Gets or sets whether this container sizes its border box to its content, overriding stretch and star sizing.</summary>
    /// <remarks>Honors <see cref="Control.MinWidth"/>/<see cref="Control.MaxWidth"/> and the height equivalents. See <see cref="AutoSizeMode"/>.</remarks>
    /// <exception cref="InvalidOperationException">The attached container is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The container is disposed.</exception>
    public bool AutoSize
    {
        get;
        set => _ = Set(ref field, value, Invalidation.Measure);
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

            _ = Set(ref field, value, Invalidation.Measure);
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
        int? width = (ScrollBars & ScrollBars.Horizontal) != 0 ? null : content.Width;
        int? height = (ScrollBars & ScrollBars.Vertical) != 0 ? null : content.Height;
        return new Constraint(width, height);
    }

    /// <inheritdoc/>
    internal override Size OnMeasuredDesired(Size desired) => !AutoSize
        ? desired
        : new Size(
            AutoSizeAxis(ContentExtent.Width, Padding.Horizontal, Width, MinWidth, MaxWidth),
            AutoSizeAxis(ContentExtent.Height, Padding.Vertical, Height, MinHeight, MaxHeight));

    // GrowAndShrink fits content exactly; GrowOnly never shrinks below an explicit
    // fixed-cell size. Both honor Min/Max.
    private int AutoSizeAxis(int contentExtent, int padding, Length length, int minimum, int maximum)
    {
        long content = (long) contentExtent + padding;
        int floor = AutoSizeMode == AutoSizeMode.GrowOnly && length.Kind == Kind.Cells
            ? (int) length.Value
            : 0;
        long requested = Math.Max(content, floor);
        return (int) Math.Clamp(requested, minimum, maximum);
    }

    #endregion

    #region Scrolling

    private Size _extent;
    private Size _viewport;
    private int _horizontalOffset;
    private int _verticalOffset;

    // Consumed starting in Task 6 (bar chrome reservation/arrange) and Task 9 (BringIntoView hit-testing).
    [SuppressMessage("Performance", "IDE0052:Remove unread private members", Justification = "Consumed starting in Task 6/9.")]
    private Rect _viewportBounds;

    [SuppressMessage("Performance", "IDE0052:Remove unread private members", Justification = "Consumed starting in Task 6.")]
    private bool _reserveHorizontal;

    [SuppressMessage("Performance", "IDE0052:Remove unread private members", Justification = "Consumed starting in Task 6.")]
    private bool _reserveVertical;

    /// <summary>Gets or sets whether this container clips and offsets overflowing content along enabled axes.</summary>
    /// <exception cref="InvalidOperationException">The attached container is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The container is disposed.</exception>
    public bool AutoScroll
    {
        get;
        set
        {
            if (Set(ref field, value, Invalidation.Measure) && !value)
            {
                _horizontalOffset = 0;
                _verticalOffset = 0;
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

            _ = Set(ref field, value, Invalidation.Measure);
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
            _ = Set(ref field, value, Invalidation.Measure);
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
            _ = Set(ref field, value, Invalidation.Measure);
        }
    } = ScrollBarVisibility.Auto;

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
            _ = Set(ref field, value, Invalidation.None);
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
            _ = Set(ref field, value, Invalidation.None);
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

    /// <inheritdoc/>
    internal override Rect ResolveContentSlot(Rect padded)
    {
        if (!AutoScroll)
        {
            Size box = new(padded.Width, padded.Height);
            _ = Set(ref _extent, box, Invalidation.None, nameof(Extent));
            _ = Set(ref _viewport, box, Invalidation.None, nameof(Viewport));
            _viewportBounds = padded;
            _horizontalOffset = 0;
            _verticalOffset = 0;
            return padded;
        }

        Resolve(new Size(padded.Width, padded.Height), ContentExtent, out bool horizontal, out bool vertical, out Size viewport);
        _viewportBounds = new Rect(padded.X, padded.Y, viewport.Width, viewport.Height);
        _ = Set(ref _extent, ContentExtent, Invalidation.None, nameof(Extent));
        _ = Set(ref _viewport, viewport, Invalidation.None, nameof(Viewport));
        _reserveHorizontal = horizontal;
        _reserveVertical = vertical;
        _ = Apply(Math.Min(HorizontalOffset, MaximumX()), Math.Min(VerticalOffset, MaximumY()), Cause.Resize);

        return new Rect(
            Difference(padded.X, HorizontalOffset),
            Difference(padded.Y, VerticalOffset),
            Math.Max(Extent.Width, viewport.Width),
            Math.Max(Extent.Height, viewport.Height));
    }

    private bool Apply(int x, int y, Cause cause)
    {
        _ = cause;
        x = Math.Clamp(x, 0, MaximumX());
        y = Math.Clamp(y, 0, MaximumY());
        bool changedX = Set(ref _horizontalOffset, x, Invalidation.Arrange, nameof(HorizontalOffset));
        bool changedY = Set(ref _verticalOffset, y, Invalidation.Arrange, nameof(VerticalOffset));
        return changedX || changedY;
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

    #endregion
}
