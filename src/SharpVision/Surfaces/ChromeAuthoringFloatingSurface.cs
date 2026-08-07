// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Surfaces;

/// <summary>Widens <see cref="FloatingSurfaceBase"/>'s protected <see cref="Border"/>/
/// <see cref="Shadow"/> authoring surface to public, for the two floating surfaces whose whole
/// purpose is letting a caller author their own chrome directly (<c>Window</c>, <c>Popup</c>). See
/// <see cref="ChromeAuthoringContainer"/> for the analogous
/// <see cref="Container"/>-side widening and the reasoning this mirrors.</summary>
[PublicAPI]
public abstract class ChromeAuthoringFloatingSurface: FloatingSurfaceBase
{
    /// <summary>Gets or sets the complete locally authored border.</summary>
    public new Border Border { get => base.Border; set => base.Border = value; }

    /// <summary>Returns border ownership to the active Theme.</summary>
    public new void ResetBorder() => base.ResetBorder();

    /// <summary>Gets or sets the complete locally authored shadow.</summary>
    public new Shadow Shadow { get => base.Shadow; set => base.Shadow = value; }

    /// <summary>Returns shadow ownership to the active Theme.</summary>
    public new void ResetShadow() => base.ResetShadow();
}
