// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling.Collisions;

using System.Diagnostics.CodeAnalysis;

/// <summary>Models an external style whose bare name collides with a privileged root.</summary>
public sealed record InputStyle: ControlStyle
{
    /// <summary>Initializes the colliding style.</summary>
    [SetsRequiredMembers]
    public InputStyle() : base(DefaultFace, NoBorder, NoShadow)
    {
    }
}
