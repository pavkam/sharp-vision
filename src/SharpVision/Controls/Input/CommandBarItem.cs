// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using System.ComponentModel;
using System.Runtime.ExceptionServices;

/// <summary>Defines one semantic command face retained by a <see cref="CommandBar"/>.</summary>
[PublicAPI]
public sealed class CommandBarItem: InputBase, IStyled<CommandBarItemStyle>
{
    private readonly StyleSlot<CommandBarItemStyle> _style;
    private ulong _activationGeneration;

    /// <summary>Initializes an empty focusable command item with caption, command, and press behavior.</summary>
    public CommandBarItem()
    {
        EnablePressActivation();
        EnableCaption();
        EnableCommand();
        _style = InitializeStyle(CommandBarItemStyle.Definition);
        PropertyChanged += OnOwnPropertyChanged;
        ParentChanged += OnOwnParentChanged;
    }

    /// <summary>Raised after an eligible action is accepted and before its owning bar event and command.</summary>
    public event EventHandler<ActivationEventArgs>? Invoked;

    /// <summary>Gets or sets the complete local presentation, or null for theme ownership.</summary>
    /// <exception cref="InvalidOperationException">The attached item is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The item is disposed.</exception>
    public CommandBarItemStyle? Style
    {
        get => _style.Local;
        set => _style.Local = value;
    }

    /// <summary>Gets the complete local, theme-owned, or code-owned presentation.</summary>
    public CommandBarItemStyle ActualStyle => _style.Actual;

    /// <summary>Gets or sets the optional leading edge-pinned decoration.</summary>
    /// <exception cref="InvalidOperationException">The attached item is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The item is disposed.</exception>
    public Affix? StartAffix
    {
        get;
        set => _ = SetProperty(ref field, value, GetAffixChangeImpact(field, value));
    }

    /// <summary>Gets or sets the optional trailing edge-pinned decoration.</summary>
    /// <exception cref="InvalidOperationException">The attached item is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The item is disposed.</exception>
    public Affix? EndAffix
    {
        get;
        set => _ = SetProperty(ref field, value, GetAffixChangeImpact(field, value));
    }

    /// <summary>Gets whether the current bar layout presents this item through the overflow menu.</summary>
    public bool IsOverflowed { get; private set; }

    /// <summary>Activates an available item through the programmatic path.</summary>
    /// <exception cref="InvalidOperationException">The attached item is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The item is disposed.</exception>
    public void PerformInvoke() => _ = TryActivate(ActivationCause.Programmatic);

    /// <summary>Gets the identity of the current availability generation.</summary>
    internal ulong AvailabilityGeneration { get; private set; }

    /// <summary>Publishes the item-level event for one bar-owned activation stage.</summary>
    /// <param name="cause">The validated activation path.</param>
    internal void RaiseInvoked(ActivationCause cause) => Invoked?.Invoke(this, new ActivationEventArgs(cause));

    /// <summary>Attempts activation from a retained overflow projection.</summary>
    /// <param name="cause">The validated menu activation path.</param>
    internal void InvokeFromProjection(ActivationCause cause) => _ = TryActivate(cause);

    /// <summary>Commits the live primary-or-overflow projection fact.</summary>
    /// <param name="value">Whether overflow currently presents this item.</param>
    internal void SetOverflowed(bool value)
    {
        if (IsOverflowed == value)
        {
            return;
        }

        IsOverflowed = value;
        NotifyPropertyChanged(nameof(IsOverflowed), InvalidationImpact.None);
    }

    /// <summary>Commits owner-driven selected appearance without changing focusability.</summary>
    /// <param name="value">Whether this item is the bar's selected item.</param>
    internal void CommitSelection(bool value) => SetSelectedState(value);

    /// <inheritdoc/>
    protected override void Activate(ActivationCause cause)
    {
        if (FindAncestor<CommandBar>() is { } owner)
        {
            owner.InvokeItem(this, cause);
            return;
        }

        var binding = CaptureCommand();

        if (binding.Command is not null && !binding.Command.CanExecute(binding.Parameter))
        {
            return;
        }

        var generation = ++_activationGeneration;
        var availability = AvailabilityGeneration;
        ExceptionDispatchInfo? failure = null;
        ExceptionAggregation.Capture(() => RaiseInvoked(cause), ref failure);

        if (!IsDisposed &&
            generation == _activationGeneration &&
            availability == AvailabilityGeneration &&
            EffectiveIsEnabled &&
            EffectiveIsVisible)
        {
            ExceptionAggregation.Capture(() => binding.Command?.Execute(binding.Parameter), ref failure);
        }

        failure?.Throw();
    }

    /// <inheritdoc/>
    protected override bool OnAccessKey(Rune key) =>
        FindAncestor<CommandBar>()?.InvokeAccessKey(this, key) ?? base.OnAccessKey(key);

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        var content = TextControl;
        var padding = ActualStyle.Padding;
        var affixes = MeasureAffixes(StartAffix, EndAffix, ActualStyle.AffixGap);
        var affixInset = affixes.StartCells + affixes.EndCells;

        if (content is null || content.Visibility == Visibility.Collapsed)
        {
            return new Size(padding.Horizontal.Add(affixInset), Math.Max(1, padding.Vertical));
        }

        var desired = MeasureChild(
            content,
            new Constraint(
                constraint.Width.Subtract(padding.Horizontal.Add(affixInset)),
                constraint.Height.Subtract(padding.Vertical)));
        return new Size(
            desired.Width.Add(content.Margin.Horizontal).Add(padding.Horizontal).Add(affixInset),
            Math.Max(1, desired.Height.Add(content.Margin.Vertical).Add(padding.Vertical)));
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        if (TextControl is not { } content)
        {
            return;
        }

        var affixes = MeasureAffixes(StartAffix, EndAffix, ActualStyle.AffixGap);
        var face = DeflateForAffixes(ActualStyle.Padding.Deflate(bounds), affixes);
        ArrangeChild(content, face, ResolvedAxes.Both);
    }

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        base.OnRenderContent(canvas);
        var affixes = MeasureAffixes(StartAffix, EndAffix, ActualStyle.AffixGap);
        var face = ActualStyle.Padding.Deflate(ContentBounds);
        RenderAffixes(canvas, face, affixes, StartAffix, EndAffix, ResolvedStyle);
    }

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        FindAncestor<CommandBar>()?.PrepareItemPointer(this, eventArgs);
        base.OnEvent(eventArgs);
        HandlePressActivation(eventArgs);
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        AvailabilityGeneration++;
        base.OnUnavailable(reason);

        if (reason == ReleaseReason.Disposed)
        {
            PropertyChanged -= OnOwnPropertyChanged;
            ParentChanged -= OnOwnParentChanged;
            Invoked = null;
        }
    }

    private void OnOwnPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.PropertyName is nameof(IsEnabled) or nameof(EffectiveIsEnabled) or
            nameof(Visibility) or nameof(EffectiveIsVisible))
        {
            AvailabilityGeneration++;
        }
    }

    private void OnOwnParentChanged(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        AvailabilityGeneration++;
    }
}
