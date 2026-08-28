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
public partial class Window: FloatingSurfaceBase, IOverlayPositionConstraint
{
    private const int _closeChromeWidth = 7;
    private const int _closeTargetWidth = 3;
    private const int _minimumCloseWidth = 4;

    private readonly PressBehavior _closeInteraction;
    private readonly ThemeValueDependency<WindowCloseChromeThemeValue> _closeChromeThemeDependency;
    private bool _dragging;
    private Point _dragPointerOrigin;
    private Point _dragWindowOrigin;
    private bool _resizing;
    private Point _resizePointerOrigin;
    private Size _resizeWindowOrigin;
    private Point _resizeWindowPosition;
    private bool _closePointerOver;
    private bool _closePressed;
    private bool _isShowingModal;

    #region Construction and properties

    /// <inheritdoc/>
    protected override AppearanceStates GetDefaultAppearanceStates(Theme? theme) =>
        (theme ?? ThemeCatalog.Dark).GetWindowStyleSet().ToAppearanceStates();

    /// <summary>Initializes an opaque movable Window with a paired-line border and composite shadow.</summary>
    public Window()
    {
        _closeChromeThemeDependency = new ThemeValueDependency<WindowCloseChromeThemeValue>(
            ResolveCloseChromeThemeValue,
            InvalidationImpact.Render);
        _closeInteraction = new PressBehavior(
            ResolveCloseTargetBounds,
            () => !IsDisposed && CanClose && EffectiveIsEnabled && EffectiveIsVisible && ResolveCloseTargetBounds().Width > 0,
            static () => false,
            RequestFocus,
            CapturePointer,
            () => HasPointerCapture,
            ReleasePointerCapture,
            SetClosePressed,
            _ => RequestClose(),
            () => Capabilities.KeyReleaseEvents.Authoritative);
        BeginSurfaceOpenLifetime();
        PropertyChanged += OnWindowPropertyChanged;
        EnableChromeAuthoring();
    }

    /// <summary>Gets the retained close-chrome hover detail used to prove reconciliation with the
    /// framework pointer-over transition.</summary>
    /// <returns>Whether close-chrome hover is retained.</returns>
    internal bool HasClosePointerOver() => _closePointerOver;

    /// <summary>Raised after this Window becomes visible.</summary>
    public event EventHandler? Shown;

    /// <summary>Gets whether this Window is the active Window of its owning Application.</summary>
    /// <remarks>Activation is application-owned and does not imply keyboard focus or z-order promotion.</remarks>
    public bool IsActive { get; private set; }

    /// <summary>Gets or sets the identity of the activation manager that most recently activated this Window.</summary>
    /// <remarks>
    /// The bounded manager history uses this retained-tree marker to distinguish an evicted prior
    /// activation candidate from a Window that has never participated in that manager.
    /// </remarks>
    internal object? ActivationHistoryOwner { get; set; }

    /// <summary>Commits application-owned activation and invalidates the active appearance.</summary>
    /// <param name="value">Whether this Window is active.</param>
    internal void SetActive(bool value)
    {
        if (!value && IsDisposed)
        {
            // A disposal triggered from the activation notification completes before the
            // serialized manager can drain its fallback request. The dead Window must still
            // release its internal activation identity, but it cannot publish or invalidate.
            IsActive = false;
            return;
        }

        VerifyMutable();

        if (IsActive == value)
        {
            return;
        }

        IsActive = value;
        InvalidateVisualState();
        NotifyPropertyChanged(nameof(IsActive), InvalidationImpact.None);
    }

    /// <summary>Gets whether this Window is currently open.</summary>
    /// <remarks>
    /// Mirrors the intent of <see cref="Popups.Popup.IsOpen"/> - answering "is this surface open" - but is a
    /// read-only computed query rather than a settable driver: Window's open/close mechanics remain
    /// owned by <see cref="ControlBase.Visibility"/> and <see cref="Close"/>. An attached, presented
    /// Window is open exactly while <see cref="FloatingSurfaceBase.IsSurfacePresented"/> holds.
    /// An unattached Window is never presented - it has no attach transition to opt into - so a
    /// never-attached Window that is <see cref="Visibility.Visible"/> and has not yet completed a
    /// <see cref="Close"/> since is also considered open. This is precisely the logical negation of
    /// the closed-guard <see cref="Close"/> checks before running its veto/collapse sequence.
    /// </remarks>
    public bool IsOpen => IsSurfaceOpen;

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
            ArgumentOutOfRangeException.ThrowIfNotDefined(outsideInteraction, nameof(outsideInteraction), "The outside-interaction policy is unknown.");

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
    /// <remarks>
    /// Dragging keeps the window's border box inside its parent's committed content area. Setting
    /// this property to <see langword="false"/> ends an active move gesture on its next pointer event.
    /// </remarks>
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
    /// Setting this property to <see langword="false"/> ends an active resize gesture on its next
    /// pointer event.
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
            ArgumentOutOfRangeException.ThrowIfNotDefined(value, nameof(value), "The close placement is unknown.");

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
            ArgumentException.ThrowIfContainsControls(value, nameof(value), "A window header cannot contain terminal controls.");
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
            ArgumentOutOfRangeException.ThrowIfNotDefined(value, nameof(value), "The title placement is unknown.");

            _ = SetProperty(ref field, value, InvalidationImpact.Render);
        }
    } = WindowTitlePlacement.Left;

    /// <inheritdoc/>
    protected override string? AccessKeyText => null;

    #endregion

    #region Layout and rendering

    /// <inheritdoc/>
    protected override Rect VisualBounds
    {
        get
        {
            var shadow = ActualShadow;
            return Bounds.ExpandVisualBounds(shadow.IsVisible, shadow.Mode, shadow.Offset);
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

        if (IsSurfacePresented && Visibility == Visibility.Visible)
        {
            SurfaceBounds = Bounds;
        }
    }

    /// <inheritdoc/>
    [Pure]
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

    [Pure]
    private static int CenterOverlayOrigin(int extent, int leading, int trailing)
    {
        var remaining = (long) trailing - leading - extent;
        return SaturateOverlayCoordinate(leading + (remaining / 2));
    }

    [Pure]
    private static int SaturateOverlayCoordinate(long value) => value switch
    {
        < int.MinValue => int.MinValue,
        > int.MaxValue => int.MaxValue,
        _ => (int) value
    };

    [Pure]
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
        return IsActive ? state | VisualState.FocusWithin : state;
    }

    /// <inheritdoc/>
    internal override void RenderOverlay(TerminalCanvas canvas)
    {
        var opaque = this.HasOpaqueFill(GetAppearanceState());

        if (Bounds.Width == 0 || Bounds.Height == 0)
        {
            return;
        }

        var borderStyles = this.ResolveBorderStyles(GetAppearanceState());
        var border = borderStyles.Top;
        var closeMark = ResolveCloseMarkStyle(border);
        var background = opaque ? BackgroundMode.Opaque : BackgroundMode.Transparent;
        var actualBorder = ActualBorder;
        var borderGlyphs = ResolveBorderGlyphs(actualBorder.GlyphStyle);
        canvas.DrawPartialBorder(
            Bounds,
            actualBorder.Sides,
            borderGlyphs,
            borderStyles,
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
                var ellipsisGlyph = ControlGlyphs.Text.Ellipsis.Value.Resolve(
                    ControlGlyphs.Text.Ellipsis.Fallback,
                    CellPolicy.AmbiguousWidth);
                headerText = line.HasEllipsis
                    ? string.Concat(truncated, ellipsisGlyph.ToString())
                    : truncated.ToString();
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

    /// <summary>Gets the window style whose close-chrome members (glyphs and mark colors) back
    /// this Window's close affordance, falling back to the code-owned default.</summary>
    /// <param name="theme">The theme to resolve against, or null for the code-owned default.</param>
    /// <remarks>
    /// The close chrome reads its glyphs from here rather than from the internal
    /// <c>ControlGlyphs</c> registry, which nothing in the theme pipeline parses. Window resolves
    /// appearance through <c>GetDefaultAppearanceStates</c>, which flattens the style to
    /// <c>AppearanceStates</c> and drops every non-appearance member, so the style set is consulted
    /// directly for the glyph family. A plain Window has no style of its own beyond the generic
    /// "window" theme section, so this base implementation is that section's Normal state; a
    /// dialog subtype that owns its own style overrides this to resolve its own section instead.
    /// </remarks>
    private protected virtual WindowStyle ResolveCloseChromeStyle(Theme? theme) =>
        (theme ?? ThemeCatalog.Dark).GetWindowStyleSet().Normal;

    /// <summary>Resolves every close-chrome member to immutable Theme-specific data.</summary>
    private WindowCloseChromeThemeValue ResolveCloseChromeThemeValue(Theme theme)
    {
        var style = ResolveCloseChromeStyle(theme);
        return new WindowCloseChromeThemeValue(
            style.CloseGlyph,
            style.CloseLeftBracket,
            style.CloseRightBracket,
            style.CloseMarkColor.Resolve(theme),
            style.CloseMarkActiveColor.Resolve(theme),
            style.CloseMarkPressedColor.Resolve(theme),
            style.CloseMarkDisabledColor.Resolve(theme));
    }

    private Rune ResolveCloseGlyph() =>
        ResolveThemeValue(_closeChromeThemeDependency).CloseGlyph.Resolve(
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
        var style = ResolveThemeValue(_closeChromeThemeDependency);
        var glyph = ResolveCloseGlyph();
        var leftBracket = style.LeftBracket.Resolve(
            ControlGlyphs.Chrome.WindowCloseLeft.Fallback,
            CellPolicy.AmbiguousWidth);
        var rightBracket = style.RightBracket.Resolve(
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
        var style = ResolveThemeValue(_closeChromeThemeDependency);
        var foreground = !EffectiveIsEnabled
            ? style.DisabledForeground
            : _closePressed
                ? style.PressedForeground
                : _closePointerOver
                    ? style.ActiveForeground
                    : style.Foreground;
        return new TerminalStyle(foreground, border.Background, border.Attributes);
    }

    [Pure]
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

    [Pure]
    private Rect ResolveCloseTargetBounds()
    {
        var chrome = ResolveCloseChromeBounds();

        return chrome.Width == _closeChromeWidth
            ? new Rect(chrome.X + 2, chrome.Y, _closeTargetWidth, 1)
            : chrome;
    }

    [Pure]
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

        if (eventArgs.IsHandled)
        {
            return;
        }

        if (eventArgs is KeyEventArgs { IsInitialKeyDown: true } key)
        {
            var button = key.Stroke.Modifiers.IsActivationEligible()
                ? key.Stroke.Code == Code.Enter
                    ? FindButton(this, static candidate => candidate.IsDefault)
                    : key.Stroke.Code == Code.Escape
                        ? FindButton(this, static candidate => candidate.IsCancel)
                        : null
                : null;

            if (button is not null)
            {
                button.PerformClick();
                eventArgs.IsHandled = true;
            }
            else if (key.Stroke.Code == Code.Escape &&
                     key.Stroke.Modifiers.IsActivationEligible() &&
                     CloseOnEscape &&
                     CanClose)
            {
                RequestClose();
                eventArgs.IsHandled = true;
            }

            return;
        }

        if (eventArgs is PointerEventArgs pointer)
        {
            UpdateClosePointerOver(pointer);
            _closeInteraction.Handle(pointer);

            if (!pointer.IsHandled)
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
    protected override void OnPointerOverChanged(bool isPointerOver, bool isPointerDirectlyOver)
    {
        base.OnPointerOverChanged(isPointerOver, isPointerDirectlyOver);
        _ = isPointerDirectlyOver;

        if (!isPointerOver)
        {
            SetClosePointerOver(false);
        }
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        ExceptionDispatchInfo? failure = null;

        ExceptionAggregation.Capture(() => base.OnUnavailable(reason), ref failure);
        ExceptionAggregation.Capture(_closeInteraction.Unavailable, ref failure);

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
        var visibilityTouchedByHandler = false;

        void OnVisibilityChangedDuringClosing(object? sender, EventArgs eventArgs)
        {
            _ = sender;
            _ = eventArgs;
            visibilityTouchedByHandler = true;
        }

        void PrepareClosingState() => VisibilityChanged += OnVisibilityChangedDuringClosing;

        bool CommitClosingState()
        {
            VisibilityChanged -= OnVisibilityChangedDuringClosing;

            if (visibilityTouchedByHandler)
            {
                return !IsSurfacePresented;
            }

            if (IsSurfacePresented)
            {
                Visibility = Visibility.Collapsed;
            }

            return true;
        }

        try
        {
            _ = CloseSurfaceAfterClosing(
                PrepareClosingState,
                CommitClosingState,
                static () => { });
        }
        finally
        {
            VisibilityChanged -= OnVisibilityChangedDuringClosing;
        }
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
        if ((action == PointerAction.Leave || PointerButtonTransition.IsPrimaryRelease(eventArgs.Pointer)) &&
            (_dragging || _resizing))
        {
            _dragging = false;
            _resizing = false;
            ReleasePointerCapture();
            eventArgs.IsHandled = true;
            return;
        }

        if (eventArgs.Pointer.Cells is not { } cells)
        {
            return;
        }

        if (action == PointerAction.Press &&
            (eventArgs.Pointer.Buttons & Buttons.Primary) != 0)
        {
            // The resize corner is checked first: at a minimum window size it can coincide
            // with the title bar row, and resizing is the more specific gesture there.
            if (CanResize && IsResizeCorner(cells) && CapturePointer())
            {
                _resizing = true;
                _resizePointerOrigin = cells;
                _resizeWindowOrigin = new Size(LocalBounds.Width, LocalBounds.Height);
                _resizeWindowPosition = new Point(LocalBounds.X, LocalBounds.Y);
                eventArgs.IsHandled = true;
                return;
            }

            if (CanMove && IsTitleBar(cells) && CapturePointer())
            {
                _dragging = true;
                _dragPointerOrigin = cells;
                _dragWindowOrigin = new Point(LocalBounds.X, LocalBounds.Y);
                eventArgs.IsHandled = true;
            }

            return;
        }

        if (action != PointerAction.Move || !HasPointerCapture)
        {
            return;
        }

        if ((_resizing && !CanResize) || (_dragging && !CanMove))
        {
            _dragging = false;
            _resizing = false;
            ReleasePointerCapture();
            eventArgs.IsHandled = true;
            return;
        }

        if (_resizing)
        {
            var gestureParent = Parent;
            var originalHeight = Height;
            var originalLeft = Overlay.GetLeft(this);
            var originalTop = Overlay.GetTop(this);
            var originalRight = Overlay.GetRight(this);
            var originalBottom = Overlay.GetBottom(this);
            var deltaX = (long) cells.X - _resizePointerOrigin.X;
            var deltaY = (long) cells.Y - _resizePointerOrigin.Y;
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
            var width = (int) Math.Clamp(
                _resizeWindowOrigin.Width + deltaX,
                minWidth,
                Math.Max(minWidth, Math.Min(MaxWidth, maximumWidth)));
            var height = (int) Math.Clamp(
                _resizeWindowOrigin.Height + deltaY,
                minHeight,
                Math.Max(minHeight, Math.Min(MaxHeight, maximumHeight)));
            var targetWidth = Length.Cells(width);
            var targetHeight = Length.Cells(height);
            var targetLeft = Length.Cells(_resizeWindowPosition.X);
            var targetTop = Length.Cells(_resizeWindowPosition.Y);
            eventArgs.IsHandled = true;
            Width = targetWidth;

            if (!CanContinueResize(
                    gestureParent,
                    targetWidth,
                    originalHeight,
                    originalLeft,
                    originalTop,
                    originalRight,
                    originalBottom))
            {
                return;
            }

            Height = targetHeight;

            if (!CanContinueResize(
                    gestureParent,
                    targetWidth,
                    targetHeight,
                    originalLeft,
                    originalTop,
                    originalRight,
                    originalBottom))
            {
                return;
            }

            // Own the origin for the duration of the gesture, exactly as the drag path
            // already does, so the top-left corner stays fixed regardless of the window's
            // alignment or Overlay.Right/Bottom anchoring.
            Overlay.SetLeft(this, targetLeft);

            if (!CanContinueResize(
                    gestureParent,
                    targetWidth,
                    targetHeight,
                    targetLeft,
                    originalTop,
                    originalRight,
                    originalBottom))
            {
                return;
            }

            Overlay.SetTop(this, targetTop);
        }
        else if (_dragging)
        {
            var gestureParent = Parent;
            var deltaX = cells.X - _dragPointerOrigin.X;
            var deltaY = cells.Y - _dragPointerOrigin.Y;
            var clientBounds = Parent?.ContentBounds ?? default;
            var maximumLeft = Math.Max(0, clientBounds.Width - LocalBounds.Width);
            var maximumTop = Math.Max(0, clientBounds.Height - LocalBounds.Height);

            // A move owns resolved geometry, not flexible sizing semantics. Snapshot Auto/Star
            // dimensions before replacing trailing anchors with leading anchors; otherwise the
            // next arrange resolves a different width or height and turns a move into a resize.
            if (Width.Kind is LengthKind.Auto or LengthKind.Star)
            {
                Width = Length.Cells(LocalBounds.Width);

                if (!CanContinueDrag(gestureParent))
                {
                    return;
                }
            }

            if (Height.Kind is LengthKind.Auto or LengthKind.Star)
            {
                Height = Length.Cells(LocalBounds.Height);

                if (!CanContinueDrag(gestureParent))
                {
                    return;
                }
            }

            Overlay.SetRight(this, null);
            Overlay.SetBottom(this, null);
            Overlay.SetLeft(this, Length.Cells(Math.Clamp(_dragWindowOrigin.X + deltaX, 0, maximumLeft)));
            Overlay.SetTop(this, Length.Cells(Math.Clamp(_dragWindowOrigin.Y + deltaY, 0, maximumTop)));
            eventArgs.IsHandled = true;
        }
    }

    [Pure]
    private bool IsTitleBar(Point cells) =>
        cells.Y == Bounds.Y && cells.X >= Bounds.X && cells.X < Bounds.Right;

    [Pure]
    private bool IsResizeCorner(Point cells) =>
        cells.Y == Bounds.Bottom - 1 && cells.X == Bounds.Right - 1;

    [Pure]
    private bool CanContinueResize(
        ControlBase? gestureParent,
        Length expectedWidth,
        Length expectedHeight,
        Length? expectedLeft,
        Length? expectedTop,
        Length? expectedRight,
        Length? expectedBottom) =>
        !IsDisposed &&
        CanResize &&
        _resizing &&
        HasPointerCapture &&
        ReferenceEquals(Parent, gestureParent) &&
        Width == expectedWidth &&
        Height == expectedHeight &&
        Overlay.GetLeft(this) == expectedLeft &&
        Overlay.GetTop(this) == expectedTop &&
        Overlay.GetRight(this) == expectedRight &&
        Overlay.GetBottom(this) == expectedBottom;

    [Pure]
    private bool CanContinueDrag(ControlBase? gestureParent) =>
        !IsDisposed &&
        CanMove &&
        _dragging &&
        HasPointerCapture &&
        ReferenceEquals(Parent, gestureParent);

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

        if (Visibility == Visibility.Visible && !IsSurfacePresented)
        {
            ExceptionDispatchInfo? failure = null;
            ExceptionAggregation.Capture(() => OpenSurface(() => SurfaceBounds = Bounds), ref failure);
            var presentationVersion = SurfacePresentationVersion;

            // Mirrors the Shown notification OnWindowPropertyChanged raises for an explicit
            // Visibility transition: the default-Visible field initializer never runs the
            // property's set block, so this attach path is otherwise the only one that opens
            // the surface without ever raising Shown. Initial focus assignment cannot run
            // synchronously here: ShowModal requires ModalityOwner, which only becomes
            // non-null once OnAttached returns, so an "attach, then ShowModal" call in the
            // same statement block always finishes this method first, before _isShowingModal
            // is ever true during it. Running the fallback synchronously would therefore fire
            // unconditionally - even for a Window about to become modal - and corrupt
            // ModalityManager.Enter's background-focus snapshot, which that same-tick
            // ShowModal takes right after. Posting it instead lets a same-tick ShowModal run
            // first; RunAttachFocusFallback re-checks state on the later tick it actually
            // runs on and backs off if a modal became active (or Visibility/attachment
            // changed) in the meantime.
            if (CanContinueShownPresentation(presentationVersion))
            {
                ExceptionAggregation.Capture(() => Shown?.Invoke(this, EventArgs.Empty), ref failure);
            }

            var dispatcher = Dispatcher;

            if (dispatcher is not null && CanContinueShownPresentation(presentationVersion))
            {
                var attachment = CaptureAttachment();
                PostForCurrentAttachment(attachment, RunAttachFocusFallback);
            }

            failure?.Throw();
        }
    }

    /// <summary>Runs the deferred post-attach focus fallback for a Window that attached while
    /// already <see cref="Visibility.Visible"/> and never received a subsequent modal or
    /// explicit-visibility focus assignment.</summary>
    /// <remarks>
    /// <para>
    /// Posted from <see cref="OnAttached"/> rather than run synchronously there - see the
    /// remark on that call site for why. Re-validates every bit of state this depends on,
    /// since it runs on a later dispatcher tick than the attach that scheduled it: the
    /// Window may have been detached or disposed, hidden again, or entered an active modal
    /// presentation in the meantime.
    /// </para>
    /// <para>
    /// Deliberately unconditional on whether another window was already active: this also
    /// fires for the very first Window attached at application startup, so a cold start with
    /// exactly one Visible window now focuses it instead of leaving focus unset. Nothing in
    /// this codebase's tests or the showcase relies on "nothing focused immediately after the
    /// first attach" - the alternative (special-casing the no-prior-window case to suppress
    /// the fallback) would leave that one scenario as the sole path where an ordinary attach
    /// still doesn't activate its Window, reintroducing a narrower version of the bug this
    /// fallback exists to close.
    /// </para>
    /// </remarks>
    private void RunAttachFocusFallback()
    {
        if (Visibility != Visibility.Visible ||
            HasActiveSurfaceModal)
        {
            return;
        }

        ApplyVisibleFocusFallback();
    }

    private void OnWindowPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.PropertyName != nameof(Visibility) ||
            Visibility != Visibility.Visible)
        {
            return;
        }

        BeginSurfaceOpenLifetime();

        ExceptionDispatchInfo? failure = null;

        if (Dispatcher is null)
        {
            ExceptionAggregation.Capture(() => Shown?.Invoke(this, EventArgs.Empty), ref failure);
            failure?.Throw();
            return;
        }

        if (!IsSurfacePresented)
        {
            ExceptionAggregation.Capture(() => OpenSurface(() => SurfaceBounds = Bounds), ref failure);
        }

        var presentationVersion = SurfacePresentationVersion;

        if (CanContinueShownPresentation(presentationVersion))
        {
            ExceptionAggregation.Capture(() => Shown?.Invoke(this, EventArgs.Empty), ref failure);
        }

        if (!_isShowingModal && CanContinueShownPresentation(presentationVersion))
        {
            ExceptionAggregation.Capture(ApplyVisibleFocusFallback, ref failure);
        }

        failure?.Throw();
    }

    [Pure]
    private bool CanContinueShownPresentation(long presentationVersion) =>
        !IsDisposed &&
        Dispatcher is not null &&
        Visibility == Visibility.Visible &&
        IsSurfacePresented &&
        SurfacePresentationVersion == presentationVersion;

    /// <summary>Focuses the first eligible focusable descendant, or this Window itself when none
    /// is eligible.</summary>
    /// <remarks>Shared by <see cref="OnWindowPropertyChanged"/>'s synchronous explicit-visibility
    /// fallback and <see cref="RunAttachFocusFallback"/>'s deferred post-attach fallback, which
    /// differ only in when they run and what state they re-validate first.</remarks>
    private void ApplyVisibleFocusFallback()
    {
        var target = InitialFocusResolver.FindFirstEligibleFocusTarget(
            this,
            includeRoot: true,
            ModalityOwner);
        _ = target is not null && FocusOwner?.Focus(target) == true;
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

    [Pure]
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
