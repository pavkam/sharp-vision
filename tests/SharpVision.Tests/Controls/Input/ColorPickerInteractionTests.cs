// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Proves ColorPicker interactions through a mounted terminal surface and the HSV/RGB
/// conversions they rest on: focus order across the retained parts, keyboard editing of the
/// plane and every slider, pointer drags that leave the plane, value round-trips at the HSV
/// boundaries (hue wrap, greys, black, white), ValueChanged ordering and arguments, color-depth
/// presentation, disabled input, and tiny bounds.</summary>
public sealed class ColorPickerInteractionTests
{
    #region Focus order and part synchronization

    /// <summary>Verifies Tab visits the plane, hue slider, and the red, green, and blue sliders in
    /// that order, and Shift+Tab walks back.</summary>
    [Fact]
    public async Task Tab_WhenPressedRepeatedly_VisitsPlaneThenHueThenRgbSlidersAsync()
    {
        var picker = NewPicker();
        await using var surface = await MountAsync(picker, new Size(40, 18));

        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(picker.Plane);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(picker.HueSlider);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(picker.RedSlider);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(picker.GreenSlider);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(picker.BlueSlider);

        await surface.Keyboard.PressAsync(Code.Tab, Modifiers.Shift);
        surface.ShouldHaveFocus(picker.GreenSlider);
    }

    /// <summary>Verifies the hue slider's keyboard contract commits a new value: Right steps hue
    /// by one degree, End reaches 359, Home returns to 0, and every commit keeps the plane's hue,
    /// the RGB sliders, and the readout synchronized.</summary>
    [Fact]
    public async Task HueSlider_WhenEditedByKeyboard_CommitsHueAndSynchronizesPartsAsync()
    {
        var picker = NewPicker();
        var changes = new List<ColorChangedEventArgs>();
        picker.ValueChanged += (_, eventArgs) => changes.Add(eventArgs);
        await using var surface = await MountAsync(picker, new Size(40, 18));
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(picker.HueSlider);

        await surface.Keyboard.PressAsync(Code.Right);

        picker.HueSlider.Value.ShouldBe(1);
        picker.Plane.Hue.ShouldBe(1);
        picker.Plane.Saturation.ShouldBe(1);
        picker.Plane.Value.ShouldBe(1);
        picker.Value.ShouldBe(Color.Rgb(255, 4, 0));
        picker.RedSlider.Value.ShouldBe(255);
        picker.GreenSlider.Value.ShouldBe(4);
        picker.BlueSlider.Value.ShouldBe(0);
        picker.HexText.ShouldBe("#FF0400");
        changes.Count.ShouldBe(1);
        changes[0].PreviousValue.ShouldBe(Color.Rgb(255, 0, 0));
        changes[0].Value.ShouldBe(Color.Rgb(255, 4, 0));

        await surface.Keyboard.PressAsync(Code.End);

        picker.HueSlider.Value.ShouldBe(359);
        picker.Plane.Hue.ShouldBe(359);
        picker.Value.ShouldBe(Color.Rgb(255, 0, 4));
        picker.HexText.ShouldBe("#FF0004");

        await surface.Keyboard.PressAsync(Code.Home);

        picker.HueSlider.Value.ShouldBe(0);
        picker.Value.ShouldBe(Color.Rgb(255, 0, 0));
        changes.Count.ShouldBe(3);
        changes[2].PreviousValue.ShouldBe(Color.Rgb(255, 0, 4));
    }

    /// <summary>Verifies a hue edit preserves the plane's saturation and value instead of
    /// resetting them, so a muted color keeps its shade while its hue rotates.</summary>
    [Fact]
    public async Task HueSlider_WhenEditedOnMutedColor_PreservesSaturationAndValueAsync()
    {
        var picker = NewPicker();
        picker.Value = Color.Rgb(128, 64, 64);
        await using var surface = await MountAsync(picker, new Size(40, 18));
        picker.Plane.Hue.ShouldBe(0);
        var saturation = picker.Plane.Saturation;
        var value = picker.Plane.Value;
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Tab);

        await surface.Keyboard.PressAsync(Code.PageUp);

        picker.HueSlider.Value.ShouldBe(10);
        picker.Plane.Hue.ShouldBe(10);
        picker.Plane.Saturation.ShouldBe(saturation, tolerance: 0.005);
        picker.Plane.Value.ShouldBe(value, tolerance: 0.005);
        picker.Value.ShouldBe(Color.FromHsv(10, saturation, value));
    }

    /// <summary>Verifies each RGB slider's keyboard contract edits exactly its own channel, with
    /// Right, PageUp, End, and Home mapping to +1, +10, maximum, and minimum.</summary>
    [Theory]
    [InlineData("red", 2)]
    [InlineData("green", 3)]
    [InlineData("blue", 4)]
    public async Task RgbSlider_WhenEditedByKeyboard_CommitsOnlyItsChannelAsync(string channel, int tabs)
    {
        var picker = NewPicker();
        picker.Value = Color.Rgb(100, 100, 100);
        var changes = 0;
        picker.ValueChanged += (_, _) => changes++;
        await using var surface = await MountAsync(picker, new Size(40, 18));

        for (var press = 0; press < tabs + 1; press++)
        {
            await surface.Keyboard.PressAsync(Code.Tab);
        }

        var slider = channel switch
        {
            "red" => picker.RedSlider,
            "green" => picker.GreenSlider,
            _ => picker.BlueSlider
        };
        surface.ShouldHaveFocus(slider);

        await surface.Keyboard.PressAsync(Code.Right);
        Channel(picker.Value, channel).ShouldBe(101);
        OtherChannels(picker.Value, channel).ShouldAllBe(component => component == 100);

        await surface.Keyboard.PressAsync(Code.PageUp);
        Channel(picker.Value, channel).ShouldBe(111);

        await surface.Keyboard.PressAsync(Code.End);
        Channel(picker.Value, channel).ShouldBe(255);

        await surface.Keyboard.PressAsync(Code.Home);
        Channel(picker.Value, channel).ShouldBe(0);
        OtherChannels(picker.Value, channel).ShouldAllBe(component => component == 100);
        changes.ShouldBe(4);
        picker.HexText.ShouldBe($"#{picker.Value.Red:X2}{picker.Value.Green:X2}{picker.Value.Blue:X2}");
        picker.Plane.Hue.ShouldBe(HueOf(picker.Value));
    }

    /// <summary>Verifies plane arrow keys commit through the picker: each press raises exactly one
    /// ValueChanged whose PreviousValue is the prior commit, and the RGB sliders and readout follow.</summary>
    [Fact]
    public async Task Plane_WhenEditedByKeyboard_CommitsWithPreviousValueAndSyncsSlidersAsync()
    {
        var picker = NewPicker();
        var changes = new List<ColorChangedEventArgs>();
        picker.ValueChanged += (_, eventArgs) => changes.Add(eventArgs);
        await using var surface = await MountAsync(picker, new Size(40, 18));
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(picker.Plane);

        await surface.Keyboard.PressAsync(Code.Left);

        // The plane asked for saturation 0.99; the committed 8-bit RGB re-derives to 252/255,
        // so the plane's coordinates always mirror the committed color, not the raw request.
        var expected = Color.FromHsv(0, 0.99, 1);
        expected.ShouldBe(Color.Rgb(255, 3, 3));
        picker.Value.ShouldBe(expected);
        picker.Plane.Saturation.ShouldBe(252 / 255d, tolerance: 1e-9);
        picker.Plane.Value.ShouldBe(1);
        picker.RedSlider.Value.ShouldBe(255);
        picker.GreenSlider.Value.ShouldBe(3);
        picker.BlueSlider.Value.ShouldBe(3);
        picker.HexText.ShouldBe("#FF0303");
        changes.Count.ShouldBe(1);
        changes[0].PreviousValue.ShouldBe(Color.Rgb(255, 0, 0));
        changes[0].Value.ShouldBe(expected);
        var expectedSecond = Color.FromHsv(0, picker.Plane.Saturation, 0.99);

        await surface.Keyboard.PressAsync(Code.Down);

        changes.Count.ShouldBe(2);
        changes[1].PreviousValue.ShouldBe(expected);
        changes[1].Value.ShouldBe(expectedSecond);
        picker.Value.ShouldBe(expectedSecond);
        picker.Plane.Value.ShouldBe(expectedSecond.Red / 255d, tolerance: 1e-9);
    }

    /// <summary>Verifies a Shift- or Control-modified plane arrow, and PageUp/PageDown, change
    /// nothing and raise no value event.</summary>
    [Theory]
    [InlineData(Code.Right, Modifiers.Shift)]
    [InlineData(Code.Right, Modifiers.Control)]
    [InlineData(Code.Up, Modifiers.Alt)]
    [InlineData(Code.PageUp, Modifiers.None)]
    [InlineData(Code.PageDown, Modifiers.None)]
    public async Task Plane_WhenKeyIsModifiedOrUnbound_LeavesSelectionUnchangedAsync(Code code, Modifiers modifiers)
    {
        var picker = NewPicker();
        picker.Value = Color.Rgb(128, 64, 64);
        var changes = 0;
        picker.ValueChanged += (_, _) => changes++;
        await using var surface = await MountAsync(picker, new Size(40, 18));
        await surface.Keyboard.PressAsync(Code.Tab);
        var saturation = picker.Plane.Saturation;
        var value = picker.Plane.Value;

        await surface.Keyboard.PressAsync(code, modifiers);

        picker.Plane.Saturation.ShouldBe(saturation);
        picker.Plane.Value.ShouldBe(value);
        picker.Value.ShouldBe(Color.Rgb(128, 64, 64));
        changes.ShouldBe(0);
        surface.ShouldHaveFocus(picker.Plane);
    }

    /// <summary>Verifies PropertyChanged(Value) precedes the single ValueChanged, ActualStyle is
    /// republished for the value-dependent readout foreground, the retained parts are already
    /// synchronized when ValueChanged observers run, and a same-value assignment publishes nothing.</summary>
    [Fact]
    public async Task Value_WhenAssigned_PublishesPropertyThenTypedEventAsync()
    {
        var picker = NewPicker();
        var events = new List<string>();
        picker.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName is nameof(ColorPicker.Value) or nameof(ColorPicker.ActualStyle))
            {
                events.Add($"PropertyChanged:{eventArgs.PropertyName}");
            }
        };
        picker.ValueChanged += (_, _) =>
        {
            events.Add("ValueChanged");
            picker.HexText.ShouldBe("#0080FF");
            picker.BlueSlider.Value.ShouldBe(255);
        };
        await using var surface = await MountAsync(picker, new Size(40, 18));

        await surface.UpdateAsync(() => picker.Value = Color.Rgb(0, 128, 255), "assign a value");

        events.Count(entry => entry == "ValueChanged").ShouldBe(1);
        events.IndexOf("PropertyChanged:Value").ShouldBeLessThan(events.IndexOf("ValueChanged"));
        events[^1].ShouldBe("ValueChanged");
        events.ShouldContain("PropertyChanged:ActualStyle");
        events.Clear();

        await surface.UpdateAsync(() => picker.Value = Color.Rgb(0, 128, 255), "assign the same value");

        events.ShouldBeEmpty();
    }

    #endregion

    #region Pointer

    /// <summary>Verifies a captured drag that leaves the plane keeps capture, clamps the selection
    /// to the plane's edge, and releasing outside ends the drag without a further commit.</summary>
    [Fact]
    public async Task Plane_WhenDragLeavesThePlane_ClampsAndReleasesCleanlyAsync()
    {
        var picker = NewPicker();
        var changes = 0;
        picker.ValueChanged += (_, _) => changes++;
        await using var surface = await MountAsync(picker, new Size(40, 18));
        var plane = picker.Plane.Bounds;
        await surface.Pointer.MoveToAsync(picker.Plane, new Point(plane.Width / 2, plane.Height / 2));
        await surface.Pointer.PressAsync();
        surface.ShouldHaveCapture(picker.Plane);
        surface.ShouldHaveFocus(picker.Plane);
        changes.ShouldBe(1);

        await surface.Pointer.MovePressedToAsync(new Point(plane.Right + 2, plane.Bottom + 3));

        surface.ShouldHaveCapture(picker.Plane);
        // The drag clamps to the bottom-right corner (value 0); the committed RGB is black, and
        // re-deriving HSV from black collapses saturation to 0 because black has no chroma.
        picker.Plane.Value.ShouldBe(0);
        picker.Plane.Saturation.ShouldBe(0);
        picker.Value.ShouldBe(Color.Rgb(0, 0, 0));
        picker.HexText.ShouldBe("#000000");
        changes.ShouldBe(2);

        await surface.Pointer.ReleaseAsync();

        surface.ShouldHaveCapture(null);
        picker.Plane.IsPressed.ShouldBeFalse();
        changes.ShouldBe(2);
        picker.Value.ShouldBe(Color.Rgb(0, 0, 0));

        await surface.Pointer.MoveToAsync(picker.Plane, new Point(0, 0));

        changes.ShouldBe(2, "movement after release no longer edits");
    }

    /// <summary>Verifies pressing each plane corner maps to the documented saturation/value
    /// extremes and the exact RGB that follows. Both bottom corners commit black, whose
    /// re-derived HSV has zero saturation, so the plane reports saturation 0 for either.</summary>
    [Theory]
    [InlineData("top-left", 0.0, 1.0, 255, 255, 255)]
    [InlineData("top-right", 1.0, 1.0, 255, 0, 0)]
    [InlineData("bottom-left", 0.0, 0.0, 0, 0, 0)]
    [InlineData("bottom-right", 0.0, 0.0, 0, 0, 0)]
    public async Task Plane_WhenCornerIsPressed_MapsToExtremesAsync(
        string corner,
        double saturation,
        double value,
        int red,
        int green,
        int blue)
    {
        var picker = NewPicker();
        picker.Value = Color.Rgb(128, 64, 64);
        await using var surface = await MountAsync(picker, new Size(40, 18));
        var plane = picker.Plane.Bounds;
        var relative = corner switch
        {
            "top-left" => new Point(0, 0),
            "top-right" => new Point(plane.Width - 1, 0),
            "bottom-left" => new Point(0, plane.Height - 1),
            _ => new Point(plane.Width - 1, plane.Height - 1)
        };

        await surface.Pointer.ClickAsync(picker.Plane, relative);

        picker.Plane.Saturation.ShouldBe(saturation);
        picker.Plane.Value.ShouldBe(value);
        picker.Value.ShouldBe(Color.Rgb(red, green, blue));
        surface.ShouldHaveCapture(null);
    }

    /// <summary>Verifies a right-click on the plane neither selects nor captures.</summary>
    [Fact]
    public async Task Plane_WhenRightClicked_DoesNotSelectOrCaptureAsync()
    {
        var picker = NewPicker();
        var changes = 0;
        picker.ValueChanged += (_, _) => changes++;
        await using var surface = await MountAsync(picker, new Size(40, 18));

        await surface.Pointer.RightClickAsync(picker.Plane, new Point(0, 0));

        changes.ShouldBe(0);
        picker.Value.ShouldBe(Color.Rgb(255, 0, 0));
        surface.ShouldHaveCapture(null);
    }

    /// <summary>Verifies the hue slider accepts a pointer press and commits the hue at that
    /// column, keeping the plane, sliders, and readout synchronized and focus on the slider.</summary>
    [Fact]
    public async Task HueSlider_WhenClicked_CommitsHueAtThatColumnAsync()
    {
        var picker = NewPicker();
        var changes = 0;
        picker.ValueChanged += (_, _) => changes++;
        await using var surface = await MountAsync(picker, new Size(40, 18));
        var bounds = picker.HueSlider.Bounds;

        await surface.Pointer.ClickAsync(picker.HueSlider, new Point(bounds.Width - 1, 0));

        picker.HueSlider.Value.ShouldBe(359);
        picker.Plane.Hue.ShouldBe(359);
        picker.Value.ShouldBe(Color.Rgb(255, 0, 4));
        picker.BlueSlider.Value.ShouldBe(4);
        picker.HexText.ShouldBe("#FF0004");
        changes.ShouldBe(1);
        surface.ShouldHaveFocus(picker.HueSlider);
        surface.ShouldHaveCapture(null);

        await surface.Pointer.ClickAsync(picker.HueSlider, new Point(0, 0));

        picker.HueSlider.Value.ShouldBe(0);
        picker.Value.ShouldBe(Color.Rgb(255, 0, 0));
        changes.ShouldBe(2);
    }

    #endregion

    #region Round-trips and boundaries

    /// <summary>Verifies boundary colors round-trip through the HSV parts: greys have zero
    /// saturation, black has zero value, and hues past 300 degrees (negative in the raw formula)
    /// wrap into the 0-359 range.</summary>
    [Theory]
    [InlineData(255, 0, 0, 0, 1.0, 1.0)]
    [InlineData(0, 255, 0, 120, 1.0, 1.0)]
    [InlineData(0, 0, 255, 240, 1.0, 1.0)]
    [InlineData(255, 0, 128, 330, 1.0, 1.0)]
    [InlineData(255, 0, 255, 300, 1.0, 1.0)]
    [InlineData(128, 128, 128, 0, 0.0, 0.50196)]
    [InlineData(255, 255, 255, 0, 0.0, 1.0)]
    [InlineData(0, 0, 0, 0, 0.0, 0.0)]
    [InlineData(1, 0, 0, 0, 1.0, 0.00392)]
    public void Value_WhenAssignedBoundaryColor_ProjectsToDocumentedHsvParts(
        int red,
        int green,
        int blue,
        int hue,
        double saturation,
        double value)
    {
        var picker = new ColorPicker();
        var changes = 0;
        picker.ValueChanged += (_, _) => changes++;

        picker.Value = Color.Rgb(red, green, blue);

        changes.ShouldBe(Color.Rgb(red, green, blue) == Color.Rgb(255, 0, 0) ? 0 : 1);
        picker.Plane.Hue.ShouldBe(hue);
        picker.Plane.Saturation.ShouldBe(saturation, tolerance: 1e-4);
        picker.Plane.Value.ShouldBe(value, tolerance: 1e-4);
        picker.HueSlider.Value.ShouldBe(hue);
        picker.RedSlider.Value.ShouldBe(red);
        picker.GreenSlider.Value.ShouldBe(green);
        picker.BlueSlider.Value.ShouldBe(blue);
        picker.Preview.Value.ShouldBe(Color.Rgb(red, green, blue));
        picker.HexText.ShouldBe($"#{red:X2}{green:X2}{blue:X2}");
        Color.FromHsv(hue, picker.Plane.Saturation, picker.Plane.Value).ShouldBe(Color.Rgb(red, green, blue));
    }

    /// <summary>Verifies FromHsv covers all six hue sectors and both saturation/value extremes.</summary>
    [Theory]
    [InlineData(0, 1.0, 1.0, 255, 0, 0)]
    [InlineData(59, 1.0, 1.0, 255, 251, 0)]
    [InlineData(60, 1.0, 1.0, 255, 255, 0)]
    [InlineData(120, 1.0, 1.0, 0, 255, 0)]
    [InlineData(180, 1.0, 1.0, 0, 255, 255)]
    [InlineData(240, 1.0, 1.0, 0, 0, 255)]
    [InlineData(300, 1.0, 1.0, 255, 0, 255)]
    [InlineData(359, 1.0, 1.0, 255, 0, 4)]
    [InlineData(200, 0.0, 1.0, 255, 255, 255)]
    [InlineData(200, 1.0, 0.0, 0, 0, 0)]
    [InlineData(200, 0.5, 0.5, 64, 106, 128)]
    public void FromHsv_WhenGivenSectorAndExtremes_ProducesDocumentedRgb(
        int hue,
        double saturation,
        double value,
        int red,
        int green,
        int blue) =>
        Color.FromHsv(hue, saturation, value).ShouldBe(Color.Rgb(red, green, blue));

    /// <summary>Verifies every fully saturated hue survives an RGB round-trip exactly, so hue
    /// slider edits never drift when the picker re-derives HSV from the committed RGB.</summary>
    [Fact]
    public void ToHsv_WhenGivenEverySaturatedHue_RoundTripsExactly()
    {
        for (var hue = 0; hue < 360; hue++)
        {
            Color.FromHsv(hue, 1, 1).ToHsv(out var actualHue, out var saturation, out var value);

            actualHue.ShouldBe(hue, $"hue {hue}");
            saturation.ShouldBe(1, tolerance: 1e-9);
            value.ShouldBe(1, tolerance: 1e-9);
        }
    }

    /// <summary>Verifies Contrast picks black only for light backgrounds, at the exact luminance
    /// threshold, so the readout stays legible on both sides of it.</summary>
    [Theory]
    [InlineData(255, 255, 255, true)]
    [InlineData(0, 0, 0, false)]
    [InlineData(255, 255, 0, true)]
    [InlineData(0, 0, 255, false)]
    [InlineData(128, 128, 128, true)]
    [InlineData(127, 127, 127, false)]
    public void Contrast_WhenGivenBackground_PicksBlackOnlyForLightColors(int red, int green, int blue, bool black) =>
        Color.Rgb(red, green, blue).Contrast().ShouldBe(black ? Color.Rgb(0, 0, 0) : Color.Rgb(255, 255, 255));

    /// <summary>Verifies Color.Default renders the DEFAULT readout, resets the sliders and plane to
    /// black's coordinates, and a later RGB assignment restores a hex readout.</summary>
    [Fact]
    public async Task Value_WhenAssignedDefault_ShowsDefaultReadoutAndBlackCoordinatesAsync()
    {
        var picker = NewPicker();
        await using var surface = await MountAsync(picker, new Size(40, 18));

        await surface.UpdateAsync(() => picker.Value = Color.Default, "assign the terminal default");

        picker.HexText.ShouldBe("DEFAULT");
        picker.RedSlider.Value.ShouldBe(0);
        picker.GreenSlider.Value.ShouldBe(0);
        picker.BlueSlider.Value.ShouldBe(0);
        picker.Plane.Value.ShouldBe(0);
        picker.Preview.Value.ShouldBe(Color.Default);
        var origin = new Point(picker.Preview.Bounds.X, picker.Preview.Bounds.Y);
        surface.Cell(origin).Text.ShouldBe("D");

        await surface.UpdateAsync(() => picker.Value = Color.Rgb(1, 2, 3), "assign an RGB value again");

        picker.HexText.ShouldBe("#010203");
    }

    #endregion

    #region Color depth, availability, and sizing

    /// <summary>Verifies the model keeps exact RGB at every color-capable depth even where the
    /// terminal must quantize: two adjacent colors stay distinct in Value and readout at every depth,
    /// and only TrueColor paints the exact authored RGB into the preview cell.</summary>
    [Theory]
    [InlineData(ColorDepth.TrueColor)]
    [InlineData(ColorDepth.Indexed256)]
    [InlineData(ColorDepth.Basic16)]
    public async Task Value_WhenTerminalDepthVaries_KeepsExactModelAndDistinctReadoutsAsync(ColorDepth depth)
    {
        var picker = NewPicker();
        var options = TerminalOptions.Minimal with
        {
            Capabilities = TerminalCapabilities.Conservative with { ColorDepth = depth }
        };
        await using var surface = await ComponentSurface.MountAsync(
            picker,
            new Size(40, 18),
            options,
            TestContext.Current.CancellationToken);
        picker.EffectiveColorDepth.ShouldBe(depth);
        picker.IsRgbEditorVisible.ShouldBeTrue();

        await surface.UpdateAsync(() => picker.Value = Color.Rgb(95, 135, 175), "assign the first color");
        var firstHex = picker.HexText;
        var firstCell = surface.Cell(new Point(picker.Preview.Bounds.X, picker.Preview.Bounds.Y)).Style.Background;

        await surface.UpdateAsync(() => picker.Value = Color.Rgb(95, 135, 176), "assign an adjacent color");

        picker.Value.ShouldBe(Color.Rgb(95, 135, 176));
        firstHex.ShouldBe("#5F87AF");
        picker.HexText.ShouldBe("#5F87B0");
        var secondCell = surface.Cell(new Point(picker.Preview.Bounds.X, picker.Preview.Bounds.Y)).Style.Background;

        picker.Plane.Hue.ShouldBe(210);

        if (depth == ColorDepth.TrueColor)
        {
            firstCell.ShouldBe(Color.Rgb(95, 135, 175));
            secondCell.ShouldBe(Color.Rgb(95, 135, 176));
        }
        else
        {
            // A quantizing depth may project both authored colors onto one terminal color (and
            // may therefore skip repainting the cell), so the cells can legitimately agree while
            // the model and readout above stay exact and distinct.
            firstCell.ShouldNotBe(Color.Default);
            secondCell.ShouldNotBe(Color.Default);
        }
    }

    /// <summary>Verifies the monochrome fallback presents the disabled default-only surface,
    /// keeps the authored RGB, takes no focus stop, and upgrades back to the RGB editor with the
    /// value intact.</summary>
    [Fact]
    public async Task Depth_WhenMonochrome_PresentsDisabledFallbackAndPreservesValueAsync()
    {
        var picker = NewPicker();
        picker.Value = Color.Rgb(10, 20, 30);
        var changes = 0;
        picker.ValueChanged += (_, _) => changes++;
        var options = TerminalOptions.Minimal with
        {
            Capabilities = TerminalCapabilities.Conservative with { ColorDepth = ColorDepth.Monochrome }
        };
        await using var surface = await ComponentSurface.MountAsync(
            picker,
            new Size(48, 18),
            options,
            TestContext.Current.CancellationToken);

        picker.IsRgbEditorVisible.ShouldBeFalse();
        picker.Value.ShouldBe(Color.Rgb(10, 20, 30));
        SurfaceText(surface, 48, 18).ShouldContain("Monochrome terminal");
        SurfaceText(surface, 48, 18).ShouldNotContain("#0A141E");

        await surface.Keyboard.PressAsync(Code.Tab);

        picker.Plane.IsFocused.ShouldBeFalse();
        picker.HueSlider.IsFocused.ShouldBeFalse();

        await surface.UpdateAsync(
            () => picker.SetCapabilities(TerminalCapabilities.Conservative with { ColorDepth = ColorDepth.TrueColor }),
            "upgrade to true color");

        picker.IsRgbEditorVisible.ShouldBeTrue();
        picker.Value.ShouldBe(Color.Rgb(10, 20, 30));
        picker.HexText.ShouldBe("#0A141E");
        SurfaceText(surface, 48, 18).ShouldNotContain("Monochrome terminal");
        changes.ShouldBe(0);
    }

    /// <summary>Verifies a disabled picker takes no focus stop and ignores plane keys and clicks,
    /// while a programmatic Value assignment still commits and repaints.</summary>
    [Fact]
    public async Task Disabled_WhenInputArrives_IgnoresItButAcceptsProgrammaticValueAsync()
    {
        var picker = NewPicker();
        picker.IsEnabled = false;
        var changes = 0;
        picker.ValueChanged += (_, _) => changes++;
        await using var surface = await MountAsync(picker, new Size(40, 18));

        await surface.Keyboard.PressAsync(Code.Tab);
        picker.Plane.IsFocused.ShouldBeFalse();
        await surface.Keyboard.PressAsync(Code.Left);
        await surface.Pointer.ClickAsync(picker.Plane, new Point(0, 0));
        await surface.Pointer.ClickAsync(picker.HueSlider, new Point(picker.HueSlider.Bounds.Width - 1, 0));

        changes.ShouldBe(0);
        picker.Value.ShouldBe(Color.Rgb(255, 0, 0));
        surface.ShouldHaveCapture(null);
        surface.ShouldHaveState(picker, VisualState.Disabled);

        await surface.UpdateAsync(() => picker.Value = Color.Rgb(0, 0, 255), "assign while disabled");

        changes.ShouldBe(1);
        picker.HexText.ShouldBe("#0000FF");
        surface.Cell(new Point(picker.Preview.Bounds.X, picker.Preview.Bounds.Y)).Style.Background
            .ShouldBe(Color.Rgb(0, 0, 255));
    }

    /// <summary>Verifies tiny and one-cell surfaces mount, render, and accept a value without
    /// throwing or drawing outside their bounds.</summary>
    [Theory]
    [InlineData(1, 1)]
    [InlineData(6, 3)]
    [InlineData(12, 8)]
    public async Task Render_WhenSurfaceIsTiny_StaysContainedAsync(int width, int height)
    {
        var picker = NewPicker();
        await using var surface = await MountAsync(picker, new Size(width, height));

        await surface.UpdateAsync(() => picker.Value = Color.Rgb(0, 255, 0), "assign on a tiny surface");

        picker.Value.ShouldBe(Color.Rgb(0, 255, 0));
        picker.Bounds.Width.ShouldBeLessThanOrEqualTo(width);
        picker.Bounds.Height.ShouldBeLessThanOrEqualTo(height);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                _ = surface.Cell(new Point(x, y));
            }
        }
    }

    /// <summary>Verifies resizing while the plane owns focus and a value keeps the marker on the
    /// selected coordinate of the new geometry.</summary>
    [Fact]
    public async Task Resize_WhenPlaneIsFocused_KeepsMarkerOnSelectionAsync()
    {
        var picker = NewPicker();
        picker.Value = Color.FromHsv(0, 0.5, 0.5);
        await using var surface = await MountAsync(picker, new Size(40, 18));
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(picker.Plane);

        await surface.ResizeAsync(new Size(60, 26));

        surface.ShouldHaveFocus(picker.Plane);
        var plane = picker.Plane.Bounds;
        var markerX = (int) Math.Round(picker.Plane.Saturation * (plane.Width - 1), MidpointRounding.AwayFromZero);
        var markerY = (int) Math.Round((1 - picker.Plane.Value) * (plane.Height - 1), MidpointRounding.AwayFromZero);
        var cell = surface.Cell(new Point(plane.X + markerX, plane.Y + markerY));
        cell.Text.ShouldBe("◆", "the code-owned selection marker follows the selection");
        picker.Value.ShouldBe(Color.FromHsv(0, 0.5, 0.5));
    }

    #endregion

    private static string SurfaceText(ComponentSurface surface, int width, int height)
    {
        var text = new StringBuilder();

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                _ = text.Append(surface.Cell(new Point(x, y)).Text);
            }

            _ = text.Append('\n');
        }

        return text.ToString();
    }

    private static ColorPicker NewPicker() => new()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch
    };

    private static Task<ComponentSurface> MountAsync(ControlBase control, Size size) =>
        ComponentSurface.MountAsync(
            control,
            size,
            TerminalOptions.Minimal with
            {
                Capabilities = TerminalCapabilities.Conservative with { ColorDepth = ColorDepth.TrueColor }
            },
            TestContext.Current.CancellationToken);

    private static int Channel(Color color, string channel) => channel switch
    {
        "red" => color.Red,
        "green" => color.Green,
        _ => color.Blue
    };

    private static int[] OtherChannels(Color color, string channel) => channel switch
    {
        "red" => [color.Green, color.Blue],
        "green" => [color.Red, color.Blue],
        _ => [color.Red, color.Green]
    };

    private static int HueOf(Color color)
    {
        color.ToHsv(out var hue, out _, out _);
        return hue;
    }
}
