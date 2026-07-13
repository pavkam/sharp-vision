// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase;

using System.Diagnostics;

using SharpVision.Controls;
using SharpVision.Input;
using SharpVision.Layout;
using SharpVision.Runtime;
using SharpVision.Showcase.Panes;
using SharpVision.Styling;
using SharpVision.Terminal.Input;

using KeyAction = Terminal.Input.Action;
using TerminalAttributes = Terminal.Rendering.Attributes;

/// <summary>Builds the navigable traditional-control documentation gallery.</summary>
public sealed class Gallery: Screen
{
    private readonly ControlScrollView _main;
    private readonly ControlScrollView _navigationScroll;
    private readonly NavigationItem[] _navigation;
    private readonly ControlButton _lightTheme;
    private readonly ControlButton _darkTheme;
    private FocusManager? _focus;

    #region Construction and navigation

    /// <summary>Initializes the complete sidebar and first selected control page.</summary>
    public Gallery()
    {
        Pages = PaneCatalog.Pages;
        _main = new ControlScrollView
        {
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.WhenNeeded,
            ScrollBarChrome = ScrollBarStyle.Thin,
            ScrollBarFill = ScrollBarFill.Line,
            HorizontalBarVisibility = ScrollBarVisibility.Hidden,
            ConstrainContentToViewport = true,
        };
        _navigation = new NavigationItem[Pages.Count];
        ControlStack entries = new ControlStack { Padding = new Thickness(1, 0) };
        entries.Children.Add(new ControlText("Components")
        {
            Foreground = Palette.Accent,
            Background = Palette.Panel,
            Attributes = TerminalAttributes.Bold,
        });

        for (var index = 0; index < Pages.Count; index++)
        {
            NavigationItem item = new NavigationItem(index, Pages[index].Name)
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            item.Invoked += OnNavigationInvoked;
            _navigation[index] = item;
            entries.Children.Add(item);
        }

        _navigationScroll = new ControlScrollView
        {
            Content = entries,
            ScrollBars = ScrollBars.Both,
            ShowScrollBars = ShowScrollBars.WhenNeeded,
            ScrollBarChrome = ScrollBarStyle.Thin,
            ScrollBarFill = ScrollBarFill.Line,
        };
        ControlDock sidebarLayout = new ControlDock();
        ControlStack header = CreateSidebarHeader();
        _lightTheme = new ControlButton { Content = new ControlText("Light") };
        _darkTheme = new ControlButton { Content = new ControlText("Dark") };
        _lightTheme.Click += (_, _) => SetTheme(Themes.White);
        _darkTheme.Click += (_, _) => SetTheme(Themes.Dark);
        ControlStack footer = CreateSidebarFooter(_lightTheme, _darkTheme);
        ControlDock.SetSide(header, Side.Top);
        ControlDock.SetSide(footer, Side.Bottom);
        sidebarLayout.Children.Add(header);
        sidebarLayout.Children.Add(footer);
        sidebarLayout.Children.Add(_navigationScroll);
        Sidebar = new ControlBorder
        {
            Width = Length.Cells(28),
            BorderThickness = new Thickness(1),
            Glyphs = Glyphs.Rounded,
            BorderColor = Palette.Border,
            Background = Palette.Panel,
            Child = sidebarLayout,
        };
        _ = Sidebar.AddHandler(Events.Key, OnNavigationKey);
        ControlBorder surface = new ControlBorder
        {
            Background = Palette.Canvas,
            Child = _main,
        };
        ControlDock layout = new ControlDock
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        ControlDock.SetSide(Sidebar, Side.Left);
        layout.Children.Add(Sidebar);
        layout.Children.Add(surface);
        Children.Add(layout);
        Selected = Pages[0];
        Select(0);
    }

    /// <summary>Gets the framed keyboard- and pointer-enabled component navigation sidebar.</summary>
    public ControlBorder Sidebar { get; }

    /// <summary>Gets the current documentation page content.</summary>
    public Control Content => _main.Content!;

    /// <summary>Gets the current exact concrete control name.</summary>
    public string SelectedPage => Selected.Name;

    /// <summary>Gets the selected page's stable zero-based catalog index.</summary>
    internal int SelectedIndex { get; private set; } = -1;

    /// <summary>Gets the stable complete control-page inventory.</summary>
    internal IReadOnlyList<GalleryEntry> Pages { get; }

    /// <summary>Gets the stable stateful navigation entries in catalog order.</summary>
    internal IReadOnlyList<NavigationItem> Navigation => _navigation;

    /// <summary>Gets the currently selected gallery entry.</summary>
    internal GalleryEntry Selected { get; private set; }

    /// <inheritdoc/>
    protected override void OnAttach(Application application)
    {
        Theme theme = Themes.Dark.Clone();
        theme.SetStyle(Palette.ListForTheme());
        application.Theme = theme;
    }

    /// <inheritdoc/>
    protected override void OnStarted(Application application) =>
        _ = FocusSelected(application.Focus);

    /// <summary>Focuses the selected sidebar entry after the application has attached the gallery tree.</summary>
    /// <param name="focus">The non-null attached root focus manager.</param>
    /// <returns>True when the selected entry accepted focus; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="focus"/> is null.</exception>
    internal bool FocusSelected(FocusManager focus)
    {
        ArgumentNullException.ThrowIfNull(focus);
        _focus = focus;
        return focus.Focus(_navigation[SelectedIndex]);
    }

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
        Control? previous = _main.Content;
        Selected = Pages[index];
        SelectedIndex = index;

        // A catalog selection changes subject, not just content. Preserve the
        // sidebar state but start the newly created documentation page at its header.
        if (_main.HorizontalOffset != 0)
        {
            _main.HorizontalOffset = 0;
        }

        if (_main.VerticalOffset != 0)
        {
            _main.VerticalOffset = 0;
        }

        _main.Content = Selected.CreatePane();

        for (var navigationIndex = 0; navigationIndex < _navigation.Length; navigationIndex++)
        {
            _navigation[navigationIndex].SetSelected(navigationIndex == index);
        }

        previous?.Dispose();
    }

    private static ControlStack CreateSidebarHeader()
    {
        ControlStack header = new ControlStack
        {
            Height = Length.Cells(4),
            Padding = new Thickness(1, 0),
        };
        header.Children.Add(new ControlText("SHARP VISION")
        {
            Foreground = Palette.Accent,
            Background = Palette.Panel,
            Attributes = TerminalAttributes.Bold,
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

    private static ControlStack CreateSidebarFooter(ControlButton lightTheme, ControlButton darkTheme)
    {
        ControlStack footer = new ControlStack
        {
            Height = Length.Cells(4),
            Padding = new Thickness(1, 0),
            Spacing = 0,
        };
        footer.Children.Add(new ControlText("Theme")
        {
            Foreground = Palette.Muted,
            Background = Palette.Panel,
            Attributes = TerminalAttributes.Dim,
        });
        footer.Children.Add(new ControlStack
        {
            Orientation = Orientation.Horizontal,
            Spacing = 1,
            Children = { lightTheme, darkTheme },
        });
        footer.Children.Add(new ControlText("Enter select · Click")
        {
            Foreground = Palette.Muted,
            Background = Palette.Panel,
            Attributes = TerminalAttributes.Dim,
        });
        return footer;
    }

    private void SetTheme(Theme theme)
    {
        if (Application is { } application)
        {
            application.Theme = theme;
        }
    }

    private void OnNavigationInvoked(object? sender, ActivationEventArgs eventArgs)
    {
        _ = eventArgs;

        if (sender is NavigationItem item)
        {
            Select(item.Index);
        }
    }

    private void OnNavigationKey(object? sender, KeyEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.Phase != Phase.Bubble || eventArgs.Stroke.Action != KeyAction.Press)
        {
            return;
        }

        NavigationItem current = FindNavigation(eventArgs.OriginalSource) ?? _navigation[SelectedIndex];
        var target = ResolveNavigation(current.Index, eventArgs.Stroke);

        if (target < 0)
        {
            return;
        }

        Select(target);
        _ = _focus?.Focus(_navigation[target]);
        _ = _navigationScroll.BringIntoView(_navigation[target]);
        eventArgs.Handled = true;
    }

    private int ResolveNavigation(int current, Stroke stroke)
    {
        var count = _navigation.Length;

        if (stroke.Code is Code.Up or Code.Left ||
            (stroke.Code == Code.Tab && (stroke.Modifiers & Modifiers.Shift) != 0))
        {
            return Math.Max(0, current - 1);
        }

        if (stroke.Code is Code.Down or Code.Right or Code.Tab)
        {
            return Math.Min(count - 1, current + 1);
        }

        if (stroke.Code == Code.Home)
        {
            return 0;
        }

        if (stroke.Code == Code.End)
        {
            return count - 1;
        }

        var page = Math.Max(1, _navigationScroll.Viewport.Height - 1);

        return stroke.Code == Code.PageUp
            ? Math.Max(0, current - page)
            : stroke.Code == Code.PageDown
            ? Math.Min(count - 1, current + page)
            : -1;
    }

    private static NavigationItem? FindNavigation(Control? source)
    {
        for (Control? current = source; current is not null; current = current.Parent)
        {
            if (current is NavigationItem item)
            {
                return item;
            }
        }

        return null;
    }

    /// <inheritdoc/>
    protected override void OnDispose()
    {
        foreach (NavigationItem item in _navigation)
        {
            item.Invoked -= OnNavigationInvoked;
        }

        _focus = null;
    }

    #endregion
}
