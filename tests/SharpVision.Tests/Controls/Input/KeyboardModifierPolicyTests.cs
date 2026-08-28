// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Verifies controls apply the shared keyboard-modifier classifications consistently.</summary>
public sealed class KeyboardModifierPolicyTests
{
    /// <summary>Gets scalar-navigation modifier cases after incidental lock normalization.</summary>
    public static TheoryData<Modifiers, bool> ScalarNavigationModifiers => new()
    {
        { Modifiers.None, true },
        { Modifiers.CapsLock, true },
        { Modifiers.NumLock, true },
        { Modifiers.Shift, false },
        { Modifiers.Control, false },
        { Modifiers.Alt, false },
        { Modifiers.Super, false },
        { Modifiers.Hyper, false },
        { Modifiers.Meta, false }
    };

    /// <summary>Gets collection-navigation modifier cases including selection modifiers.</summary>
    public static TheoryData<Modifiers, bool> CollectionNavigationModifiers => new()
    {
        { Modifiers.None, true },
        { Modifiers.CapsLock, true },
        { Modifiers.NumLock, true },
        { Modifiers.Shift, true },
        { Modifiers.Control, true },
        { Modifiers.Control | Modifiers.Shift, true },
        { Modifiers.Alt, false },
        { Modifiers.Super, false },
        { Modifiers.Hyper, false },
        { Modifiers.Meta, false }
    };

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

    /// <summary>Verifies Menu navigation applies the scalar modifier policy.</summary>
    [Theory]
    [MemberData(nameof(ScalarNavigationModifiers))]
    public void Dispatch_WhenMenuMovementHasModifiers_UsesExactCommandPolicy(Modifiers modifiers, bool expectedHandled)
    {
        using var control = new Menu { Orientation = Orientation.Vertical };
        control.Items.Add(new MenuItem { Text = "First" });
        control.Items.Add(new MenuItem { Text = "Second" });
        control.SelectedIndex = 0;

        var key = Route(control, Code.Down, modifiers);

        key.IsHandled.ShouldBe(expectedHandled);
        control.SelectedIndex.ShouldBe(expectedHandled ? 1 : 0);
    }

    /// <summary>Verifies NavigationView navigation applies the scalar modifier policy.</summary>
    [Theory]
    [MemberData(nameof(ScalarNavigationModifiers))]
    public void Dispatch_WhenNavigationViewMovementHasModifiers_UsesExactCommandPolicy(
        Modifiers modifiers,
        bool expectedHandled)
    {
        using var control = new NavigationView();
        var first = new NavigationViewItem { Text = "First" };
        control.Items.Add(first);
        control.Items.Add(new NavigationViewItem { Text = "Second" });

        var key = Route(control, Code.Down, modifiers);

        key.IsHandled.ShouldBe(expectedHandled);
        control.SelectedItem.ShouldBe(expectedHandled ? first : null);
    }

    /// <summary>Verifies ListView navigation applies the scalar modifier policy.</summary>
    [Theory]
    [MemberData(nameof(ScalarNavigationModifiers))]
    public void Dispatch_WhenListViewMovementHasModifiers_UsesExactCommandPolicy(
        Modifiers modifiers,
        bool expectedHandled)
    {
        using var control = new UiListView { Items = ["First", "Second"], SelectedIndex = 0 };

        var key = Route(control, Code.Down, modifiers);

        key.IsHandled.ShouldBe(expectedHandled);
        control.SelectedIndex.ShouldBe(expectedHandled ? 1 : 0);
    }

    /// <summary>Verifies ComboBox popup navigation applies the scalar modifier policy.</summary>
    [Theory]
    [MemberData(nameof(ScalarNavigationModifiers))]
    public void Dispatch_WhenComboBoxMovementHasModifiers_UsesExactCommandPolicy(
        Modifiers modifiers,
        bool expectedHandled)
    {
        using var control = new ComboBox { Items = ["First", "Second"], SelectedIndex = 0, IsOpen = true };
        new LayoutEngine().Layout(control, new Size(16, 6));

        var key = Route(control, Code.Down, modifiers);

        key.IsHandled.ShouldBe(expectedHandled);
        control.GetDropDownList().ActiveIndex.ShouldBe(expectedHandled ? 1 : 0);
        control.SelectedIndex.ShouldBe(0);
    }

    /// <summary>Verifies JsonView navigation applies the scalar modifier policy.</summary>
    [Theory]
    [MemberData(nameof(ScalarNavigationModifiers))]
    public void Dispatch_WhenJsonViewMovementHasModifiers_UsesExactCommandPolicy(
        Modifiers modifiers,
        bool expectedHandled)
    {
        using var control = new JsonView { Json = "[0,1]" };

        var key = Route(control, Code.Down, modifiers);

        key.IsHandled.ShouldBe(expectedHandled);
        control.SelectedPath.ShouldBe(expectedHandled ? "/1" : "/0");
    }

    /// <summary>Verifies non-progressive Table navigation applies the scalar modifier policy.</summary>
    [Theory]
    [MemberData(nameof(ScalarNavigationModifiers))]
    public void Dispatch_WhenTableMovementHasModifiers_UsesExactCommandPolicy(
        Modifiers modifiers,
        bool expectedHandled)
    {
        var first = new TableRow([new ControlText("First")]);
        var second = new TableRow([new ControlText("Second")]);
        using var control = new Table { SelectionMode = TableSelectionMode.Row };
        control.Columns.Add(TableColumn.Auto("Name"));
        control.Rows.Add(first);
        control.Rows.Add(second);
        control.SelectRow(first);

        var key = Route(control, Code.Down, modifiers);

        key.IsHandled.ShouldBe(expectedHandled);
        control.ActiveRow.ShouldBeSameAs(expectedHandled ? second : first);
    }

    /// <summary>Verifies TreeView navigation retains collection modifiers and rejects command modifiers.</summary>
    [Theory]
    [MemberData(nameof(CollectionNavigationModifiers))]
    public void Dispatch_WhenTreeViewMovementHasModifiers_UsesCollectionSelectionPolicy(
        Modifiers modifiers,
        bool expectedHandled)
    {
        var first = new TreeViewItem { Header = "First" };
        var second = new TreeViewItem { Header = "Second" };
        using var control = new TreeView { SelectionMode = TreeSelectionMode.Multiple };
        control.Items.Add(first);
        control.Items.Add(second);
        control.SelectItem(first);

        var key = Route(control, Code.Down, modifiers);

        key.IsHandled.ShouldBe(expectedHandled);
        control.SelectedItems.Contains(second).ShouldBe(expectedHandled);
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
