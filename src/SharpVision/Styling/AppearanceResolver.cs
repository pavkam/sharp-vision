// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Resolves semantic, local, state, and ambient appearance into concrete terminal values.</summary>
internal static class AppearanceResolver
{
    extension(ControlBase control)
    {
        internal ResolvedAppearance Resolve(VisualState visualState)
        {
            ArgumentNullException.ThrowIfNull(control);

            return Resolve(
                control,
                visualState,
                control.Theme,
                control.ResolvedAppearanceStates,
                parentAmbientFace: null,
                useExplicitAmbient: false);
        }

        /// <summary>Resolves one state from an explicit ambient parent without touching appearance caches.</summary>
        /// <param name="visualState">The exact defined visual-state flags.</param>
        /// <param name="parentAmbientFace">The explicitly resolved parent ambient face, or null at a root.</param>
        /// <returns>The exact concrete resolved appearance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
        internal ResolvedAppearance ResolveSnapshot(VisualState visualState, Face? parentAmbientFace)
        {
            ArgumentNullException.ThrowIfNull(control);
            return control.ResolveSnapshot(
                visualState,
                control.Theme,
                control.ResolvedAppearanceStates,
                parentAmbientFace);
        }

        /// <summary>Resolves one prospective visual state from an explicit Theme, appearance states, and ambient context.</summary>
        /// <param name="visualState">The exact defined visual-state flags.</param>
        /// <param name="theme">The prospective Theme, or null.</param>
        /// <param name="states">The non-null prospective appearance states.</param>
        /// <param name="parentAmbientFace">The explicit parent ambient face, or null at a root.</param>
        /// <returns>The exact concrete resolved appearance.</returns>
        /// <exception cref="ArgumentNullException">A required control or appearance-states value is null.</exception>
        internal ResolvedAppearance ResolveSnapshot(
            VisualState visualState,
            Theme? theme,
            AppearanceStates states,
            Face? parentAmbientFace)
        {
            ArgumentNullException.ThrowIfNull(control);
            ArgumentNullException.ThrowIfNull(states);
            return Resolve(
                control,
                visualState,
                theme,
                states,
                parentAmbientFace,
                useExplicitAmbient: true);
        }

        /// <summary>Compares the active visual state using explicit old and new ambient parent faces.</summary>
        /// <param name="previousTheme">The Theme before the prospective change, or null.</param>
        /// <param name="previousStates">The non-null complete appearance states before the change.</param>
        /// <param name="currentTheme">The prospective Theme after the change, or null.</param>
        /// <param name="currentStates">The non-null complete appearance states after the change.</param>
        /// <param name="previousParentAmbientFace">The explicit parent ambient face before the change.</param>
        /// <param name="currentParentAmbientFace">The explicit parent ambient face after the change.</param>
        /// <returns>The exact invalidation impact for the currently rendered state.</returns>
        /// <exception cref="ArgumentNullException">A required control or appearance-states value is null.</exception>
        internal InvalidationImpact GetImpact(
            Theme? previousTheme,
            AppearanceStates previousStates,
            Theme? currentTheme,
            AppearanceStates currentStates,
            Face? previousParentAmbientFace,
            Face? currentParentAmbientFace)
        {
            ArgumentNullException.ThrowIfNull(control);
            ArgumentNullException.ThrowIfNull(previousStates);
            ArgumentNullException.ThrowIfNull(currentStates);

            var state = control.GetAppearanceState();
            var previous = Resolve(
                control,
                state,
                previousTheme,
                previousStates,
                previousParentAmbientFace,
                useExplicitAmbient: true);
            var current = Resolve(
                control,
                state,
                currentTheme,
                currentStates,
                currentParentAmbientFace,
                useExplicitAmbient: true);
            return control.GetAppearanceChangeImpact(previous, current);
        }
    }

    [SuppressMessage(
        "Style",
        "IDE0051:Remove unused private members",
        Justification = "Called only from within extension(...) blocks; the analyzer doesn't track that usage yet.")]
    private static ResolvedAppearance Resolve(
        ControlBase control,
        VisualState visualState,
        Theme? theme,
        AppearanceStates states,
        Face? parentAmbientFace,
        bool useExplicitAmbient)
    {
        var normal = states.Normal;

        // Ambient inheritance must still act as a base that a later, more specific authoring step
        // — a state overlay — can win over, exactly as it already wins over Normal's own authored
        // foreground. Only the transparency gate that decides WHETHER to inherit needs the
        // fully-folded result, not Normal's background alone: a dry-run fold with no
        // inheritance applied answers that question without disturbing the real fold's layering.
        // A LocalFace is a complete override rather than a partial delta and, unlike a state
        // overlay, is authored expecting its own foreground to survive regardless of background —
        // e.g. decorative content (FigletText, Spinner) commonly leaves LocalFace's background at
        // the default transparent while still choosing its own explicit foreground. It keeps
        // opting out of inheritance unconditionally.
        var dryRunFace = ResolveFace(theme, FoldAuthoredAppearance(control, states, visualState, normal).Face);
        var isTransparent = control.LocalFace is null &&
            !control.IsAppearanceBoundary &&
            dryRunFace.Background.Literal == Color.Transparent;

        var inheritedFace = ResolveFace(theme, normal.Face);

        if (isTransparent && ResolveAmbientParentFace(control, parentAmbientFace, useExplicitAmbient) is { } ambient)
        {
            inheritedFace = ApplyAmbientFace(inheritedFace, ambient);
        }

        var authored = FoldAuthoredAppearance(
            control,
            states,
            visualState,
            new ControlAppearance(inheritedFace, normal.Border, normal.Shadow));

        var face = ResolveFace(theme, authored.Face);
        var border = ResolveBorder(theme, authored.Border);
        var shadow = ResolveShadow(theme, authored.Shadow);
        return CreateResolved(face, border, shadow);
    }

    private static ControlAppearance FoldAuthoredAppearance(
        ControlBase control,
        AppearanceStates states,
        VisualState visualState,
        ControlAppearance baseAppearance)
    {
        var authored = states.ApplyStates(baseAppearance, visualState);
        authored = new ControlAppearance(
            control.LocalFace ?? authored.Face,
            control.LocalBorder ?? authored.Border,
            control.LocalShadow ?? authored.Shadow);

        // Combined as plain data first, for the same reason AppearanceStates.ApplyStates folds its
        // own overlays before validating: an intermediate per-overlay Face construction can
        // reject a partial combination that the complete local-overlay fold would resolve cleanly.
        var localCombined = AppearanceOverlay.Empty;

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

    extension(ResolvedAppearance previous)
    {
        /// <summary>Compares two concrete appearances for their exact earliest invalidation phase.</summary>
        /// <param name="current">The concrete appearance after a transaction.</param>
        /// <returns>The strongest phase affected by the concrete difference.</returns>
        internal InvalidationImpact GetImpact(ResolvedAppearance current)
        {
            // Mode changes the footprint exactly as IsVisible and Offset do - see
            // AppearanceStates.ChangesChromeGeometry, which classifies the same four members for
            // the partial-overlay form of this question.
            return previous.Border.Sides != current.Border.Sides ||
                   previous.Shadow.IsVisible != current.Shadow.IsVisible ||
                   previous.Shadow.Offset != current.Shadow.Offset ||
                   previous.Shadow.Mode != current.Shadow.Mode
                ? InvalidationImpact.Measure
                : previous.Face != current.Face ||
                  previous.Border != current.Border ||
                  previous.Shadow != current.Shadow
                    ? InvalidationImpact.Render
                    : InvalidationImpact.None;
        }
    }

    private static Face ResolveFace(Theme? theme, Face face)
    {
        var foreground = Resolve(theme, face.Foreground);
        var background = Resolve(theme, face.Background);
        var attributes = Resolve(theme, face.Attributes);
        var underlineColor = Resolve(theme, face.UnderlineColor);

        // Reported here, against the channel that actually failed, and before the catch below can
        // see them. That catch exists for one specific failure and names three channels as its
        // cause; a transparent paint channel is a different failure in a channel it does not even
        // mention, so letting it fall through produced a message asserting a conflict between
        // three values that were all fine.
        ValidatePaint(foreground, face.Foreground, "foreground");
        ValidatePaint(underlineColor, face.UnderlineColor, "underline color");

        try
        {
            return new Face(foreground, background, attributes, face.Underline, underlineColor);
        }
        catch (ArgumentException exception)
        {
            // The Face constructor only validates decoration conflicts once every channel is a
            // literal: a theme-referenced attribute or underline color defers the
            // check to here, where the semantic colors that produced the conflict are still known.
            throw new ArgumentException(
                $"Resolving this face against the active theme produced a decoration conflict: " +
                $"attributes '{face.Attributes}', underline '{face.Underline}', underline color " +
                $"'{face.UnderlineColor}'. {exception.Message}",
                exception);
        }
    }

    private static Border ResolveBorder(Theme? theme, Border border)
    {
        var foreground = Resolve(theme, border.Foreground);
        ValidatePaint(foreground, border.Foreground, "border foreground");

        return new Border(
            border.Sides,
            border.GlyphStyle,
            foreground,
            Resolve(theme, border.Background),
            Resolve(theme, border.Attributes));
    }

    private static Shadow ResolveShadow(Theme? theme, Shadow shadow)
    {
        var foreground = Resolve(theme, shadow.Foreground);
        ValidatePaint(foreground, shadow.Foreground, "shadow foreground");

        return new Shadow(
            shadow.IsVisible,
            shadow.Mode,
            shadow.Offset,
            shadow.Glyph,
            foreground,
            Resolve(theme, shadow.Background),
            Resolve(theme, shadow.Attributes));
    }

    // Names the channel and keeps the pre-resolution value, so a semantic color the active theme
    // maps to transparent is diagnosable from the message alone - the resolved literal on its own
    // says nothing about which theme entry produced it.
    private static void ValidatePaint(Color resolved, ControlColor source, string channel)
    {
        if (resolved != Color.Transparent)
        {
            return;
        }

        throw new ArgumentException(
            source.IsLiteral
                ? $"The {channel} resolved against the active theme is transparent, which cannot paint."
                : $"The {channel} '{source}' resolved against the active theme to transparent, which cannot paint.");
    }

    private static Color Resolve(Theme? theme, ControlColor value) => value.IsLiteral
        ? value.Literal
        : theme?.ResolveColor(value.SemanticColor) ?? Color.Default;

    private static TerminalAttributes Resolve(Theme? theme, ControlDecoration value) => value.IsLiteral
        ? value.Literal
        : theme?.ResolveAttributes(value.SemanticDecoration) ?? TerminalAttributes.None;

    private static Face? ResolveAmbientParentFace(
        ControlBase control,
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
