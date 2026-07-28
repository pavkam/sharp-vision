// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Compatibility;

using SharpVision.DataBinding;

using ControlCalendar = SharpVision.Controls.Input.Calendar;

/// <summary>Compiles and executes data binding through public package-consumer APIs only.</summary>
public sealed class DataBindingConsumerTests
{
    /// <summary>Verifies nested two-way binding needs no internal framework access.</summary>
    [Fact]
    public void Bind_WhenNestedValueChanges_SynchronizesPublicEndpoints()
    {
        var address = new ConsumerAddress { City = "Porto" };
        var model = new ConsumerModel { Address = address };
        var input = new TextInput();
        using var binding = input.Bind(model, source => source.Address!.City);

        input.Text = "Lisbon";

        address.City.ShouldBe("Lisbon");
    }

    /// <summary>Verifies calendar binding compiles and runs from public API.</summary>
    [Fact]
    public void CalendarBind_Compiles()
    {
        var calendar = new ControlCalendar();
        var model = new ConsumerModel();
        using var binding = calendar.Bind(model, m => m.DateSelection);

        binding.Mode.ShouldBe(BindingMode.TwoWay);
    }

    /// <summary>Verifies expander binding compiles and runs from public API.</summary>
    [Fact]
    public void ExpanderBind_Compiles()
    {
        var expander = new Expander();
        var model = new ConsumerModel();
        using var binding = expander.Bind(model, m => m.IsExpanded);

        binding.Mode.ShouldBe(BindingMode.TwoWay);
    }

    /// <summary>Verifies FIGlet text binding compiles and runs from public API.</summary>
    [Fact]
    public void FigletTextBind_Compiles()
    {
        var font = FigletCatalog.Default.Load("Small");
        var figlet = new FigletText(font);
        var model = new ConsumerModel { DisplayText = "hello" };
        using var binding = figlet.Bind(model, m => m.DisplayText);

        figlet.Content.ShouldBe("hello");
        binding.Mode.ShouldBe(BindingMode.OneWay);
    }

    /// <summary>Verifies observable items and selection compile as public generic APIs.</summary>
    [Fact]
    public void BindItems_WhenSelectionChanges_SynchronizesPublicModel()
    {
        var first = new ConsumerItem("A");
        var second = new ConsumerItem("B");
        var model = new ConsumerModel
        {
            Items = [first, second],
            SelectedItem = first
        };
        var list = new ListView();
        using var items = list.BindItems(model, source => source.Items);
        using var selection = list.BindSelection(model, source => source.SelectedItem);

        list.SelectedIndex = 1;

        model.SelectedItem.ShouldBeSameAs(second);
    }
}
