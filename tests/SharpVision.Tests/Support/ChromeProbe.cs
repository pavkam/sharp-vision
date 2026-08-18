// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Enables intrinsic chrome authoring solely for infrastructure tests.</summary>
internal class ChromeProbe: ControlBase
{
    /// <summary>Initializes a probe with chrome authoring enabled.</summary>
    internal ChromeProbe() => EnableChromeAuthoring();

    /// <summary>Gets the resolved inner content rectangle.</summary>
    internal Rect ExposedContentBounds => ContentBounds;

    /// <summary>Re-invokes the protected chrome-authoring opt-in, exercising the already-enabled
    /// guard from outside the class.</summary>
    internal void EnableChromeAuthoringAgain() => EnableChromeAuthoring();

    /// <summary>Sets or clears one explicit visual-state appearance contribution.</summary>
    /// <param name="state">The exact visual state to author.</param>
    /// <param name="appearance">The replacement contribution, or <see langword="null"/> to clear it.</param>
    public void SetStateAppearance(VisualState state, AppearanceOverlay? appearance) =>
        SetAppearance(state, appearance);
}
