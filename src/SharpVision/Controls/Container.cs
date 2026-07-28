// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using Scrolling;

using SharpVision.Terminal.Input;

/// <summary>Defines a mutable control that owns an ordered child collection.</summary>
[PublicAPI]
public abstract class Container: Control
{
    /// <summary>Initializes an empty ordered child collection.</summary>
    protected Container() : this(int.MaxValue)
    {
    }

    /// <summary>Initializes an empty ordered child collection with a finite capacity.</summary>
    /// <param name="capacity">The non-negative maximum child count.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is negative.</exception>
    protected Container(int capacity) => Children = new ControlCollection(this, capacity);

    /// <summary>Gets the owned ordered children.</summary>
    public ControlCollection Children { get; }

    /// <summary>Measures the concrete container's public children within the supplied content constraint.</summary>
    /// <param name="constraint">The non-negative content-box constraint.</param>
    /// <returns>The non-negative intrinsic content size.</returns>
    protected abstract override Size MeasureOverride(Constraint constraint);

    /// <summary>Arranges the concrete container's public children within the committed content box.</summary>
    /// <param name="bounds">The non-negative content-box rectangle.</param>
    protected abstract override void ArrangeOverride(Rect bounds);

    /// <inheritdoc/>
    internal override bool ClipsDescendantVisualOverflow => AutoScroll;

    /// <inheritdoc/>
    internal override Control? HitTest(Point point)
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
            var bar = _scroll.Bars is not null
                ? _scroll.Vertical!.HitTest(point) ?? _scroll.Horizontal!.HitTest(point)
                : null;

            return bar ?? (_scroll.ViewportBounds.Contains(point) ? HitTestChildren(point) : null) ?? this;
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
    internal override void RenderChildren(TerminalCanvas canvas, Rect contentClip)
    {
        if (!AutoScroll)
        {
            RenderContent(canvas, contentClip);
            return;
        }

        var viewportCanvas = canvas.Clip(_scroll.ViewportBounds);
        var viewportClip = contentClip.Intersect(_scroll.ViewportBounds);
        RenderContent(viewportCanvas, viewportClip);
        _scroll.Horizontal?.Render(canvas, contentClip);
        _scroll.Vertical?.Render(canvas, contentClip);
    }

    /// <inheritdoc/>
    internal override void RenderContent(TerminalCanvas canvas, Rect contentClip)
    {
        foreach (var child in Children)
        {
            if (child.RendersInNormalLayer)
            {
                child.Render(canvas, contentClip);
            }
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
        set => _ = SetProperty(ref field, value, InvalidationImpact.Measure);
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

            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
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
    internal override Size OnMeasuredDesired(Size desired)
    {
        var result = !AutoSize
            ? desired
            : new Size(
                AutoSizeAxis(
                    ContentExtent.Width,
                    LayoutMath.Add(Padding.Horizontal, BorderInset.Horizontal),
                    Width,
                    MinWidth,
                    MaxWidth),
                AutoSizeAxis(
                    ContentExtent.Height,
                    LayoutMath.Add(Padding.Vertical, BorderInset.Vertical),
                    Height,
                    MinHeight,
                    MaxHeight));

        if (!AutoScroll)
        {
            return result;
        }

        var needsVertical = Width.Kind == Kind.Auto &&
                            (ScrollBars & ScrollBars.Vertical) != 0 &&
                            (VerticalBarVisibility == ScrollBarVisibility.Always ||
                             (VerticalBarVisibility == ScrollBarVisibility.Auto &&
                              ContentExtent.Height > result.Height));
        var needsHorizontal = Height.Kind == Kind.Auto &&
                              (ScrollBars & ScrollBars.Horizontal) != 0 &&
                              (HorizontalBarVisibility == ScrollBarVisibility.Always ||
                               (HorizontalBarVisibility == ScrollBarVisibility.Auto &&
                                ContentExtent.Width > result.Width));

        return new Size(
            needsVertical ? LayoutMath.Add(result.Width, 1) : result.Width,
            needsHorizontal ? LayoutMath.Add(result.Height, 1) : result.Height);
    }

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

    private readonly ContainerScrollController _scroll = new();

    /// <summary>Gets the private generated scrollbar parts for specialized container layout.</summary>
    private protected ControlCollection? Bars => _scroll.Bars;

    /// <summary>Gets the committed viewport bounds for specialized container clipping.</summary>
    private protected Rect ViewportBounds => _scroll.ViewportBounds;

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
            if (!SetProperty(ref field, value, InvalidationImpact.Measure))
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
                _scroll.HorizontalOffset = 0;
                _scroll.VerticalOffset = 0;

                if (_scroll.Bars is not null)
                {
                    SetVisibility(_scroll.Horizontal!, visible: false);
                    SetVisibility(_scroll.Vertical!, visible: false);
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
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    "The scrollbar axes contain unknown flags.");
            }

            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
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
            EnumValidation.ValidateDefined(value);

            if (!SetProperty(ref field, value, InvalidationImpact.Measure))
            {
                return;
            }

            var visibility = value switch
            {
                ShowScrollBars.Never => ScrollBarVisibility.Hidden,
                ShowScrollBars.WhenNeeded => ScrollBarVisibility.Auto,
                ShowScrollBars.Always => ScrollBarVisibility.Always,
                _ => throw new UnreachableException()
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
            EnumValidation.ValidateDefined(value);
            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
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
            EnumValidation.ValidateDefined(value);
            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
        }
    } = ScrollBarVisibility.Auto;

    /// <summary>Gets or sets the complete local style shared by both generated bars.</summary>
    /// <exception cref="InvalidOperationException">The attached container is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The container is disposed.</exception>
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
            var impact = previous.Chrome != current.Chrome
                ? InvalidationImpact.Measure
                : previous == current
                    ? InvalidationImpact.None
                    : InvalidationImpact.Render;
            SynchronizeBarAppearance();
            NotifyPropertyChanged(nameof(ScrollBarStyle), impact);
            if (previous != current)
            {
                NotifyPropertyChanged(nameof(ActualScrollBarStyle), InvalidationImpact.None);
            }
        }
    }

    /// <summary>Gets the complete local or library-default generated-bar style.</summary>
    public ScrollBarStyle ActualScrollBarStyle =>
        ScrollBarStyle ?? Controls.ScrollBarStyle.Default;

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
        var previousStyle = ActualScrollBarStyle;
        var currentStyle = ActualScrollBarStyle;
        var styleImpact = previousStyle.Chrome != currentStyle.Chrome
            ? InvalidationImpact.Measure
            : previousStyle == currentStyle
                ? InvalidationImpact.None
                : InvalidationImpact.Render;

        return MaximumImpact(appearanceImpact, styleImpact);
    }

    /// <inheritdoc/>
    protected override string? GetThemeResolvedStylePropertyName(Theme? previous, Theme? current) => null;

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
            _ = SetProperty(ref field, value, InvalidationImpact.None);
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
            _ = SetProperty(ref field, value, InvalidationImpact.None);
        }
    }

    /// <summary>Gets the committed non-negative content extent.</summary>
    public Size Extent => _scroll.Extent;

    /// <summary>Gets the committed non-negative visible extent.</summary>
    public Size Viewport => _scroll.Viewport;

    /// <summary>Gets or sets the valid horizontal content offset.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside the current extent.</exception>
    /// <exception cref="InvalidOperationException">The attached container is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The container is disposed.</exception>
    public int HorizontalOffset
    {
        get => _scroll.HorizontalOffset;
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
        get => _scroll.VerticalOffset;
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
        EnumValidation.ValidateDefined(cause);
        VerifyMutable();
        return Apply(LayoutMath.Add(HorizontalOffset, x), LayoutMath.Add(VerticalOffset, y), cause);
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

        var logicalX = LayoutMath.Add(Difference(descendant.Bounds.X, _scroll.ViewportBounds.X), HorizontalOffset);
        var logicalY = LayoutMath.Add(Difference(descendant.Bounds.Y, _scroll.ViewportBounds.Y), VerticalOffset);
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

        for (var current = this;
             current is not null && (remainingX != 0 || remainingY != 0);
             current = Ancestor(current))
        {
            var previousX = current.HorizontalOffset;
            var previousY = current.VerticalOffset;
            _ = current.ScrollBy(remainingX, remainingY, Cause.Wheel);
            remainingX = Difference(remainingX, current.HorizontalOffset - previousX);
            remainingY = Difference(remainingY, current.VerticalOffset - previousY);
        }

        eventArgs.Handled = AutoScroll;
    }

    private static Container? Ancestor(Control control)
    {
        Debug.Assert(control is not null, "Scrollable ancestor lookup requires a control.");

        for (var current = control.Parent; current is not null; current = current.Parent)
        {
            if (control.ModalityOwner?.Allows(current) == false)
            {
                return null;
            }

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
            _ = SetProperty(ref _scroll.Extent, box, InvalidationImpact.None, nameof(Extent));
            _ = SetProperty(ref _scroll.Viewport, box, InvalidationImpact.None, nameof(Viewport));
            _scroll.ViewportBounds = padded;
            _scroll.HorizontalOffset = 0;
            _scroll.VerticalOffset = 0;
            return padded;
        }

        if (HorizontalBarVisibility != ScrollBarVisibility.Hidden ||
            VerticalBarVisibility != ScrollBarVisibility.Hidden)
        {
            EnsureBars();
        }

        Resolve(new Size(padded.Width, padded.Height), ContentExtent, out var horizontal, out var vertical,
            out var viewport);
        _scroll.ViewportBounds = new Rect(padded.X, padded.Y, viewport.Width, viewport.Height);
        var extentChanged = _scroll.Extent != ContentExtent;
        _ = SetProperty(ref _scroll.Extent, ContentExtent, InvalidationImpact.None, nameof(Extent));
        _ = SetProperty(ref _scroll.Viewport, viewport, InvalidationImpact.None, nameof(Viewport));
        _scroll.ReserveHorizontal = horizontal;
        _scroll.ReserveVertical = vertical;
        _ = Apply(
            Math.Min(HorizontalOffset, MaximumX()),
            Math.Min(VerticalOffset, MaximumY()),
            extentChanged ? Cause.Content : Cause.Resize);

        var scrollsHorizontally = (ScrollBars & ScrollBars.Horizontal) != 0;
        var scrollsVertically = (ScrollBars & ScrollBars.Vertical) != 0;

        return new Rect(
            Difference(padded.X, HorizontalOffset),
            Difference(padded.Y, VerticalOffset),
            scrollsHorizontally ? Math.Max(Extent.Width, viewport.Width) : viewport.Width,
            scrollsVertically ? Math.Max(Extent.Height, viewport.Height) : viewport.Height);
    }

    /// <inheritdoc/>
    internal override void ArrangeOverlays(Rect padded)
    {
        if (!AutoScroll || _scroll.Bars is null)
        {
            return;
        }

        Debug.Assert(_scroll.Horizontal is not null && _scroll.Vertical is not null,
            "Created scrollbar chrome owns both axes.");

        SynchronizeBarAppearance();
        SetVisibility(_scroll.Horizontal, _scroll.ReserveHorizontal);
        SetVisibility(_scroll.Vertical, _scroll.ReserveVertical);
        _scroll.Horizontal.Arrange(
            new Rect(padded.X, padded.Y + _scroll.ViewportBounds.Height, _scroll.ViewportBounds.Width,
                _scroll.ReserveHorizontal ? 1 : 0),
            widthResolved: true,
            heightResolved: true);
        _scroll.Vertical.Arrange(
            new Rect(padded.X + _scroll.ViewportBounds.Width, padded.Y, _scroll.ReserveVertical ? 1 : 0,
                _scroll.ViewportBounds.Height),
            widthResolved: true,
            heightResolved: true);
        Synchronize();
    }

    private void EnsureBars()
    {
        if (_scroll.Bars is not null)
        {
            return;
        }

        _scroll.Horizontal = new ScrollBar
        {
            Orientation = Orientation.Horizontal,
            Style = ScrollBarStyle,
            Focusable = false,
            TabStop = false
        };
        _scroll.Vertical = new ScrollBar
        {
            Orientation = Orientation.Vertical,
            Style = ScrollBarStyle,
            Focusable = false,
            TabStop = false
        };
        _scroll.Horizontal.ValueChanged += OnHorizontalChanged;
        _scroll.Vertical.ValueChanged += OnVerticalChanged;
        _scroll.Bars = new ControlCollection(
            this,
            capacity: 2,
            new OwnedControlOptions(
                OwnedControlRole.FrameworkPart,
                OwnedControlLayer.Normal,
                participatesInHitTesting: true,
                participatesInNavigation: false,
                partKey: "scroll-bars",
                InvalidationImpact.Measure)) { _scroll.Horizontal, _scroll.Vertical };

        Debug.Assert(_scroll.Bars.Count == 2, "Scrollbar chrome owns exactly one control per axis.");
    }

    private void SynchronizeBarAppearance()
    {
        if (_scroll.Horizontal is null || _scroll.Vertical is null)
        {
            return;
        }

        if (_scroll.Horizontal.Style != ScrollBarStyle)
        {
            _scroll.Horizontal.Style = ScrollBarStyle;
        }

        if (_scroll.Vertical.Style != ScrollBarStyle)
        {
            _scroll.Vertical.Style = ScrollBarStyle;
        }
    }

    private void Synchronize()
    {
        if (_scroll.IsSynchronizing || _scroll.Bars is null)
        {
            return;
        }

        Debug.Assert(_scroll.Horizontal is not null && _scroll.Vertical is not null,
            "Scrollbar synchronization requires both axes.");

        _scroll.IsSynchronizing = true;

        try
        {
            Configure(_scroll.Horizontal, MaximumX(), Viewport.Width, HorizontalOffset);
            Configure(_scroll.Vertical, MaximumY(), Viewport.Height, VerticalOffset);
        }
        finally
        {
            _scroll.IsSynchronizing = false;
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

        if (!_scroll.IsSynchronizing)
        {
            _ = Apply(eventArgs.Value, VerticalOffset, eventArgs.Cause);
        }
    }

    private void OnVerticalChanged(object? sender, ScrollEventArgs eventArgs)
    {
        _ = sender;

        if (!_scroll.IsSynchronizing)
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
        var changedX = SetProperty(ref _scroll.HorizontalOffset, x, InvalidationImpact.Arrange, nameof(HorizontalOffset));
        var changedY = SetProperty(ref _scroll.VerticalOffset, y, InvalidationImpact.Arrange, nameof(VerticalOffset));

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

        var end = LayoutMath.Add(start, length);
        return end > LayoutMath.Add(current, viewport) ? Math.Max(0, end - viewport) : current;
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
        Debug.Assert(available is { Width: >= 0, Height: >= 0 }, "Scrollbar resolution uses available cell extents.");
        Debug.Assert(extent is { Width: >= 0, Height: >= 0 },
            "Scrollbar resolution uses non-negative content extents.");

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

    private static int Difference(int left, int right) =>
        (int) Math.Clamp((long) left - right, int.MinValue, int.MaxValue);

    private static int MultiplyNegative(int left, int right) =>
        (int) Math.Clamp(-(long) left * right, int.MinValue, int.MaxValue);

    #endregion
}
