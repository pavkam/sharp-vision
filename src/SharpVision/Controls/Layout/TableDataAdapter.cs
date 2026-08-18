// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Layout;

/// <summary>Type-erases one <see cref="ITableDataSource{T}"/> and <see cref="TableRowTemplate{T}"/>
/// pair so <see cref="TableDataController"/> can drive progressive loading without a generic
/// parameter of its own.</summary>
internal abstract class TableDataAdapter
{
    /// <summary>Gets the source's currently known total item count, or null while unknown.</summary>
    public abstract int? Count { get; }

    /// <summary>Raised when the underlying source signals previously loaded data may be stale.</summary>
    public abstract event EventHandler? Changed;

    /// <summary>Gets one stable key for a boxed item previously returned by <see cref="LoadAsync"/>.</summary>
    /// <param name="item">The boxed item.</param>
    /// <returns>The non-null stable key.</returns>
    /// <exception cref="InvalidOperationException">The source returned a null key.</exception>
    public abstract object GetKey(object item);

    /// <summary>Builds and validates one detached row for a boxed loaded item.</summary>
    /// <param name="item">The boxed item.</param>
    /// <returns>The validated detached row.</returns>
    /// <exception cref="ArgumentException">The template returned an invalid row.</exception>
    public abstract TableRow BuildRow(object item);

    /// <summary>Loads one contiguous range from the underlying source.</summary>
    /// <param name="request">The requested range.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The boxed loaded items and whether the source reached its end.</returns>
    public abstract Task<TableDataAdapterResult> LoadAsync(TableDataRequest request, CancellationToken cancellationToken);

    /// <summary>Releases the subscription installed on the underlying source.</summary>
    public abstract void Detach();
}

/// <summary>Carries one type-erased <see cref="ITableDataSource{T}.LoadAsync"/> response.</summary>
/// <param name="Items">The boxed loaded items, in source order.</param>
/// <param name="IsEndOfData">Whether no further items exist beyond the end of <paramref name="Items"/>.</param>
internal readonly record struct TableDataAdapterResult(IReadOnlyList<object?> Items, bool IsEndOfData);

/// <summary>Binds one concrete <see cref="ITableDataSource{T}"/> and <see cref="TableRowTemplate{T}"/>
/// pair to the type-erased <see cref="TableDataAdapter"/> contract.</summary>
/// <typeparam name="T">The concrete item type.</typeparam>
internal sealed class TableDataAdapter<T>: TableDataAdapter
{
    private readonly Table _owner;
    private readonly ITableDataSource<T> _source;
    private readonly TableRowTemplate<T> _template;
    private EventHandler? _changed;

    /// <summary>Initializes an adapter bound to one owning table.</summary>
    /// <param name="owner">The owning table.</param>
    /// <param name="source">The non-null data source.</param>
    /// <param name="template">The non-null row template.</param>
    public TableDataAdapter(Table owner, ITableDataSource<T> source, TableRowTemplate<T> template)
    {
        _owner = owner;
        _source = source;
        _template = template;
        _source.Changed += OnSourceChanged;
    }

    /// <inheritdoc/>
    public override int? Count => _source.Count;

    /// <inheritdoc/>
    public override event EventHandler? Changed
    {
        add => _changed += value;
        remove => _changed -= value;
    }

    /// <inheritdoc/>
    public override object GetKey(object item) =>
        _source.GetKey((T) item) ??
        throw new InvalidOperationException("ITableDataSource<T>.GetKey returned a null key.");

    /// <inheritdoc/>
    public override TableRow BuildRow(object item)
    {
        _owner.Dispatcher?.VerifyAccess();
        var row = _template((T) item) ??
            throw new ArgumentException("TableRowTemplate<T> must return a non-null row.", nameof(item));
        _owner.ValidateProgressiveRow(row);
        return row;
    }

    /// <inheritdoc/>
    public override async Task<TableDataAdapterResult> LoadAsync(TableDataRequest request, CancellationToken cancellationToken)
    {
        var result = await _source.LoadAsync(request, cancellationToken).ConfigureAwait(false);

        if (result.Items is null)
        {
            throw new InvalidOperationException("ITableDataSource<T>.LoadAsync returned a result with a null Items list.");
        }

        var boxed = new object?[result.Items.Count];

        for (var index = 0; index < boxed.Length; index++)
        {
            boxed[index] = result.Items[index];
        }

        return new TableDataAdapterResult(boxed, result.IsEndOfData);
    }

    /// <inheritdoc/>
    public override void Detach() => _source.Changed -= OnSourceChanged;

    private void OnSourceChanged(object? sender, EventArgs eventArgs)
    {
        var dispatcher = _owner.Dispatcher;

        if (dispatcher is null)
        {
            return;
        }

        if (dispatcher.CheckAccess())
        {
            _changed?.Invoke(this, EventArgs.Empty);
            return;
        }

        // ITableDataSource<T>.Changed is a public extensibility surface a real implementer could
        // plausibly raise from a background notification (file watcher, socket callback, timer
        // thread) rather than the dispatcher thread - marshaling defensively here keeps Reload()
        // (TableDataController's own handler, subscribed indirectly through this adapter's own
        // Changed event) confined to the dispatcher thread instead of mutating cache/pending state
        // from an arbitrary one. Mirrors the same defensive Post/catch pattern
        // TableDataController.OnRetryElapsed already uses for its own off-thread timer callback.
        try
        {
            dispatcher.Post(() => _changed?.Invoke(this, EventArgs.Empty));
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }
}
