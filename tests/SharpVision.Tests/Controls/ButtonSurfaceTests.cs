// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies Button appearance and interaction through a mounted terminal surface.</summary>
public sealed class ButtonSurfaceTests
{
    #region Interaction surfaces

    /// <summary>Verifies initial layout, normal styling, and the detached composite shadow.</summary>
    [Fact]
    public async Task Render_WhenButtonIsMounted_ShowsNormalFaceAndCompositeShadowAsync()
    {
        // Arrange
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(8),
            Height = Length.Cells(3),
            Content = new ControlText("Save"),
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(10, 5),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldHaveState(button, VisualState.Normal);
        surface.ShouldRender("""
            ╭──────╮
            │Save  │
            ╰──────╯


            """);
        Themes.Dark.TryGetColor(ColorRole.Border, out var borderColor).ShouldBeTrue();
        surface.Cell(new Point(0, 0)).Style.Foreground.ShouldBe(borderColor);
        surface.Cell(new Point(8, 1)).Style.Attributes.ShouldBe(Attributes.Dim);
    }

    /// <summary>Verifies snapshots and cells preserve a wide Button content grapheme.</summary>
    [Fact]
    public async Task Render_WhenButtonContentIsWide_PreservesSurfaceCellGeometryAsync()
    {
        // Arrange
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(6),
            Height = Length.Cells(3),
            Content = new ControlText("界"),
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(8, 5),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("""
            ╭────╮
            │界  │
            ╰────╯


            """);
        var lead = surface.Cell(new Point(1, 1));
        lead.Text.ShouldBe("界");
        lead.Width.ShouldBe(2);
        var continuation = surface.Cell(new Point(2, 1));
        continuation.IsContinuation.ShouldBeTrue();
        continuation.LeadX.ShouldBe(1);
    }

    /// <summary>Verifies decoded pointer movement updates hover state, face styling, and final cells.</summary>
    [ComponentBehaviorEvidence(typeof(Button), ComponentBehavior.Mounted | ComponentBehavior.Hover)]
    [Fact]
    public async Task Pointer_WhenMovedOverButton_ShowsHoveredAppearanceAsync()
    {
        // Arrange
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(8),
            Height = Length.Cells(3),
            Content = new ControlText("Save"),
        };
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(10, 5),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.MoveToAsync(button);

        // Assert
        surface.ShouldHaveState(button, VisualState.PointerOver);
        surface.ShouldRender("""
            ╭──────╮
            │Save  │
            ╰──────╯


            """);
        surface.Cell(new Point(0, 0)).Style.Foreground.ShouldBe(Color.Indexed(14));
        var contentForeground = surface.Cell(new Point(1, 1)).Style.Foreground;
        contentForeground.Kind.ShouldBe(ColorKind.Indexed);
        contentForeground.Red.ShouldBe((byte) 14);
        surface.Cell(new Point(8, 1)).Style.Foreground.ShouldBe(Color.Indexed(15));
        surface.Cell(new Point(8, 1)).Style.Attributes.ShouldBe(Attributes.Dim);
    }

    /// <summary>Verifies disabling focus eligibility also removes the built-in interactive hover treatment.</summary>
    [Fact]
    public async Task Pointer_WhenButtonIsNotFocusable_PreservesNormalAppearanceAsync()
    {
        // Arrange
        var button = new Button
        {
            Focusable = false,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(8),
            Height = Length.Cells(3),
            Content = new ControlText("Save"),
        };
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(10, 5),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.MoveToAsync(button);

        // Assert
        surface.ShouldHaveState(button, VisualState.PointerOver);
        button.CanFocus.ShouldBeFalse();
        Themes.Dark.TryGetColor(ColorRole.Border, out var borderColor).ShouldBeTrue();
        surface.Cell(default).Style.Foreground.ShouldBe(borderColor);
        surface.Cell(new Point(1, 1)).Style.Foreground.ShouldBe(Color.Indexed(15));
    }

    /// <summary>Verifies a held primary pointer translates the complete focused face into its shadow.</summary>
    [ComponentBehaviorEvidence(
        typeof(Button),
        ComponentBehavior.Focus |
        ComponentBehavior.PressRelease |
        ComponentBehavior.UnavailableCleanup)]
    [Fact]
    public async Task Pointer_WhenPrimaryButtonIsHeld_ShowsPressedTranslatedFaceAsync()
    {
        // Arrange
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(8),
            Height = Length.Cells(3),
            Content = new ControlText("Save"),
        };
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(10, 5),
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(button);

        // Act
        await surface.Pointer.PressAsync();

        // Assert
        surface.ShouldHaveState(button, VisualState.PointerOver | VisualState.Focused | VisualState.Pressed);
        surface.ShouldRender("""

             ╭──────╮
             │Save  │
             ╰──────╯

            """);
        surface.Cell(new Point(0, 0)).Text.ShouldBe(" ");
        surface.Cell(new Point(1, 1)).Text.ShouldBe("╭");
        surface.Cell(new Point(1, 1)).Style.Foreground.ShouldBe(Color.Indexed(14));
        surface.Cell(new Point(2, 2)).Text.ShouldBe("S");
        surface.Cell(new Point(2, 2)).Style.Foreground.ShouldBe(Color.Indexed(14));
        surface.Cell(new Point(8, 1)).Style.Attributes.ShouldNotBe(Attributes.Dim);
        surface.Cell(new Point(9, 2)).Style.Attributes.ShouldBe(Attributes.None);
        surface.Cell(new Point(2, 4)).Style.Attributes.ShouldBe(Attributes.None);

        // Act unavailable while held
        await surface.UpdateAsync(() => button.IsEnabled = false, "disable held Button");

        // Assert unavailable cleanup
        button.IsPressed.ShouldBeFalse();
        button.IsFocused.ShouldBeFalse();
        surface.ShouldHaveCapture(null);
        surface.ShouldHaveFocus(null);
        surface.ShouldHaveState(button, VisualState.Disabled);
    }

    /// <summary>Verifies a decoded Tab key focuses the sole mounted Button and updates its cells.</summary>
    [ComponentBehaviorEvidence(
        typeof(Button),
        ComponentBehavior.Tab |
        ComponentBehavior.DirectionalExcluded)]
    [Fact]
    public async Task Keyboard_WhenTabIsPressed_ShowsFocusedAppearanceAsync()
    {
        // Arrange
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(8),
            Height = Length.Cells(3),
            Content = new ControlText("Save"),
        };
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(10, 5),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Right);

        // Assert
        surface.ShouldHaveState(button, VisualState.Focused);
        button.IsFocused.ShouldBeTrue();
        clicks.ShouldBe(0);
        surface.ShouldRender("""
            ╭──────╮
            │Save  │
            ╰──────╯


            """);
        surface.Cell(new Point(0, 0)).Style.Foreground.ShouldBe(Color.Indexed(14));
        surface.Cell(new Point(1, 1)).Style.Foreground.ShouldBe(Color.Indexed(14));
        surface.Cell(new Point(8, 1)).Style.Attributes.ShouldBe(Attributes.Dim);
    }

    /// <summary>Verifies the click shorthand emits move, press, and release and settles activation.</summary>
    [ComponentBehaviorEvidence(typeof(Button), ComponentBehavior.Activation)]
    [Fact]
    public async Task Pointer_WhenButtonIsClicked_ReleasesAndActivatesOnceAsync()
    {
        // Arrange
        var clicks = 0;
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(8),
            Height = Length.Cells(3),
            Content = new ControlText("Save"),
        };
        button.Click += (_, _) => clicks++;
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(10, 5),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.ClickAsync(button);

        // Assert
        clicks.ShouldBe(1);
        surface.ShouldHaveState(button, VisualState.PointerOver | VisualState.Focused);
        surface.ShouldRender("""
            ╭──────╮
            │Save  │
            ╰──────╯


            """);
        surface.Cell(new Point(0, 0)).Style.Foreground.ShouldBe(Color.Indexed(14));
        surface.Cell(new Point(8, 1)).Style.Attributes.ShouldBe(Attributes.Dim);
    }

    /// <summary>Verifies every border/shadow combination has deterministic released and held geometry.</summary>
    /// <param name="hasBorder">Whether the Button reserves and renders its physical border.</param>
    /// <param name="hasShadow">Whether the Button renders a detached block shadow.</param>
    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task Pointer_WhenChromeCombinationIsPressed_RendersExpectedGeometryAsync(
        bool hasBorder,
        bool hasShadow)
    {
        // Arrange
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(8),
            Height = Length.Cells(3),
            BorderThickness = hasBorder ? new Thickness(1) : default,
            HasShadow = hasShadow,
            ShadowMode = ShadowMode.BlockGlyph,
            ShadowGlyph = new Rune('▓'),
            Content = new ControlText("Save"),
        };
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(10, 5),
            TestContext.Current.CancellationToken);

        // Act and assert released appearance
        surface.ShouldHaveState(button, VisualState.Normal);
        surface.ShouldRender(ReleasedSnapshot(hasBorder, hasShadow));

        if (hasShadow)
        {
            surface.Cell(new Point(8, 1)).Text.ShouldBe("▓");
            surface.Cell(new Point(8, 1)).Style.Attributes.ShouldBe(Attributes.Dim);
        }
        else
        {
            surface.Cell(new Point(8, 1)).Text.ShouldBe(" ");
            surface.Cell(new Point(8, 1)).Style.Attributes.ShouldBe(Attributes.None);
        }

        await surface.Pointer.MoveToAsync(button);
        await surface.Pointer.PressAsync();

        // Assert held appearance
        surface.ShouldHaveState(button, VisualState.PointerOver | VisualState.Focused | VisualState.Pressed);
        surface.ShouldRender(PressedSnapshot(hasBorder, hasShadow));
        surface.Cell(new Point(9, 2)).Text.ShouldBe(" ");
        surface.Cell(new Point(9, 2)).Style.Attributes.ShouldBe(Attributes.None);
        surface.Cell(new Point(2, 4)).Text.ShouldBe(" ");
        surface.Cell(new Point(2, 4)).Style.Attributes.ShouldBe(Attributes.None);
    }

    #endregion

    #region Snapshot fixtures

    private static string ReleasedSnapshot(bool hasBorder, bool hasShadow) =>
        (hasBorder, hasShadow) switch
        {
            (true, true) => """
                ╭──────╮
                │Save  │▓
                ╰──────╯▓
                  ▓▓▓▓▓▓▓

                """,
            (true, false) => """
                ╭──────╮
                │Save  │
                ╰──────╯


                """,
            (false, true) => """
                Save
                        ▓
                        ▓
                  ▓▓▓▓▓▓▓

                """,
            (false, false) => """
                Save




                """,
        };

    private static string PressedSnapshot(bool hasBorder, bool hasShadow) =>
        (hasBorder, hasShadow) switch
        {
            (true, true) => """

                 ╭──────╮
                 │Save  │
                 ╰──────╯

                """,
            (true, false) => """
                ╭──────╮
                │Save  │
                ╰──────╯


                """,
            (false, true) => """

                 Save



                """,
            (false, false) => """
                Save




                """,
        };

    #endregion
}
