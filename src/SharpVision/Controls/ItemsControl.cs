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
    private RetainedPropertyOverrideService? _itemPropertyOverrides;
    private SelectedItemPressBehavior? _selectedItemPress;
    private bool _isOwnerFocusModelEnabled;

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

    /// <summary>Gets the retained-property override service that leases temporary property values
    /// on this owner's realized item controls, created on first access.</summary>
    /// <remarks>
    /// This is the same generation-checked lease machinery <see cref="EnableOwnerFocusModel"/> uses
    /// for <see cref="ControlBase.IsFocusable"/> and <see cref="ControlBase.IsTabStop"/>. A derived
    /// owner that does not enable that model reaches this service directly - typically through its
    /// own <see cref="RetainedPropertyOverrideService.Acquire"/> calls in its own insertion,
    /// replacement, and removal paths - to lease a property of its own, such as a fixed item width
    /// or a visibility override, using the exact recipe <see cref="EnableOwnerFocusModel"/> itself
    /// builds on. An owner that does enable the focus model instead declares any extra property
    /// through <see cref="GetOwnerFocusModelExtraDescriptors"/> so it shares this model's one
    /// generation per item rather than competing with it: every lease this service hands out is
    /// scoped to one exact realized child, and a child supports only one active generation, so a
    /// second, independent <see cref="RetainedPropertyOverrideService.Acquire"/> call for the same
    /// child would silently retire whichever generation it already held without restoring it. Once
    /// a child stops being the committed occupant of its position - because it was detached through
    /// an ordinary owner API or because it disposed itself - the lease that captured its authored
    /// values is the only lease still permitted to act on it, and every other in-flight reference to
    /// a stale generation for the same child becomes a silent no-op rather than a crash or a
    /// corrupted write. See <see cref="RetainedPropertyOverrideLease.IsCurrent"/> for the exact
    /// currency rule.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The presentation host is not available.</exception>
    protected RetainedPropertyOverrideService ItemPropertyOverrides =>
        _itemPropertyOverrides ??= new RetainedPropertyOverrideService(this, ItemControlsSlot);

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

    /// <summary>Opts one-focus item owners into shared selected-face Space activation.</summary>
    /// <param name="getSelectedTarget">Returns the current selected face, or null.</param>
    /// <param name="isTargetAvailable">Reports whether one captured face can still activate.</param>
    /// <param name="setTargetPressed">Commits the captured face's pressed presentation.</param>
    /// <param name="activateTarget">Activates one still-current captured face.</param>
    /// <param name="consumeWhenNoTarget">Whether eligible Space input with no face is consumed.</param>
    /// <remarks>
    /// A derived item owner with exactly one navigable selected face - a menu, a command bar, or a
    /// similar roving-selection collection - calls this once, typically from its constructor, to
    /// wire the shared Space-activation gesture onto that face without re-implementing press
    /// tracking, release-authority detection, or cancellation.
    /// </remarks>
    /// <exception cref="ArgumentNullException">A delegate is null.</exception>
    /// <exception cref="InvalidOperationException">Selected-item press activation is already enabled.</exception>
    protected void EnableSelectedItemPressActivation(
        Func<ControlBase?> getSelectedTarget,
        Func<ControlBase, bool> isTargetAvailable,
        Action<ControlBase, bool> setTargetPressed,
        Action<ControlBase> activateTarget,
        bool consumeWhenNoTarget)
    {
        VerifyMutable();

        if (_selectedItemPress is not null)
        {
            throw new InvalidOperationException("Selected-item press activation is already enabled.");
        }

        _selectedItemPress = new SelectedItemPressBehavior(
            getSelectedTarget,
            isTargetAvailable,
            setTargetPressed,
            activateTarget,
            () => Capabilities.KeyReleaseEvents.Authoritative,
            consumeWhenNoTarget);
    }

    /// <summary>Routes one key event through selected-face Space activation when enabled.</summary>
    /// <param name="eventArgs">The non-null routed key event.</param>
    /// <remarks>
    /// A no-op when <see cref="EnableSelectedItemPressActivation"/> was never called. A derived owner
    /// calls this from its own preview or bubble key handling for the keys it wants to arm the
    /// shared Space gesture for.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="eventArgs"/> is null and activation is enabled.</exception>
    protected void HandleSelectedItemPressActivation(KeyEventArgs eventArgs) =>
        _selectedItemPress?.Handle(eventArgs);

    /// <summary>Cancels a held selected-face Space interaction without activation.</summary>
    /// <remarks>A no-op when <see cref="EnableSelectedItemPressActivation"/> was never called.</remarks>
    protected void CancelSelectedItemPressActivation() => _selectedItemPress?.Cancel();

    /// <summary>Opts a single-focus-stop item owner into the shared owner-focus item model.</summary>
    /// <remarks>
    /// <para>
    /// A derived owner whose realized items act together as one collective tab stop - a menu, a
    /// command bar, a breadcrumb path, or a similar roving-selection collection - calls this once,
    /// typically from its own constructor after <see cref="InitializeItemsHost"/>, instead of
    /// re-implementing the acquire/suppress/restore recipe for every insertion, replacement, and
    /// removal path. Enabling the model immediately sets this owner's own
    /// <see cref="ControlBase.IsFocusable"/> and <see cref="ControlBase.IsTabStop"/> to true and its
    /// <see cref="ControlBase.TabNavigation"/> to <see cref="TabNavigation.None"/>, then, for
    /// every future item control committed through <see cref="ItemControlsSlot"/>, leases that
    /// item's own <see cref="ControlBase.IsFocusable"/> and <see cref="ControlBase.IsTabStop"/>
    /// through <see cref="ItemPropertyOverrides"/> and forces both false for as long as it remains
    /// realized.
    /// </para>
    /// <para>
    /// A caller's own authored value for either suppressed property is captured, not discarded. It
    /// is written back once the item leaves through an ordinary owner API - a single removal, a
    /// replacement, or a complete reset - because that generation is still current when the
    /// restoration runs. It is discarded without ever attempting a write when the item instead
    /// leaves through its own disposal (<see cref="OwnedControlMutationKind.DirectDisposal"/>),
    /// because a disposing control's storage is no longer a safe write target. This behavior lives
    /// inside the protected <see cref="OnItemControlsChanged(OwnedControlChange)"/> seam: a derived
    /// owner that overrides that seam must still call the base implementation for the enabled model
    /// to keep applying to later structural changes. A derived owner that needs an additional
    /// property leased alongside this model - a fixed item height, for example - declares it
    /// through <see cref="GetOwnerFocusModelExtraDescriptors"/> and sets its initial live value
    /// through <see cref="ConfigureOwnerFocusModelItem"/>, rather than acquiring a second,
    /// independent lease for the same item: one control supports only one active retained-property
    /// generation at a time, so a second <see cref="RetainedPropertyOverrideService.Acquire"/> call
    /// for the same item would silently retire this model's own generation for it without restoring
    /// anything.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// The attached owner is mutated off-dispatcher, or the owner focus model is already enabled.
    /// </exception>
    protected void EnableOwnerFocusModel()
    {
        VerifyMutable();

        if (_isOwnerFocusModelEnabled)
        {
            throw new InvalidOperationException("The owner focus model is already enabled.");
        }

        _isOwnerFocusModelEnabled = true;
        IsFocusable = true;
        IsTabStop = true;
        TabNavigation = TabNavigation.None;
    }

    /// <summary>Gets extra property descriptors this owner leases together with
    /// <see cref="ControlBase.IsFocusable"/> and <see cref="ControlBase.IsTabStop"/> on one item
    /// newly realized while the owner focus model is enabled.</summary>
    /// <param name="item">The item just committed through <see cref="ItemControlsSlot"/>.</param>
    /// <returns>The additional descriptors, or an empty list when this owner needs none.</returns>
    /// <remarks>
    /// The default implementation requests no extra property. A derived owner overrides this only
    /// when <see cref="EnableOwnerFocusModel"/> is also enabled and it needs an additional property
    /// captured and restored on the exact same generation the owner focus model installs for
    /// <paramref name="item"/> - because, as <see cref="EnableOwnerFocusModel"/> documents, one
    /// control supports only one active generation at a time. The returned descriptors become part
    /// of the single <see cref="RetainedPropertyOverrideService.Acquire"/> call this model issues
    /// for <paramref name="item"/>; <see cref="ConfigureOwnerFocusModelItem"/> runs immediately
    /// afterward so the extra properties can receive their initial live values on that same lease.
    /// </remarks>
    protected virtual IReadOnlyList<RetainedPropertyOverrideDescriptor> GetOwnerFocusModelExtraDescriptors(
        ControlBase item) => [];

    /// <summary>Called immediately after the owner focus model acquires one item's combined lease.</summary>
    /// <param name="item">The item just committed through <see cref="ItemControlsSlot"/>.</param>
    /// <param name="lease">
    /// The lease already covering <see cref="ControlBase.IsFocusable"/> and
    /// <see cref="ControlBase.IsTabStop"/> for <paramref name="item"/>, forced false, plus every
    /// descriptor <see cref="GetOwnerFocusModelExtraDescriptors"/> returned for it.
    /// </param>
    /// <remarks>
    /// The default implementation does nothing. A derived owner that declared extra descriptors
    /// through <see cref="GetOwnerFocusModelExtraDescriptors"/> overrides this to call
    /// <see cref="RetainedPropertyOverrideLease.SetLive{T}"/> on <paramref name="lease"/> for each
    /// one, exactly as it would for a lease it acquired itself.
    /// </remarks>
    protected virtual void ConfigureOwnerFocusModelItem(ControlBase item, RetainedPropertyOverrideLease lease)
    {
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
    /// <param name="oldIndex">The current zero-based item-control position.</param>
    /// <param name="newIndex">The destination zero-based item-control position.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="oldIndex"/> or <paramref name="newIndex"/> is outside the realized controls.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The presentation host is unavailable, the attached owner is accessed off-dispatcher, or an
    /// ownership transaction is active.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The owner or host is disposed.</exception>
    protected void MoveItemControl(int oldIndex, int newIndex) =>
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
        CaptureFailure(() => _itemPropertyOverrides?.Dispose(), ref failure);

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
    /// <remarks>
    /// The default implementation applies the owner focus model enabled through
    /// <see cref="EnableOwnerFocusModel"/>, if any, then forwards to the parameterless
    /// <see cref="OnItemControlsChanged()"/> overload, so an owner that only needs to know a change
    /// occurred can keep overriding that overload alone. A derived owner that overrides this typed
    /// overload and does not call <c>base.OnItemControlsChanged(change)</c> suppresses both the
    /// enabled owner focus model and the parameterless callback for that change.
    /// <paramref name="change"/> is copied at commit time rather than aliasing live registry
    /// storage, so it remains safe to inspect or retain after this callback returns; the
    /// <see cref="ControlBase"/> instances it references stay live, mutable objects whose own state
    /// may keep changing independently of this snapshot.
    /// </remarks>
    protected virtual void OnItemControlsChanged(OwnedControlChange change)
    {
        if (_isOwnerFocusModelEnabled)
        {
            ApplyOwnerFocusModel(change);
        }

        OnItemControlsChanged();
    }

    private void OnHostItemsChanged(OwnedControlChange change)
    {
        System.Runtime.ExceptionServices.ExceptionDispatchInfo? failure = null;
        CaptureFailure(() => _selectedItemPress?.ReconcileRemoved(change.Removed.Span), ref failure);
        CaptureFailure(() => OnItemControlsChanged(change), ref failure);
        failure?.Throw();
    }

    // Every item this owner realizes while the model is enabled loses independent focus and
    // tab-stop reachability for as long as it stays realized, because the owner itself is the one
    // collective tab stop: EnableOwnerFocusModel already made that owner focusable and tab-stopped
    // in its place. An item added or swapped in has both properties - plus any extra descriptors a
    // derived owner declared - forced onto one combined generation immediately, with SetLive
    // re-checking lease currency independently before each write so a write never lands on a
    // generation a reentrant callback already superseded. An item leaving through an ordinary owner
    // API gets its captured authored values written back through Restore. An item leaving through
    // its own direct disposal never reaches Restore here at all: ControlBase already retired that
    // item's currently-installed lease - discarding its captured values without attempting a write,
    // because a disposing control's storage is never a safe write target - before publishing the
    // DirectDisposal change this callback observes, so Restore would find no lease left to act on.
    private void ApplyOwnerFocusModel(OwnedControlChange change)
    {
        foreach (var added in change.Added.Span)
        {
            var extra = GetOwnerFocusModelExtraDescriptors(added);
            var lease = extra.Count == 0
                ? ItemPropertyOverrides.Acquire(
                    added,
                    RetainedPropertyOverrides.IsFocusable,
                    RetainedPropertyOverrides.IsTabStop)
                : ItemPropertyOverrides.Acquire(added, CombineWithFocusDescriptors(extra));
            lease.SetLive(RetainedControlProperty.IsFocusable, false);
            lease.SetLive(RetainedControlProperty.IsTabStop, false);
            ConfigureOwnerFocusModelItem(added, lease);
        }

        if (change.Kind == OwnedControlMutationKind.DirectDisposal)
        {
            return;
        }

        foreach (var removed in change.Removed.Span)
        {
            ItemPropertyOverrides.Restore(removed);
        }
    }

    private static RetainedPropertyOverrideDescriptor[] CombineWithFocusDescriptors(
        IReadOnlyList<RetainedPropertyOverrideDescriptor> extra)
    {
        var descriptors = new RetainedPropertyOverrideDescriptor[extra.Count + 2];
        descriptors[0] = RetainedPropertyOverrides.IsFocusable;
        descriptors[1] = RetainedPropertyOverrides.IsTabStop;

        for (var index = 0; index < extra.Count; index++)
        {
            descriptors[index + 2] = extra[index];
        }

        return descriptors;
    }
}
