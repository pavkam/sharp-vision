// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents CommandBarItem interaction, availability, affixes, styling, and activation evidence.</summary>
internal sealed class CommandBarItemPane: CompositeControlBase
{
    /// <summary>Initializes the retained CommandBarItem documentation page.</summary>
    internal CommandBarItemPane() => InitializeContent(CreateContent());

    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "CommandBarItem";

    /// <summary>Creates the retained documentation page and its owner-backed item specimen.</summary>
    /// <returns>The complete page root.</returns>
    private static DocPage CreateContent()
    {
        var itemLog = new Text("CommandBarItem.Invoked: waiting") { Overflow = Overflow.Wrap };
        var ownerLog = new Text("CommandBar.ItemInvoked: waiting") { Overflow = Overflow.Wrap };
        var commandLog = new Text("ICommand: waiting") { Overflow = Overflow.Wrap };
        var availabilityLog = new Text("Deploy availability: enabled") { Overflow = Overflow.Wrap };

        var build = CreateItem("&Build", "Build", itemLog);
        var deploy = CreateItem("界 &Deploy 🚀", "界 Deploy 🚀", itemLog);
        deploy.StartAffix = new Affix("◆", "*", SemanticColor.Accent);
        deploy.EndAffix = new Affix("↑", "^", SemanticColor.Success);
        deploy.Style = CommandBarItemStyle.Default with
        {
            Face = CommandBarItemStyle.Default.Face with
            {
                Foreground = SemanticColor.Accent,
                Attributes = TerminalAttributes.Bold
            }
        };
        deploy.CommandParameter = "preview";
        deploy.Command = new ShowcaseCommand(
            parameter => commandLog.Content = $"ICommand: deployed {parameter}",
            static _ => true);
        var archive = CreateItem("&Archive", "Archive", itemLog);
        archive.IsEnabled = false;

        var bar = new CommandBar
        {
            Width = Length.Cells(42),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        bar.Items.Add(build);
        bar.Items.Add(deploy);
        bar.Items.Add(archive);
        bar.ItemInvoked += (_, eventArgs) =>
            ownerLog.Content = $"CommandBar.ItemInvoked: {Plain(eventArgs.Item)} ({eventArgs.Cause})";

        var invoke = new Button { Text = "Invoke &programmatically" };
        invoke.Click += (_, _) => deploy.PerformInvoke();
        var availability = new Button { Text = "Toggle availabilit&y" };
        availability.Click += (_, _) =>
        {
            deploy.IsEnabled = !deploy.IsEnabled;
            availabilityLog.Content = deploy.IsEnabled
                ? "Deploy availability: enabled"
                : "Deploy availability: disabled · activation is inert";
        };

        return new DocPage(
            Title,
            "<info>CommandBarItem</info> is one retained semantic command face. Its owner supplies roving selection and overflow while the item keeps its caption, affixes, style, availability, event, and captured command identity.",
            new DocSection(
                "⌘",
                "Semantic command item",
                "The styled Unicode Deploy item exposes both affixes and remains the event source whether pointer, keyboard, access-key, or programmatic activation reaches it. Archive shows the unavailable state.",
                new DocExample(
                    "Item activation and availability",
                    "Activate Build or Deploy in the owner, invoke Deploy programmatically, or disable it. The three readouts distinguish the named item's event, the owner's forwarded event, and captured ICommand execution.",
                    new DocColumn(
                        bar,
                        new DocRow(invoke, availability),
                        availabilityLog,
                        itemLog,
                        ownerLog,
                        commandLog),
                    "var item = new CommandBarItem\n{\n    Text = \"界 &Deploy 🚀\",\n    StartAffix = new Affix(\"◆\", \"*\"),\n    EndAffix = new Affix(\"↑\", \"^\"),\n};\nitem.Invoked += (_, args) => Log(args.Cause);\ncommands.Items.Add(item);")));
    }

    /// <summary>Creates one item whose own activation event updates the visible evidence line.</summary>
    /// <param name="text">The mnemonic-aware authored caption.</param>
    /// <param name="label">The plain event label.</param>
    /// <param name="log">The retained item-event readout.</param>
    /// <returns>The initialized item.</returns>
    private static CommandBarItem CreateItem(string text, string label, Text log)
    {
        var item = new CommandBarItem { Text = text };
        item.Invoked += (_, eventArgs) =>
            log.Content = $"CommandBarItem.Invoked: {label} ({eventArgs.Cause})";
        return item;
    }

    /// <summary>Returns one item's caption without mnemonic syntax.</summary>
    /// <param name="item">The non-null item accepted by the owner.</param>
    /// <returns>The plain caption.</returns>
    private static string Plain(CommandBarItem item) => DocCaption.PlainCaption(item.Text);
}
