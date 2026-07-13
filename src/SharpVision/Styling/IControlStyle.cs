// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Defines the read-only style view shared by controls and themes.</summary>
/// <remarks>
/// The single creatable implementation is <see cref="ControlStyle{TControl}"/>. This interface is a
/// stable read-only projection used when a style's concrete value type is not statically known
/// (for example a control's per-instance style); theme lifecycle operations are internal.
/// </remarks>
public interface IControlStyle
{
    /// <summary>Raised after one committed style mutation publishes a new snapshot.</summary>
    public event EventHandler<ThemeChangedEventArgs>? Changed;

    /// <summary>Gets the concrete control type targeted by this style.</summary>
    public Type TargetType { get; }

    /// <summary>Gets whether this style rejects further mutation.</summary>
    public bool IsFrozen { get; }

    /// <summary>Gets the earliest impact of the current style contents.</summary>
    public Impact AggregateImpact { get; }

    /// <summary>Gets one stored value from the current immutable snapshot.</summary>
    /// <param name="styleProperty">The style property.</param>
    /// <param name="state">The visual state.</param>
    /// <param name="value">The stored value when present.</param>
    /// <returns>Whether a value exists for the property and state.</returns>
    public bool TryGetValue(IStyleProperty styleProperty, State state, out object? value);
}
