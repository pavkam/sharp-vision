// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Exposes a primary style slot that owns appearance alongside one registered immutable
/// overlay, for testing <c>GetStyleThemeImpact</c>'s overlay composition.</summary>
internal sealed class StyledAppearanceOverlayProbe: ControlBase
{
    /// <summary>Initializes a probe with a style-owned appearance primary slot and one registered
    /// immutable overlay.</summary>
    /// <param name="overlay">The overlay to register.</param>
    internal StyledAppearanceOverlayProbe(AppearanceStatesOverlay overlay)
    {
        Slot = InitializeStyle(TextStyle.Definition);
        InitializeAppearanceOverlay(overlay);
    }

    /// <summary>Gets the primary style slot, which owns appearance.</summary>
    internal StyleSlot<TextStyle> Slot { get; }
}
