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
        var theme = new Theme();
        var style = new ControlStyle<Control>();
        style.Set(Control.ForegroundProperty, State.Checked, Color.Indexed(1));
        style.Set(Control.ForegroundProperty, State.Selected, Color.Indexed(2));
        theme.SetStyle(style);
        var control = new ProbeControl();
        ThemeTestSupport.ApplyTheme(control, theme);

        control.SetSelectedState(true);

        control.Foreground.ShouldBe(Color.Indexed(2));
    }

    /// <summary>Verifies an unchecked checkbox in a selected row does not pick up checked styling.</summary>
    [Fact]
    public void UncheckedCheckBox_WhenRowSelected_DoesNotResolveCheckedStyle()
    {
        var theme = new Theme();
        var style = new ControlStyle<Control>();
        style.Set(Control.BackgroundProperty, State.Checked, Color.Indexed(1));
        style.Set(Control.BackgroundProperty, State.Selected, Color.Indexed(2));
        theme.SetStyle(style);
        var box = new CheckBox();
        ThemeTestSupport.ApplyTheme(box, theme);

        box.SetSelectedState(true);

        box.Background.ShouldBe(Color.Indexed(2));
    }

    /// <summary>Verifies a more specific combined-state definition wins over single-state ones.</summary>
    [Fact]
    public void Resolve_WhenCombinedStateDefined_WinsOverSingleStates()
    {
        var theme = new Theme();
        var style = new ControlStyle<Control>();
        style.Set(Control.ForegroundProperty, State.Hovered, Color.Indexed(1));
        style.Set(Control.ForegroundProperty, State.Focused, Color.Indexed(2));
        style.Set(Control.ForegroundProperty, State.Hovered | State.Focused, Color.Indexed(3));
        theme.SetStyle(style);
        var control = new ProbeControl();
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
        var theme = new Theme();
        var style = new ControlStyle<Control>();
        style.Set(Control.ForegroundProperty, State.Indeterminate, Color.Indexed(5));
        theme.SetStyle(style);
        var box = new CheckBox() { IsThreeState = true, IsChecked = null };
        ThemeTestSupport.ApplyTheme(box, theme);

        box.Foreground.ShouldBe(Color.Indexed(5));
    }

    /// <summary>Verifies a measure-impact overlay triggers a layout pass when its state activates.</summary>
    [Fact]
    public async Task Pressed_WhenMeasureOverlayDefined_InvalidatesMeasureAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(
            () =>
            {
                var style = ThemeTestSupport.CreateStyle<Control>();
                style.Set(Control.PaddingProperty, State.Pressed, new Thickness(2));
                var control = new ProbeControl() { Style = style };
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
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(
            () =>
            {
                var style = ThemeTestSupport.CreateStyle<Control>();
                style.Set(Control.ForegroundProperty, State.Pressed, Color.Indexed(3));
                var control = new ProbeControl() { Style = style };
                control.Attach(dispatcher);
                control.Clear(Invalidation.All);

                control.SetPressed(true);

                control.Pending.ShouldBe(Invalidation.Render);
            },
            TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies checked geometry resolves immediately after a warmed normal-state lookup.</summary>
    [Fact]
    public async Task Checked_WhenMeasureOverlayActivates_ClearsCacheAndInvalidatesMeasureAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(
            () =>
            {
                var style = ThemeTestSupport.CreateStyle<Control>();
                style.Set(Control.PaddingProperty, State.Checked, new Thickness(2));
                var control = new CheckBox() { Style = style };
                control.Attach(dispatcher);
                control.Padding.ShouldBe(default);
                control.Clear(Invalidation.All);

                control.IsChecked = true;

                control.Padding.ShouldBe(new Thickness(2));
                control.Pending.ShouldBe(Invalidation.All);
            },
            TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies indeterminate geometry resolves immediately after a warmed normal-state lookup.</summary>
    [Fact]
    public async Task Indeterminate_WhenMeasureOverlayActivates_ClearsCacheAndInvalidatesMeasureAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(
            () =>
            {
                var style = ThemeTestSupport.CreateStyle<Control>();
                style.Set(Control.PaddingProperty, State.Indeterminate, new Thickness(3));
                var control = new CheckBox() { IsThreeState = true, Style = style };
                control.Attach(dispatcher);
                control.Padding.ShouldBe(default);
                control.Clear(Invalidation.All);

                control.IsChecked = null;

                control.Padding.ShouldBe(new Thickness(3));
                control.Pending.ShouldBe(Invalidation.All);
            },
            TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies selected-state propagation clears resolved caches before impact calculation.</summary>
    [Fact]
    public async Task Selected_WhenMeasureOverlayActivates_ClearsCacheAndInvalidatesMeasureAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(
            () =>
            {
                var style = ThemeTestSupport.CreateStyle<Control>();
                style.Set(Control.PaddingProperty, State.Selected, new Thickness(4));
                var control = new ProbeControl() { Style = style };
                control.Attach(dispatcher);
                control.Padding.ShouldBe(default);
                control.Clear(Invalidation.All);

                control.SetSelectedState(true);

                control.Padding.ShouldBe(new Thickness(4));
                control.Pending.ShouldBe(Invalidation.All);
            },
            TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a render-only checked overlay stays render-only and equivalent state is quiet.</summary>
    [Fact]
    public async Task Checked_WhenRenderOverlayActivates_InvalidatesOnlyOnceAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(
            () =>
            {
                var style = ThemeTestSupport.CreateStyle<Control>();
                style.Set(Control.ForegroundProperty, State.Checked, Color.Indexed(3));
                var control = new CheckBox() { Style = style };
                var notifications = 0;
                control.PropertyChanged += (_, eventArgs) =>
                {
                    if (eventArgs.PropertyName == nameof(CheckBox.IsChecked))
                    {
                        notifications++;
                    }
                };
                control.Attach(dispatcher);
                _ = control.Foreground;
                control.Clear(Invalidation.All);

                control.IsChecked = true;

                control.Pending.ShouldBe(Invalidation.Render);
                (control.Foreground == Color.Indexed(3)).ShouldBeTrue();
                control.Clear(Invalidation.All);
                control.IsChecked = true;
                control.Pending.ShouldBe(Invalidation.None);
                notifications.ShouldBe(1);
            },
            TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies attached equivalent visual-state assignment still checks dispatcher access.</summary>
    [Fact]
    public async Task Checked_WhenEquivalentAssignmentIsOffDispatcher_ThrowsBeforeObservationAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var control = new CheckBox();
        await dispatcher.InvokeAsync(
            () =>
            {
                control.Attach(dispatcher);
                control.IsChecked = true;
            },
            TestContext.Current.CancellationToken);

        _ = Should.Throw<InvalidOperationException>(() => control.IsChecked = true);

        control.IsChecked.ShouldBe(true);
    }
}
