// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Stores one immutable fixture and behavioral requirement set.</summary>
internal readonly struct ComponentBehaviorRequirement
{
    /// <summary>Contains every defined behavior flag.</summary>
    internal const ComponentBehavior AllBehaviors = ComponentBehavior.Mounted |
        ComponentBehavior.Hover |
        ComponentBehavior.HoverExcluded |
        ComponentBehavior.Focus |
        ComponentBehavior.FocusExcluded |
        ComponentBehavior.Tab |
        ComponentBehavior.TabExcluded |
        ComponentBehavior.Directional |
        ComponentBehavior.DirectionalExcluded |
        ComponentBehavior.PressRelease |
        ComponentBehavior.PressReleaseExcluded |
        ComponentBehavior.Activation |
        ComponentBehavior.UnavailableCleanup |
        ComponentBehavior.Transient |
        ComponentBehavior.Composition;

    /// <summary>Initializes one non-null fixture and non-empty defined behavior set.</summary>
    /// <param name="fixture">The named test fixture that owns the evidence.</param>
    /// <param name="behaviors">The non-empty required behavior flags.</param>
    /// <exception cref="ArgumentNullException"><paramref name="fixture"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="fixture"/> is not a concrete class.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="behaviors"/> is empty or contains undefined flags.</exception>
    internal ComponentBehaviorRequirement(Type fixture, ComponentBehavior behaviors)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        if (!fixture.IsClass || fixture.IsAbstract)
        {
            throw new ArgumentException("A behavior fixture must be a concrete class.", nameof(fixture));
        }

        if (behaviors == ComponentBehavior.None || (behaviors & ~AllBehaviors) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(behaviors), behaviors, "Requirements must contain defined flags.");
        }

        Fixture = fixture;
        Behaviors = behaviors;
    }

    /// <summary>Gets the concrete test fixture that owns the evidence.</summary>
    internal Type Fixture { get; }

    /// <summary>Gets the complete required behavior flags.</summary>
    internal ComponentBehavior Behaviors { get; }
}
