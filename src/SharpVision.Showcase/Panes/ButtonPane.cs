// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using SharpVision.Text;

using Text = SharpVision.Controls.Text;


/// <summary>Documents the Button control with live, themed activation specimens.</summary>
internal sealed class ButtonPane: View
{
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "Button";

    /// <inheritdoc/>
    protected override Control Build()
    {
        var status = new Text("Activation log: waiting");
        var primary = new Button() { Content = new Text("Click or press Enter") };
        primary.Click += (_, eventArgs) => status.Content = $"Activation log: {eventArgs.Cause}";

        var commandStatus = new Text("Command log: ready with parameter release");
        var commandEnabled = new CheckBox
        {
            Content = new Text("Command enabled"),
            IsChecked = true,
        };
        var command = new ShowcaseCommand(
            parameter => commandStatus.Content = $"Command log: executed {parameter}",
            _ => commandEnabled.IsChecked == true);
        var commandButton = new Button
        {
            Content = new Text("Deploy command"),
            Command = command,
            CommandParameter = "release",
        };
        commandEnabled.StateChanged += (_, eventArgs) =>
        {
            commandButton.IsEnabled = eventArgs.Current == true;
            command.RaiseCanExecuteChanged();
            commandStatus.Content = eventArgs.Current == true
                ? "Command log: enabled"
                : "Command log: unavailable";
        };

        var roleStatus = new Text("Window action: waiting");
        var dialogFocusTarget = new Dock
        {
            CanFocus = true,
            Children = { new Text("Focus here, then use Enter or Escape") },
        };
        var dialogDefault = new Button() { Content = new Text("Apply"), IsDefault = true };
        dialogDefault.Click += (_, eventArgs) => roleStatus.Content = $"Window action: Apply ({eventArgs.Cause})";
        var dialogCancel = new Button() { Content = new Text("Cancel"), IsCancel = true };
        dialogCancel.Click += (_, eventArgs) => roleStatus.Content = $"Window action: Cancel ({eventArgs.Cause})";
        var dialog = new Window
        {
            Width = Length.Cells(34),
            Height = Length.Cells(11),
            Title = "Command roles",
            Child = Doc.Column(
                new Text("Enter chooses Apply; Escape chooses Cancel."),
                dialogFocusTarget,
                Doc.Row(dialogDefault, dialogCancel),
                roleStatus),
        };

        var composite = new Button() { Content = new Text("Composite: parent pattern") };
        var blockShadow = new Button()
        {
            Content = new Text("Block: independent glyph"),
            ShadowMode = ShadowMode.BlockGlyph,
            ShadowGlyph = new Rune('░'),
        };
        var flat = new Button() { Content = new Text("Flat: color only"), HasShadow = false };
        var disabled = new Button() { Content = new Text("Disabled"), IsEnabled = false };
        var shadowBackdrop = new Dock
        {
            Background = ThemeColors.Surface,
            FillMode = FillMode.Opaque,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children =
            {
                new Text(
                    "· · · · · · · · · · · · · · · · · · · · · · · ·\n" +
                    " · · · · · · · · · · · · · · · · · · · · · · · ·\n" +
                    "· · · · · · · · · · · · · · · · · · · · · · · ·\n" +
                    " · · · · · · · · · · · · · · · · · · · · · · · ·\n" +
                    "· · · · · · · · · · · · · · · · · · · · · · · ·")
                {
                    Foreground = ThemeColors.Muted,
                    Overflow = Overflow.Clip,
                },
            },
        };
        var shadowVariants = Doc.Column(
            Doc.Row(composite, blockShadow),
            Doc.Row(flat, disabled));
        shadowVariants.Margin = new Thickness(1);
        var shadowStage = new Overlay
        {
            Width = Length.Cells(56),
            Height = Length.Cells(9),
            Children = { shadowBackdrop, shadowVariants },
        };

        var autosaveStatus = new Text("Autosave log: waiting");
        var savedDrafts = 0;
        var saveDraft = new Button() { Content = new Text("Save draft") };
        saveDraft.Click += (_, eventArgs) =>
        {
            savedDrafts++;
            var noun = savedDrafts == 1 ? "draft" : "drafts";
            autosaveStatus.Content = $"Autosave log: {eventArgs.Cause} · {savedDrafts} {noun} saved";
        };
        var autosave = new Button() { Content = new Text("Simulate autosave") };
        autosave.Click += (_, _) => saveDraft.PerformClick();

        return Doc.Page(
            Title,
            "Activates one semantic action through keyboard, pointer, programmatic, or command paths.",
            Doc.Section(
                "🔘",
                "Start here",
                "Begin with one action and a visible activation result.",
                Doc.Example(
                    "Primary action",
                    "Focus the button and press Enter or Space, or click it. The log identifies the committed input path.",
                    Doc.Column(primary, status),
                    "var save = new Button { Content = new Text(\"Save\") };\nsave.Click += (_, e) => status.Content = e.Cause.ToString();")),
            Doc.Section(
                "🔘",
                "Commands",
                "Use Command and CommandParameter when an action owns reusable availability and execution policy.",
                Doc.Example(
                    "Availability and borrowed parameter",
                    "Toggle command availability, then activate Deploy. The command receives the exact borrowed parameter only while it can execute.",
                    Doc.Column(commandEnabled, commandButton, commandStatus),
                    "button.Command = deployCommand;\nbutton.CommandParameter = \"release\";")),
            Doc.Section(
                "🔘",
                "Window roles",
                "Default and cancel buttons become Enter and Escape fallbacks only inside an owning Window.",
                Doc.Example(
                    "Dialog fallback actions",
                    "Focus the neutral target, then use Enter for Apply or Escape for Cancel. The owning Window invokes the matching role through its public activation path.",
                    dialog)),
            Doc.Section(
                "🔘",
                "Chrome and states",
                "Choose depth and border treatment without changing the command contract.",
                Doc.Example(
                    "Shadow and availability variants",
                    "The patterned Surface makes the distinction honest: composite shadow restyles parent cells, block shadow owns its glyph, and the flat face changes color without translating.",
                    shadowStage)),
            Doc.Section(
                "🔘",
                "Programmatic use",
                "PerformClick shares validation, event ordering, and command execution with user activation.",
                Doc.Example(
                    "Autosave shares the Save action",
                    "Save draft is the user action. Simulate autosave calls the same PerformClick path, reports Programmatic, and increments the visible save count.",
                    Doc.Column(Doc.Row(saveDraft, autosave), autosaveStatus),
                    "autosave.Click += (_, _) => saveDraft.PerformClick();")));
    }
}
