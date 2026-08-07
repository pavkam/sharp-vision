// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.DataBinding;

using System.Collections.ObjectModel;
using System.ComponentModel;

/// <summary>Provides observable chart series for public binding tests.</summary>
internal sealed class ChartBindingModel: INotifyPropertyChanged
{
    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets or sets the observable chart series source.</summary>
    public IReadOnlyList<ChartSeries>? Series
    {
        get;
        set
        {
            if (ReferenceEquals(field, value))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Series)));
        }
    } = new ObservableCollection<ChartSeries>();

    /// <summary>Gets the current source as its mutable observable implementation.</summary>
    internal ObservableCollection<ChartSeries> ObservableSeries => (ObservableCollection<ChartSeries>) Series!;
}
