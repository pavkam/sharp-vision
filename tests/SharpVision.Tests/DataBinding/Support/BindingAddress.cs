// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.DataBinding.Support;

using System.ComponentModel;

/// <summary>Provides one nested notifying address for property-path tests.</summary>
internal sealed class BindingAddress: INotifyPropertyChanged
{
    /// <summary>Raised after a property commits.</summary>
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

    /// <summary>Gets or sets a mutable value-type coordinate, for property-path validation tests.</summary>
    public BindingLocation Location { get; set; }

    /// <summary>Gets the number of currently subscribed PropertyChanged handlers, for leak assertions.</summary>
    internal int PropertyChangedSubscriberCount => PropertyChanged?.GetInvocationList().Length ?? 0;
}
