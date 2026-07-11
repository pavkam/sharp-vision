using System.Diagnostics;

using SharpVision.Controls;
using SharpVision.Input;
using SharpVision.Layout;

using ControlList = SharpVision.Controls.List;

namespace SharpVision.Showcase;

/// <summary>Builds the navigable traditional-control documentation gallery.</summary>
public sealed class Gallery: IDisposable
{
    private readonly ScrollView _main;

    #region Construction and navigation

    /// <summary>Initializes the complete sidebar and first selected control page.</summary>
    public Gallery()
    {
        Pages = Catalog.Pages;
        Sidebar = new ControlList
        {
            Width = Length.Cells(24),
            Items = Pages.Select(static page => (object?) page.Name).ToArray(),
        };
        Sidebar.SelectionChanged += OnSelectionChanged;
        _main = new ScrollView
        {
            HorizontalBarVisibility = ScrollBarVisibility.Auto,
            VerticalBarVisibility = ScrollBarVisibility.Auto,
        };
        Root = new Dock { Spacing = 1 };
        Dock.SetSide(Sidebar, Side.Left);
        Root.Children.Add(Sidebar);
        Root.Children.Add(_main);
        Selected = Pages[0];
        Sidebar.SelectedIndex = 0;
    }

    /// <summary>Gets the root control passed directly to the application runtime.</summary>
    public Dock Root { get; }

    /// <summary>Gets the selectable keyboard- and pointer-enabled control sidebar.</summary>
    public ControlList Sidebar { get; }

    /// <summary>Gets the current documentation page content.</summary>
    public Control Content => _main.Content!;

    /// <summary>Gets the current exact concrete control name.</summary>
    public string SelectedPage => Selected.Name;

    /// <summary>Gets the stable complete control-page inventory.</summary>
    internal IReadOnlyList<Page> Pages { get; }

    /// <summary>Gets the currently selected immutable page definition.</summary>
    internal Page Selected { get; private set; }

    private void OnSelectionChanged(object? sender, ListSelectionChangedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        var index = Sidebar.SelectedIndex;

        if ((uint) index >= (uint) Pages.Count)
        {
            return;
        }

        Debug.Assert(Pages[index].Name == Sidebar.Items[index]?.ToString());
        var previous = _main.Content;
        Selected = Pages[index];
        _main.Content = Selected.CreateContent();
        previous?.Dispose();
    }

    #endregion

    #region Lifetime

    /// <summary>Releases the detached or application-owned gallery control tree.</summary>
    public void Dispose()
    {
        Sidebar.SelectionChanged -= OnSelectionChanged;
        Root.Dispose();
        GC.SuppressFinalize(this);
    }

    #endregion
}
