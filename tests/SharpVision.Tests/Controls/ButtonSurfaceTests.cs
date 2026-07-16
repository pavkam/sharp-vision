// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies Button appearance and interaction through a mounted terminal surface.</summary>
public sealed class ButtonSurfaceTests
{
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
        surface.ShouldHaveState(button, State.Normal);
        surface.ShouldRender("""
            ╭──────╮
            │Save  │
            ╰──────╯


            """);
        surface.Cell(new Point(0, 0)).Style.Foreground.ShouldBe(Color.Indexed(8));
        surface.Cell(new Point(8, 1)).Style.Attributes.ShouldBe(Attributes.Dim);
    }

    /// <summary>Verifies decoded pointer movement updates hover state, face styling, and final cells.</summary>
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
        surface.ShouldHaveState(button, State.Hovered);
        surface.ShouldRender("""
            ╭──────╮
            │Save  │
            ╰──────╯


            """);
        surface.Cell(new Point(0, 0)).Style.Foreground.ShouldBe(Color.Indexed(14));
        var contentForeground = surface.Cell(new Point(1, 1)).Style.Foreground;
        contentForeground.Kind.ShouldBe(ColorKind.Indexed);
        contentForeground.Red.ShouldBe((byte) 15);
        surface.Cell(new Point(8, 1)).Style.Foreground.ShouldBe(Color.Indexed(8));
        surface.Cell(new Point(8, 1)).Style.Attributes.ShouldBe(Attributes.Dim);
    }

    /// <summary>Verifies a held primary pointer translates the complete focused face into its shadow.</summary>
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
        surface.ShouldHaveState(button, State.Hovered | State.Focused | State.Pressed);
        surface.ShouldRender("""

             ╭──────╮
             │Save  │
             ╰──────╯

            """);
        surface.Cell(new Point(0, 0)).Text.ShouldBe(" ");
        surface.Cell(new Point(1, 1)).Text.ShouldBe("╭");
        surface.Cell(new Point(1, 1)).Style.Foreground.ShouldBe(Color.Indexed(14));
        surface.Cell(new Point(2, 2)).Text.ShouldBe("S");
        surface.Cell(new Point(2, 2)).Style.Foreground.ShouldBe(Color.Indexed(15));
        surface.Cell(new Point(8, 1)).Style.Attributes.ShouldNotBe(Attributes.Dim);
        surface.Cell(new Point(9, 2)).Style.Attributes.ShouldBe(Attributes.None);
        surface.Cell(new Point(2, 4)).Style.Attributes.ShouldBe(Attributes.None);
    }

    /// <summary>Verifies a decoded Tab key focuses the sole mounted Button and updates its cells.</summary>
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
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(10, 5),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert
        surface.ShouldHaveState(button, State.Focused);
        button.IsFocused.ShouldBeTrue();
        surface.ShouldRender("""
            ╭──────╮
            │Save  │
            ╰──────╯


            """);
        surface.Cell(new Point(0, 0)).Style.Foreground.ShouldBe(Color.Indexed(14));
        surface.Cell(new Point(1, 1)).Style.Foreground.ShouldBe(Color.Indexed(15));
        surface.Cell(new Point(8, 1)).Style.Attributes.ShouldBe(Attributes.Dim);
    }

    /// <summary>Verifies the click shorthand emits move, press, and release and settles activation.</summary>
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
        surface.ShouldHaveState(button, State.Hovered | State.Focused);
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
        surface.ShouldHaveState(button, State.Normal);
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
        surface.ShouldHaveState(button, State.Hovered | State.Focused | State.Pressed);
        surface.ShouldRender(PressedSnapshot(hasBorder, hasShadow));
        surface.Cell(new Point(9, 2)).Text.ShouldBe(" ");
        surface.Cell(new Point(9, 2)).Style.Attributes.ShouldBe(Attributes.None);
        surface.Cell(new Point(2, 4)).Text.ShouldBe(" ");
        surface.Cell(new Point(2, 4)).Style.Attributes.ShouldBe(Attributes.None);
    }

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
}
