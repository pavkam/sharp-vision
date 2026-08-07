// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.DataBinding;

using SharpVision.DataBinding;

using Support;

/// <summary>Verifies concise natural-value and command adapters.</summary>
public sealed class BindingAdapterTests
{
    /// <summary>Verifies display and editable text adapters project null as empty text.</summary>
    [Fact]
    public void Bind_WhenTextSourceIsNull_ProjectsEmptyText()
    {
        var model = new BindingModel();
        var display = new ControlText("stale");
        var input = new TextInput { Text = "stale" };
        using var displayBinding = display.Bind(model, source => source.Name);
        using var inputBinding = input.Bind(model, source => source.Name);

        display.Content.ShouldBeEmpty();
        input.Text.ShouldBeEmpty();
    }

    /// <summary>Verifies CheckBox maps its nullable state two-way.</summary>
    [Fact]
    public void Bind_WhenCheckBoxChanges_SynchronizesNullableState()
    {
        var model = new BindingModel { Checked = true };
        var target = new CheckBox { ThreeState = true };
        using var binding = target.Bind(model, source => source.Checked);

        target.IsChecked.ShouldBe(true);

        target.IsChecked = null;

        model.Checked.ShouldBeNull();
    }

    /// <summary>Verifies Slider maps its bounded integer two-way.</summary>
    [Fact]
    public void Bind_WhenSliderChanges_SynchronizesValue()
    {
        var model = new BindingModel { Number = 25 };
        var target = new Slider { Maximum = 50 };
        using var binding = target.Bind(model, source => source.Number);

        target.Value.ShouldBe(25);

        target.Value = 40;

        model.Number.ShouldBe(40);
    }

    /// <summary>Verifies RadioButton maps Boolean state two-way.</summary>
    [Fact]
    public void Bind_WhenRadioButtonChanges_SynchronizesState()
    {
        var model = new BindingModel { Enabled = true };
        var target = new RadioButton();
        using var binding = target.Bind(model, source => source.Enabled);

        target.IsChecked.ShouldBeTrue();

        target.IsChecked = false;

        model.Enabled.ShouldBeFalse();
    }

    /// <summary>Verifies ScrollBar maps its bounded integer two-way.</summary>
    [Fact]
    public void Bind_WhenScrollBarChanges_SynchronizesValue()
    {
        var model = new BindingModel { Number = 20 };
        var target = new ScrollBar { Maximum = 50 };
        using var binding = target.Bind(model, source => source.Number);

        target.Value.ShouldBe(20);

        target.Value = 30;

        model.Number.ShouldBe(30);
    }

    /// <summary>Verifies ProgressBar maps its display value one-way.</summary>
    [Fact]
    public void Bind_WhenProgressChanges_UpdatesDisplayOnly()
    {
        var model = new BindingModel { Progress = 25.5 };
        var target = new ProgressBar { Maximum = 100 };
        using var binding = target.Bind(model, source => source.Progress);

        target.Value.ShouldBe(25.5);
        binding.Mode.ShouldBe(BindingMode.OneWay);
    }

    /// <summary>Verifies ColorPicker maps a typed color two-way while detached.</summary>
    [Fact]
    public void Bind_WhenColorPickerChanges_SynchronizesColor()
    {
        var blue = Color.Rgb(0, 0, 255);
        var red = Color.Rgb(255, 0, 0);
        var model = new BindingModel { Color = blue };
        var target = new ColorPicker();
        using var binding = target.Bind(model, source => source.Color);

        target.Value.ShouldBe(blue);

        target.Value = red;

        model.Color.ShouldBe(red);
    }

    /// <summary>Verifies ListView maps selected index two-way.</summary>
    [Fact]
    public void Bind_WhenListSelectionChanges_SynchronizesIndex()
    {
        var model = new BindingModel { Number = 1 };
        var target = new UiListView { Items = ["A", "B", "C"] };
        using var binding = target.Bind(model, source => source.Number);

        target.SelectedIndex.ShouldBe(1);

        target.SelectedIndex = 2;

        model.Number.ShouldBe(2);
    }

    /// <summary>Verifies ComboBox maps selected index two-way.</summary>
    [Fact]
    public void Bind_WhenComboSelectionChanges_SynchronizesIndex()
    {
        var model = new BindingModel { Number = 1 };
        var target = new ComboBox { Items = ["A", "B"] };
        using var binding = target.Bind(model, source => source.Number);

        target.SelectedIndex.ShouldBe(1);

        target.SelectedIndex = 0;

        model.Number.ShouldBe(0);
    }

    /// <summary>Verifies TabControl maps selected index two-way.</summary>
    [Fact]
    public void Bind_WhenTabSelectionChanges_SynchronizesIndex()
    {
        var model = new BindingModel { Number = 1 };
        var target = new TabControl();
        target.Items.Add(new TabItem { HeaderText = "A" });
        target.Items.Add(new TabItem { HeaderText = "B" });
        using var binding = target.Bind(model, source => source.Number);

        target.SelectedIndex.ShouldBe(1);

        target.SelectedIndex = 0;

        model.Number.ShouldBe(0);
    }

    /// <summary>Verifies Menu maps selected index two-way.</summary>
    [Fact]
    public void Bind_WhenMenuSelectionChanges_SynchronizesIndex()
    {
        var model = new BindingModel { Number = 0 };
        var target = new Menu { Items = { new MenuItem(), new MenuItem() } };
        using var binding = target.Bind(model, source => source.Number);

        target.SelectedIndex.ShouldBe(0);

        target.SelectedIndex = 1;

        model.Number.ShouldBe(1);
    }

    /// <summary>Verifies Button command and parameter replacements remain live.</summary>
    [Fact]
    public void BindCommand_WhenModelReplacesValues_UsesLatestCommandAndParameter()
    {
        var first = new BindingCommand();
        var second = new BindingCommand();
        var parameter = new object();
        var model = new BindingModel { Command = first };
        var target = new Button();
        using var commandBinding = target.BindCommand(model, source => source.Command);
        using var parameterBinding = target.BindCommandParameter(model, source => source.Parameter);

        model.Command = second;
        model.Parameter = parameter;
        target.PerformClick();

        first.ExecutedParameter.ShouldBeNull();
        second.ExecutedParameter.ShouldBeSameAs(parameter);
    }
}
