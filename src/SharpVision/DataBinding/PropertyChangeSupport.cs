// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.DataBinding;

using System.ComponentModel;

/// <summary>Provides the shared default-equality property-setter idiom used by observable models.</summary>
internal static class PropertyChangeSupport
{
    /// <summary>Assigns a field and raises <paramref name="handler"/> unless the value is unchanged.</summary>
    /// <returns><see langword="true"/> if the field changed and the notification was raised.</returns>
    internal static bool SetField<T>(
        ref T field,
        T value,
        PropertyChangedEventHandler? handler,
        object sender,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        handler?.Invoke(sender, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
