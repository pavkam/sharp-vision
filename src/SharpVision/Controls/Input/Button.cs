// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using Text;

using DisplayText = Display.Text;

/// <summary>Defines a focusable command control with one optional owned content child.</summary>
[PublicAPI]
public sealed partial class Button: Pressable
{
    private static readonly StyleContract<ButtonStyle> _styleContract = new(
        ThemeRole.Input,
        static profile => new ButtonStyle(ButtonStyle.Standard.Padding, profile),
        static (previous, _, current, _) =>
            previous.Padding != current.Padding
                ? InvalidationImpact.Measure
                : PressedTranslationChanged(previous.Appearance, current.Appearance)
                    ? InvalidationImpact.Arrange
                    : InvalidationImpact.None,
        static style => style.Appearance);
    private ButtonStyle? _actualStyleCache;
    private ButtonStyle? _actualStyleCacheKey;
    private Theme? _actualStyleCacheTheme;

    #region Construction and command properties

    /// <summary>Initializes an empty focusable Button that inherits its presentation from the active Theme.</summary>
    public Button()
    {
    }

    /// <summary>Initializes a focusable Button with the specified text content.</summary>
    /// <param name="text">The non-null text content.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    public Button(string text) : this()
    {
        ArgumentNullException.ThrowIfNull(text);
        Content = new DisplayText(text);
    }

    /// <summary>Gets or sets the complete local presentation, or null to use the semantic input profile.</summary>
    /// <exception cref="InvalidOperationException">The attached Button is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The Button is disposed.</exception>
    public ButtonStyle? Style
    {
        get;
        set => _ = SetControlStyle(
            ref field,
            value,
            _styleContract.Resolve,
            _styleContract.CompareStructure,
            _styleContract.Appearance,
            nameof(Style),
            nameof(ActualStyle));
    }

    /// <summary>Gets the complete local presentation or the library structure completed with the semantic input profile.</summary>
    /// <remarks>
    /// Memoized against the exact (<see cref="Style"/>, <see cref="Theme"/>) pair that produced it:
    /// resolving the default style re-validates all 512 <see cref="VisualState"/> combinations
    /// against the shadow/border invariant (see #179), and this property is read from multiple
    /// per-frame paths.
    /// </remarks>
    public ButtonStyle ActualStyle =>
        ResolveContractStyle(
            _styleContract,
            ref _actualStyleCache,
            ref _actualStyleCacheKey,
            ref _actualStyleCacheTheme,
            Style,
            Theme);

    /// <inheritdoc/>
    protected override ThemeRole ThemeRole => _styleContract.Role;

    /// <inheritdoc/>
    protected override ThemeProfile AppearanceProfile => ActualStyle.Appearance;

    /// <inheritdoc/>
    protected override ThemeProfile GetAppearanceProfile(Theme? theme) =>
        GetContractAppearanceProfile(_styleContract, Style, theme);

    /// <inheritdoc/>
    protected override InvalidationImpact GetThemeChangeImpact(
        Theme? previous,
        Theme? current,
        Face? previousParentAmbientFace,
        Face? currentParentAmbientFace) =>
        GetContractThemeChangeImpact(
            _styleContract,
            Style,
            previous,
            current,
            previousParentAmbientFace,
            currentParentAmbientFace);

    /// <inheritdoc/>
    protected override string? GetThemeResolvedStylePropertyName(Theme? previous, Theme? current) =>
        GetContractResolvedStylePropertyName(_styleContract, Style, previous, current, nameof(ActualStyle));

    /// <inheritdoc/>
    internal override InvalidationImpact GetAppearanceChangeImpact(
        ResolvedAppearance previous,
        ResolvedAppearance current) =>
        previous.Border.Sides != current.Border.Sides
            ? InvalidationImpact.Measure
            : ResolvePressedTranslation(previous.Shadow) != ResolvePressedTranslation(current.Shadow)
                ? InvalidationImpact.Arrange
                : previous.Face != current.Face ||
                  previous.Border != current.Border ||
                  previous.Shadow != current.Shadow
            ? InvalidationImpact.Render
            : InvalidationImpact.None;

    /// <summary>Raised after released state commits and before command execution.</summary>
    public event EventHandler<ActivationEventArgs>? Click;

    /// <summary>Gets or sets whether an owning Window treats Enter as a fallback activation.</summary>
    /// <exception cref="InvalidOperationException">The attached Button is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The Button is disposed.</exception>
    public bool IsDefault
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.None);
    }

    /// <summary>Gets or sets whether an owning Window treats Escape as a fallback activation.</summary>
    /// <exception cref="InvalidOperationException">The attached Button is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The Button is disposed.</exception>
    public bool IsCancel
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.None);
    }

    /// <summary>Gets or sets the horizontal placement of retained <see cref="Display.Text"/> content inside the button face.</summary>
    /// <remarks>Non-text content retains its own ordinary layout behavior.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached Button is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The Button is disposed.</exception>
    public Alignment TextAlignment
    {
        get;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The button text alignment is unknown.");
            }

            _ = SetProperty(ref field, value, InvalidationImpact.Arrange);
        }
    } = Alignment.Center;

    #endregion

    #region Activation and lifecycle

    /// <summary>Activates an available executable Button through its public API.</summary>
    /// <exception cref="InvalidOperationException">The attached Button is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The Button is disposed.</exception>
    public void PerformClick()
    {
        VerifyMutable();

        if (EffectiveIsEnabled && EffectiveIsVisible)
        {
            Activate(ActivationCause.Programmatic);
        }
    }

    /// <inheritdoc/>
    protected override void Activate(ActivationCause cause)
    {
        var command = Command;
        var parameter = CommandParameter;

        if (command is not null && !command.CanExecute(parameter))
        {
            return;
        }

        var eventArgs = new ActivationEventArgs(cause);
        Click?.Invoke(this, eventArgs);
        command?.Execute(parameter);
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        var content = Content;
        var padding = ActualStyle.Padding;

        if (content is null || content.Visibility == Visibility.Collapsed)
        {
            return new Size(padding.Horizontal, padding.Vertical);
        }

        var desired = MeasureChild(
            content,
            new Constraint(
                DeflateConstraint(constraint.Width, padding.Horizontal),
                DeflateConstraint(constraint.Height, padding.Vertical)));

        return new Size(
            desired.Width.Add(content.Margin.Horizontal).Add(padding.Horizontal),
            desired.Height.Add(content.Margin.Vertical).Add(padding.Vertical));
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        if (Content is { } content)
        {
            ArrangeContent(content, bounds);
        }
    }

    /// <inheritdoc/>
    protected override Rect VisualBounds
    {
        get
        {
            var shadow = ActualShadow;
            return IsPressed && shadow.IsVisible
                ? FaceBounds
                : ControlChrome.ExpandVisualBounds(Bounds, shadow.IsVisible, shadow.Mode, shadow.Offset);
        }
    }

    /// <inheritdoc/>
    internal override Rect DescendantRenderBounds => FaceBounds;

    /// <inheritdoc/>
    protected override ChromeRenderOptions GetChromeRenderOptions() => new()
    {
        BodyBounds = FaceBounds,
        ShadowExcludeBounds = FaceBounds,
        PreserveButtonShadowGap = true,
        ClearBodyWhenPressedWithShadow = true,
        SkipShadow = IsPressed
    };

    /// <inheritdoc/>
    protected override void OnPressedChanged(bool pressed)
    {
        base.OnPressedChanged(pressed);

        if (!UsesWholeCellPressedTranslation || Content is not { } content)
        {
            return;
        }

        // Pointer and keyboard press state must be drawable before a later
        // layout drain, so keep owned content in the same translated face box.
        ArrangeContent(content, ContentBounds);
        Invalidate(Invalidation.Arrange);
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);

        if (reason == ReleaseReason.Disposed)
        {
            Click = null;
        }
    }

    #endregion

    #region Layout and rendering

    private bool UsesWholeCellPressedTranslation => ActualShadow.IsVisible;

    private void ArrangeContent(Control content, Rect bounds)
    {
        var face = ActualStyle.Padding.Deflate(FaceContentBounds(bounds));

        if (content is not DisplayText)
        {
            ArrangeChild(content, face, ResolvedAxes.Both);
            return;
        }

        var width = Math.Min(face.Width, content.DesiredSize.Width.Add(content.Margin.Horizontal));
        var x = TextAlignment switch
        {
            Alignment.Start => face.X,
            Alignment.Center => face.X + ((face.Width - width) / 2),
            Alignment.End => face.Right - width,
            _ => throw new UnreachableException()
        };
        ArrangeChild(
            content,
            new Rect(x, face.Y, width, face.Height),
            ResolvedAxes.Both);
    }

    private Point PressedTranslation
    {
        get
        {
            var shadow = ActualShadow;
            return shadow.Mode == ShadowMode.FractionalBlock
                ? new Point(shadow.Offset.X, 0)
                : shadow.Offset;
        }
    }

    private Rect FaceBounds =>
        IsPressed && UsesWholeCellPressedTranslation
            ? ControlChrome.Shift(Bounds, PressedTranslation)
            : Bounds;

    private Rect FaceContentBounds(Rect bounds) =>
        IsPressed && UsesWholeCellPressedTranslation
            ? ControlChrome.Shift(bounds, PressedTranslation)
            : bounds;

    private static bool PressedTranslationChanged(ThemeProfile previous, ThemeProfile current)
    {
        var combinations = 1 << VisualStateOrder.OrderedOverlays.Length;
        for (var flags = 0; flags < combinations; flags++)
        {
            var state = VisualState.Normal;
            for (var index = 0; index < VisualStateOrder.OrderedOverlays.Length; index++)
            {
                if ((flags & (1 << index)) != 0)
                {
                    state |= VisualStateOrder.OrderedOverlays[index];
                }
            }

            var previousShadow = previous.Resolve(state).Shadow;
            var currentShadow = current.Resolve(state).Shadow;
            if (ResolvePressedTranslation(previousShadow) != ResolvePressedTranslation(currentShadow))
            {
                return true;
            }
        }

        return false;
    }

    private static Point ResolvePressedTranslation(Shadow shadow) => !shadow.IsVisible
        ? default
        : shadow.Mode == ShadowMode.FractionalBlock
            ? new Point(shadow.Offset.X, 0)
            : shadow.Offset;

    private static int? DeflateConstraint(int? value, int inset) =>
        value.HasValue ? Math.Max(0, value.Value - inset) : null;

    #endregion
}
