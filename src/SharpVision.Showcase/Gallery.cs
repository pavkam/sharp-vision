// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase;

using Text = SharpVision.Controls.Text;

/// <summary>Builds the navigable traditional-control documentation gallery.</summary>
public sealed class Gallery: Screen
{
    private static readonly (string Name, Func<View> Create)[] _catalog =
    [
        (ButtonPane.Title, static () => new ButtonPane()),
        (CanvasPane.Title, static () => new CanvasPane()),
        (CheckBoxPane.Title, static () => new CheckBoxPane()),
        (ComboBoxPane.Title, static () => new ComboBoxPane()),
        (DockPane.Title, static () => new DockPane()),
        (FigletTextPane.Title, static () => new FigletTextPane()),
        (GridPane.Title, static () => new GridPane()),
        (ListPane.Title, static () => new ListPane()),
        (MenuPane.Title, static () => new MenuPane()),
        (OverlayPane.Title, static () => new OverlayPane()),
        (PopupPane.Title, static () => new PopupPane()),
        (RadioButtonPane.Title, static () => new RadioButtonPane()),
        (ScrollBarPane.Title, static () => new ScrollBarPane()),
        (StackPane.Title, static () => new StackPane()),
        (TablePane.Title, static () => new TablePane()),
        (TextPane.Title, static () => new TextPane()),
        (TextInputPane.Title, static () => new TextInputPane()),
        (WindowPane.Title, static () => new WindowPane()),
        (ThemingPane.Title, static () => new ThemingPane()),
    ];

    // Ordered theme catalog surfaced by the sidebar picker: every embedded theme from
    // SharpVision.Styling.ThemeCatalog.Default, dark group first then light, preserving each
    // group's catalog (order, slug) sequence. A new embedded theme JSON resource is one entry.
    private static readonly ThemeCatalogEntry[] _themePickerEntries = BuildThemePickerEntries();

    private static ThemeCatalogEntry[] BuildThemePickerEntries()
    {
        var entries = ThemeCatalog.Default.Entries;
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

    private readonly Stack _main;
    private readonly Stack _navigationScroll;
    private readonly NavigationItem[] _navigation;
    private readonly ComboBox _themePicker;
    private readonly Button _quit;
    private readonly Dock _root;
    private FocusManager? _focus;

    #region Construction and navigation

    /// <summary>Initializes the complete sidebar and first selected control page.</summary>
    public Gallery()
    {
        Pages = Array.ConvertAll(_catalog, static entry => entry.Name);
        _main = new Stack
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.WhenNeeded,
            ScrollBarChrome = ScrollBarChrome.Thin,
            ScrollBarFill = ScrollBarFill.Line,
            HorizontalBarVisibility = ScrollBarVisibility.Hidden,
        };
        _navigation = new NavigationItem[Pages.Count];
        var entries = new Stack() { Padding = new Thickness(1, 0) };
        entries.Children.Add(new Text("Components")
        {
            Attributes = TerminalAttributes.Bold,
        });

        for (var index = 0; index < Pages.Count; index++)
        {
            var item = new NavigationItem(index, Pages[index])
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            item.Invoked += OnNavigationInvoked;
            _navigation[index] = item;
            entries.Children.Add(item);
        }

        _navigationScroll = new Stack
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Both,
            ShowScrollBars = ShowScrollBars.WhenNeeded,
            ScrollBarChrome = ScrollBarChrome.Thin,
            ScrollBarFill = ScrollBarFill.Line,
            Children = { entries },
        };
        var sidebarLayout = new Dock();
        var header = CreateSidebarHeader();
        var darkIndex = Array.FindIndex(_themePickerEntries, static entry => entry.Slug == "default-dark");
        _themePicker = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Items = Array.ConvertAll(_themePickerEntries, static entry => (object?) entry.Name),
            SelectedIndex = darkIndex >= 0 ? darkIndex : 0,
        };
        _themePicker.SelectionChanged += OnThemeSelected;
        _quit = new Button { Content = new Text("Quit") };
        _quit.Click += OnQuitClicked;
        var footer = CreateSidebarFooter(_themePicker, _quit);
        Dock.SetSide(header, Side.Top);
        Dock.SetSide(footer, Side.Bottom);
        sidebarLayout.Children.Add(header);
        sidebarLayout.Children.Add(footer);
        sidebarLayout.Children.Add(_navigationScroll);
        Sidebar = new Dock
        {
            Width = Length.Cells(28),
            BorderThickness = new Thickness(1),
            BorderGlyphs = Glyphs.Rounded,
            Children = { sidebarLayout },
        };
        _ = Sidebar.AddHandler(Events.Key, OnNavigationKey);

        // Quit chords are handled at the screen root in the preview pass so Ctrl+C exits from
        // anywhere, including terminals whose Kitty keyboard protocol delivers it as a key event
        // rather than a host cancellation signal.
        _ = AddHandler(Events.Key, OnGlobalKey);
        var surface = new Dock()
        {
            Children = { _main },
        };
        var layout = new Dock()
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        Dock.SetSide(Sidebar, Side.Left);
        layout.Children.Add(Sidebar);
        layout.Children.Add(surface);
        _root = layout;
        Select(0);
    }

    /// <summary>Gets the framed keyboard- and pointer-enabled component navigation sidebar.</summary>
    public Dock Sidebar { get; }

    /// <summary>Gets the current documentation page content.</summary>
    /// <remarks>Deliberately hides <see cref="View.Content"/>: this is the selected showcase page,
    /// unrelated to the protected built-content root that <see cref="View"/> exposes to subclasses.</remarks>
    public new Control Content => (_main.Children.Count > 0 ? _main.Children[0] : null)!;

    /// <summary>Gets the current exact concrete control name.</summary>
    public string SelectedPage => Pages[SelectedIndex];

    /// <summary>Gets the selected page's stable zero-based catalog index.</summary>
    internal int SelectedIndex { get; private set; }

    /// <summary>Gets the stable page names in catalog order.</summary>
    internal IReadOnlyList<string> Pages { get; }

    /// <summary>Gets the stable stateful navigation entries in catalog order.</summary>
    internal IReadOnlyList<NavigationItem> Navigation => _navigation;

    /// <summary>Creates a fresh detached showcase pane for one catalog index.</summary>
    /// <param name="index">The zero-based page index.</param>
    /// <returns>A new showcase pane instance.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the catalog.</exception>
    internal static View CreatePage(int index)
    {
        return (uint) index >= (uint) _catalog.Length
            ? throw new ArgumentOutOfRangeException(nameof(index), index, "The page index is outside the catalog.")
            : _catalog[index].Create();
    }

    /// <inheritdoc/>
    protected override Control Build() => _root;

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
        if ((uint) index >= (uint) _catalog.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "The page index is outside the catalog.");
        }

        Debug.Assert(_navigation[index].Label == _catalog[index].Name);
        var previous = _main.Children.Count > 0 ? _main.Children[0] : null;
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

        _main.Children.Clear();
        _main.Children.Add(_catalog[index].Create());

        for (var navigationIndex = 0; navigationIndex < _navigation.Length; navigationIndex++)
        {
            _navigation[navigationIndex].SetSelected(navigationIndex == index);
        }

        previous?.Dispose();
    }

    private static Stack CreateSidebarHeader()
    {
        var header = new Stack()
        {
            Height = Length.Cells(4),
            Padding = new Thickness(1, 0),
        };
        header.Children.Add(new Text("SHARP VISION")
        {
            Attributes = TerminalAttributes.Bold,
        });
        header.Children.Add(new Text("Terminal UI toolkit")
        {
            Attributes = TerminalAttributes.Dim,
        });
        header.Children.Add(new Text("Control showcase"));
        return header;
    }

    private static Stack CreateSidebarFooter(ComboBox themePicker, Button quit)
    {
        var themeGroup = new Stack() { Spacing = 0 };
        themeGroup.Children.Add(new Text("Theme")
        {
            Attributes = TerminalAttributes.Dim,
        });
        themeGroup.Children.Add(themePicker);

        var exitGroup = new Stack() { Spacing = 0 };
        exitGroup.Children.Add(quit);
        exitGroup.Children.Add(new Text("Ctrl+C to quit")
        {
            Attributes = TerminalAttributes.Dim,
        });

        // No fixed height: the footer sizes to its content so the bordered Quit
        // button and the exit hint are never clipped on short terminals.
        return new Stack
        {
            Padding = new Thickness(1, 0),
            Spacing = 1,
            Children = { themeGroup, exitGroup },
        };
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
            SetTheme(ThemeCatalog.Default.Load(_themePickerEntries[index].Slug));
        }
    }

    /// <summary>Requests cooperative application shutdown when the sidebar Quit button is activated.</summary>
    private void OnQuitClicked(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        RequestQuit();
    }

    /// <summary>Exits the showcase on a Ctrl+C key press regardless of the focused control.</summary>
    private void OnGlobalKey(object? sender, KeyEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.Phase != Phase.Preview ||
            eventArgs.Handled ||
            eventArgs.Stroke.Action != KeyAction.Press ||
            (eventArgs.Stroke.Modifiers & Modifiers.Control) == 0 ||
            eventArgs.Stroke.Code != Code.Character ||
            eventArgs.Stroke.Character is not { } character ||
            Rune.ToLowerInvariant(character) != new Rune('c'))
        {
            return;
        }

        RequestQuit();
        eventArgs.Handled = true;
    }

    /// <summary>Drives graceful shutdown through the attached application's cooperative close path.</summary>
    private void RequestQuit() => Application?.Closed();

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

        var current = FindNavigation(eventArgs.OriginalSource) ?? _navigation[SelectedIndex];
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
        for (var current = source; current is not null; current = current.Parent)
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
        foreach (var item in _navigation)
        {
            item.Invoked -= OnNavigationInvoked;
        }

        _themePicker.SelectionChanged -= OnThemeSelected;
        _quit.Click -= OnQuitClicked;
        _focus = null;
    }

    #endregion
}
