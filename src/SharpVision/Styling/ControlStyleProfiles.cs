// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Provides shared code-owned semantic profiles for complete control-style fallbacks.</summary>
internal static class ControlStyleProfiles
{
    /// <summary>Gets the default editable or selectable input semantic profile.</summary>
    internal static ThemeProfile Input { get; } = Create(ThemeRole.Input);

    /// <summary>Gets the default borderless selectable-control semantic profile.</summary>
    internal static ThemeProfile Selection { get; } = Input.WithoutChrome();

    /// <summary>Gets the default passive-control semantic profile.</summary>
    internal static ThemeProfile Control { get; } = Create(ThemeRole.Control);

    extension(ThemeProfile profile)
    {
        /// <summary>Returns one profile with its normal face preserved and intrinsic chrome disabled.</summary>
        /// <returns>A borderless, shadowless profile retaining every state contribution.</returns>
        internal ThemeProfile WithoutChrome()
        {
            ArgumentNullException.ThrowIfNull(profile);
            var chrome = Theme.CreateDefaultAppearance();
            var normal = new ThemeAppearance(profile.Normal.Face, chrome.Border, chrome.Shadow);
            return new ThemeProfile(
                normal,
                profile.PointerOver,
                profile.FocusWithin,
                profile.Focused,
                profile.Current,
                profile.Selected,
                profile.Checked,
                profile.Indeterminate,
                profile.Pressed,
                profile.Disabled);
        }
    }

    private static ThemeProfile Create(ThemeRole role) => new(Theme.CreateDefaultAppearance(role));
}
