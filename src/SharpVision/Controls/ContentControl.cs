// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using System.Runtime.ExceptionServices;

/// <summary>Defines a mutable control that owns zero or one publicly replaceable content control.</summary>
/// <remarks>
/// Content participates in the ordinary owned-control lifecycle, rendering, hit testing, focus
/// navigation, inherited context, and disposal. Replacing or clearing content detaches the previous
/// control without disposing it; disposing this owner disposes content that remains assigned.
/// </remarks>
public abstract class ContentControl: Control
{
    private readonly OwnedControlSlot _contentSlot;
    private Control? _publishedContent;

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
                ChangeImpact.Measure),
            capacity: 1);
        _contentSlot.Changed += OnContentSlotChanged;
    }

    /// <summary>Gets or sets the single publicly replaceable content control.</summary>
    /// <remarks>
    /// Null clears the slot. A successful change commits structure, publishes lifecycle callbacks,
    /// requests measure invalidation, invokes <see cref="OnContentChanged"/>, and then raises
    /// <see cref="Control.PropertyChanged"/> while guarded publication remains active. Assigning the
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
    public Control? Content
    {
        get => _contentSlot.Count == 0 ? null : _contentSlot[0];
        set => _contentSlot.ReplaceAll(value is null ? [] : [value]);
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
    protected virtual void OnContentChanged(Control? previous, Control? current)
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
                SaturatingAdd(desired.Width, content.Margin.Horizontal),
                SaturatingAdd(desired.Height, content.Margin.Vertical));
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

    private void OnContentSlotChanged()
    {
        var previous = _publishedContent;
        var current = Content;
        _publishedContent = current;
        var failure = (ExceptionDispatchInfo?) null;

        CaptureFailure(() => OnContentChanged(previous, current), ref failure);
        CaptureFailure(
            () => NotifyPropertyChanged(nameof(Content), ChangeImpact.None),
            ref failure);

        failure?.Throw();
    }

    private static int SaturatingAdd(int left, int right)
    {
        Debug.Assert(left >= 0, "Content desired size is non-negative.");
        Debug.Assert(right >= 0, "Content margin extent is non-negative.");
        var result = (long) left + right;
        return result >= int.MaxValue ? int.MaxValue : (int) result;
    }

    private static void CaptureFailure(Action action, ref ExceptionDispatchInfo? failure)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            failure ??= ExceptionDispatchInfo.Capture(exception);
        }
    }
}
