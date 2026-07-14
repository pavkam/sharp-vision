// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using SharpVision.Terminal.Input;

/// <summary>Owns one realized List template control and its selection behavior.</summary>
internal sealed class ListItem: Pressable
{
    private bool _isSelected;

    /// <summary>Initializes one indexed detached realized control.</summary>
    /// <param name="index">The non-negative stable item index.</param>
    /// <param name="content">The non-null detached template control.</param>
    internal ListItem(int index, Control content) : base(capacity: 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentNullException.ThrowIfNull(content);
        HorizontalAlignment = HorizontalAlignment.Stretch;
        Index = index;
        Children.Add(content);
    }

    /// <summary>Raised after eligible Space, Enter, or primary pointer activation.</summary>
    internal event EventHandler<ActivationEventArgs>? Activated;

    /// <summary>Gets the stable item index.</summary>
    internal int Index { get; }

    /// <summary>Gets the owned template control.</summary>
    internal Control Content => Children[0];

    /// <summary>Gets whether this item is committed selected.</summary>
    internal bool IsSelected => _isSelected;

    /// <summary>Gets modifiers captured from the activation transition.</summary>
    internal Modifiers LastModifiers { get; private set; }

    /// <summary>Gets the activating key, or null for pointer input.</summary>
    internal Code? LastKey { get; private set; }

    /// <summary>Gets whether content is effectively available for navigation and activation.</summary>
    internal bool IsAvailable => Content.EffectiveIsEnabled && Content.EffectiveIsVisible;

    /// <summary>Commits selected visual state after the owning transaction.</summary>
    /// <param name="value">The committed selected flag.</param>
    internal void CommitSelection(bool value) =>
        Commit(value);

    /// <inheritdoc/>
    public override Control? HitTest(Point point) =>
        !IsDisposed && IsHitTestVisible && EffectiveIsVisible && EffectiveIsEnabled &&
        Bounds.Contains(point)
            ? this
            : null;

    /// <inheritdoc/>
    protected override void Activate(ActivationCause cause)
    {
        if (IsAvailable)
        {
            Activated?.Invoke(this, new ActivationEventArgs(cause));
        }
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
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        Content.Measure(constraint);
        return new Size(
            Add(Content.DesiredSize.Width, Content.Margin.Horizontal),
            Add(Content.DesiredSize.Height, Content.Margin.Vertical));
    }

    /// <inheritdoc/>
    protected override void OnRender(TerminalCanvas canvas)
    {
        if (Bounds.Width == 0 || Bounds.Height == 0 || !ControlAppearance.HasOpaqueFill(this, GetVisualState()))
        {
            return;
        }

        canvas.Clear(Bounds, ResolvedStyle);
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds) =>
        Content.Arrange(bounds, widthResolved: true, heightResolved: false);

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);

        if (reason == ReleaseReason.Disposed)
        {
            Activated = null;
        }
    }

    private static int Add(int left, int right)
    {
        Debug.Assert(left >= 0, "ListItem accumulation uses non-negative extents.");
        Debug.Assert(right >= 0, "ListItem accumulation uses non-negative extents.");

        return (int) Math.Min(int.MaxValue, (long) left + right);
    }

    private void Commit(bool value)
    {
        if (Set(ref _isSelected, value, Invalidation.Render, nameof(IsSelected)))
        {
            SetSelectedState(value);
        }
    }
}
