// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Charts;

using System.ComponentModel;

using SharpVision.DataBinding;

/// <summary>Represents one observable named and optionally colored chart series.</summary>
[PublicAPI]
public sealed class ChartSeries: INotifyPropertyChanged
{
    private string _name;

    /// <summary>Initializes an empty named series.</summary>
    /// <param name="name">The non-null legend name.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    public ChartSeries(string name) : this(name, [])
    {
    }

    /// <summary>Initializes a named series containing the supplied points.</summary>
    /// <param name="name">The non-null legend name.</param>
    /// <param name="points">The validated points to copy.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/>, <paramref name="points"/>, or one point is null.</exception>
    /// <exception cref="ArgumentException">One point reference is repeated.</exception>
    public ChartSeries(string name, IEnumerable<ChartDataPoint> points)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(points);
        _name = name;
        Points = new ChartDataPointCollection(points);
    }

    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets or sets the non-null legend name.</summary>
    /// <exception cref="ArgumentNullException">The assigned value is null.</exception>
    public string Name
    {
        get => _name;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (!PropertyChangeSupport.SetField(ref _name, value, PropertyChanged, this))
            {
                return;
            }
        }
    }

    /// <summary>Gets or sets the optional series color used when a point has no override.</summary>
    /// <exception cref="ArgumentException">The assigned color is transparent.</exception>
    public ControlColor? Color
    {
        get;
        set
        {
            if (value is { } color)
            {
                ControlColor.ValidatePaint(color, nameof(value));
            }

            if (!PropertyChangeSupport.SetField(ref field, value, PropertyChanged, this))
            {
                return;
            }
        }
    }

    /// <summary>Gets or sets the optional dash pattern that overrides the style's default for
    /// this series when the chart draws through the quadrant line renderer.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The assigned value is unknown.</exception>
    public LinePattern? LinePattern
    {
        get;
        set
        {
            if (value is { } pattern)
            {
                ArgumentOutOfRangeException.ThrowIfNotDefined(pattern, nameof(value), "The line pattern is unknown.");
            }

            if (!PropertyChangeSupport.SetField(ref field, value, PropertyChanged, this))
            {
                return;
            }
        }
    }

    /// <summary>Gets the retained observable point collection owned by this series.</summary>
    public ChartDataPointCollection Points { get; }
}
