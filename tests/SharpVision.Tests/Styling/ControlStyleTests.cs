// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

/// <summary>Verifies the well-known base style types - ControlStyle and
/// its five sibling roles - construct correctly, expose the same code-owned per-role border/shadow
/// defaults the code-owned per-type switch used to, and round-trip through
/// IAppearanceFragment.Clone.</summary>
public sealed class ControlStyleTests
{
    /// <summary>Verifies ControlStyle's own default is borderless with no shadow, matching
    /// CreateDefaultAppearance()'s base case.</summary>
    [Fact]
    public void ControlStyle_Default_IsBorderlessWithNoShadow()
    {
        ControlStyle.Default.Border.Sides.ShouldBe(BorderSide.None);
        ControlStyle.Default.Shadow.Visible.ShouldBeFalse();
    }

    /// <summary>Verifies InputStyle's default is a heavy all-side border, matching
    /// the code-owned input default's prior behavior.</summary>
    [Fact]
    public void ThemeInputStyle_Default_IsHeavyAllSideBorder()
    {
        InputStyle.Default.Border.Sides.ShouldBe(BorderSide.All);
        InputStyle.Default.Border.GlyphStyle.ShouldBe(BorderGlyphStyle.Heavy);
        InputStyle.Default.Shadow.Visible.ShouldBeFalse();
    }

    /// <summary>Verifies ContainerStyle's default is a light all-side border, matching
    /// the code-owned container default's prior behavior.</summary>
    [Fact]
    public void ThemeContainerStyle_Default_IsLightAllSideBorder()
    {
        ContainerStyle.Default.Border.Sides.ShouldBe(BorderSide.All);
        ContainerStyle.Default.Border.GlyphStyle.ShouldBe(BorderGlyphStyle.Light);
    }

    /// <summary>Verifies WindowStyle's default is a paired all-side border with a visible
    /// shadow - the only well-known role that defaults to a visible shadow, matching
    /// the code-owned window default's prior behavior.</summary>
    [Fact]
    public void ThemeWindowStyle_Default_IsPairedBorderWithVisibleShadow()
    {
        WindowStyle.Default.Border.Sides.ShouldBe(BorderSide.All);
        WindowStyle.Default.Border.GlyphStyle.ShouldBe(BorderGlyphStyle.Paired);
        WindowStyle.Default.Shadow.Visible.ShouldBeTrue();
        WindowStyle.Default.Shadow.Offset.ShouldBe(new Point(2, 1));
    }

    /// <summary>Verifies PopupChrome's default is a rounded all-side border, matching
    /// the code-owned popup default's prior behavior.</summary>
    [Fact]
    public void ThemePopupStyle_Default_IsRoundedAllSideBorder()
    {
        PopupStyle.Default.Border.Sides.ShouldBe(BorderSide.All);
        PopupStyle.Default.Border.GlyphStyle.ShouldBe(BorderGlyphStyle.Rounded);
    }

    /// <summary>Verifies TooltipStyle's default is deliberately borderless - the same
    /// intentional asymmetry documented for the tooltip style, preserved here.</summary>
    [Fact]
    public void ThemeTooltipStyle_Default_IsBorderlessWithNoShadow()
    {
        TooltipStyle.Default.Border.Sides.ShouldBe(BorderSide.None);
        TooltipStyle.Default.Shadow.Visible.ShouldBeFalse();
    }

    /// <summary>Verifies a sibling role's Clone() (via the explicit IAppearanceFragment interface)
    /// returns an independent copy of the correct runtime type, not the base type - required for
    /// Overlay's recursion to preserve the concrete style's own shape when patching a property
    /// typed as a sibling role.</summary>
    [Fact]
    public void Clone_WhenCalledOnASiblingStyle_ReturnsTheSameConcreteRuntimeType()
    {
        IAppearanceFragment original = InputStyle.Default;

        var clone = original.Clone();

        _ = clone.ShouldBeOfType<InputStyle>();
        clone.ShouldNotBeSameAs(original);
    }

    /// <summary>Verifies StyleStates requires Normal but leaves every other state slot
    /// optional, mirroring AppearanceStates's own nine named slots for the formerly-fixed roles.</summary>
    [Fact]
    public void StyleStates_WhenOnlyNormalIsSupplied_LeavesEveryOtherSlotNull()
    {
        var set = new StyleStates<InputStyle> { Normal = InputStyle.Default };

        set.Normal.ShouldBeSameAs(InputStyle.Default);
        set.PointerOver.ShouldBeNull();
        set.Focused.ShouldBeNull();
        set.Disabled.ShouldBeNull();
    }

    /// <summary>The regression this pins: every well-known root's static Default must be built from
    /// the code-owned Face/Border/Shadow, not from zero-initialized structs. ControlStyle
    /// declares Default alongside the three members it reads, so a declaration above them captured
    /// default(Face)/default(Border)/default(Shadow) - static property initializers run in textual
    /// order. A zeroed Face carries a Color.Default background instead of the declared Transparent
    /// one, which closes the ambient-inheritance gate in AppearanceResolver (it inherits only when
    /// the resolved background is Transparent) and stops transparent controls from picking up their
    /// parent's face at all. Asserting the transparent background specifically, rather than
    /// equality with DefaultFace, keeps this meaningful even if both were zeroed together.</summary>
    [Fact]
    public void Default_ForEveryWellKnownRoot_IsBuiltFromCodeOwnedValuesNotZeroedStructs()
    {
        ControlStyle.Default.Face.Background.Literal.ShouldBe(Color.Transparent);
        InputStyle.Default.Face.Background.Literal.ShouldBe(Color.Transparent);
        ContainerStyle.Default.Face.Background.Literal.ShouldBe(Color.Transparent);
        WindowStyle.Default.Face.Background.Literal.ShouldBe(Color.Transparent);
        PopupStyle.Default.Face.Background.Literal.ShouldBe(Color.Transparent);
        TooltipStyle.Default.Face.Background.Literal.ShouldBe(Color.Transparent);

        // The distinct code-owned border each root bakes in is the other half of the same evidence:
        // a zeroed Border would collapse all six to BorderSide.None with the default glyph style.
        ControlStyle.Default.Border.Sides.ShouldBe(BorderSide.None);
        InputStyle.Default.Border.GlyphStyle.ShouldBe(BorderGlyphStyle.Heavy);
        ContainerStyle.Default.Border.GlyphStyle.ShouldBe(BorderGlyphStyle.Light);
        WindowStyle.Default.Border.GlyphStyle.ShouldBe(BorderGlyphStyle.Paired);
        PopupStyle.Default.Border.GlyphStyle.ShouldBe(BorderGlyphStyle.Rounded);
    }

    /// <summary>Verifies a transparent control actually inherits its parent's ambient face, the
    /// behavior the zeroed default silently disabled. Existing ambient coverage drives color
    /// through a theme's "control" section, which kept working, so nothing caught the loss of
    /// parent-Face-derived inheritance.</summary>
    [Fact]
    public void AmbientFace_WhenParentAuthorsAFace_IsInheritedByATransparentChild()
    {
        var theme = new Theme();
        theme.Freeze();
        var child = new ProbeControl();
        var parent = new ProbeContainer { Face = AppearanceTestValues.Face(foreground: Color.Rgb(1, 2, 3)) };
        parent.Children.Add(child);
        parent.PropagateTheme(theme);

        child.ActualFace.Foreground.Literal.ShouldBe(Color.Rgb(1, 2, 3));
    }
}
