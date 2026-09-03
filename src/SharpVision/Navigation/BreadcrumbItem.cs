// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Navigation;

/// <summary>Defines one command-bearing retained location in a <see cref="Breadcrumb"/> path.</summary>
[PublicAPI]
public sealed class BreadcrumbItem: InputBase, IStyled<BreadcrumbItemStyle>
{
    private bool _isCurrent;
    private readonly StyleSlot<BreadcrumbItemStyle> _style;

    /// <summary>Initializes an empty focusable breadcrumb item.</summary>
    public BreadcrumbItem()
    {
        EnableCaption();
        EnableCommand();
        _style = InitializeStyle(BreadcrumbItemStyle.Definition);
        Height = Length.Cells(1);
    }

    /// <summary>Raised after an activation commits its owning breadcrumb's current location and
    /// before the captured command executes.</summary>
    public event EventHandler<ActivationEventArgs>? Invoked;

    /// <summary>Gets or sets the non-null mnemonic-aware location caption.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="ArgumentException">The value contains a terminal control character.</exception>
    /// <exception cref="InvalidOperationException">The attached item is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The item is disposed.</exception>
    public override string Text
    {
        get => base.Text;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            ArgumentException.ThrowIfContainsControls(
                value,
                nameof(value),
                "A breadcrumb item text cannot contain terminal controls.");
            base.Text = value;
        }
    }

    /// <summary>Gets whether this item is its owner's semantic current location.</summary>
    public new bool IsCurrent => _isCurrent;

    /// <summary>Gets or sets the complete local item presentation, or null for theme ownership.</summary>
    /// <exception cref="InvalidOperationException">The attached item is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The item is disposed.</exception>
    public BreadcrumbItemStyle? Style
    {
        get => _style.Local;
        set => _style.Local = value;
    }

    /// <summary>Gets the complete local, theme-owned, or code-owned item presentation.</summary>
    public BreadcrumbItemStyle ActualStyle => _style.Actual;

    /// <summary>Activates this available item through the programmatic route.</summary>
    /// <exception cref="InvalidOperationException">The attached item is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The item is disposed.</exception>
    public void PerformInvoke() => _ = TryActivate(ActivationCause.Programmatic);

    /// <inheritdoc/>
    protected override string? AccessKeyText => Text;

    /// <inheritdoc/>
    protected override bool IsSelectedState => _isCurrent;

    /// <summary>Commits semantic selected state on behalf of the owning breadcrumb.</summary>
    /// <param name="value">Whether this item is current.</param>
    internal void CommitSemanticCurrent(bool value) =>
        _ = SetVisualStateProperty(ref _isCurrent, value, nameof(IsCurrent));

    /// <summary>Publishes the item callback after its owner has committed current state.</summary>
    /// <param name="cause">The validated activation source.</param>
    internal void InvokeAfterOwnerCommit(ActivationCause cause) =>
        Invoked?.Invoke(this, new ActivationEventArgs(cause));

    /// <inheritdoc/>
    protected override void Activate(ActivationCause cause)
    {
        var command = CaptureCommand();

        if (FindBreadcrumb() is { } owner)
        {
            _ = owner.TryActivateItem(this, cause, command);
            return;
        }

        if (!TryCaptureDetachedAttachment(out var attachment))
        {
            return;
        }

        InvokeAfterOwnerCommit(cause);
        _ = TryPublishForCurrentDetachedAttachment(
            attachment,
            () => ExecuteCommandIfAny(command),
            () => FindBreadcrumb() is null);
    }

    /// <inheritdoc/>
    protected override bool OnAccessKey(Rune key) =>
        FindBreadcrumb() is { } owner ? owner.ActivateAccessKey(this, key) : base.OnAccessKey(key);

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint) => MeasureCaption(constraint);

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds) => ArrangeCaption(bounds);

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);

        if (reason == ReleaseReason.Disposed)
        {
            Invoked = null;
        }
    }

    /// <inheritdoc/>
    internal override void OnDirectDisposalRequested()
    {
        FindBreadcrumb()?.RemoveItemForDisposal(this);
        base.OnDirectDisposalRequested();
    }

    /// <summary>Gets the live semantic owner, or null while detached.</summary>
    [Pure]
    internal Breadcrumb? FindBreadcrumb() => FindAncestor<Breadcrumb>();
}
