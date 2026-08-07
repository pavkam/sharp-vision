// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Charts;

using System.ComponentModel;

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
        ValidateFinite(value, nameof(value));
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

            if (_label == value)
            {
                return;
            }

            _label = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Label)));
        }
    }

    /// <summary>Gets or sets the finite numeric value represented by this point.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The assigned value is not finite.</exception>
    public double Value
    {
        get => _value;
        set
        {
            ValidateFinite(value, nameof(value));

            if (_value.Equals(value))
            {
                return;
            }

            _value = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
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

            if (field == value)
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Color)));
        }
    }

    private static void ValidateFinite(double value, string parameterName)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Chart values must be finite.");
        }
    }
}
