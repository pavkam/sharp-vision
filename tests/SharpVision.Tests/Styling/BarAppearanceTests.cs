// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

/// <summary>Verifies BarAppearance.Rebase forwards a theme-authored Face.AccessKeyColor through
/// both the physical-hover and disabled overlay rebuilds, alongside each rebuild's own background
/// handling - physical hover drops the authored background to keep the continuous Bar plane, and
/// disabled forces the Bar background back on, but neither may drop the access-key color along
/// the way.</summary>
public sealed class BarAppearanceTests
{
    private static readonly InputStyle _normal = InputStyle.Default;

    /// <summary>Verifies a pointer-over state authoring only Face.Background and
    /// Face.AccessKeyColor keeps its access-key color once physical hover drops the background to
    /// avoid punching a control-colored hole through the Bar plane.</summary>
    [Fact]
    public void Rebase_WhenPointerOverAuthorsBackgroundAndAccessKeyColor_KeepsAccessKeyColorAndDropsBackground()
    {
        var pointerOver = _normal with
        {
            Face = _normal.Face with
            {
                Background = SemanticColor.Surface,
                AccessKeyColor = SemanticColor.Accent
            }
        };
        var states = new StyleStates<InputStyle>
        {
            Normal = _normal,
            IsPointerOver = pointerOver
        };

        var rebased = BarAppearance.Rebase(states);

        var face = rebased.IsPointerOver.Face.ShouldNotBeNull();
        face.Background.ShouldBeNull();
        face.AccessKeyColor.ShouldBe((ControlColor) SemanticColor.Accent);
    }

    /// <summary>Verifies a disabled state authoring only Face.Background and Face.AccessKeyColor
    /// keeps its access-key color once disablement forces the background back onto the Bar plane.
    /// The disabled Bar face never actually paints an access-key grapheme today, so this is a
    /// consistency fix rather than one that changes rendered output, but the rebuilt overlay itself
    /// must still carry the member it was given.</summary>
    [Fact]
    public void Rebase_WhenDisabledAuthorsBackgroundAndAccessKeyColor_CarriesBarBackgroundAndKeepsAccessKeyColor()
    {
        var disabled = _normal with
        {
            Face = _normal.Face with
            {
                Background = SemanticColor.Surface,
                AccessKeyColor = SemanticColor.Error
            }
        };
        var states = new StyleStates<InputStyle>
        {
            Normal = _normal,
            Disabled = disabled
        };

        var rebased = BarAppearance.Rebase(states);

        var face = rebased.Disabled.Face.ShouldNotBeNull();
        face.Background.ShouldBe((ControlColor) SemanticColor.Bar);
        face.AccessKeyColor.ShouldBe((ControlColor) SemanticColor.Error);
    }
}
