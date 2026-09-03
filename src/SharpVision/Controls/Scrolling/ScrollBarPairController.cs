// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Scrolling;

/// <summary>Owns one retained horizontal and vertical scrollbar pair, its two-axis reservation
/// fixed point, physical rail layout, and guarded range synchronization.</summary>
/// <remarks>Hosts retain content-specific extent, offset, and input policy. This controller keeps
/// generated rail ownership and mechanics identical across container and editor viewports.</remarks>
internal sealed class ScrollBarPairController
{
    private readonly ControlBase _owner;
    private readonly Action<ScrollBar> _bindStyle;
    private readonly bool _isFocusable;
    private readonly bool _horizontalIncludesCorner;
    private readonly string _partKey;
    private readonly InvalidationImpact _ownershipImpact;
    private bool _isSynchronizing;
    private ScrollBarPairConfiguration? _pendingConfiguration;

    /// <summary>Initializes one lazily-created owned scrollbar pair.</summary>
    /// <param name="owner">The non-null control that owns the generated rails.</param>
    /// <param name="partKey">The non-empty stable owned-part key.</param>
    /// <param name="ownershipImpact">The phase invalidated when rail ownership changes.</param>
    /// <param name="isFocusable">Whether each generated scrollbar may receive focus.</param>
    /// <param name="horizontalIncludesCorner">Whether the horizontal rail spans the vertical
    /// rail's corner cell when both are visible.</param>
    /// <param name="bindStyle">Binds the host's registered part style to one generated rail.</param>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> or
    /// <paramref name="bindStyle"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="partKey"/> is empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="ownershipImpact"/> is unknown.</exception>
    public ScrollBarPairController(
        ControlBase owner,
        string partKey,
        InvalidationImpact ownershipImpact,
        bool isFocusable,
        bool horizontalIncludesCorner,
        Action<ScrollBar> bindStyle)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(partKey);
        ArgumentOutOfRangeException.ThrowIfNotDefined(
            ownershipImpact,
            nameof(ownershipImpact),
            "The scrollbar ownership invalidation impact is unknown.");
        ArgumentNullException.ThrowIfNull(bindStyle);

        _owner = owner;
        _partKey = partKey;
        _ownershipImpact = ownershipImpact;
        _isFocusable = isFocusable;
        _horizontalIncludesCorner = horizontalIncludesCorner;
        _bindStyle = bindStyle;
    }

    /// <summary>Raised when the horizontal rail commits a user- or programmatic value change.</summary>
    public event EventHandler<ScrollEventArgs>? HorizontalValueChanged;

    /// <summary>Raised when the vertical rail commits a user- or programmatic value change.</summary>
    public event EventHandler<ScrollEventArgs>? VerticalValueChanged;

    /// <summary>Gets the retained pair collection after creation, or null beforehand.</summary>
    public ControlCollection? Bars { get; private set; }

    /// <summary>Gets the horizontal generated rail after creation, or null beforehand.</summary>
    public ScrollBar? Horizontal { get; private set; }

    /// <summary>Gets the vertical generated rail after creation, or null beforehand.</summary>
    public ScrollBar? Vertical { get; private set; }

    /// <summary>Gets or sets the committed non-negative content extent for a host that stores its
    /// scroll geometry in this controller.</summary>
    public Size Extent;

    /// <summary>Gets or sets the committed non-negative viewport extent for a host that stores its
    /// scroll geometry in this controller.</summary>
    public Size Viewport;

    /// <summary>Gets or sets the committed horizontal content offset.</summary>
    public int HorizontalOffset;

    /// <summary>Gets or sets the committed vertical content offset.</summary>
    public int VerticalOffset;

    /// <summary>Gets or sets the committed physical viewport rectangle.</summary>
    public Rect ViewportBounds;

    /// <summary>Gets or sets whether the horizontal scrollbar consumes one cell.</summary>
    public bool ReserveHorizontal;

    /// <summary>Gets or sets whether the vertical scrollbar consumes one cell.</summary>
    public bool ReserveVertical;

    /// <summary>Creates, retains, subscribes, and style-binds both rails exactly once.</summary>
    public void EnsureBars()
    {
        if (Bars is not null)
        {
            return;
        }

        Horizontal = CreateBar(Orientation.Horizontal);
        Vertical = CreateBar(Orientation.Vertical);
        Horizontal.ValueChanged += OnHorizontalValueChanged;
        Vertical.ValueChanged += OnVerticalValueChanged;
        Bars = new ControlCollection(
            _owner,
            capacity: 2,
            new OwnedControlOptions(
                OwnedControlRole.FrameworkPart,
                OwnedControlLayer.Normal,
                participatesInHitTesting: true,
                participatesInNavigation: false,
                _partKey,
                _ownershipImpact)) { Horizontal, Vertical };
        _bindStyle(Horizontal);
        _bindStyle(Vertical);
    }

    /// <summary>Resolves the stable two-axis scrollbar reservation and final viewport.</summary>
    /// <param name="available">The non-negative physical host extent.</param>
    /// <param name="extent">The initial non-negative content extent.</param>
    /// <param name="axes">The enabled scroll axes.</param>
    /// <param name="horizontalVisibility">The horizontal reservation policy.</param>
    /// <param name="verticalVisibility">The vertical reservation policy.</param>
    /// <param name="remeasure">Optionally recomputes content after either reservation changes.</param>
    /// <param name="remeasureInitial">Whether to remeasure once against the initial candidate viewport before visibility probes, settling AutoSize content and initially reserved rails.</param>
    /// <returns>The final content extent, viewport, and two reservation flags.</returns>
    public (Size Extent, Size Viewport, bool Horizontal, bool Vertical) Resolve(
        Size available,
        Size extent,
        ScrollBars axes,
        ScrollBarVisibility horizontalVisibility,
        ScrollBarVisibility verticalVisibility,
        Func<bool, bool, Size>? remeasure = null,
        bool remeasureInitial = false)
    {
        Debug.Assert(available is { Width: >= 0, Height: >= 0 }, "Scrollbar resolution requires non-negative bounds.");
        Debug.Assert(extent is { Width: >= 0, Height: >= 0 }, "Scrollbar resolution requires non-negative content.");

        var horizontal = (axes & ScrollBars.Horizontal) != 0 &&
                         horizontalVisibility == ScrollBarVisibility.Always;
        var vertical = (axes & ScrollBars.Vertical) != 0 &&
                       verticalVisibility == ScrollBarVisibility.Always;

        if (remeasureInitial && remeasure is not null)
        {
            extent = remeasure(horizontal, vertical);
        }

        for (var probe = 0; probe < 2; probe++)
        {
            var viewport = ViewportFor(available, horizontal, vertical);
            var nextHorizontal = horizontal ||
                                 ((axes & ScrollBars.Horizontal) != 0 &&
                                  horizontalVisibility == ScrollBarVisibility.Auto &&
                                  extent.Width > viewport.Width);
            var nextVertical = vertical ||
                               ((axes & ScrollBars.Vertical) != 0 &&
                                verticalVisibility == ScrollBarVisibility.Auto &&
                                extent.Height > viewport.Height);

            if (nextHorizontal == horizontal && nextVertical == vertical)
            {
                ReserveHorizontal = horizontal;
                ReserveVertical = vertical;
                return (extent, viewport, horizontal, vertical);
            }

            horizontal = nextHorizontal;
            vertical = nextVertical;

            if (remeasure is not null)
            {
                extent = remeasure(horizontal, vertical);
            }
        }

        ReserveHorizontal = horizontal;
        ReserveVertical = vertical;
        return (extent, ViewportFor(available, horizontal, vertical), horizontal, vertical);
    }

    /// <summary>Applies visibility and final physical rectangles to both generated rails.</summary>
    /// <param name="bounds">The non-negative physical host rectangle.</param>
    /// <param name="viewport">The non-negative reserved viewport rectangle.</param>
    /// <param name="horizontal">Whether the horizontal rail consumes one row.</param>
    /// <param name="vertical">Whether the vertical rail consumes one column.</param>
    public void Arrange(Rect bounds, Rect viewport, bool horizontal, bool vertical)
    {
        EnsureBars();
        Debug.Assert(Horizontal is not null && Vertical is not null, "Created scrollbar chrome owns both axes.");

        Horizontal.Visibility = horizontal ? Visibility.Visible : Visibility.Collapsed;
        Vertical.Visibility = vertical ? Visibility.Visible : Visibility.Collapsed;
        Horizontal.Arrange(
            new Rect(
                bounds.X,
                bounds.Y.Add(viewport.Height),
                vertical && _horizontalIncludesCorner ? bounds.Width : viewport.Width,
                horizontal && bounds.Height > 0 ? 1 : 0),
            widthResolved: true,
            heightResolved: true);
        Vertical.Arrange(
            new Rect(
                bounds.X.Add(viewport.Width),
                bounds.Y,
                vertical && bounds.Width > 0 ? 1 : 0,
                viewport.Height),
            widthResolved: true,
            heightResolved: true);
    }

    /// <summary>Synchronizes both rails from already-clamped host ranges while suppressing
    /// callbacks caused by that synchronization.</summary>
    /// <remarks>A reentrant request replaces any older pending request and is applied after the
    /// current property publication completes, so the newest complete configuration wins.</remarks>
    /// <param name="horizontalMaximum">The non-negative horizontal maximum.</param>
    /// <param name="verticalMaximum">The non-negative vertical maximum.</param>
    /// <param name="horizontalViewport">The non-negative horizontal viewport extent.</param>
    /// <param name="verticalViewport">The non-negative vertical viewport extent.</param>
    /// <param name="horizontalValue">The valid horizontal value.</param>
    /// <param name="verticalValue">The valid vertical value.</param>
    /// <param name="horizontalSmallChange">The non-negative horizontal small increment.</param>
    /// <param name="verticalSmallChange">The non-negative vertical small increment.</param>
    /// <param name="horizontalLargeChange">The non-negative horizontal large increment.</param>
    /// <param name="verticalLargeChange">The non-negative vertical large increment.</param>
    public void Synchronize(
        int horizontalMaximum,
        int verticalMaximum,
        int horizontalViewport,
        int verticalViewport,
        int horizontalValue,
        int verticalValue,
        int horizontalSmallChange,
        int verticalSmallChange,
        int horizontalLargeChange,
        int verticalLargeChange)
    {
        if (Bars is null)
        {
            return;
        }

        var configuration = new ScrollBarPairConfiguration(
            horizontalMaximum,
            verticalMaximum,
            horizontalViewport,
            verticalViewport,
            horizontalValue,
            verticalValue,
            horizontalSmallChange,
            verticalSmallChange,
            horizontalLargeChange,
            verticalLargeChange);

        if (_isSynchronizing)
        {
            _pendingConfiguration = configuration;
            return;
        }

        Debug.Assert(Horizontal is not null && Vertical is not null, "Created scrollbar chrome owns both axes.");
        _isSynchronizing = true;

        try
        {
            do
            {
                _pendingConfiguration = null;
                ApplyConfiguration(configuration);

                if (_pendingConfiguration is { } pending)
                {
                    configuration = pending;
                }
            }
            while (_pendingConfiguration.HasValue);
        }
        finally
        {
            _pendingConfiguration = null;
            _isSynchronizing = false;
        }
    }

    /// <summary>Hides both rails without releasing their retained ownership.</summary>
    public void Hide()
    {
        if (Bars is null)
        {
            return;
        }

        Debug.Assert(Horizontal is not null && Vertical is not null, "Created scrollbar chrome owns both axes.");
        Horizontal.Visibility = Visibility.Collapsed;
        Vertical.Visibility = Visibility.Collapsed;
    }

    /// <summary>Returns the topmost generated rail at one point, or null.</summary>
    /// <param name="point">The absolute terminal-cell point.</param>
    /// <returns>The hit rail, or null.</returns>
    public ControlBase? HitTest(Point point) => Bars is null
        ? null
        : Vertical!.HitTest(point) ?? Horizontal!.HitTest(point);

    /// <summary>Renders both generated rails into the host's normal child pass.</summary>
    /// <param name="canvas">The frame canvas.</param>
    /// <param name="contentClip">The inherited content clip.</param>
    public void Render(TerminalCanvas canvas, Rect contentClip)
    {
        Horizontal?.Render(canvas, contentClip);
        Vertical?.Render(canvas, contentClip);
    }

    private ScrollBar CreateBar(Orientation orientation) => new()
    {
        Orientation = orientation,
        IsFocusable = _isFocusable,
        IsTabStop = _isFocusable,
    };

    private void OnHorizontalValueChanged(object? sender, ScrollEventArgs eventArgs)
    {
        if (!_isSynchronizing)
        {
            HorizontalValueChanged?.Invoke(sender, eventArgs);
        }
    }

    private void OnVerticalValueChanged(object? sender, ScrollEventArgs eventArgs)
    {
        if (!_isSynchronizing)
        {
            VerticalValueChanged?.Invoke(sender, eventArgs);
        }
    }

    private static Size ViewportFor(Size available, bool horizontal, bool vertical) => new(
        Math.Max(0, available.Width - (vertical ? 1 : 0)),
        Math.Max(0, available.Height - (horizontal ? 1 : 0)));

    private void ApplyConfiguration(ScrollBarPairConfiguration configuration)
    {
        Debug.Assert(Horizontal is not null && Vertical is not null, "Created scrollbar chrome owns both axes.");
        Configure(
            Horizontal,
            configuration.HorizontalMaximum,
            configuration.HorizontalViewport,
            configuration.HorizontalValue,
            configuration.HorizontalSmallChange,
            configuration.HorizontalLargeChange);
        Configure(
            Vertical,
            configuration.VerticalMaximum,
            configuration.VerticalViewport,
            configuration.VerticalValue,
            configuration.VerticalSmallChange,
            configuration.VerticalLargeChange);
    }

    private static void Configure(
        ScrollBar bar,
        int maximum,
        int viewport,
        int value,
        int smallChange,
        int largeChange)
    {
        Debug.Assert(maximum >= 0 && viewport >= 0, "Scrollbar geometry is non-negative.");
        Debug.Assert(value >= 0 && value <= maximum, "Scrollbar value is clamped before synchronization.");

        if (bar.Value > maximum)
        {
            bar.Value = maximum;
        }

        bar.Maximum = maximum;
        bar.ViewportSize = viewport;
        bar.SmallChange = smallChange;
        bar.LargeChange = largeChange;
        bar.Value = Math.Clamp(value, 0, maximum);
    }
}
