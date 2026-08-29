// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Stores the latest caller-authored value for one leased control property.</summary>
internal sealed class RetainedPropertyOverrideEntry
{
    /// <summary>Initializes one entry by capturing the control's current authored value.</summary>
    /// <param name="descriptor">The typed property accessors.</param>
    /// <param name="control">The non-null newly leased control.</param>
    internal RetainedPropertyOverrideEntry(RetainedPropertyOverrideDescriptor descriptor, ControlBase control)
    {
        Descriptor = descriptor;
        AuthoredValue = descriptor.Read(control);
    }

    /// <summary>Gets the typed accessors for this property.</summary>
    internal RetainedPropertyOverrideDescriptor Descriptor { get; }

    /// <summary>Gets or sets the latest boxed caller-authored value.</summary>
    internal object AuthoredValue { get; set; }
}
