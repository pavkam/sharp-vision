using System.Diagnostics;

using SharpVision.Controls;
using SharpVision.Input;
using SharpVision.Layout;

using Attributes = SharpVision.Terminal.Rendering.Attributes;
using ControlText = SharpVision.Controls.Text;

namespace SharpVision.Showcase;

/// <summary>Builds the navigable traditional-control documentation gallery.</summary>
public sealed class Gallery: IDisposable
{
    private readonly ScrollView _main;
    private readonly NavigationItem[] _navigation;

    #region Construction and navigation

    /// <summary>Initializes the complete sidebar and first selected control page.</summary>
    public Gallery()
    {
        Pages = Catalog.Pages;
        _main = new ScrollView
        {
            HorizontalBarVisibility = ScrollBarVisibility.Auto,
            VerticalBarVisibility = ScrollBarVisibility.Auto,
        };
        _navigation = new NavigationItem[Pages.Count];
        var entries = new Stack { Padding = new Thickness(1, 0) };
        entries.Children.Add(new ControlText("Components")
        {
            Foreground = Palette.Accent,
            Background = Palette.Panel,
            Attributes = Attributes.Bold,
        });

        for (var index = 0; index < Pages.Count; index++)
        {
            var item = new NavigationItem(index, Pages[index].Name);
            item.Invoked += OnNavigationInvoked;
            _navigation[index] = item;
            entries.Children.Add(item);
        }

        var navigation = new ScrollView
        {
            Content = entries,
            HorizontalBarVisibility = ScrollBarVisibility.Auto,
            VerticalBarVisibility = ScrollBarVisibility.Auto,
        };
        var sidebarLayout = new Dock();
        var header = CreateSidebarHeader();
        var footer = CreateSidebarFooter();
        Dock.SetSide(header, Side.Top);
        Dock.SetSide(footer, Side.Bottom);
        sidebarLayout.Children.Add(header);
        sidebarLayout.Children.Add(footer);
        sidebarLayout.Children.Add(navigation);
        Sidebar = new Border
        {
            Width = Length.Cells(28),
            BorderThickness = new Thickness(1),
            Glyphs = Glyphs.Rounded,
            BorderColor = Palette.Border,
            Background = Palette.Panel,
            Child = sidebarLayout,
        };
        var surface = new Border
        {
            Background = Palette.Canvas,
            Child = _main,
        };
        Root = new Dock();
        Dock.SetSide(Sidebar, Side.Left);
        Root.Children.Add(Sidebar);
        Root.Children.Add(surface);
        Selected = Pages[0];
        Select(0);
    }

    /// <summary>Gets the root control passed directly to the application runtime.</summary>
    public Dock Root { get; }

    /// <summary>Gets the framed keyboard- and pointer-enabled component navigation sidebar.</summary>
    public Border Sidebar { get; }

    /// <summary>Gets the current documentation page content.</summary>
    public Control Content => _main.Content!;

    /// <summary>Gets the current exact concrete control name.</summary>
    public string SelectedPage => Selected.Name;

    /// <summary>Gets the selected page's stable zero-based catalog index.</summary>
    internal int SelectedIndex { get; private set; } = -1;

    /// <summary>Gets the stable complete control-page inventory.</summary>
    internal IReadOnlyList<Page> Pages { get; }

    /// <summary>Gets the stable stateful navigation entries in catalog order.</summary>
    internal IReadOnlyList<NavigationItem> Navigation => _navigation;

    /// <summary>Gets the currently selected immutable page definition.</summary>
    internal Page Selected { get; private set; }

    /// <summary>Selects one validated catalog page and replaces only the main documentation tree.</summary>
    /// <param name="index">The zero-based page index.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the catalog.</exception>
    internal void Select(int index)
    {
        if ((uint) index >= (uint) Pages.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "The page index is outside the catalog.");
        }

        Debug.Assert(_navigation[index].Label == Pages[index].Name);
        var previous = _main.Content;
        Selected = Pages[index];
        SelectedIndex = index;
        _main.Content = Selected.CreateContent();

        for (var navigationIndex = 0; navigationIndex < _navigation.Length; navigationIndex++)
        {
            _navigation[navigationIndex].SetSelected(navigationIndex == index);
        }

        previous?.Dispose();
    }

    private static Stack CreateSidebarHeader()
    {
        var header = new Stack
        {
            Height = Length.Cells(4),
            Padding = new Thickness(1, 0),
        };
        header.Children.Add(new ControlText("SHARP VISION")
        {
            Foreground = Palette.Accent,
            Background = Palette.Panel,
            Attributes = Attributes.Bold,
        });
        header.Children.Add(new ControlText("Terminal UI toolkit")
        {
            Foreground = Palette.Muted,
            Background = Palette.Panel,
        });
        header.Children.Add(new ControlText("Control showcase")
        {
            Foreground = Palette.Text,
            Background = Palette.Panel,
        });
        return header;
    }

    private static ControlText CreateSidebarFooter() => new("Enter select · Click")
    {
        Height = Length.Cells(2),
        Padding = new Thickness(1, 0),
        Foreground = Palette.Muted,
        Background = Palette.Panel,
        Attributes = Attributes.Dim,
    };

    private void OnNavigationInvoked(object? sender, ActivationEventArgs eventArgs)
    {
        _ = eventArgs;

        if (sender is NavigationItem item)
        {
            Select(item.Index);
        }
    }

    #endregion

    #region Lifetime

    /// <summary>Releases the detached or application-owned gallery control tree.</summary>
    public void Dispose()
    {
        foreach (var item in _navigation)
        {
            item.Invoked -= OnNavigationInvoked;
        }

        Root.Dispose();
        GC.SuppressFinalize(this);
    }

    #endregion
}
