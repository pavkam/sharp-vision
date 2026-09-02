// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using System.ComponentModel;
using System.Runtime.ExceptionServices;

/// <summary>Forwards one typed retained-part property through its semantic owner.</summary>
internal sealed class RetainedPartProperty<T>: IDisposable
{
    private readonly IEqualityComparer<T> _comparer;
    private readonly Func<T> _get;
    private readonly ControlBase[] _ownershipPath;
    private readonly ControlBase _owner;
    private readonly string _ownerPropertyName;
    private readonly Action<T>? _set;
    private readonly ControlBase _source;
    private readonly string _sourcePropertyName;
    private T _observed;
    private bool _isDisposed;
    private long _sourceVersion;

    /// <summary>Initializes and subscribes one current-value forwarding relationship.</summary>
    public RetainedPartProperty(
        ControlBase owner,
        ControlBase source,
        string sourcePropertyName,
        string ownerPropertyName,
        Func<T> get,
        Action<T>? set = null,
        IEqualityComparer<T>? comparer = null)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePropertyName);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerPropertyName);
        ArgumentNullException.ThrowIfNull(get);
        _owner = owner;
        _source = source;
        _sourcePropertyName = sourcePropertyName;
        _ownerPropertyName = ownerPropertyName;
        _get = get;
        _set = set;
        _comparer = comparer ?? EqualityComparer<T>.Default;
        _observed = get();
        List<ControlBase> ownershipPath = [];

        var current = source;

        while (!ReferenceEquals(current, owner))
        {
            ownershipPath.Add(current);
            current = current.Parent ?? throw new InvalidOperationException(
                "A retained-part bridge requires an owned descendant.");
        }

        _ownershipPath = [.. ownershipPath];
        source.PropertyChanged += OnSourcePropertyChanged;

        foreach (var control in _ownershipPath)
        {
            control.ParentChanged += OnOwnershipPathChanged;
        }
    }

    /// <summary>Gets or sets the current retained-part value.</summary>
    public T Value
    {
        get => _get();
        set
        {
            if (_set is null)
            {
                throw new InvalidOperationException("The forwarded retained-part property is read-only.");
            }

            _owner.VerifyMutable();

            if (_comparer.Equals(_get(), value))
            {
                return;
            }

            var version = _sourceVersion;
            ExceptionDispatchInfo? failure = null;
            ExceptionAggregation.Capture(() => _set(value), ref failure);
            ExceptionAggregation.Capture(() => Refresh(version), ref failure);
            failure?.Throw();
        }
    }

    /// <summary>Refreshes a value whose source reports change through a non-property event.</summary>
    public void Refresh() => Refresh(_sourceVersion);

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _source.PropertyChanged -= OnSourcePropertyChanged;

        foreach (var control in _ownershipPath)
        {
            control.ParentChanged -= OnOwnershipPathChanged;
        }
    }

    private void OnOwnershipPathChanged(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        Dispose();
    }

    // Trusts the source's own notification rather than re-deriving "did it change" from T's
    // equality: for a style-valued T (e.g. CalendarStyle), the value can hold an unresolved
    // semantic color token (SemanticColor.Accent) that compares equal before and after a theme
    // swap even though the source's own theme-aware invalidation logic already decided the swap
    // was meaningful enough to raise its PropertyChanged. Re-checking with the default T comparer
    // here would silently swallow exactly that class of change. The explicit Value setter and the
    // external no-arg Refresh() below are different: they have no such authoritative signal to
    // trust, so they keep the equality-gated Refresh(long) path.
    private void OnSourcePropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.PropertyName is null ||
            string.Equals(eventArgs.PropertyName, _sourcePropertyName, StringComparison.Ordinal))
        {
            _sourceVersion++;
            var version = _sourceVersion;
            _observed = _get();

            if (_sourceVersion == version)
            {
                _owner.NotifyRetainedPartPropertyChanged(_ownerPropertyName);
            }
        }
    }

    private void Refresh(long version)
    {
        var current = _get();

        if (_comparer.Equals(_observed, current))
        {
            return;
        }

        _observed = current;

        if (_sourceVersion == version)
        {
            _owner.NotifyRetainedPartPropertyChanged(_ownerPropertyName);
        }
    }
}
