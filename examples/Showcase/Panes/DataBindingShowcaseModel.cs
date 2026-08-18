// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

/// <summary>Provides nested scalar and observable collection state for the binding page.</summary>
internal sealed class DataBindingShowcaseModel: INotifyPropertyChanged
{
    /// <summary>Raised after one scalar property commits.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets or sets the replaceable nested address.</summary>
    public DataBindingShowcaseAddress? Address
    {
        get;
        set
        {
            if (ReferenceEquals(field, value))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Address)));
        }
    }

    /// <summary>Gets or sets the nullable enabled state.</summary>
    public bool? IsEnabled
    {
        get;
        set
        {
            if (!PropertyChangeSupport.SetField(ref field, value, PropertyChanged, this))
            {
                return;
            }
        }
    }

    /// <summary>Gets or sets the source-owned one-way text.</summary>
    public string? OneWayText
    {
        get;
        set
        {
            if (!PropertyChangeSupport.SetField(ref field, value, PropertyChanged, this))
            {
                return;
            }
        }
    }

    /// <summary>Gets or sets the shared two-way text.</summary>
    public string? TwoWayText
    {
        get;
        set
        {
            if (!PropertyChangeSupport.SetField(ref field, value, PropertyChanged, this))
            {
                return;
            }
        }
    }

    /// <summary>Gets or sets the target-owned one-way-to-source text.</summary>
    public string? OneWayToSourceText
    {
        get;
        set
        {
            if (!PropertyChangeSupport.SetField(ref field, value, PropertyChanged, this))
            {
                return;
            }
        }
    }

    /// <summary>Gets or sets the radio-button state.</summary>
    public bool IsPrimary
    {
        get;
        set
        {
            if (!PropertyChangeSupport.SetField(ref field, value, PropertyChanged, this))
            {
                return;
            }
        }
    }

    /// <summary>Gets or sets the shared integral range value.</summary>
    public int NumericValue
    {
        get;
        set
        {
            if (!PropertyChangeSupport.SetField(ref field, value, PropertyChanged, this))
            {
                return;
            }
        }
    }

    /// <summary>Gets or sets the one-way progress value.</summary>
    public double ProgressValue
    {
        get;
        set
        {
            if (!PropertyChangeSupport.SetField(ref field, value, PropertyChanged, this))
            {
                return;
            }
        }
    }

    /// <summary>Gets or sets the selected RGB color.</summary>
    public Color SelectedColor
    {
        get;
        set
        {
            if (!PropertyChangeSupport.SetField(ref field, value, PropertyChanged, this))
            {
                return;
            }
        }
    }

    /// <summary>Gets or sets the selected-index adapter value.</summary>
    public int SelectedIndex
    {
        get;
        set
        {
            if (!PropertyChangeSupport.SetField(ref field, value, PropertyChanged, this))
            {
                return;
            }
        }
    }

    /// <summary>Gets or sets the one-way display text.</summary>
    public string? StatusText
    {
        get;
        set
        {
            if (!PropertyChangeSupport.SetField(ref field, value, PropertyChanged, this))
            {
                return;
            }
        }
    }

    /// <summary>Gets or sets the command borrowed by the bound button.</summary>
    public ICommand? Command
    {
        get;
        set
        {
            if (ReferenceEquals(field, value))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Command)));
        }
    }

    /// <summary>Gets or sets the borrowed command parameter.</summary>
    public object? CommandParameter
    {
        get;
        set
        {
            if (!PropertyChangeSupport.SetField(ref field, value, PropertyChanged, this))
            {
                return;
            }
        }
    }

    /// <summary>Gets the live observable item collection.</summary>
    public ObservableCollection<DataBindingShowcaseItem> Items { get; } = [];

    /// <summary>Gets or sets the selected item identity.</summary>
    public DataBindingShowcaseItem? SelectedItem
    {
        get;
        set
        {
            if (ReferenceEquals(field, value))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedItem)));
        }
    }
}
