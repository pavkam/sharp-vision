// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Collections;

using SharpVision.Terminal.Input;

using NonNegativeValue = JetBrains.Annotations.NonNegativeValueAttribute;

/// <summary>Owns one realized ListView template control and its selection behavior.</summary>
/// <remarks>
/// Composes <see cref="PressBehavior"/> directly instead of deriving <see cref="InputBase"/> and
/// opting into its caption capability, so it can keep arbitrary owned
/// <see cref="ContentControl.Content"/> for realized template output, which the single-text-caption
/// capability does not support.
/// </remarks>
internal sealed class ListItem: ContentControl, IOwnedChildDisposalObserver
{
    private readonly PressBehavior _interaction;
    private int _pointerClickCount;
    private Modifiers _pointerModifiers;

    /// <summary>Initializes one indexed detached realized control.</summary>
    /// <param name="index">The non-negative stable item index.</param>
    /// <param name="content">The non-null detached template control.</param>
    public ListItem([NonNegativeValue] int index, ControlBase content)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentNullException.ThrowIfNull(content);
        _interaction = new PressBehavior(
            () => Bounds,
            () => !IsDisposed && EffectiveIsEnabled && EffectiveIsVisible,
            () => FocusOwner is null || IsFocused,
            RequestFocus,
            CapturePointer,
            () => HasPointerCapture,
            ReleasePointerCapture,
            SetPressed,
            Activate,
            () => Capabilities.KeyReleaseEvents.Authoritative);
        RegisterLifecycleParticipant(_interaction);
        HorizontalAlignment = HorizontalAlignment.Stretch;
        IsFocusable = false;
        IsTabStop = false;
        Index = index;
        Content = content;

    }

    /// <summary>Raised after eligible Space, Enter, or primary pointer activation.</summary>
    public event EventHandler<ActivationEventArgs>? Activated;

    /// <summary>Raised after the owned content's own Visibility changes, which may flip
    /// <see cref="IsAvailable"/>.</summary>
    public event EventHandler? AvailabilityChanged;

    /// <summary>Gets or sets the item index within the owning ListView.</summary>
    [NonNegativeValue]
    public int Index { get; set; }

    /// <summary>Gets whether this item is committed selected.</summary>
    public bool IsSelected { get; private set; }

    /// <summary>Gets modifiers captured from the activation transition.</summary>
    public Modifiers LastModifiers { get; private set; }

    /// <summary>Gets the activating key, or null for pointer input.</summary>
    public Code? LastKey { get; private set; }

    /// <summary>Gets the consecutive same-target press count captured from the activation
    /// transition, or zero for key-driven activation.</summary>
    [NonNegativeValue]
    internal int LastClickCount { get; private set; }

    /// <summary>Gets whether content is effectively available for navigation and activation.</summary>
    public bool IsAvailable => Content is { EffectiveIsEnabled: true, EffectiveIsVisible: true };

    /// <summary>Gets whether content is locally available while an owner deliberately keeps this
    /// retained list hidden behind a collapsed popup.</summary>
    internal bool IsLocallyAvailable =>
        Content is { IsEnabled: true, Visibility: Visibility.Visible };

    /// <inheritdoc/>
    void IOwnedChildDisposalObserver.OnOwnedChildDisposalRequested(ControlBase child) =>
        FindList()?.OnItemContentDisposalRequested(this, child);

    /// <summary>Gets whether owned content is directly collapsed, mirroring the
    /// <see cref="MeasureOverride"/> zero-size contract without folding in ancestor or enabled
    /// state the way <see cref="IsAvailable"/> does.</summary>
    internal bool IsContentCollapsed => Content?.Visibility == Visibility.Collapsed;

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
        !IsDisposed && IsHitTestVisible && EffectiveIsVisible && EffectiveIsEnabled &&
        Bounds.Contains(point)
            ? this
            : null;

    /// <inheritdoc/>
    internal override VisualState AmbientAppearanceState => GetAppearanceState();

    /// <inheritdoc/>
    internal override bool StateAffectsAmbientAppearance => true;

    /// <inheritdoc/>
    protected override AppearanceStates GetDefaultAppearanceStates(Theme? theme)
    {
        var resolvedTheme = theme ?? ThemeCatalog.Dark;
        var style = FindAncestor<ListView>()?.ActualStyle ??
            ListViewStyle.Definition.Resolve(null, resolvedTheme);
        var overlay = new AppearanceStatesOverlay(
            current: new AppearanceOverlay(
                face: new FaceOverlay(underline: style.CurrentUnderline)),
            selected: new AppearanceOverlay(
                face: new FaceOverlay(
                    foreground: style.SelectedTextColor,
                    background: style.SelectedBackground)));
        return resolvedTheme.GetInteractiveRowStyleSet().ToAppearanceStates().Compose(overlay);
    }

    private void Activate(ActivationCause cause)
    {
        if (cause == ActivationCause.Pointer)
        {
            LastKey = null;
            LastModifiers = _pointerModifiers;
            LastClickCount = _pointerClickCount;
        }

        if (IsAvailable)
        {
            Activated?.Invoke(this, new ActivationEventArgs(cause));
        }
    }

    /// <inheritdoc/>
    protected override void OnFocusChanged(bool focused)
    {
        base.OnFocusChanged(focused);

        if (focused)
        {
            var list = FindList();
            Debug.Assert(list is not null, "A focused ListItem belongs to a ListView.");
            list.NotifyItemFocused(this);
        }
    }

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        if (eventArgs is KeyEventArgs { IsInitialKeyDown: true } key)
        {
            LastModifiers = key.Stroke.Modifiers;
            LastKey = key.Stroke.Code;
            LastClickCount = 0;
        }
        else if (eventArgs is PointerEventArgs { Pointer.Action: PointerAction.Press } pointer)
        {
            _pointerModifiers = pointer.Pointer.Modifiers;
            _pointerClickCount = pointer.ClickCount;
            LastModifiers = _pointerModifiers;
            LastKey = null;
            LastClickCount = _pointerClickCount;
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

        if (reason == ReleaseReason.Disposed)
        {
            Activated = null;
            AvailabilityChanged = null;
        }
    }

    /// <inheritdoc/>
    protected override void OnContentChanged(ControlBase? previous, ControlBase? current)
    {
        base.OnContentChanged(previous, current);

        previous?.VisibilityChanged -= OnContentAvailabilityChanged;
        previous?.EnabledChanged -= OnContentAvailabilityChanged;
        current?.VisibilityChanged += OnContentAvailabilityChanged;
        current?.EnabledChanged += OnContentAvailabilityChanged;
    }

    private void OnContentAvailabilityChanged(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        AvailabilityChanged?.Invoke(this, EventArgs.Empty);
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

        if (IsSelected == value)
        {
            return;
        }

        IsSelected = value;
        SetSelectedState(value);
        NotifyPropertyChanged(nameof(IsSelected), InvalidationImpact.None);
    }
}
