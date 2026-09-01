// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Defines a semantic item owner with one private presentation container.</summary>
/// <remarks>
/// A derived constructor installs one detached host through <see cref="InitializeItemsHost"/> and
/// exposes its own typed data collection. Realized controls remain available only through the
/// protected semantic helpers. The host and its mutable child collection never become public API.
/// Item controls participate in normal ownership, layout, rendering, hit testing, navigation,
/// inherited context, style-scope resolution, and disposal.
/// </remarks>
[PublicAPI]
public abstract class ItemsControl: ControlBase
{
    private readonly OwnedControlSlot _itemsHostSlot;
    private Container? _itemsHost;

    /// <summary>Initializes an item owner whose derived constructor must install one presentation host.</summary>
    protected ItemsControl()
    {
        _itemsHostSlot = RegisterPermanentOwnedSlot(
            new OwnedControlOptions(
                OwnedControlRole.ItemHost,
                OwnedControlLayer.Normal,
                participatesInHitTesting: true,
                participatesInNavigation: true,
                partKey: null,
                InvalidationImpact.Measure),
            "item presentation host");
    }

    /// <summary>Gets the number of currently realized item controls.</summary>
    /// <exception cref="InvalidOperationException">The presentation host is not available.</exception>
    protected int ItemControlCount => GetItemsHost().Children.Count;

    /// <summary>Gets the realized-control slot for a framework-owned compound transaction.</summary>
    private protected OwnedControlSlot ItemControlsSlot => GetItemsHost().Children.OwnedSlot;

    /// <summary>Installs the one private presentation host for this item owner.</summary>
    /// <param name="host">The non-null detached container that will own realized item controls.</param>
    /// <remarks>
    /// Initialization succeeds at most once. A candidate rejected before structural commit does not
    /// consume the one attempt. If a lifecycle callback fails after commit, the host remains owned,
    /// initialization remains consumed, and the original callback failure is rethrown.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// The host already belongs to a tree or would create an ownership cycle.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// A host was already committed, the attached owner is accessed off-dispatcher, or an ownership
    /// transaction is active.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The owner or host is disposed.</exception>
    protected void InitializeItemsHost(Container host)
    {
        ArgumentNullException.ThrowIfNull(host);
        host.Children.Changed += OnHostItemsChanged;

        try
        {
            _itemsHostSlot.InitializePermanent(host);
        }
        finally
        {
            if (_itemsHostSlot.Count == 1 && ReferenceEquals(_itemsHostSlot[0], host))
            {
                _itemsHost = host;
            }
            else
            {
                host.Children.Changed -= OnHostItemsChanged;
            }
        }
    }

    /// <summary>Gets one realized item control by zero-based position.</summary>
    /// <param name="index">The valid item-control position.</param>
    /// <returns>The realized control at <paramref name="index"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the realized controls.</exception>
    /// <exception cref="InvalidOperationException">The presentation host is not available.</exception>
    protected ControlBase GetItemControl(int index) => GetItemsHost().Children[index];

    /// <summary>Gets the identity position of one realized item control.</summary>
    /// <param name="control">The non-null candidate.</param>
    /// <returns>The zero-based position, or -1 when the control is not realized by this owner.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The presentation host is not available.</exception>
    protected int IndexOfItemControl(ControlBase control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return GetItemsHost().Children.IndexOf(control);
    }

    /// <summary>Inserts one detached realized control at a validated position.</summary>
    /// <param name="index">The insertion position from zero through <see cref="ItemControlCount"/>.</param>
    /// <param name="control">The non-null detached control.</param>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the insertion range.</exception>
    /// <exception cref="ArgumentException">The control cannot belong to the presentation host.</exception>
    /// <exception cref="InvalidOperationException">
    /// The presentation host is unavailable, the attached owner is accessed off-dispatcher, or an
    /// ownership transaction is active.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The owner, host, or control is disposed.</exception>
    protected void InsertItemControl(int index, ControlBase control)
    {
        ArgumentNullException.ThrowIfNull(control);
        GetItemsHost().Children.Insert(index, control);
    }

    /// <summary>Removes one identical realized control without disposing it.</summary>
    /// <param name="control">The non-null candidate.</param>
    /// <returns>True when the control was removed; false when it was not realized by this owner.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// The presentation host is unavailable, the attached owner is accessed off-dispatcher, or an
    /// ownership transaction is active.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The owner or host is disposed.</exception>
    protected bool RemoveItemControl(ControlBase control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return GetItemsHost().Children.Remove(control);
    }

    /// <summary>Removes one realized control by position without disposing it.</summary>
    /// <param name="index">The valid zero-based item-control position.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the realized controls.</exception>
    /// <exception cref="InvalidOperationException">
    /// The presentation host is unavailable, the attached owner is accessed off-dispatcher, or an
    /// ownership transaction is active.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The owner or host is disposed.</exception>
    protected void RemoveItemControlAt(int index) => GetItemsHost().Children.RemoveAt(index);

    /// <summary>Atomically replaces one realized control without disposing the previous control.</summary>
    /// <param name="index">The valid zero-based item-control position.</param>
    /// <param name="control">The non-null detached replacement.</param>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the realized controls.</exception>
    /// <exception cref="ArgumentException">The replacement cannot belong to the presentation host.</exception>
    /// <exception cref="InvalidOperationException">
    /// The presentation host is unavailable, the attached owner is accessed off-dispatcher, or an
    /// ownership transaction is active.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The owner, host, or replacement is disposed.</exception>
    protected void ReplaceItemControl(int index, ControlBase control)
    {
        ArgumentNullException.ThrowIfNull(control);
        GetItemsHost().Children[index] = control;
    }

    /// <summary>Atomically clears all realized controls without disposing them.</summary>
    /// <exception cref="InvalidOperationException">
    /// The presentation host is unavailable, the attached owner is accessed off-dispatcher, or an
    /// ownership transaction is active.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The owner or host is disposed.</exception>
    protected void ClearItemControls() => GetItemsHost().Children.Clear();

    /// <summary>Atomically replaces the complete realized-control snapshot.</summary>
    /// <param name="controls">The non-null borrowed candidate sequence.</param>
    /// <remarks>
    /// The complete sequence is copied and validated before ownership changes. Removed controls are
    /// detached without disposal. A post-commit callback failure never rolls back the new snapshot.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="controls"/> or one of its controls is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// A candidate is duplicated, already owned, attached, or would create a cycle.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The presentation host is unavailable, the attached owner is accessed off-dispatcher, or an
    /// ownership transaction is active.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The owner, host, or a candidate is disposed.</exception>
    protected void ReplaceItemControls(IEnumerable<ControlBase> controls)
    {
        ArgumentNullException.ThrowIfNull(controls);
        GetItemsHost().Children.ReplaceAll(controls);
    }

    /// <summary>Atomically reorders one realized control without detaching it.</summary>
    /// <param name="oldIndex">The current zero-based position.</param>
    /// <param name="newIndex">The destination zero-based position.</param>
    internal void MoveItemControl(int oldIndex, int newIndex) =>
        GetItemsHost().Children.Move(oldIndex, newIndex);

    /// <summary>Responds after one complete realized-control snapshot is structurally committed.</summary>
    /// <remarks>
    /// The callback also runs after child-initiated disposal. It runs during guarded structural
    /// publication, so reentrant ownership mutation is rejected. Throwing does not roll back the
    /// committed snapshot.
    /// </remarks>
    protected virtual void OnItemControlsChanged()
    {
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        var host = GetItemsHost();
        var desired = MeasureChild(host, constraint);

        return host.Visibility == Visibility.Collapsed
            ? default
            : new Size(
                desired.Width.SaturatingAdd(host.Margin.Horizontal),
                desired.Height.SaturatingAdd(host.Margin.Vertical));
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds) =>
        ArrangeChild(GetItemsHost(), bounds, ResolvedAxes.Both);

    /// <inheritdoc/>
    protected override void OnDisposing()
    {
        System.Runtime.ExceptionServices.ExceptionDispatchInfo? failure = null;
        CaptureFailure(OnItemsControlDisposing, ref failure);

        _itemsHost?.Children.Changed -= OnHostItemsChanged;

        CaptureFailure(base.OnDisposing, ref failure);
        failure?.Throw();
    }

    /// <summary>Allows framework item owners to settle semantic state before their private hosts are disposed.</summary>
    private protected virtual void OnItemsControlDisposing()
    {
    }

    [Pure]
    private Container GetItemsHost() => (Container) _itemsHostSlot.RequirePermanentControl();

    /// <summary>Responds to the complete committed mutation of the private item host.</summary>
    /// <param name="change">The immutable structural change.</param>
    private protected virtual void OnItemControlsChanged(OwnedControlChange change) => OnItemControlsChanged();

    private void OnHostItemsChanged(OwnedControlChange change) => OnItemControlsChanged(change);

}
