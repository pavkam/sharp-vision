// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Groups optional face, border, and shadow contributions for one visual state.</summary>
public readonly record struct AppearanceOverlay
{
    /// <summary>Initializes a partial intrinsic appearance contribution.</summary>
    /// <param name="face">The optional face contribution.</param>
    /// <param name="border">The optional border contribution.</param>
    /// <param name="shadow">The optional shadow contribution.</param>
    public AppearanceOverlay(FaceOverlay? face = null, BorderOverlay? border = null, ShadowOverlay? shadow = null)
    {
        Face = face;
        Border = border;
        Shadow = shadow;
    }

    /// <summary>Gets the optional face contribution.</summary>
    public FaceOverlay? Face { get; }

    /// <summary>Gets the optional border contribution.</summary>
    public BorderOverlay? Border { get; }

    /// <summary>Gets the optional shadow contribution.</summary>
    public ShadowOverlay? Shadow { get; }

    /// <summary>Gets an appearance contribution with no supplied members.</summary>
    public static AppearanceOverlay Empty => default;

    /// <summary>Overlays a later partial contribution over this contribution.</summary>
    /// <param name="later">The later contribution whose supplied members win.</param>
    /// <returns>The combined partial contribution.</returns>
    public AppearanceOverlay Overlay(AppearanceOverlay later) => new(
        Overlay(Face, later.Face),
        Overlay(Border, later.Border),
        Overlay(Shadow, later.Shadow));

    private static FaceOverlay? Overlay(FaceOverlay? earlier, FaceOverlay? later) =>
        later is null ? earlier : earlier?.Overlay(later.Value) ?? later;

    private static BorderOverlay? Overlay(BorderOverlay? earlier, BorderOverlay? later) =>
        later is null ? earlier : earlier?.Overlay(later.Value) ?? later;

    private static ShadowOverlay? Overlay(ShadowOverlay? earlier, ShadowOverlay? later) =>
        later is null ? earlier : earlier?.Overlay(later.Value) ?? later;
}
