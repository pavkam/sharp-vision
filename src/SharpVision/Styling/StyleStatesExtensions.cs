// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Adapts a complete-per-state <see cref="StyleStates{TStyle}"/> into today's
/// <see cref="AppearanceStates"/> (one complete Normal appearance plus nine partial per-state
/// <see cref="AppearanceOverlay"/> contributions), so <see cref="AppearanceResolver"/> and
/// <see cref="AppearanceStates.ApplyStates"/> need no changes at all.</summary>
internal static class StyleStatesExtensions
{
    /// <summary>Converts every present state to a partial contribution: a member is recorded when
    /// it differs from Normal's value, or when <see cref="StyleStates{TStyle}.Authored"/> says this
    /// state's JSON wrote it regardless of what it wrote.</summary>
    /// <remarks>
    /// Value-diffing alone is not sound, though it reads as though it should be. The argument for
    /// it was that a member resolving to exactly Normal's value is observationally identical
    /// whether recorded or left unset - which holds for a single state and fails for the
    /// simultaneous multi-state fold. <see cref="AppearanceStates.ApplyStates"/> walks
    /// <see cref="VisualStateOrder"/> combining with <c>later.X ?? X</c>, so an unset member yields
    /// to whatever an *earlier* state supplied rather than falling back to Normal. Dropping a
    /// member the author deliberately wrote back to Normal's value therefore hands that member to
    /// the earlier state: a row that is both Selected and Disabled kept Selected's accent
    /// background under a theme that had explicitly asked for Normal's surface when disabled, and
    /// no spelling of the request could survive, because the erasure keyed on the value being the
    /// one the author wanted.
    /// </remarks>
    /// <typeparam name="TStyle">The concrete themeable style type.</typeparam>
    /// <param name="set">The complete per-state resolution to adapt.</param>
    /// <returns>The equivalent Normal-plus-nine-overlays appearance set.</returns>
    internal static AppearanceStates ToAppearanceStates<TStyle>(this StyleStates<TStyle> set)
        where TStyle : ControlStyle
    {
        var normal = new ControlAppearance(set.Normal.Face, set.Normal.Border, set.Normal.Shadow);
        return new AppearanceStates(
            normal,
            Diff(set.Normal, set.PointerOver, set.AuthoredFor("pointerOver")),
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

    private static BorderOverlay DiffBorder(Border normal, Border state, IReadOnlySet<string>? authored) => new(
        Keep(state.Sides, normal.Sides, authored, "Border.Sides"),
        Keep(state.GlyphStyle, normal.GlyphStyle, authored, "Border.GlyphStyle"),
        Keep(state.Foreground, normal.Foreground, authored, "Border.Foreground"),
        Keep(state.Background, normal.Background, authored, "Border.Background"),
        Keep(state.Attributes, normal.Attributes, authored, "Border.Attributes"));

    private static ShadowOverlay DiffShadow(Shadow normal, Shadow state, IReadOnlySet<string>? authored) => new(
        Keep(state.Visible, normal.Visible, authored, "Shadow.Visible"),
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
