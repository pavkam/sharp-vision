// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Charts;

using System.ComponentModel;

using SharpVision.DataBinding;

/// <summary>Represents one observable labeled finite value in a chart series.</summary>
[PublicAPI]
public sealed class ChartDataPoint: INotifyPropertyChanged
{
    private string _label;
    private double _value;

    /// <summary>Initializes one labeled finite chart value.</summary>
    /// <param name="label">The non-null category label.</param>
    /// <param name="value">The finite numeric value.</param>
    /// <exception cref="ArgumentNullException"><paramref name="label"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not finite.</exception>
    public ChartDataPoint(string label, double value)
    {
        ArgumentNullException.ThrowIfNull(label);
        ArgumentOutOfRangeException.ThrowIfNotFinite(value, nameof(value), "Chart values must be finite.");
        _label = label;
        _value = value;
    }

    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets or sets the category label used by axes and value descriptions.</summary>
    /// <exception cref="ArgumentNullException">The assigned value is null.</exception>
    public string Label
    {
        get => _label;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (!PropertyChangeSupport.SetField(ref _label, value, PropertyChanged, this))
            {
                return;
            }
        }
    }

    /// <summary>Gets or sets the finite numeric value represented by this point.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The assigned value is not finite.</exception>
    public double Value
    {
        get => _value;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNotFinite(value, nameof(value), "Chart values must be finite.");

            if (!PropertyChangeSupport.SetField(ref _value, value, PropertyChanged, this))
            {
                return;
            }
        }
    }

    /// <summary>Gets or sets the optional point color overriding its series color.</summary>
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
}
