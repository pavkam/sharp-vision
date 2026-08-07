// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Holds one control style's per-visual-state resolutions - the same nine named slots
/// <see cref="AppearanceStates"/> already exposes for the six formerly-fixed styles, generalized
/// to any themeable style type.</summary>
/// <typeparam name="TStyle">The concrete themeable style type.</typeparam>
[PublicAPI]
public sealed class StyleStates<TStyle> where TStyle : ControlStyle
{
    /// <summary>Gets the complete normal-state style. Always present - every control style
    /// resolves to at least its code-owned default.</summary>
    public required TStyle Normal { get; init; }

    /// <summary>Gets the pointer-over contribution, or null when this theme did not author one.</summary>
    public TStyle? PointerOver { get; init; }

    /// <summary>Gets the descendant-focus contribution, or null when this theme did not author one.</summary>
    public TStyle? FocusWithin { get; init; }

    /// <summary>Gets the direct-focus contribution, or null when this theme did not author one.</summary>
    public TStyle? Focused { get; init; }

    /// <summary>Gets the current-item contribution, or null when this theme did not author one.</summary>
    public TStyle? Current { get; init; }

    /// <summary>Gets the selected-item contribution, or null when this theme did not author one.</summary>
    public TStyle? Selected { get; init; }

    /// <summary>Gets the checked contribution, or null when this theme did not author one.</summary>
    public TStyle? Checked { get; init; }

    /// <summary>Gets the indeterminate contribution, or null when this theme did not author one.</summary>
    public TStyle? Indeterminate { get; init; }

    /// <summary>Gets the pressed contribution, or null when this theme did not author one.</summary>
    public TStyle? Pressed { get; init; }

    /// <summary>Gets the disabled contribution, or null when this theme did not author one.</summary>
    public TStyle? Disabled { get; init; }

    /// <summary>Gets, per state name, the chrome members that state's JSON actually wrote, as
    /// <c>"Face.Background"</c>-style property paths.</summary>
    /// <remarks>
    /// Provenance, not value. A state resolves by patching its JSON onto the resolved Normal, so a
    /// member the author wrote back to exactly Normal's value is indistinguishable from one they
    /// never mentioned - by value. The distinction matters: <see cref="AppearanceStates.ApplyStates"/>
    /// combines simultaneously active states with <c>later.X ?? X</c>, where an unset member does
    /// not mean "inherit Normal" but "whatever an earlier state said stands". An authored-equal
    /// member is therefore exactly the value that stops an earlier state from winning.
    /// </remarks>
    internal IReadOnlyDictionary<string, IReadOnlySet<string>>? Authored { get; init; }
}
