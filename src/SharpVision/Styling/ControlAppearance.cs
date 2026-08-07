// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Groups one complete face, border, and shadow appearance.</summary>
public readonly record struct ControlAppearance
{
    /// <summary>Initializes a complete semantic appearance.</summary>
    /// <param name="face">The complete face.</param>
    /// <param name="border">The complete border.</param>
    /// <param name="shadow">The complete shadow.</param>
    public ControlAppearance(Face face, Border border, Shadow shadow)
    {
        Face = face;
        Border = border;
        Shadow = shadow;
    }

    /// <summary>Gets the complete face.</summary>
    public Face Face { get; }

    /// <summary>Gets the complete border.</summary>
    public Border Border { get; }

    /// <summary>Gets the complete shadow.</summary>
    public Shadow Shadow { get; }

    /// <summary>Applies a partial appearance contribution.</summary>
    /// <param name="set">The later member-wise contribution.</param>
    /// <returns>The composed complete appearance.</returns>
    public ControlAppearance Apply(AppearanceOverlay set) => new(
        set.Face?.Apply(Face) ?? Face,
        set.Border?.Apply(Border) ?? Border,
        set.Shadow?.Apply(Shadow) ?? Shadow);
}
