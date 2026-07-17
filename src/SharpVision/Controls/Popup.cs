// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using System.Runtime.ExceptionServices;

using SharpVision.Terminal.Input;

/// <summary>Displays one owned content control on an opaque, framed, anchor-relative surface.</summary>
public sealed class Popup: ContentControl
{

    #region Construction and ownership

    private IDisposable? _lightDismissRegistration;

    /// <summary>Initializes a closed popup below its eventual anchor.</summary>
    public Popup()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        Focusable = false;
    }

    /// <inheritdoc/>
    protected override void OnContentChanged(Control? previous, Control? current)
    {
        base.OnContentChanged(previous, current);

        _ = current?.Visibility = IsOpen ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Gets or sets the optional sibling anchor used to place the open surface.</summary>
    /// <exception cref="InvalidOperationException">The attached popup is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The popup is disposed.</exception>
    public Control? Anchor
    {
        get;
        set => _ = SetProperty(ref field, value, ChangeImpact.Arrange);
    }

    /// <summary>Gets or sets the preferred anchor-relative placement.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached popup is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The popup is disposed.</exception>
    public PopupPlacement Placement
    {
        get;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The popup placement is unknown.");
            }

            _ = SetProperty(ref field, value, ChangeImpact.Arrange);
        }
    } = PopupPlacement.Below;

    #endregion

    #region Surface appearance

    /// <summary>Gets or sets the terminal-safe physical glyph family used for the popup frame.</summary>
    /// <exception cref="InvalidOperationException">The attached popup is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The popup is disposed.</exception>
    public Glyphs Glyphs
    {
        get => BorderGlyphs;
        set => BorderGlyphs = value;
    }

    /// <summary>Gets the committed visible surface rectangle, or an empty rectangle while closed.</summary>
    public Rect SurfaceBounds { get; private set; }

    /// <summary>Gets or sets whether placement flips and clamps inside the owning root.</summary>
    internal bool ConstrainToRoot { get; set; } = true;

    #endregion

    #region Visibility and interaction

    private bool IsOpenTransitioning { get; set; }

    /// <summary>Raised immediately before a closing popup hides its content.</summary>
    /// <remarks>Owners use this event to restore focus while the content remains eligible.</remarks>
    public event EventHandler? Closing;

    /// <summary>Raised after a popup has hidden its content and cleared its surface.</summary>
    public event EventHandler? Closed;

    /// <summary>Gets or sets whether opening transfers focus to the first eligible descendant.</summary>
    /// <remarks>
    /// The default preserves popup behavior for dialogs and menus. Composite controls whose popup
    /// is an implementation detail set this to <see langword="false"/> and retain focus on their
    /// public owner.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The attached popup is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The popup is disposed.</exception>
    public bool FocusOnOpen
    {
        get;
        set => _ = SetProperty(ref field, value, ChangeImpact.None);
    } = true;

    /// <summary>Gets or sets whether the popup surface and content are arranged, rendered, and hit-testable.</summary>
    /// <remarks>
    /// A changed value commits and publishes first. Opening then exposes the current content and,
    /// when <see cref="FocusOnOpen"/> is enabled, requests focus. Closing therefore raises
    /// <see cref="Closing"/> after this property is false:
    /// current content retains its pre-close availability and the previous <see cref="SurfaceBounds"/>
    /// remains readable, while the surface is already ineligible for rendering and hit testing. The
    /// transition then collapses current content, clears the surface bounds, and raises
    /// <see cref="Closed"/>. Every stage completes when a callback fails, after which the earliest
    /// failure is rethrown. Reentrant open-state transitions are rejected.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// The attached popup is mutated off-dispatcher or an open-state transition is reentered.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The popup is disposed.</exception>
    public bool IsOpen
    {
        get;
        set
        {
            VerifyMutable();

            if (IsOpenTransitioning)
            {
                throw new InvalidOperationException("Popup open-state transitions cannot be reentered.");
            }

            if (field == value)
            {
                return;
            }

            IsOpenTransitioning = true;
            var failure = (ExceptionDispatchInfo?) null;

            try
            {
                field = value;
                CaptureFailure(
                    () => NotifyPropertyChanged(nameof(IsOpen), ChangeImpact.Measure),
                    ref failure);

                if (value)
                {
                    CaptureFailure(() => CloseOtherPopups(this), ref failure);
                    CaptureFailure(
                        () =>
                        {
                            if (Content is { } child)
                            {
                                child.Visibility = Visibility.Visible;
                            }
                        },
                        ref failure);
                    CaptureFailure(
                        () =>
                        {
                            if (FocusOnOpen && Content is { } focusableChild && FindFocusable(focusableChild) is { } target)
                            {
                                _ = FocusOwner?.Focus(target);
                            }
                        },
                        ref failure);
                    CaptureFailure(RegisterLightDismiss, ref failure);
                }
                else
                {
                    UnregisterLightDismiss();
                    CaptureFailure(
                        () => Closing?.Invoke(this, EventArgs.Empty),
                        ref failure);
                    CaptureFailure(
                        () =>
                        {
                            if (Content is { } child)
                            {
                                child.Visibility = Visibility.Collapsed;
                            }
                        },
                        ref failure);
                    SurfaceBounds = default;
                    CaptureFailure(
                        () => Closed?.Invoke(this, EventArgs.Empty),
                        ref failure);
                }
            }
            finally
            {
                IsOpenTransitioning = false;
            }

            failure?.Throw();
        }
    }

    /// <summary>Gets or sets whether Escape closes this open popup.</summary>
    /// <exception cref="InvalidOperationException">The attached popup is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The popup is disposed.</exception>
    public bool CloseOnEscape
    {
        get;
        set => _ = SetProperty(ref field, value, ChangeImpact.None);
    } = true;

    /// <inheritdoc/>
    protected override bool ClipsChildren => false;

    /// <inheritdoc/>
    public override Control? HitTest(Point point)
    {
        return !IsOpen || IsDisposed || !IsHitTestVisible || !EffectiveIsVisible || !EffectiveIsEnabled
            ? null
            : Content?.HitTest(point) ?? (SurfaceBounds.Contains(point) ? this : null);
    }

    /// <inheritdoc/>
    internal override Control? HitTestPopupCore(Point point) => IsOpen ? HitTest(point) : null;

    /// <inheritdoc/>
    internal override OwnedControlLayer IntrinsicLayer => OwnedControlLayer.Popup;

    #endregion

    #region Layout and rendering

    /// <inheritdoc/>
    protected override Rect VisualBounds => IsOpen ? SurfaceBounds : default;

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        if (Content is not { } child)
        {
            return default;
        }

        _ = MeasureChild(
            child,
            new Constraint(Subtract(constraint.Width, 2), Subtract(constraint.Height, 2)));
        return IsOpen
            ? SurfaceSize(child, anchorWidth: 0, constraint.Width, constraint.Height)
            : default;
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        if (Content is not { } child)
        {
            SurfaceBounds = default;
            return;
        }

        if (!IsOpen)
        {
            ArrangeChild(child, bounds, ResolvedAxes.Both);
            SurfaceBounds = default;
            return;
        }

        var anchor = Anchor?.Bounds ?? bounds;
        var desired = SurfaceSize(child, anchor.Width, bounds.Width, bounds.Height);
        var placement = ConstrainToRoot ? ResolvePlacement(bounds, anchor, desired) : Placement;
        var x = placement is PopupPlacement.Left
            ? anchor.X - desired.Width
            : placement is PopupPlacement.Right
                ? anchor.Right
                : anchor.X;
        var y = placement is PopupPlacement.Above
            ? anchor.Y - desired.Height
            : placement is PopupPlacement.Below
                ? anchor.Bottom
                : anchor.Y;
        if (ConstrainToRoot)
        {
            x = Math.Clamp(x, bounds.X, Math.Max(bounds.X, bounds.Right - desired.Width));
            y = Math.Clamp(y, bounds.Y, Math.Max(bounds.Y, bounds.Bottom - desired.Height));
        }
        SurfaceBounds = new Rect(x, y, desired.Width, desired.Height);

        // Content is constrained to the frame interior. This keeps lists and
        // scrollbars inside the popup even when an edge forces placement to flip.
        ArrangeChild(
            child,
            new Thickness(1).Deflate(SurfaceBounds),
            ResolvedAxes.Both);
    }

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        if (!IsOpen || SurfaceBounds.Width == 0 || SurfaceBounds.Height == 0)
        {
            return;
        }

        var style = GetResolvedStyle(VisualState.Normal);
        canvas.Clear(SurfaceBounds, style);
        DrawFrame(canvas, GetResolvedAppearance(VisualState.Normal).BorderStyle);
    }

    /// <inheritdoc/>
    internal override void RenderChildren(TerminalCanvas canvas)
    {
        if (IsOpen)
        {
            base.RenderChildren(canvas);
        }
    }

    /// <inheritdoc/>
    internal override void RenderPopupLayer(TerminalCanvas canvas)
    {
        if (IsOpen)
        {
            Render(canvas);
        }
    }

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);

        if (IsOpen && CloseOnEscape && eventArgs is KeyEventArgs { Stroke.Code: Code.Escape, Stroke.Action: KeyAction.Press })
        {
            IsOpen = false;
            eventArgs.Handled = true;
        }
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);

        if (reason == ReleaseReason.Disposed)
        {
            Closing = null;
            Closed = null;
        }
    }

    private static void CaptureFailure(System.Action action, ref ExceptionDispatchInfo? failure)
    {
        Debug.Assert(action is not null, "Popup transition capture requires one action.");

        try
        {
            action();
        }
        catch (Exception exception)
        {
            failure ??= ExceptionDispatchInfo.Capture(exception);
        }
    }

    #endregion

    #region Geometry

    private static Size SurfaceSize(Control child, int anchorWidth, int? availableWidth, int? availableHeight)
    {
        Debug.Assert(anchorWidth >= 0, "Anchor width is non-negative.");

        var contentWidth = Add(child.DesiredSize.Width, child.Margin.Horizontal);
        var contentHeight = Add(child.DesiredSize.Height, child.Margin.Vertical);

        // The framed surface is at least as wide as the anchor and always
        // includes one border cell on every side.
        var width = Math.Max(anchorWidth, Add(contentWidth, 2));
        var height = Add(contentHeight, 2);

        return new Size(
            availableWidth.HasValue ? Math.Min(width, availableWidth.Value) : width,
            availableHeight.HasValue ? Math.Min(height, availableHeight.Value) : height);
    }

    private PopupPlacement ResolvePlacement(Rect bounds, Rect anchor, Size desired)
    {
        // Keep the preferred direction when it fits. A terminal-edge popup
        // flips before clamping so its framed surface remains legible.
        return Placement switch
        {
            PopupPlacement.Below when anchor.Bottom + desired.Height > bounds.Bottom &&
                anchor.Y - desired.Height >= bounds.Y => PopupPlacement.Above,
            PopupPlacement.Above when anchor.Y - desired.Height < bounds.Y &&
                anchor.Bottom + desired.Height <= bounds.Bottom => PopupPlacement.Below,
            PopupPlacement.Right when anchor.Right + desired.Width > bounds.Right &&
                anchor.X - desired.Width >= bounds.X => PopupPlacement.Left,
            PopupPlacement.Left when anchor.X - desired.Width < bounds.X &&
                anchor.Right + desired.Width <= bounds.Right => PopupPlacement.Right,
            _ => Placement,
        };
    }

    private void DrawFrame(TerminalCanvas canvas, TerminalStyle style)
    {
        var glyphs = ResolveBorderGlyphs(Glyphs);

        for (var x = SurfaceBounds.X; x < SurfaceBounds.Right; x++)
        {
            var top = x == SurfaceBounds.X ? glyphs.TopLeft : x == SurfaceBounds.Right - 1 ? glyphs.TopRight : glyphs.Top;
            var bottom = x == SurfaceBounds.X ? glyphs.BottomLeft : x == SurfaceBounds.Right - 1 ? glyphs.BottomRight : glyphs.Bottom;
            canvas.DrawRune(top, new Point(x, SurfaceBounds.Y), style, BackgroundMode.Opaque);

            if (SurfaceBounds.Height > 1)
            {
                canvas.DrawRune(bottom, new Point(x, SurfaceBounds.Bottom - 1), style, BackgroundMode.Opaque);
            }
        }

        for (var y = SurfaceBounds.Y + 1; y < SurfaceBounds.Bottom - 1; y++)
        {
            canvas.DrawRune(glyphs.Left, new Point(SurfaceBounds.X, y), style, BackgroundMode.Opaque);

            if (SurfaceBounds.Width > 1)
            {
                canvas.DrawRune(glyphs.Right, new Point(SurfaceBounds.Right - 1, y), style, BackgroundMode.Opaque);
            }
        }
    }

    private static int Add(int left, int right)
    {
        Debug.Assert(left >= 0, "Popup accumulation uses non-negative extents.");
        Debug.Assert(right >= 0, "Popup accumulation uses non-negative extents.");

        return (int) Math.Min(int.MaxValue, (long) left + right);
    }

    private static int? Subtract(int? value, int extent)
    {
        Debug.Assert(extent >= 0, "Subtracted popup frame extent cannot be negative.");

        return value.HasValue
            ? Math.Max(0, value.Value - extent)
            : null;
    }

    private static Control? FindFocusable(Control control)
    {
        if (control.CanFocus && control.EffectiveIsEnabled && control.EffectiveIsVisible)
        {
            return control;
        }

        var count = control.OwnedControlCount;

        for (var index = 0; index < count; index++)
        {
            if (FindFocusable(control.OwnedControlAt(index)) is { } result)
            {
                return result;
            }
        }

        return null;
    }

    private void RegisterLightDismiss()
    {
        UnregisterLightDismiss();
        Control root = this;

        while (root.Parent is { } parent)
        {
            root = parent;
        }

        _lightDismissRegistration = root.AddHandler(Events.Pointer, OnLightDismissPointer);
    }

    private void UnregisterLightDismiss()
    {
        _lightDismissRegistration?.Dispose();
        _lightDismissRegistration = null;
    }

    private void OnLightDismissPointer(object? sender, PointerEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.Phase != Phase.Preview || eventArgs.Pointer.Action != PointerAction.Press || !IsOpen)
        {
            return;
        }

        if (eventArgs.Pointer.Cells is not { } cells)
        {
            return;
        }

        if (SurfaceBounds.Contains(cells))
        {
            return;
        }

        // A nested popup is outside this surface by design but remains inside
        // the same logical open chain. Let its press route complete before a
        // menu or other owner decides whether invocation closes the chain.
        if (ContainsOpenDescendantSurface(this, cells))
        {
            return;
        }

        if (Anchor is not null && Anchor.Bounds.Contains(cells))
        {
            return;
        }

        IsOpen = false;
    }

    private static bool ContainsOpenDescendantSurface(Control owner, Point point)
    {
        var count = owner.OwnedControlCount;

        for (var index = 0; index < count; index++)
        {
            var child = owner.OwnedControlAt(index);

            if (child is Popup { IsOpen: true } popup && popup.SurfaceBounds.Contains(point))
            {
                return true;
            }

            if (ContainsOpenDescendantSurface(child, point))
            {
                return true;
            }
        }

        return false;
    }

    private static void CloseOtherPopups(Popup opening)
    {
        Control root = opening;

        while (root.Parent is { } parent)
        {
            root = parent;
        }

        CloseDescendantPopups(root, opening);
    }

    private static void CloseDescendantPopups(Control control, Popup except)
    {
        var count = control.OwnedControlCount;

        for (var index = 0; index < count; index++)
        {
            var child = control.OwnedControlAt(index);

            if (child is Popup { IsOpen: true } popup && !ReferenceEquals(popup, except) && !IsAncestorOf(popup, except))
            {
                popup.IsOpen = false;
            }

            CloseDescendantPopups(child, except);
        }
    }

    private static bool IsAncestorOf(Control candidate, Control descendant)
    {
        for (var current = descendant.Parent; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, candidate))
            {
                return true;
            }
        }

        return false;
    }

    #endregion
}
