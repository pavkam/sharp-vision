// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;



/// <summary>Proves the shared mounted box model and intrinsic frame for every exported concrete control.</summary>
public sealed class ComponentGeometrySurfaceTests
{
    private static readonly Type[] _controlTypes =
    [
        typeof(AreaChart),
        typeof(Button),
        typeof(UiCalendar),
        typeof(ChaseIndicator),
        typeof(CheckBox),
        typeof(ColorPicker),
        typeof(ComboBox),
        typeof(CurrencyInput),
        typeof(DateInput),
        typeof(DateTimeInput),
        typeof(Dock),
        typeof(Document),
        typeof(Expander),
        typeof(FigletText),
        typeof(FilePickerDialog),
        typeof(Flyout),
        typeof(Grid),
        typeof(GroupBox),
        typeof(HorizontalBarChart),
        typeof(HyperlinkButton),
        typeof(Image),
        typeof(JsonView),
        typeof(LineChart),
        typeof(UiListView),
        typeof(Menu),
        typeof(MenuItem),
        typeof(MenuSeparator),
        typeof(MessageBox),
        typeof(NavigationView),
        typeof(NavigationViewGroup),
        typeof(NavigationViewItem),
        typeof(NavigationViewSeparator),
        typeof(NumberInput),
        typeof(Overlay),
        typeof(Popup),
        typeof(Prism),
        typeof(ProgressBar),
        typeof(RadioButton),
        typeof(SaveFileDialog),
        typeof(ScrollBar),
        typeof(Separator),
        typeof(Slider),
        typeof(Sparkline),
        typeof(Spinner),
        typeof(Stack),
        typeof(StatusBar),
        typeof(StatusBarItem),
        typeof(TabControl),
        typeof(TabItem),
        typeof(Table),
        typeof(ControlText),
        typeof(TextInput),
        typeof(TimeInput),
        typeof(Tooltip),
        typeof(TreeView),
        typeof(TreeViewItem),
        typeof(VerticalBarChart),
        typeof(Window)
    ];

    /// <summary>Gets every exported concrete control type in deterministic name order.</summary>
    public static TheoryData<Type> ControlTypes => [.. _controlTypes];

    /// <summary>Verifies the matrix remains identical to the exported concrete control inventory.</summary>
    [Fact]
    public void Catalog_WhenPublicConcreteControlsChange_RequiresMountedGeometryCase()
    {
        var exported = new[] { typeof(ControlBase).Assembly, typeof(Document).Assembly }
            .SelectMany(assembly => assembly.GetExportedTypes())
            .Where(type => !type.IsAbstract && typeof(ControlBase).IsAssignableFrom(type))
            .OrderBy(type => type.Name)
            .ToArray();
        var catalogued = _controlTypes.OrderBy(type => type.Name).ToArray();

        catalogued.ShouldBe(exported);
    }

    /// <summary>Verifies protected intrinsic chrome participates in the shared mounted box model.</summary>
    [Fact]
    public async Task Render_WhenBoxModelIsApplied_UsesExactBoundsContentAndFrameAsync()
    {
        // Arrange
        var control = new ChromeProbe
        {
            Border = AppearanceTestValues.Border(BorderSide.All, BorderGlyphStyle.Light),
            Width = Length.Cells(10),
            Height = Length.Cells(5),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(1),
            Padding = new Thickness(1)
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            control,
            new Size(14, 9),
            TestContext.Current.CancellationToken);

        // Assert
        control.Bounds.ShouldBe(new Rect(1, 1, 10, 5));
        control.ContentBounds.ShouldBe(new Rect(3, 3, 6, 1));
        surface.Cell(new Point(1, 1)).Text.ShouldBe(BorderGlyphStyle.Light.TopLeft.ToString());
    }

    /// <summary>Verifies resize and tiny bounds preserve box-model geometry and complete frame cells.</summary>
    [Fact]
    public async Task ResizeAsync_WhenBoxModelStretches_PreservesInsetsAndTinyFrameAsync()
    {
        // Arrange
        var control = new ChromeProbe
        {
            Border = AppearanceTestValues.Border(BorderSide.All, BorderGlyphStyle.Light),
            Width = Length.Percent(100),
            Height = Length.Percent(100),
            Margin = new Thickness(1),
            Padding = new Thickness(1)
        };
        await using var surface = await ComponentSurface.MountAsync(
            control,
            new Size(14, 9),
            TestContext.Current.CancellationToken);

        // Assert initial geometry
        control.Bounds.ShouldBe(new Rect(1, 1, 12, 7));
        control.ContentBounds.ShouldBe(new Rect(3, 3, 8, 3));

        // Act larger resize
        await surface.ResizeAsync(new Size(18, 11));

        // Assert committed larger geometry and frame
        control.Bounds.ShouldBe(new Rect(1, 1, 16, 9));
        control.ContentBounds.ShouldBe(new Rect(3, 3, 12, 5));
        surface.Cell(new Point(1, 1)).Text.ShouldBe(BorderGlyphStyle.Light.TopLeft.ToString());

        // Act tiny resize
        await surface.ResizeAsync(new Size(5, 5));

        // Assert saturated content and intact corner
        control.Bounds.ShouldBe(new Rect(1, 1, 3, 3));
        control.ContentBounds.Width.ShouldBe(0);
        control.ContentBounds.Height.ShouldBe(0);
        surface.Cell(new Point(1, 1)).Text.ShouldBe(BorderGlyphStyle.Light.TopLeft.ToString());
    }

}
