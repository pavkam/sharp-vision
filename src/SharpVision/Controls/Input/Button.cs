// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using System.Runtime.ExceptionServices;

using SharpVision.Windows;

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

        // Only this button's own availability can move the owning Window's resolved default
        // button (Window.ResolveDefaultButton only ever considers IsDefault candidates), so a
        // sibling IsDefault button may need to repaint its Current cue when this one becomes or
        // stops being eligible - a fact it cannot detect from its own unchanged state.
        EnabledChanged += OnDefaultCandidateAvailabilityChanged;
        VisibilityChanged += OnDefaultCandidateAvailabilityChanged;
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
    protected internal override InvalidationImpact GetAppearanceChangeImpact(
        ResolvedAppearance previous,
        ResolvedAppearance current) =>
        previous.Border.Sides != current.Border.Sides
            ? InvalidationImpact.Measure
            : ResolvePressedTranslation(previous.Shadow) != ResolvePressedTranslation(current.Shadow)
                ? InvalidationImpact.Arrange
                : previous.Face != current.Face ||
                  previous.Border != current.Border ||
                  previous.BorderStyles != current.BorderStyles ||
                  previous.Shadow != current.Shadow
            ? InvalidationImpact.Render
            : InvalidationImpact.None;

    /// <summary>Raised after released state commits and before command execution.</summary>
    public event EventHandler<ActivationEventArgs>? Click;

    /// <summary>Gets or sets whether an owning Window treats Enter as a fallback activation.</summary>
    /// <remarks>
    /// The effective default button - the one <see cref="Window.ResolveDefaultButton"/>
    /// resolves, the first enabled and visible <see cref="IsDefault"/> descendant in ownership
    /// order - is also presented as the <see cref="VisualState.Current"/> item of its window, so a
    /// theme may distinguish it through <c>styles.button.current</c> (Turbo Vision's bright-cyan
    /// default-button caption, for example). Several descendants may keep this flag at once, but
    /// only the one Enter would currently activate paints as current; a theme that authors nothing
    /// there shows no difference, exactly as before.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The attached Button is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The Button is disposed.</exception>
    public bool IsDefault
    {
        get;
        set
        {
            // The default flag folds into the appearance state (Current), so the resolved
            // appearance caches - this button's and its ambient caption's - must be cleared the
            // same way any other state fact clears them, not merely repainted from stale caches.
            if (SetProperty(ref field, value, InvalidationImpact.None))
            {
                InvalidateVisualState();

                // This button's own cache is cleared above; a sibling IsDefault descendant whose
                // own facts did not change may still need to move its Current cue now that the
                // window's resolution has a new or one fewer candidate to consider.
                FindAncestor<Window>()?.InvalidateDefaultButtonCues();
            }
        }
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

    /// <summary>Activates an available executable Button through an owning framework interaction.</summary>
    /// <param name="cause">The validated input route that initiated the activation.</param>
    /// <remarks>
    /// This seam lets retained owners such as Window preserve the real input identity when they
    /// invoke a fallback Button on behalf of an unhandled routed key.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cause"/> is undefined.</exception>
    /// <exception cref="InvalidOperationException">The attached Button is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The Button is disposed.</exception>
    internal void PerformClick(ActivationCause cause) => _ = TryActivate(cause);

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
        ExceptionDispatchInfo? failure = null;
        CaptureFailure(() => Click?.Invoke(this, eventArgs), ref failure);
        CaptureFailure(() => command?.Execute(parameter), ref failure);
        failure?.Throw();
    }

    /// <inheritdoc/>
    /// <remarks>A bound command that cannot execute suppresses <see cref="Click"/>, so the face
    /// must not answer hover or a press as if an activation could follow: the button presents its
    /// disabled appearance instead, and the command's <c>CanExecuteChanged</c> repaints it. Only
    /// the appearance follows the command; focus, traversal, and hit testing stay those of an
    /// enabled control.</remarks>
    protected internal override VisualState GetAppearanceState()
    {
        var state = base.GetAppearanceState();

        if (IsDefault && IsEffectiveDefaultButton)
        {
            state |= VisualState.Current;
        }

        return IsCommandExecutable
            ? state
            : (state & ~(VisualState.IsPointerOver | VisualState.Pressed)) | VisualState.Disabled;
    }

    /// <summary>Gets whether this button is the one an owning Window's Enter key currently
    /// targets - or, absent an owning Window, whether it is simply available - so
    /// <see cref="GetAppearanceState"/> presents <see cref="VisualState.Current"/> on at most one
    /// of several <see cref="IsDefault"/> siblings, matching <see cref="Window.OnEvent"/>'s
    /// own key-time resolution exactly rather than duplicating or drifting from it.</summary>
    private bool IsEffectiveDefaultButton =>
        FindAncestor<Window>() is { } window
            ? window.IsEffectiveDefault(this)
            : EffectiveIsEnabled && EffectiveIsVisible;

    /// <summary>Repaints this button's <see cref="VisualState.Current"/> cue after the owning
    /// <see cref="Window"/>'s default-button resolution moves to or away from this
    /// instance.</summary>
    /// <remarks>
    /// The internal seam <see cref="Window.InvalidateDefaultButtonCues()"/> needs: this
    /// button's own facts may be unchanged, so nothing else already invalidates its cache, and
    /// <see cref="ControlBase.InvalidateVisualState"/> stays protected because Window does not
    /// derive from Button.
    /// </remarks>
    internal void InvalidateDefaultButtonCue() => InvalidateVisualState();

    private void OnDefaultCandidateAvailabilityChanged(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;

        // A non-default button's own availability never enters Window.ResolveDefaultButton's
        // predicate, so only a candidate that carries the flag can move the resolution.
        if (IsDefault)
        {
            FindAncestor<Window>()?.InvalidateDefaultButtonCues();
        }
    }

    /// <inheritdoc/>
    protected override void OnParentChanged(ControlBase? previous, ControlBase? current)
    {
        base.OnParentChanged(previous, current);

        if (!IsDefault)
        {
            return;
        }

        // Reparenting can add this candidate to one window's resolution and remove it from
        // another's, so both ownership chains - the one being left and the one being joined -
        // need their default-button cues re-evaluated.
        FindWindowAncestor(previous)?.InvalidateDefaultButtonCues();
        FindWindowAncestor(current)?.InvalidateDefaultButtonCues();
    }

    private static Window? FindWindowAncestor(ControlBase? control)
    {
        for (var candidate = control; candidate is not null; candidate = candidate.Parent)
        {
            if (candidate is Window window)
            {
                return window;
            }
        }

        return null;
    }

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
    protected internal override Rect DescendantRenderBounds => FaceBounds;

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
