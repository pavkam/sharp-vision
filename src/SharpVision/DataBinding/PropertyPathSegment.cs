// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.DataBinding;

/// <summary>Stores compiled access to one public instance property in a binding path.</summary>
internal sealed class PropertyPathSegment
{
    /// <summary>Initializes one compiled property segment.</summary>
    public PropertyPathSegment(
        PropertyInfo property,
        Func<object, object?> getter,
        Action<object, object?>? setter)
    {
        ArgumentNullException.ThrowIfNull(property);
        ArgumentNullException.ThrowIfNull(getter);
        Property = property;
        Getter = getter;
        Setter = setter;
    }

    /// <summary>Gets the reflected property used only for declaration metadata.</summary>
    public PropertyInfo Property { get; }

    /// <summary>Gets the cached property reader.</summary>
    public Func<object, object?> Getter { get; }

    /// <summary>Gets the cached property writer, or null for a read-only property.</summary>
    public Action<object, object?>? Setter { get; }
}
