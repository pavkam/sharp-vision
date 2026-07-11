using System.Text;

using SharpVision.Controls;
using SharpVision.Fonts;
using SharpVision.Input;
using SharpVision.Layout;
using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Protocols;
using SharpVision.Terminal.Rendering;

using ControlList = SharpVision.Controls.List;
using ControlText = SharpVision.Controls.Text;
using TextRun = SharpVision.Controls.Run;
using Wrapping = SharpVision.Text.Wrapping;

namespace SharpVision.Showcase;

/// <summary>Builds the navigable traditional-control showcase tree.</summary>
public sealed class Gallery: IDisposable
{
    private static readonly string[] _pages =
    [
        "Borders & Shadows",
        "Typography",
        "Buttons & Selection",
        "Inputs & Lists",
        "Layout & Scrolling",
    ];

    private readonly ScrollView _main;

    #region Construction and navigation

    /// <summary>Initializes the complete sidebar and first selected page.</summary>
    public Gallery()
    {
        Sidebar = new ControlList
        {
            Width = Length.Cells(24),
            Items = _pages,
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
        Sidebar.SelectedIndex = 0;
    }

    /// <summary>Gets the root control passed directly to the application runtime.</summary>
    public Dock Root { get; }

    /// <summary>Gets the selectable, keyboard- and pointer-enabled page sidebar.</summary>
    public ControlList Sidebar { get; }

    /// <summary>Gets the current page content.</summary>
    public Control Content => _main.Content!;

    /// <summary>Gets the current exact page title.</summary>
    public string SelectedPage { get; private set; } = string.Empty;

    private void OnSelectionChanged(object? sender, ListSelectionChangedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        var index = Sidebar.SelectedIndex;

        if ((uint) index >= (uint) _pages.Length)
        {
            return;
        }

        SelectedPage = _pages[index];
        _main.Content = CreatePage(SelectedPage);
    }

    #endregion

    #region Pages

    /// <summary>Releases the detached or application-owned control tree.</summary>
    public void Dispose()
    {
        Sidebar.SelectionChanged -= OnSelectionChanged;
        Root.Dispose();
        GC.SuppressFinalize(this);
    }

    private static Stack CreatePage(string name) => name switch
    {
        "Borders & Shadows" => CreateBordersPage(),
        "Typography" => CreateTypographyPage(),
        "Buttons & Selection" => CreateSelectionPage(),
        "Inputs & Lists" => CreateInputPage(),
        "Layout & Scrolling" => CreateLayoutPage(),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "The showcase page is unknown."),
    };

    private static Stack CreateBordersPage()
    {
        var page = Page(
            "Borders & Shadows",
            "Unicode light, heavy, paired-line, rounded, ASCII, block, and shade borders. " +
            "The final cards demonstrate composite and block-glyph shadows.");
        AddBorder(page, "Light", Glyphs.Light);
        AddBorder(page, "Heavy", Glyphs.Heavy);
        AddBorder(page, "Paired", Glyphs.Paired);
        AddBorder(page, "Rounded", Glyphs.Rounded);
        AddBorder(page, "ASCII", Glyphs.Ascii);
        AddBorder(page, "Solid", Glyphs.Solid);
        AddBorder(page, "Light shade", Glyphs.LightShade);
        AddBorder(page, "Medium shade", Glyphs.MediumShade);
        AddBorder(page, "Dark shade", Glyphs.DarkShade);
        page.Children.Add(new CanvasSample());
        page.Children.Add(new Shadow
        {
            Child = Card("Composite shadow"),
            Background = Color.Indexed(0),
            Offset = new Point(2, 1),
        });
        page.Children.Add(new Shadow
        {
            Child = Card("Block-glyph shadow"),
            Mode = ShadowMode.BlockGlyph,
            Glyph = new Rune('▓'),
            Offset = new Point(2, 1),
        });
        return page;
    }

    private static Stack CreateTypographyPage()
    {
        var page = Page(
            "Typography",
            "Text, typed RichText runs and hyperlinks, and lazily loaded FIGlet fonts all " +
            "render through the same grapheme-safe canvas.");
        page.Children.Add(new ControlText("Plain Unicode: café · 你好 · 👩‍💻"));
        var rich = new RichText { Wrapping = Wrapping.Word };
        rich.Inlines.Add(new TextRun("Styled ") { Foreground = Color.Indexed(2) });
        rich.Inlines.Add(new TextRun("runs") { Attributes = Attributes.Bold });
        rich.Inlines.Add(new TextRun(" and "));
        rich.Inlines.Add(new Hyperlink("semantic links", "https://github.com/pavkam"));
        page.Children.Add(rich);
        page.Children.Add(new FigletText(FigletCatalog.Default.Load("Standard"))
        {
            Content = "SharpVision",
        });
        return page;
    }

    private static Stack CreateSelectionPage()
    {
        var page = Page(
            "Buttons & Selection",
            "Focusable controls expose normal, hovered, pressed, focused, checked, and " +
            "disabled behavior through ordinary mutable properties and events.");
        page.Children.Add(new Button { Content = new ControlText("Enabled button") });
        page.Children.Add(new Button
        {
            Content = new ControlText("Disabled button"),
            IsEnabled = false,
        });
        page.Children.Add(new CheckBox { Content = new ControlText("Unchecked") });
        page.Children.Add(new CheckBox
        {
            Content = new ControlText("Checked"),
            IsChecked = true,
        });
        page.Children.Add(new RadioButton
        {
            Content = new ControlText("Radio A"),
            GroupName = "showcase",
            IsChecked = true,
        });
        page.Children.Add(new RadioButton
        {
            Content = new ControlText("Radio B"),
            GroupName = "showcase",
        });
        return page;
    }

    private static Stack CreateInputPage()
    {
        var page = Page(
            "Inputs & Lists",
            "Text input, realized lists, automatic scrollbars, keyboard navigation, and pointer " +
            "selection are composed from the public controls shown here.");
        page.Children.Add(new TextInput { Text = "Edit me" });
        page.Children.Add(new ControlList
        {
            Height = Length.Cells(5),
            Items = new object?[] { "Alpha", "Beta", "Gamma", "Delta", "Epsilon", "Zeta" },
        });
        return page;
    }

    private static Stack CreateLayoutPage()
    {
        var page = Page(
            "Layout & Scrolling",
            "Fixed, automatic, percentage, and proportional sizes combine with margin, padding, " +
            "docking, grids, overlays, canvases, and automatic scrollbars.");
        var row = new Stack { Orientation = Orientation.Horizontal, Spacing = 1 };
        var fixedCard = Card("Fixed 12");
        fixedCard.Width = Length.Cells(12);
        row.Children.Add(fixedCard);
        var percentCard = Card("50 percent");
        percentCard.Width = Length.Percent(50);
        row.Children.Add(percentCard);
        page.Children.Add(row);
        var scroll = new ScrollView
        {
            Height = Length.Cells(5),
            HorizontalBarVisibility = ScrollBarVisibility.Auto,
            VerticalBarVisibility = ScrollBarVisibility.Auto,
        };
        var content = new Stack();

        for (var index = 1; index <= 12; index++)
        {
            content.Children.Add(new ControlText($"Scrollable row {index:00}"));
        }

        scroll.Content = content;
        page.Children.Add(scroll);
        return page;
    }

    #endregion

    private static Stack Page(string title, string description)
    {
        var page = new Stack { Spacing = 1, Padding = new Thickness(1) };
        var docs = new RichText { Wrapping = Wrapping.Word };
        docs.Inlines.Add(new TextRun(title) { Attributes = Attributes.Bold });
        docs.Inlines.Add(new LineBreak());
        docs.Inlines.Add(new TextRun(description));
        page.Children.Add(docs);
        return page;
    }

    private static Border Card(string text) => new()
    {
        Child = new ControlText(text),
        BorderThickness = new Thickness(1),
        Padding = new Thickness(1, 0, 1, 0),
        Glyphs = Glyphs.Rounded,
    };

    private static void AddBorder(Stack page, string name, Glyphs glyphs)
    {
        var border = Card(name);
        border.Glyphs = glyphs;
        page.Children.Add(border);
    }
}
