// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Collections;

using SharpVision.Terminal.Input;

/// <summary>Owns one realized ListView template control and its selection behavior.</summary>
/// <remarks>
/// Composes <see cref="PressBehavior"/> directly instead of inheriting <see cref="PressableBase"/> so
/// it can keep arbitrary owned <see cref="ContentControl.Content"/> for realized template output,
/// which <see cref="PressableBase"/> no longer supports now that it converges on a single text
/// caption.
/// </remarks>
internal sealed class ListItem: ContentControl
{
    private readonly PressBehavior _interaction;

    /// <summary>Initializes one indexed detached realized control.</summary>
    /// <param name="index">The non-negative stable item index.</param>
    /// <param name="content">The non-null detached template control.</param>
    public ListItem(int index, ControlBase content)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentNullException.ThrowIfNull(content);
        _interaction = new PressBehavior(
            () => Bounds,
            () => EffectiveIsEnabled && EffectiveIsVisible,
            () => FocusOwner is null || Focused,
            RequestFocus,
            CapturePointer,
            () => HasPointerCapture,
            ReleasePointerCapture,
            SetPressed,
            Activate,
            () => Capabilities.KittyKeyboard.Authoritative);
        HorizontalAlignment = HorizontalAlignment.Stretch;
        Focusable = false;
        TabStop = false;
        Index = index;
        Content = content;
    }

    /// <summary>Raised after eligible Space, Enter, or primary pointer activation.</summary>
    public event EventHandler<ActivationEventArgs>? Activated;

    /// <summary>Gets or sets the item index within the owning ListView.</summary>
    public int Index { get; set; }

    /// <summary>Gets whether this item is committed selected.</summary>
    public bool Selected { get; private set; }

    /// <summary>Gets modifiers captured from the activation transition.</summary>
    public Modifiers LastModifiers { get; private set; }

    /// <summary>Gets the activating key, or null for pointer input.</summary>
    public Code? LastKey { get; private set; }

    /// <summary>Gets whether content is effectively available for navigation and activation.</summary>
    public bool Available => Content is { EffectiveIsEnabled: true, EffectiveIsVisible: true };

    /// <summary>Commits selected visual state after the owning transaction.</summary>
    /// <param name="value">The committed selected flag.</param>
    public void CommitSelection(bool value) =>
        Commit(value);

    /// <summary>Invokes this available item on behalf of its owning selector.</summary>
    /// <param name="cause">The semantic activation source.</param>
    /// <param name="key">The activating key, or null when activation is not key driven.</param>
    /// <param name="modifiers">The modifiers captured with <paramref name="key"/>.</param>
    public void ActivateFromOwner(ActivationCause cause, Code? key, Modifiers modifiers)
    {
        LastKey = key;
        LastModifiers = modifiers;
        Activate(cause);
    }

    /// <inheritdoc/>
    internal override ControlBase? HitTest(Point point) =>
        !Disposed && HitTestVisible && EffectiveIsVisible && EffectiveIsEnabled &&
        Bounds.Contains(point)
            ? this
            : null;

    /// <inheritdoc/>
    internal override VisualState AmbientAppearanceState => GetAppearanceState();

    /// <inheritdoc/>
    internal override bool StateAffectsAmbientAppearance => true;

    private void Activate(ActivationCause cause)
    {
        if (Available)
        {
            Activated?.Invoke(this, new ActivationEventArgs(cause));
        }
    }

    /// <inheritdoc/>
    protected override void OnFocusChanged(bool focused)
    {
        base.OnFocusChanged(focused);
        _interaction.FocusChanged(focused);

        if (focused)
        {
            var list = FindList();
            Debug.Assert(list is not null, "A focused ListItem belongs to a ListView.");
            list.NotifyItemFocused(this);
        }
    }

    /// <inheritdoc/>
    protected override void OnLostPointerCapture(PointerCaptureLossReason reason)
    {
        base.OnLostPointerCapture(reason);
        _interaction.CaptureLost();
    }

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        if (eventArgs is KeyEventArgs { Stroke.Action: KeyAction.Press } key)
        {
            LastModifiers = key.Stroke.Modifiers;
            LastKey = key.Stroke.Code;
        }
        else if (eventArgs is PointerEventArgs { Pointer.Action: PointerAction.Press } pointer)
        {
            LastModifiers = pointer.Pointer.Modifiers;
            LastKey = null;
        }

        base.OnEvent(eventArgs);
        _interaction.Handle(eventArgs);
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        var content = Content;
        Debug.Assert(content is not null, "A realized ListItem always owns template content.");
        var desired = MeasureChild(content, constraint);

        return content.Visibility == Visibility.Collapsed
            ? default
            : new Size(
                desired.Width.Add(content.Margin.Horizontal),
                desired.Height.Add(content.Margin.Vertical));
    }

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        if (Bounds.Width == 0 || Bounds.Height == 0 || !this.HasOpaqueFill(GetAppearanceState()))
        {
            return;
        }

        canvas.Clear(Bounds, ResolvedStyle);
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        var content = Content;
        Debug.Assert(content is not null, "A realized ListItem always owns template content.");
        ArrangeChild(content, bounds, ResolvedAxes.Width);
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);
        _interaction.Unavailable();

        if (reason == ReleaseReason.Disposed)
        {
            Activated = null;
        }
    }

    private ListView? FindList()
    {
        var list = FindAncestor<ListView>();
        Debug.Assert(list is not null, "A ListItem always has a ListView ancestor while attached.");
        return list;
    }

    private void Commit(bool value)
    {
        VerifyMutable();

        if (Selected == value)
        {
            return;
        }

        Selected = value;
        SetSelectedState(value);
        NotifyPropertyChanged(nameof(Selected), InvalidationImpact.None);
    }
}
