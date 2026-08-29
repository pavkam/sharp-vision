// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Provides shared typed descriptors for properties imposed by retained control owners.</summary>
internal static class RetainedPropertyOverrides
{
    /// <summary>Gets the requested-width descriptor.</summary>
    internal static RetainedPropertyOverrideDescriptor Width { get; } =
        RetainedPropertyOverrideDescriptor.Create(
            RetainedControlProperty.Width,
            static control => control.Width,
            static (control, value) => control.Width = value);

    /// <summary>Gets the requested-height descriptor.</summary>
    internal static RetainedPropertyOverrideDescriptor Height { get; } =
        RetainedPropertyOverrideDescriptor.Create(
            RetainedControlProperty.Height,
            static control => control.Height,
            static (control, value) => control.Height = value);

    /// <summary>Gets the visibility descriptor.</summary>
    internal static RetainedPropertyOverrideDescriptor Visibility { get; } =
        RetainedPropertyOverrideDescriptor.Create(
            RetainedControlProperty.Visibility,
            static control => control.Visibility,
            static (control, value) => control.Visibility = value);

    /// <summary>Gets the focusability descriptor.</summary>
    internal static RetainedPropertyOverrideDescriptor IsFocusable { get; } =
        RetainedPropertyOverrideDescriptor.Create(
            RetainedControlProperty.IsFocusable,
            static control => control.IsFocusable,
            static (control, value) => control.IsFocusable = value);

    /// <summary>Gets the tab-stop descriptor.</summary>
    internal static RetainedPropertyOverrideDescriptor IsTabStop { get; } =
        RetainedPropertyOverrideDescriptor.Create(
            RetainedControlProperty.IsTabStop,
            static control => control.IsTabStop,
            static (control, value) => control.IsTabStop = value);
}
