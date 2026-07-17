// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Tests;

using SharpVision.Input;
using SharpVision.Terminal.Input;

using InputAction = Terminal.Input.Action;

/// <summary>Verifies the interactive command and choice showcase recipes.</summary>
public sealed class InputPaneTests
{
    /// <summary>Verifies the Button page demonstrates the public programmatic activation path.</summary>
    [Fact]
    public void Button_WhenAutosaveExampleRuns_ReportsProgrammaticCauseAndCount()
    {
        // Arrange
        using var page = new ButtonPane();
        new Engine().Layout(page, new Size(100, 80));
        var trigger = FindButton(page, "Simulate autosave");

        // Act
        trigger.PerformClick();
        trigger.PerformClick();

        // Assert
        ControlTree.Text(page).ShouldContain("Autosave log: Programmatic · 2 drafts saved");
    }

    /// <summary>Verifies shadow depth is demonstrated over a populated semantic surface and flat press stays stationary.</summary>
    [Fact]
    public void Button_WhenChromeExamplesBuild_ShowsPatternedParentAndStationaryFlatFace()
    {
        using var page = new ButtonPane();
        new Engine().Layout(page, new Size(100, 90));
        var stage = ControlTree.FindAll<Overlay>(page).Single(value =>
            ControlTree.FindAll<Button>(value).Any(button => button.ShadowMode == ShadowMode.Composite));
        var backdrop = stage.Children[0];
        var flat = FindButton(page, "Flat: color only");
        var before = flat.Bounds;

        _ = Router.Route(flat, Events.Key, Key(Code.Character, new Rune(' ')));

        _ = backdrop.Background.ShouldNotBeNull();
        ControlTree.Text(backdrop).ShouldContain("·");
        flat.IsPressed.ShouldBeTrue();
        flat.Bounds.ShouldBe(before);
    }

    /// <summary>Verifies command execution and availability through the live Button recipe.</summary>
    [Fact]
    public void Button_WhenCommandAvailabilityChanges_ExecutesThenDisablesCommand()
    {
        // Arrange
        using var page = new ButtonPane();
        new Engine().Layout(page, new Size(100, 80));
        var commandButton = FindButton(page, "Deploy command");
        var commandEnabled = FindCheckBox(page, "Command enabled");

        // Act
        commandButton.PerformClick();

        // Assert
        ControlTree.Text(page).ShouldContain("Command log: executed release");

        // Act
        commandEnabled.PerformToggle();

        // Assert
        commandButton.IsEnabled.ShouldBeFalse();
        ControlTree.Text(page).ShouldContain("Command log: unavailable");
    }

    /// <summary>Verifies routed Enter and Escape use the showcased Window default and cancel roles.</summary>
    [Fact]
    public void Button_WhenWindowFallbackKeysRoute_ReportsDefaultAndCancelActions()
    {
        // Arrange
        using var page = new ButtonPane();
        new Engine().Layout(page, new Size(100, 100));
        var focusTarget = ControlTree.FindAll<Dock>(page).Single(value =>
            value.CanFocus && value.Children.Any(static child =>
                child is ControlText { Content: "Focus here, then use Enter or Escape" }));

        // Act
        _ = Router.Route(focusTarget, Events.Key, Key(Code.Enter));

        // Assert
        ControlTree.Text(page).ShouldContain("Window action: Apply (Programmatic)");

        // Act
        _ = Router.Route(focusTarget, Events.Key, Key(Code.Escape));

        // Assert
        ControlTree.Text(page).ShouldContain("Window action: Cancel (Programmatic)");
    }

    /// <summary>Verifies the CheckBox page includes caller-defined validated state marks.</summary>
    [Fact]
    public void CheckBox_WhenPageBuilds_ContainsCustomMarks()
    {
        // Arrange
        using var page = new CheckBoxPane();
        new Engine().Layout(page, new Size(100, 80));

        // Act
        var checkBox = ControlTree.FindAll<CheckBox>(page).Single(value =>
            value.Content is ControlText { Content: "Custom marks" });

        // Assert
        checkBox.Marks.ShouldBe(new Marks(new Rune('·'), new Rune('✓'), new Rune('~')));
    }

    /// <summary>Verifies the CheckBox page demonstrates public programmatic toggling and event order.</summary>
    [Fact]
    public void CheckBox_WhenProgrammaticToggleRuns_ReportsCauseAndEventOrder()
    {
        // Arrange
        using var page = new CheckBoxPane();
        new Engine().Layout(page, new Size(100, 100));
        var trigger = FindButton(page, "Toggle programmatically");
        var eventProbe = FindCheckBox(page, "Observe event order");

        // Act
        trigger.PerformClick();
        eventProbe.PerformToggle();

        // Assert
        ControlTree.Text(page).ShouldContain("Programmatic toggle: True (Programmatic)");
        ControlTree.Text(page).ShouldContain("Events: Checked → StateChanged");
    }

    /// <summary>Verifies the RadioButton page begins one group empty and selects it programmatically.</summary>
    [Fact]
    public void RadioButton_WhenEmptyGroupIsSelected_ReportsCommittedMember()
    {
        // Arrange
        using var page = new RadioButtonPane();
        new Engine().Layout(page, new Size(100, 80));
        var trigger = FindButton(page, "Select first programmatically");

        // Act
        trigger.PerformClick();

        // Assert
        ControlTree.Text(page).ShouldContain("Empty group: First");
    }

    /// <summary>Verifies the RadioButton page exposes traversal members and programmatic regrouping.</summary>
    [Fact]
    public void RadioButton_WhenTraversalAndRegroupingRun_ReportsCommittedGroups()
    {
        // Arrange
        using var page = new RadioButtonPane();
        new Engine().Layout(page, new Size(100, 120));
        var traversal = FindRadioButton(page, "Traversal two");
        var movable = FindRadioButton(page, "Movable option");
        var regroup = FindButton(page, "Move selected option to right group");

        // Act
        traversal.PerformSelect();
        regroup.PerformClick();

        // Assert
        traversal.IsChecked.ShouldBeTrue();
        ControlTree.Text(page).ShouldContain("Traversal: two");
        movable.GroupName.ShouldBe("right");
        ControlTree.Text(page).ShouldContain("Regrouped: Movable option → right");
    }

    private static Button FindButton(Control root, string content) =>
        ControlTree.FindAll<Button>(root).Single(value =>
            value.Content is ControlText text && text.Content == content);

    private static CheckBox FindCheckBox(Control root, string content) =>
        ControlTree.FindAll<CheckBox>(root).Single(value =>
            value.Content is ControlText text && text.Content == content);

    private static RadioButton FindRadioButton(Control root, string content) =>
        ControlTree.FindAll<RadioButton>(root).Single(value =>
            value.Content is ControlText text && text.Content == content);

    private static KeyEventArgs Key(Code code, Rune? character = null) => new(new Stroke(
        code,
        character,
        nativeCode: 0,
        Modifiers.None,
        InputAction.Press));
}
