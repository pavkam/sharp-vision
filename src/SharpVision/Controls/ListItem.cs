// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using SharpVision.Terminal.Input;

/// <summary>Owns one realized List template control and its selection behavior.</summary>
internal sealed class ListItem: Pressable
{
    /// <summary>Initializes one indexed detached realized control.</summary>
    /// <param name="index">The non-negative stable item index.</param>
    /// <param name="content">The non-null detached template control.</param>
    internal ListItem(int index, Control content)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentNullException.ThrowIfNull(content);
        HorizontalAlignment = HorizontalAlignment.Stretch;
        Focusable = false;
        TabStop = false;
        Index = index;
        Content = content;
    }

    /// <summary>Raised after eligible Space, Enter, or primary pointer activation.</summary>
    internal event EventHandler<ActivationEventArgs>? Activated;

    /// <summary>Gets the stable item index.</summary>
    internal int Index { get; }

    /// <summary>Gets whether this item is committed selected.</summary>
    internal bool IsSelected { get; private set; }

    /// <summary>Gets modifiers captured from the activation transition.</summary>
    internal Modifiers LastModifiers { get; private set; }

    /// <summary>Gets the activating key, or null for pointer input.</summary>
    internal Code? LastKey { get; private set; }

    /// <summary>Gets whether content is effectively available for navigation and activation.</summary>
    internal bool IsAvailable => Content is { } content && content.EffectiveIsEnabled && content.EffectiveIsVisible;

    /// <summary>Commits selected visual state after the owning transaction.</summary>
    /// <param name="value">The committed selected flag.</param>
    internal void CommitSelection(bool value) =>
        Commit(value);

    /// <summary>Invokes this available item on behalf of its owning selector.</summary>
    /// <param name="cause">The semantic activation source.</param>
    /// <param name="key">The activating key, or null when activation is not key driven.</param>
    /// <param name="modifiers">The modifiers captured with <paramref name="key"/>.</param>
    internal void ActivateFromOwner(ActivationCause cause, Code? key, Modifiers modifiers)
    {
        LastKey = key;
        LastModifiers = modifiers;
        Activate(cause);
    }

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
    protected override void OnFocusChanged(bool focused)
    {
        base.OnFocusChanged(focused);

        if (focused)
        {
            var list = FindList();
            Debug.Assert(list is not null, "A focused ListItem belongs to a List.");
            list.NotifyItemFocused(this);
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
        var content = Content;
        Debug.Assert(content is not null, "A realized ListItem always owns template content.");
        var desired = MeasureChild(content, constraint);

        return content.Visibility == Visibility.Collapsed
            ? default
            : new Size(
                Add(desired.Width, content.Margin.Horizontal),
                Add(desired.Height, content.Margin.Vertical));
    }

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        if (Bounds.Width == 0 || Bounds.Height == 0 || !ControlAppearance.HasOpaqueFill(this, GetAppearanceState()))
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
        }
    }

    private List? FindList()
    {
        for (var current = Parent; current is not null; current = current.Parent)
        {
            if (current is List list)
            {
                return list;
            }
        }

        Debug.Assert(false, "A ListItem always has a List ancestor while attached.");
        return null;
    }

    private static int Add(int left, int right)
    {
        Debug.Assert(left >= 0, "ListItem accumulation uses non-negative extents.");
        Debug.Assert(right >= 0, "ListItem accumulation uses non-negative extents.");

        return (int) Math.Min(int.MaxValue, (long) left + right);
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
        NotifyPropertyChanged(nameof(IsSelected), ChangeImpact.None);
    }
}
