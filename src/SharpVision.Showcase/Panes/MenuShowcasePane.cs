namespace SharpVision.Showcase.Panes;

using SharpVision.Controls;
using SharpVision.Layout;

/// <summary>Documents and demonstrates the Menu control.</summary>
internal sealed class MenuShowcasePane: ShowcasePane
{
    internal const string Title = "Menu";
    private const string _catalogSummary =
        "Arranges typed command, check, radio, and separator items with semantic selected state and keyboard navigation.";

    private static readonly InteractionDescription[] _catalogInteractions =
    [
        new InteractionDescription("Directional arrows", "Move according to Orientation", "The next eligible item becomes selected while separators and disabled items are skipped."),
        new InteractionDescription("Enter or Space", "Activate the selected item", "Check or radio state commits before ItemInvoked is raised."),
        new InteractionDescription("Pointer", "Click an available item", "The item selects and invokes through the same semantic path as keyboard activation."),
    ];

    private static readonly PropertyDescription[] _catalogProperties =
    [
        new PropertyDescription("Items", "MenuItems", "empty", "Owns detached MenuItem controls through a typed collection and tracks each item invocation."),
        new PropertyDescription("Orientation", "Orientation", "Horizontal", "Chooses left-to-right or top-to-bottom item geometry and matching directional keyboard navigation."),
        new PropertyDescription("Spacing", "int", "1", "Adds non-negative terminal cells between participating menu items."),
        new PropertyDescription("SelectedIndex", "int", "-1", "Selects the active non-separator item, applies checked visual state, and optionally moves keyboard focus."),
        new PropertyDescription("ItemInvoked", "event", "null", "Reports the activated item and the keyboard, pointer, or programmatic activation cause after state commit."),
    ];

    /// <summary>Initializes the Menu showcase page and composes its specimens.</summary>
    internal MenuShowcasePane()
        : base(Title, _catalogSummary, _catalogInteractions, _catalogProperties)
    {
    }


    /// <inheritdoc/>
    protected override void BuildExamples(ControlStack examples)
    {
        var status = new ControlText("Choose an action.") { Foreground = Palette.Muted };
        var menu = new ControlMenu
        {
            Orientation = Orientation.Vertical,
            Spacing = 0,
            Style = Palette.Interactive(),
        };
        menu.Items.Add(new MenuItem { Header = "New project" });
        menu.Items.Add(new MenuItem { Header = "Open recent" });
        menu.Items.Add(new MenuItem { Kind = MenuItemKind.Separator });
        menu.Items.Add(new MenuItem { Header = "Auto save", Kind = MenuItemKind.Check, IsChecked = true });
        menu.Items.Add(new MenuItem { Header = "Compact mode", Kind = MenuItemKind.Radio, GroupName = "density", IsChecked = true });
        menu.Items.Add(new MenuItem { Header = "Comfortable mode", Kind = MenuItemKind.Radio, GroupName = "density" });
        menu.ItemInvoked += (_, eventArgs) => status.Content = $"Invoked {eventArgs.Item.Header}.";
        examples.Children.Add(PaneSupport.SampleSection(
            "Command menu",
            "Use arrow keys to skip the separator, Enter or Space to invoke, or click an item. Check and radio states commit before the invocation message.",
            new ControlBorder
            {
                BorderThickness = new Thickness(1),
                Glyphs = Glyphs.Rounded,
                BorderColor = Palette.Border,
                Background = Palette.Surface,
                Child = menu,
            }));
        examples.Children.Add(status);
    }
}
