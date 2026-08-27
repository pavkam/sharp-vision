// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Adapts a complete-per-state <see cref="StyleStates{TStyle}"/> into an
/// <see cref="AppearanceStates"/> - one complete Normal appearance plus nine partial per-state
/// <see cref="AppearanceOverlay"/> contributions - the shape the control rendering pipeline
/// resolves every visual state from.</summary>
[PublicAPI]
public static class StyleStatesExtensions
{
    /// <summary>Converts a complete per-state style resolution - from
    /// <see cref="Theme.GetStyleSet{TStyle}(TStyle)"/>,
    /// <see cref="Theme.GetInteractiveControlStyleSet"/>, or one of that method's three siblings -
    /// into the <see cref="AppearanceStates"/> shape a control returns from its
    /// <c>GetDefaultAppearanceStates</c> hook, or assigns as a <c>StyleDefinition</c>'s appearance
    /// selector when it owns a primary style slot.</summary>
    /// <remarks>
    /// A member equal to Normal's own value is still recorded as a real per-state contribution
    /// when this set's own resolution deliberately wrote it there, rather than dropped as
    /// redundant: the fold that later combines simultaneously active states prefers whichever
    /// later state actually supplied a member and only then falls through to an earlier state's
    /// own contribution, so silently dropping a deliberately-repeated value would hand that member
    /// back to whichever earlier state supplied it instead. That provenance travels only through
    /// <see cref="Theme"/>'s own resolution; a <see cref="StyleStates{TStyle}"/> a consumer builds
    /// directly rather than obtains from <see cref="Theme"/> can only be value-diffed against its
    /// own Normal.
    /// </remarks>
    /// <typeparam name="TStyle">The concrete themeable style type.</typeparam>
    /// <param name="set">The complete per-state resolution to adapt.</param>
    /// <returns>The equivalent Normal-plus-nine-overlays appearance set.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="set"/> is null.</exception>
    [Pure]
    public static AppearanceStates ToAppearanceStates<TStyle>(this StyleStates<TStyle> set)
        where TStyle : ControlStyle
    {
        ArgumentNullException.ThrowIfNull(set);

        var normal = new ControlAppearance(set.Normal.Face, set.Normal.Border, set.Normal.Shadow);
        return new AppearanceStates(
            normal,
            Diff(set.Normal, set.IsPointerOver, set.AuthoredFor("pointerOver")),
            Diff(set.Normal, set.FocusWithin, set.AuthoredFor("focusWithin")),
            Diff(set.Normal, set.Focused, set.AuthoredFor("focused")),
            Diff(set.Normal, set.Current, set.AuthoredFor("current")),
            Diff(set.Normal, set.Selected, set.AuthoredFor("selected")),
            Diff(set.Normal, set.Checked, set.AuthoredFor("checked")),
            Diff(set.Normal, set.Indeterminate, set.AuthoredFor("indeterminate")),
            Diff(set.Normal, set.Pressed, set.AuthoredFor("pressed")),
            Diff(set.Normal, set.Disabled, set.AuthoredFor("disabled")));
    }

    /// <summary>Returns the chrome members one state's JSON wrote, or null when nothing is known
    /// about that state's provenance.</summary>
    /// <typeparam name="TStyle">The concrete themeable style type.</typeparam>
    /// <param name="set">The set carrying the provenance.</param>
    /// <param name="state">The JSON state name.</param>
    /// <returns>The authored property paths, or null.</returns>
    [Pure]
    internal static IReadOnlySet<string>? AuthoredFor<TStyle>(this StyleStates<TStyle> set, string state)
        where TStyle : ControlStyle =>
        set.Authored?.TryGetValue(state, out var members) == true ? members : null;

    /// <summary>Value-diffs one resolved state's Face/Border/Shadow against another's, member by
    /// member, producing the partial delta between them. Also used by
    /// <see cref="Theme.BuildFallbackAwareStates{TStyle,TFallback}"/> to isolate a fallback's
    /// state-specific contribution so it can be re-applied onto a leaf's own resolved Normal
    /// instead of discarding it - a leaf's local Face/Border/Shadow and
    /// structural members must survive fallback-driven per-state completion.</summary>
    /// <param name="normal">The Normal-state resolution to diff against.</param>
    /// <param name="state">The state resolution, or null when this theme authored no such state.</param>
    /// <param name="authored">
    /// The members this state's JSON wrote, which survive the diff whatever their value; null when
    /// the two sides being diffed are both code-completed values with no JSON provenance to carry.
    /// </param>
    /// <returns>The partial delta.</returns>
    [Pure]
    internal static AppearanceOverlay Diff(
        ControlStyle normal,
        ControlStyle? state,
        IReadOnlySet<string>? authored = null) =>
        state is null
            ? AppearanceOverlay.Empty
            : new AppearanceOverlay(
                DiffFace(normal.Face, state.Face, authored),
                DiffBorder(normal.Border, state.Border, authored),
                DiffShadow(normal.Shadow, state.Shadow, authored));

    private static FaceOverlay DiffFace(Face normal, Face state, IReadOnlySet<string>? authored) => new(
        Keep(state.Foreground, normal.Foreground, authored, "Face.Foreground"),
        Keep(state.Background, normal.Background, authored, "Face.Background"),
        Keep(state.Attributes, normal.Attributes, authored, "Face.Attributes"),
        Keep(state.Underline, normal.Underline, authored, "Face.Underline"),
        Keep(state.UnderlineColor, normal.UnderlineColor, authored, "Face.UnderlineColor"));

    private static BorderOverlay DiffBorder(Border normal, Border state, IReadOnlySet<string>? authored)
    {
        var sides = Keep(state.Sides, normal.Sides, authored, "Border.Sides");
        var glyphStyle = Keep(state.GlyphStyle, normal.GlyphStyle, authored, "Border.GlyphStyle");
        var foreground = Keep(state.Foreground, normal.Foreground, authored, "Border.Foreground");
        var edgeColors = Keep(state.EdgeColors, normal.EdgeColors, authored, "Border.EdgeColors");
        var background = Keep(state.Background, normal.Background, authored, "Border.Background");
        var attributes = Keep(state.Attributes, normal.Attributes, authored, "Border.Attributes");
        return edgeColors is { } edges
            ? BorderOverlay.WithEdgeColors(edges, sides, glyphStyle, foreground, background, attributes)
            : new BorderOverlay(sides, glyphStyle, foreground, background, attributes);
    }

    private static ShadowOverlay DiffShadow(Shadow normal, Shadow state, IReadOnlySet<string>? authored) => new(
        Keep(state.IsVisible, normal.IsVisible, authored, "Shadow.IsVisible"),
        Keep(state.Mode, normal.Mode, authored, "Shadow.Mode"),
        Keep(state.Offset, normal.Offset, authored, "Shadow.Offset"),
        Keep(state.Glyph, normal.Glyph, authored, "Shadow.Glyph"),
        Keep(state.Foreground, normal.Foreground, authored, "Shadow.Foreground"),
        Keep(state.Background, normal.Background, authored, "Shadow.Background"),
        Keep(state.Attributes, normal.Attributes, authored, "Shadow.Attributes"));

    // "Differs from Normal" and "the author wrote it" are both reasons to record a member, and
    // only the union of the two is correct: the first alone loses an authored-equal member, and the
    // second alone loses everything a cascade or a code-owned per-state default contributed
    // without any JSON of its own.
    private static T? Keep<T>(T state, T normal, IReadOnlySet<string>? authored, string path)
        where T : struct =>
        !EqualityComparer<T>.Default.Equals(state, normal) || authored?.Contains(path) == true
            ? state
            : null;
}
