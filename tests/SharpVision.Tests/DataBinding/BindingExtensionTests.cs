// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.DataBinding;

using SharpVision.DataBinding;

using Support;

using ControlCalendar = SharpVision.Controls.Input.Calendar;

/// <summary>Verifies typed binding extensions for Calendar, Expander, and FigletText controls.</summary>
public sealed class BindingExtensionTests
{
    /// <summary>Verifies a calendar binding initializes from the model and syncs two-way.</summary>
    [Fact]
    public void CalendarBind_WhenSourceChanges_SyncsSelection()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var interval = new DateInterval(today, today);
        var model = new BindingModel { DateSelection = interval };
        var calendar = new ControlCalendar();
        using var binding = calendar.Bind(model, source => source.DateSelection);

        calendar.Selection.ShouldBe(interval);
        binding.Mode.ShouldBe(BindingMode.TwoWay);
    }

    /// <summary>Verifies a calendar selection change propagates back to the model.</summary>
    [Fact]
    public void CalendarBind_WhenTargetChanges_UpdatesModel()
    {
        var model = new BindingModel();
        var calendar = new ControlCalendar();
        using var binding = calendar.Bind(model, source => source.DateSelection);
        var today = DateOnly.FromDateTime(DateTime.Now);
        var interval = new DateInterval(today, today);

        calendar.Selection = interval;

        model.DateSelection.ShouldBe(interval);
    }

    /// <summary>Verifies a null model selection clears the calendar.</summary>
    [Fact]
    public void CalendarBind_WhenSourceIsNull_ClearsSelection()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var model = new BindingModel { DateSelection = new DateInterval(today, today) };
        var calendar = new ControlCalendar();
        using var binding = calendar.Bind(model, source => source.DateSelection);

        model.DateSelection = null;

        calendar.Selection.ShouldBeNull();
    }

    /// <summary>Verifies an expander binding initializes from the model.</summary>
    [Fact]
    public void ExpanderBind_WhenSourceChanges_SyncsIsExpanded()
    {
        var model = new BindingModel { IsExpanded = false };
        var expander = new Expander();
        using var binding = expander.Bind(model, source => source.IsExpanded);

        expander.IsExpanded.ShouldBeFalse();
        binding.Mode.ShouldBe(BindingMode.TwoWay);
    }

    /// <summary>Verifies an expander toggle propagates back to the model.</summary>
    [Fact]
    public void ExpanderBind_WhenTargetChanges_UpdatesModel()
    {
        var model = new BindingModel { IsExpanded = true };
        var expander = new Expander();
        using var binding = expander.Bind(model, source => source.IsExpanded);

        expander.IsExpanded = false;

        model.IsExpanded.ShouldBeFalse();
    }

    /// <summary>Verifies a FIGlet text binding initializes from the model.</summary>
    [Fact]
    public void FigletTextBind_WhenSourceChanges_SyncsContent()
    {
        var model = new BindingModel { Name = "Hello" };
        var font = FigletCatalog.Default.Load("Small");
        var target = new FigletText(font);
        using var binding = target.Bind(model, source => source.Name);

        target.Content.ShouldBe("Hello");
        binding.Mode.ShouldBe(BindingMode.OneWay);
    }

    /// <summary>Verifies a FIGlet text binding converts null to empty.</summary>
    [Fact]
    public void FigletTextBind_WhenSourceIsNull_AppliesEmpty()
    {
        var model = new BindingModel();
        var font = FigletCatalog.Default.Load("Small");
        var target = new FigletText(font);
        using var binding = target.Bind(model, source => source.Name);

        target.Content.ShouldBeEmpty();
    }

    /// <summary>Verifies a FIGlet text binding updates when the model changes.</summary>
    [Fact]
    public void FigletTextBind_WhenSourceUpdates_ReflectsChange()
    {
        var model = new BindingModel { Name = "Before" };
        var font = FigletCatalog.Default.Load("Small");
        var target = new FigletText(font);
        using var binding = target.Bind(model, source => source.Name);

        model.Name = "After";

        target.Content.ShouldBe("After");
    }
}
