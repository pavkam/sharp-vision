// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies convenience CLR events on the base ControlBase class through a mounted
/// ComponentSurface, exercising ControlBase's routed-event plumbing.</summary>
public sealed class ControlBaseSurfaceTests
{
    /// <summary>Verifies the primary-only convenience event ignores every auxiliary button.</summary>
    [Theory]
    [InlineData(Buttons.Middle)]
    [InlineData(Buttons.Secondary)]
    [InlineData(Buttons.Back)]
    [InlineData(Buttons.Forward)]
    public void PointerPressed_WhenNonPrimaryButtonPressed_DoesNotFire(Buttons buttons)
    {
        // Arrange
        var fired = 0;
        var control = new Button { Bounds = new Rect(0, 0, 8, 3) };
        control.PointerPressed += (_, _) => fired++;
        var input = new PointerEventArgs(new Pointer(
            new Point(1, 1),
            pixels: null,
            buttons,
            PointerAction.Press,
            wheelX: 0,
            wheelY: 0,
            Modifiers.None,
            isMotion: false,
            isCellPositionInferred: false));

        // Act
        _ = Router.Route(control, Events.Pointer, input);

        // Assert
        fired.ShouldBe(0);
    }

    /// <summary>Verifies inherited events publish before handled concrete editing and navigation
    /// defaults across representative control families.</summary>
    [Fact]
    public void KeyDown_WhenConcreteDefaultHandlesInput_StillFiresExactlyOnce()
    {
        // Arrange
        var inputs = new (ControlBase Control, Stroke Stroke)[]
        {
            (new TextInput { Text = "ab" }, new Stroke(Code.Backspace, null, 0, Modifiers.None, KeyAction.Press)),
            (new UiCalendar(), new Stroke(Code.Right, null, 0, Modifiers.None, KeyAction.Press)),
            (new ScrollBar { Maximum = 10 }, new Stroke(Code.Down, null, 0, Modifiers.None, KeyAction.Press))
        };

        foreach (var (control, stroke) in inputs)
        {
            var fired = 0;
            control.KeyDown += (_, _) => fired++;

            // Act
            _ = Router.Route(control, Events.Key, new KeyEventArgs(stroke));

            // Assert
            fired.ShouldBe(1, control.GetType().Name);
        }
    }

    /// <summary>Verifies controls with self-contained defaults cannot accidentally omit the
    /// inherited key and primary-pointer convenience events.</summary>
    [Fact]
    public void InputEvents_WhenOverrideDoesNotCallBase_StillPublishExactlyOnce()
    {
        // Arrange
        ControlBase[] controls = [new Window(), new Popup(), new Menu(), new NavigationViewGroup()];

        foreach (var control in controls)
        {
            var keyDown = 0;
            var pointerPressed = 0;
            control.KeyDown += (_, _) => keyDown++;
            control.PointerPressed += (_, _) => pointerPressed++;

            // Act
            _ = Router.Route(
                control,
                Events.Key,
                new KeyEventArgs(new Stroke(Code.F1, null, 0, Modifiers.None, KeyAction.Press)));
            _ = Router.Route(
                control,
                Events.Pointer,
                new PointerEventArgs(new Pointer(
                    new Point(0, 0),
                    pixels: null,
                    Buttons.Primary,
                    PointerAction.Press,
                    wheelX: 0,
                    wheelY: 0,
                    Modifiers.None,
                    isMotion: false,
                    isCellPositionInferred: false)));

            // Assert
            keyDown.ShouldBe(1, control.GetType().Name);
            pointerPressed.ShouldBe(1, control.GetType().Name);
        }
    }

    /// <summary>Verifies the modeless Escape path still publishes the inherited event before its
    /// dialog-specific no-op default.</summary>
    [Fact]
    public void KeyDown_WhenModelessDialogReceivesEscape_FiresExactlyOnce()
    {
        // Arrange
        var control = new ModelessDialogProbe();
        var fired = 0;
        control.KeyDown += (_, _) => fired++;

        // Act
        _ = Router.Route(
            control,
            Events.Key,
            new KeyEventArgs(new Stroke(Code.Escape, null, 0, Modifiers.None, KeyAction.Press)));

        // Assert
        fired.ShouldBe(1);
    }

    /// <summary>Verifies a handled inherited key event suppresses both a direct pressable control
    /// and a composed press consumer.</summary>
    [Fact]
    public void KeyDown_WhenHandlerConsumesEnter_SuppressesEveryPressDefault()
    {
        // Arrange
        var buttonClicks = 0;
        var button = new Button();
        button.Click += (_, _) => buttonClicks++;
        button.KeyDown += (_, eventArgs) => eventArgs.IsHandled = true;
        var expander = new Expander { IsExpanded = false };
        expander.KeyDown += (_, eventArgs) => eventArgs.IsHandled = true;

        // Act
        _ = Router.Route(
            button,
            Events.Key,
            new KeyEventArgs(new Stroke(Code.Enter, null, 0, Modifiers.None, KeyAction.Press)));
        _ = Router.Route(
            expander,
            Events.Key,
            new KeyEventArgs(new Stroke(Code.Enter, null, 0, Modifiers.None, KeyAction.Press)));

        // Assert
        buttonClicks.ShouldBe(0);
        expander.IsExpanded.ShouldBeFalse();
    }

    /// <summary>Verifies a handled inherited pointer press prevents composed press defaults from
    /// arming or activating through the rest of the physical click.</summary>
    [Fact]
    public async Task PointerPressed_WhenHandlerConsumesPress_SuppressesEveryPressDefaultAsync()
    {
        // Arrange
        var buttonClicks = 0;
        var button = new Button { Text = "Button" };
        button.Click += (_, _) => buttonClicks++;
        button.PointerPressed += (_, eventArgs) => eventArgs.IsHandled = true;
        await using var buttonSurface = await ComponentSurface.MountAsync(
            button,
            new Size(12, 3),
            TestContext.Current.CancellationToken);

        var expander = new Expander { HeaderText = "Details", IsExpanded = false };
        expander.PointerPressed += (_, eventArgs) => eventArgs.IsHandled = true;
        await using var expanderSurface = await ComponentSurface.MountAsync(
            expander,
            new Size(16, 3),
            TestContext.Current.CancellationToken);

        // Act
        await buttonSurface.Pointer.ClickAsync(button);
        await expanderSurface.Pointer.ClickAsync(expander, new Point(1, 0));

        // Assert
        buttonClicks.ShouldBe(0);
        button.IsPressed.ShouldBeFalse();
        expander.IsExpanded.ShouldBeFalse();
        expander.IsPressed.ShouldBeFalse();
    }

    /// <summary>Verifies an auxiliary release cannot complete or cancel an armed primary click.</summary>
    [Fact]
    public async Task PointerRelease_WhenSecondaryArrivesDuringPrimaryHold_PreservesButtonGestureAsync()
    {
        // Arrange
        var clicks = 0;
        var button = new Button { Text = "Button" };
        button.Click += (_, _) => clicks++;
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(12, 3),
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(button);
        await surface.Pointer.PressAsync();
        var pressOrigin = surface.Application.Pointer.PressOrigin;

        // Act
        await surface.Pointer.ReleaseSecondaryWhilePrimaryHeldAsync();

        // Assert
        clicks.ShouldBe(0);
        button.IsPressed.ShouldBeTrue();
        button.HasPointerCapture.ShouldBeTrue();
        var expectedPressOrigin = pressOrigin.ShouldNotBeNull();
        surface.Application.Pointer.PressOrigin.ShouldBeSameAs(expectedPressOrigin);

        await surface.Pointer.ReleaseAsync();
        clicks.ShouldBe(1);
        button.IsPressed.ShouldBeFalse();
        button.HasPointerCapture.ShouldBeFalse();
    }

    /// <summary>Verifies PointerPressed fires when a primary press arrives.</summary>
    [Fact]
    public async Task PointerPressed_WhenPrimaryButtonPressed_FiresAsync()
    {
        // Arrange
        var fired = 0;
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Text = "OK"
        };
        button.PointerPressed += (_, _) => fired++;
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(8, 3),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.MoveToAsync(button);
        await surface.Pointer.PressAsync();

        // Assert
        fired.ShouldBeGreaterThanOrEqualTo(1);
    }

    /// <summary>Verifies PointerReleased fires on release.</summary>
    [Fact]
    public async Task PointerReleased_WhenPrimaryButtonReleased_FiresAsync()
    {
        // Arrange
        var fired = 0;
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Text = "OK"
        };
        button.PointerReleased += (_, _) => fired++;
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(8, 3),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.MoveToAsync(button);
        await surface.Pointer.PressAsync();
        await surface.Pointer.ReleaseAsync();

        // Assert
        fired.ShouldBeGreaterThanOrEqualTo(1);
    }

    /// <summary>Verifies PointerMoved fires on move.</summary>
    [Fact]
    public async Task PointerMoved_WhenPointerMoves_FiresAsync()
    {
        // Arrange
        var fired = 0;
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Text = "OK"
        };
        button.PointerMoved += (_, _) => fired++;
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(8, 3),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.MoveToAsync(button);

        // Assert
        fired.ShouldBeGreaterThanOrEqualTo(1);
    }

    /// <summary>Verifies KeyDown fires on key press.</summary>
    [Fact]
    public async Task KeyDown_WhenKeyIsPressed_FiresAsync()
    {
        // Arrange
        var fired = 0;
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            IsFocusable = true,
            Text = "OK"
        };
        button.KeyDown += (_, _) => fired++;
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(8, 3),
            TestContext.Current.CancellationToken);

        // Act — focus and press a key
        await surface.Pointer.ClickAsync(button);
        await surface.Keyboard.PressAsync(Code.Right);

        // Assert
        fired.ShouldBeGreaterThanOrEqualTo(1);
    }

    /// <summary>Verifies KeyUp fires on key release.</summary>
    [Fact]
    public async Task KeyUp_WhenKeyIsReleased_FiresAsync()
    {
        // Arrange
        var fired = 0;
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            IsFocusable = true,
            Text = "OK"
        };
        button.KeyUp += (_, _) => fired++;
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(8, 3),
            TestContext.Current.CancellationToken);

        // Act — focus and complete a character (which produces press+release)
        await surface.Pointer.ClickAsync(button);
        await surface.Keyboard.CompleteCharacterAsync(new Rune('x'));

        // Assert
        fired.ShouldBeGreaterThanOrEqualTo(1);
    }

    /// <summary>Verifies BoundsChanged fires after layout.</summary>
    [Fact]
    public async Task BoundsChanged_WhenControlIsArranged_FiresAsync()
    {
        // Arrange
        var fired = 0;
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Text = "OK"
        };
        button.BoundsChanged += (_, _) => fired++;

        // Act — mounting arranges the control, which sets Bounds from default
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(8, 3),
            TestContext.Current.CancellationToken);

        // Assert
        fired.ShouldBeGreaterThanOrEqualTo(1);
    }
}
