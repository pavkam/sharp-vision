// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Captures typed accessors for one property controlled by a retained-owner lease.</summary>
internal readonly struct RetainedPropertyOverrideDescriptor
{
    private readonly Func<ControlBase, object> _read;
    private readonly Action<ControlBase, object> _write;

    private RetainedPropertyOverrideDescriptor(
        RetainedControlProperty property,
        Type valueType,
        Func<ControlBase, object> read,
        Action<ControlBase, object> write)
    {
        Property = property;
        ValueType = valueType;
        _read = read;
        _write = write;
    }

    /// <summary>Gets the intercepted property identity.</summary>
    internal RetainedControlProperty Property { get; }

    /// <summary>Gets the exact property value type.</summary>
    internal Type ValueType { get; }

    /// <summary>Creates a descriptor whose accessors retain their compile-time value type.</summary>
    /// <typeparam name="T">The exact property value type.</typeparam>
    /// <param name="property">The property identity.</param>
    /// <param name="read">The non-null typed reader.</param>
    /// <param name="write">The non-null typed writer.</param>
    /// <returns>The boxed storage descriptor used by heterogeneous leases.</returns>
    internal static RetainedPropertyOverrideDescriptor Create<T>(
        RetainedControlProperty property,
        Func<ControlBase, T> read,
        Action<ControlBase, T> write)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(write);
        return new RetainedPropertyOverrideDescriptor(
            property,
            typeof(T),
            control => read(control),
            (control, value) => write(control, (T) value));
    }

    /// <summary>Reads the current live value from one control.</summary>
    /// <param name="control">The non-null leased control.</param>
    /// <returns>The boxed typed value.</returns>
    internal object Read(ControlBase control) => _read(control);

    /// <summary>Writes one validated live value to a leased control.</summary>
    /// <param name="control">The non-null leased control.</param>
    /// <param name="value">The boxed value of <see cref="ValueType"/>.</param>
    internal void Write(ControlBase control, object value) => _write(control, value);
}
