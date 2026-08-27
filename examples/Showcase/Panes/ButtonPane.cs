// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents the Button control with live, themed activation specimens.</summary>
internal sealed class ButtonPane: CompositeControlBase
{
    internal ButtonPane() => InitializeContent(CreateContent());

    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "Button";

    /// <inheritdoc/>
    private static DocPage CreateContent()
    {
        // Primary action with a visible activation log.
        var status = new Text("Activation log: waiting");
        var primary = new Button { Text = "&Click or press Enter" };
        primary.Click += (_, eventArgs) => status.Content = $"Activation log: {eventArgs.Cause}";

        // Command availability and borrowed parameter execution.
        var commandStatus = new Text("Command log: ready with parameter release");
        var commandEnabled = new CheckBox { Text = "Command &enabled", IsChecked = true };
        var command = new ShowcaseCommand(
            parameter => commandStatus.Content = $"Command log: executed {parameter}",
            _ => commandEnabled.IsChecked == true);
        var commandButton = new Button
        {
            Text = "Depl&oy command",
            Command = command,
            CommandParameter = "release"
        };
        commandEnabled.StateChanged += (_, eventArgs) =>
        {
            commandButton.IsEnabled = eventArgs.Current == true;
            command.RaiseCanExecuteChanged();
            commandStatus.Content = eventArgs.Current == true
                ? "Command log: enabled"
                : "Command log: unavailable";
        };

        // IsDefault and IsCancel fallbacks apply only inside an owning Window.
        var roleStatus = new Text("Window action: waiting");
        var dialogFocusTarget = new Dock
        {
            IsFocusable = true,
            Children = { new Text("Focus here, then use Enter or Escape") }
        };
        var dialogDefault = new Button { Text = "&Apply", IsDefault = true };
        dialogDefault.Click += (_, eventArgs) => roleStatus.Content = $"Window action: Apply ({eventArgs.Cause})";
        var dialogCancel = new Button { Text = "Ca&ncel", IsCancel = true };
        dialogCancel.Click += (_, eventArgs) => roleStatus.Content = $"Window action: Cancel ({eventArgs.Cause})";
        var dialogActions = new DocRow(dialogDefault, dialogCancel)
        {
            HorizontalAlignment = HorizontalAlignment.Center
        };
        var dialog = new Window
        {
            UseMnemonic = false,
            Width = Length.Cells(46),
            Height = Length.Cells(13),
            Header = "Command roles",
            Content = new DocColumn(
                new Text("Enter chooses Apply; Escape chooses Cancel.") { Overflow = Overflow.Wrap },
                dialogFocusTarget,
                dialogActions,
                roleStatus)
        };

        // Button owns caption placement without requiring Text-specific setup.
        var startAligned = new Button
        {
            Width = Length.Cells(28),
            Text = "Start (&1)",
            TextAlignment = Alignment.Start
        };
        var centerAligned = new Button
        {
            Width = Length.Cells(28),
            Text = "Center (&2, default)"
        };
        var endAligned = new Button
        {
            Width = Length.Cells(28),
            Text = "End (&3)",
            TextAlignment = Alignment.End
        };

        // Shadow variants share activation semantics but differ in elevation rendering. Captions
        // stay short so two of these fit side by side inside the shrunk shadow stage; the
        // DocExample description below spells out what each mode actually looks like.
        var filled = new Button
        {
            Style = ButtonStyle.Filled,
            Width = Length.Cells(20),
            Text = "F&illed: depth"
        };
        var composite = new Button
        {
            Width = Length.Cells(20),
            Text = "Com&posite: parent",
            Style = WithShadow(new Shadow(
                true,
                ShadowMode.Composite,
                new Point(1, 1),
                default,
                SemanticColor.ControlShadow,
                Color.Transparent,
                SemanticDecoration.Shadow))
        };
        var blockShadow = new Button
        {
            Width = Length.Cells(20),
            Text = "&Block: glyph",
            Style = WithShadow(new Shadow(
                true,
                ShadowMode.BlockGlyph,
                new Point(1, 1),
                new Rune('░'),
                SemanticColor.ControlShadow,
                Color.Transparent,
                SemanticDecoration.Shadow))
        };
        var flat = new Button
        {
            Width = Length.Cells(20),
            Text = "&Flat: color",
            Style = WithShadow(new Shadow(
                false,
                ShadowMode.Composite,
                default,
                default,
                SemanticColor.ControlShadow,
                Color.Transparent,
                SemanticDecoration.Shadow))
        };
        var disabled = new Button
        {
            Text = "&Disabled",
            IsEnabled = false
        };
        var shadowBackdrop = new Dock
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Face = new Face(
                SemanticColor.ControlText,
                Color.Rgb(0x30, 0x30, 0x3A),
                SemanticDecoration.NormalText,
                Underline.None,
                Color.Default)
        };
        var shadowPatternLine = string.Concat(Enumerable.Repeat("   ·", 11));
        var shadowPattern = new Text(string.Join('\n', Enumerable.Repeat(shadowPatternLine, 15)))
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Face = new Face(
                SemanticColor.DisabledText,
                Color.Transparent,
                SemanticDecoration.NormalText,
                Underline.None,
                Color.Default)
        };
        var transparentFace = new Face(
            SemanticColor.ControlText,
            Color.Transparent,
            SemanticDecoration.NormalText,
            Underline.None,
            Color.Default);
        var elevatedRow = new DocRow(filled, composite) { Face = transparentFace };
        var depthRow = new DocRow(blockShadow, flat) { Face = transparentFace };
        var shadowVariants = new DocColumn(
            elevatedRow,
            depthRow,
            disabled)
        {
            Margin = new Thickness(1),
            Face = transparentFace
        };
        var shadowStage = new Overlay
        {
            Width = Length.Cells(46),
            Height = Length.Cells(15),
            Children = { shadowBackdrop, shadowPattern, shadowVariants }
        };
        shadowVariants.HorizontalAlignment = HorizontalAlignment.Center;
        shadowVariants.VerticalAlignment = VerticalAlignment.Center;

        // PerformClick shares validation, event ordering, and command execution with user activation.
        var autosaveStatus = new Text("Autosave log: waiting");
        var savedDrafts = 0;
        var saveDraft = new Button { Text = "&Save draft" };
        saveDraft.Click += (_, eventArgs) =>
        {
            savedDrafts++;
            var noun = savedDrafts == 1 ? "draft" : "drafts";
            autosaveStatus.Content = $"Autosave log: {eventArgs.Cause} · {savedDrafts} {noun} saved";
        };
        var autosave = new Button { Text = "Si&mulate autosave" };
        autosave.Click += (_, _) => saveDraft.PerformClick();

        // StartAffix reserves a fixed cell for application-owned decoration beside the caption.
        var affixStatus = new Text("Affix log: waiting");
        var retryUpload = new Button
        {
            Text = "&Retry upload",
            StartAffix = new Affix("⚠", "!", SemanticColor.Warning)
        };
        retryUpload.Click += (_, eventArgs) => affixStatus.Content = $"Affix log: {eventArgs.Cause}";

        return new DocPage(
            Title,
            "<info>Button</info> activates one <i>semantic action</i> through keyboard, pointer, programmatic, or command paths.",
            new DocSection(
                "🔘",
                "Start here",
                "Begin with one action and a visible activation result.",
                new DocExample(
                    "Primary action",
                    "Hover for a subtle label accent; focus strengthens the frame. Press <reverse>Enter</reverse> or <reverse>Space</reverse>, or click it. The log identifies the <success>committed input path</success>.",
                    new DocColumn(primary, status),
                    "var save = new Button { Text = \"&Save\" };\nsave.Click += (_, e) => status.Content = e.Cause.ToString();")),
            new DocSection(
                "📎",
                "Commands",
                "Use Command and CommandParameter when an action owns reusable availability and execution policy.",
                new DocExample(
                    "Availability and borrowed parameter",
                    "Toggle command availability, then activate Deploy. The command receives the exact borrowed parameter only while it can execute.",
                    new DocColumn(commandEnabled, commandButton, commandStatus),
                    "button.Command = deployCommand;\nbutton.CommandParameter = \"release\";")),
            new DocSection(
                "🪟",
                "Window roles",
                "<info>IsDefault</info> and <info>IsCancel</info> buttons become <reverse>Enter</reverse> and <reverse>Escape</reverse> fallbacks only inside an owning <info>Window</info>.",
                new DocExample(
                    "Dialog fallback actions",
                    "Focus the neutral target, then use <reverse>Enter</reverse> for Apply or <reverse>Escape</reverse> for Cancel. The owning <info>Window</info> invokes the matching role through its public activation path.",
                    dialog)),
            new DocSection(
                "↔️",
                "Caption alignment",
                "Button captions center by default and can move to either physical edge without changing the retained Text child.",
                new DocExample(
                    "Start, center, and end",
                    "All three buttons have the same width; only TextAlignment changes caption placement.",
                    new DocColumn(startAligned, centerAligned, endAligned),
                    "button.TextAlignment = Alignment.End;")),
            new DocSection(
                "◩",
                "Shadow depth",
                "Each button uses a different shadow mode while sharing the same activation and command contract.",
                new DocExample(
                    "Filled, composite, block, flat, and disabled",
                    "Filled uses a borderless opaque face and fractional block depth. Composite preserves and restyles the dotted surface behind it. Block uses one visible glyph. Flat removes depth.",
                    shadowStage,
                    "var add = new Button { Style = ButtonStyle.Filled, Text = \"&Add\" };")),
            new DocSection(
                "⚡",
                "Programmatic use",
                "PerformClick shares validation, event ordering, and command execution with user activation.",
                new DocExample(
                    "Autosave shares the Save action",
                    "Save draft is the user action. Simulate autosave calls the same PerformClick path, reports Programmatic, and increments the visible save count.",
                    new DocColumn(new DocRow(saveDraft, autosave), autosaveStatus),
                    "autosave.Click += (_, _) => saveDraft.PerformClick();")),
            new DocSection(
                "⚠️",
                "Affixes",
                "<info>StartAffix</info> and <info>EndAffix</info> reserve a fixed cell beside the caption for application-owned, data-driven decoration that a theme never authors.",
                new DocExample(
                    "Warning glyph before the caption",
                    "The glyph and its portable fallback are unrelated to any theme glyph; only the color is semantic. Activate the button to see the reported cause.",
                    new DocColumn(retryUpload, affixStatus),
                    "var button = new Button\n{\n    Text = \"&Retry upload\",\n    StartAffix = new Affix(\"⚠\", \"!\", SemanticColor.Warning)\n};")));
    }

    private static ButtonStyle WithShadow(Shadow shadow) => ButtonStyle.Standard with
    {
        Border = new Border(
            BorderSide.None,
            BorderGlyphStyle.Default,
            SemanticColor.ControlBorder,
            Color.Transparent,
            SemanticDecoration.Border),
        Shadow = shadow
    };
}
