// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Compatibility;

using System.Collections.ObjectModel;
using System.ComponentModel;

/// <summary>Provides public notifying model state without friend-assembly access.</summary>
public sealed class ConsumerModel: INotifyPropertyChanged
{
    /// <summary>Raised after a property commits.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets or sets the nested address.</summary>
    public ConsumerAddress? Address
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

    /// <summary>Gets or sets the observable items.</summary>
    public ObservableCollection<ConsumerItem>? Items
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

    /// <summary>Gets or sets a Boolean expansion state.</summary>
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

    /// <summary>Gets or sets a nullable date interval.</summary>
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

    /// <summary>Gets or sets optional display text.</summary>
    public string? DisplayText
    {
        get;
        set
        {
            if (string.Equals(field, value, StringComparison.Ordinal))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayText)));
        }
    }

    /// <summary>Gets or sets the selected item.</summary>
    public ConsumerItem? SelectedItem
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
