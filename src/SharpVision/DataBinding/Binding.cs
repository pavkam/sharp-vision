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
                _collectionObserver = new CollectionObserver(OnSourceInvalidated);
            }

            if (Mode is BindingMode.OneWay or BindingMode.TwoWay)
            {
                _sourceObserver = new PropertyPathObserver(_source, _sourcePath, OnSourceInvalidated);
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

        if (TryApplyIncrementalChange())
        {
            return;
        }

        var available = _sourcePath.TryRead(_source, out var sourceValue);
        _collectionObserver?.Observe(available ? sourceValue : null);
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
    }

    private bool TryApplyIncrementalChange()
    {
        if (_collectionObserver is not { } observer ||
            _applyIncrementalChange is not { } applyIncremental ||
            !observer.TryTakePendingChange(out var change))
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

    private void DrainSourceUpdates(Dispatcher dispatcher)
    {
        Debug.Assert(dispatcher.CheckAccess(), "A posted binding update runs on its target dispatcher.");

        try
        {
            for (var pass = 0; pass < 2; pass++)
            {
                lock (_gate)
                {
                    if (IsDisposed)
                    {
                        _sourceScheduled = false;
                        return;
                    }

                    _sourceDirty = false;
                }

                ApplySourceToTarget();

                lock (_gate)
                {
                    if (!_sourceDirty)
                    {
                        _sourceScheduled = false;
                        return;
                    }
                }
            }
        }
        catch
        {
            lock (_gate)
            {
                _sourceScheduled = false;
            }

            throw;
        }

        // A target whose dispatcher is stopping or whose queue is momentarily full is going
        // away regardless; drop the pending update instead of rethrowing into this dispatcher
        // work item, matching DispatcherSynchronizationContext.Post's contract for the same
        // underlying race.
        try
        {
            dispatcher.Post(() => DrainSourceUpdates(dispatcher));
        }
        catch (Exception exception) when (exception is ObjectDisposedException or InvalidOperationException)
        {
            lock (_gate)
            {
                _sourceScheduled = false;
            }
        }
    }

    private void OnSourceInvalidated()
    {
        var dispatcher = Target.Dispatcher;

        if (dispatcher is null || dispatcher.CheckAccess())
        {
            ApplySourceToTarget();
            return;
        }

        lock (_gate)
        {
            if (IsDisposed)
            {
                return;
            }

            _sourceDirty = true;

            if (_sourceScheduled)
            {
                return;
            }

            _sourceScheduled = true;
        }

        // A dispatcher that is stopping or whose queue is momentarily full is going away
        // regardless; drop the pending update instead of rethrowing into whatever thread
        // mutated the source model — that thread never expected binding internals to fail.
        try
        {
            dispatcher.Post(() => DrainSourceUpdates(dispatcher));
        }
        catch (Exception exception) when (exception is ObjectDisposedException or InvalidOperationException)
        {
            lock (_gate)
            {
                _sourceScheduled = false;
            }
        }
    }

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
