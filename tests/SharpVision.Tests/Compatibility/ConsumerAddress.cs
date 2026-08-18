// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Compatibility;

using System.ComponentModel;

/// <summary>Provides one public notifying nested value to consumer-facing compatibility tests.</summary>
public sealed class ConsumerAddress: INotifyPropertyChanged
{
    /// <summary>Raised after a value commits.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets or sets the city.</summary>
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
