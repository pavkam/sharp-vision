// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Defines the small, deterministic built-in appearance policy for each control role.</summary>
/// <remarks>
/// This is deliberately not a theme style registry. Themes supply only palette values; controls
/// supply their own stable visual policy and callers can override it through direct appearance
/// properties or <see cref="Control.SetAppearance"/>.
/// </remarks>
internal static class ControlAppearanceDefaults
{
    /// <summary>Gets the built-in appearance contribution for one control state.</summary>
    /// <param name="control">The control being resolved.</param>
    /// <param name="state">The single state profile being applied.</param>
    /// <returns>The policy contribution, if any.</returns>
    internal static Appearance Get(Control control, VisualState state)
    {
        ArgumentNullException.ThrowIfNull(control);

        if (state == VisualState.PointerOver && control is ListItem or NavigationViewItem)
        {
            return new Appearance(ColorRole.Accent, null, null, null, null, null, null, null, null, null);
        }

        if (state == VisualState.PointerOver && control is MenuItem)
        {
            return new Appearance(
                ColorRole.SelectionForeground,
                ColorRole.SelectionBackground,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null);
        }

        if (state == VisualState.PointerOver && (!control.CanFocus || control is List))
        {
            return Appearance.Empty;
        }

        var baseAppearance = GetBase(state);
        return control switch
        {
            Button => baseAppearance.Overlay(GetButton(state)),
            ComboBox => baseAppearance.Overlay(GetComboBox(state)),
            CheckBox => baseAppearance.Overlay(GetCheckBox(state)),
            RadioButton => baseAppearance.Overlay(GetRadioButton(state)),
            ScrollBar => baseAppearance.Overlay(GetScrollBar(state)),
            TextInput => baseAppearance.Overlay(GetTextInput(state)),
            _ => baseAppearance,
        };
    }

#pragma warning disable IDE0072 // VisualState is a flags enum; compound states are intentionally empty profiles.
    private static Appearance GetBase(VisualState state) => state switch
    {
        VisualState.Normal => new Appearance(
            ColorRole.Foreground,
            null,
            null,
            null,
            null,
            ColorRole.Border,
            null,
            ColorRole.Border,
            null,
            null),
        VisualState.PointerOver => new Appearance(ColorRole.Accent, null, null, null, null, null, null, null, null, null),
        VisualState.Selected => new Appearance(ColorRole.SelectionForeground, ColorRole.SelectionBackground, null, null, null, null, null, null, null, null),
        VisualState.Disabled => new Appearance(ColorRole.Muted, null, null, null, null, null, null, null, null, null),
        VisualState.FocusWithin or VisualState.Focused or VisualState.Current or VisualState.Checked or VisualState.Indeterminate or VisualState.Pressed => Appearance.Empty,
        _ => Appearance.Empty,
    };
#pragma warning restore IDE0072

    private static Appearance GetButton(VisualState state) => state switch
    {
        VisualState.PointerOver or VisualState.Focused or VisualState.Pressed =>
            new Appearance(ColorRole.Accent, null, null, null, null, ColorRole.Accent, null, null, null, null),
        VisualState.Normal or VisualState.FocusWithin or VisualState.Current or VisualState.Selected or VisualState.Checked or VisualState.Indeterminate or VisualState.Disabled => Appearance.Empty,
        _ => Appearance.Empty,
    };

    private static Appearance GetComboBox(VisualState state) => state switch
    {
        VisualState.Normal => new Appearance(null, ColorRole.Surface, null, null, null, null, null, null, null, null),
        VisualState.Focused => new Appearance(ColorRole.Accent, null, null, null, null, ColorRole.Accent, null, null, null, null),
        VisualState.PointerOver or VisualState.FocusWithin or VisualState.Current or VisualState.Selected or VisualState.Checked or VisualState.Indeterminate or VisualState.Pressed or VisualState.Disabled => Appearance.Empty,
        _ => Appearance.Empty,
    };

    private static Appearance GetCheckBox(VisualState state) => state switch
    {
        VisualState.Focused or VisualState.Checked => new Appearance(ColorRole.Accent, null, null, null, null, null, null, null, null, null),
        VisualState.Indeterminate => new Appearance(ColorRole.Warning, null, null, null, null, null, null, null, null, null),
        VisualState.Normal or VisualState.PointerOver or VisualState.FocusWithin or VisualState.Current or VisualState.Selected or VisualState.Pressed or VisualState.Disabled => Appearance.Empty,
        _ => Appearance.Empty,
    };

    private static Appearance GetRadioButton(VisualState state)
    {
        return state is VisualState.Focused or VisualState.Checked
            ? new Appearance(ColorRole.Accent, null, null, null, null, null, null, null, null, null)
            : Appearance.Empty;
    }

    private static Appearance GetScrollBar(VisualState state)
    {
        return state is VisualState.Focused or VisualState.Pressed
            ? new Appearance(ColorRole.Accent, null, null, null, null, null, null, null, null, null)
            : Appearance.Empty;
    }

    private static Appearance GetTextInput(VisualState state)
    {
        return state == VisualState.Focused
            ? new Appearance(ColorRole.Accent, null, null, null, null, null, null, null, null, null)
            : Appearance.Empty;
    }

}
