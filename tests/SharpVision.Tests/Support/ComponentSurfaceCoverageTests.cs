// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

using SharpVision.Tests.Controls;

using UiCanvas = SharpVision.Controls.Canvas;
using UiText = ControlText;

/// <summary>Guards the public concrete control catalog against unproved mounted behavior.</summary>
public sealed class ComponentSurfaceCoverageTests
{
    private static readonly HashSet<Type> _deferred =
    [
        typeof(ComboBox),
        typeof(Menu),
        typeof(MenuItem),
        typeof(MenuSeparator),
        typeof(Popup),
        typeof(Window),
    ];

    private static readonly Dictionary<Type, Type> _evidence = new()
    {
        [typeof(Button)] = typeof(ButtonSurfaceTests),
        [typeof(UiCanvas)] = typeof(CanvasSurfaceTests),
        [typeof(CheckBox)] = typeof(CheckBoxSurfaceTests),
        [typeof(Dock)] = typeof(DockSurfaceTests),
        [typeof(Expander)] = typeof(ExpanderSurfaceTests),
        [typeof(FigletText)] = typeof(FigletTextSurfaceTests),
        [typeof(Grid)] = typeof(GridSurfaceTests),
        [typeof(GroupBox)] = typeof(GroupBoxSurfaceTests),
        [typeof(List)] = typeof(ListSurfaceTests),
        [typeof(NavigationView)] = typeof(NavigationViewSurfaceTests),
        [typeof(NavigationViewGroup)] = typeof(NavigationViewSurfaceTests),
        [typeof(NavigationViewItem)] = typeof(NavigationViewSurfaceTests),
        [typeof(NavigationViewSeparator)] = typeof(NavigationViewSurfaceTests),
        [typeof(Overlay)] = typeof(OverlaySurfaceTests),
        [typeof(ProgressBar)] = typeof(ProgressBarSurfaceTests),
        [typeof(RadioButton)] = typeof(RadioButtonSurfaceTests),
        [typeof(ScrollBar)] = typeof(ScrollBarSurfaceTests),
        [typeof(Separator)] = typeof(SeparatorSurfaceTests),
        [typeof(Stack)] = typeof(StackSurfaceTests),
        [typeof(TabControl)] = typeof(TabControlTests),
        [typeof(TabItem)] = typeof(TabControlTests),
        [typeof(Table)] = typeof(TableSurfaceTests),
        [typeof(UiText)] = typeof(TextSurfaceTests),
        [typeof(TextInput)] = typeof(TextInputSurfaceTests),
    };

    /// <summary>Verifies every exported concrete Control has named mounted evidence or an explicit deferred-family decision.</summary>
    [Fact]
    public void Catalog_WhenPublicConcreteControlsChange_RequiresMountedEvidenceOrExplicitDeferral()
    {
        var controls = typeof(Control).Assembly.GetExportedTypes()
            .Where(type => !type.IsAbstract && typeof(Control).IsAssignableFrom(type))
            .ToHashSet();
        var accounted = _evidence.Keys.Concat(_deferred).ToHashSet();

        controls.Except(accounted).Select(type => type.FullName).ShouldBeEmpty(
            "A new public concrete control requires a ComponentSurface fixture or an explicit family deferral.");
        accounted.Except(controls).Select(type => type.FullName).ShouldBeEmpty(
            "The coverage map must not retain controls that are no longer public and concrete.");
    }
}
