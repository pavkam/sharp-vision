// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Associates one detached unit test method with the concrete control it proves.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
internal sealed class ComponentUnitEvidenceAttribute: Attribute
{
    /// <summary>Initializes evidence for one concrete control.</summary>
    /// <param name="controlType">The concrete exported control type.</param>
    /// <exception cref="ArgumentNullException"><paramref name="controlType"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="controlType"/> is not a concrete control.</exception>
    internal ComponentUnitEvidenceAttribute(Type controlType)
    {
        ArgumentNullException.ThrowIfNull(controlType);

        if (controlType.IsAbstract || !typeof(Control).IsAssignableFrom(controlType))
        {
            throw new ArgumentException("Unit evidence requires a concrete Control type.", nameof(controlType));
        }

        ControlType = controlType;
    }

    /// <summary>Gets the concrete control proved by the annotated test.</summary>
    internal Type ControlType { get; }
}
