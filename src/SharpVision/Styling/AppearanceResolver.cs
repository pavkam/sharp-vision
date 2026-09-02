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
                useExplicitAmbient: false,
                useContinuousBackground: control.UsesContinuousBackground);
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

        /// <summary>Resolves one state from explicit ambient and continuous-background context.</summary>
        /// <param name="visualState">The exact defined visual-state flags.</param>
        /// <param name="parentAmbientFace">The explicitly resolved parent ambient face, or null at a root.</param>
        /// <param name="useContinuousBackground">Whether an ancestor owns the continuous background plane.</param>
        /// <returns>The exact concrete resolved appearance.</returns>
        internal ResolvedAppearance ResolveSnapshot(
            VisualState visualState,
            Face? parentAmbientFace,
            bool useContinuousBackground) =>
            Resolve(
                control,
                visualState,
                control.Theme,
                control.ResolvedAppearanceStates,
                parentAmbientFace,
                useExplicitAmbient: true,
                useContinuousBackground);

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
                useExplicitAmbient: true,
                useContinuousBackground: control.UsesContinuousBackground);
        }

        /// <summary>Resolves one prospective state from explicit appearance and continuous-background context.</summary>
        /// <param name="visualState">The exact defined visual-state flags.</param>
        /// <param name="theme">The prospective inherited Theme, or null.</param>
        /// <param name="states">The non-null prospective appearance states.</param>
        /// <param name="parentAmbientFace">The explicit parent ambient face, or null at a root.</param>
        /// <param name="useContinuousBackground">Whether an ancestor owns the continuous background plane.</param>
        /// <returns>The exact concrete resolved appearance.</returns>
        internal ResolvedAppearance ResolveSnapshot(
            VisualState visualState,
            Theme? theme,
            AppearanceStates states,
            Face? parentAmbientFace,
            bool useContinuousBackground)
        {
            ArgumentNullException.ThrowIfNull(control);
            ArgumentNullException.ThrowIfNull(states);
            return Resolve(
                control,
                visualState,
                theme,
                states,
                parentAmbientFace,
                useExplicitAmbient: true,
                useContinuousBackground);
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
                useExplicitAmbient: true,
                useContinuousBackground: control.UsesContinuousBackground);
            var current = Resolve(
                control,
                state,
                currentTheme,
                currentStates,
                currentParentAmbientFace,
                useExplicitAmbient: true,
                useContinuousBackground: control.UsesContinuousBackground);
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
        bool useExplicitAmbient,
        bool useContinuousBackground)
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
        var dryRunFace = ResolveFace(theme, FoldAuthoredAppearance(control, states, visualState, normal).Appearance.Face);
        var isTransparent = control.LocalFace is null &&
            !control.IsAppearanceBoundary &&
            dryRunFace.Background.Literal == Color.Transparent;

        var inheritedFace = ResolveFace(theme, normal.Face);

        if (isTransparent && ResolveAmbientParentFace(control, parentAmbientFace, useExplicitAmbient) is { } ambient)
        {
            inheritedFace = ApplyAmbientFace(inheritedFace, ambient);
        }

        var (authoredAppearance, borderForegroundAuthored) = FoldAuthoredAppearance(
            control,
            states,
            visualState,
            new ControlAppearance(inheritedFace, normal.Border, normal.Shadow));

        var face = ResolveFace(theme, authoredAppearance.Face);
        if (useContinuousBackground && !control.AuthorsLocalBackground(visualState))
        {
            // A status-like strip owns one continuous paint plane. Framework defaults may still
            // contribute their foreground, decorations, and interaction state, but their opaque
            // control background must not punch rectangular holes through that plane. Explicit
            // local face, style, and state-background authoring remain authoritative.
            face = face with { Background = Color.Transparent };
        }
        var border = ResolveBorder(theme, authoredAppearance.Border);
        var shadow = ResolveShadow(theme, authoredAppearance.Shadow);
        return CreateResolved(theme, face, border, shadow, borderForegroundAuthored);
    }

    /// <summary>Folds per-state theme and local overlays onto one base appearance, additionally
    /// reporting whether the currently active visual state(s) - theme-authored or locally
    /// overlaid - actually changed <see cref="Border.Foreground"/> away from what it would be with
    /// no visual-state overlay active at all, rather than it merely surviving unchanged.</summary>
    /// <remarks>
    /// A plain value comparison, not a presence check against the per-state overlay machinery: a
    /// state whose own delta happens to restate Normal's exact Foreground must not opt out of
    /// relief highlight/shade substitution for a color that never actually changed.
    ///
    /// <para>The comparison baseline is <see cref="ControlBase.LocalBorder"/> when present, not
    /// <paramref name="baseAppearance"/>'s own Normal - a local override is itself part of this
    /// control's baseline authoring, applied identically regardless of visual state, and must not
    /// be mistaken for a per-state delta merely because its Foreground happens to differ from the
    /// Theme's own Normal. Only a subsequent visual-state-specific change on top of it - theme or
    /// local - counts as authored.</para>
    ///
    /// <para>Always false when <see cref="AppearanceStates.StateAuthorsOwnRelief"/>. A state that
    /// explicitly authors relief owns the edge-color feedback for that state, so an inherited
    /// foreground difference must not suppress the relief during resolved-border creation. See
    /// <see cref="ResolvedBorderStyles.Create"/>.</para>
    /// </remarks>
    private static (ControlAppearance Appearance, bool BorderForegroundAuthored) FoldAuthoredAppearance(
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

        var finalAppearance = authored.Apply(localCombined);
        var authoredBaseline = control.LocalBorder ?? baseAppearance.Border;
        var borderForegroundAuthored = !states.StateAuthorsOwnRelief &&
            finalAppearance.Border.Foreground != authoredBaseline.Foreground;

        return (finalAppearance, borderForegroundAuthored);
    }

    extension(ResolvedAppearance previous)
    {
        /// <summary>Compares two concrete appearances for their exact earliest invalidation phase.</summary>
        /// <param name="current">The concrete appearance after a transaction.</param>
        /// <returns>The strongest phase affected by the concrete difference.</returns>
        [Pure]
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
                  previous.BorderStyles != current.BorderStyles ||
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

        // Reported here, against the channel that actually failed. A transparent paint channel is
        // a different failure than a decoration conflict, so it is diagnosed directly rather than
        // folded into the reconciliation below.
        ValidatePaint(foreground, face.Foreground, "foreground");

        // Theme values (and application-authored semantic values) are resolved independently, so a
        // combination that is individually legal per channel can still conflict once every channel
        // becomes a literal - e.g. a semantic underline color with no active underline, or a
        // semantic attribute carrying the legacy Underline flag alongside a distinct typed
        // underline. Reconciling here, against the already-resolved literals and before Face ever
        // sees them, applies the same precedence already shipped at every other decoration call
        // site: a typed underline wins over the legacy flag, a legacy flag with no typed underline
        // wins over the typed value, and an underline color with neither active is cleared. That
        // makes the combination degrade gracefully instead of surviving validation only to throw
        // the moment it becomes literal.
        var (reconciledAttributes, reconciledUnderline, reconciledUnderlineColor) = DecorationResolver.Resolve(
            inherited: default,
            attributes: attributes,
            underline: face.Underline == Underline.None ? null : face.Underline,
            underlineColor: underlineColor);

        ValidatePaint(reconciledUnderlineColor, face.UnderlineColor, "underline color");

        return new Face(foreground, background, reconciledAttributes, reconciledUnderline, reconciledUnderlineColor);
    }

    private static Border ResolveBorder(Theme? theme, Border border)
    {
        var foreground = Resolve(theme, border.Foreground);
        ValidatePaint(foreground, border.Foreground, "border foreground");
        return new Border(
            border.Sides,
            border.GlyphStyle,
            foreground,
            border.Relief,
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

    private static ResolvedAppearance CreateResolved(
        Theme? theme,
        Face face,
        Border border,
        Shadow shadow,
        bool borderForegroundAuthoredForState)
    {
        var style = new TerminalStyle(
            face.Foreground.Literal,
            face.Background.Literal,
            face.Attributes.Literal,
            underline: face.Underline,
            underlineColor: face.UnderlineColor.Literal);
        var shadowStyle = new TerminalStyle(
            shadow.Foreground.Literal,
            shadow.Background.Literal,
            shadow.Attributes.Literal);
        return new ResolvedAppearance(
            face,
            border,
            ResolvedBorderStyles.Create(border, theme, borderForegroundAuthoredForState),
            shadow,
            style,
            Background(face.Background.Literal),
            Background(border.Background.Literal),
            shadowStyle,
            Background(shadow.Background.Literal));
    }

    private static BackgroundMode Background(Color color) => color == Color.Transparent
        ? BackgroundMode.Transparent
        : BackgroundMode.Opaque;
}
