// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Groups optional face, border, and shadow contributions for one visual state.</summary>
public readonly record struct AppearanceSet
{
    /// <summary>Initializes a partial intrinsic appearance contribution.</summary>
    /// <param name="face">The optional face contribution.</param>
    /// <param name="border">The optional border contribution.</param>
    /// <param name="shadow">The optional shadow contribution.</param>
    public AppearanceSet(FaceSet? face = null, BorderSet? border = null, ShadowSet? shadow = null)
    {
        Face = face;
        Border = border;
        Shadow = shadow;
    }

    /// <summary>Gets the optional face contribution.</summary>
    public FaceSet? Face { get; }

    /// <summary>Gets the optional border contribution.</summary>
    public BorderSet? Border { get; }

    /// <summary>Gets the optional shadow contribution.</summary>
    public ShadowSet? Shadow { get; }

    /// <summary>Gets an appearance contribution with no supplied members.</summary>
    public static AppearanceSet Empty => default;

    /// <summary>Overlays a later partial contribution over this contribution.</summary>
    /// <param name="later">The later contribution whose supplied members win.</param>
    /// <returns>The combined partial contribution.</returns>
    public AppearanceSet Overlay(AppearanceSet later) => new(
        Overlay(Face, later.Face),
        Overlay(Border, later.Border),
        Overlay(Shadow, later.Shadow));

    private static FaceSet? Overlay(FaceSet? earlier, FaceSet? later) =>
        later is null ? earlier : earlier?.Overlay(later.Value) ?? later;

    private static BorderSet? Overlay(BorderSet? earlier, BorderSet? later) =>
        later is null ? earlier : earlier?.Overlay(later.Value) ?? later;

    private static ShadowSet? Overlay(ShadowSet? earlier, ShadowSet? later) =>
        later is null ? earlier : earlier?.Overlay(later.Value) ?? later;
}
