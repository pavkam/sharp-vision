// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Compatibility;

/// <summary>Freezes the public UI namespace architecture and standardized type names.</summary>
public sealed class NamespaceArchitectureTests
{
    private static readonly IReadOnlyDictionary<string, string[]> _expectedNamespaces =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["SharpVision.Controls"] =
            [
                "CompositeControl",
                "Container",
                "ContentControl",
                "Control",
                "ControlCollection",
                "IContextMenu",
                "InvalidationImpact",
                "ItemTemplate",
                "ItemsControl",
                "Overlay",
                "Pressable",
                "ScrollBar",
                "ScrollBarGlyphs",
                "ScrollBarStyle",
                "ScrollBarStyleSet",
                "Screen"
            ],
            ["SharpVision.Controls.Collections"] =
            [
                "ListSelectionMode",
                "ListView",
                "TabCloseRequestedEventArgs",
                "TabControl",
                "TabHeaderOverflowPolicy",
                "TabItem",
                "TabItemCollection",
                "TabSelectionChangedEventArgs",
                "TreeSelectionMode",
                "TreeView",
                "TreeViewItem",
                "TreeViewItemCollection",
                "TreeViewItemInvokedEventArgs",
                "TreeViewSelectionChangedEventArgs",
                "TreeViewSelectionChangingEventArgs"
            ],
            ["SharpVision.Controls.Display"] =
            [
                "ChaseIndicator",
                "ChaseIndicatorStyle",
                "ChaseIndicatorStyleSet",
                "ChaseMovement",
                "FigletText",
                "Image",
                "ImageStretch",
                "Prism",
                "PrismDirection",
                "ProgressBar",
                "ProgressValueChangedEventArgs",
                "Separator",
                "Spinner",
                "SpinnerStyle",
                "SpinnerStyleSet",
                "StatusBar",
                "StatusBarItem",
                "StatusBarItemAlignment",
                "StatusBarItemCollection",
                "StatusBarSeparatorGlyphs",
                "Text"
            ],
            ["SharpVision.Controls.Input"] =
            [
                "Button",
                "ButtonStyle",
                "ButtonStyleSet",
                "Calendar",
                "CalendarBlockedDateCollection",
                "CalendarSelectionChangedEventArgs",
                "CalendarSelectionMode",
                "CheckBox",
                "CheckBoxGlyphs",
                "CheckBoxMarkStyle",
                "CheckBoxStyle",
                "CheckBoxStyleSet",
                "ColorPicker",
                "ComboBox",
                "DateInput",
                "DateInterval",
                "DateTimeInput",
                "HyperlinkButton",
                "RadioButton",
                "RadioButtonGlyphs",
                "RadioButtonMarkStyle",
                "RadioButtonSelectionChangedEventArgs",
                "RadioButtonStyle",
                "RadioButtonStyleSet",
                "Slider",
                "TextInput",
                "TextInputContextMenu",
                "TimeInput"
            ],
            ["SharpVision.Controls.Layout"] =
            [
                "Dock",
                "ExpandedChangedEventArgs",
                "Expander",
                "Grid",
                "GroupBox",
                "Stack",
                "Table",
                "TableBuilder",
                "TableCellReference",
                "TableColumn",
                "TableColumnCollection",
                "TableRow",
                "TableRowCollection"
                ,"TableRowInvokedEventArgs"
                ,"TableSelectionChangedEventArgs"
                ,"TableSelectionMode"
                ,"TableSortChangedEventArgs"
                ,"TableSortDirection"
            ],
            ["SharpVision.Menus"] =
            [
                "ContextMenu",
                "Menu",
                "MenuBuilder",
                "MenuEntryCollection",
                "MenuItem",
                "MenuItemInvokedEventArgs",
                "MenuItemKind",
                "MenuSeparator"
            ],
            ["SharpVision.Navigation"] =
            [
                "NavigationView",
                "NavigationViewEntryCollection",
                "NavigationViewGroup",
                "NavigationViewItem",
                "NavigationViewSelectionChangedEventArgs",
                "NavigationViewSeparator"
            ],
            ["SharpVision.Popups"] =
            [
                "Flyout",
                "Popup",
                "PopupModalBehavior",
                "PopupPlacement",
                "Tooltip"
            ],
            ["SharpVision.Surfaces"] =
            [
                "FloatingSurface"
            ],
            ["SharpVision.Windows"] =
            [
                "Window",
                "WindowClosePlacement",
                "WindowTitlePlacement"
            ]
        };

    private static readonly string[] _expectedSharedTypes =
    [
        "SharpVision.Dialogs.MessageBox",
        "SharpVision.Dialogs.MessageBoxButtons",
        "SharpVision.Dialogs.MessageBoxResult",
        "SharpVision.Layout.ResolvedAxes",
        "SharpVision.Styling.BorderGlyphStyle",
        "SharpVision.Styling.ChromeRenderOptions",
        "SharpVision.Styling.ShadowMode"
    ];

    private static readonly string[] _retiredTypes =
    [
        "SharpVision.Controls.ChangeImpact",
        "SharpVision.Controls.Children",
        "SharpVision.Controls.Layout." + "Canvas",
        "SharpVision.Controls.Layout." + "I" + "CanvasPositionConstraint",
        "SharpVision.Controls.CheckBoxMarks",
        "SharpVision.Controls.Glyphs",
        "SharpVision.Controls.List",
        "SharpVision.Controls.Marks",
        "SharpVision.Controls.MenuItems",
        "SharpVision.Controls.NavigationViewItems",
        "SharpVision.Controls.SelectionMode",
        "SharpVision.Controls.StatusBarAlignment",
        "SharpVision.Controls.StatusBarItems",
        "SharpVision.Controls.StatusBarSeparators",
        "SharpVision.Controls.TabItems",
        "SharpVision.Controls.TableColumns",
        "SharpVision.Controls.TableRows",
        "SharpVision.Controls.TreeViewItems",
        "SharpVision.Controls.Display.ChasePattern",
        "SharpVision.Controls.Display.SpinnerPattern",
        "SharpVision.Input.SelectionChangedEventArgs"
    ];

    /// <summary>Verifies every feature namespace exposes its exact approved public type set.</summary>
    [Fact]
    public void PublicTypes_WhenGroupedByFeatureNamespace_MatchArchitecture()
    {
        var assembly = typeof(Control).Assembly;

        foreach (var expected in _expectedNamespaces)
        {
            var actual = assembly.GetExportedTypes()
                .Where(type => string.Equals(type.Namespace, expected.Key, StringComparison.Ordinal))
                .Select(type => type.Name)
                .Order(StringComparer.Ordinal)
                .ToArray();

            actual.ShouldBe([.. expected.Value.Order(StringComparer.Ordinal)]);
        }
    }

    /// <summary>Verifies shared and dialog contracts live in their architectural namespaces.</summary>
    [Fact]
    public void PublicTypes_WhenSharedAcrossFeatures_HaveArchitecturalOwners()
    {
        var assembly = typeof(Control).Assembly;

        foreach (var expected in _expectedSharedTypes)
        {
            _ = assembly.GetType(expected, throwOnError: false).ShouldNotBeNull(expected);
        }
    }

    /// <summary>Verifies representative controls from every public feature namespace can be consumed together.</summary>
    [Fact]
    public void PublicTypes_WhenConsumedFromNewNamespaces_AreConstructible()
    {
        Control[] controls =
        [
            new ListView(),
            new ControlText(),
            new Button(),
            new Stack(),
            new Menu(),
            new NavigationView(),
            new Popup(),
            new Window(),
            new MessageBox("Ready")
        ];

        foreach (var control in controls)
        {
            control.Dispose();
        }
    }

    /// <summary>Verifies retired flat-namespace names are not retained as compatibility aliases.</summary>
    [Fact]
    public void PublicTypes_WhenNamesAreRetired_AreAbsent()
    {
        var assembly = typeof(Control).Assembly;

        foreach (var retired in _retiredTypes)
        {
            assembly.GetType(retired, throwOnError: false).ShouldBeNull(retired);
        }
    }
}
