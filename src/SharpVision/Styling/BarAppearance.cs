// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Rebases resting and physically hovered bar-surface faces onto
/// <see cref="SemanticColor.Bar"/> while retaining the fallback style's other explicitly authored
/// visual-state contributions.</summary>
internal static class BarAppearance
{
    /// <summary>Rebases one complete fallback face for a leaf bar style.</summary>
    /// <typeparam name="TStyle">The fallback style type.</typeparam>
    /// <param name="fallback">The complete fallback value for <paramref name="state"/>.</param>
    /// <param name="state">The one visual state being completed.</param>
    /// <param name="states">The fallback set carrying normal values and authorship provenance.</param>
    /// <returns>
    /// A face using Bar for normal and physical-hover states; other explicitly authored state
    /// backgrounds remain authoritative.
    /// </returns>
    internal static Face CompleteFace<TStyle>(
        TStyle fallback,
        VisualState state,
        StyleStates<TStyle> states)
        where TStyle : ControlStyle
    {
        ArgumentNullException.ThrowIfNull(fallback);
        ArgumentNullException.ThrowIfNull(states);

        var stateName = StateName(state);
        var authorsBackground = stateName is not null &&
            states.AuthoredFor(stateName)?.Contains("Face.Background") == true;
        var inheritsNormalBackground = fallback.Face.Background == states.Normal.Face.Background;

        // Physical hover changes the foreground and decorations without punching a control-colored
        // hole through the continuous menu, command-bar, or status-bar plane.
        var preservesBarPlane = state is VisualState.Normal or VisualState.IsPointerOver;

        return preservesBarPlane || (!authorsBackground && inheritsNormalBackground)
            ? fallback.Face with { Background = SemanticColor.Bar }
            : fallback.Face;
    }

    /// <summary>
    /// Rebases a control's normal appearance onto Bar while keeping physical hover on that plane.
    /// </summary>
    /// <typeparam name="TStyle">The complete style type.</typeparam>
    /// <param name="states">The resolved complete fallback states.</param>
    /// <returns>
    /// Appearance states whose normal face uses Bar and whose physical-hover overlay contributes
    /// every authored member except its background.
    /// </returns>
    internal static AppearanceStates Rebase<TStyle>(StyleStates<TStyle> states)
        where TStyle : ControlStyle
    {
        ArgumentNullException.ThrowIfNull(states);

        var appearances = states.ToAppearanceStates();
        var normal = appearances.Normal;
        return new AppearanceStates(
            new ControlAppearance(
                normal.Face with { Background = SemanticColor.Bar },
                normal.Border,
                normal.Shadow),
            PreserveBarBackground(appearances.IsPointerOver),
            appearances.FocusWithin,
            appearances.Focused,
            appearances.Current,
            appearances.Selected,
            appearances.Checked,
            appearances.Indeterminate,
            appearances.Pressed,
            appearances.Disabled);
    }

    /// <summary>Removes only a physical-hover background contribution from an appearance overlay.</summary>
    /// <param name="overlay">The source appearance overlay.</param>
    /// <returns>The overlay with its face background omitted and every other contribution retained.</returns>
    [Pure]
    private static AppearanceOverlay PreserveBarBackground(AppearanceOverlay overlay) =>
        overlay.Face is { Background: not null } face
            ? new AppearanceOverlay(
                new FaceOverlay(
                    foreground: face.Foreground,
                    attributes: face.Attributes,
                    underline: face.Underline,
                    underlineColor: face.UnderlineColor),
                overlay.Border,
                overlay.Shadow)
            : overlay;

    [Pure]
    private static string? StateName(VisualState state) => state switch
    {
        VisualState.Normal => null,
        VisualState.IsPointerOver => "pointerOver",
        VisualState.FocusWithin => "focusWithin",
        VisualState.Focused => "focused",
        VisualState.Current => "current",
        VisualState.Selected => "selected",
        VisualState.Checked => "checked",
        VisualState.Indeterminate => "indeterminate",
        VisualState.Pressed => "pressed",
        VisualState.Disabled => "disabled",
        _ => null
    };
}
