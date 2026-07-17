// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase;

using SharpVision.Text;

using Text = SharpVision.Controls.Text;

/// <summary>Builds the navigable traditional-control documentation gallery.</summary>
public sealed class Gallery: Screen
{
    private static readonly (string Name, Func<CompositeControl> Create)[] _catalog =
    [
        (ButtonPane.Title, static () => new ButtonPane()),
        (CanvasPane.Title, static () => new CanvasPane()),
        (ChaseIndicatorPane.Title, static () => new ChaseIndicatorPane()),
        (CheckBoxPane.Title, static () => new CheckBoxPane()),
        (ColorPickerPane.Title, static () => new ColorPickerPane()),
        (ComboBoxPane.Title, static () => new ComboBoxPane()),
        (DockPane.Title, static () => new DockPane()),
        (ExpanderPane.Title, static () => new ExpanderPane()),
        (FigletTextPane.Title, static () => new FigletTextPane()),
        (GridPane.Title, static () => new GridPane()),
        (GroupBoxPane.Title, static () => new GroupBoxPane()),
        (ListPane.Title, static () => new ListPane()),
        (MenuPane.Title, static () => new MenuPane()),
        (NavigationViewPane.Title, static () => new NavigationViewPane()),
        (OverlayPane.Title, static () => new OverlayPane()),
        (PopupPane.Title, static () => new PopupPane()),
        (PrismPane.Title, static () => new PrismPane()),
        (ProgressBarPane.Title, static () => new ProgressBarPane()),
        (RadioButtonPane.Title, static () => new RadioButtonPane()),
        (ScrollBarPane.Title, static () => new ScrollBarPane()),
        (SeparatorPane.Title, static () => new SeparatorPane()),
        (SliderPane.Title, static () => new SliderPane()),
        (SpinnerPane.Title, static () => new SpinnerPane()),
        (StackPane.Title, static () => new StackPane()),
        (TabControlPane.Title, static () => new TabControlPane()),
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

    private readonly Dock _main;
    private readonly NavigationViewItem[] _navigation;
    private readonly ComboBox _themePicker;
    private readonly Button _quit;

    #region Construction and navigation

    /// <summary>Initializes the complete sidebar and first selected control page.</summary>
    public Gallery()
    {
        Pages = Array.ConvertAll(_catalog, static entry => entry.Name);
        _main = new Dock();
        NavigationControl = new NavigationView { Header = "Components" };
        _navigation = new NavigationViewItem[Pages.Count];

        for (var index = 0; index < Pages.Count; index++)
        {
            var item = new NavigationViewItem
            {
                Header = Pages[index],
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            _navigation[index] = item;
            NavigationControl.Items.Add(item);
        }

        NavigationControl.SelectionChanged += OnNavigationSelectionChanged;
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
        var quitHint = new Text("Ctrl+Q") { Attributes = TerminalAttributes.Dim };
        Dock.SetSide(quitHint, Side.Right);
        var quitContent = new Dock
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Children =
            {
                quitHint,
                new Text("⏻ Quit"),
            },
        };
        _quit = new Button
        {
            Content = quitContent,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        _quit.Click += OnQuitClicked;
        var footer = CreateSidebarFooter(_themePicker, _quit);
        Dock.SetSide(header, Side.Top);
        Dock.SetSide(footer, Side.Bottom);
        sidebarLayout.Children.Add(footer);
        sidebarLayout.Children.Add(header);
        sidebarLayout.Children.Add(NavigationControl);
        Sidebar = new Dock
        {
            Width = Length.Cells(28),
            BorderThickness = new Thickness(1),
            BorderGlyphs = Glyphs.Rounded,
            Children = { sidebarLayout },
        };

        // Quit is handled at the screen root in the preview pass so Ctrl+Q exits from anywhere
        // without stealing the standard TextInput copy chord.
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
        InitializeContent(layout);
        Select(0);
    }

    /// <summary>Gets the framed keyboard- and pointer-enabled component navigation sidebar.</summary>
    public Dock Sidebar { get; }

    /// <summary>Gets the current documentation page content.</summary>
    public Control CurrentPage => (_main.Children.Count > 0 ? _main.Children[0] : null)!;

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
    internal static CompositeControl CreatePage(int index)
    {
        return (uint) index >= (uint) _catalog.Length
            ? throw new ArgumentOutOfRangeException(nameof(index), index, "The page index is outside the catalog.")
            : _catalog[index].Create();
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

        Debug.Assert(_navigation[index].Header == _catalog[index].Name);
        var previous = _main.Children.Count > 0 ? _main.Children[0] : null;
        SelectedIndex = index;
        NavigationControl.SelectItem(_navigation[index]);

        // A catalog selection creates a fresh page and therefore a fresh body viewport at offset zero.
        _main.Children.Clear();
        _main.Children.Add(_catalog[index].Create());

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

    private static Dock CreateSidebarFooter(ComboBox themePicker, Button quit)
    {
        var appearance = new Text("<accent><b>🎨 Appearance</b></accent>")
        {
            Overflow = Overflow.Clip,
        };
        var utilities = new Stack
        {
            Padding = new Thickness(1, 0),
            Children =
            {
                appearance,
                themePicker,
                quit,
            },
        };

        return new Dock
        {
            BorderThickness = new Thickness(0, 1, 0, 0),
            BorderGlyphs = Glyphs.Light,
            Children = { utilities },
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

    /// <summary>Exits the showcase on a Ctrl+Q key press regardless of the focused control.</summary>
    private void OnGlobalKey(object? sender, KeyEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.Phase != Phase.Preview ||
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

    /// <inheritdoc/>
    protected override void OnDispose()
    {
        NavigationControl.SelectionChanged -= OnNavigationSelectionChanged;
        _themePicker.SelectionChanged -= OnThemeSelected;
        _quit.Click -= OnQuitClicked;
    }

    #endregion
}
