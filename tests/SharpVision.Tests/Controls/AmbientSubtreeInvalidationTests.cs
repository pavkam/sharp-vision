// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies every writer of a control's ambient face render-invalidates its subtree, not
/// just the control it was called on.
///
/// <para>A control's resolved Normal Face is what transparent descendants inherit. Clearing a
/// descendant's resolved-appearance cache without also setting its render bit leaves a subtree with
/// no scheduled frame - and a descendant that does repaint takes the render-clean-reuse path and
/// copies stale previous-frame cells that never reflect the cleared cache.
/// <c>Face_WhenChanged_RenderInvalidatesEveryDescendant</c> encodes that rationale for
/// <c>Face</c>/<c>ResetFace</c>/<c>AppearanceBoundary</c>; two other writers of the same state
/// skipped it.</para>
///
/// <para>Both are narrow enough to have gone unnoticed. <c>CommitStyle</c> needs the control's own
/// active state to mask its Normal change, so its own resolved appearance compares equal, its
/// <c>Compare</c> returns None, and <c>Invalidate</c> is a no-op while the ambient face genuinely
/// moved. <c>SetAppearance</c> needs a control whose ambient face is its <em>active</em> state
/// rather than its Normal, which is <c>PressableBase</c> and <c>ListItem</c>.</para>
/// </summary>
public sealed class AmbientSubtreeInvalidationTests
{
    /// <summary>The regression the <c>CommitStyle</c> half exists to pin: a control whose active
    /// state masks its own Normal change still has to schedule a frame for the descendants that
    /// inherit that Normal.
    ///
    /// <para>The report frames this with a disabled Button, which is not quite right - a Pressable's
    /// ambient face is its ACTIVE state, so a pinned disabled overlay means its caption genuinely
    /// sees no change. The defect needs a control whose ambient face is its Normal while its own
    /// impact is computed from the masked active state, which is what this probe is.</para>
    /// </summary>
    [Fact]
    public void Style_WhenTheActiveStateMasksTheNormalChange_StillRenderInvalidatesDescendants()
    {
        using var owner = new StyledProbeContainer();
        var child = new ProbeControl();
        owner.Children.Add(child);
        owner.Author(
            VisualState.Disabled,
            new AppearanceOverlay(face: new FaceOverlay(foreground: Color.Rgb(200, 200, 200))));
        owner.Enabled = false;
        owner.Clear(Invalidation.All);
        child.Clear(Invalidation.All);

        owner.Style = owner.ActualStyle with
        {
            Face = owner.ActualStyle.Face with { Foreground = Color.Rgb(1, 2, 3) }
        };

        (owner.Pending & Invalidation.Render).ShouldBe(
            Invalidation.Render,
            "the subtree walk starts at the control itself, whose own ambient face moved");
        ((child.Pending & Invalidation.Render) != 0).ShouldBeTrue(
            "the child inherits the changed Normal even though the owner's disabled face is pinned");
    }

    /// <summary>Verifies the same for a plainly-visible change, so the fix is not specific to the
    /// masking case that made it detectable.</summary>
    [Fact]
    public void Style_WhenTheNormalFaceChanges_RenderInvalidatesEveryDescendant()
    {
        using var owner = new StyledProbeContainer();
        var child = new ProbeControl();
        owner.Children.Add(child);
        owner.Clear(Invalidation.All);
        child.Clear(Invalidation.All);

        owner.Style = owner.ActualStyle with
        {
            Face = owner.ActualStyle.Face with { Foreground = Color.Rgb(4, 5, 6) }
        };

        ((child.Pending & Invalidation.Render) != 0).ShouldBeTrue();
    }

    /// <summary>The counter-case that keeps the cheap path alive: a style change that leaves the
    /// Normal face alone must not render-invalidate the whole subtree. This is the common case, and
    /// the subtree walk is deliberately unconditional once entered.</summary>
    [Fact]
    public void Style_WhenOnlyPaddingChanges_DoesNotRenderInvalidateDescendants()
    {
        using var owner = new StyledProbeContainer();
        var child = new ProbeControl();
        owner.Children.Add(child);
        owner.Clear(Invalidation.All);
        child.Clear(Invalidation.All);

        owner.Style = owner.ActualStyle with { Padding = new Thickness(3, 1) };

        ((child.Pending & Invalidation.Render) != 0).ShouldBeFalse();
    }

    /// <summary>The counter-case the two pre-existing rationale-bearing tests already state for the
    /// self-only path, restated for the subtree walk: two authored faces that RESOLVE identically
    /// must not invalidate. Comparing the raw values would render-invalidate a whole subtree for a
    /// semantic reference replaced by the literal its theme maps it to.</summary>
    [Fact]
    public void Style_WhenTheNewFaceResolvesIdentically_DoesNotRenderInvalidateDescendants()
    {
        using var owner = new StyledProbeContainer();
        var child = new ProbeControl();
        owner.Children.Add(child);
        owner.SetTheme(ThemeCatalog.Dark);
        owner.Style = owner.ActualStyle with
        {
            Face = owner.ActualStyle.Face with { Foreground = SemanticColor.ControlText }
        };
        owner.Clear(Invalidation.All);
        child.Clear(Invalidation.All);

        owner.Style = owner.ActualStyle with
        {
            Face = owner.ActualStyle.Face with
            {
                Foreground = ThemeCatalog.Dark.ResolveColor(SemanticColor.ControlText)
            }
        };

        ((child.Pending & Invalidation.Render) != 0).ShouldBeFalse();
    }

    /// <summary>The regression the <c>SetAppearance</c> half exists to pin. A Pressable's ambient
    /// face is its ACTIVE state, so a per-state overlay is folded into the face its caption
    /// inherits - and clearing only the button left the caption holding a resolved appearance built
    /// from the previous overlay.</summary>
    [Fact]
    public void SetAppearance_WhenTheControlsAmbientFaceIsItsActiveState_RenderInvalidatesDescendants()
    {
        using var owner = new AppearanceProbeContainer();
        var child = new ProbeControl();
        owner.Children.Add(child);
        owner.Clear(Invalidation.All);
        child.Clear(Invalidation.All);

        owner.Author(
            VisualState.PointerOver,
            new AppearanceOverlay(face: new FaceOverlay(foreground: SemanticColor.Accent)));

        ((child.Pending & Invalidation.Render) != 0).ShouldBeTrue();
    }

    /// <summary>Verifies removing that overlay is treated identically - the state it described is
    /// just as gone from the ambient face as it was newly present.</summary>
    [Fact]
    public void SetAppearance_WhenAnOverlayIsRemoved_RenderInvalidatesDescendants()
    {
        using var owner = new AppearanceProbeContainer();
        var child = new ProbeControl();
        owner.Children.Add(child);
        owner.Author(
            VisualState.PointerOver,
            new AppearanceOverlay(face: new FaceOverlay(foreground: SemanticColor.Accent)));
        owner.Clear(Invalidation.All);
        child.Clear(Invalidation.All);

        owner.Author(VisualState.PointerOver, null);

        ((child.Pending & Invalidation.Render) != 0).ShouldBeTrue();
    }

    /// <summary>The counter-case for that half: a control whose ambient face is its Normal keeps the
    /// cheap self-only clear, since no per-state overlay of its can reach a descendant.</summary>
    [Fact]
    public void SetAppearance_WhenTheControlsAmbientFaceIsNormal_DoesNotRenderInvalidateDescendants()
    {
        using var container = new NormalAmbientProbeContainer();
        var child = new ProbeControl();
        container.Children.Add(child);
        container.Clear(Invalidation.All);
        child.Clear(Invalidation.All);

        container.Author(
            VisualState.PointerOver,
            new AppearanceOverlay(face: new FaceOverlay(foreground: SemanticColor.Accent)));

        ((child.Pending & Invalidation.Render) != 0).ShouldBeFalse();
    }

    // Both concrete controls whose ambient face is their active state - Button, via PressableBase,
    // and ListItem - are sealed, so the flag is set here directly instead. That keeps these two
    // tests on the branch being changed rather than on which controls happen to set the flag, which
    // PressableBase and ListItem already assert for themselves.

    /// <summary>A container with its own primary style slot, so <c>CommitStyle</c> runs on a
    /// control that actually has descendants. The library controls that own a style slot are either
    /// sealed or Pressables, whose ambient face is their active state.</summary>
    private sealed class StyledProbeContainer: Container
    {
        private readonly StyleSlot<ButtonStyle> _style;

        internal StyledProbeContainer() => _style = InitializeStyle(ButtonStyle.Definition);

        internal ButtonStyle? Style
        {
            get => _style.Local;
            set => _style.Local = value;
        }

        internal ButtonStyle ActualStyle => _style.Actual;

        internal void Author(VisualState state, AppearanceOverlay? appearance) =>
            SetAppearance(state, appearance);

        protected override Size MeasureOverride(Constraint constraint)
        {
            _ = constraint;
            return default;
        }

        protected override void ArrangeOverride(Rect bounds) => _ = bounds;
    }

    /// <summary>Republishes the protected authoring seam on a control whose ambient face is its
    /// active state.</summary>
    private sealed class AppearanceProbeContainer: Container
    {
        internal override bool StateAffectsAmbientAppearance => true;

        internal void Author(VisualState state, AppearanceOverlay? appearance) =>
            SetAppearance(state, appearance);

        protected override Size MeasureOverride(Constraint constraint)
        {
            _ = constraint;
            return default;
        }

        protected override void ArrangeOverride(Rect bounds) => _ = bounds;
    }

    /// <summary>The same seam on a control whose ambient face is its Normal.</summary>
    private sealed class NormalAmbientProbeContainer: Container
    {
        internal void Author(VisualState state, AppearanceOverlay? appearance) =>
            SetAppearance(state, appearance);

        protected override Size MeasureOverride(Constraint constraint)
        {
            _ = constraint;
            return default;
        }

        protected override void ArrangeOverride(Rect bounds) => _ = bounds;
    }
}
