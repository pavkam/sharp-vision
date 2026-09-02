// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents command-bar ownership, activation, entries, and source-order overflow.</summary>
internal sealed class CommandBarPane: CompositeControlBase
{
    /// <summary>Initializes the command-bar documentation page.</summary>
    internal CommandBarPane() => InitializeContent(CreateContent());

    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "CommandBar";

    private static DocPage CreateContent()
    {
        var sourceLog = new Text("CommandBarItem.Invoked source: waiting") { Overflow = Overflow.Wrap };
        var ownerLog = new Text("CommandBar.ItemInvoked source: waiting") { Overflow = Overflow.Wrap };
        var commandLog = new Text("ICommand source: waiting") { Overflow = Overflow.Wrap };
        var widthLog = new Text("Width: 30 cells · source-order tail overflows") { Overflow = Overflow.Wrap };
        var width = 30;
        var bar = new CommandBar
        {
            Width = Length.Cells(width),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        var open = CreateItem("&Open", "Open", sourceLog);
        open.StartAffix = new Affix("▣", "#", SemanticColor.Accent);
        var save = CreateItem("&Save", "Save", sourceLog);
        save.CommandParameter = "draft.md";
        save.Command = new ShowcaseCommand(
            parameter => commandLog.Content = $"ICommand source: saved {parameter}",
            static _ => true);
        var export = CreateItem("E&xport", "Export", sourceLog);
        var publish = CreateItem("&Publish", "Publish", sourceLog);
        publish.EndAffix = new Affix("↑", "^", SemanticColor.Success);
        var disabled = CreateItem("S&hare", "Share", sourceLog);
        disabled.IsEnabled = false;
        bar.Items.Add(open);
        bar.Items.Add(save);
        bar.Items.Add(new CommandBarSeparator());
        bar.Items.Add(export);
        bar.Items.Add(publish);
        bar.Items.Add(disabled);
        bar.ItemInvoked += (_, eventArgs) =>
            ownerLog.Content = $"CommandBar.ItemInvoked source: {Plain(eventArgs.Item)} ({eventArgs.Cause})";

        var narrower = new Button { Text = "&Narrow bar" };
        var wider = new Button { Text = "&Widen bar" };
        var toggleBar = new Button { Text = "Disable b&ar" };
        narrower.Click += (_, _) => Resize(-6);
        wider.Click += (_, _) => Resize(6);
        toggleBar.Click += (_, _) =>
        {
            bar.IsEnabled = !bar.IsEnabled;
            toggleBar.Text = bar.IsEnabled ? "Disable b&ar" : "Enable ba&r";
        };
        var specimen = new DocColumn(
            bar,
            new DocRow(narrower, wider),
            toggleBar,
            widthLog,
            sourceLog,
            ownerLog,
            commandLog)
        {
            Width = Length.Cells(44)
        };

        var itemLog = new Text("CommandBarItem.Invoked: waiting") { Overflow = Overflow.Wrap };
        var itemOwnerLog = new Text("CommandBar.ItemInvoked: waiting") { Overflow = Overflow.Wrap };
        var itemCommandLog = new Text("ICommand: waiting") { Overflow = Overflow.Wrap };
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
            parameter => itemCommandLog.Content = $"ICommand: deployed {parameter}",
            static _ => true);
        var archive = CreateItem("&Archive", "Archive", itemLog);
        archive.IsEnabled = false;
        var itemBar = new CommandBar
        {
            Width = Length.Cells(42),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        itemBar.Items.Add(build);
        itemBar.Items.Add(deploy);
        itemBar.Items.Add(archive);
        itemBar.ItemInvoked += (_, eventArgs) =>
            itemOwnerLog.Content = $"CommandBar.ItemInvoked: {Plain(eventArgs.Item)} ({eventArgs.Cause})";
        var invokeDeploy = new Button { Text = "&Invoke Deploy" };
        invokeDeploy.Click += (_, _) => deploy.PerformInvoke();
        var toggleDeploy = new Button { Text = "Toggle Deploy availabilit&y" };
        toggleDeploy.Click += (_, _) =>
        {
            deploy.IsEnabled = !deploy.IsEnabled;
            availabilityLog.Content = deploy.IsEnabled
                ? "Deploy availability: enabled"
                : "Deploy availability: disabled · activation is inert";
        };
        var itemActions = new Wrap { Width = Length.Cells(42), Spacing = 1, LineSpacing = 1 };
        itemActions.Children.Add(invokeDeploy);
        itemActions.Children.Add(toggleDeploy);

        var separatorLog = new Text("CommandBarSeparator.PropertyChanged: waiting")
        {
            Overflow = Overflow.Wrap
        };
        var neighborLog = new Text("Neighbor item event: waiting") { Overflow = Overflow.Wrap };
        var glyphs = new[]
        {
            new ControlGlyph(new Rune('╎'), new Rune('|')),
            new ControlGlyph(new Rune('┃'), new Rune('|'))
        };
        var glyphIndex = 0;
        var separator = new CommandBarSeparator
        {
            Style = CommandBarSeparatorStyle.Default with
            {
                Face = CommandBarSeparatorStyle.Default.Face with { Foreground = SemanticColor.Accent },
                Glyph = glyphs[glyphIndex]
            }
        };
        separator.PropertyChanged += (_, eventArgs) =>
            separatorLog.Content =
                $"CommandBarSeparator.PropertyChanged: {eventArgs.PropertyName} · " +
                $"{separator.Visibility} · glyph {separator.ActualStyle.Glyph.Value}";
        var compile = new CommandBarItem { Text = "&Compile" };
        var separatorPublish = new CommandBarItem { Text = "P&ublish" };
        compile.Invoked += (_, eventArgs) =>
            neighborLog.Content = $"Neighbor item event: Compile ({eventArgs.Cause})";
        separatorPublish.Invoked += (_, eventArgs) =>
            neighborLog.Content = $"Neighbor item event: Publish ({eventArgs.Cause})";
        var separatorBar = new CommandBar
        {
            Width = Length.Cells(38),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        separatorBar.Items.Add(compile);
        separatorBar.Items.Add(separator);
        separatorBar.Items.Add(separatorPublish);
        var cycleGlyph = new Button { Text = "Cycle &glyph" };
        cycleGlyph.Click += (_, _) =>
        {
            glyphIndex = (glyphIndex + 1) % glyphs.Length;
            separator.Style = separator.ActualStyle with { Glyph = glyphs[glyphIndex] };
        };
        var toggleSeparator = new Button { Text = "Toggle &visibility" };
        toggleSeparator.Click += (_, _) =>
            separator.Visibility = separator.Visibility == Visibility.Visible
                ? Visibility.Hidden
                : Visibility.Visible;

        return new DocPage(
            Title,
            "<info>CommandBar</info> is one keyboard stop whose retained commands remain the semantic sources while a stable source-order tail moves into a framed private Menu.",
            new DocSection(
                "⌘",
                "Retained command surface",
                "Resize the bounded bar to watch its longest fitting source prefix stay primary. The ellipsis opens framed private MenuItem projections; disabled Share remains visible but unavailable.",
                new DocExample(
                    "Resizable command bar",
                    "Resize or disable the bar, activate a caption, or open overflow. Disabled rows and the overflow trigger keep the shared Bar fill while their ink changes. Separate logs prove the CommandBarItem, owner event, and ICommand retain the original semantic source.",
                    specimen,
                    "var commands = new CommandBar { Width = Length.Cells(30) };\n" +
                    "commands.Items.Add(new CommandBarItem { Text = \"&Open\" });\n" +
                    "commands.Items.Add(new CommandBarSeparator());\n" +
                    "commands.Items.Add(new CommandBarItem { Text = \"&Publish\" });\n" +
                    "commands.ItemInvoked += (_, e) => Run(e.Item);\n" +
                    "var toggle = new Button { Text = \"Toggle bar\" };\n" +
                    "toggle.Click += (_, _) => commands.IsEnabled = !commands.IsEnabled;")),
            new DocSection(
                "🧩",
                "Owned entry roles",
                "Command items keep their own activation, command, affix, style, and availability state. Separators remain passive participants that can change style or layout visibility.",
                new DocExample(
                    "Item activation and availability",
                    "Activate Build or Deploy in the owner, invoke Deploy programmatically, or disable it. The readouts distinguish the named item's event, the owner's forwarded event, and captured ICommand execution.",
                    new DocColumn(
                        itemBar,
                        itemActions,
                        availabilityLog,
                        itemLog,
                        itemOwnerLog,
                        itemCommandLog),
                    "var item = new CommandBarItem { Text = \"界 &Deploy 🚀\" };\n" +
                    "item.Invoked += (_, args) => Log(args.Cause);\n" +
                    "commands.Items.Add(item);"),
                new DocExample(
                    "Separator styling and participation",
                    "Cycle the preferred glyph, hide or show the separator, and activate either neighbor. PropertyChanged proves the separator's own state while the item log proves navigation skips it.",
                    new DocColumn(
                        separatorBar,
                        new DocRow(cycleGlyph, toggleSeparator),
                        separatorLog,
                        neighborLog),
                    "commands.Items.Add(new CommandBarSeparator\n" +
                    "{\n" +
                    "    Style = CommandBarSeparatorStyle.Default with\n" +
                    "    {\n" +
                    "        Glyph = new ControlGlyph(new Rune('╎'), new Rune('|'))\n" +
                    "    }\n" +
                    "});")));

        void Resize(int delta)
        {
            width = Math.Clamp(width + delta, 12, 44);
            bar.Width = Length.Cells(width);
            widthLog.Content = $"Width: {width} cells · source-order identities preserved";
        }
    }

    private static CommandBarItem CreateItem(string text, string label, Text log)
    {
        var item = new CommandBarItem { Text = text };
        item.Invoked += (_, eventArgs) =>
            log.Content = $"CommandBarItem.Invoked source: {label} ({eventArgs.Cause})";
        return item;
    }

    private static string Plain(CommandBarItem item) => DocCaption.PlainCaption(item.Text);
}
