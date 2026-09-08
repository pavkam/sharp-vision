// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using System.ComponentModel;
using System.Runtime.ExceptionServices;

/// <summary>Forwards one typed retained-part property through its semantic owner.</summary>
/// <typeparam name="T">The forwarded property value type.</typeparam>
/// <remarks>
/// Obtained only through <see cref="ControlBase.RegisterRetainedPartProperty{T}"/>: a derived
/// control owning one private retained descendant asks its base for a bridge for one property, then
/// exposes this bridge's <see cref="Value"/> under its own semantic property name. The bridge walks
/// and subscribes to every control on the ownership path between the source and the owner, so it
/// disposes itself the moment any control on that path changes parent - the source has left the
/// owner's retained tree and the forwarding relationship it depended on no longer holds.
/// </remarks>
[PublicAPI]
public sealed class RetainedPartProperty<T>: IDisposable
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
    /// <param name="owner">The non-null semantic owner republishing this bridge's value.</param>
    /// <param name="source">The non-null retained descendant already owned by <paramref name="owner"/>.</param>
    /// <param name="sourcePropertyName">The non-empty source property name observed for change.</param>
    /// <param name="ownerPropertyName">The non-empty owner property name published on change.</param>
    /// <param name="get">The non-null delegate reading the current source value.</param>
    /// <param name="set">The optional delegate writing the source value; null makes <see cref="Value"/> read-only.</param>
    /// <param name="comparer">The optional equality comparer; null uses <see cref="EqualityComparer{T}.Default"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/>, <paramref name="source"/>, or <paramref name="get"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="sourcePropertyName"/> or <paramref name="ownerPropertyName"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="source"/> is not an owned descendant of <paramref name="owner"/>.</exception>
    internal RetainedPartProperty(
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
    /// <exception cref="InvalidOperationException">
    /// The bridge was constructed without a setter delegate, or the owning control is mutated
    /// off-dispatcher.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The owner or source is disposed.</exception>
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
    /// <remarks>
    /// Some sources commit a change and raise a domain event - such as
    /// <see cref="Container.ScrollChanged"/> - without also raising
    /// <see cref="INotifyPropertyChanged.PropertyChanged"/> for every affected property. A bridge
    /// wired to such a property is refreshed explicitly from that domain event instead of relying on
    /// the automatic <see cref="INotifyPropertyChanged.PropertyChanged"/> subscription.
    /// </remarks>
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
