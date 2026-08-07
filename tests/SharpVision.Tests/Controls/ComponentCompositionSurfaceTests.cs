// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.


namespace SharpVision.Tests.Controls;

/// <summary>Verifies heterogeneous roots and deep retained composition through mounted terminal input.</summary>
public sealed class ComponentCompositionSurfaceTests
{
    /// <summary>Verifies unrelated interactive controls share one root without stealing focus or input state.</summary>
    [Fact]
    public async Task Input_WhenDifferentControlsShareRoot_PreservesFocusOrderAndLocalBehaviorAsync()
    {
        // Arrange
        var button = new Button
        {
            Text = "Run",
            Style = TestButtonStyles.Flat,
            Padding = default,
            Height = Length.Cells(1)
        };
        var checkBox = new CheckBox { Text = "Choice" };
        var input = new TextInput { Text = "AB", Height = Length.Cells(1) };
        var combo = new ComboBox { Items = ["One", "Two"], Height = Length.Cells(1) };
        var root = new Stack
        {
            Children = { button, checkBox, input, combo },
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(14, 4),
            TestContext.Current.CancellationToken);

        // Act and assert forward Tab order
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(button);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(checkBox);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(input);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(combo);

        // Act and assert reverse Tab order
        await surface.Keyboard.PressAsync(Code.Tab, Modifiers.Shift);
        surface.ShouldHaveFocus(input);

        // Act local directional behavior
        await surface.Keyboard.PressAsync(Code.Left);
        input.CaretIndex.ShouldBe(1);
        combo.SelectedIndex.ShouldBe(0);
        checkBox.IsChecked.ShouldBe(false);

        // Act pointer press and release on another component
        await surface.Pointer.MoveToAsync(checkBox);
        await surface.Pointer.PressAsync();
        checkBox.Pressed.ShouldBeTrue();
        input.Focused.ShouldBeFalse();
        surface.ShouldHaveCapture(checkBox);
        await surface.Pointer.ReleaseAsync();

        // Assert local activation and hover transfer
        checkBox.IsChecked.ShouldBe(true);
        checkBox.Pressed.ShouldBeFalse();
        surface.ShouldHaveFocus(checkBox);
        await surface.Pointer.MoveToAsync(button);
        button.PointerOver.ShouldBeTrue();
        checkBox.PointerOver.ShouldBeFalse();
        combo.Opened.ShouldBeFalse();
    }

    /// <summary>Verifies deep ancestors observe ordered routing, hover, focus-within, and cleanup.</summary>
    [Fact]
    public async Task Input_WhenInteractiveLeafIsDeeplyNested_ComposesAncestryAndCleanupAsync()
    {
        // Arrange
        var leaf = new CheckBox { Text = "Deep" };
        var label = leaf.TextControl!;
        var inner = new Stack { Children = { leaf } };
        var expander = new Expander
        {
            HeaderText = "Section",
            Content = inner,
            Expanded = true
        };
        var group = new GroupBox
        {
            HeaderText = "Group",
            Content = expander
        };
        var window = new Window
        {
            Header = "Root",
            Content = group,
            Border = AppearanceTestValues.Border(BorderSide.All, BorderGlyphStyle.Rounded),
            Width = Length.Cells(18),
            Height = Length.Cells(7)
        };
        var root = new Overlay { Children = { window } };
        var route = new List<string>();
        Record(root, "root");
        Record(window, "window");
        Record(group, "group");
        Record(expander, "expander");
        Record(inner, "inner");
        Record(leaf, "leaf");
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(22, 9),
            TestContext.Current.CancellationToken);

        // Act focus traversal through nested focus owners
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(expander);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(leaf);

        // Act complete leaf click
        await surface.Pointer.ClickAsync(leaf);

        // Assert routed press order and semantic result. CheckBox handles the press, which ends
        // ordinary handling and ancestor defaults but never truncates the route: every opted-in
        // ancestor handler still observes the full bubble walk.
        route.ShouldBe([
            "root-Preview",
            "window-Preview",
            "group-Preview",
            "expander-Preview",
            "inner-Preview",
            "leaf-Preview",
            "leaf-Bubble",
            "inner-Bubble",
            "expander-Bubble",
            "group-Bubble",
            "window-Bubble",
            "root-Bubble"
        ]);
        leaf.IsChecked.ShouldBe(true);
        leaf.PointerOver.ShouldBeTrue();
        leaf.PointerDirectlyOver.ShouldBeFalse();
        label.PointerDirectlyOver.ShouldBeTrue();
        inner.PointerOver.ShouldBeTrue();
        expander.PointerOver.ShouldBeTrue();
        group.PointerOver.ShouldBeTrue();
        window.PointerOver.ShouldBeTrue();
        root.PointerOver.ShouldBeTrue();
        (window.GetAppearanceState() & VisualState.FocusWithin).ShouldBe(VisualState.FocusWithin);
        (group.GetAppearanceState() & VisualState.FocusWithin).ShouldBe(VisualState.FocusWithin);

        // Act unavailable ancestor while the leaf is held
        await surface.Pointer.PressAsync();
        leaf.Pressed.ShouldBeTrue();
        await surface.UpdateAsync(() => group.Enabled = false, "disable deep pressed ancestry");

        // Assert transitive cleanup
        leaf.Pressed.ShouldBeFalse();
        leaf.Focused.ShouldBeFalse();
        leaf.EffectiveIsEnabled.ShouldBeFalse();
        surface.ShouldHaveCapture(null);
        surface.ShouldHaveFocus(null);
        return;

        void Record(ControlBase control, string name) =>
            _ = control.AddHandler(
                Events.Pointer,
                (_, eventArgs) =>
                {
                    if (eventArgs.Pointer.Action == PointerAction.Press)
                    {
                        route.Add($"{name}-{eventArgs.Phase}");
                    }
                },
                handledEventsToo: true);
    }
}
