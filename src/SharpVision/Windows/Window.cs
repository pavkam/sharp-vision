// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Windows;

using System.Runtime.ExceptionServices;

using SharpVision.Controls;
using SharpVision.Controls.Input;
using SharpVision.Controls.Layout;
using SharpVision.Surfaces;
using SharpVision.Terminal.Input;
using SharpVision.Text;

using Terminal.Rendering;

/// <summary>Frames one owned content control as a titled terminal window with optional Turbo Vision-style shadowing.</summary>
[PublicAPI]
public partial class Window: ChromeAuthoringFloatingSurface, IOverlayPositionConstraint
{
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
    private bool _isRequestingClose;
    private bool _isShowingModal;

    /// <summary>Whether a completed <see cref="RequestClose"/> has run since Visibility last
    /// became <see cref="Visibility.Visible"/>. An unattached Window has no
    /// <see cref="FloatingSurfaceBase.SurfacePresented"/> transition to detect a repeated
    /// close from, since it was never presented in the first place and closing it does not
    /// collapse it - this latch is the substitute open/closed bit for exactly that case, cleared
    /// whenever <see cref="OnWindowPropertyChanged"/> observes Visibility return to Visible.
    /// </summary>
    private bool _closedSinceVisible;

    #region Construction and properties

    /// <inheritdoc/>
    protected override AppearanceStates GetDefaultAppearanceStates(Theme? theme) =>
        (theme ?? ThemeCatalog.Dark).GetWindowStyleSet().ToAppearanceStates();

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
            _ => RequestClose(),
            () => Capabilities.KeyReleaseEvents.Authoritative);
        PropertyChanged += OnWindowPropertyChanged;
    }

    /// <summary>Raised after this Window becomes visible.</summary>
    public event EventHandler? Shown;

    /// <summary>Gets whether this Window is the active Window of its owning Application.</summary>
    /// <remarks>Activation is application-owned and does not imply keyboard focus or z-order promotion.</remarks>
    public bool Active { get; private set; }

    /// <summary>Commits application-owned activation and invalidates the active appearance.</summary>
    /// <param name="value">Whether this Window is active.</param>
    internal void SetActive(bool value)
    {
        VerifyMutable();

        if (Active == value)
        {
            return;
        }

        Active = value;
        InvalidateVisualState();
        NotifyPropertyChanged(nameof(Active), InvalidationImpact.None);
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
    /// <see cref="FloatingSurfaceBase.Closing"/> and, by default, collapses and closes the Window afterward,
    /// ending its modal presentation; a <see cref="FloatingSurfaceBase.Closing"/> handler that itself changes
    /// <see cref="Visibility"/> (hiding, restoring, or disposing the Window) takes responsibility for the
    /// outcome instead. This call suppresses only the legacy visibility autofocus transaction,
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
        ControlBase? initialFocus = null)
    {
        VerifyMutable();

        if (_isShowingModal)
        {
            throw new InvalidOperationException("Window modal presentations cannot be reentered.");
        }

        _isShowingModal = true;

        try
        {
            outsideInteraction.ValidateDefined(nameof(outsideInteraction), "The outside-interaction policy is unknown.");

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
    /// Resizing keeps the window's top-left corner fixed and adjusts <see cref="ControlBase.Width"/> and
    /// <see cref="ControlBase.Height"/> within <see cref="ControlBase.MinWidth"/>/<see cref="ControlBase.MaxWidth"/>,
    /// <see cref="ControlBase.MinHeight"/>/<see cref="ControlBase.MaxHeight"/>, and the parent's committed
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
    } = true;

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
            value.ValidateDefined(nameof(value), "The close placement is unknown.");

            _ = SetProperty(ref field, value, InvalidationImpact.Render);
        }
    } = WindowClosePlacement.Left;

    /// <summary>Gets or sets the non-null header written into the top edge.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="ArgumentException">The value contains a terminal control character.</exception>
    /// <exception cref="InvalidOperationException">The attached window is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The window is disposed.</exception>
    public string Header
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            value.ThrowIfContainsControls(
                nameof(value),
                "A window header cannot contain terminal controls.");
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
            value.ValidateDefined(nameof(value), "The title placement is unknown.");

            _ = SetProperty(ref field, value, InvalidationImpact.Render);
        }
    } = WindowTitlePlacement.Left;

    /// <inheritdoc/>
    protected override string? AccessKeyText => null;

    #endregion

    #region Layout and rendering

    /// <inheritdoc/>
    /// <inheritdoc/>
    protected override Rect VisualBounds
    {
        get
        {
            var shadow = ActualShadow;
            return Bounds.ExpandVisualBounds(shadow.Visible, shadow.Mode, shadow.Offset);
        }
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        var child = Content;
        var titleWidth = Header.Length == 0
            ? 0
            : 2.Add(Header.Measure(CellPolicy.AmbiguousWidth, useMnemonic: false));
        var chromeWidth = CanClose ? _closeChromeWidth : 0;
        chromeWidth = chromeWidth.Add(titleWidth);

        if (child is null)
        {
            return new Size(chromeWidth, 0);
        }

        var desired = MeasureChild(child, constraint);
        var contentWidth = child.Visibility == Visibility.Collapsed
            ? 0
            : desired.Width.Add(child.Margin.Horizontal);
        var contentHeight = child.Visibility == Visibility.Collapsed
            ? 0
            : desired.Height.Add(child.Margin.Vertical);
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

        if (SurfacePresented && Visibility == Visibility.Visible)
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
        var opaque = this.HasOpaqueFill(GetAppearanceState());

        if (opaque)
        {
            canvas.Clear(Bounds, ResolvedStyle);
        }
    }

    /// <inheritdoc/>
    internal override VisualState GetAppearanceState()
    {
        var state = base.GetAppearanceState() & ~VisualState.FocusWithin;
        return Active ? state | VisualState.FocusWithin : state;
    }

    /// <inheritdoc/>
    internal override void RenderOverlay(TerminalCanvas canvas)
    {
        var opaque = this.HasOpaqueFill(GetAppearanceState());

        if (Bounds.Width == 0 || Bounds.Height == 0)
        {
            return;
        }

        var border = this.ResolveBorderStyle(GetAppearanceState());
        var closeMark = ResolveCloseMarkStyle(border);
        var background = opaque ? BackgroundMode.Opaque : BackgroundMode.Transparent;
        var actualBorder = ActualBorder;
        var borderGlyphs = ResolveBorderGlyphs(actualBorder.GlyphStyle);
        canvas.DrawPartialBorder(
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
            var cells = 2.Add(headerText.Measure(CellPolicy.AmbiguousWidth, useMnemonic: false));

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
                cells = 2.Add(line.Cells);
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
            var titleCells = headerText.Draw(
                title,
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
            canvas.DrawRune(
                ResolveCloseGlyph(),
                new Point(closeChrome.X, closeChrome.Y),
                closeMark,
                background);
        }
    }

    /// <summary>Gets the active window style, falling back to the code-owned default.</summary>
    /// <remarks>
    /// The close chrome reads its glyphs from here rather than from the internal
    /// <c>ControlGlyphs</c> registry, which nothing in the theme pipeline parses. Window resolves
    /// appearance through <c>GetDefaultAppearanceStates</c>, which flattens the style to
    /// <c>AppearanceStates</c> and drops every non-appearance member, so the style set is consulted
    /// directly for the glyph family.
    /// </remarks>
    private WindowStyle WindowStyleValue =>
        (Theme ?? ThemeCatalog.Dark).GetWindowStyleSet().Normal;

    private Rune ResolveCloseGlyph() =>
        WindowStyleValue.CloseGlyph.Resolve(
            ControlGlyphs.Chrome.WindowClose.Fallback,
            CellPolicy.AmbiguousWidth);

    private void DrawFullCloseChrome(
        TerminalCanvas canvas,
        Rect closeChrome,
        BorderGlyphStyle borderGlyphs,
        TerminalStyle border,
        TerminalStyle closeMark,
        BackgroundMode background)
    {
        Debug.Assert(closeChrome.Width == _closeChromeWidth, "Full close chrome has its fixed width.");
        var style = WindowStyleValue;
        var glyph = ResolveCloseGlyph();
        var leftBracket = style.CloseLeftBracket.Resolve(
            ControlGlyphs.Chrome.WindowCloseLeft.Fallback,
            CellPolicy.AmbiguousWidth);
        var rightBracket = style.CloseRightBracket.Resolve(
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
            ? Theme?.ResolveColor(SemanticColor.DisabledText) ?? Color.Default
            : _closePressed
                ? Theme?.ResolveColor(SemanticColor.PressedText) ?? Color.Default
                : _closePointerOver
                    ? Theme?.ResolveColor(SemanticColor.ActiveText) ?? Color.Default
                    : Theme?.ResolveColor(SemanticColor.FocusedText) ?? Color.Default;
        return new TerminalStyle(foreground, border.Background, border.Attributes);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The close-button glyph states resolve <see cref="SemanticColor.DisabledText"/>,
    /// <see cref="SemanticColor.PressedText"/>, <see cref="SemanticColor.ActiveText"/>, and
    /// <see cref="SemanticColor.FocusedText"/> directly at render time rather than through a style
    /// contract, so the base role-profile comparison alone cannot detect a theme swap
    /// confined to these roles.
    /// </remarks>
    protected override InvalidationImpact GetThemeChangeImpact(
        Theme? previous,
        Theme? current,
        Face? previousParentAmbientFace,
        Face? currentParentAmbientFace)
    {
        var baseImpact = base.GetThemeChangeImpact(
            previous,
            current,
            previousParentAmbientFace,
            currentParentAmbientFace);
        var colorImpact =
            ResolveDirectColor(previous, SemanticColor.DisabledText) != ResolveDirectColor(current, SemanticColor.DisabledText) ||
            ResolveDirectColor(previous, SemanticColor.PressedText) != ResolveDirectColor(current, SemanticColor.PressedText) ||
            ResolveDirectColor(previous, SemanticColor.ActiveText) != ResolveDirectColor(current, SemanticColor.ActiveText) ||
            ResolveDirectColor(previous, SemanticColor.FocusedText) != ResolveDirectColor(current, SemanticColor.FocusedText)
                ? InvalidationImpact.Render
                : InvalidationImpact.None;

        return MaximumImpact(baseImpact, colorImpact);
    }

    private static Color ResolveDirectColor(Theme? theme, SemanticColor color) =>
        theme?.ResolveColor(color) ?? Color.Default;

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

    /// <summary>Requests this Window close, following the same veto and default-collapse sequence as
    /// its close affordance, Escape, and modal dismissal.</summary>
    /// <remarks>
    /// Raises <see cref="FloatingSurfaceBase.CloseRequested"/> first; a handler that cancels leaves the
    /// Window untouched. Otherwise raises <see cref="FloatingSurfaceBase.Closing"/> and, by default,
    /// collapses the Window - unless a <see cref="FloatingSurfaceBase.Closing"/> handler already took
    /// responsibility for <see cref="Visibility"/> itself, in which case
    /// <see cref="FloatingSurfaceBase.Closed"/> is suppressed. Unlike <see cref="CanClose"/>, which only
    /// gates the close affordance and <see cref="CloseOnEscape"/>, this method always attempts to
    /// close regardless of <see cref="CanClose"/>, matching how modal outside-dismissal already
    /// behaves.
    /// </remarks>
    public void Close() => RequestClose();

    private void RequestClose()
    {
        // Every other public mutator verifies dispatcher affinity and disposal first; Close()
        // was the sole outlier, silently succeeding on a disposed Window and running the
        // handlers below on the calling thread before anything threw for an off-dispatcher call.
        VerifyMutable();

        if (_isRequestingClose)
        {
            return;
        }

        // An unattached Window is Visible but never presented, and Escape/affordance closure is
        // still expected to raise Closing for it - so the guard cannot be the plain
        // !SurfacePresented check CloseSurfaceCore uses. A Window that is neither presented
        // nor still Visible (already closed, or detached after being collapsed) has nothing left
        // to close, and closing it again must raise nothing. That is not sufficient by
        // itself: a never-attached, still-Visible Window is neither presented nor collapsed by a
        // completed close (there is no presentation to tear down), so Visibility alone cannot
        // distinguish "legitimately still open" from "already closed once already" for it -
        // _closedSinceVisible is the substitute bit for exactly that case.
        if (!(SurfacePresented || (Visibility == Visibility.Visible && !_closedSinceVisible)))
        {
            return;
        }

        // Set before raising CloseRequested, not after: a handler that calls Close()/RequestClose()
        // synchronously from inside CloseRequested must see the guard already armed, the same way
        // a Closing handler already does (guarded below), or the reentrant call re-enters
        // RaiseCloseRequested and invokes the same handler again with no bound on the recursion.
        _isRequestingClose = true;

        if (!RaiseCloseRequested())
        {
            _isRequestingClose = false;
            return;
        }

        var wasPresented = SurfacePresented;
        var closedHandlers = CaptureClosedHandlers();
        var visibilityTouchedByHandler = false;
        void OnVisibilityChangedDuringClosing(object? sender, EventArgs eventArgs) => visibilityTouchedByHandler = true;
        ExceptionDispatchInfo? failure = null;

        try
        {
            VisibilityChanged += OnVisibilityChangedDuringClosing;

            try
            {
                ExceptionAggregation.Capture(RaiseSurfaceClosing, ref failure);
            }
            finally
            {
                VisibilityChanged -= OnVisibilityChangedDuringClosing;
            }

            // Close by default: only skip when a Closing handler already took responsibility for
            // visibility itself (hid it, restored it, or disposed the Window), whether or not that
            // left Visibility back at its original value.
            if (wasPresented && SurfacePresented && !visibilityTouchedByHandler)
            {
                ExceptionAggregation.Capture(() => Visibility = Visibility.Collapsed, ref failure);
            }

            if (wasPresented && !SurfacePresented)
            {
                ExceptionAggregation.Capture(() => closedHandlers?.Invoke(this, EventArgs.Empty), ref failure);
            }

            // Marks a never-attached Window closed even though nothing above could collapse it -
            // OnWindowPropertyChanged clears this the moment Visibility next becomes Visible, so
            // a handler that reopens it during Closing is unaffected.
            _closedSinceVisible = true;
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

        // A Release or Leave must always be able to end an active drag/resize and release
        // capture, regardless of whether CanMove/CanResize was toggled off mid-gesture or
        // this particular event has no cell coordinates (a legitimate state
        // in SGR-pixel mouse mode without cell-metrics mapping, and true of every
        // Leave by construction). Otherwise the Window keeps pointer capture and the
        // gesture flag stuck true forever — releasing the button outside the terminal
        // delivers only a Leave, never a Release.
        if (action is PointerAction.Release or PointerAction.Leave && (_dragging || _resizing))
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
            // MaxWidth/MaxHeight are validated only against MinWidth/MinHeight (which default to
            // 0), not against the chrome resize floor computed above, so a caller can legally set
            // e.g. MaxWidth below the border chrome's minimum drawable width. Clamping the upper
            // bound up to at least minWidth/minHeight (in addition to the lower bound already
            // being minWidth/minHeight) keeps Math.Clamp's [low, high] arguments ordered in that
            // case, instead of throwing ArgumentException on the very first resize drag.
            var width = Math.Clamp(
                _resizeWindowOrigin.Width + deltaX,
                minWidth,
                Math.Max(minWidth, Math.Min(MaxWidth, maximumWidth)));
            var height = Math.Clamp(
                _resizeWindowOrigin.Height + deltaY,
                minHeight,
                Math.Max(minHeight, Math.Min(MaxHeight, maximumHeight)));
            Width = Length.Cells(width);
            Height = Length.Cells(height);

            // Own the origin for the duration of the gesture, exactly as the drag path
            // already does, so the top-left corner stays fixed regardless of the window's
            // alignment or Overlay.Right/Bottom anchoring.
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
    // shrink a window to 0x0.
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

        if (Visibility == Visibility.Visible && !SurfacePresented)
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

        // A fresh Visible state is a fresh open, regardless of how many times this Window
        // closed before - clear the never-attached substitute open/closed bit here so the next
        // RequestClose is not rejected as a repeat of an already-completed close.
        _closedSinceVisible = false;

        if (Dispatcher is not null && !SurfacePresented)
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
        ControlBase? initialFocus,
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
            if (!scope.Active || Visibility != Visibility.Visible)
            {
                if (scope.Active)
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

            if (scope is { Active: true })
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
        Debug.Assert(scope.Active, "A Window observes only an active modal lifetime.");
        scope.DismissRequested += OnModalDismissRequested;
        scope.Exited += OnWindowModalExited;
    }

    private void OnModalDismissRequested(object? sender, EventArgs eventArgs)
    {
        _ = eventArgs;

        if (sender is ModalScope scope &&
            scope.Active &&
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

    private static ControlBase? FindFirstFocusable(ControlBase root)
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

    private static Button? FindButton(ControlBase control, Func<Button, bool> predicate)
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
