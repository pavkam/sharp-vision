// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents command-bar ownership, activation, and source-order overflow.</summary>
internal sealed class CommandBarPane: CompositeControlBase
{
    /// <summary>Initializes the retained command-bar documentation page.</summary>
    internal CommandBarPane() => InitializeContent(CreateContent());

    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "CommandBar";

    private static DocPage CreateContent()
    {
        var itemLog = new Text("Item event: waiting") { Overflow = Overflow.Wrap };
        var commandLog = new Text("Command log: waiting") { Overflow = Overflow.Wrap };
        var widthLog = new Text("Width: 30 cells · tail actions overflow") { Overflow = Overflow.Wrap };
        var width = 30;
        var bar = new CommandBar
        {
            Width = Length.Cells(width),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        var open = Item("&Open", "folder", itemLog);
        open.StartAffix = new Affix("▣", "#", SemanticColor.Accent);
        var save = Item("&Save", "document", itemLog);
        save.CommandParameter = "draft.md";
        save.Command = new ShowcaseCommand(
            parameter => commandLog.Content = $"Command log: saved {parameter}",
            static _ => true);
        var export = Item("E&xport", "archive", itemLog);
        var publish = Item("&Publish", "release", itemLog);
        publish.EndAffix = new Affix("↑", "^", SemanticColor.Success);
        var disabled = Item("S&hare", "disabled", itemLog);
        disabled.IsEnabled = false;
        bar.Items.Add(open);
        bar.Items.Add(save);
        bar.Items.Add(new CommandBarSeparator());
        bar.Items.Add(export);
        bar.Items.Add(publish);
        bar.Items.Add(disabled);
        bar.ItemInvoked += (_, eventArgs) =>
            itemLog.Content = $"Item event: {eventArgs.Item.Text.Replace("&", string.Empty, StringComparison.Ordinal)} ({eventArgs.Cause})";

        var narrower = new Button { Text = "&Narrower" };
        var wider = new Button { Text = "&Wider" };
        narrower.Click += (_, _) => Resize(-6);
        wider.Click += (_, _) => Resize(6);
        var resizeActions = new DocRow(narrower, wider);
        var specimen = new DocColumn(bar, resizeActions, widthLog, itemLog, commandLog);

        return new DocPage(
            Title,
            "<info>CommandBar</info> is one keyboard stop whose typed semantic actions remain retained while a stable tail moves into private Menu projections.",
            new DocSection(
                "⌘",
                "Retained command surface",
                "Resize the real bar to watch its longest fitting source prefix stay primary. The ellipsis opens private MenuItem projections; disabled Share remains visible but unavailable.",
                new DocExample(
                    "Resizable command bar",
                    "Use Narrower and Wider, activate access-key captions, or open the overflow trigger. Item and ICommand logs prove the semantic source remains the activation identity in either plane.",
                    specimen,
                    "var commands = new CommandBar { Width = Length.Cells(30) };\ncommands.Items.Add(new CommandBarItem { Text = \"&Open\" });\ncommands.Items.Add(new CommandBarSeparator());\ncommands.Items.Add(new CommandBarItem { Text = \"&Publish\" });\ncommands.ItemInvoked += (_, e) => Run(e.Item);")));

        void Resize(int delta)
        {
            var available = Math.Max(12, bar.Parent?.Bounds.Width ?? width);
            width = Math.Clamp(width + delta, 12, available);
            bar.Width = Length.Cells(width);
            widthLog.Content = $"Width: {width} cells · resize preserves source identity";
        }
    }

    private static CommandBarItem Item(string text, string label, Text log)
    {
        var item = new CommandBarItem { Text = text };
        item.Invoked += (_, eventArgs) => log.Content = $"Item event: {label} ({eventArgs.Cause})";
        return item;
    }
}
