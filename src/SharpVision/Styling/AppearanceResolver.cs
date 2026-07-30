// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Resolves semantic, local, state, and ambient appearance into concrete terminal values.</summary>
internal static class AppearanceResolver
{
    internal static ResolvedAppearance Resolve(Control control, VisualState visualState)
    {
        ArgumentNullException.ThrowIfNull(control);

        return Resolve(
            control,
            visualState,
            control.Theme,
            control.ResolvedAppearanceProfile,
            parentAmbientFace: null,
            useExplicitAmbient: false);
    }

    /// <summary>Resolves one state from an explicit ambient parent without touching appearance caches.</summary>
    /// <param name="control">The non-null control to resolve.</param>
    /// <param name="visualState">The exact defined visual-state flags.</param>
    /// <param name="parentAmbientFace">The explicitly resolved parent ambient face, or null at a root.</param>
    /// <returns>The exact concrete resolved appearance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    internal static ResolvedAppearance ResolveSnapshot(
        Control control,
        VisualState visualState,
        Face? parentAmbientFace)
    {
        ArgumentNullException.ThrowIfNull(control);
        return ResolveSnapshot(
            control,
            visualState,
            control.Theme,
            control.ResolvedAppearanceProfile,
            parentAmbientFace);
    }

    /// <summary>Resolves one prospective state from explicit Theme, profile, and ambient context.</summary>
    /// <param name="control">The non-null control whose local values participate.</param>
    /// <param name="visualState">The exact defined visual-state flags.</param>
    /// <param name="theme">The prospective Theme, or null.</param>
    /// <param name="profile">The non-null prospective appearance profile.</param>
    /// <param name="parentAmbientFace">The explicit parent ambient face, or null at a root.</param>
    /// <returns>The exact concrete resolved appearance.</returns>
    /// <exception cref="ArgumentNullException">A required control or profile is null.</exception>
    internal static ResolvedAppearance ResolveSnapshot(
        Control control,
        VisualState visualState,
        Theme? theme,
        ThemeProfile profile,
        Face? parentAmbientFace)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(profile);
        return Resolve(
            control,
            visualState,
            theme,
            profile,
            parentAmbientFace,
            useExplicitAmbient: true);
    }

    /// <summary>Compares the active visual state using explicit old and new ambient parent faces.</summary>
    /// <param name="control">The non-null control whose local appearance values participate.</param>
    /// <param name="previousTheme">The Theme before the prospective change, or null.</param>
    /// <param name="previousProfile">The non-null complete profile before the change.</param>
    /// <param name="currentTheme">The prospective Theme after the change, or null.</param>
    /// <param name="currentProfile">The non-null complete profile after the change.</param>
    /// <param name="previousParentAmbientFace">The explicit parent ambient face before the change.</param>
    /// <param name="currentParentAmbientFace">The explicit parent ambient face after the change.</param>
    /// <returns>The exact invalidation impact for the currently rendered state.</returns>
    /// <exception cref="ArgumentNullException">A required control or profile is null.</exception>
    internal static InvalidationImpact GetImpact(
        Control control,
        Theme? previousTheme,
        ThemeProfile previousProfile,
        Theme? currentTheme,
        ThemeProfile currentProfile,
        Face? previousParentAmbientFace,
        Face? currentParentAmbientFace)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(previousProfile);
        ArgumentNullException.ThrowIfNull(currentProfile);

        var state = control.GetAppearanceState();
        var previous = Resolve(
            control,
            state,
            previousTheme,
            previousProfile,
            previousParentAmbientFace,
            useExplicitAmbient: true);
        var current = Resolve(
            control,
            state,
            currentTheme,
            currentProfile,
            currentParentAmbientFace,
            useExplicitAmbient: true);
        return control.GetAppearanceChangeImpact(previous, current);
    }

    private static ResolvedAppearance Resolve(
        Control control,
        VisualState visualState,
        Theme? theme,
        ThemeProfile profile,
        Face? parentAmbientFace,
        bool useExplicitAmbient)
    {
        var normal = profile.Normal;

        // Ambient inheritance must still act as a base that a later, more specific authoring step
        // — a state overlay — can win over, exactly as it already wins over Normal's own authored
        // foreground. Only the transparency gate that decides WHETHER to inherit needs the
        // fully-folded result, not Normal's background alone (see #131): a dry-run fold with no
        // inheritance applied answers that question without disturbing the real fold's layering.
        // A LocalFace is a complete override rather than a partial delta and, unlike a state
        // overlay, is authored expecting its own foreground to survive regardless of background —
        // e.g. decorative content (FigletText, Spinner) commonly leaves LocalFace's background at
        // the default transparent while still choosing its own explicit foreground. It keeps
        // opting out of inheritance unconditionally.
        var dryRunFace = ResolveFace(theme, FoldAuthoredAppearance(control, profile, visualState, normal).Face);
        var isTransparent = control.LocalFace is null &&
            !control.AppearanceBoundary &&
            dryRunFace.Background.Literal == Color.Transparent;

        var inheritedFace = ResolveFace(theme, normal.Face);

        if (isTransparent && ResolveAmbientParentFace(control, parentAmbientFace, useExplicitAmbient) is { } ambient)
        {
            inheritedFace = ApplyAmbientFace(inheritedFace, ambient);
        }

        var authored = FoldAuthoredAppearance(
            control,
            profile,
            visualState,
            new ThemeAppearance(inheritedFace, normal.Border, normal.Shadow));

        var face = ResolveFace(theme, authored.Face);
        var border = ResolveBorder(theme, authored.Border);
        var shadow = ResolveShadow(theme, authored.Shadow);
        return CreateResolved(face, border, shadow);
    }

    private static ThemeAppearance FoldAuthoredAppearance(
        Control control,
        ThemeProfile profile,
        VisualState visualState,
        ThemeAppearance baseAppearance)
    {
        var authored = profile.ApplyStates(baseAppearance, visualState);
        authored = new ThemeAppearance(
            control.LocalFace ?? authored.Face,
            control.LocalBorder ?? authored.Border,
            control.LocalShadow ?? authored.Shadow);

        // Combined as plain data first, for the same reason ThemeProfile.ApplyStates folds its
        // own overlays before validating: an intermediate per-overlay Face construction can
        // reject a partial combination that the complete local-overlay fold would resolve cleanly.
        var localCombined = AppearanceSet.Empty;

        foreach (var overlay in VisualStateOrder.OrderedOverlays)
        {
            if ((visualState & overlay) != 0 &&
                control.AppearanceSets.TryGetValue(overlay, out var localSet))
            {
                localCombined = localCombined.Overlay(localSet);
            }
        }

        return authored.Apply(localCombined);
    }

    /// <summary>Compares two concrete appearances for their exact earliest invalidation phase.</summary>
    /// <param name="previous">The concrete appearance before a transaction.</param>
    /// <param name="current">The concrete appearance after a transaction.</param>
    /// <returns>The strongest phase affected by the concrete difference.</returns>
    internal static InvalidationImpact GetImpact(ResolvedAppearance previous, ResolvedAppearance current)
    {
        return previous.Border.Sides != current.Border.Sides ||
               previous.Shadow.IsVisible != current.Shadow.IsVisible ||
               previous.Shadow.Offset != current.Shadow.Offset
            ? InvalidationImpact.Measure
            : previous.Face != current.Face ||
              previous.Border != current.Border ||
              previous.Shadow != current.Shadow
                ? InvalidationImpact.Render
                : InvalidationImpact.None;
    }

    private static Face ResolveFace(Theme? theme, Face face)
    {
        try
        {
            return new Face(
                Resolve(theme, face.Foreground),
                Resolve(theme, face.Background),
                Resolve(theme, face.Attributes),
                face.Underline,
                Resolve(theme, face.UnderlineColor));
        }
        catch (ArgumentException exception)
        {
            // The Face constructor only validates decoration conflicts once every channel is a
            // literal (see #107): a theme-referenced attribute or underline color defers the
            // check to here, where the semantic roles that produced the conflict are still known.
            throw new ArgumentException(
                $"Resolving this face against the active theme produced a decoration conflict: " +
                $"attributes '{face.Attributes}', underline '{face.Underline}', underline color " +
                $"'{face.UnderlineColor}'. {exception.Message}",
                exception);
        }
    }

    private static Border ResolveBorder(Theme? theme, Border border) => new(
        border.Sides,
        border.GlyphStyle,
        Resolve(theme, border.Foreground),
        Resolve(theme, border.Background),
        Resolve(theme, border.Attributes));

    private static Shadow ResolveShadow(Theme? theme, Shadow shadow) => new(
        shadow.IsVisible,
        shadow.Mode,
        shadow.Offset,
        shadow.Glyph,
        Resolve(theme, shadow.Foreground),
        Resolve(theme, shadow.Background),
        Resolve(theme, shadow.Attributes));

    private static Color Resolve(Theme? theme, ColorValue value) => value.IsLiteral
        ? value.Literal
        : theme?.ResolveColor(value.ThemeColor) ?? Color.Default;

    private static TerminalAttributes Resolve(Theme? theme, AttributeValue value) => value.IsLiteral
        ? value.Literal
        : theme?.ResolveAttributes(value.ThemeDecoration) ?? TerminalAttributes.None;

    private static Face? ResolveAmbientParentFace(
        Control control,
        Face? parentAmbientFace,
        bool useExplicitAmbient)
    {
        if (useExplicitAmbient)
        {
            // Ownership transactions clear Parent before prospective resolution. The explicit face
            // represents that old or new edge without consulting the already-mutated live tree.
            return parentAmbientFace;
        }

        return control.Parent is { } currentParent
            ? currentParent.GetActualFace(currentParent.AmbientAppearanceState)
            : null;
    }

    private static Face ApplyAmbientFace(Face face, Face parent) => new(
        parent.Foreground.Literal,
        face.Background.Literal,
        parent.Attributes.Literal,
        parent.Underline,
        parent.UnderlineColor.Literal);

    private static ResolvedAppearance CreateResolved(Face face, Border border, Shadow shadow)
    {
        var style = new TerminalStyle(
            face.Foreground.Literal,
            face.Background.Literal,
            face.Attributes.Literal,
            underline: face.Underline,
            underlineColor: face.UnderlineColor.Literal);
        var borderStyle = new TerminalStyle(
            border.Foreground.Literal,
            border.Background.Literal,
            border.Attributes.Literal);
        var shadowStyle = new TerminalStyle(
            shadow.Foreground.Literal,
            shadow.Background.Literal,
            shadow.Attributes.Literal);
        return new ResolvedAppearance(
            face,
            border,
            shadow,
            style,
            Background(face.Background.Literal),
            borderStyle,
            Background(border.Background.Literal),
            shadowStyle,
            Background(shadow.Background.Literal));
    }

    private static BackgroundMode Background(Color color) => color == Color.Transparent
        ? BackgroundMode.Transparent
        : BackgroundMode.Opaque;
}
