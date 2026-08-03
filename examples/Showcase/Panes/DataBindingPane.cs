// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using System.Globalization;

using DataBinding;

using Text = SharpVision.Controls.Display.Text;
using UiListView = ListView;

/// <summary>Demonstrates live scalar, nested, collection, and selection bindings.</summary>
internal sealed class DataBindingPane: CompositeControlBase
{
    private readonly Text _status;

    /// <summary>Initializes the retained binding specimens and their model.</summary>
    internal DataBindingPane()
    {
        var first = new DataBindingShowcaseItem("Alpha");
        var second = new DataBindingShowcaseItem("Beta");
        Model = new DataBindingShowcaseModel
        {
            Address = new DataBindingShowcaseAddress { City = "Porto" },
            Enabled = true,
            IsPrimary = true,
            NumericValue = 35,
            OneWayText = "Source owns this value",
            TwoWayText = "Edit either endpoint",
            ProgressValue = 0.65,
            SelectedColor = Color.Rgb(85, 120, 210),
            SelectedIndex = 0,
            StatusText = "One-way model status",
            SelectedItem = second
        };
        Model.Items.Add(first);
        Model.Items.Add(second);

        var oneWay = new TextInput { Width = Length.Cells(32), Placeholder = nameof(BindingMode.OneWay) };
        var twoWay = new TextInput { Width = Length.Cells(32), Placeholder = nameof(BindingMode.TwoWay) };
        var oneWayToSource = new TextInput
        {
            Width = Length.Cells(32),
            Text = "Target seeds the source",
            Placeholder = nameof(BindingMode.OneWayToSource)
        };
        _ = oneWay.Bind(Model, source => source.OneWayText, BindingMode.OneWay);
        _ = twoWay.Bind(Model, source => source.TwoWayText, BindingMode.TwoWay);
        _ = oneWayToSource.Bind(Model, source => source.OneWayToSourceText, BindingMode.OneWayToSource);

        NameInput = new TextInput { Width = Length.Cells(28) };
        var enabled = new CheckBox
        {
            Text = "&Model-enabled option",
            IsThreeState = true
        };
        var primary = new RadioButton
        {
            Text = "Primary option",
            UseMnemonic = false
        };
        var slider = new Slider { Width = Length.Cells(30), Minimum = 0, Maximum = 100 };
        var scrollBar = new ScrollBar
        {
            Width = Length.Cells(30),
            Orientation = Orientation.Horizontal,
            Minimum = 0,
            Maximum = 100
        };
        var progress = new ProgressBar { Width = Length.Cells(30), Minimum = 0, Maximum = 1 };
        var color = new ColorPicker { Width = Length.Cells(34), Height = Length.Cells(12) };
        Items = new UiListView { Height = Length.Cells(6), Width = Length.Cells(28) };
        _ = NameInput.Bind(Model, source => source.Address!.City);
        _ = enabled.Bind(Model, source => source.Enabled);
        _ = primary.Bind(Model, source => source.IsPrimary);
        _ = slider.Bind(Model, source => source.NumericValue);
        _ = scrollBar.Bind(Model, source => source.NumericValue);
        _ = progress.Bind(Model, source => source.ProgressValue);
        _ = color.Bind(Model, source => source.SelectedColor);
        _ = Items.BindItems(Model, source => source.Items);
        _ = Items.BindSelection(Model, source => source.SelectedItem);

        var choices = new ComboBox { Width = Length.Cells(28) };
        _ = choices.BindItems(Model, source => source.Items);
        _ = choices.BindSelection(Model, source => source.SelectedItem);

        var tabs = new TabControl { Width = Length.Cells(36), Height = Length.Cells(6) };
        tabs.Items.Add(new TabItem
        {
            Header = "First",
            UseMnemonic = false,
            Content = new Text("Bound tab index 0")
        });
        tabs.Items.Add(new TabItem
        {
            Header = "Second",
            UseMnemonic = false,
            Content = new Text("Bound tab index 1")
        });
        _ = tabs.Bind(Model, source => source.SelectedIndex);

        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(new MenuItem { Text = "First", UseMnemonic = false });
        menu.Items.Add(new MenuItem { Text = "Second", UseMnemonic = false });
        _ = menu.Bind(Model, source => source.SelectedIndex);

        _status = new Text("Binding log: ready");
        NameInput.TextChanged += (_, _) => _status.Content = $"Model city: {Model.Address?.City ?? "none"}";
        Items.SelectionChanged += (_, _) =>
            _status.Content = $"Selected: {Model.SelectedItem?.Name ?? "none"}";
        var replaceAddress = new Button { Text = "&Replace address" };
        replaceAddress.Click += (_, _) =>
            Model.Address = new DataBindingShowcaseAddress { City = "Coimbra" };
        var addItem = new Button { Text = "&Add item" };
        addItem.Click += (_, _) =>
            Model.Items.Add(new DataBindingShowcaseItem($"Item {Model.Items.Count + 1}"));

        var oneWayText = new Text();
        _ = oneWayText.Bind(Model, source => source.StatusText);
        var convertedValue = new Text();
        _ = convertedValue.BindProperty(
            target => target.Content,
            Model,
            source => source.NumericValue,
            value => $"Converted numeric value: {value.ToString(CultureInfo.InvariantCulture)}",
            convertBack: null,
            BindingMode.OneWay,
            "Converted numeric value: 0");

        var commandStatus = new Text("Command log: ready");
        Model.Command = new DataBindingShowcaseCommand(parameter =>
            commandStatus.Content = $"Command parameter: {parameter ?? "null"}");
        Model.CommandParameter = "bound payload";
        var command = new Button { Text = "Run bound command", UseMnemonic = false };
        _ = command.BindCommand(Model, source => source.Command);
        _ = command.BindCommandParameter(Model, source => source.CommandParameter);

        InitializeContent(new DocPage(
            Title,
            "<info>Data binding</info> connects retained controls to ordinary notifying .NET models without manual event plumbing.",
            new DocSection(
                "↔️",
                "Binding directions",
                "OneWay initializes and follows the source; TwoWay synchronizes both endpoints; OneWayToSource initializes the model from the target and ignores later source changes.",
                new DocExample(
                    "Every BindingMode",
                    "The placeholder names the explicit mode. Edit the targets and compare which values can flow back to the model.",
                    new DocColumn(oneWay, twoWay, oneWayToSource),
                    "input.Bind(model, x => x.Name, BindingMode.OneWayToSource);")),
            new DocSection(
                "🔗",
                "Scalar and nested paths",
                "Edits flow to the model immediately; replacing a nested object rewires the live path.",
                new DocExample(
                    "Two-way city and nullable state",
                    "Edit the city, toggle the option, or replace the complete Address object.",
                    new DocColumn(NameInput, enabled, replaceAddress, _status),
                    "nameInput.Bind(customer, x => x.Address.City);\ncheckBox.Bind(settings, x => x.Enabled);")),
            new DocSection(
                "🎛️",
                "Natural scalar adapters",
                "Text, editable text, check and radio state, integral ranges, one-way progress, and RGB color use strongly typed control-specific defaults.",
                new DocExample(
                    "Every scalar adapter family",
                    "Slider and ScrollBar share one model value; ProgressBar follows its source one-way; ColorPicker writes stable RGB values.",
                    new DocColumn(
                        new DocRow(new Text("Status text")
                        {
                            Face = new Face(
                ThemeColor.ControlText,
                Color.Transparent,
                TerminalAttributes.Dim,
                Underline.None,
                Color.Default),
                            Width = Length.Cells(14)
                        }, oneWayText),
                        new DocRow(new Text("RadioButton")
                        {
                            Face = new Face(
                ThemeColor.ControlText,
                Color.Transparent,
                TerminalAttributes.Dim,
                Underline.None,
                Color.Default),
                            Width = Length.Cells(14)
                        }, primary),
                        new DocRow(new Text("Slider")
                        {
                            Face = new Face(
                ThemeColor.ControlText,
                Color.Transparent,
                TerminalAttributes.Dim,
                Underline.None,
                Color.Default),
                            Width = Length.Cells(14)
                        }, slider),
                        new DocRow(new Text("ScrollBar")
                        {
                            Face = new Face(
                ThemeColor.ControlText,
                Color.Transparent,
                TerminalAttributes.Dim,
                Underline.None,
                Color.Default),
                            Width = Length.Cells(14)
                        }, scrollBar),
                        new DocRow(new Text("ProgressBar")
                        {
                            Face = new Face(
                ThemeColor.ControlText,
                Color.Transparent,
                TerminalAttributes.Dim,
                Underline.None,
                Color.Default),
                            Width = Length.Cells(14)
                        }, progress),
                        new DocRow(new Text("ColorPicker")
                        {
                            Face = new Face(
                ThemeColor.ControlText,
                Color.Transparent,
                TerminalAttributes.Dim,
                Underline.None,
                Color.Default),
                            Width = Length.Cells(14)
                        }, color)))),
            new DocSection(
                "📚",
                "Observable items and selection",
                "Membership follows INotifyCollectionChanged while selected identity remains coordinated.",
                new DocExample(
                    "ListView and ComboBox item identity",
                    "Add items and select from either target; no transient selection is written during collection refresh.",
                    new DocColumn(Items, choices, addItem),
                    "list.BindItems(vm, x => x.Items);\nlist.BindSelection(vm, x => x.SelectedItem);"),
                new DocExample(
                    "Selected-index adapters",
                    "TabControl and Menu coordinate the same integer property through their natural TwoWay adapter.",
                    new DocColumn(tabs, menu))),
            new DocSection(
                "⚙️",
                "Conversion, commands, and lifetime",
                "BindProperty supplies typed conversion and fallback. Button borrows ICommand and its parameter. The target owns every returned IDisposable Binding.",
                new DocExample(
                    "Generic conversion",
                    "The numeric model value becomes invariant display text through the explicit OneWay escape hatch.",
                    convertedValue),
                new DocExample(
                    "Command and parameter",
                    "The button receives command and parameter as separate one-way bindings while ICommand keeps execution policy.",
                    new DocColumn(command, commandStatus),
                    "button.BindCommand(vm, x => x.Command);\nbutton.BindCommandParameter(vm, x => x.Parameter);"))));
    }

    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "Data Binding";

    /// <summary>Gets the live showcase model.</summary>
    internal DataBindingShowcaseModel Model { get; }

    /// <summary>Gets the two-way nested text input.</summary>
    internal TextInput NameInput { get; }

    /// <summary>Gets the observable items target.</summary>
    internal UiListView Items { get; }
}
