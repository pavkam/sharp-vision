// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Associates one detached unit test method with the concrete control it proves.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
internal sealed class ComponentUnitEvidenceAttribute: Attribute
{
    /// <summary>Initializes evidence for one concrete control and an optional detached behavior set.</summary>
    /// <param name="controlType">The concrete exported control type.</param>
    /// <param name="behaviors">The detached behaviors proved by the annotated method.</param>
    /// <exception cref="ArgumentNullException"><paramref name="controlType"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="controlType"/> is not a concrete control.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="behaviors"/> contains undefined flags.</exception>
    internal ComponentUnitEvidenceAttribute(Type controlType, ComponentBehavior behaviors = ComponentBehavior.None)
    {
        ArgumentNullException.ThrowIfNull(controlType);

        if (controlType.IsAbstract || !typeof(ControlBase).IsAssignableFrom(controlType))
        {
            throw new ArgumentException("Unit evidence requires a concrete Control type.", nameof(controlType));
        }

        if ((behaviors & ~ComponentBehaviorRequirement.AllBehaviors) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(behaviors), behaviors,
                "Unit evidence behaviors must contain defined flags.");
        }

        ControlType = controlType;
        Behaviors = behaviors;
    }

    /// <summary>Gets the concrete control proved by the annotated test.</summary>
    internal Type ControlType { get; }

    /// <summary>Gets the detached behavior flags proved by the annotated test.</summary>
    internal ComponentBehavior Behaviors { get; }
}
