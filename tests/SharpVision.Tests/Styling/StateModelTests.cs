// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;



/// <summary>Verifies the visual-state model: selection vs checked, combined states, and geometry.</summary>
public sealed class StateModelTests
{
    /// <summary>Verifies a selected but unchecked control resolves selected, never checked, styling.</summary>
    [Fact]
    public void SelectedUncheckedControl_ResolvesSelectedNotChecked()
    {
        Theme theme = new();
        ControlStyle<Control> style = new();
        style.Set(Control.ForegroundProperty, State.Checked, Color.Indexed(1));
        style.Set(Control.ForegroundProperty, State.Selected, Color.Indexed(2));
        theme.SetStyle(style);
        ProbeControl control = new();
        ThemeTestSupport.ApplyTheme(control, theme);

        control.SetSelectedState(true);

        control.Foreground.ShouldBe(Color.Indexed(2));
    }

    /// <summary>Verifies an unchecked checkbox in a selected row does not pick up checked styling.</summary>
    [Fact]
    public void UncheckedCheckBox_WhenRowSelected_DoesNotResolveCheckedStyle()
    {
        Theme theme = new();
        ControlStyle<Control> style = new();
        style.Set(Control.BackgroundProperty, State.Checked, Color.Indexed(1));
        style.Set(Control.BackgroundProperty, State.Selected, Color.Indexed(2));
        theme.SetStyle(style);
        CheckBox box = new();
        ThemeTestSupport.ApplyTheme(box, theme);

        box.SetSelectedState(true);

        box.Background.ShouldBe(Color.Indexed(2));
    }

    /// <summary>Verifies a more specific combined-state definition wins over single-state ones.</summary>
    [Fact]
    public void Resolve_WhenCombinedStateDefined_WinsOverSingleStates()
    {
        Theme theme = new();
        ControlStyle<Control> style = new();
        style.Set(Control.ForegroundProperty, State.Hovered, Color.Indexed(1));
        style.Set(Control.ForegroundProperty, State.Focused, Color.Indexed(2));
        style.Set(Control.ForegroundProperty, State.Hovered | State.Focused, Color.Indexed(3));
        theme.SetStyle(style);
        ProbeControl control = new();
        ThemeTestSupport.ApplyTheme(control, theme);

        ThemeTestSupport.Resolve(control, Control.ForegroundProperty, State.Hovered | State.Focused)
            .ShouldBe(Color.Indexed(3));
        ThemeTestSupport.Resolve(control, Control.ForegroundProperty, State.Hovered)
            .ShouldBe(Color.Indexed(1));
    }

    /// <summary>Verifies a tri-state checkbox resolves the indeterminate visual state.</summary>
    [Fact]
    public void IndeterminateCheckBox_ResolvesIndeterminateStyle()
    {
        Theme theme = new();
        ControlStyle<Control> style = new();
        style.Set(Control.ForegroundProperty, State.Indeterminate, Color.Indexed(5));
        theme.SetStyle(style);
        CheckBox box = new() { IsThreeState = true, IsChecked = null };
        ThemeTestSupport.ApplyTheme(box, theme);

        box.Foreground.ShouldBe(Color.Indexed(5));
    }

    /// <summary>Verifies a measure-impact overlay triggers a layout pass when its state activates.</summary>
    [Fact]
    public async Task Pressed_WhenMeasureOverlayDefined_InvalidatesMeasureAsync()
    {
        await using Dispatcher dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(
            () =>
            {
                ControlStyle<Control> style = ThemeTestSupport.CreateStyle<Control>();
                style.Set(Control.PaddingProperty, State.Pressed, new Thickness(2));
                ProbeControl control = new() { Style = style };
                control.Attach(dispatcher);
                control.Clear(Invalidation.All);

                control.SetPressed(true);

                control.Pending.ShouldBe(Invalidation.All);
            },
            TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a render-only overlay keeps state changes render-only (no layout thrash).</summary>
    [Fact]
    public async Task Pressed_WhenOnlyRenderOverlayDefined_InvalidatesRenderOnlyAsync()
    {
        await using Dispatcher dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(
            () =>
            {
                ControlStyle<Control> style = ThemeTestSupport.CreateStyle<Control>();
                style.Set(Control.ForegroundProperty, State.Pressed, Color.Indexed(3));
                ProbeControl control = new() { Style = style };
                control.Attach(dispatcher);
                control.Clear(Invalidation.All);

                control.SetPressed(true);

                control.Pending.ShouldBe(Invalidation.Render);
            },
            TestContext.Current.CancellationToken);
    }
}
