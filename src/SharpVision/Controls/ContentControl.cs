// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using System.Runtime.ExceptionServices;

using SharpVision.Text;

/// <summary>Defines a mutable control that owns zero or one publicly replaceable content control.</summary>
/// <remarks>
/// Content participates in the ordinary owned-control lifecycle, rendering, hit testing, focus
/// navigation, inherited context, and disposal. Replacing or clearing content detaches the previous
/// control without disposing it; disposing this owner disposes content that remains assigned.
/// </remarks>
[PublicAPI]
public abstract class ContentControl: ControlBase
{
    private readonly OwnedControlSlot _contentSlot;

    /// <summary>Initializes an empty single-content control.</summary>
    protected ContentControl()
    {
        _contentSlot = RegisterOwnedSlot(
            new OwnedControlOptions(
                OwnedControlRole.Content,
                OwnedControlLayer.Normal,
                participatesInHitTesting: true,
                participatesInNavigation: true,
                partKey: null,
                InvalidationImpact.Measure),
            capacity: 1);
        _contentSlot.Changed += OnContentSlotChanged;
    }

    /// <summary>Gets or sets the single publicly replaceable content control.</summary>
    /// <remarks>
    /// Null clears the slot. A successful change commits structure, publishes lifecycle callbacks,
    /// requests measure invalidation, invokes <see cref="OnContentChanged"/>, and then raises
    /// <see cref="ControlBase.PropertyChanged"/> while guarded publication remains active. Assigning the
    /// identical control is a no-op after dispatcher and lifetime validation. If the content hook
    /// throws, the property notification is still attempted against the committed structure. The
    /// hook failure remains authoritative over a later property-subscriber failure, while an earlier
    /// ownership-transaction callback failure takes precedence over both. Callback and subscriber
    /// failures never roll back the committed content edge.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// The assigned control already belongs to a tree or would create an ownership cycle.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The attached owner is accessed off-dispatcher or an ownership transaction is active.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The owner or assigned control is disposed.</exception>
    public ControlBase? Content
    {
        get => _contentSlot.Count == 0 ? null : _contentSlot[0];
        set => _contentSlot.ReplaceAll(value is null ? [] : [value]);
    }

    /// <inheritdoc/>
    public override SelectableTextSnapshot GetSelectableTextSnapshot()
    {
        VerifyMutable();
        return SelectableTextAggregation.Create(this);
    }

    /// <inheritdoc/>
    internal override bool AddSelectableTextChildren(List<ControlBase> children)
    {
        ArgumentNullException.ThrowIfNull(children);

        if (Content is { } content)
        {
            children.Add(content);
        }

        return true;
    }

    /// <summary>Responds after the content ownership change is structurally committed.</summary>
    /// <param name="previous">The detached previous content, or null.</param>
    /// <param name="current">The committed current content, or null.</param>
    /// <remarks>
    /// The callback runs before the public property notification. Its exception is retained until
    /// that notification is attempted; an earlier structural callback failure remains authoritative
    /// for the complete ownership transaction. It runs while guarded structural publication remains
    /// active, so changing owned slots or disposing the owner or either affected content control is
    /// rejected as reentrant. The committed ownership change is never rolled back.
    /// </remarks>
    protected virtual void OnContentChanged(ControlBase? previous, ControlBase? current)
    {
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        var content = Content;

        if (content is null)
        {
            return default;
        }

        var desired = MeasureChild(content, constraint);

        return content.Visibility == Visibility.Collapsed
            ? default
            : new Size(
                desired.Width.SaturatingAdd(content.Margin.Horizontal),
                desired.Height.SaturatingAdd(content.Margin.Vertical));
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        var content = Content;

        if (content is not null)
        {
            ArrangeChild(content, bounds, ResolvedAxes.Both);
        }
    }

    private void OnContentSlotChanged(OwnedControlChange change)
    {
        var previous = change.Previous.Length == 0 ? null : change.Previous.Span[0];
        var current = change.Current.Length == 0 ? null : change.Current.Span[0];
        ExceptionDispatchInfo? failure = null;

        ExceptionAggregation.Capture(() => OnContentChanged(previous, current), ref failure);
        ExceptionAggregation.Capture(
            () => NotifyPropertyChanged(nameof(Content), InvalidationImpact.None),
            ref failure);

        failure?.Throw();
    }

}
