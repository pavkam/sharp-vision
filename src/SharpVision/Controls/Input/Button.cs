// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using Text;

using DisplayText = Display.Text;

/// <summary>Defines a focusable command control with one optional owned content child.</summary>
[PublicAPI]
public sealed class Button: InputBase, IStyled<ButtonStyle>
{
    #region Construction and command properties

    private readonly StyleSlot<ButtonStyle> _style;

    /// <summary>Initializes an empty focusable Button that inherits its presentation from the active Theme
    /// and centers its desired chrome vertically by default.</summary>
    public Button()
    {
        EnablePressActivation();
        EnableCaption();
        EnableCommand();
        _style = InitializeStyle(ButtonStyle.Definition);
        VerticalAlignment = VerticalAlignment.Center;
    }

    /// <summary>Gets or sets the complete local presentation, or null for theme ownership.</summary>
    /// <exception cref="InvalidOperationException">The attached Button is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The Button is disposed.</exception>
    public ButtonStyle? Style
    {
        get => _style.Local;
        set => _style.Local = value;
    }

    /// <summary>Gets the complete local, theme-owned, or code-owned presentation.</summary>
    public ButtonStyle ActualStyle => _style.Actual;

    /// <summary>Initializes a focusable Button with the specified text content.</summary>
    /// <param name="text">The non-null text content.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    public Button(string text) : this()
    {
        ArgumentNullException.ThrowIfNull(text);
        Text = text;
    }

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
            ArgumentOutOfRangeException.ThrowIfNotDefined(value, nameof(value), "The button text alignment is unknown.");

            _ = SetProperty(ref field, value, InvalidationImpact.Arrange);
        }
    } = Alignment.Center;

    #endregion

    #region Activation and lifecycle

    /// <summary>Activates an available executable Button through its public API.</summary>
    /// <exception cref="InvalidOperationException">The attached Button is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The Button is disposed.</exception>
    public void PerformClick() => _ = TryActivate(ActivationCause.Programmatic);

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
    /// <remarks>A bound command that cannot execute suppresses <see cref="Click"/>, so the face
    /// must not answer hover or a press as if an activation could follow: the button presents its
    /// disabled appearance instead, and the command's <c>CanExecuteChanged</c> repaints it. Only
    /// the appearance follows the command; focus, traversal, and hit testing stay those of an
    /// enabled control.</remarks>
    internal override VisualState GetAppearanceState()
    {
        var state = base.GetAppearanceState();

        return IsCommandExecutable
            ? state
            : (state & ~(VisualState.IsPointerOver | VisualState.Pressed)) | VisualState.Disabled;
    }

    private bool IsCommandExecutable => Command is not { } command || command.CanExecute(CommandParameter);

    // The shadowed face shifts while pressed; a press that cannot activate shows no shift either.
    private bool IsFacePressed => IsPressed && IsCommandExecutable;

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        var content = TextControl;
        var padding = ActualStyle.Padding;
        var affixes = MeasureAffixes(StartAffix, EndAffix, ActualStyle.AffixGap);
        var affixInset = affixes.StartCells + affixes.EndCells;

        if (content is null || content.Visibility == Visibility.Collapsed)
        {
            return new Size(padding.Horizontal + affixInset, padding.Vertical);
        }

        var desired = MeasureChild(
            content,
            new Constraint(
                DeflateConstraint(constraint.Width, padding.Horizontal + affixInset),
                DeflateConstraint(constraint.Height, padding.Vertical)));

        return new Size(
            desired.Width.Add(content.Margin.Horizontal).Add(padding.Horizontal).Add(affixInset),
            desired.Height.Add(content.Margin.Vertical).Add(padding.Vertical));
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        if (TextControl is { } content)
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
            return IsFacePressed && shadow.IsVisible
                ? FaceBounds
                : Bounds.ExpandVisualBounds(shadow.IsVisible, shadow.Mode, shadow.Offset);
        }
    }

    /// <inheritdoc/>
    internal override Rect DescendantRenderBounds => FaceBounds;

    /// <inheritdoc/>
    /// <remarks>
    /// While pressed with a whole-cell shadow visible, the drawn face is <see cref="FaceBounds"/>
    /// - translated away from <see cref="ControlBase.Bounds"/> - so pointer press/drag/release
    /// must be evaluated against that same rectangle; otherwise a release on the visibly-lit face
    /// can land outside the interaction rectangle and silently fail to activate.
    /// </remarks>
    protected override Rect InteractionBounds => FaceBounds;

    /// <inheritdoc/>
    protected override ChromeRenderOptions GetChromeRenderOptions() => new()
    {
        BodyBounds = FaceBounds,
        ShadowExcludeBounds = FaceBounds,
        PreserveButtonShadowGap = true,
        ClearBodyWhenPressedWithShadow = true,
        SkipShadow = IsFacePressed
    };

    /// <inheritdoc/>
    protected override void OnPressedChanged(bool pressed)
    {
        base.OnPressedChanged(pressed);

        if (!UsesWholeCellPressedTranslation || TextControl is not { } content)
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

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        base.OnEvent(eventArgs);
        HandlePressActivation(eventArgs);
    }

    #endregion

    #region Layout and rendering

    /// <summary>Gets or sets the optional leading edge-pinned decoration, reserved inside the face
    /// and outside the caption's own alignment box.</summary>
    /// <exception cref="InvalidOperationException">The attached Button is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The Button is disposed.</exception>
    public Affix? StartAffix
    {
        get;
        set => _ = SetProperty(ref field, value, GetAffixChangeImpact(field, value));
    }

    /// <summary>Gets or sets the optional trailing edge-pinned decoration, reserved inside the face
    /// and outside the caption's own alignment box.</summary>
    /// <exception cref="InvalidOperationException">The attached Button is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The Button is disposed.</exception>
    public Affix? EndAffix
    {
        get;
        set => _ = SetProperty(ref field, value, GetAffixChangeImpact(field, value));
    }

    private bool UsesWholeCellPressedTranslation => ActualShadow.IsVisible;

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        base.OnRenderContent(canvas);

        // ContentBounds - not the outer Bounds - already excludes the border and this control's own
        // generic Padding, exactly matching the untranslated box ArrangeOverride receives; only the
        // style's own Padding and any live press translation remain to apply, the same two steps
        // ArrangeContent takes below.
        var affixes = MeasureAffixes(StartAffix, EndAffix, ActualStyle.AffixGap);
        var face = ActualStyle.Padding.Deflate(FaceContentBounds(ContentBounds));
        RenderAffixes(canvas, face, affixes, StartAffix, EndAffix, ResolvedStyle);
    }

    private void ArrangeContent(DisplayText content, Rect bounds)
    {
        var affixes = MeasureAffixes(StartAffix, EndAffix, ActualStyle.AffixGap);
        var face = DeflateForAffixes(ActualStyle.Padding.Deflate(FaceContentBounds(bounds)), affixes);
        var width = Math.Min(face.Width, content.DesiredSize.Width.Add(content.Margin.Horizontal));
        var x = TextAlignment switch
        {
            Alignment.Start => face.X,
            Alignment.Center => face.X.SaturatingAdd((face.Width - width) / 2),
            Alignment.End => face.Right.SaturatingSubtract(width),
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
        IsFacePressed && UsesWholeCellPressedTranslation
            ? Bounds.Shift(PressedTranslation)
            : Bounds;

    private Rect FaceContentBounds(Rect bounds) =>
        IsFacePressed && UsesWholeCellPressedTranslation
            ? bounds.Shift(PressedTranslation)
            : bounds;

    [Pure]
    private static Point ResolvePressedTranslation(Shadow shadow) => !shadow.IsVisible
        ? default
        : shadow.Mode == ShadowMode.FractionalBlock
            ? new Point(shadow.Offset.X, 0)
            : shadow.Offset;

    [Pure]
    private static int? DeflateConstraint(int? value, int inset) =>
        value.HasValue ? Math.Max(0, value.Value - inset) : null;

    #endregion
}
