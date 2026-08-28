// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.DataBinding;

using System.Collections.Specialized;
using System.ComponentModel;

/// <summary>Owns one live relationship between a retained control property and a model property.</summary>
[PublicAPI]
public sealed class Binding: IDisposable
{
    private readonly Func<NotifyCollectionChangedEventArgs, bool>? _applyIncrementalChange;
    private readonly Func<object?, object?> _convertBack;
    private readonly Func<object?, object?> _convertToTarget;
    private readonly bool _coordinatesItems;
    private readonly object? _fallbackValue;
    private readonly Lock _gate = new();
    private readonly ControlBindingRegistry _registry;
    private readonly bool _refreshAfterItems;
    private readonly object _source;
    private readonly PropertyPath _sourcePath;
    private readonly PropertyPath _targetPath;
    private readonly bool _tracksCollection;
    private BindingDirection _direction;
    private bool _refreshSourceAfterTarget;
    private bool _sourceDirty;
    private bool _sourceScheduled;
    private long _observedSourcePathRevision = -1;
    private long _sourcePathRevision;
    private Dispatcher? _scheduledDispatcher;
    private long _scheduledAttachmentVersion;
    private CollectionObserver? _collectionObserver;
    private PropertyPathObserver? _sourceObserver;
    private PropertyChangedEventHandler? _targetHandler;

    /// <summary>Initializes one validated binding ready for atomic startup.</summary>
    internal Binding(
        ControlBase target,
        PropertyPath targetPath,
        object source,
        PropertyPath sourcePath,
        BindingMode mode,
        Func<object?, object?> convertToTarget,
        Func<object?, object?> convertBack,
        object? fallbackValue,
        ControlBindingRegistry registry,
        bool tracksCollection = false,
        bool coordinatesItems = false,
        bool refreshAfterItems = false,
        Func<NotifyCollectionChangedEventArgs, bool>? applyIncrementalChange = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(targetPath);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(sourcePath);
        ArgumentNullException.ThrowIfNull(convertToTarget);
        ArgumentNullException.ThrowIfNull(convertBack);
        ArgumentNullException.ThrowIfNull(registry);
        Target = target;
        _targetPath = targetPath;
        _source = source;
        _sourcePath = sourcePath;
        Mode = mode;
        _convertToTarget = convertToTarget;
        _convertBack = convertBack;
        _fallbackValue = fallbackValue;
        _registry = registry;
        _tracksCollection = tracksCollection;
        _coordinatesItems = coordinatesItems;
        _refreshAfterItems = refreshAfterItems;
        _applyIncrementalChange = applyIncrementalChange;
    }

    /// <summary>Gets the control whose property is synchronized by this binding.</summary>
    public ControlBase Target { get; }

    /// <summary>Gets the direction of synchronization (source-to-target, target-to-source, or both).</summary>
    public BindingMode Mode { get; }

    /// <summary>Gets whether this binding has been disposed, releasing all property-change subscriptions.</summary>
    public bool IsDisposed { get; private set; }

    /// <summary>Gets the exact target property used for duplicate detection.</summary>
    internal string TargetPropertyName => _targetPath.LeafName;

    /// <summary>Registers subscriptions and performs mode-specific initial synchronization.</summary>
    internal void Start()
    {
        _registry.Add(this);

        try
        {
            if (_tracksCollection)
            {
                _collectionObserver = new CollectionObserver(OnCollectionInvalidated);
            }

            if (Mode is BindingMode.OneWay or BindingMode.TwoWay)
            {
                _sourceObserver = new PropertyPathObserver(_source, _sourcePath, OnSourcePathInvalidated);
            }

            if (Mode is BindingMode.TwoWay or BindingMode.OneWayToSource)
            {
                _targetHandler = OnTargetPropertyChanged;
                Target.PropertyChanged += _targetHandler;
            }

            if (Mode == BindingMode.OneWayToSource)
            {
                ApplyTargetToSource();
            }
            else
            {
                ApplySourceToTarget();
            }
        }
        catch
        {
            DisposeCore(removeFromRegistry: true);
            throw;
        }
    }

    /// <summary>Releases subscriptions and removes this relationship from its target.</summary>
    /// <exception cref="InvalidOperationException">The attached target is accessed off-dispatcher.</exception>
    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        Target.VerifyMutable();
        DisposeCore(removeFromRegistry: true);
    }

    /// <summary>Releases one relationship during target-owned disposal.</summary>
    internal void DisposeFromOwner() => DisposeCore(removeFromRegistry: false);

    /// <summary>Reapplies model selection after a coordinated items commit.</summary>
    internal void RefreshAfterItemsChanged()
    {
        if (_refreshAfterItems && !IsDisposed)
        {
            ApplySourceToTarget();
        }
    }

    /// <summary>Schedules any source notification retained while the target was detached.</summary>
    internal void OnTargetAttached() => ScheduleSourceUpdate(deferInline: _tracksCollection);

    /// <summary>Invalidates scheduling state owned by the target's former dispatcher attachment.</summary>
    internal void OnTargetDetached()
    {
        lock (_gate)
        {
            _sourceScheduled = false;
            _scheduledDispatcher = null;
        }
    }

    private void ApplySourceToTarget()
    {
        if (IsDisposed)
        {
            return;
        }

        if (_direction == BindingDirection.TargetToSource)
        {
            _refreshSourceAfterTarget = true;
            return;
        }

        long sourcePathRevisionBeforeRead;

        lock (_gate)
        {
            sourcePathRevisionBeforeRead = _sourcePathRevision;
        }

        var available = _sourcePath.TryRead(_source, out var sourceValue);
        var observedValue = available ? sourceValue : null;
        var observer = _collectionObserver;
        observer?.Observe(observedValue);
        long sourcePathRevision;
        bool canApplyIncrementally;

        lock (_gate)
        {
            sourcePathRevision = _sourcePathRevision;
            canApplyIncrementally = sourcePathRevisionBeforeRead == sourcePathRevision &&
                sourcePathRevision == _observedSourcePathRevision;
        }

        if (canApplyIncrementally && TryApplyIncrementalChange(observer, observedValue))
        {
            return;
        }

        _ = observer?.TryTakePendingChange(observedValue, out _);
        object? targetValue;

        if (available)
        {
            try
            {
                targetValue = _convertToTarget(sourceValue);
            }
            catch
            {
                targetValue = _fallbackValue;
            }
        }
        else
        {
            targetValue = _fallbackValue;
        }

        if (_targetPath.TryRead(Target, out var current) && Equals(current, targetValue))
        {
            MarkSourcePathObserved(sourcePathRevisionBeforeRead, sourcePathRevision);
            return;
        }

        _direction = BindingDirection.SourceToTarget;
        var coordinated = _coordinatesItems;
        var succeeded = false;

        if (coordinated)
        {
            _registry.EnterTargetUpdate();
        }

        try
        {
            try
            {
                succeeded = _targetPath.Write(Target, targetValue);
            }
            catch when (!Equals(targetValue, _fallbackValue))
            {
                // A target property that validates rather than clamps (e.g. Slider.Value
                // outside Minimum/Maximum) can reject an ordinary identity-converted value
                // with no conversion step to soft-fail. Fall back the same way a conversion
                // failure already does, instead of letting the exception escape to the
                // dispatcher and stop the whole loop over one out-of-range mutation. Skipped
                // when the write already attempted the fallback value itself, since a retry
                // would fail identically — that failure propagates as-is.
                succeeded = _targetPath.Write(Target, _fallbackValue);
            }
        }
        finally
        {
            _direction = BindingDirection.None;

            if (coordinated)
            {
                _registry.ExitTargetUpdate(succeeded);
            }
        }

        if (succeeded)
        {
            MarkSourcePathObserved(sourcePathRevisionBeforeRead, sourcePathRevision);
        }
    }

    /// <summary>Commits a stable source-path read as the incremental baseline revision.</summary>
    private void MarkSourcePathObserved(long revisionBeforeRead, long revisionAfterRead)
    {
        if (revisionBeforeRead != revisionAfterRead)
        {
            return;
        }

        lock (_gate)
        {
            if (_sourcePathRevision == revisionAfterRead)
            {
                _observedSourcePathRevision = revisionAfterRead;
            }
        }
    }

    private bool TryApplyIncrementalChange(CollectionObserver? observer, object? observedValue)
    {
        if (observer is null ||
            _applyIncrementalChange is not { } applyIncremental ||
            !observer.TryTakePendingChange(observedValue, out var change))
        {
            return false;
        }

        _direction = BindingDirection.SourceToTarget;
        var coordinated = _coordinatesItems;
        var succeeded = false;

        if (coordinated)
        {
            _registry.EnterTargetUpdate();
        }

        try
        {
            succeeded = applyIncremental(change);
        }
        finally
        {
            _direction = BindingDirection.None;

            if (coordinated)
            {
                _registry.ExitTargetUpdate(succeeded);
            }
        }

        return succeeded;
    }

    private void ApplyTargetToSource()
    {
        if (IsDisposed || _direction == BindingDirection.SourceToTarget)
        {
            return;
        }

        _ = _targetPath.TryRead(Target, out var targetValue);
        object? sourceValue;

        try
        {
            sourceValue = _convertBack(targetValue);
        }
        catch
        {
            // A throwing reverse converter is a value rejection, not a binding contract
            // violation - the direct mirror of a validating model setter below. Drop it here,
            // before _direction is set, so there is nothing to unwind; the target keeps its
            // already-committed value.
            return;
        }

        if (_sourcePath.TryRead(_source, out var current) && Equals(current, sourceValue))
        {
            return;
        }

        _direction = BindingDirection.TargetToSource;

        try
        {
            // A null intermediate is a transient data-population race, not a binding failure:
            // the model is simply left untouched, and the next forward update (once the
            // intermediate is non-null) reconciles the target back to the source's real value.
            // Throwing here would erupt out of the control's own property setter, after the
            // control already committed its value and raised PropertyChanged.
            //
            // A validating model setter that throws outright is the same value-rejection case
            // as the null intermediate above, not a binding contract violation: drop it and
            // leave the model untouched rather than letting it erupt out of the control's own
            // property setter and force-stop the application.
            _ = _sourcePath.Write(_source, sourceValue);
        }
        catch
        {
        }
        finally
        {
            _direction = BindingDirection.None;
        }

        if (_refreshSourceAfterTarget)
        {
            _refreshSourceAfterTarget = false;
            ApplySourceToTarget();
        }
    }

    private void DisposeCore(bool removeFromRegistry)
    {
        lock (_gate)
        {
            if (IsDisposed)
            {
                return;
            }

            IsDisposed = true;
            _sourceDirty = false;
        }

        _sourceObserver?.Dispose();
        _sourceObserver = null;
        _collectionObserver?.Dispose();
        _collectionObserver = null;

        if (_targetHandler is not null)
        {
            Target.PropertyChanged -= _targetHandler;
            _targetHandler = null;
        }

        if (removeFromRegistry)
        {
            _registry.Remove(this);
        }
    }

    private void DrainSourceUpdates(Dispatcher dispatcher, long attachmentVersion)
    {
        Debug.Assert(dispatcher.CheckAccess(), "A posted binding update runs on its target dispatcher.");

        if (!ReferenceEquals(Target.Dispatcher, dispatcher) ||
            Target.BindingAttachmentVersion != attachmentVersion)
        {
            ClearScheduled(dispatcher, attachmentVersion);
            ScheduleSourceUpdate();
            return;
        }

        try
        {
            for (var pass = 0; pass < 2; pass++)
            {
                lock (_gate)
                {
                    if (IsDisposed)
                    {
                        ClearScheduledCore();
                        return;
                    }

                    _sourceDirty = false;
                }

                ApplySourceToTarget();

                if (!ReferenceEquals(Target.Dispatcher, dispatcher) ||
                    Target.BindingAttachmentVersion != attachmentVersion)
                {
                    ClearScheduled(dispatcher, attachmentVersion);
                    ScheduleSourceUpdate();
                    return;
                }

                lock (_gate)
                {
                    if (!_sourceDirty)
                    {
                        ClearScheduledCore();
                        return;
                    }
                }
            }
        }
        catch
        {
            ClearScheduled(dispatcher, attachmentVersion);
            throw;
        }

        // This method itself runs as the body of one already-dequeued dispatcher work item
        // (DrainSourceUpdates is only ever reached through a prior Dispatcher.Post), so a full
        // queue on the self-repost below does not need the off-thread bridging OnSourceInvalidated
        // requires: propagating it here reaches the dispatcher's own synchronous callback-failure
        // path (Dispatcher.Report, via the same try/catch that already wraps every work item's
        // Execute()) exactly like any other callback exception would, with no separate signal
        // needed. Only a genuinely disposed dispatcher - truly going away regardless - is dropped
        // rather than propagated; _sourceScheduled is still reset first in both cases so a later
        // source change is not left believing a drain is already pending forever.
        try
        {
            dispatcher.Post(() => DrainSourceUpdates(dispatcher, attachmentVersion));
        }
        catch (ObjectDisposedException)
        {
            ClearScheduled(dispatcher, attachmentVersion);
        }
        catch (InvalidOperationException)
        {
            ClearScheduled(dispatcher, attachmentVersion);
            throw;
        }
    }

    private void OnCollectionInvalidated() => OnSourceInvalidated(sourcePathChanged: false);

    private void OnSourcePathInvalidated() => OnSourceInvalidated(sourcePathChanged: true);

    private void OnSourceInvalidated(bool sourcePathChanged)
    {
        lock (_gate)
        {
            if (IsDisposed)
            {
                return;
            }

            if (sourcePathChanged)
            {
                _sourcePathRevision++;
            }

            _sourceDirty = true;
        }

        if (Target.Dispatcher is null && Target.BindingAttachmentVersion == 0)
        {
            lock (_gate)
            {
                _sourceDirty = false;
            }

            ApplySourceToTarget();
            return;
        }

        ScheduleSourceUpdate();
    }

    private void ScheduleSourceUpdate(bool deferInline = false)
    {
        var dispatcher = Target.Dispatcher;

        if (dispatcher is null)
        {
            return;
        }

        var attachmentVersion = Target.BindingAttachmentVersion;

        lock (_gate)
        {
            if (IsDisposed || !_sourceDirty || _sourceScheduled)
            {
                return;
            }

            _sourceScheduled = true;
            _scheduledDispatcher = dispatcher;
            _scheduledAttachmentVersion = attachmentVersion;
        }

        if (dispatcher.CheckAccess() && !deferInline)
        {
            DrainSourceUpdates(dispatcher, attachmentVersion);
            return;
        }

        // Unlike DrainSourceUpdates' own self-repost above, this call runs from an arbitrary
        // background thread - whatever thread raised the source's PropertyChanged or
        // CollectionChanged notification - with no synchronous caller ever able to observe a
        // thrown exception and no dispatcher work item wrapping this call to report it either.
        // A saturated (but otherwise healthy) queue here previously vanished with no signal
        // anywhere - not a target update, not Dispatcher.UnhandledException - leaving the target
        // silently stale until some unrelated later change happened to find the queue clear.
        // PostOrReportFault bridges that full-queue case into the dispatcher's own
        // callback-failure path instead, the same fire-and-forget shape
        // TreeViewItem.RunLoadAsync, FileDialogBase.ObserveLoadAsync, and
        // Application.ObserveRenderAsync/ObserveOutOfBandAsync already bridge.
        PostOrReportFault(
            dispatcher,
            () => DrainSourceUpdates(dispatcher, attachmentVersion),
            () => ClearScheduled(dispatcher, attachmentVersion));
    }

    private void ClearScheduled(Dispatcher dispatcher, long attachmentVersion)
    {
        lock (_gate)
        {
            if (ReferenceEquals(_scheduledDispatcher, dispatcher) &&
                _scheduledAttachmentVersion == attachmentVersion)
            {
                ClearScheduledCore();
            }
        }
    }

    private void ClearScheduledCore()
    {
        _sourceScheduled = false;
        _scheduledDispatcher = null;
    }

    /// <summary>Posts <paramref name="action"/> as the source-to-target drain reached from a
    /// background source-notification thread; a full bounded queue
    /// (<see cref="InvalidOperationException"/>) is bridged into the dispatcher's own
    /// callback-failure path by re-posting a callback that rethrows the caught exception, so a
    /// failure originating off the dispatcher thread is reported through
    /// <see cref="Dispatcher.UnhandledException"/> exactly like one thrown by a callback already
    /// running on it - the same bridge <c>TreeViewItem.RunLoadAsync</c>,
    /// <c>FileDialogBase.ObserveLoadAsync</c>, and
    /// <c>Application.ObserveRenderAsync</c>/<c>ObserveOutOfBandAsync</c> use for their own
    /// fire-and-forget completion posts. <paramref name="onNotScheduled"/> runs whenever
    /// <paramref name="action"/> itself will never run - a disposed dispatcher, a full queue on
    /// both this attempt and the bridging retry, or a full queue on this attempt whose retry only
    /// ever queues the rethrow rather than the real drain - so a caller can release bookkeeping
    /// (such as <see cref="_sourceScheduled"/>) that would otherwise wrongly believe a drain is
    /// still pending forever.</summary>
    /// <param name="dispatcher">The target dispatcher.</param>
    /// <param name="action">The drain callback to post.</param>
    /// <param name="onNotScheduled">Runs when <paramref name="action"/> will never run.</param>
    private void PostOrReportFault(Dispatcher dispatcher, Action action, Action onNotScheduled)
    {
        try
        {
            dispatcher.Post(action);
            return;
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException exception)
        {
            PostRetryHookForTests?.Invoke();

            try
            {
                dispatcher.Post(() => throw exception);
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        onNotScheduled();
    }

    /// <summary>
    /// Test-only synchronization seam. When set, invoked once by <see cref="PostOrReportFault"/>
    /// immediately after a first <see cref="Dispatcher.Post(Action)"/> attempt is rejected for a
    /// full queue, but before the bridging retry attempt - letting a test deterministically free
    /// the queue slot the retry needs in the otherwise nanosecond-wide window between the two
    /// attempts, rather than racing a genuine drain. Instance-scoped, like the analogous seams on
    /// <c>TreeViewItem</c>, <c>FileDialogBase</c>, and <c>Application</c>.
    /// </summary>
    internal Action? PostRetryHookForTests { get; set; }

    private void OnTargetPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        _ = sender;

        if (!_registry.IsTargetUpdateActive &&
            (string.IsNullOrEmpty(eventArgs.PropertyName) ||
             string.Equals(eventArgs.PropertyName, TargetPropertyName, StringComparison.Ordinal)))
        {
            ApplyTargetToSource();
        }
    }
}
