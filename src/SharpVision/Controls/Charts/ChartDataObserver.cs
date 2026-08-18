// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Charts;

using System.Collections.Specialized;
using System.ComponentModel;

/// <summary>Owns one chart's subscriptions to borrowed observable model data.</summary>
internal sealed class ChartDataObserver: IDisposable
{
    private readonly Lock _gate = new();
    private readonly IChartControl _owner;
    private readonly List<ChartDataPoint> _subscribedPoints = [];
    private bool _disposed;
    private bool _scheduled;
    private IReadOnlyList<ChartSeries> _source = Array.Empty<ChartSeries>();
    private INotifyCollectionChanged? _observableSource;
    private ChartSeries[] _snapshot = [];

    /// <summary>Initializes observation for one non-null chart owner.</summary>
    internal ChartDataObserver(IChartControl owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
    }

    /// <summary>Gets the last atomically validated series snapshot.</summary>
    internal IReadOnlyList<ChartSeries> Current => _snapshot;

    /// <summary>Replaces the borrowed source after validating its current membership.</summary>
    internal void Replace(IReadOnlyList<ChartSeries> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var snapshot = Snapshot(source);

        if (ReferenceEquals(_source, source) && SnapshotEquals(_snapshot, snapshot))
        {
            return;
        }

        Unsubscribe();
        _source = source;
        _snapshot = snapshot;
        Subscribe();
        _owner.OnChartDataChanged(InvalidationImpact.Measure, seriesMembershipChanged: true);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Unsubscribe();
        _source = Array.Empty<ChartSeries>();
        _snapshot = [];
    }

    private ChartSeries[] Snapshot(IReadOnlyList<ChartSeries> source)
    {
        var snapshot = new ChartSeries[source.Count];

        for (var index = 0; index < snapshot.Length; index++)
        {
            var series = source[index];
            ArgumentNullException.ThrowIfNull(series);

            for (var previous = 0; previous < index; previous++)
            {
                if (ReferenceEquals(snapshot[previous], series))
                {
                    throw new ArgumentException("A chart cannot contain the same series reference twice.", nameof(source));
                }
            }

            snapshot[index] = series;
        }

        _owner.ValidateSeries(snapshot);
        return snapshot;
    }

    private static bool SnapshotEquals(ChartSeries[] left, ChartSeries[] right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (var index = 0; index < left.Length; index++)
        {
            if (!ReferenceEquals(left[index], right[index]))
            {
                return false;
            }
        }

        return true;
    }

    private void Subscribe()
    {
        _observableSource = _source as INotifyCollectionChanged;

        if (_observableSource is { } observableSource)
        {
            observableSource.CollectionChanged += OnSourceCollectionChanged;
        }

        foreach (var series in _snapshot)
        {
            series.PropertyChanged += OnSeriesPropertyChanged;
            series.Points.CollectionChanged += OnPointCollectionChanged;

            foreach (var point in series.Points)
            {
                point.PropertyChanged += OnPointPropertyChanged;
                _subscribedPoints.Add(point);
            }
        }
    }

    private void Unsubscribe()
    {
        if (_observableSource is { } observableSource)
        {
            observableSource.CollectionChanged -= OnSourceCollectionChanged;
            _observableSource = null;
        }

        foreach (var series in _snapshot)
        {
            series.PropertyChanged -= OnSeriesPropertyChanged;
            series.Points.CollectionChanged -= OnPointCollectionChanged;

        }

        foreach (var point in _subscribedPoints)
        {
            point.PropertyChanged -= OnPointPropertyChanged;
        }

        _subscribedPoints.Clear();
    }

    private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        Schedule(refreshMembership: true, InvalidationImpact.Measure);
    }

    private void OnPointCollectionChanged(object? sender, NotifyCollectionChangedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        Schedule(refreshMembership: true, InvalidationImpact.Measure);
    }

    private void OnSeriesPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        _ = sender;
        Schedule(
            refreshMembership: false,
            eventArgs.PropertyName == nameof(ChartSeries.Name) ? InvalidationImpact.Measure : InvalidationImpact.Render);
    }

    private void OnPointPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        _ = sender;
        Schedule(
            refreshMembership: false,
            eventArgs.PropertyName == nameof(ChartDataPoint.Label)
                ? InvalidationImpact.Measure
                : InvalidationImpact.Render);
    }

    private void Schedule(bool refreshMembership, InvalidationImpact impact)
    {
        if (_disposed)
        {
            return;
        }

        var dispatcher = _owner.Control.Dispatcher;

        if (dispatcher is null || dispatcher.CheckAccess())
        {
            Apply(refreshMembership, impact);
            return;
        }

        lock (_gate)
        {
            if (_disposed || _scheduled)
            {
                return;
            }

            _scheduled = true;
        }

        try
        {
            // Reached from the caller's own INotifyCollectionChanged/INotifyPropertyChanged
            // handlers on their model, possibly off-thread, as one subscriber in that model's
            // multicast invocation list. _scheduled is reset in the catch below, so a swallowed
            // full queue only means this refresh is dropped and self-heals on the model's next
            // change notification; propagating would fault the caller's own event dispatch and
            // could break delivery to its other subscribers, which is worse than one stale chart.
            dispatcher.Post(() =>
            {
                lock (_gate)
                {
                    _scheduled = false;
                }

                if (!_disposed)
                {
                    Apply(refreshMembership: true, InvalidationImpact.Measure);
                }
            });
        }
        catch (Exception exception) when (exception is ObjectDisposedException or InvalidOperationException)
        {
            lock (_gate)
            {
                _scheduled = false;
            }
        }
    }

    private void Apply(bool refreshMembership, InvalidationImpact impact)
    {
        if (refreshMembership)
        {
            ChartSeries[] snapshot;

            try
            {
                snapshot = Snapshot(_source);
            }
            catch (Exception exception) when (exception is ArgumentException or ArgumentNullException)
            {
                return;
            }

            Unsubscribe();
            _snapshot = snapshot;
            Subscribe();
        }

        _owner.OnChartDataChanged(impact, refreshMembership);
    }
}
