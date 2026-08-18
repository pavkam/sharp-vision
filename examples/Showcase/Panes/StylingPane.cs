// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Demonstrates direct appearance values, visual states, themes, and custom controls.</summary>
internal sealed class StylingPane: CompositeControlBase
{
    /// <summary>Initializes the retained Styling concept page.</summary>
    internal StylingPane() => InitializeContent(CreateContent());

    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "Styling";

    private static DocPage CreateContent()
    {
        var colorKinds = new DocColumn(
            CreateColorSample("Terminal default", Color.Default),
            CreateColorSample("24-bit RGB", Color.Rgb(120, 70, 180)),
            CreateColorSample("Transparent composition", Color.Transparent));

        var defaultControls = new DocColumn(
            new Button { Text = "Default button", UseMnemonic = false },
            new TextInput { Width = Length.Cells(24), Text = "Default text field" },
            new ComboBox { Width = Length.Cells(24), Items = ["Default choice"], SelectedIndex = 0 },
            new CheckBox { Text = "Default toggle", IsChecked = true, UseMnemonic = false });

        var stateVocabulary = CreateVisualStates();

        var catalogEntry = ThemeCatalog.Entries[0];
        var catalog = new Text(
            $"Catalog entry: {Text.Escape(catalogEntry.Name)}\n" +
            $"Slug: {Text.Escape(catalogEntry.Slug)} · Scheme: {catalogEntry.ColorScheme}\n" +
            $"Author: {Text.Escape(catalogEntry.Author)} · License: {Text.Escape(catalogEntry.License)}")
        {
            Overflow = Overflow.Wrap
        };

        var panel = new ShowcasePanel();
        var placementPreview = new ShowcasePanel();
        var left = new Button { Text = "Left", UseMnemonic = false };
        var right = new Button { Text = "Right", UseMnemonic = false };
        var above = new Button { Text = "Above", UseMnemonic = false };
        var below = new Button { Text = "Below", UseMnemonic = false };
        ShowcasePaneHelpers.WireLabelPlacement(left, right, above, below, panel, placementPreview);

        return new DocPage(
            Title,
            "<info>Styling</info> uses concrete terminal colors, semantic theme values, complete appearance composites, and deterministic state sets.",
            new DocSection(
                "🎨",
                "Color values",
                "Color represents terminal-default, 24-bit RGB, or transparent composition. ControlColor can instead retain a semantic SemanticColor until resolution.",
                new DocExample(
                    "Every Color representation",
                    "RGB values stay fixed across themes; transparent preserves the destination background.",
                    colorKinds,
                    "control.Face = new Face(SemanticColor.ControlText, SemanticColor.Control, SemanticDecoration.NormalText, Underline.None, Color.Default);")),
            new DocSection(
                "🎭",
                "Visual states",
                "PointerOver, FocusWithin, Focused, Current, Selected, Checked, Indeterminate, Pressed, and Disabled overlays apply in a stable order after the normal appearance.",
                new DocExample(
                    "Complete state vocabulary",
                    "Move pointer and focus, press the button, enter the focus-within group, inspect the current menu item, and compare checked, indeterminate, and disabled faces.",
                    stateVocabulary,
                    "button.Style = ButtonStyle.Filled;")),
            new DocSection(
                "🧩",
                "Control defaults",
                "The Control role supplies passive defaults; interactive controls opt into Input or a specialized role for pointer, focus, press, and selection cues. Complete local composites remain explicit overrides.",
                new DocExample(
                    "Zero-configuration controls",
                    "Button, TextInput, ComboBox, and CheckBox remain readable through normal and interactive states without local colors.",
                    defaultControls)),
            new DocSection(
                "📚",
                "Theme catalog",
                "ThemeCatalog exposes stable metadata before loading one immutable application theme snapshot.",
                new DocExample(
                    "Attribution and scheme metadata",
                    "Display name, slug, dark/light scheme, author, and license support discoverable pickers and attribution.",
                    catalog,
                    "application.Theme = ThemeCatalog.Load(\"default-dark\");")),
            new DocSection(
                "🛠️",
                "Custom controls",
                "Third-party controls inherit the passive Control role, can opt into an interactive or specialized protected semantic role, and can inspect ActualFace, ActualBorder, and ActualShadow.",
                new DocExample(
                    "ShowcasePanel label placement",
                    "All four LabelPlacement values update two retained custom controls without a private style system.",
                    new DocColumn(
                        panel,
                        new DocRow(left, right, above, below),
                        placementPreview))));
    }

    private static DocRow CreateColorSample(string caption, Color background) => new DocRow(
        new Dock
        {
            Width = Length.Cells(8),
            Height = Length.Cells(2),
            Face = new Face(
                SemanticColor.ControlText,
                background,
                SemanticDecoration.NormalText,
                Underline.None,
                Color.Default),
            Border = new Border(
                BorderSide.All,
                BorderGlyphStyle.Light,
                SemanticColor.ControlBorder,
                Color.Transparent,
                SemanticDecoration.Border)
        },
        new Text(caption));

    private static DocColumn CreateVisualStates()
    {
        var pointerButton = new Button { Text = "Pointer, focus, and press", UseMnemonic = false };

        var focusInput = new TextInput { Width = Length.Cells(24), Placeholder = "Focus within" };
        var focusGroup = new GroupBox
        {
            HeaderText = "FocusWithin",
            UseMnemonic = false,
            Content = focusInput
        };

        var item = new MenuItem { Text = "Current and selected item", UseMnemonic = false };
        var menu = new Menu { Orientation = Orientation.Vertical };
        menu.Items.Add(item);
        menu.SelectedIndex = 0;

        var checkedState = new CheckBox
        {
            Text = "Checked",
            IsChecked = true,
            UseMnemonic = false
        };
        var indeterminateState = new CheckBox
        {
            Text = "Indeterminate",
            ThreeState = true,
            IsChecked = null,
            UseMnemonic = false
        };
        var disabledState = new Button
        {
            Text = "Disabled",
            IsEnabled = false,
            UseMnemonic = false
        };

        return new DocColumn(pointerButton, focusGroup, menu, checkedState, indeterminateState, disabledState);
    }

}
