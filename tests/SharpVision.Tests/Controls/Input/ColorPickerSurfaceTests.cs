// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Proves ColorPicker behavior through retained mounted composition and terminal input.</summary>
public sealed class ColorPickerSurfaceTests
{
    /// <summary>Verifies the sixth retained focus stop edits the selected color immediately through
    /// ordinary mounted TextInput selection and text input, without waiting for Enter.</summary>
    [Fact]
    public async Task Input_WhenRgbTextIsTyped_CommitsColorWithoutSubmitAsync()
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

        // Act: plane, hue, red, green, blue, then the retained value editor.
        for (var index = 0; index < 6; index++)
        {
            await surface.Keyboard.PressAsync(Code.Tab);
        }

        await surface.Keyboard.PressAsync(Code.Home);
        await surface.Keyboard.PressAsync(Code.End, Modifiers.Shift);
        await surface.Keyboard.TypeAsync("rgb(12, 34, 56)");

        // Assert
        surface.ShouldHaveFocus(picker.ValueTextInput);
        picker.Value.ShouldBe(Color.Rgb(12, 34, 56));
        picker.ValueTextInput.Text.ShouldBe("#0C2238");
        picker.RedSlider.Value.ShouldBe(12);
        picker.GreenSlider.Value.ShouldBe(34);
        picker.BlueSlider.Value.ShouldBe(56);
        changes.ShouldBe(1);
    }

    /// <summary>Verifies invalid mounted text keeps focus and raw input while painting the semantic
    /// error face, including after caller style and application Theme changes.</summary>
    [Fact]
    public async Task Render_WhenRgbTextIsInvalid_KeepsFocusedTextAndRefreshesErrorContrastAsync()
    {
        // Arrange
        var picker = new ColorPicker
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        await using var surface = await ComponentSurface.MountAsync(
            picker,
            new Size(40, 18),
            TestContext.Current.CancellationToken);

        for (var index = 0; index < 6; index++)
        {
            await surface.Keyboard.PressAsync(Code.Tab);
        }

        await surface.Keyboard.PressAsync(Code.Home);
        await surface.Keyboard.PressAsync(Code.End, Modifiers.Shift);

        // Act
        await surface.Keyboard.TypeAsync("rgb(1, 2,");
        await surface.UpdateAsync(
            () => picker.Style = new ColorPickerStyle(
                ControlStyle.DefaultFace,
                ControlStyle.NoBorder,
                ControlStyle.NoShadow,
                null,
                ColorPickerStyle.DefaultStatusFace with
                {
                    Background = Color.Rgb(1, 2, 3),
                    Attributes = TerminalAttributes.Bold
                },
                null),
            "change ColorPicker status style while value text is invalid");
        await surface.UpdateAsync(
            () => surface.Application.Theme = ThemeCatalog.Load("default-light"),
            "change Theme while ColorPicker value text is invalid");

        // Assert
        surface.ShouldHaveFocus(picker.ValueTextInput);
        picker.ValueTextInput.Text.ShouldBe("rgb(1, 2,");
        picker.Value.ShouldBe(Color.Rgb(255, 0, 0));
        picker.ActualStyle.StatusFace.ShouldNotBeNull().Background.ShouldBe((ControlColor) SemanticColor.Error);
        picker.ValueTextInput.ActualStyle.Face.Background.ShouldBe((ControlColor) SemanticColor.Error);
        picker.ValueTextInput.ActualStyle.Face.Attributes.ShouldBe((ControlDecoration) TerminalAttributes.Bold);
        picker.ValueTextInput.ActualStyle.Face.Foreground.ShouldBe((ControlColor) ThemeCatalog.White.Error.Contrast());

        var cell = surface.Cell(new Point(picker.ValueTextInput.Bounds.X, picker.ValueTextInput.Bounds.Y));
        cell.Text.ShouldBe("r");
        cell.Style.Background.ShouldBe(ThemeCatalog.White.Error);
        cell.Style.Foreground.ShouldBe(ThemeCatalog.White.Error.Contrast());
    }

    /// <summary>Verifies direct value changes replace invalid text while focused and canonical text
    /// while unfocused without taking focus from another retained part.</summary>
    [Fact]
    public async Task Value_WhenChangedWhileEditorFocusedOrUnfocused_CanonicalizesWithoutMovingFocusAsync()
    {
        // Arrange
        var picker = new ColorPicker
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        await using var surface = await ComponentSurface.MountAsync(
            picker,
            new Size(40, 18),
            TestContext.Current.CancellationToken);

        for (var index = 0; index < 6; index++)
        {
            await surface.Keyboard.PressAsync(Code.Tab);
        }

        await surface.UpdateAsync(
            () => picker.ValueTextInput.Text = "invalid",
            "enter invalid focused ColorPicker text");

        // Act and assert while focused.
        await surface.UpdateAsync(
            () => picker.Value = Color.Rgb(1, 2, 3),
            "change ColorPicker value while its editor is focused");
        surface.ShouldHaveFocus(picker.ValueTextInput);
        picker.ValueTextInput.Text.ShouldBe("#010203");

        // Act and assert while unfocused.
        await surface.Keyboard.PressAsync(Code.Tab);
        picker.ValueTextInput.IsFocused.ShouldBeFalse();
        var focus = surface.Application.Focus.Focused;
        await surface.UpdateAsync(
            () => picker.Value = Color.Default,
            "change ColorPicker value while its editor is unfocused");
        surface.ShouldHaveFocus(focus);
        picker.ValueTextInput.Text.ShouldBe("DEFAULT");
    }

    /// <summary>Verifies the wider retained value row saturates inside narrow picker bounds rather
    /// than overflowing its Overlay or exposing partial editor chrome.</summary>
    [Fact]
    public async Task Layout_WhenPickerIsNarrow_ContainsValueEditorAndPreviewAsync()
    {
        var picker = new ColorPicker
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        await using var surface = await ComponentSurface.MountAsync(
            picker,
            new Size(12, 9),
            TestContext.Current.CancellationToken);

        picker.ValueTextInput.Bounds.X.ShouldBeGreaterThanOrEqualTo(picker.Bounds.X);
        picker.ValueTextInput.Bounds.Right.ShouldBeLessThanOrEqualTo(picker.Bounds.Right);
        picker.ValueTextInput.Bounds.Y.ShouldBeGreaterThanOrEqualTo(picker.Bounds.Y);
        picker.ValueTextInput.Bounds.Bottom.ShouldBeLessThanOrEqualTo(picker.Bounds.Bottom);
        picker.Preview.Bounds.ShouldBe(picker.ValueTextInput.Bounds);
    }

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
                TerminalCapabilities.Conservative with { ColorDepth = ColorDepth.TrueColor }),
            "enable true-color ColorPicker branch");
        changes = 0;

        // Assert retained public role
        picker.OwnedControlCount.ShouldBe(1);
        picker.CanFocus.ShouldBeFalse();
        picker.CanTabStop.ShouldBeFalse();
        picker.IsRgbEditorVisible.ShouldBeTrue();

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
        surface.ShouldHaveState(picker, VisualState.Disabled);

        // Act re-enable and resume interaction through the retained plane, moving to a different
        // plane cell than the first press so a genuinely new value is guaranteed to commit. The
        // disable cleanup dropped the control-side capture and press without the test-side pointer
        // driver observing it, so release its own bookkeeping before pressing again.
        var previousValue = picker.Value;
        await surface.Pointer.ReleaseAsync();
        await surface.UpdateAsync(() => picker.IsEnabled = true, "re-enable ColorPicker");
        surface.ShouldHaveState(picker, VisualState.Normal);
        await surface.Pointer.MoveToAsync(picker.Plane, new Point(picker.Plane.Bounds.Width - 1, picker.Plane.Bounds.Height - 1));
        await surface.Pointer.PressAsync();

        // Assert normal interaction resumes
        surface.ShouldHaveFocus(picker.Plane);
        surface.ShouldHaveCapture(picker.Plane);
        changes.ShouldBeGreaterThan(1);
        picker.Value.ShouldNotBe(previousValue);
    }

    /// <summary>Verifies a ColorPicker inherits disabled state from an ancestor and keeps stable
    /// geometry across a genuine resize while disabled, matching an independently-mounted enabled
    /// instance arranged at the same size.</summary>
    [Fact]
    public async Task Input_WhenAncestorDisablesColorPickerAndResized_InheritsStateAndPreservesGeometryAsync()
    {
        // Arrange a ColorPicker disabled only through its ancestor
        var picker = new ColorPicker
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        var overlay = new Overlay
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { picker }
        };
        await using var surface = await ComponentSurface.MountAsync(
            overlay,
            new Size(40, 18),
            TestContext.Current.CancellationToken);

        // Act disable the ancestor, not the ColorPicker itself
        await surface.UpdateAsync(() => overlay.IsEnabled = false, "disable ColorPicker's ancestor");

        // Assert the disabled state is inherited
        picker.EffectiveIsEnabled.ShouldBeFalse();
        surface.ShouldHaveState(picker, VisualState.Disabled);

        // Act resize to a genuinely different size while disabled
        await surface.ResizeAsync(new Size(50, 24));

        // Assert geometry matches an independently-mounted enabled instance at the same size
        var reference = new ColorPicker
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        await using var referenceSurface = await ComponentSurface.MountAsync(
            reference,
            new Size(50, 24),
            TestContext.Current.CancellationToken);

        picker.Bounds.ShouldBe(reference.Bounds);
        picker.DesiredSize.ShouldBe(reference.DesiredSize);
    }

    /// <summary>Verifies a focused ColorPlane renders reverse video on its outer ring of cells only.
    /// ColorPlane is borderless and fully covered by dynamic per-cell HSV content, so - unlike
    /// Table/TreeView/JsonView, which get a themed border recolor - reversing every cell would
    /// obscure the gradient being picked. <see cref="ColorPlane"/>
    /// mirrors the Slider borderless-focus fallback (a literal <see cref="TerminalAttributes.Reverse"/>
    /// swap) but confines it to the edge cells so the interior gradient and the selected marker
    /// stay legible.</summary>
    [Fact]
    public async Task Render_WhenPlaneReceivesFocus_AppliesReverseAttributeToEdgeCellsOnlyAsync()
    {
        // Arrange
        var picker = new ColorPicker
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        await using var surface = await ComponentSurface.MountAsync(
            picker,
            new Size(40, 18),
            TestContext.Current.CancellationToken);

        // Center the selection so the marker sits well inside the ring, not on it.
        await surface.UpdateAsync(
            () => picker.Plane.SetSelection(180, 0.5, 0.5),
            "center the ColorPlane selection away from its edges");

        var planeBounds = picker.Plane.Bounds;
        planeBounds.Width.ShouldBeGreaterThan(2);
        planeBounds.Height.ShouldBeGreaterThan(2);
        var edgeCell = new Point(planeBounds.X, planeBounds.Y);
        var markerX = (int) Math.Round(0.5 * (planeBounds.Width - 1), MidpointRounding.AwayFromZero);
        var markerY = (int) Math.Round(0.5 * (planeBounds.Height - 1), MidpointRounding.AwayFromZero);
        var markerCell = new Point(planeBounds.X + markerX, planeBounds.Y + markerY);
        var markerGlyphBefore = surface.Cell(markerCell).Text;

        // Assert no Reverse before focus.
        (surface.Cell(edgeCell).Style.Attributes & TerminalAttributes.Reverse).ShouldBe(TerminalAttributes.None);
        (surface.Cell(markerCell).Style.Attributes & TerminalAttributes.Reverse).ShouldBe(TerminalAttributes.None);

        // Act
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert the plane took focus and only its ring reversed.
        surface.ShouldHaveFocus(picker.Plane);
        (surface.Cell(edgeCell).Style.Attributes & TerminalAttributes.Reverse).ShouldBe(TerminalAttributes.Reverse);
        (surface.Cell(markerCell).Style.Attributes & TerminalAttributes.Reverse).ShouldBe(TerminalAttributes.None);
        surface.Cell(markerCell).Text.ShouldBe(markerGlyphBefore);
    }

    private static void AssertPreview(
        ComponentSurface surface,
        ColorPicker picker,
        string text,
        Color background,
        Color foreground)
    {
        var bounds = picker.ValueTextInput.Bounds;
        var projectedBackground = TerminalPalette.Project(background, picker.EffectiveColorDepth);
        var projectedForeground = TerminalPalette.Project(foreground, picker.EffectiveColorDepth);
        bounds.Width.ShouldBeGreaterThan(0);
        bounds.Height.ShouldBe(1);

        for (var index = 0; index < bounds.Width; index++)
        {
            var cell = surface.Cell(new Point(bounds.X + index, bounds.Y));
            cell.Style.Background.ShouldBe(projectedBackground);

            if (index < text.Length)
            {
                cell.Text.ShouldBe(text[index].ToString());
                cell.Style.Foreground.ShouldBe(projectedForeground);
            }
        }
    }
}
