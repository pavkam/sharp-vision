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

/// <summary>Guards every public concrete control and its explicit mounted behavior requirements.</summary>
public sealed class ComponentSurfaceCoverageTests
{
    private const ComponentBehavior _passive = ComponentBehavior.Mounted |
                                               ComponentBehavior.FocusExcluded |
                                               ComponentBehavior.TabExcluded |
                                               ComponentBehavior.DirectionalExcluded |
                                               ComponentBehavior.PressReleaseExcluded;

    private const ComponentBehavior _interactive = ComponentBehavior.Mounted |
                                                   ComponentBehavior.Hover |
                                                   ComponentBehavior.Focus |
                                                   ComponentBehavior.Tab;

    private const ComponentBehavior _ownedPressFace = ComponentBehavior.Mounted |
                                                      ComponentBehavior.Hover |
                                                      ComponentBehavior.FocusExcluded |
                                                      ComponentBehavior.TabExcluded |
                                                      ComponentBehavior.DirectionalExcluded;

    // An index initializer ([key] = value) calls the Dictionary indexer setter, which silently
    // overwrites a duplicate key instead of failing — the exact mechanism that let a duplicate
    // SaveFileDialog entry collapse here undetected. An Add-based initializer ({ key, value })
    // calls Dictionary.Add instead, which throws ArgumentException on a duplicate key, so a
    // catalog mistake fails the moment this type initializes (see #15).
    private static readonly Dictionary<Type, ComponentBehaviorRequirement> _requirements = new()
    {
        { typeof(UiText), Requirement<TextSurfaceTests>(_passive | ComponentBehavior.Hover) },
        { typeof(ChaseIndicator), Requirement<ChaseIndicatorSurfaceTests>(_passive | ComponentBehavior.HoverExcluded) },
        { typeof(FigletText), Requirement<FigletTextSurfaceTests>(_passive | ComponentBehavior.Hover) },
        { typeof(Image), Requirement<ImageSurfaceTests>(_passive | ComponentBehavior.Hover) },
        { typeof(Prism),
            Requirement<PrismSurfaceTests>(_passive | ComponentBehavior.Hover | ComponentBehavior.Composition) },
        { typeof(ProgressBar), Requirement<ProgressBarSurfaceTests>(_passive | ComponentBehavior.HoverExcluded) },
        { typeof(Spinner), Requirement<SpinnerSurfaceTests>(_passive | ComponentBehavior.HoverExcluded) },
        { typeof(Separator), Requirement<SeparatorSurfaceTests>(_passive | ComponentBehavior.HoverExcluded) },
        { typeof(Dock),
            Requirement<DockSurfaceTests>(_passive | ComponentBehavior.Hover | ComponentBehavior.Composition) },
        { typeof(Grid),
            Requirement<GridSurfaceTests>(_passive | ComponentBehavior.Hover | ComponentBehavior.Composition) },
        { typeof(Overlay),
            Requirement<OverlaySurfaceTests>(_passive | ComponentBehavior.Hover | ComponentBehavior.Composition) },
        { typeof(Stack),
            Requirement<StackSurfaceTests>(_passive | ComponentBehavior.Hover | ComponentBehavior.Composition) },
        { typeof(GroupBox),
            Requirement<GroupBoxSurfaceTests>(_passive | ComponentBehavior.Hover | ComponentBehavior.Composition) },
        { typeof(Expander), Requirement<ExpanderSurfaceTests>(
            _interactive |
            ComponentBehavior.DirectionalExcluded |
            ComponentBehavior.PressRelease |
            ComponentBehavior.Activation |
            ComponentBehavior.PointerActivation |
            ComponentBehavior.KeyboardActivation |
            ComponentBehavior.UnavailableCleanup |
            ComponentBehavior.Composition) },
        { typeof(Button), Requirement<ButtonSurfaceTests>(
            _interactive |
            ComponentBehavior.DirectionalExcluded |
            ComponentBehavior.PressRelease |
            ComponentBehavior.Activation |
            ComponentBehavior.PointerActivation |
            ComponentBehavior.KeyboardActivation |
            ComponentBehavior.PressedFrame |
            ComponentBehavior.UnavailableCleanup) },
        { typeof(CheckBox), Requirement<CheckBoxSurfaceTests>(
            _interactive |
            ComponentBehavior.DirectionalExcluded |
            ComponentBehavior.PressRelease |
            ComponentBehavior.Activation |
            ComponentBehavior.PointerActivation |
            ComponentBehavior.KeyboardActivation |
            ComponentBehavior.UnavailableCleanup) },
        { typeof(RadioButton), Requirement<RadioButtonSurfaceTests>(
            _interactive |
            ComponentBehavior.Directional |
            ComponentBehavior.PressRelease |
            ComponentBehavior.Activation |
            ComponentBehavior.PointerActivation |
            ComponentBehavior.KeyboardActivation |
            ComponentBehavior.UnavailableCleanup) },
        { typeof(TextInput), Requirement<TextInputSurfaceTests>(
            _interactive |
            ComponentBehavior.Directional |
            ComponentBehavior.PressReleaseExcluded |
            ComponentBehavior.Activation |
            ComponentBehavior.PointerActivation |
            ComponentBehavior.KeyboardActivation |
            ComponentBehavior.UnavailableCleanup) },
        { typeof(ComboBox), Requirement<ComboBoxSurfaceTests>(
            _interactive |
            ComponentBehavior.Directional |
            ComponentBehavior.PressRelease |
            ComponentBehavior.Activation |
            ComponentBehavior.PointerActivation |
            ComponentBehavior.KeyboardActivation |
            ComponentBehavior.UnavailableCleanup |
            ComponentBehavior.Transient |
            ComponentBehavior.Composition) },
        { typeof(ScrollBar), Requirement<ScrollBarSurfaceTests>(
            _interactive |
            ComponentBehavior.Directional |
            ComponentBehavior.PressRelease |
            ComponentBehavior.Activation |
            ComponentBehavior.PointerActivation |
            ComponentBehavior.KeyboardActivation |
            ComponentBehavior.UnavailableCleanup) },
        { typeof(Slider), Requirement<SliderSurfaceTests>(
            _interactive |
            ComponentBehavior.Directional |
            ComponentBehavior.PressRelease |
            ComponentBehavior.Activation |
            ComponentBehavior.PointerActivation |
            ComponentBehavior.KeyboardActivation |
            ComponentBehavior.UnavailableCleanup) },
        { typeof(UiCalendar), Requirement<CalendarSurfaceTests>(
            _interactive |
            ComponentBehavior.Directional |
            ComponentBehavior.PressReleaseExcluded |
            ComponentBehavior.Activation |
            ComponentBehavior.PointerActivation |
            ComponentBehavior.KeyboardActivation |
            ComponentBehavior.UnavailableCleanup) },
        { typeof(DateInput), Requirement<DateInputSurfaceTests>(
            _interactive |
            ComponentBehavior.DirectionalExcluded |
            ComponentBehavior.PressReleaseExcluded) },
        { typeof(DateTimeInput), Requirement<DateTimeInputSurfaceTests>(
            _interactive |
            ComponentBehavior.DirectionalExcluded |
            ComponentBehavior.PressReleaseExcluded) },
        { typeof(TimeInput), Requirement<TimeInputSurfaceTests>(
            _interactive |
            ComponentBehavior.DirectionalExcluded |
            ComponentBehavior.PressReleaseExcluded) },
        { typeof(ColorPicker), Requirement<ColorPickerSurfaceTests>(
            _passive |
            ComponentBehavior.Hover |
            ComponentBehavior.Activation |
            ComponentBehavior.PointerActivation |
            ComponentBehavior.RetainedPointerActivation |
            ComponentBehavior.UnavailableCleanup |
            ComponentBehavior.Composition) },
        { typeof(ListView), Requirement<ListViewSurfaceTests>(
            _interactive |
            ComponentBehavior.Directional |
            ComponentBehavior.PressReleaseExcluded |
            ComponentBehavior.Activation |
            ComponentBehavior.PointerActivation |
            ComponentBehavior.KeyboardActivation |
            ComponentBehavior.RetainedPointerActivation |
            ComponentBehavior.UnavailableCleanup |
            ComponentBehavior.Composition) },
        { typeof(Table), Requirement<TableSurfaceTests>(
            _interactive |
            ComponentBehavior.Directional |
            ComponentBehavior.PressReleaseExcluded |
            ComponentBehavior.Activation |
            ComponentBehavior.PointerActivation |
            ComponentBehavior.KeyboardActivation |
            ComponentBehavior.Composition) },
        { typeof(TabControl), Requirement<TabBehaviorSurfaceTests>(
            _interactive |
            ComponentBehavior.Directional |
            ComponentBehavior.PressReleaseExcluded |
            ComponentBehavior.Activation |
            ComponentBehavior.PointerActivation |
            ComponentBehavior.KeyboardActivation |
            ComponentBehavior.RetainedPointerActivation |
            ComponentBehavior.UnavailableCleanup |
            ComponentBehavior.Composition) },
        { typeof(TabItem),
            Requirement<TabBehaviorSurfaceTests>(_passive | ComponentBehavior.Hover | ComponentBehavior.Composition) },
        { typeof(NavigationView), Requirement<NavigationViewSurfaceTests>(
            _interactive |
            ComponentBehavior.Directional |
            ComponentBehavior.PressReleaseExcluded |
            ComponentBehavior.Activation |
            ComponentBehavior.PointerActivation |
            ComponentBehavior.KeyboardActivation |
            ComponentBehavior.RetainedPointerActivation |
            ComponentBehavior.UnavailableCleanup |
            ComponentBehavior.Composition) },
        { typeof(NavigationViewGroup), Requirement<NavigationViewSurfaceTests>(
            _passive | ComponentBehavior.Hover | ComponentBehavior.Composition) },
        { typeof(NavigationViewItem), Requirement<NavigationViewSurfaceTests>(
            _ownedPressFace |
            ComponentBehavior.PressRelease |
            ComponentBehavior.Activation |
            ComponentBehavior.PointerActivation |
            ComponentBehavior.UnavailableCleanup) },
        { typeof(NavigationViewSeparator), Requirement<NavigationViewSurfaceTests>(
            _passive | ComponentBehavior.HoverExcluded) },
        { typeof(TreeView), Requirement<TreeViewSurfaceTests>(
            _interactive |
            ComponentBehavior.Directional |
            ComponentBehavior.PressReleaseExcluded |
            ComponentBehavior.Activation |
            ComponentBehavior.PointerActivation |
            ComponentBehavior.KeyboardActivation |
            ComponentBehavior.UnavailableCleanup |
            ComponentBehavior.Composition) },
        { typeof(TreeViewItem), Requirement<TreeViewSurfaceTests>(
            _passive |
            ComponentBehavior.Hover |
            ComponentBehavior.Activation |
            ComponentBehavior.PointerActivation |
            ComponentBehavior.UnavailableCleanup) },
        { typeof(Menu), Requirement<MenuSurfaceTests>(
            _interactive |
            ComponentBehavior.Directional |
            ComponentBehavior.PressReleaseExcluded |
            ComponentBehavior.Activation |
            ComponentBehavior.PointerActivation |
            ComponentBehavior.KeyboardActivation |
            ComponentBehavior.RetainedPointerActivation |
            ComponentBehavior.UnavailableCleanup |
            ComponentBehavior.Composition) },
        { typeof(MenuItem), Requirement<MenuSurfaceTests>(
            _ownedPressFace |
            ComponentBehavior.PressRelease |
            ComponentBehavior.Activation |
            ComponentBehavior.PointerActivation |
            ComponentBehavior.UnavailableCleanup) },
        { typeof(MenuSeparator), Requirement<MenuSurfaceTests>(_passive | ComponentBehavior.HoverExcluded) },
        { typeof(StatusBar), Requirement<StatusBarSurfaceTests>(
            _passive | ComponentBehavior.Hover | ComponentBehavior.Composition) },
        { typeof(StatusBarItem), Requirement<StatusBarSurfaceTests>(
            _passive | ComponentBehavior.Hover | ComponentBehavior.Composition) },
        { typeof(Popup), Requirement<PopupSurfaceTests>(
            _passive | ComponentBehavior.Hover | ComponentBehavior.Transient | ComponentBehavior.Composition) },
        { typeof(ContextMenu), Requirement<ContextMenuSurfaceTests>(
            _passive | ComponentBehavior.Hover | ComponentBehavior.Transient | ComponentBehavior.Composition) },
        { typeof(TextInputContextMenu), Requirement<ContextMenuSurfaceTests>(
            _passive | ComponentBehavior.Hover | ComponentBehavior.Transient | ComponentBehavior.Composition) },
        { typeof(Flyout), Requirement<PopupSurfaceTests>(
            _passive | ComponentBehavior.Hover | ComponentBehavior.Transient | ComponentBehavior.Composition) },
        { typeof(Tooltip), Requirement<PopupSurfaceTests>(
            _passive | ComponentBehavior.HoverExcluded | ComponentBehavior.Transient | ComponentBehavior.Composition) },
        { typeof(HyperlinkButton), Requirement<HyperlinkButtonSurfaceTests>(
            _interactive |
            ComponentBehavior.DirectionalExcluded |
            ComponentBehavior.PressRelease |
            ComponentBehavior.Activation |
            ComponentBehavior.PointerActivation |
            ComponentBehavior.KeyboardActivation |
            ComponentBehavior.UnavailableCleanup) },
        { typeof(Window), Requirement<WindowSurfaceTests>(
            _interactive |
            ComponentBehavior.DirectionalExcluded |
            ComponentBehavior.PressReleaseExcluded |
            ComponentBehavior.Activation |
            ComponentBehavior.PointerActivation |
            ComponentBehavior.KeyboardActivation |
            ComponentBehavior.UnavailableCleanup |
            ComponentBehavior.Composition) },
        { typeof(MessageBox), Requirement<MessageBoxTests>(
            _interactive |
            ComponentBehavior.DirectionalExcluded |
            ComponentBehavior.PressReleaseExcluded |
            ComponentBehavior.Activation |
            ComponentBehavior.KeyboardActivation |
            ComponentBehavior.UnavailableCleanup |
            ComponentBehavior.Transient |
            ComponentBehavior.Composition) },
        { typeof(FilePickerDialog), Requirement<FilePickerDialogSurfaceTests>(
            _interactive |
            ComponentBehavior.DirectionalExcluded |
            ComponentBehavior.PressReleaseExcluded |
            ComponentBehavior.Activation |
            ComponentBehavior.KeyboardActivation |
            ComponentBehavior.UnavailableCleanup |
            ComponentBehavior.Transient |
            ComponentBehavior.Composition) },
        { typeof(SaveFileDialog), Requirement<SaveFileDialogTests>(
            _interactive |
            ComponentBehavior.DirectionalExcluded |
            ComponentBehavior.PressReleaseExcluded |
            ComponentBehavior.Activation |
            ComponentBehavior.KeyboardActivation |
            ComponentBehavior.UnavailableCleanup |
            ComponentBehavior.Transient |
            ComponentBehavior.Composition) },
    };

    /// <summary>Verifies every exported concrete Control has one explicit complete behavior requirement.</summary>
    [Fact]
    public void Catalog_WhenPublicConcreteControlsChange_RequiresExplicitBehaviorClassification()
    {
        var controls = typeof(Control).Assembly.GetExportedTypes()
            .Where(type => !type.IsAbstract && typeof(Control).IsAssignableFrom(type))
            .ToHashSet();

        _requirements.Keys.ShouldBe(controls, ignoreOrder: true);
        _requirements.Values.ShouldAllBe(requirement =>
            HasExactlyOne(requirement.Behaviors, ComponentBehavior.Hover, ComponentBehavior.HoverExcluded) &&
            HasExactlyOne(requirement.Behaviors, ComponentBehavior.Focus, ComponentBehavior.FocusExcluded) &&
            HasExactlyOne(requirement.Behaviors, ComponentBehavior.Tab, ComponentBehavior.TabExcluded) &&
            HasExactlyOne(
                requirement.Behaviors,
                ComponentBehavior.Directional,
                ComponentBehavior.DirectionalExcluded) &&
            HasExactlyOne(
                requirement.Behaviors,
                ComponentBehavior.PressRelease,
                ComponentBehavior.PressReleaseExcluded));
    }

    /// <summary>Verifies every catalog requirement is backed by attributed mounted test evidence.</summary>
    [Fact]
    public void Evidence_WhenBehaviorRequirementsChange_RequiresExactMountedCoverage()
    {
        var evidence = new Dictionary<Type, ComponentBehavior>();

        foreach (var fixture in _requirements.Values.Select(requirement => requirement.Fixture).Distinct())
        {
            foreach (var method in fixture.GetMethods())
            {
                foreach (var attribute in method
                             .GetCustomAttributes(typeof(ComponentBehaviorEvidenceAttribute), inherit: false)
                             .Cast<ComponentBehaviorEvidenceAttribute>())
                {
                    _requirements.ContainsKey(attribute.ControlType).ShouldBeTrue(
                        $"{fixture.Name}.{method.Name} references an uncatalogued control.");
                    _requirements[attribute.ControlType].Fixture.ShouldBe(
                        fixture,
                        $"{attribute.ControlType.Name} evidence belongs in its catalogued fixture.");
                    _ = evidence.TryGetValue(attribute.ControlType, out var previous);
                    evidence[attribute.ControlType] = previous | attribute.Behaviors;
                }
            }
        }

        evidence.Keys.ShouldBe(_requirements.Keys, ignoreOrder: true);

        foreach (var (control, requirement) in _requirements)
        {
            evidence[control].ShouldBe(
                requirement.Behaviors,
                $"{control.Name} must have exact evidence for every required or excluded behavior.");
        }
    }

    private static ComponentBehaviorRequirement Requirement<TFixture>(ComponentBehavior behaviors) =>
        new(typeof(TFixture), behaviors);

    private static bool HasExactlyOne(
        ComponentBehavior value,
        ComponentBehavior first,
        ComponentBehavior second)
    {
        var selected = value & (first | second);
        return selected == first || selected == second;
    }
}
