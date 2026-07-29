// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using System.ComponentModel;

/// <summary>Provides one replaceable nested address for the data-binding showcase.</summary>
public sealed class DataBindingShowcaseAddress: INotifyPropertyChanged
{
    /// <summary>Raised after the city commits.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets or sets the optional city.</summary>
    public string? City
    {
        get;
        set
        {
            if (string.Equals(field, value, StringComparison.Ordinal))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(City)));
        }
    }
}
