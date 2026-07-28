// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Associates one mounted test method with the concrete behavior it proves.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
internal sealed class ComponentBehaviorEvidenceAttribute: Attribute
{
    /// <summary>Initializes evidence for one concrete control and non-empty behavior set.</summary>
    /// <param name="controlType">The concrete exported control type.</param>
    /// <param name="behaviors">The non-empty behaviors proved by the annotated method.</param>
    /// <exception cref="ArgumentNullException"><paramref name="controlType"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="controlType"/> is not a concrete control.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="behaviors"/> is empty or contains undefined flags.</exception>
    internal ComponentBehaviorEvidenceAttribute(Type controlType, ComponentBehavior behaviors)
    {
        ArgumentNullException.ThrowIfNull(controlType);

        if (controlType.IsAbstract || !typeof(Control).IsAssignableFrom(controlType))
        {
            throw new ArgumentException("Behavior evidence requires a concrete Control type.", nameof(controlType));
        }

        if (behaviors == ComponentBehavior.None || (behaviors & ~ComponentBehaviorRequirement.AllBehaviors) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(behaviors), behaviors,
                "Behavior evidence must contain defined flags.");
        }

        ControlType = controlType;
        Behaviors = behaviors;
    }

    /// <summary>Gets the concrete control whose behavior is proved.</summary>
    internal Type ControlType { get; }

    /// <summary>Gets the behavior flags proved by the annotated test.</summary>
    internal ComponentBehavior Behaviors { get; }
}
