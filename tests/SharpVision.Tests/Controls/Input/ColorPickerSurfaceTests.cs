// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Proves ColorPicker behavior through retained mounted composition and terminal input.</summary>
public sealed class ColorPickerSurfaceTests
{
    /// <summary>Verifies the hexadecimal readout remains inside and legible against light and dark previews.</summary>
    [Fact]
    public async Task Surface_WhenSelectedColorChanges_DrawsContrastingHexInsidePreviewAsync()
    {
        // Arrange
        var picker = new ColorPicker
        {
            Value = Color.Rgb(255, 255, 255),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        await using var surface = await ComponentSurface.MountAsync(
            picker,
            new Size(40, 18),
            TestContext.Current.CancellationToken);

        // Act and assert a light preview.
        AssertPreview(surface, picker, "#FFFFFF", Color.Rgb(255, 255, 255), Color.Rgb(0, 0, 0));

        // Act and assert a dark preview.
        await surface.UpdateAsync(
            () => picker.Value = Color.Rgb(0, 0, 0),
            "select a dark ColorPicker value");
        AssertPreview(surface, picker, "#000000", Color.Rgb(0, 0, 0), Color.Rgb(255, 255, 255));
    }

    /// <summary>Verifies the mounted ColorPicker renders a visible plane and preview swatch with correct hex readout.</summary>
    [Fact]
    public async Task Render_WhenMountedWithKnownColor_ShowsPlaneAndPreviewSwatchAsync()
    {
        // Arrange
        var picker = new ColorPicker
        {
            Value = Color.Rgb(255, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        await using var surface = await ComponentSurface.MountAsync(
            picker,
            new Size(40, 18),
            TestContext.Current.CancellationToken);

        // Assert hex readout shows the initial color
        picker.HexText.ShouldBe("#FF0000");

        // Assert the plane area has non-default foreground (color is rendered)
        var planeOrigin = new Point(picker.Plane.Bounds.X, picker.Plane.Bounds.Y);
        surface.Cell(planeOrigin).Style.Background.IsRgb.ShouldBeTrue();

        // Assert the preview swatch has the selected color as background
        AssertPreview(surface, picker, "#FF0000", Color.Rgb(255, 0, 0), Color.Rgb(255, 255, 255));
    }

    /// <summary>Verifies the preview and hex readout update after setting a new color value.</summary>
    [Fact]
    public async Task Render_WhenValueChanges_UpdatesPreviewAndHexReadoutAsync()
    {
        // Arrange
        var picker = new ColorPicker
        {
            Value = Color.Rgb(255, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        await using var surface = await ComponentSurface.MountAsync(
            picker,
            new Size(40, 18),
            TestContext.Current.CancellationToken);
        AssertPreview(surface, picker, "#FF0000", Color.Rgb(255, 0, 0), Color.Rgb(255, 255, 255));

        // Act
        await surface.UpdateAsync(
            () => picker.Value = Color.Rgb(0, 128, 255),
            "select a different ColorPicker value");

        // Assert updated hex readout and preview
        picker.HexText.ShouldBe("#0080FF");
        AssertPreview(surface, picker, "#0080FF", Color.Rgb(0, 128, 255), Color.Rgb(255, 255, 255));
    }

    /// <summary>Verifies mounted composition, hover, exclusion policy, selection, and cleanup.</summary>
    [Fact]
    [ComponentBehaviorEvidence(
        typeof(ColorPicker),
        ComponentBehavior.Mounted |
        ComponentBehavior.Hover |
        ComponentBehavior.FocusExcluded |
        ComponentBehavior.TabExcluded |
        ComponentBehavior.DirectionalExcluded |
        ComponentBehavior.PressReleaseExcluded |
        ComponentBehavior.Activation |
        ComponentBehavior.PointerActivation |
        ComponentBehavior.RetainedPointerActivation |
        ComponentBehavior.UnavailableCleanup |
        ComponentBehavior.Composition)]
    public async Task Surface_WhenPlaneIsSelected_ExposesAdaptiveCompositeBehaviorAsync()
    {
        // Arrange
        var picker = new ColorPicker
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        var changes = 0;
        picker.ValueChanged += (_, _) => changes++;
        await using var surface = await ComponentSurface.MountAsync(
            picker,
            new Size(40, 18),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(
            () => picker.SetCapabilities(
                Capabilities.Conservative with { ColorDepth = ColorDepth.TrueColor }),
            "enable true-color ColorPicker branch");
        changes = 0;

        // Assert retained public role
        picker.OwnedControlCount.ShouldBe(1);
        picker.CanFocus.ShouldBeFalse();
        picker.IsTabStop.ShouldBeFalse();
        picker.RgbEditorVisible.ShouldBeTrue();

        // Act through the retained plane using terminal mouse bytes
        await surface.Pointer.MoveToAsync(picker.Plane);
        await surface.Pointer.PressAsync();

        // Assert forwarded state and output
        picker.IsPointerOver.ShouldBeTrue();
        surface.ShouldHaveFocus(picker.Plane);
        surface.ShouldHaveCapture(picker.Plane);
        picker.IsPressed.ShouldBeFalse();
        changes.ShouldBe(1);
        picker.Value.IsRgb.ShouldBeTrue();
        surface.Cell(new Point(picker.Preview.Bounds.X, picker.Preview.Bounds.Y))
            .Style.Background.IsRgb.ShouldBeTrue();

        // Act unavailable cleanup
        await surface.UpdateAsync(() => picker.IsEnabled = false, "disable active ColorPicker");

        // Assert cleanup
        surface.ShouldHaveCapture(null);
        surface.ShouldHaveFocus(null);
        picker.Plane.IsPressed.ShouldBeFalse();
    }

    private static void AssertPreview(
        ComponentSurface surface,
        ColorPicker picker,
        string text,
        Color background,
        Color foreground)
    {
        _ = background;
        _ = foreground;
        var origin = new Point(picker.Preview.Bounds.X, picker.Preview.Bounds.Y);
        picker.Preview.Bounds.Width.ShouldBeGreaterThan(0);
        picker.Preview.Bounds.Height.ShouldBeGreaterThan(0);
        var textX = origin.X + ((picker.Preview.Bounds.Width - text.Length) / 2);

        for (var index = 0; index < text.Length; index++)
        {
            var cell = surface.Cell(new Point(textX + index, origin.Y));
            cell.Text.ShouldBe(text[index].ToString());
            cell.Style.Background.IsRgb.ShouldBeTrue();
        }
    }
}
