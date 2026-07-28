// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.DataBinding.Support;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

/// <summary>Provides deterministic notifying values for data-binding tests.</summary>
internal sealed class BindingModel: INotifyPropertyChanged
{
    /// <summary>Raised after a model property commits.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets or sets the notifying optional name.</summary>
    public string? Name
    {
        get;
        set
        {
            if (string.Equals(field, value, StringComparison.Ordinal))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
        }
    }

    /// <summary>Gets or sets the optional nested address.</summary>
    public BindingAddress? Address
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

    /// <summary>Gets or sets a notifying nullable check state.</summary>
    public bool? Checked
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Checked)));
        }
    }

    /// <summary>Gets or sets a notifying Boolean value.</summary>
    public bool Enabled
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Enabled)));
        }
    }

    /// <summary>Gets or sets a notifying integer.</summary>
    public int Number
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Number)));
        }
    }

    /// <summary>Gets or sets a notifying floating-point value.</summary>
    public double Progress
    {
        get;
        set
        {
            if (field.Equals(value))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Progress)));
        }
    }

    /// <summary>Gets or sets a notifying terminal color.</summary>
    public Color Color
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Color)));
        }
    }

    /// <summary>Gets or sets a notifying nullable date interval.</summary>
    public DateInterval? DateSelection
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DateSelection)));
        }
    }

    /// <summary>Gets or sets a notifying expansion state.</summary>
    public bool IsExpanded
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
        }
    }

    /// <summary>Gets one deliberately read-only property.</summary>
    public string ReadOnly => Name ?? string.Empty;

    /// <summary>Gets or sets a notifying command.</summary>
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

    /// <summary>Gets or sets a notifying borrowed parameter.</summary>
    public object? Parameter
    {
        get;
        set
        {
            if (ReferenceEquals(field, value))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Parameter)));
        }
    }

    /// <summary>Gets or sets the notifying observable item collection.</summary>
    public ObservableCollection<BindingItem>? Items
    {
        get;
        set
        {
            if (ReferenceEquals(field, value))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Items)));
        }
    }

    /// <summary>Gets or sets the notifying selected item.</summary>
    public BindingItem? SelectedItem
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

    /// <summary>Raises the standard all-properties notification.</summary>
    internal void RaiseAll() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));

    /// <summary>Raises one exact property name without changing state.</summary>
    internal void Raise(string propertyName)
    {
        ArgumentException.ThrowIfNullOrEmpty(propertyName);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
