// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using Text;

using DisplayText = Display.Text;

/// <summary>Defines a focusable command control with one optional owned content child.</summary>
[PublicAPI]
public sealed partial class Button: Pressable<ButtonStyle>
{
    #region Construction and command properties

    /// <summary>Initializes an empty focusable Button that inherits its presentation from the active Theme.</summary>
    public Button() : base(ButtonStyle.Definition)
    {
    }

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
        var content = TextControl;
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
            return Pressed && shadow.Visible
                ? FaceBounds
                : Bounds.ExpandVisualBounds(shadow.Visible, shadow.Mode, shadow.Offset);
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
        SkipShadow = Pressed
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

    #endregion

    #region Layout and rendering

    private bool UsesWholeCellPressedTranslation => ActualShadow.Visible;

    private void ArrangeContent(DisplayText content, Rect bounds)
    {
        var face = ActualStyle.Padding.Deflate(FaceContentBounds(bounds));
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
        Pressed && UsesWholeCellPressedTranslation
            ? Bounds.Shift(PressedTranslation)
            : Bounds;

    private Rect FaceContentBounds(Rect bounds) =>
        Pressed && UsesWholeCellPressedTranslation
            ? bounds.Shift(PressedTranslation)
            : bounds;

    private static Point ResolvePressedTranslation(Shadow shadow) => !shadow.Visible
        ? default
        : shadow.Mode == ShadowMode.FractionalBlock
            ? new Point(shadow.Offset.X, 0)
            : shadow.Offset;

    private static int? DeflateConstraint(int? value, int inset) =>
        value.HasValue ? Math.Max(0, value.Value - inset) : null;

    #endregion
}
