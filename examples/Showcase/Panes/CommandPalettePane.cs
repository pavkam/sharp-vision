// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents command-palette resolution, embedded composition, placement, and chrome.</summary>
internal sealed class CommandPalettePane: CompositeControlBase
{
    private static readonly string[] _commands =
    [
        "Open file",
        "Open folder",
        "Go to symbol",
        "Toggle terminal",
        "Change theme",
        "Format document",
        "Run tests",
        "Show shortcuts"
    ];

    internal CommandPalettePane() => InitializeContent(CreateContent());

    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "CommandPalette";

    private static DocPage CreateContent()
    {
        var embeddedStatus = new Text("Type to resolve commands on demand.");
        var embedded = Palette(borderless: true);
        embedded.Width = Length.Cells(24);
        embedded.StartAffix = new Affix("⌕", "?");
        embedded.EndAffix = new Affix("/", "/");
        WireStatus(embedded, embeddedStatus);
        var file = new MenuItem { Text = "&File" };
        var edit = new MenuItem { Text = "&Edit" };
        var focusPalette = new MenuItem { Text = "&Command" };
        var menu = new Menu { Orientation = Orientation.Horizontal, Spacing = 1 };
        menu.Items.Add(file);
        menu.Items.Add(edit);
        menu.Items.Add(focusPalette);
        focusPalette.Invoked += (_, _) => _ = embedded.Open();
        var menuBar = new Stack
        {
            Orientation = Orientation.Horizontal,
            Spacing = 1,
            Border = new Border(
                BorderSide.Bottom,
                BorderGlyphStyle.Light,
                SemanticColor.ControlBorder,
                Color.Transparent,
                SemanticDecoration.Border),
            Children = { menu, embedded }
        };
        var embeddedExample = new Stack
        {
            Width = Length.Cells(44),
            Spacing = 1,
            Children = { menuBar, embeddedStatus }
        };

        var presentationStatus = new Text("Choose a presentation.");
        var centered = Palette(borderless: false);
        centered.Width = Length.Cells(32);
        centered.HorizontalAlignment = HorizontalAlignment.Center;
        centered.VerticalAlignment = VerticalAlignment.Center;
        centered.Visibility = Visibility.Collapsed;
        centered.PopupChrome = new PopupChrome
        {
            Shadow = new Shadow(
                true,
                ShadowMode.BlockGlyph,
                new Point(1, 1),
                new Rune('▓'),
                SemanticColor.ControlShadow,
                SemanticColor.ControlShadow,
                SemanticDecoration.Shadow)
        };
        var topCentered = Palette(borderless: false);
        topCentered.Width = Length.Cells(32);
        topCentered.HorizontalAlignment = HorizontalAlignment.Center;
        topCentered.VerticalAlignment = VerticalAlignment.Top;
        topCentered.Visibility = Visibility.Collapsed;
        topCentered.StartAffix = new Affix(">");
        topCentered.FieldBorder = new Border(
            BorderSide.All,
            BorderGlyphStyle.Ascii,
            SemanticColor.ControlBorder,
            Color.Transparent,
            SemanticDecoration.Border);
        topCentered.PopupChrome = new PopupChrome
        {
            Border = new Border(
                BorderSide.All,
                BorderGlyphStyle.Ascii,
                SemanticColor.ControlBorder,
                Color.Transparent,
                SemanticDecoration.Border)
        };
        WireStatus(centered, presentationStatus, hideAfterInvoke: true);
        WireStatus(topCentered, presentationStatus, hideAfterInvoke: true);
        var showCenter = new Button { Text = "Open &centered" };
        var showTop = new Button { Text = "Open at &top" };
        showCenter.Click += (_, _) => Show(centered, topCentered, presentationStatus, "Centered palette");
        showTop.Click += (_, _) => Show(topCentered, centered, presentationStatus, "Top-centered palette");
        var triggers = new Stack
        {
            Orientation = Orientation.Horizontal,
            Spacing = 1,
            Children = { showCenter, showTop }
        };
        Overlay.SetTop(triggers, Length.Cells(12));
        Overlay.SetTop(presentationStatus, Length.Cells(15));
        var presentationStage = new Overlay
        {
            Width = Length.Cells(44),
            Height = Length.Cells(18),
            ClipToBounds = false,
            Border = new Border(
                BorderSide.All,
                BorderGlyphStyle.Light,
                SemanticColor.ControlBorder,
                Color.Transparent,
                SemanticDecoration.Border),
            Children = { centered, topCentered, triggers, presentationStatus }
        };

        return new DocPage(
            Title,
            "<info>CommandPalette</info> keeps a real TextInput focused while a cancellable resolver supplies fresh ListView results; pending queries cannot invoke stale rows.",
            new DocSection(
                "⌕",
                "Embedded search",
                "Compose a borderless palette beside a real menu and use affixes as compact visual delimiters.",
                new DocExample(
                    "Menu-bar command search",
                    "Activate <reverse>Command</reverse> to focus the embedded editor, then type. The resolver filters on every edit and the result popup follows the field.",
                    embeddedExample,
                    "var palette = new CommandPalette\n{\n    Resolver = ResolveCommands,\n    FieldBorder = borderless,\n    StartAffix = new Affix(\"⌕\", \"?\")\n};\ncommandItem.Invoked += (_, _) => palette.Open();")),
            new DocSection(
                "⌘",
                "Triggered placement",
                "Place the same component with ordinary Overlay alignment; the palette owns search and results, while layout owns where it appears.",
                new DocExample(
                    "Centered and top-centered variants",
                    "Open either presentation, type to narrow the live results, and invoke a row. The centered variant uses a shadow; the top-centered variant uses ASCII field and popup borders.",
                    presentationStage,
                    "palette.HorizontalAlignment = HorizontalAlignment.Center;\npalette.VerticalAlignment = VerticalAlignment.Center;\npalette.Visibility = Visibility.Visible;\npalette.Open();")));
    }

    private static CommandPalette Palette(bool borderless)
    {
        var palette = new CommandPalette
        {
            Placeholder = "Type a command…",
            Resolver = ResolveCommands,
            DropDownHeight = 6
        };

        if (borderless)
        {
            palette.FieldBorder = new Border(
                BorderSide.None,
                BorderGlyphStyle.Default,
                Color.Default,
                Color.Transparent,
                TerminalAttributes.None);
        }

        return palette;
    }

    private static ValueTask<IReadOnlyList<object?>> ResolveCommands(
        string searchTerms,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var matches = new List<object?>();

        foreach (var command in _commands)
        {
            if (searchTerms.Length == 0 || command.Contains(searchTerms, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(command);
            }
        }

        return ValueTask.FromResult<IReadOnlyList<object?>>(matches);
    }

    private static void WireStatus(CommandPalette palette, Text status, bool hideAfterInvoke = false)
    {
        palette.ItemInvoked += (_, eventArgs) =>
        {
            status.Content = $"Invoked: {eventArgs.Item}";

            if (hideAfterInvoke)
            {
                palette.Visibility = Visibility.Collapsed;
            }
        };
        palette.ResolutionFailed += (_, eventArgs) =>
            status.Content = $"Resolver failed: {eventArgs.Exception.Message}";
    }

    private static void Show(
        CommandPalette palette,
        CommandPalette other,
        Text status,
        string label)
    {
        other.Close();
        other.Visibility = Visibility.Collapsed;
        palette.Visibility = Visibility.Visible;
        status.Content = $"{label} opened.";
        _ = palette.Open();
    }
}
