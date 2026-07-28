// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Composes reusable partial style profiles against complete theme profiles.</summary>
public static class StyleResolution
{
    /// <summary>Applies a partial appearance profile to an already-complete theme baseline.</summary>
    /// <param name="baseline">The complete profile that supplies omitted appearance members.</param>
    /// <param name="set">The partial profile whose supplied members win.</param>
    /// <returns>A complete theme profile containing the composed normal and state appearances.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="baseline"/> is <see langword="null"/>.</exception>
    public static ThemeProfile Apply(ThemeProfile baseline, AppearanceProfileSet set)
    {
        ArgumentNullException.ThrowIfNull(baseline);

        return new ThemeProfile(
            set.Normal is null ? baseline.Normal : baseline.Normal.Apply(set.Normal.Value),
            Apply(baseline.PointerOver, set.PointerOver),
            Apply(baseline.FocusWithin, set.FocusWithin),
            Apply(baseline.Focused, set.Focused),
            Apply(baseline.Current, set.Current),
            Apply(baseline.Selected, set.Selected),
            Apply(baseline.Checked, set.Checked),
            Apply(baseline.Indeterminate, set.Indeterminate),
            Apply(baseline.Pressed, set.Pressed),
            Apply(baseline.Disabled, set.Disabled));
    }

    private static AppearanceSet Apply(AppearanceSet baseline, AppearanceSet? set) =>
        set is null ? baseline : baseline.Overlay(set.Value);
}
