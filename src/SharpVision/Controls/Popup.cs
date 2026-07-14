// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using SharpVision.Input;
using SharpVision.Terminal.Input;

/// <summary>Displays one owned child on an opaque, framed, anchor-relative surface.</summary>
public sealed class Popup: Container
{

    #region Construction and ownership

    /// <summary>Initializes a closed capacity-one popup below its eventual anchor.</summary>
    public Popup() : base(capacity: 1) => HorizontalAlignment = HorizontalAlignment.Stretch;

    /// <summary>Gets or atomically sets the content displayed within the popup frame.</summary>
    /// <remarks>The popup owns child visibility while closed so that closed content cannot receive focus, render, or hit testing.</remarks>
    /// <exception cref="ArgumentException">The child cannot be owned by this popup.</exception>
    /// <exception cref="InvalidOperationException">The attached popup is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The popup or child is disposed.</exception>
    public Control? Child
    {
        get => Children.Count == 0 ? null : Children[0];
        set
        {
            VerifyMutable();

            if (ReferenceEquals(Child, value))
            {
                return;
            }

            Children.SetOnly(value);

            _ = value?.Visibility = IsOpen ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    /// <summary>Gets or sets the optional sibling anchor used to place the open surface.</summary>
    /// <exception cref="InvalidOperationException">The attached popup is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The popup is disposed.</exception>
    public Control? Anchor
    {
        get;
        set => _ = Set(ref field, value, Invalidation.Arrange);
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

            _ = Set(ref field, value, Invalidation.Arrange);
        }
    } = PopupPlacement.Below;

    #endregion

    #region Surface appearance

    /// <summary>Gets or sets the terminal-safe physical glyph family used for the popup frame.</summary>
    /// <exception cref="InvalidOperationException">The attached popup is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The popup is disposed.</exception>
    public Glyphs Glyphs
    {
        get;
        set => _ = Set(ref field, value, Invalidation.Render);
    } = Glyphs.Rounded;

    /// <summary>Gets the committed visible surface rectangle, or an empty rectangle while closed.</summary>
    public Rect SurfaceBounds { get; private set; }

    #endregion

    #region Visibility and interaction

    /// <summary>Raised immediately before a closing popup hides its child.</summary>
    /// <remarks>Owners use this event to restore focus while the child remains eligible.</remarks>
    public event EventHandler? Closing;

    /// <summary>Raised after a popup has hidden its child.</summary>
    public event EventHandler? Closed;

    /// <summary>Gets or sets whether the popup surface and child are arranged, rendered, and hit-testable.</summary>
    /// <exception cref="InvalidOperationException">The attached popup is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The popup is disposed.</exception>
    public bool IsOpen
    {
        get;
        set
        {
            if (!Set(ref field, value, Invalidation.Measure))
            {
                return;
            }

            if (!value)
            {
                Closing?.Invoke(this, EventArgs.Empty);
            }

            if (Child is { } child)
            {
                child.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            }

            if (value && Child is { } focusableChild && FindFocusable(focusableChild) is { } target)
            {
                _ = FocusOwner?.Focus(target);
            }
            else if (!value)
            {
                SurfaceBounds = default;
                Closed?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>Gets or sets whether Escape closes this open popup.</summary>
    /// <exception cref="InvalidOperationException">The attached popup is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The popup is disposed.</exception>
    public bool CloseOnEscape
    {
        get;
        set => _ = Set(ref field, value, Invalidation.None);
    } = true;

    /// <inheritdoc/>
    internal override bool ClipsChildren => false;

    /// <inheritdoc/>
    public override Control? HitTest(Point point)
    {
        return !IsOpen || IsDisposed || !IsHitTestVisible || !EffectiveIsVisible || !EffectiveIsEnabled
            ? null
            : Child?.HitTest(point) ?? (SurfaceBounds.Contains(point) ? this : null);
    }

    /// <inheritdoc/>
    internal override Control? HitTestPopup(Point point) => IsOpen ? HitTest(point) : null;

    #endregion

    #region Layout and rendering

    /// <inheritdoc/>
    protected override Rect VisualBounds => IsOpen ? SurfaceBounds : default;

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        if (!IsOpen || Child is not { } child)
        {
            return default;
        }

        child.Measure(new Constraint(Subtract(constraint.Width, 2), Subtract(constraint.Height, 2)));
        return SurfaceSize(child, anchorWidth: 0, constraint.Width, constraint.Height);
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        if (!IsOpen || Child is not { } child)
        {
            SurfaceBounds = default;
            return;
        }

        Rect anchor = Anchor?.Bounds ?? bounds;
        Size desired = SurfaceSize(child, anchor.Width, bounds.Width, bounds.Height);
        PopupPlacement placement = ResolvePlacement(bounds, anchor, desired);
        int x = placement is PopupPlacement.Left
            ? anchor.X - desired.Width
            : placement is PopupPlacement.Right
                ? anchor.Right
                : anchor.X;
        int y = placement is PopupPlacement.Above
            ? anchor.Y - desired.Height
            : placement is PopupPlacement.Below
                ? anchor.Bottom
                : anchor.Y;
        x = Math.Clamp(x, bounds.X, Math.Max(bounds.X, bounds.Right - desired.Width));
        y = Math.Clamp(y, bounds.Y, Math.Max(bounds.Y, bounds.Bottom - desired.Height));
        SurfaceBounds = new Rect(x, y, desired.Width, desired.Height);

        // The child is constrained to the frame interior. This keeps lists and
        // scrollbars inside the popup even when an edge forces placement to flip.
        child.Arrange(
            new Thickness(1).Deflate(SurfaceBounds),
            widthResolved: true,
            heightResolved: true);
    }

    /// <inheritdoc/>
    protected override void OnRender(TerminalCanvas canvas)
    {
        if (!IsOpen || SurfaceBounds.Width == 0 || SurfaceBounds.Height == 0)
        {
            return;
        }

        TerminalStyle inherited = ResolvedStyle;
        canvas.Clear(SurfaceBounds, inherited);
        DrawFrame(canvas, ControlAppearance.ResolveBorderStyle(this, GetVisualState()));
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

    #endregion

    #region Geometry

    private static Size SurfaceSize(Control child, int anchorWidth, int? availableWidth, int? availableHeight)
    {
        Debug.Assert(anchorWidth >= 0, "Anchor width is non-negative.");

        int contentWidth = Add(child.DesiredSize.Width, child.Margin.Horizontal);
        int contentHeight = Add(child.DesiredSize.Height, child.Margin.Vertical);

        // The framed surface is at least as wide as the anchor and always
        // includes one border cell on every side.
        int width = Math.Max(anchorWidth, Add(contentWidth, 2));
        int height = Add(contentHeight, 2);

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
        for (int x = SurfaceBounds.X; x < SurfaceBounds.Right; x++)
        {
            Rune top = x == SurfaceBounds.X ? Glyphs.TopLeft : x == SurfaceBounds.Right - 1 ? Glyphs.TopRight : Glyphs.Top;
            Rune bottom = x == SurfaceBounds.X ? Glyphs.BottomLeft : x == SurfaceBounds.Right - 1 ? Glyphs.BottomRight : Glyphs.Bottom;
            canvas.DrawRune(top, new Point(x, SurfaceBounds.Y), style, BackgroundMode.Opaque);

            if (SurfaceBounds.Height > 1)
            {
                canvas.DrawRune(bottom, new Point(x, SurfaceBounds.Bottom - 1), style, BackgroundMode.Opaque);
            }
        }

        for (int y = SurfaceBounds.Y + 1; y < SurfaceBounds.Bottom - 1; y++)
        {
            canvas.DrawRune(Glyphs.Left, new Point(SurfaceBounds.X, y), style, BackgroundMode.Opaque);

            if (SurfaceBounds.Width > 1)
            {
                canvas.DrawRune(Glyphs.Right, new Point(SurfaceBounds.Right - 1, y), style, BackgroundMode.Opaque);
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

        Control? result = null;
        control.VisitChildren(child => result ??= FindFocusable(child));
        return result;
    }

    #endregion
}
