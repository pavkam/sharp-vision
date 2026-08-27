// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents the CheckBox control with live, themed toggle specimens.</summary>
internal sealed class CheckBoxPane: CompositeControlBase
{
    internal CheckBoxPane() => InitializeContent(CreateContent());

    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "CheckBox";

    /// <inheritdoc/>
    private static DocPage CreateContent()
    {
        var status = new Text("State log: waiting");
        var live = new CheckBox { Text = "&Toggle with Space, Enter, or a pointer click" };
        live.StateChanged += (_, eventArgs) =>
            status.Content = $"State log: {eventArgs.Current?.ToString() ?? "indeterminate"} ({eventArgs.Cause})";

        // Built-in mark families and one validated custom glyph set.
        var square = new CheckBox
        {
            Text = "&Square marks",
            Style = CheckBoxStyle.Square,
            IsChecked = true
        };
        var brackets = new CheckBox
        {
            Text = "&Bracket marks",
            Style = CheckBoxStyle.Brackets,
            IsChecked = true
        };
        var tick = new CheckBox
        {
            Text = "Tic&k marks",
            Style = CheckBoxStyle.Tick,
            IsChecked = true
        };
        var custom = new CheckBox
        {
            Text = "Custo&m marks",
            Style = new CheckBoxStyle(
                CheckBoxStyle.Default.Face,
                CheckBoxStyle.Default.Border,
                CheckBoxStyle.Default.Shadow,
                CheckBoxMarkStyle.Square,
                new CheckBoxGlyphs(new Rune('·'), new Rune('✓'), new Rune('~'))),
            IsChecked = true
        };

        // Three-state specimens: unchecked, checked, and indeterminate.
        var @unchecked = new CheckBox { Text = "&Unchecked", Style = CheckBoxStyle.Brackets };
        var @checked = new CheckBox
        {
            Text = "&Checked",
            Style = CheckBoxStyle.Brackets,
            IsChecked = true
        };
        var indeterminate = new CheckBox
        {
            Text = "&Indeterminate",
            Style = CheckBoxStyle.Brackets,
            ThreeState = true,
            IsChecked = null
        };
        var stateStage = new Dock
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(1, 0),
            Children = { new DocColumn(@unchecked, @checked, indeterminate) }
        };

        var disabled = new CheckBox
        {
            Text = "&Disabled, checked",
            Style = CheckBoxStyle.Brackets,
            IsChecked = true,
            IsEnabled = false
        };

        // Three-state policy normalizes null to false when returning to two states.
        var policyStatus = new Text("Policy: indeterminate");
        var policy = new CheckBox
        {
            Text = "&Optional inherited value",
            ThreeState = true,
            IsChecked = null
        };
        var twoState = new Button { Text = "&Return to two states" };
        twoState.Click += (_, _) =>
        {
            policy.ThreeState = false;
            policyStatus.Content = $"Policy: {policy.IsChecked}";
        };

        // State-specific notifications precede the general StateChanged notification.
        var eventStatus = new Text("Events: waiting");
        var eventProbe = new CheckBox { Text = "Obser&ve event order" };
        eventProbe.Checked += (_, _) => eventStatus.Content = "Events: Checked";
        eventProbe.Unchecked += (_, _) => eventStatus.Content = "Events: Unchecked";
        eventProbe.Indeterminate += (_, _) => eventStatus.Content = "Events: Indeterminate";
        eventProbe.StateChanged += (_, _) => eventStatus.Content += " → StateChanged";

        // PerformClick reports Programmatic as the activation cause.
        var programmaticStatus = new Text("Programmatic toggle: waiting");
        var programmaticTarget = new CheckBox { Text = "&Programmatic target" };
        programmaticTarget.StateChanged += (_, eventArgs) =>
            programmaticStatus.Content = $"Programmatic toggle: {eventArgs.Current} ({eventArgs.Cause})";
        var programmaticTrigger = new Button { Text = "Toggle pro&grammatically" };
        programmaticTrigger.Click += (_, _) => programmaticTarget.PerformClick();

        // StartAffix reserves a fixed cell for an application-owned priority marker.
        var priority = new CheckBox
        {
            Text = "Esca&late to on-call",
            StartAffix = new Affix("!", "!", SemanticColor.Error)
        };

        // Form recipe: grouped options with one disabled retained value.
        var settings = new DocCard(new DocColumn(
            new Text("Export options")
            {
                Face = new Face(
                SemanticColor.ControlText,
                Color.Transparent,
                TerminalAttributes.Bold,
                Underline.None,
                Color.Default)
            },
            new CheckBox { Text = "Include metadat&a", IsChecked = true },
            new CheckBox { Text = "Compr&ess output" },
            disabled));

        return new DocPage(
            Title,
            "<info>CheckBox</info> toggles an optional label through <i>two-state</i> or <i>three-state</i> selection with explicit events.",
            new DocSection(
                "☑️",
                "Two-state choice",
                "Use the default false/true cycle for independent Boolean options.",
                new DocExample(
                    "Live toggle",
                    "Use <reverse>Space</reverse>, <reverse>Enter</reverse>, or a primary click. The status reports both the <success>committed value</success> and activation cause.",
                    new DocColumn(live, status),
                    "var option = new CheckBox { Text = \"Include metadata\" };")),
            new DocSection(
                "☑️",
                "Three-state policy",
                "Use null only when the option genuinely represents an inherited or mixed value.",
                new DocExample(
                    "Cycle and normalize",
                    "The compact marks sit on a contrasting Surface without painting selection blocks. Return the live policy to two states and null normalizes to false.",
                    new DocColumn(stateStage, policy, twoState, policyStatus),
                    "option.ThreeState = true;\noption.IsChecked = null;")),
            new DocSection(
                "☑️",
                "Styles",
                "Built-in families and validated caller glyphs retain stable label placement.",
                new DocExample(
                    "Built-in and custom glyphs",
                    "Square, bracket, tick, and custom marks all use printable one-cell state glyphs.",
                    new DocColumn(square, brackets, tick, custom))),
            new DocSection(
                "☑️",
                "Events",
                "State-specific notifications precede the general StateChanged notification.",
                new DocExample(
                    "Committed event order",
                    "Toggle the probe and read the exact state-specific → StateChanged sequence.",
                    new DocColumn(eventProbe, eventStatus)),
                new DocExample(
                    "Programmatic activation",
                    "Toggle the target through PerformClick. The same event contract reports Programmatic as the cause.",
                    new DocColumn(programmaticTrigger, programmaticTarget, programmaticStatus),
                    "target.PerformClick();")),
            new DocSection(
                "☑️",
                "Form recipe",
                "Group related options and keep unavailable retained values visible as context.",
                new DocExample(
                    "Export settings",
                    "The disabled checked option remains readable but refuses focus and activation.",
                    settings)),
            new DocSection(
                "📌",
                "Affixes",
                "<info>StartAffix</info> and <info>EndAffix</info> reserve a fixed cell before the mark or after the caption for application-owned, data-driven decoration that a theme never authors.",
                new DocExample(
                    "Priority glyph before the mark",
                    "The exclamation mark sits in its own reserved cell before the check mark and never shares space with it.",
                    priority,
                    "var option = new CheckBox\n{\n    Text = \"Escalate to on-call\",\n    StartAffix = new Affix(\"!\", \"!\", SemanticColor.Error)\n};")));
    }
}
