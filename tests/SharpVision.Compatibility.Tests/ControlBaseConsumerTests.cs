// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Compatibility.Tests;

using SharpVision.Controls;
using SharpVision.Styling;

/// <summary>
/// Verifies <see cref="ControlBase.ResolveAppearance"/> is a genuinely usable consumer seam for
/// asserting theme-resolved appearance: this project carries no <c>InternalsVisibleTo</c>
/// relationship with SharpVision, so every control, resolution, and assertion here compiles and
/// resolves against public surface alone — the unit-layer proof
/// <c>docs/concepts/theming-new-controls.md</c> prescribes for a third-party control, without a
/// mounted application, transport, or dispatcher.
/// </summary>
public sealed class ControlBaseConsumerTests
{
    /// <summary>Models the plainest external control: no style slot and no appearance hook, so it
    /// presents the universal "control" appearance every theme owns.</summary>
    private sealed class PlainControl: ControlBase;

    /// <summary>Models an external control that selects a well-known sibling appearance through
    /// the protected hook — the no-structural-members extension path the theming documentation
    /// describes for a control that only wants an existing type's appearance.</summary>
    private sealed class FieldLikeControl: ControlBase
    {
        /// <inheritdoc/>
        protected override AppearanceStates GetDefaultAppearanceStates(Theme? theme) =>
            (theme ?? ThemeCatalog.Dark).Input;
    }

    /// <summary>Verifies a detached external control resolves its semantic background to the
    /// supplied Theme's own literal — the assertion an unattached control's live properties cannot
    /// make, since without an inherited Theme every semantic color floors to the default
    /// literal.</summary>
    [Fact]
    public void ResolveAppearance_WhenResolvedAgainstAnExplicitTheme_ResolvesSemanticColorsToItsLiterals()
    {
        var control = new PlainControl();

        var appearance = control.ResolveAppearance(ThemeCatalog.Dark);

        appearance.Face.Background.Literal.ShouldBe(ThemeCatalog.Dark.ResolveColor(SemanticColor.Control));
    }

    /// <summary>Verifies the Theme parameter actually selects: the same control previews distinct
    /// literals under two bundled themes.</summary>
    [Fact]
    public void ResolveAppearance_WhenResolvedAgainstTwoThemes_ResolvesDistinctLiterals()
    {
        var control = new PlainControl();

        var dark = control.ResolveAppearance(ThemeCatalog.Dark);
        var white = control.ResolveAppearance(ThemeCatalog.White);

        dark.Face.Background.Literal.ShouldNotBe(white.Face.Background.Literal);
    }

    /// <summary>Verifies the protected appearance hook travels into the preview: a control
    /// selecting the input appearance previews the input set's border geometry, distinct from the
    /// universal control appearance a plain derivation previews.</summary>
    [Fact]
    public void ResolveAppearance_WhenControlSelectsASiblingAppearanceThroughTheHook_PreviewsThatSelection()
    {
        var field = new FieldLikeControl();
        var plain = new PlainControl();

        var fieldAppearance = field.ResolveAppearance(ThemeCatalog.Dark);
        var plainAppearance = plain.ResolveAppearance(ThemeCatalog.Dark);

        fieldAppearance.Border.Sides.ShouldBe(ThemeCatalog.Dark.Input.Normal.Border.Sides);
        fieldAppearance.Border.Sides.ShouldNotBe(plainAppearance.Border.Sides);
    }

    /// <summary>Verifies visual states fold without any input machinery: the focused preview of an
    /// input-shaped control differs from its normal preview under the same Theme.</summary>
    [Fact]
    public void ResolveAppearance_WhenPreviewingAVisualState_FoldsTheThemesStateContribution()
    {
        var field = new FieldLikeControl();

        var normal = field.ResolveAppearance(ThemeCatalog.Dark);
        var focused = field.ResolveAppearance(ThemeCatalog.Dark, VisualState.Focused);

        focused.ShouldNotBe(normal);
    }
}
