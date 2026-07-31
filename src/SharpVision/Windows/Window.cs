// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Windows;

using System.Runtime.ExceptionServices;

using SharpVision.Controls.Input;
using SharpVision.Controls.Layout;
using SharpVision.Surfaces;
using SharpVision.Terminal.Input;
using SharpVision.Text;

using Terminal.Rendering;

/// <summary>Frames one owned content control as a titled terminal window with optional Turbo Vision-style shadowing.</summary>
[PublicAPI]
public partial class Window: FloatingSurface, IOverlayPositionConstraint
{
    /// <summary>Gets or sets the complete locally authored border.</summary>
    public new Border Border { get => base.Border; set => base.Border = value; }

    /// <summary>Returns border ownership to the active Theme.</summary>
    public new void ResetBorder() => base.ResetBorder();

    /// <summary>Gets or sets the complete locally authored shadow.</summary>
    public new Shadow Shadow { get => base.Shadow; set => base.Shadow = value; }

    /// <summary>Returns shadow ownership to the active Theme.</summary>
    public new void ResetShadow() => base.ResetShadow();

    private const int _closeChromeWidth = 7;
    private const int _closeTargetWidth = 3;
    private const int _minimumCloseWidth = 4;

    private readonly PressBehavior _closeInteraction;
    private bool _dragging;
    private Point _dragPointerOrigin;
    private Point _dragWindowOrigin;
    private bool _resizing;
    private Point _resizePointerOrigin;
    private Size _resizeWindowOrigin;
    private Point _resizeWindowPosition;
    private bool _closePointerOver;
    private bool _closePressed;
    private Rune? _closeGlyph;
    private bool _isRequestingClose;
    private bool _isShowingModal;

    #region Construction and properties

    /// <inheritdoc/>
    protected override ThemeRole ThemeRole => ThemeRole.Window;

    /// <summary>Initializes an opaque movable Window with a paired-line border and composite shadow.</summary>
    public Window()
    {
        _closeInteraction = new PressBehavior(
            ResolveCloseTargetBounds,
            () => CanClose && EffectiveIsEnabled && EffectiveIsVisible && ResolveCloseTargetBounds().Width > 0,
            static () => false,
            RequestFocus,
            CapturePointer,
            () => HasPointerCapture,
            ReleasePointerCapture,
            SetClosePressed,
            _ => RequestClose());
        PropertyChanged += OnWindowPropertyChanged;
    }

    /// <summary>Raised after this Window becomes visible.</summary>
    public event EventHandler? Shown;

    /// <summary>Gets whether this Window is the active Window of its owning Application.</summary>
    /// <remarks>Activation is application-owned and does not imply keyboard focus or z-order promotion.</remarks>
    public bool IsActive { get; private set; }

    /// <summary>Commits application-owned activation and invalidates the active appearance.</summary>
    /// <param name="value">Whether this Window is active.</param>
    internal void SetActive(bool value)
    {
        VerifyMutable();

        if (IsActive == value)
        {
            return;
        }

        IsActive = value;
        InvalidateVisualState();
    }


    /// <summary>Makes this Window visible and enters one application-owned modal presentation rooted at it.</summary>
    /// <param name="outsideInteraction">
    /// The outside-input policy. The default consumes outside input without requesting closure.
    /// </param>
    /// <param name="initialFocus">
    /// An optional eligible focus target owned by this Window; null selects the first eligible descendant.
    /// </param>
    /// <returns>The disposable scope representing this presentation's modal lifetime.</returns>
    /// <remarks>
    /// One Window may own only one live modal presentation. The returned scope owns modality, not
    /// visual lifetime: disposing it externally leaves <see cref="Visibility"/> unchanged and returns
    /// the Window to modeless interaction. Changing visibility away from <see cref="Visibility.Visible"/>
    /// ordinarily ends a live presentation before the visibility notification. A dismiss request raises
    /// <see cref="FloatingSurface.Closing"/>; the Window remains visible and modal unless its owner changes visibility or
    /// disposes the returned scope. This call suppresses only the legacy visibility autofocus transaction,
    /// allowing modal entry to snapshot background focus and select <paramref name="initialFocus"/> once.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="outsideInteraction"/> is undefined.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// This Window is not a valid available application modal root, or <paramref name="initialFocus"/>
    /// is unavailable, ineligible, foreign, or outside this Window.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The Window is detached, is mutated off-dispatcher, is reentering modal presentation, or already
    /// owns a live modal presentation.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The Window, modality manager, or supplied focus target is disposed.
    /// </exception>
    /// <exception cref="Exception">
    /// A visibility, pointer-cleanup, focus, scope, or user callback fails. Modal entry and visibility
    /// are rolled back before the earliest initiating failure is rethrown.
    /// </exception>
    public ModalScope ShowModal(
        OutsideInteraction outsideInteraction = OutsideInteraction.Ignore,
        Control? initialFocus = null)
    {
        VerifyMutable();

        if (_isShowingModal)
        {
            throw new InvalidOperationException("Window modal presentations cannot be reentered.");
        }

        _isShowingModal = true;

        try
        {
            if (!Enum.IsDefined(outsideInteraction))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(outsideInteraction),
                    outsideInteraction,
                    "The outside-interaction policy is unknown.");
            }

            if (HasActiveSurfaceModal)
            {
                throw new InvalidOperationException("The Window already has an active modal presentation.");
            }

            _ = ModalityOwner ?? throw new InvalidOperationException(
                "A modal Window must belong to an attached application tree.");
            return ShowModalCore(outsideInteraction, initialFocus, Visibility);
        }
        finally
        {
            _isShowingModal = false;
        }
    }

    /// <summary>Gets or sets whether the window can be dragged by its title bar.</summary>
    /// <remarks>Dragging keeps the window's border box inside its parent's committed content area.</remarks>
    /// <exception cref="InvalidOperationException">The attached window is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The window is disposed.</exception>
    public bool CanMove
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.None);
    } = true;

    /// <summary>Gets or sets whether the window can be resized by dragging its bottom-right corner.</summary>
    /// <remarks>
    /// Resizing keeps the window's top-left corner fixed and adjusts <see cref="Control.Width"/> and
    /// <see cref="Control.Height"/> within <see cref="Control.MinWidth"/>/<see cref="Control.MaxWidth"/>,
    /// <see cref="Control.MinHeight"/>/<see cref="Control.MaxHeight"/>, and the parent's committed
    /// content area. Off by default.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The attached window is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The window is disposed.</exception>
    public bool CanResize
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.None);
    }

    /// <summary>Gets or sets whether the window renders a framed close affordance in the title edge.</summary>
    /// <exception cref="InvalidOperationException">The attached window is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The window is disposed.</exception>
    public bool CanClose
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.Measure);
    }

    /// <summary>Gets or sets whether Escape requests closure when no cancel button handles it.</summary>
    /// <remarks>This independent policy keeps keyboard dismissal explicit instead of inferring a Window role.</remarks>
    /// <exception cref="InvalidOperationException">The attached window is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The window is disposed.</exception>
    public bool CloseOnEscape
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.None);
    }

    /// <summary>Gets or sets the title-bar edge that hosts the close control.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached window is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The window is disposed.</exception>
    public WindowClosePlacement ClosePlacement
    {
        get;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The close placement is unknown.");
            }

            _ = SetProperty(ref field, value, InvalidationImpact.Render);
        }
    } = WindowClosePlacement.Left;

    /// <summary>Gets or sets the non-null header written into the top edge.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="InvalidOperationException">The attached window is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The window is disposed.</exception>
    public string Header
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
        }
    } = string.Empty;

    /// <summary>Gets or sets the left, centered, or right header placement inside the top frame edge.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached window is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The window is disposed.</exception>
    public WindowTitlePlacement HeaderPlacement
    {
        get;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The title placement is unknown.");
            }

            _ = SetProperty(ref field, value, InvalidationImpact.Render);
        }
    } = WindowTitlePlacement.Left;

    /// <inheritdoc/>
    protected override string? AccessKeyText => null;

    /// <summary>Gets or sets the local close-button glyph.</summary>
    public Rune CloseGlyph
    {
        get => _closeGlyph ?? ControlGlyphs.Chrome.WindowClose.Value;
        set
        {
            _ = new ControlGlyph(value, value);
            VerifyMutable();
            if (_closeGlyph == value)
            {
                return;
            }

            _closeGlyph = value;
            NotifyPropertyChanged(nameof(CloseGlyph), InvalidationImpact.Render);
        }
    }

    /// <summary>Clears the local close-button glyph to the code-owned default.</summary>
    /// <exception cref="InvalidOperationException">The attached window is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The window is disposed.</exception>
    public void ResetCloseGlyph()
    {
        VerifyMutable();

        if (_closeGlyph.HasValue)
        {
            _closeGlyph = null;
            NotifyPropertyChanged(nameof(CloseGlyph), InvalidationImpact.Render);
        }
    }

    #endregion

    #region Layout and rendering

    /// <inheritdoc/>
    /// <inheritdoc/>
    protected override Rect VisualBounds
    {
        get
        {
            var shadow = ActualShadow;
            return ControlChrome.ExpandVisualBounds(Bounds, shadow.IsVisible, shadow.Mode, shadow.Offset);
        }
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        var child = Content;
        var titleWidth = Header.Length == 0
            ? 0
            : LayoutMath.Add(2, Input.AccessKeyText.Measure(Header, CellPolicy.AmbiguousWidth, useMnemonic: false));
        var chromeWidth = CanClose ? _closeChromeWidth : 0;
        chromeWidth = LayoutMath.Add(chromeWidth, titleWidth);

        if (child is null)
        {
            return new Size(chromeWidth, 0);
        }

        var desired = MeasureChild(child, constraint);
        var contentWidth = child.Visibility == Visibility.Collapsed
            ? 0
            : LayoutMath.Add(desired.Width, child.Margin.Horizontal);
        var contentHeight = child.Visibility == Visibility.Collapsed
            ? 0
            : LayoutMath.Add(desired.Height, child.Margin.Vertical);
        return new Size(
            Math.Max(contentWidth, chromeWidth),
            contentHeight);
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        if (Content is { } content)
        {
            ArrangeChild(content, bounds, ResolvedAxes.Both);
        }

        if (IsSurfacePresented && Visibility == Visibility.Visible)
        {
            SurfaceBounds = Bounds;
        }
    }

    /// <inheritdoc/>
    Rect IOverlayPositionConstraint.ConstrainOverlaySlot(Rect slot, Rect contentBounds)
    {
        var requestedX = Overlay.GetLeft(this) is null && Overlay.GetRight(this) is null &&
                         HorizontalAlignment == HorizontalAlignment.Center
            ? CenterOverlayOrigin(slot.Width, contentBounds.X, contentBounds.Right)
            : slot.X;
        var requestedY = Overlay.GetTop(this) is null && Overlay.GetBottom(this) is null &&
                         VerticalAlignment == VerticalAlignment.Center
            ? CenterOverlayOrigin(slot.Height, contentBounds.Y, contentBounds.Bottom)
            : slot.Y;
        var x = ClampOverlayOrigin(requestedX, slot.Width, contentBounds.X, contentBounds.Right);
        var y = ClampOverlayOrigin(requestedY, slot.Height, contentBounds.Y, contentBounds.Bottom);
        return new Rect(x, y, slot.Width, slot.Height);
    }

    private static int CenterOverlayOrigin(int extent, int leading, int trailing)
    {
        var remaining = (long) trailing - leading - extent;
        return SaturateOverlayCoordinate(leading + (remaining / 2));
    }

    private static int SaturateOverlayCoordinate(long value) => value switch
    {
        < int.MinValue => int.MinValue,
        > int.MaxValue => int.MaxValue,
        _ => (int) value
    };

    private static int ClampOverlayOrigin(int origin, int extent, int leading, int trailing)
    {
        Debug.Assert(extent >= 0, "A Window Overlay slot has a non-negative extent.");
        var maximum = (long) trailing - extent;

        return maximum <= leading
            ? leading
            : Math.Clamp(origin, leading, maximum >= int.MaxValue ? int.MaxValue : (int) maximum);
    }

    /// <inheritdoc/>
    protected override ChromeRenderOptions GetChromeRenderOptions() => new() { SkipBodyFill = true, SkipBorder = true };

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        var opaque = ControlAppearance.HasOpaqueFill(this, GetAppearanceState());

        if (opaque)
        {
            canvas.Clear(Bounds, ResolvedStyle);
        }
    }

    /// <inheritdoc/>
    internal override VisualState GetAppearanceState()
    {
        var state = base.GetAppearanceState() & ~VisualState.FocusWithin;
        return IsActive ? state | VisualState.FocusWithin : state;
    }

    /// <inheritdoc/>
    internal override void RenderOverlay(TerminalCanvas canvas)
    {
        var opaque = ControlAppearance.HasOpaqueFill(this, GetAppearanceState());

        if (Bounds.Width == 0 || Bounds.Height == 0)
        {
            return;
        }

        var border = ControlAppearance.ResolveBorderStyle(this, GetAppearanceState());
        var closeMark = ResolveCloseMarkStyle(border);
        var background = opaque ? BackgroundMode.Opaque : BackgroundMode.Transparent;
        var actualBorder = ActualBorder;
        var borderGlyphs = ResolveBorderGlyphs(actualBorder.GlyphStyle);
        ControlChrome.DrawPartialBorder(
            canvas,
            Bounds,
            actualBorder.Sides,
            borderGlyphs,
            border,
            background);
        var closeChrome = ResolveCloseChromeBounds();
        var titleLane = ResolveTitleLane(closeChrome);

        if (!string.IsNullOrEmpty(Header) && titleLane.Width > 0)
        {
            var headerText = Header;
            var cells = LayoutMath.Add(2, Input.AccessKeyText.Measure(headerText, CellPolicy.AmbiguousWidth, useMnemonic: false));

            if (cells > titleLane.Width && titleLane.Width >= 4)
            {
                // Truncate by cell width, not UTF-16 char count: a char-count
                // slice takes too many code units for wide (CJK/fullwidth)
                // glyphs and can split a surrogate pair mid-codepoint. Reuse
                // the grapheme/cell-aware ellipsis truncation Text.Layout
                // already implements for wrapped text instead of re-deriving
                // it here.
                Span<Line> lines = stackalloc Line[1];
                _ = Layout.Format(
                    headerText,
                    Math.Max(0, titleLane.Width - 2),
                    Overflow.Ellipsis,
                    Alignment.Start,
                    CellPolicy.AmbiguousWidth,
                    lines);
                var line = lines[0];
                var truncated = headerText.AsSpan(line.Offset, line.Length);
                headerText = line.HasEllipsis ? string.Concat(truncated, "…") : truncated.ToString();
                cells = LayoutMath.Add(2, line.Cells);
            }

            var fullInterior = Math.Max(0, Bounds.Width - 2);
            var laneOffset = titleLane.X - (Bounds.X + 1);
            var offset = HeaderPlacement switch
            {
                WindowTitlePlacement.Left => 0,
                WindowTitlePlacement.Center => Math.Max(0, ((fullInterior - cells) / 2) - laneOffset),
                WindowTitlePlacement.Right => Math.Max(0, titleLane.Width - cells),
                _ => throw new InvalidOperationException("The validated title placement is unknown.")
            };
            var title = canvas.Clip(titleLane);
            var leading = title.Draw(
                " ".AsSpan(),
                new Point(titleLane.X + offset, Bounds.Y),
                border,
                background: background);
            var titleCells = Input.AccessKeyText.Draw(
                title,
                headerText,
                leading.Final,
                border,
                background,
                CellPolicy.AmbiguousWidth,
                useMnemonic: false);
            _ = title.Draw(
                " ".AsSpan(),
                new Point(leading.Final.X + titleCells, leading.Final.Y),
                border,
                background: background);
        }

        if (closeChrome.Width == _closeChromeWidth)
        {
            DrawFullCloseChrome(canvas, closeChrome, borderGlyphs, border, closeMark, background);
        }
        else if (closeChrome.Width == 1)
        {
            var themed = ControlGlyphs.Chrome.WindowClose;
            canvas.DrawRune(
                CellGlyphResolver.Resolve(CloseGlyph, themed.Fallback, CellPolicy.AmbiguousWidth),
                new Point(closeChrome.X, closeChrome.Y),
                closeMark,
                background);
        }
    }

    private void DrawFullCloseChrome(
        TerminalCanvas canvas,
        Rect closeChrome,
        BorderGlyphStyle borderGlyphs,
        TerminalStyle border,
        TerminalStyle closeMark,
        BackgroundMode background)
    {
        Debug.Assert(closeChrome.Width == _closeChromeWidth, "Full close chrome has its fixed width.");
        var themed = ControlGlyphs.Chrome.WindowClose;
        var glyph = CellGlyphResolver.Resolve(CloseGlyph, themed.Fallback, CellPolicy.AmbiguousWidth);
        var leftBracket = CellGlyphResolver.Resolve(
            ControlGlyphs.Chrome.WindowCloseLeft.Value,
            ControlGlyphs.Chrome.WindowCloseLeft.Fallback,
            CellPolicy.AmbiguousWidth);
        var rightBracket = CellGlyphResolver.Resolve(
            ControlGlyphs.Chrome.WindowCloseRight.Value,
            ControlGlyphs.Chrome.WindowCloseRight.Fallback,
            CellPolicy.AmbiguousWidth);
        var y = closeChrome.Y;
        canvas.DrawRune(borderGlyphs.Top, new Point(closeChrome.X, y), border, background);
        canvas.DrawRune(borderGlyphs.Top, new Point(closeChrome.X + 1, y), border, background);
        canvas.DrawRune(leftBracket, new Point(closeChrome.X + 2, y), border, background);
        canvas.DrawRune(glyph, new Point(closeChrome.X + 3, y), closeMark, background);
        canvas.DrawRune(rightBracket, new Point(closeChrome.X + 4, y), border, background);
        canvas.DrawRune(borderGlyphs.Top, new Point(closeChrome.X + 5, y), border, background);
        canvas.DrawRune(borderGlyphs.Top, new Point(closeChrome.X + 6, y), border, background);
    }

    private TerminalStyle ResolveCloseMarkStyle(TerminalStyle border)
    {
        var foreground = !EffectiveIsEnabled
            ? Theme?.ResolveColor(ThemeColor.DisabledText) ?? Color.Default
            : _closePressed
                ? Theme?.ResolveColor(ThemeColor.PressedText) ?? Color.Default
                : _closePointerOver
                    ? Theme?.ResolveColor(ThemeColor.ActiveText) ?? Color.Default
                    : Theme?.ResolveColor(ThemeColor.FocusedText) ?? Color.Default;
        return new TerminalStyle(foreground, border.Background, border.Attributes);
    }

    private Rect ResolveCloseChromeBounds()
    {
        if (!CanClose || Bounds.Height == 0 || Bounds.Width < _minimumCloseWidth)
        {
            return default;
        }

        var width = Bounds.Width >= _closeChromeWidth + 2 ? _closeChromeWidth : 1;
        var x = ClosePlacement switch
        {
            WindowClosePlacement.Left => Bounds.X + 1,
            WindowClosePlacement.Right => Bounds.Right - width - 1,
            _ => throw new InvalidOperationException("The validated close placement is unknown.")
        };
        return new Rect(x, Bounds.Y, width, 1);
    }

    private Rect ResolveCloseTargetBounds()
    {
        var chrome = ResolveCloseChromeBounds();

        return chrome.Width == _closeChromeWidth
            ? new Rect(chrome.X + 2, chrome.Y, _closeTargetWidth, 1)
            : chrome;
    }

    private Rect ResolveTitleLane(Rect closeChrome)
    {
        var interiorWidth = Math.Max(0, Bounds.Width - 2);
        var interior = new Rect(Bounds.X + 1, Bounds.Y, interiorWidth, Bounds.Height == 0 ? 0 : 1);

        return closeChrome.Width == 0
            ? interior
            : ClosePlacement switch
            {
                WindowClosePlacement.Left => new Rect(
                    closeChrome.Right,
                    Bounds.Y,
                    Math.Max(0, interior.Right - closeChrome.Right),
                    1),
                WindowClosePlacement.Right => new Rect(
                    interior.X,
                    Bounds.Y,
                    Math.Max(0, closeChrome.X - interior.X),
                    1),
                _ => throw new InvalidOperationException("The validated close placement is unknown.")
            };
    }

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);

        if (eventArgs.Handled)
        {
            return;
        }

        if (eventArgs is KeyEventArgs { Stroke.Action: KeyAction.Press } key)
        {
            var button = key.Stroke.Code == Code.Enter
                ? FindButton(this, static candidate => candidate.IsDefault)
                : key.Stroke.Code == Code.Escape
                    ? FindButton(this, static candidate => candidate.IsCancel)
                    : null;

            if (button is not null)
            {
                button.PerformClick();
                eventArgs.Handled = true;
            }
            else if (key.Stroke.Code == Code.Escape && CloseOnEscape && CanClose)
            {
                RequestClose();
                eventArgs.Handled = true;
            }

            return;
        }

        if (eventArgs is PointerEventArgs pointer)
        {
            UpdateClosePointerOver(pointer);
            _closeInteraction.Handle(pointer);

            if (!pointer.Handled)
            {
                HandlePointerDrag(pointer);
            }
        }
    }

    /// <inheritdoc/>
    protected override void OnLostPointerCapture(PointerCaptureLossReason reason)
    {
        base.OnLostPointerCapture(reason);
        _closeInteraction.CaptureLost();
        _dragging = false;
        _resizing = false;
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        ExceptionDispatchInfo? failure = null;

        ExceptionAggregation.Capture(() => base.OnUnavailable(reason), ref failure);
        ExceptionAggregation.Capture(_closeInteraction.Unavailable, ref failure);
        ExceptionAggregation.Capture(() => SetClosePointerOver(false), ref failure);

        if (reason == ReleaseReason.Disposed)
        {
            PropertyChanged -= OnWindowPropertyChanged;
            Shown = null;
        }

        failure?.Throw();
    }

    #endregion

    #region Close and drag interaction

    private void RequestClose()
    {
        if (_isRequestingClose)
        {
            return;
        }

        _isRequestingClose = true;
        var wasPresented = IsSurfacePresented;
        var closedHandlers = CaptureClosedHandlers();
        ExceptionDispatchInfo? failure = null;

        try
        {
            ExceptionAggregation.Capture(RaiseSurfaceClosing, ref failure);

            if (wasPresented && !IsSurfacePresented)
            {
                ExceptionAggregation.Capture(() => closedHandlers?.Invoke(this, EventArgs.Empty), ref failure);
            }
        }
        finally
        {
            _isRequestingClose = false;
        }

        failure?.Throw();
    }

    private void SetClosePressed(bool value)
    {
        if (_closePressed == value)
        {
            return;
        }

        _closePressed = value;
        Invalidate(InvalidationImpact.Render);
    }

    private void UpdateClosePointerOver(PointerEventArgs eventArgs)
    {
        Debug.Assert(eventArgs is not null, "Pointer handling receives a non-null event.");
        var pointer = eventArgs.Pointer;
        var isPointerOver = pointer.Action != PointerAction.Leave &&
            CanClose &&
            EffectiveIsEnabled &&
            pointer.Cells is { } cells &&
            ResolveCloseTargetBounds().Contains(cells);
        SetClosePointerOver(isPointerOver);
    }

    private void SetClosePointerOver(bool value)
    {
        if (_closePointerOver == value)
        {
            return;
        }

        _closePointerOver = value;
        Invalidate(InvalidationImpact.Render);
    }

    private void HandlePointerDrag(PointerEventArgs eventArgs)
    {
        Debug.Assert(eventArgs is not null, "Pointer handling receives a non-null event.");

        var action = eventArgs.Pointer.Action;

        // A Release must always be able to end an active drag/resize and release
        // capture, regardless of whether CanMove/CanResize was toggled off mid-gesture or
        // this particular event has no cell coordinates (a legitimate state
        // in SGR-pixel mouse mode without cell-metrics mapping). Otherwise
        // the Window keeps pointer capture and the gesture flag stuck true forever.
        if (action == PointerAction.Release && (_dragging || _resizing))
        {
            _dragging = false;
            _resizing = false;
            ReleasePointerCapture();
            eventArgs.Handled = true;
            return;
        }

        if (eventArgs.Pointer.Cells is not { } cells)
        {
            return;
        }

        if (action == PointerAction.Press && eventArgs.Pointer.Buttons == Buttons.Primary)
        {
            // The resize corner is checked first: at a minimum window size it can coincide
            // with the title bar row, and resizing is the more specific gesture there.
            if (CanResize && IsResizeCorner(cells) && CapturePointer())
            {
                _resizing = true;
                _resizePointerOrigin = cells;
                _resizeWindowOrigin = new Size(LocalBounds.Width, LocalBounds.Height);
                _resizeWindowPosition = new Point(LocalBounds.X, LocalBounds.Y);
                eventArgs.Handled = true;
                return;
            }

            if (CanMove && IsTitleBar(cells) && CapturePointer())
            {
                _dragging = true;
                _dragPointerOrigin = cells;
                _dragWindowOrigin = new Point(LocalBounds.X, LocalBounds.Y);
                eventArgs.Handled = true;
            }

            return;
        }

        if (action != PointerAction.Move || !HasPointerCapture)
        {
            return;
        }

        if (_resizing)
        {
            var deltaX = cells.X - _resizePointerOrigin.X;
            var deltaY = cells.Y - _resizePointerOrigin.Y;
            var clientBounds = Parent?.ContentBounds ?? default;
            var (floorWidth, floorHeight) = ChromeResizeFloor();
            var minWidth = Math.Max(MinWidth, floorWidth);
            var minHeight = Math.Max(MinHeight, floorHeight);
            var maximumWidth = Math.Max(minWidth, clientBounds.Width - _resizeWindowPosition.X);
            var maximumHeight = Math.Max(minHeight, clientBounds.Height - _resizeWindowPosition.Y);
            var width = Math.Clamp(_resizeWindowOrigin.Width + deltaX, minWidth, Math.Min(MaxWidth, maximumWidth));
            var height = Math.Clamp(_resizeWindowOrigin.Height + deltaY, minHeight, Math.Min(MaxHeight, maximumHeight));
            Width = Length.Cells(width);
            Height = Length.Cells(height);

            // Own the origin for the duration of the gesture, exactly as the drag path
            // already does, so the top-left corner stays fixed regardless of the window's
            // alignment or Overlay.Right/Bottom anchoring (see #174).
            Overlay.SetLeft(this, Length.Cells(_resizeWindowPosition.X));
            Overlay.SetTop(this, Length.Cells(_resizeWindowPosition.Y));
            eventArgs.Handled = true;
        }
        else if (_dragging)
        {
            var deltaX = cells.X - _dragPointerOrigin.X;
            var deltaY = cells.Y - _dragPointerOrigin.Y;
            var clientBounds = Parent?.ContentBounds ?? default;
            var maximumLeft = Math.Max(0, clientBounds.Width - LocalBounds.Width);
            var maximumTop = Math.Max(0, clientBounds.Height - LocalBounds.Height);
            Overlay.SetLeft(this, Length.Cells(Math.Clamp(_dragWindowOrigin.X + deltaX, 0, maximumLeft)));
            Overlay.SetTop(this, Length.Cells(Math.Clamp(_dragWindowOrigin.Y + deltaY, 0, maximumTop)));
            eventArgs.Handled = true;
        }
    }

    private bool IsTitleBar(Point cells) =>
        cells.Y == Bounds.Y && cells.X >= Bounds.X && cells.X < Bounds.Right;

    private bool IsResizeCorner(Point cells) =>
        cells.Y == Bounds.Bottom - 1 && cells.X == Bounds.Right - 1;

    // The resize gesture must never collapse the window past a size the user cannot see or grab
    // again: enough cells for the border sides that are actually enabled, plus one content cell.
    // MinWidth/MinHeight default to 0, so clamping to them alone lets an ordinary inward drag
    // shrink a window to 0x0 (see #170).
    private (int Width, int Height) ChromeResizeFloor()
    {
        var sides = ActualBorder.Sides;
        var horizontal = ((sides & BorderSide.Left) != 0 ? 1 : 0) + ((sides & BorderSide.Right) != 0 ? 1 : 0);
        var vertical = ((sides & BorderSide.Top) != 0 ? 1 : 0) + ((sides & BorderSide.Bottom) != 0 ? 1 : 0);
        return (horizontal + 1, vertical + 1);
    }

    #endregion

    #region Implementation

    /// <inheritdoc/>
    protected override void OnAttached()
    {
        base.OnAttached();

        if (Visibility == Visibility.Visible && !IsSurfacePresented)
        {
            OpenSurface(() => SurfaceBounds = Bounds);

            // Mirrors the Shown notification OnWindowPropertyChanged raises for an explicit
            // Visibility transition: the default-Visible field initializer never runs the
            // property's set block, so this attach path is otherwise the only one that opens
            // the surface without ever raising Shown. Initial focus assignment stays out of
            // this path — it's already owned by ShowModal's background-focus snapshot for
            // windows that become modal right after attaching.
            Shown?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnWindowPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.PropertyName != nameof(Visibility) ||
            Visibility != Visibility.Visible)
        {
            return;
        }

        if (Dispatcher is not null && !IsSurfacePresented)
        {
            OpenSurface(() => SurfaceBounds = Bounds);
        }

        Shown?.Invoke(this, EventArgs.Empty);

        if (_isShowingModal)
        {
            return;
        }

        var first = FindFirstFocusable(this);
        _ = first is not null ? FocusOwner?.Focus(first) : FocusOwner?.Focus(this);
    }

    private ModalScope ShowModalCore(
        OutsideInteraction outsideInteraction,
        Control? initialFocus,
        Visibility previousVisibility)
    {
        ModalScope? scope = null;

        try
        {
            if (Visibility != Visibility.Visible)
            {
                Visibility = Visibility.Visible;
            }

            scope = EnterSurfaceModal(outsideInteraction, initialFocus);

            // Modal-entry callbacks may synchronously hide the Window or dispose
            // the just-entered manager scope. Preserve that owner decision and
            // never retain an inactive presentation handle.
            if (!scope.IsActive || Visibility != Visibility.Visible)
            {
                if (scope.IsActive)
                {
                    scope.Dispose();
                }

                return scope;
            }

            TrackModalCallbacks(scope);
            return scope;
        }
        catch (Exception exception)
        {
            var failure = ExceptionDispatchInfo.Capture(exception);

            if (scope is { IsActive: true })
            {
                try
                {
                    scope.Dispose();
                }
                catch
                {
                    // The initiating failure remains authoritative.
                }
            }

            if (Visibility != previousVisibility)
            {
                try
                {
                    Visibility = previousVisibility;
                }
                catch
                {
                    // Visibility commits before callbacks, so exact state is restored
                    // even when rollback publication fails.
                }
            }

            failure.Throw();
            throw;
        }
    }

    private void TrackModalCallbacks(ModalScope scope)
    {
        Debug.Assert(scope is not null, "A Window observes one concrete modal lifetime.");
        Debug.Assert(scope.IsActive, "A Window observes only an active modal lifetime.");
        scope.DismissRequested += OnModalDismissRequested;
        scope.Exited += OnWindowModalExited;
    }

    private void OnModalDismissRequested(object? sender, EventArgs eventArgs)
    {
        _ = eventArgs;

        if (sender is ModalScope scope &&
            scope.IsActive &&
            Visibility == Visibility.Visible)
        {
            RequestClose();
        }
    }

    private void OnWindowModalExited(object? sender, EventArgs eventArgs)
    {
        _ = eventArgs;

        if (sender is ModalScope scope)
        {
            scope.DismissRequested -= OnModalDismissRequested;
            scope.Exited -= OnWindowModalExited;
        }
    }

    private static Control? FindFirstFocusable(Control root)
    {
        var count = root.OwnedControlCount;

        for (var i = 0; i < count; i++)
        {
            var child = root.OwnedControlAt(i);

            if (child is { CanFocus: true, EffectiveIsVisible: true, EffectiveIsEnabled: true })
            {
                return child;
            }

            if (FindFirstFocusable(child) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private static Button? FindButton(Control control, Func<Button, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(predicate);

        if (control is Button { EffectiveIsEnabled: true, EffectiveIsVisible: true } button && predicate(button))
        {
            return button;
        }

        var count = control.OwnedControlCount;

        for (var index = 0; index < count; index++)
        {
            if (FindButton(control.OwnedControlAt(index), predicate) is { } result)
            {
                return result;
            }
        }

        return null;
    }

    #endregion
}
