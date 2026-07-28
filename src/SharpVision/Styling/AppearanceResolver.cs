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
        var inheritedFace = ResolveFace(theme, normal.Face);
        inheritedFace = InheritAmbientFace(
            control,
            inheritedFace,
            parentAmbientFace,
            useExplicitAmbient);
        var authored = new ThemeAppearance(inheritedFace, normal.Border, normal.Shadow);
        authored = profile.ApplyStates(authored, visualState);

        authored = ApplyCompleteLocalValues(control, authored);

        foreach (var overlay in VisualStateOrder.OrderedOverlays)
        {
            if ((visualState & overlay) != 0 &&
                control.AppearanceSets.TryGetValue(overlay, out var localSet))
            {
                authored = authored.Apply(localSet);
            }
        }

        var face = ResolveFace(theme, authored.Face);
        var border = ResolveBorder(theme, authored.Border);
        var shadow = ResolveShadow(theme, authored.Shadow);
        return CreateResolved(face, border, shadow);
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

    private static ThemeAppearance ApplyCompleteLocalValues(Control control, ThemeAppearance appearance) => new(
        control.LocalFace ?? appearance.Face,
        control.LocalBorder ?? appearance.Border,
        control.LocalShadow ?? appearance.Shadow);

    private static Face ResolveFace(Theme? theme, Face face) => new(
        Resolve(theme, face.Foreground),
        Resolve(theme, face.Background),
        Resolve(theme, face.Attributes),
        face.Underline,
        Resolve(theme, face.UnderlineColor));

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

    private static Face InheritAmbientFace(
        Control control,
        Face face,
        Face? parentAmbientFace,
        bool useExplicitAmbient)
    {
        if (control.LocalFace is not null || control.AppearanceBoundary ||
            face.Background.Literal != Color.Transparent)
        {
            return face;
        }

        Face parent;
        if (useExplicitAmbient)
        {
            // Ownership transactions clear Parent before prospective resolution. The explicit face
            // represents that old or new edge without consulting the already-mutated live tree.
            if (parentAmbientFace is not { } explicitParent)
            {
                return face;
            }

            parent = explicitParent;
        }
        else
        {
            if (control.Parent is not { } currentParent)
            {
                return face;
            }

            parent = currentParent.GetActualFace(currentParent.AmbientAppearanceState);
        }

        return new Face(
            parent.Foreground.Literal,
            face.Background.Literal,
            parent.Attributes.Literal,
            parent.Underline,
            parent.UnderlineColor.Literal);
    }

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
