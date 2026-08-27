// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Verifies controls apply the shared keyboard-modifier classifications consistently.</summary>
public sealed class KeyboardModifierPolicyTests
{
    /// <summary>Verifies popup, drop-down, palette, and window dismissal families reject application chords.</summary>
    [Fact]
    public void Dispatch_WhenEscapeCarriesModifiers_DismissesOnlyActivationEligibleSurfaces()
    {
        var eligible = new[]
        {
            Modifiers.None,
            Modifiers.Shift,
            Modifiers.CapsLock | Modifiers.NumLock
        };
        var ineligible = new[]
        {
            Modifiers.Control,
            Modifiers.Alt,
            Modifiers.Super,
            Modifiers.Hyper,
            Modifiers.Meta,
            Modifiers.Control | Modifiers.Shift | Modifiers.CapsLock
        };

        foreach (var modifiers in eligible)
        {
            VerifyFamilies(modifiers, expectedDismissed: true);
        }

        foreach (var modifiers in ineligible)
        {
            VerifyFamilies(modifiers, expectedDismissed: false);
        }
    }

    /// <summary>Verifies every scalar movement key accepts only an unmodified command after lock normalization.</summary>
    [Fact]
    public void Dispatch_WhenScalarMovementHasModifiers_UsesExactCommandPolicy()
    {
        var eligible = new[] { Modifiers.None, Modifiers.CapsLock, Modifiers.NumLock };
        var ineligible = new[]
        {
            Modifiers.Shift,
            Modifiers.Control,
            Modifiers.Alt,
            Modifiers.Super,
            Modifiers.Hyper,
            Modifiers.Meta,
            Modifiers.Control | Modifiers.Shift | Modifiers.CapsLock
        };

        foreach (var modifiers in eligible)
        {
            VerifySlider(modifiers, expectedHandled: true);
            VerifyScrollBar(modifiers, expectedHandled: true);
            VerifyColorPlane(modifiers, expectedHandled: true);
            VerifyCalendar(modifiers, expectedHandled: true);
            VerifyContainer(modifiers, expectedHandled: true);
        }

        foreach (var modifiers in ineligible)
        {
            VerifySlider(modifiers, expectedHandled: false);
            VerifyScrollBar(modifiers, expectedHandled: false);
            VerifyColorPlane(modifiers, expectedHandled: false);
            VerifyCalendar(modifiers, expectedHandled: false);
            VerifyContainer(modifiers, expectedHandled: false);
        }
    }

    private static void VerifyFamilies(Modifiers modifiers, bool expectedDismissed)
    {
        using var popup = new Popup { Content = new Button(), IsOpen = true };
        var popupKey = Route(popup.Content, modifiers);
        popup.IsOpen.ShouldBe(!expectedDismissed, $"Popup with {modifiers}");
        popupKey.IsHandled.ShouldBe(expectedDismissed, $"Popup with {modifiers}");

        using var comboBox = new ComboBox { Items = ["one"], IsOpen = true };
        var comboKey = Route(comboBox, modifiers);
        comboBox.IsOpen.ShouldBe(!expectedDismissed, $"ComboBox with {modifiers}");
        comboKey.IsHandled.ShouldBe(expectedDismissed, $"ComboBox with {modifiers}");

        using var dateInput = new DateInput { IsOpen = true };
        var dateKey = Route(dateInput, modifiers);
        dateInput.IsOpen.ShouldBe(!expectedDismissed, $"DateInput with {modifiers}");
        dateKey.IsHandled.ShouldBe(expectedDismissed, $"DateInput with {modifiers}");

        using var dateTimeInput = new DateTimeInput { IsOpen = true };
        var dateTimeKey = Route(dateTimeInput, modifiers);
        dateTimeInput.IsOpen.ShouldBe(!expectedDismissed, $"DateTimeInput with {modifiers}");
        dateTimeKey.IsHandled.ShouldBe(expectedDismissed, $"DateTimeInput with {modifiers}");

        using var palette = new CommandPalette
        {
            Resolver = static (_, _) => ValueTask.FromResult<IReadOnlyList<object?>>(["command"])
        };
        palette.Text = "query";
        palette.IsOpen.ShouldBeTrue();
        var paletteKey = Route(palette, modifiers);
        palette.IsOpen.ShouldBe(!expectedDismissed, $"CommandPalette with {modifiers}");
        paletteKey.IsHandled.ShouldBe(expectedDismissed, $"CommandPalette with {modifiers}");

        using var window = new Window { CanClose = true, CloseOnEscape = true };
        var closing = 0;
        window.Closing += (_, _) => closing++;
        var windowKey = Route(window, modifiers);
        closing.ShouldBe(expectedDismissed ? 1 : 0, $"Window with {modifiers}");
        windowKey.IsHandled.ShouldBe(expectedDismissed, $"Window with {modifiers}");
    }

    private static KeyEventArgs Route(ControlBase control, Modifiers modifiers)
    {
        var key = new KeyEventArgs(new Stroke(Code.Escape, null, 0, modifiers, KeyAction.Press));
        _ = Router.Route(control, Events.Key, key);
        return key;
    }

    private static void VerifySlider(Modifiers modifiers, bool expectedHandled)
    {
        foreach (var code in new[] { Code.Left, Code.Right, Code.PageUp, Code.PageDown, Code.Home, Code.End })
        {
            var control = new Slider { Minimum = 0, Maximum = 100, Value = 50 };
            var key = Route(control, code, modifiers);

            key.IsHandled.ShouldBe(expectedHandled, $"Slider {code} with {modifiers}");
            (control.Value != 50).ShouldBe(expectedHandled, $"Slider {code} with {modifiers}");
        }
    }

    private static void VerifyScrollBar(Modifiers modifiers, bool expectedHandled)
    {
        foreach (var code in new[] { Code.Left, Code.Right, Code.PageUp, Code.PageDown, Code.Home, Code.End })
        {
            var control = new ScrollBar
            {
                Orientation = Orientation.Horizontal,
                Minimum = 0,
                Maximum = 100,
                Value = 50
            };
            var key = Route(control, code, modifiers);

            key.IsHandled.ShouldBe(expectedHandled, $"ScrollBar {code} with {modifiers}");
            (control.Value != 50).ShouldBe(expectedHandled, $"ScrollBar {code} with {modifiers}");
        }
    }

    private static void VerifyColorPlane(Modifiers modifiers, bool expectedHandled)
    {
        foreach (var code in new[] { Code.Left, Code.Right, Code.Up, Code.Down, Code.Home, Code.End })
        {
            var control = new ColorPlane();
            control.SetSelection(0, 0.5, 0.5);
            var key = Route(control, code, modifiers);

            key.IsHandled.ShouldBe(expectedHandled, $"ColorPlane {code} with {modifiers}");
            (control.Saturation != 0.5 || control.Value != 0.5)
                .ShouldBe(expectedHandled, $"ColorPlane {code} with {modifiers}");
        }
    }

    private static void VerifyCalendar(Modifiers modifiers, bool expectedHandled)
    {
        foreach (var code in new[]
                 {
                     Code.Left, Code.Right, Code.Up, Code.Down,
                     Code.Home, Code.End, Code.PageUp, Code.PageDown
                 })
        {
            var control = new UiCalendar();
            _ = control.Select(new DateOnly(2025, 6, 18));
            var previous = control.ActiveDate;
            var key = Route(control, code, modifiers);

            key.IsHandled.ShouldBe(expectedHandled, $"Calendar {code} with {modifiers}");
            (control.ActiveDate != previous).ShouldBe(expectedHandled, $"Calendar {code} with {modifiers}");
        }
    }

    private static KeyEventArgs Route(ControlBase control, Code code, Modifiers modifiers)
    {
        var key = new KeyEventArgs(new Stroke(code, null, 0, modifiers, KeyAction.Press));
        _ = Router.Route(control, Events.Key, key);
        return key;
    }

    private static void VerifyContainer(Modifiers modifiers, bool expectedHandled)
    {
        foreach (var code in new[]
                 {
                     Code.Left, Code.Right, Code.Up, Code.Down,
                     Code.Home, Code.End, Code.PageUp, Code.PageDown
                 })
        {
            var control = new LayoutProbe { AutoScroll = true, ScrollBars = ScrollBars.Both, LineSize = 2 };
            control.Children.Add(new ProbeControl(new Size(40, 40)));
            new LayoutEngine().Layout(control, new Size(10, 10));
            control.HorizontalOffset = 5;
            control.VerticalOffset = 5;

            var key = Route(control, code, modifiers);

            key.IsHandled.ShouldBe(expectedHandled, $"Container {code} with {modifiers}");
            (control.HorizontalOffset != 5 || control.VerticalOffset != 5)
                .ShouldBe(expectedHandled, $"Container {code} with {modifiers}");
        }
    }
}
