// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Builds the navigable traditional-control documentation gallery.</summary>
public sealed class Gallery: Screen
{
    private static readonly string[] _groups =
    [
        "Concepts",
        "Input",
        "Collections",
        "Navigation",
        "Layout",
        "Display",
        "Progress",
        "Dialogs",
        "Windows"
    ];

    private static readonly (string Group, string Name, Func<CompositeControlBase> Create)[] _catalog =
    [
        ("Concepts", ControlPane.Title, static () => new ControlPane()),
        ("Concepts", BorderPane.Title, static () => new BorderPane()),
        ("Concepts", ShadowPane.Title, static () => new ShadowPane()),
        ("Concepts", DataBindingPane.Title, static () => new DataBindingPane()),
        ("Concepts", StylingPane.Title, static () => new StylingPane()),
        ("Input", ButtonPane.Title, static () => new ButtonPane()),
        ("Input", HyperlinkButtonPane.Title, static () => new HyperlinkButtonPane()),
        ("Input", CalendarPane.Title, static () => new CalendarPane()),
        ("Input", DateInputPane.Title, static () => new DateInputPane()),
        ("Input", DateTimeInputPane.Title, static () => new DateTimeInputPane()),
        ("Display", CanvasPane.Title, static () => new CanvasPane()),
        ("Progress", ChaseIndicatorPane.Title, static () => new ChaseIndicatorPane()),
        ("Input", CheckBoxPane.Title, static () => new CheckBoxPane()),
        ("Input", ColorPickerPane.Title, static () => new ColorPickerPane()),
        ("Input", ComboBoxPane.Title, static () => new ComboBoxPane()),
        ("Layout", DockPane.Title, static () => new DockPane()),
        ("Layout", ExpanderPane.Title, static () => new ExpanderPane()),
        ("Dialogs", FilePickerPane.Title, static () => new FilePickerPane()),
        ("Dialogs", MessageBoxPane.Title, static () => new MessageBoxPane()),
        ("Display", FigletTextPane.Title, static () => new FigletTextPane()),
        ("Layout", GridPane.Title, static () => new GridPane()),
        ("Layout", GroupBoxPane.Title, static () => new GroupBoxPane()),
        ("Display", ImagePane.Title, static () => new ImagePane()),
        ("Collections", ListViewPane.Title, static () => new ListViewPane()),
        ("Navigation", MenuPane.Title, static () => new MenuPane()),
        ("Navigation", NavigationViewPane.Title, static () => new NavigationViewPane()),
        ("Layout", OverlayPane.Title, static () => new OverlayPane()),
        ("Windows", PopupPane.Title, static () => new PopupPane()),
        ("Windows", ContextMenuPane.Title, static () => new ContextMenuPane()),
        ("Windows", TooltipPane.Title, static () => new TooltipPane()),
        ("Windows", FlyoutPane.Title, static () => new FlyoutPane()),
        ("Display", PrismPane.Title, static () => new PrismPane()),
        ("Progress", ProgressBarPane.Title, static () => new ProgressBarPane()),
        ("Input", RadioButtonPane.Title, static () => new RadioButtonPane()),
        ("Layout", ScrollBarPane.Title, static () => new ScrollBarPane()),
        ("Display", SeparatorPane.Title, static () => new SeparatorPane()),
        ("Display", StatusBarPane.Title, static () => new StatusBarPane()),
        ("Input", SliderPane.Title, static () => new SliderPane()),
        ("Progress", SpinnerPane.Title, static () => new SpinnerPane()),
        ("Layout", StackPane.Title, static () => new StackPane()),
        ("Collections", TabControlPane.Title, static () => new TabControlPane()),
        ("Collections", TablePane.Title, static () => new TablePane()),
        ("Collections", TreeViewPane.Title, static () => new TreeViewPane()),
        ("Display", TextPane.Title, static () => new TextPane()),
        ("Input", TextInputPane.Title, static () => new TextInputPane()),
        ("Input", TimeInputPane.Title, static () => new TimeInputPane()),
        ("Windows", WindowPane.Title, static () => new WindowPane())
    ];

    // Ordered theme catalog surfaced by the sidebar picker: every embedded theme from
    // SharpVision.Styling.Themes, dark group first then light, preserving each
    // group's catalog (order, slug) sequence. A new embedded theme JSON resource is one entry.
    private static readonly ThemeCatalogEntry[] _themePickerEntries = BuildThemePickerEntries();

    private static ThemeCatalogEntry[] BuildThemePickerEntries()
    {
        var entries = Themes.Entries;
        var ordered = new List<ThemeCatalogEntry>(entries.Count);

        foreach (var entry in entries)
        {
            if (entry.ColorScheme == ColorScheme.Dark)
            {
                ordered.Add(entry);
            }
        }

        foreach (var entry in entries)
        {
            if (entry.ColorScheme == ColorScheme.Light)
            {
                ordered.Add(entry);
            }
        }

        return [.. ordered];
    }

    private readonly Dock _main;
    private readonly NavigationViewItem[] _navigation;
    private readonly NavigationViewGroup[] _navigationGroups;
    private readonly TextInput _pageFilter;
    private readonly ComboBox _themePicker;
    private readonly Button _quit;

    #region Construction and navigation

    /// <summary>Initializes the complete sidebar and first selected control page.</summary>
    public Gallery()
    {
        Pages = Array.ConvertAll(_catalog, static entry => entry.Name);
        _main = new Dock();
        NavigationControl = new NavigationView { Header = "Showcase", UseMnemonic = false };
        _navigation = new NavigationViewItem[Pages.Count];
        _navigationGroups = new NavigationViewGroup[_groups.Length];

        for (var groupIndex = 0; groupIndex < _groups.Length; groupIndex++)
        {
            var groupName = _groups[groupIndex];
            var group = new NavigationViewGroup { Header = groupName, UseMnemonic = false };
            _navigationGroups[groupIndex] = group;

            for (var index = 0; index < Pages.Count; index++)
            {
                if (_catalog[index].Group != groupName)
                {
                    continue;
                }

                var item = new NavigationViewItem
                {
                    Text = Pages[index],
                    UseMnemonic = false,
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                _navigation[index] = item;
                item.Invoked += OnNavigationItemInvoked;
                group.Items.Add(item);
            }

            NavigationControl.Items.Add(group);
        }

        NavigationControl.SelectionChanged += OnNavigationSelectionChanged;
        var sidebarLayout = new Dock();
        _pageFilter = new TextInput
        {
            Placeholder = "Filter pages",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _pageFilter.TextChanged += OnPageFilterTextChanged;
        var darkIndex = Array.FindIndex(_themePickerEntries, static entry => entry.Slug == "default-dark");
        _themePicker = new ComboBox
        {
            Width = Length.Cells(26),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(1, 0),
            Items = Array.ConvertAll(_themePickerEntries, static entry => (object?) entry.Name),
            SelectedIndex = darkIndex >= 0 ? darkIndex : 0
        };
        _themePicker.SelectionChanged += OnThemeSelected;
        _quit = new Button { Text = "&Quit  Ctrl+Q" };
        _quit.Click += OnQuitClicked;
        Dock.SetSide(_pageFilter, DockSide.Top);
        sidebarLayout.Children.Add(_pageFilter);
        sidebarLayout.Children.Add(NavigationControl);
        Sidebar = new Dock
        {
            Width = Length.Cells(26),
            Border = new Border(
                BorderSide.Right,
                BorderGlyphStyle.Light,
                ThemeColor.ControlBorder,
                Color.Transparent,
                ThemeDecoration.Border),
            Children = { sidebarLayout }
        };

        // Quit is handled at the screen root in the preview pass so Ctrl+Q exits from anywhere
        // without stealing the standard TextInput copy chord.
        _ = AddHandler(Events.Key, OnGlobalKey);
        var workspace = new Dock
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        Dock.SetSide(Sidebar, DockSide.Left);
        workspace.Children.Add(Sidebar);
        workspace.Children.Add(_main);
        ApplicationBar = CreateApplicationBar(_themePicker, _quit);
        StatusBar = CreateStatusBar();
        var shell = new Dock
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { StatusBar, ApplicationBar, workspace }
        };
        Dock.SetSide(ApplicationBar, DockSide.Top);
        Dock.SetSide(StatusBar, DockSide.Bottom);
        InitializeContent(shell);
        Select(0);
    }

    /// <summary>Gets the keyboard- and pointer-enabled page navigation sidebar.</summary>
    public Dock Sidebar { get; }

    /// <summary>Gets the application-wide top bar containing product identity, theme selection, and Quit.</summary>
    internal Dock ApplicationBar { get; }

    /// <summary>Gets the application-wide bottom status bar containing keyboard hints.</summary>
    internal StatusBar StatusBar { get; }

    /// <summary>Gets the current documentation page content.</summary>
    public ControlBase CurrentPage => (_main.Children.Count > 0 ? _main.Children[0] : null)!;

    /// <summary>Gets the current exact concrete control name.</summary>
    public string SelectedPage => Pages[SelectedIndex];

    /// <summary>Gets the selected page's stable zero-based catalog index.</summary>
    internal int SelectedIndex { get; private set; }

    /// <summary>Gets the stable page names in catalog order.</summary>
    internal IReadOnlyList<string> Pages { get; }

    /// <summary>Gets the stable stateful navigation entries in catalog order.</summary>
    internal IReadOnlyList<NavigationViewItem> Navigation => _navigation;

    /// <summary>Gets the sidebar's single keyboard-focus owner.</summary>
    internal NavigationView NavigationControl { get; }

    /// <summary>Creates a fresh detached showcase pane for one catalog index.</summary>
    /// <param name="index">The zero-based page index.</param>
    /// <returns>A new showcase pane instance.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the catalog.</exception>
    internal static CompositeControlBase CreatePage(int index)
    {
        return (uint) index >= (uint) _catalog.Length
            ? throw new ArgumentOutOfRangeException(nameof(index), index, "The page index is outside the catalog.")
            : _catalog[index].Create();
    }

    /// <summary>Gets the catalog's group name for one page index.</summary>
    /// <param name="index">The zero-based page index.</param>
    /// <returns>The group the page renders under in the sidebar.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the catalog.</exception>
    internal static string CatalogGroup(int index)
    {
        return (uint) index >= (uint) _catalog.Length
            ? throw new ArgumentOutOfRangeException(nameof(index), index, "The page index is outside the catalog.")
            : _catalog[index].Group;
    }

    /// <summary>Gets every defined sidebar group name, in declaration order.</summary>
    internal static IReadOnlyList<string> CatalogGroups => _groups;

    /// <summary>Gets the total number of catalog pages without constructing a Gallery.</summary>
    internal static int CatalogCount => _catalog.Length;

    /// <summary>Gets the catalog's page name for one page index.</summary>
    /// <param name="index">The zero-based page index.</param>
    /// <returns>The name shown in the sidebar and used as the page's public title.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the catalog.</exception>
    internal static string CatalogName(int index)
    {
        return (uint) index >= (uint) _catalog.Length
            ? throw new ArgumentOutOfRangeException(nameof(index), index, "The page index is outside the catalog.")
            : _catalog[index].Name;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="application"/> is null.</exception>
    protected override void OnAttach(Application application)
    {
        ArgumentNullException.ThrowIfNull(application);
        application.Theme = Themes.Dark;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="application"/> is null.</exception>
    protected override void OnStarted(Application application)
    {
        ArgumentNullException.ThrowIfNull(application);
        _ = FocusSelected(application.Focus);
    }

    /// <summary>Focuses the public sidebar owner after the application has attached the gallery tree.</summary>
    /// <param name="focus">The non-null attached root focus manager.</param>
    /// <returns>True when the navigation owner accepted focus; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="focus"/> is null.</exception>
    internal bool FocusSelected(FocusManager focus)
    {
        ArgumentNullException.ThrowIfNull(focus);
        return focus.Focus(NavigationControl);
    }

    /// <summary>Selects one validated catalog page and replaces only the main documentation tree.</summary>
    /// <param name="index">The zero-based page index.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the catalog.</exception>
    internal void Select(int index)
    {
        if ((uint) index >= (uint) _catalog.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "The page index is outside the catalog.");
        }

        Debug.Assert(DocCaption.PlainCaption(_navigation[index].Text) == _catalog[index].Name);
        var previous = _main.Children.Count > 0 ? _main.Children[0] : null;
        SelectedIndex = index;
        NavigationControl.SelectItem(_navigation[index]);

        // A catalog selection creates a fresh page and therefore a fresh body viewport at offset zero.
        _main.Children.Clear();
        _main.Children.Add(_catalog[index].Create());

        previous?.Dispose();
    }

    private static Dock CreateApplicationBar(ComboBox themePicker, Button quit)
    {
        ArgumentNullException.ThrowIfNull(themePicker);
        ArgumentNullException.ThrowIfNull(quit);
        themePicker.VerticalAlignment = VerticalAlignment.Center;
        var themePickerHost = new Overlay
        {
            Width = themePicker.Width,
            Children = { themePicker }
        };
        quit.Height = Length.Cells(3);
        quit.VerticalAlignment = VerticalAlignment.Center;
        var actions = new Stack
        {
            Orientation = Orientation.Horizontal,
            Spacing = 1,
            Children = { themePickerHost, quit }
        };
        Dock.SetSide(actions, DockSide.Right);

        return new Dock
        {
            Height = Length.Cells(6),
            Padding = new Thickness(horizontal: 1, vertical: 1),
            Children =
            {
                actions,
                new FigletText(FigletCatalog.Default.Load("mini"))
                {
                    Content = "SharpVision",
                    Face = new Face(
                ThemeColor.ControlText,
                Color.Transparent,
                TerminalAttributes.Bold,
                Underline.None,
                Color.Default),
                    VerticalAlignment = VerticalAlignment.Center
                }
            }
        };
    }

    private static StatusBar CreateStatusBar()
    {
        var statusBar = new StatusBar
        {
            Padding = new Thickness(1, 0)
        };
        statusBar.Items.Add(new StatusBarItem
        {
            Content = new Text(
                "<info>↑↓</info> <d>Navigate</d>   " +
                "<info>Enter</info> <d>Open</d>   " +
                "<info>Alt+key</info> <d>Access</d>   " +
                "<info>Tab</info> <d>Move focus</d>")
        });
        return statusBar;
    }

    private void SetTheme(Theme theme)
    {
        if (Application is { } application)
        {
            application.Theme = theme;
        }
    }

    /// <summary>Applies the theme catalog entry chosen in the sidebar picker.</summary>
    private void OnThemeSelected(object? sender, ListSelectionChangedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        var index = _themePicker.SelectedIndex;

        if ((uint) index < (uint) _themePickerEntries.Length)
        {
            SetTheme(Themes.Load(_themePickerEntries[index].Slug));
        }
    }

    /// <summary>Filters page navigation by the committed case-insensitive query.</summary>
    private void OnPageFilterTextChanged(object? sender, TextChangedEventArgs eventArgs)
    {
        _ = sender;
        ArgumentNullException.ThrowIfNull(eventArgs);
        var query = eventArgs.Text.Trim();

        for (var index = 0; index < _navigation.Length; index++)
        {
            _navigation[index].Visibility = query.Length == 0 ||
                Pages[index].Contains(query, StringComparison.OrdinalIgnoreCase)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        for (var groupIndex = 0; groupIndex < _navigationGroups.Length; groupIndex++)
        {
            var groupName = _groups[groupIndex];
            var hasVisibleItem = false;

            for (var index = 0; index < _navigation.Length; index++)
            {
                if (_catalog[index].Group == groupName && _navigation[index].Visibility == Visibility.Visible)
                {
                    hasVisibleItem = true;
                    break;
                }
            }

            _navigationGroups[groupIndex].Visibility = hasVisibleItem
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }

    /// <summary>Requests cooperative application shutdown when the application-bar Quit button is activated.</summary>
    private void OnQuitClicked(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        RequestQuit();
    }

    /// <summary>Exits the showcase on a Ctrl+Q key press regardless of the focused control.</summary>
    private void OnGlobalKey(object? sender, KeyEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.Phase != RoutingPhase.Preview ||
            eventArgs.Handled ||
            eventArgs.Stroke.Action != KeyAction.Press ||
            (eventArgs.Stroke.Modifiers & Modifiers.Control) == 0 ||
            eventArgs.Stroke.Code != Code.Character ||
            eventArgs.Stroke.Character is not { } character ||
            Rune.ToLowerInvariant(character) != new Rune('q'))
        {
            return;
        }

        RequestQuit();
        eventArgs.Handled = true;
    }

    /// <summary>Drives graceful shutdown through the attached application's cooperative close path.</summary>
    private void RequestQuit() => Application?.Closed();

    private void OnNavigationSelectionChanged(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;

        if (NavigationControl.SelectedItem is { } selected)
        {
            var index = Array.IndexOf(_navigation, selected);

            if (index >= 0 && index != SelectedIndex)
            {
                Select(index);
            }
        }
    }

    /// <summary>Selects a grouped page when its retained navigation item is activated.</summary>
    private void OnNavigationItemInvoked(object? sender, ActivationEventArgs eventArgs)
    {
        _ = eventArgs;

        if (sender is NavigationViewItem selected)
        {
            var index = Array.IndexOf(_navigation, selected);

            if (index >= 0 && index != SelectedIndex)
            {
                Select(index);
            }
        }
    }

    /// <inheritdoc/>
    protected override void OnDispose()
    {
        NavigationControl.SelectionChanged -= OnNavigationSelectionChanged;

        foreach (var item in _navigation)
        {
            item.Invoked -= OnNavigationItemInvoked;
        }

        _pageFilter.TextChanged -= OnPageFilterTextChanged;
        _themePicker.SelectionChanged -= OnThemeSelected;
        _quit.Click -= OnQuitClicked;
    }

    #endregion
}
