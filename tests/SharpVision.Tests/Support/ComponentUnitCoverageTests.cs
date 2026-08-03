// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

using Controls.Collections;
using Controls.Display;

using Dialogs;

using Menus;

using Navigation;

using Popups;

using SharpVision.Tests.Controls.Input;
using SharpVision.Tests.Controls.Layout;
using SharpVision.Tests.Controls.Scrolling;

using Windows;

using UiCalendar = SharpVision.Controls.Input.Calendar;
using UiText = ControlText;

/// <summary>Guards focused unit-fixture ownership for every public concrete control.</summary>
public sealed class ComponentUnitCoverageTests
{
    // An index initializer ([key] = value) calls the Dictionary indexer setter, which silently
    // overwrites a duplicate key instead of failing. A duplicate control mapping here would
    // silently drop the earlier fixture from the catalog with no signal. An Add-based initializer
    // ({ key, value }) calls Dictionary.Add instead, which throws ArgumentException on a duplicate
    // key, so a catalog mistake fails the moment this type initializes (see #15).
    private static readonly Dictionary<Type, Type> _fixtures = new()
    {
        { typeof(UiText), typeof(TextTests) },
        { typeof(ChaseIndicator), typeof(ChaseIndicatorTests) },
        { typeof(FigletText), typeof(FigletTextTests) },
        { typeof(Image), typeof(ImageTests) },
        { typeof(Prism), typeof(PrismTests) },
        { typeof(ProgressBar), typeof(ProgressBarTests) },
        { typeof(Spinner), typeof(SpinnerTests) },
        { typeof(Separator), typeof(SeparatorTests) },
        { typeof(Dock), typeof(DockTests) },
        { typeof(Grid), typeof(GridTests) },
        { typeof(Overlay), typeof(OverlayTests) },
        { typeof(Stack), typeof(StackTests) },
        { typeof(GroupBox), typeof(GroupBoxTests) },
        { typeof(Expander), typeof(ExpanderTests) },
        { typeof(Button), typeof(ButtonTests) },
        { typeof(CheckBox), typeof(CheckBoxTests) },
        { typeof(RadioButton), typeof(RadioButtonTests) },
        { typeof(TextInput), typeof(TextInputTests) },
        { typeof(ComboBox), typeof(ComboBoxTests) },
        { typeof(ScrollBar), typeof(ScrollBarTests) },
        { typeof(Slider), typeof(SliderTests) },
        { typeof(UiCalendar), typeof(CalendarTests) },
        { typeof(DateInput), typeof(DateInputTests) },
        { typeof(DateTimeInput), typeof(DateTimeInputTests) },
        { typeof(TimeInput), typeof(TimeInputTests) },
        { typeof(ColorPicker), typeof(ColorPickerTests) },
        { typeof(ListView), typeof(ListViewTests) },
        { typeof(Table), typeof(TableTests) },
        { typeof(TabControl), typeof(TabControlTests) },
        { typeof(TabItem), typeof(TabItemTests) },
        { typeof(NavigationView), typeof(NavigationViewTests) },
        { typeof(NavigationViewGroup), typeof(NavigationViewTests) },
        { typeof(NavigationViewItem), typeof(NavigationViewTests) },
        { typeof(NavigationViewSeparator), typeof(NavigationViewTests) },
        { typeof(TreeView), typeof(TreeViewTests) },
        { typeof(TreeViewItem), typeof(TreeViewTests) },
        { typeof(Menu), typeof(MenuTests) },
        { typeof(MenuItem), typeof(MenuTests) },
        { typeof(MenuSeparator), typeof(MenuTests) },
        { typeof(StatusBar), typeof(StatusBarTests) },
        { typeof(StatusBarItem), typeof(StatusBarTests) },
        { typeof(Popup), typeof(PopupTests) },
        { typeof(Flyout), typeof(FlyoutTests) },
        { typeof(Tooltip), typeof(TooltipTests) },
        { typeof(HyperlinkButton), typeof(HyperlinkButtonTests) },
        { typeof(Window), typeof(WindowTests) },
        { typeof(MessageBox), typeof(MessageBoxTests) },
        { typeof(FilePickerDialog), typeof(FilePickerDialogTests) },
        { typeof(SaveFileDialog), typeof(SaveFileDialogTests) }
    };

    /// <summary>Verifies every exported concrete control has exactly one named focused unit fixture.</summary>
    [Fact]
    public void Catalog_WhenPublicConcreteControlsChange_RequiresFocusedUnitFixture()
    {
        var controls = typeof(ControlBase).Assembly.GetExportedTypes()
            .Where(type => !type.IsAbstract && typeof(ControlBase).IsAssignableFrom(type))
            .ToHashSet();

        _fixtures.Keys.ShouldBe(controls, ignoreOrder: true);
        _fixtures.Values.ShouldAllBe(fixture => fixture.IsClass && !fixture.IsAbstract);
    }

    /// <summary>Verifies every catalogued fixture contains executable xUnit contract methods.</summary>
    [Fact]
    public void Fixtures_WhenCatalogued_ContainExecutableContractTests()
    {
        foreach (var (control, fixture) in _fixtures)
        {
            var hasContractTest = fixture.GetMethods()
                .Any(method => method.GetCustomAttributes(inherit: false)
                    .Any(attribute => attribute is FactAttribute or TheoryAttribute));

            hasContractTest.ShouldBeTrue(
                $"{control.Name} fixture {fixture.Name} must contain an executable contract test.");
        }
    }

    /// <summary>Verifies every catalogued fixture proves it actually exercises its mapped control,
    /// rather than merely containing some unrelated executable test (see #15).</summary>
    [Fact]
    public void Fixtures_WhenCatalogued_ContainEvidenceForTheMappedControl()
    {
        foreach (var (control, fixture) in _fixtures)
        {
            var hasEvidence = fixture.GetMethods()
                .SelectMany(method => method.GetCustomAttributes(inherit: false))
                .OfType<ComponentUnitEvidenceAttribute>()
                .Any(evidence => evidence.ControlType == control);

            hasEvidence.ShouldBeTrue(
                $"{control.Name} fixture {fixture.Name} must declare " +
                $"[ComponentUnitEvidence(typeof({control.Name}))] on at least one contract test.");
        }
    }
}
