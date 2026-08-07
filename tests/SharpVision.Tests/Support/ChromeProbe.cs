// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Republishes protected intrinsic chrome solely for infrastructure tests.</summary>
internal class ChromeProbe: ControlBase
{
    /// <summary>Gets the resolved inner content rectangle.</summary>
    internal Rect ExposedContentBounds => ContentBounds;

    /// <summary>Gets or sets the explicitly authored border.</summary>
    public new Border Border
    {
        get => base.Border;
        set => base.Border = value;
    }

    /// <summary>Clears the explicitly authored border.</summary>
    public new void ResetBorder() => base.ResetBorder();

    /// <summary>Gets or sets the explicitly authored shadow.</summary>
    public new Shadow Shadow
    {
        get => base.Shadow;
        set => base.Shadow = value;
    }

    /// <summary>Clears the explicitly authored shadow.</summary>
    public new void ResetShadow() => base.ResetShadow();

    /// <summary>Sets or clears one explicit visual-state appearance contribution.</summary>
    /// <param name="state">The exact visual state to author.</param>
    /// <param name="appearance">The replacement contribution, or <see langword="null"/> to clear it.</param>
    public void SetStateAppearance(VisualState state, AppearanceOverlay? appearance) =>
        SetAppearance(state, appearance);
}
