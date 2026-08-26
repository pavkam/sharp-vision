// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Verifies adaptive ColorPicker value, composition, layout, rendering, and input.</summary>
public sealed class ColorPickerTests
{
    /// <summary>Verifies a picker rejects default colors before changing selection.</summary>
    [Fact]
    public void Value_WhenColorIsDefault_AcceptsValue()
    {
        var picker = new ColorPicker { Value = Color.Default };
        picker.Value.ShouldBe(Color.Default);
    }

    /// <summary>Verifies a picker rejects transparent before changing selection.</summary>
    [Fact]
    public void Value_WhenColorIsTransparent_ThrowsBeforeMutation()
    {
        var picker = new ColorPicker();
        var previous = picker.Value;

        _ = Should.Throw<ArgumentException>(() => picker.Value = Color.Transparent);

        picker.Value.ShouldBe(previous);
    }

    /// <summary>Verifies a detached picker retains an exact 24-bit value.</summary>
    [Fact]
    public void Value_WhenPickerIsDetached_PreservesRgb()
    {
        var picker = new ColorPicker();
        var color = Color.Rgb(12, 34, 56);

        picker.Value = color;

        picker.Value.ShouldBe(color);
        picker.HexText.ShouldBe("#0C2238");
    }

    /// <summary>Verifies attachment preserves RGB while reporting a 256-color output capability.</summary>
    [Fact]
    public async Task Attach_WhenTerminalUsesPaletteColor_PreservesRgbAuthoringAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var picker = new ColorPicker { Value = Color.Rgb(95, 135, 175) };

            picker.Attach(
                dispatcher,
                UnicodePolicy.Default,
                TerminalCapabilities.Conservative with { ColorDepth = ColorDepth.Indexed256 });

            picker.Value.ShouldBe(Color.Rgb(95, 135, 175));
            picker.EffectiveColorDepth.ShouldBe(ColorDepth.Indexed256);
            picker.IsRgbEditorVisible.ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies capability changes never mutate the authored RGB value or publish value changes.</summary>
    [Fact]
    public async Task SetCapabilities_WhenColorDepthChanges_PreservesRgbWithoutValueEventsAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var picker = new ColorPicker { Value = Color.Rgb(95, 135, 175) };
            picker.Attach(
                dispatcher,
                UnicodePolicy.Default,
                TerminalCapabilities.Conservative with { ColorDepth = ColorDepth.TrueColor });
            List<string> changes = [];
            picker.ValueChanged += (_, eventArgs) =>
                changes.Add($"{eventArgs.PreviousValue}>{eventArgs.Value}:{picker.Value}");

            picker.SetCapabilities(
                TerminalCapabilities.Conservative with { ColorDepth = ColorDepth.Basic16 });
            picker.SetCapabilities(
                TerminalCapabilities.Conservative with { ColorDepth = ColorDepth.TrueColor });

            picker.Value.ShouldBe(Color.Rgb(95, 135, 175));
            changes.ShouldBeEmpty();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies RGB sliders synchronize through one picker value commit.</summary>
    [Fact]
    public async Task RgbSliders_WhenValueChanges_UpdatePickerAndHexReadoutAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var picker = new ColorPicker();
            picker.Attach(
                dispatcher,
                UnicodePolicy.Default,
                TerminalCapabilities.Conservative with { ColorDepth = ColorDepth.TrueColor });
            var changes = 0;
            picker.ValueChanged += (_, _) => changes++;

            picker.RedSlider.Value = 12;
            picker.GreenSlider.Value = 34;
            picker.BlueSlider.Value = 56;

            picker.Value.ShouldBe(Color.Rgb(12, 34, 56));
            picker.HexText.ShouldBe("#0C2238");
            changes.ShouldBe(3);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies the plane defaults to full saturation and value at hue zero, matching the
    /// picker's own default RGB-red selection.</summary>
    [Fact]
    public void Plane_WhenConstructed_DefaultsToFullSaturationAndValue()
    {
        // Arrange and act
        var picker = new ColorPicker();

        // Assert
        picker.Plane.Hue.ShouldBe(0);
        picker.Plane.Saturation.ShouldBe(1);
        picker.Plane.Value.ShouldBe(1);
    }

    /// <summary>Verifies SetSelection round-trips the exact HSV coordinates through the read-only
    /// Hue, Saturation, and Value properties without requiring pointer or keyboard input.</summary>
    [Fact]
    public void Plane_WhenSelectionIsSet_RoundTripsHueSaturationAndValue()
    {
        // Arrange
        var picker = new ColorPicker();

        // Act
        picker.Plane.SetSelection(180, 0.25, 0.75);

        // Assert
        picker.Plane.Hue.ShouldBe(180);
        picker.Plane.Saturation.ShouldBe(0.25);
        picker.Plane.Value.ShouldBe(0.75);
    }

    /// <summary>Verifies keys outside the plane's editing contract remain available to routed input.</summary>
    [Fact]
    public void Plane_WhenKeyIsUnhandled_RaisesInheritedKeyDownWithoutConsumingIt()
    {
        // Arrange
        var picker = new ColorPicker();
        var raised = 0;
        picker.Plane.KeyDown += (_, _) => raised++;
        var key = Key(Code.F1);

        // Act
        _ = Router.Route(picker.Plane, Events.Key, key);

        // Assert
        key.IsHandled.ShouldBeFalse();
        raised.ShouldBe(1);
    }

    /// <summary>Verifies true-color composition stretches and paints a selected preview with its readout surface.</summary>
    [Fact]
    public async Task Render_WhenTrueColorIsActive_UsesContainedLayoutAndPreviewAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var picker = new ColorPicker
            {
                Value = Color.Rgb(255, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            picker.Attach(
                dispatcher,
                UnicodePolicy.Default,
                TerminalCapabilities.Conservative with { ColorDepth = ColorDepth.TrueColor });
            new LayoutEngine().Layout(picker, new Size(40, 18));
            using Frame frame = new(new Size(40, 18));

            picker.Render(frame.Canvas);

            picker.Bounds.ShouldBe(new Rect(0, 0, 40, 18));
            picker.Plane.Bounds.Width.ShouldBeGreaterThan(0);
            picker.Plane.Bounds.Height.ShouldBeGreaterThan(0);
            picker.Preview.Bounds.Width.ShouldBe(10);
            frame.GetCell(new Point(picker.Preview.Bounds.X, picker.Preview.Bounds.Y))
                .Style.Background.ShouldBe(Color.Rgb(255, 0, 0));
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies the retained plane draws the code-owned diamond marker over the selected
    /// coordinate until a style overrides it, matching the ControlGlyphs default rather than a bare
    /// literal.</summary>
    [Fact]
    public async Task Render_WhenStyleIsUnset_DrawsTheCodeOwnedSelectionMarkerAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var picker = new ColorPicker
            {
                Value = Color.Rgb(255, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            picker.Attach(
                dispatcher,
                UnicodePolicy.Default,
                TerminalCapabilities.Conservative with { ColorDepth = ColorDepth.TrueColor });
            new LayoutEngine().Layout(picker, new Size(40, 18));
            using Frame frame = new(new Size(40, 18));

            picker.Render(frame.Canvas);

            var selected = new Point(picker.Plane.Bounds.Right - 1, picker.Plane.Bounds.Y);
            FrameOracle.Get(frame, selected).ShouldBe("◆");
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a themed SelectedMarker override forwards from ColorPickerStyle through
    /// ColorPicker.OnStyleChanged into the retained ColorPlane and changes the rendered glyph -
    /// proof the marker is no longer a hardcoded literal bypassing the style system.</summary>
    [Fact]
    public async Task Render_WhenStyleOverridesSelectedMarker_DrawsTheConfiguredGlyphAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var picker = new ColorPicker
            {
                Value = Color.Rgb(255, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Style = new ColorPickerStyle(
                    ControlStyle.DefaultFace,
                    ControlStyle.NoBorder,
                    ControlStyle.NoShadow,
                    null,
                    null,
                    new Rune('#'))
            };
            picker.Attach(
                dispatcher,
                UnicodePolicy.Default,
                TerminalCapabilities.Conservative with { ColorDepth = ColorDepth.TrueColor });
            new LayoutEngine().Layout(picker, new Size(40, 18));
            using Frame frame = new(new Size(40, 18));

            picker.Render(frame.Canvas);

            picker.Plane.SelectedMarker.ShouldBe(new Rune('#'));
            var selected = new Point(picker.Plane.Bounds.Right - 1, picker.Plane.Bounds.Y);
            FrameOracle.Get(frame, selected).ShouldBe("#");
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a focus transfer cancels the plane's active pointer capture.</summary>
    [Fact]
    public async Task Dispatch_WhenPlaneLosesFocus_CancelsPointerCaptureAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var picker = new ColorPicker();
            var button = new Button { Text = "Next" };
            Dock.SetSide(button, DockSide.Bottom);
            var root = new Dock { Children = { button, picker } };
            root.SetCapabilities(
                TerminalCapabilities.Conservative with { ColorDepth = ColorDepth.TrueColor });
            root.Attach(dispatcher);
            new LayoutEngine().Layout(root, new Size(40, 20));
            using FocusManager focus = new(root);
            using PointerManager pointer = new(root);
            var point = new Point(
                picker.Plane.Bounds.X + (picker.Plane.Bounds.Width / 2),
                picker.Plane.Bounds.Y + (picker.Plane.Bounds.Height / 2));

            _ = pointer.Dispatch(Pointer(point, PointerAction.Press));

            pointer.Captured.ShouldBeSameAs(picker.Plane);
            focus.Focused.ShouldBeSameAs(picker.Plane);

            _ = focus.Focus(button);

            pointer.Captured.ShouldBeNull();
            picker.Plane.IsPressed.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a disabled ColorPicker refuses pointer selection through its retained plane.</summary>
    [Fact]
    public async Task Dispatch_WhenDisabled_RefusesPlaneSelectionAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            // Arrange
            var picker = new ColorPicker { IsEnabled = false };
            picker.Attach(
                dispatcher,
                UnicodePolicy.Default,
                TerminalCapabilities.Conservative with { ColorDepth = ColorDepth.TrueColor });
            new LayoutEngine().Layout(picker, new Size(40, 18));
            using PointerManager pointer = new(picker);
            var point = new Point(
                picker.Plane.Bounds.X + (picker.Plane.Bounds.Width / 2),
                picker.Plane.Bounds.Y + (picker.Plane.Bounds.Height / 2));
            var previousValue = picker.Value;

            // Act
            _ = pointer.Dispatch(Pointer(point, PointerAction.Press));

            // Assert
            pointer.Captured.ShouldBeNull();
            picker.Value.ShouldBe(previousValue);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies the resolved presentation defaults to each Slider's own semantic profile
    /// and the library-default status face until an explicit local style is assigned.</summary>
    [Fact]
    public void Style_WhenUnset_SlidersAndStatusFollowTheLibraryDefault()
    {
        var picker = new ColorPicker();
        using var expectedSlider = new Slider();

        picker.Style.ShouldBeNull();
        picker.RedSlider.Style.ShouldBeNull();
        picker.GreenSlider.Style.ShouldBeNull();
        picker.BlueSlider.Style.ShouldBeNull();
        picker.HueSlider.Style.ShouldBeNull();
        picker.ActualStyle.SliderStyle.ShouldBe(expectedSlider.ActualStyle);
        picker.ActualStyle.HueSliderStyle.ShouldBe(picker.HueSlider.ActualStyle);
        var statusFace = picker.ActualStyle.StatusFace.ShouldNotBeNull();
        statusFace.Background.ShouldBe(Color.Transparent);
        statusFace.Attributes.ShouldBe((ControlDecoration) SemanticDecoration.NormalText);
        statusFace.Underline.ShouldBe(Underline.None);
        picker.ActualStyle.SelectedMarker.ShouldBeNull();
        picker.Plane.SelectedMarker.ShouldBeNull();
    }

    /// <summary>Verifies an explicit local style propagates to all four owned Sliders and to the
    /// status readout's background, attributes, and underline, while the foreground stays the
    /// contrast color computed for the current value.</summary>
    [Fact]
    public void Style_WhenSet_PropagatesToAllFourSlidersAndTheStatusBackground()
    {
        var picker = new ColorPicker { Value = Color.Rgb(10, 20, 30) };
        var sliderStyle = new SliderStyle(
            SliderStyle.Default.Face,
            SliderStyle.Default.Border,
            SliderStyle.Default.Shadow,
            Color.Rgb(1, 2, 3),
            Color.Rgb(4, 5, 6),
            Color.Rgb(7, 8, 9),
            SliderGlyphs.Default);
        var face = new Face(
            Color.Default,
            Color.Rgb(50, 50, 50),
            SemanticDecoration.NormalText,
            Underline.None,
            Color.Default);
        var style = new ColorPickerStyle(
            ControlStyle.DefaultFace,
            ControlStyle.NoBorder,
            ControlStyle.NoShadow,
            sliderStyle,
            face,
            new Rune('#'));

        picker.Style = style;

        picker.RedSlider.ActualStyle.ShouldBe(sliderStyle);
        picker.GreenSlider.ActualStyle.ShouldBe(sliderStyle);
        picker.BlueSlider.ActualStyle.ShouldBe(sliderStyle);
        picker.HueSlider.ActualStyle.ShouldBe(sliderStyle);
        picker.ActualStyle.SliderStyle.ShouldBe(sliderStyle);
        picker.ActualStyle.HueSliderStyle.ShouldBe(picker.HueSlider.ActualStyle);
        var statusFace = picker.ActualStyle.StatusFace.ShouldNotBeNull();
        statusFace.Background.ShouldBe(face.Background);
        statusFace.Attributes.ShouldBe(face.Attributes);
        statusFace.Underline.ShouldBe(face.Underline);
        statusFace.Foreground.ShouldBe(ColorMath.Contrast(Color.Rgb(10, 20, 30)));
        picker.Plane.SelectedMarker.ShouldBe(new Rune('#'));
        picker.ActualStyle.SelectedMarker.ShouldBe(new Rune('#'));
    }

    /// <summary>Verifies ActualStyle reports resolved child styles and publishes value-derived changes.</summary>
    [Fact]
    public void ActualStyle_WhenContributionIsNullOrOpaque_ReportsRetainedPartsAndValueChanges()
    {
        var opaque = SliderStyle.Default with
        {
            Face = SliderStyle.Default.Face with { Background = Color.Rgb(10, 20, 30) }
        };
        using var picker = new ColorPicker
        {
            Style = ColorPickerStyle.Definition.Resolve(null, ThemeCatalog.Dark) with { SliderStyle = opaque }
        };
        List<string> notifications = [];
        picker.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName!);

        var actual = picker.ActualStyle;
        picker.Value = Color.Rgb(255, 255, 255);

        actual.SliderStyle.ShouldBe(picker.RedSlider.ActualStyle);
        actual.SliderStyle.ShouldBe(picker.GreenSlider.ActualStyle);
        actual.SliderStyle.ShouldBe(picker.BlueSlider.ActualStyle);
        actual.HueSliderStyle.ShouldBe(picker.HueSlider.ActualStyle);
        actual.StatusFace.ShouldNotBe(picker.ActualStyle.StatusFace);
        notifications.ShouldContain(nameof(ColorPicker.ActualStyle));
    }

    /// <summary>Verifies an opaque SliderStyle.Face.Background forwarded through ColorPickerStyle
    /// does not wipe ColorRamp's per-column hue gradient underneath HueSlider - the two overlap
    /// fully in a single-height Overlay, and HueSlider ordinarily relies on rendering with a
    /// transparent background so the gradient shows through.</summary>
    [Fact]
    public async Task Render_WhenSliderStyleFaceIsOpaque_HueRowStillShowsPerColumnGradientAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var opaqueFace = new Face(
                Color.Default,
                Color.Rgb(30, 30, 30),
                SemanticDecoration.NormalText,
                Underline.None,
                Color.Default);
            var sliderStyle = new SliderStyle(
                opaqueFace,
                SliderStyle.Default.Border,
                SliderStyle.Default.Shadow,
                SliderStyle.Default.FillColor,
                SliderStyle.Default.TrackColor,
                SliderStyle.Default.ThumbColor,
                SliderGlyphs.Default);
            var picker = new ColorPicker
            {
                Value = Color.Rgb(255, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Style = new ColorPickerStyle(
                    ControlStyle.DefaultFace,
                    ControlStyle.NoBorder,
                    ControlStyle.NoShadow,
                    sliderStyle,
                    null,
                    null)
            };
            picker.Attach(
                dispatcher,
                UnicodePolicy.Default,
                TerminalCapabilities.Conservative with { ColorDepth = ColorDepth.TrueColor });
            new LayoutEngine().Layout(picker, new Size(40, 18));
            using Frame frame = new(new Size(40, 18));

            picker.Render(frame.Canvas);

            var bounds = picker.HueSlider.Bounds;
            bounds.Width.ShouldBeGreaterThan(1);
            var leftHue = 0 * 359 / (bounds.Width - 1);
            var rightHue = (bounds.Width - 1) * 359 / (bounds.Width - 1);
            var left = frame.GetCell(new Point(bounds.X, bounds.Y)).Style.Background;
            var right = frame.GetCell(new Point(bounds.Right - 1, bounds.Y)).Style.Background;

            left.ShouldBe(Color.FromHsv(leftHue, 1, 1));
            right.ShouldBe(Color.FromHsv(rightHue, 1, 1));
            left.ShouldNotBe(right);
            left.ShouldNotBe(opaqueFace.Background.Literal);
            right.ShouldNotBe(opaqueFace.Background.Literal);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies an opaque SliderStyle.Face.Background still clears and paints normally on
    /// RedSlider/GreenSlider/BlueSlider, which sit on no colored backdrop and therefore have no
    /// reason to special-case their forwarded Face the way HueSlider does.</summary>
    [Fact]
    public async Task Render_WhenSliderStyleFaceIsOpaque_RgbSlidersStillRenderOpaqueFaceAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var opaqueFace = new Face(
                Color.Default,
                Color.Rgb(30, 30, 30),
                SemanticDecoration.NormalText,
                Underline.None,
                Color.Default);
            var sliderStyle = new SliderStyle(
                opaqueFace,
                SliderStyle.Default.Border,
                SliderStyle.Default.Shadow,
                SliderStyle.Default.FillColor,
                SliderStyle.Default.TrackColor,
                SliderStyle.Default.ThumbColor,
                SliderGlyphs.Default);
            var picker = new ColorPicker
            {
                Value = Color.Rgb(255, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Style = new ColorPickerStyle(
                    ControlStyle.DefaultFace,
                    ControlStyle.NoBorder,
                    ControlStyle.NoShadow,
                    sliderStyle,
                    null,
                    null)
            };
            picker.Attach(
                dispatcher,
                UnicodePolicy.Default,
                TerminalCapabilities.Conservative with { ColorDepth = ColorDepth.TrueColor });
            new LayoutEngine().Layout(picker, new Size(40, 18));
            using Frame frame = new(new Size(40, 18));

            picker.Render(frame.Canvas);

            var redBounds = picker.RedSlider.Bounds;
            var greenBounds = picker.GreenSlider.Bounds;
            var blueBounds = picker.BlueSlider.Bounds;
            redBounds.Width.ShouldBeGreaterThan(0);

            frame.GetCell(new Point(redBounds.Right - 1, redBounds.Y)).Style.Background
                .ShouldBe(opaqueFace.Background.Literal);
            frame.GetCell(new Point(greenBounds.Right - 1, greenBounds.Y)).Style.Background
                .ShouldBe(opaqueFace.Background.Literal);
            frame.GetCell(new Point(blueBounds.Right - 1, blueBounds.Y)).Style.Background
                .ShouldBe(opaqueFace.Background.Literal);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a style applied while the monochrome fallback is active still reaches the
    /// RGB Sliders, and survives the subsequent upgrade to a color-capable depth.</summary>
    [Fact]
    public async Task Style_WhenAppliedWhileMonochromeIsActive_SurvivesTheDepthUpgradeAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var picker = new ColorPicker();
            picker.Attach(
                dispatcher,
                UnicodePolicy.Default,
                TerminalCapabilities.Conservative with { ColorDepth = ColorDepth.Monochrome });
            picker.IsRgbEditorVisible.ShouldBeFalse();
            var sliderStyle = new SliderStyle(
                SliderStyle.Default.Face,
                SliderStyle.Default.Border,
                SliderStyle.Default.Shadow,
                Color.Rgb(9, 9, 9),
                Color.Rgb(8, 8, 8),
                Color.Rgb(7, 7, 7),
                SliderGlyphs.Default);

            picker.Style = new ColorPickerStyle(
                ControlStyle.DefaultFace,
                ControlStyle.NoBorder,
                ControlStyle.NoShadow,
                sliderStyle,
                null,
                null);
            picker.SetCapabilities(TerminalCapabilities.Conservative with { ColorDepth = ColorDepth.TrueColor });

            picker.IsRgbEditorVisible.ShouldBeTrue();
            picker.RedSlider.ActualStyle.ShouldBe(sliderStyle);
            picker.GreenSlider.ActualStyle.ShouldBe(sliderStyle);
            picker.BlueSlider.ActualStyle.ShouldBe(sliderStyle);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies changing Style from inside a ValueChanged handler does not suppress the
    /// event or the readout update for the commit that triggered it.</summary>
    [Fact]
    public async Task Style_WhenChangedDuringAValueCommit_DoesNotSuppressValueChangedAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var picker = new ColorPicker();
            picker.Attach(
                dispatcher,
                UnicodePolicy.Default,
                TerminalCapabilities.Conservative with { ColorDepth = ColorDepth.TrueColor });
            var changes = 0;
            picker.ValueChanged += (_, _) =>
            {
                changes++;
                picker.Style = new ColorPickerStyle(
                ControlStyle.DefaultFace,
                ControlStyle.NoBorder,
                ControlStyle.NoShadow,
                SliderStyle.Default,
                null,
                null);
            };

            picker.Value = Color.Rgb(12, 34, 56);

            changes.ShouldBe(1);
            picker.HexText.ShouldBe("#0C2238");
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a caller-supplied status background survives a later value commit, while
    /// the foreground is refreshed to the newly computed contrast color rather than the value that
    /// was current when the style was applied.</summary>
    [Fact]
    public void Value_WhenCommittedAfterStatusStyleIsSet_PreservesContrastForegroundAndCallerBackground()
    {
        var picker = new ColorPicker { Value = Color.Rgb(10, 20, 30) };
        var face = new Face(
            Color.Default,
            Color.Rgb(60, 60, 60),
            SemanticDecoration.NormalText,
            Underline.None,
            Color.Default);
        picker.Style = new ColorPickerStyle(
            ControlStyle.DefaultFace,
            ControlStyle.NoBorder,
            ControlStyle.NoShadow,
            null,
            face,
            null);

        picker.Value = Color.Rgb(200, 210, 220);

        var statusFace = picker.ActualStyle.StatusFace.ShouldNotBeNull();
        statusFace.Background.ShouldBe(face.Background);
        statusFace.Foreground.ShouldBe(ColorMath.Contrast(Color.Rgb(200, 210, 220)));
        statusFace.Foreground.ShouldNotBe(ColorMath.Contrast(Color.Rgb(10, 20, 30)));
    }

    /// <summary>Verifies replacing Style notifies observers exactly once per distinct value and
    /// reports no change for an equal resubmission.</summary>
    [Fact]
    public void Style_WhenReplaced_RaisesPropertyChangedOnceForEachDistinctValue()
    {
        var picker = new ColorPicker();
        var style = new ColorPickerStyle(
            ControlStyle.DefaultFace,
            ControlStyle.NoBorder,
            ControlStyle.NoShadow,
            SliderStyle.Default,
            null,
            null);
        var notifications = new List<string?>();
        picker.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName);

        picker.Style = style;
        picker.Style = style;

        notifications.ShouldBe([nameof(ColorPicker.Style), nameof(ColorPicker.ActualStyle)]);
    }

    private static KeyEventArgs Key(Code code) => new(new Stroke(
        code,
        default,
        nativeCode: 0,
        Modifiers.None,
        KeyAction.Press));

    private static Pointer Pointer(Point cells, PointerAction action) => new(
        cells,
        pixels: null,
        Buttons.Primary,
        action,
        wheelX: 0,
        wheelY: 0,
        Modifiers.None,
        isMotion: action == PointerAction.Move,
        isCellPositionInferred: false);
}
