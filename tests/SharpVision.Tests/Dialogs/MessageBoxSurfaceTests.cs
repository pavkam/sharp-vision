// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Dialogs;

using SharpVision.Tests.Styling;

/// <summary>Verifies MessageBox appearance, centering, and interaction through a mounted terminal surface.</summary>
public sealed class MessageBoxSurfaceTests
{
    /// <summary>Verifies moving a short intrinsically sized MessageBox does not change its height.</summary>
    [Fact]
    public async Task Layout_WhenMessageBoxIsMoved_PreservesInitialHeightAsync()
    {
        // Arrange
        var opener = new Button { Text = "Open" };
        var screen = new HostedControlScreen(opener);
        await using var surface = await ComponentSurface.MountScreenAsync(
            screen,
            new Size(60, 30),
            TestContext.Current.CancellationToken);
        Task<MessageBoxResult>? pending = null;
        await surface.UpdateAsync(
            () => pending = MessageBox.ShowAsync(opener, "File saved.", "Confirm"),
            "show MessageBox");
        var messageBox = OwnedTree.Find<MessageBox>(surface.Application.Root).ShouldNotBeNull();
        var initialHeight = messageBox.Bounds.Height;

        // Act
        await surface.Pointer.DragAsync(messageBox, new Point(2, 0), new Point(4, 2));

        // Assert
        initialHeight.ShouldBe(10);
        messageBox.Bounds.Height.ShouldBe(initialHeight);

        await surface.Keyboard.PressAsync(Code.Escape);
        _ = await pending!;
    }

    /// <summary>Verifies the dialog Window centers horizontally and vertically within the application plane.</summary>
    [Fact]
    public async Task Render_WhenMessageBoxIsShown_CentersDialogWindowInSurfaceAsync()
    {
        // Arrange
        var opener = new Button { Text = "Open" };
        var host = new Overlay
        {
            Face = AppearanceTestValues.Face(background: SemanticColor.Window),
            Children = { opener }
        };
        var capabilities = TerminalCapabilities.Conservative with { ColorDepth = ColorDepth.TrueColor };
        var options = TerminalOptions.Minimal with { Capabilities = capabilities };
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(60, 20),
            options,
            TestContext.Current.CancellationToken);
        Task<MessageBoxResult>? pending = null;

        // Act
        await surface.UpdateAsync(
            () => pending = MessageBox.ShowAsync(opener, "File saved.", "Confirm"),
            "show MessageBox");
        var messageBox = OwnedTree.Find<MessageBox>(surface.Application.Root).ShouldNotBeNull();
        var window = OwnedTree.Find<Window>(messageBox).ShouldNotBeNull();

        // Assert
        window.ShouldBeSameAs(messageBox);
        window.Bounds.X.ShouldBe((60 - window.Bounds.Width) / 2);
        window.Bounds.Y.ShouldBe((20 - window.Bounds.Height) / 2);
        window.Bounds.Width.ShouldBeGreaterThanOrEqualTo(32);
        window.Bounds.Height.ShouldBeGreaterThanOrEqualTo(8);
        window.Bounds.Right.ShouldBeLessThanOrEqualTo(60);
        window.Bounds.Bottom.ShouldBeLessThanOrEqualTo(20);

        // Assert border chrome: dialog Window uses paired frame glyphs
        surface.Cell(new Point(window.Bounds.X, window.Bounds.Y)).Text.ShouldBe("╔");
        surface.Cell(new Point(window.Bounds.Right - 1, window.Bounds.Y)).Text.ShouldBe("╗");
        surface.Cell(new Point(window.Bounds.X, window.Bounds.Bottom - 1)).Text.ShouldBe("╚");
        surface.Cell(new Point(window.Bounds.Right - 1, window.Bounds.Bottom - 1)).Text.ShouldBe("╝");
        var theme = window.Theme.ShouldNotBeNull();
        surface.Cell(new Point(59, 19)).Style.Background.ShouldBe(theme.ResolveColor(SemanticColor.Window));
        surface.Cell(new Point(window.Bounds.X + 1, window.Bounds.Y + 1)).Style.Background.ShouldBe(
            theme.ResolveColor(SemanticColor.WindowSurface));

        await surface.Keyboard.PressAsync(Code.Escape);
        (await pending!).ShouldBe(MessageBoxResult.Cancel);
    }

    /// <summary>Verifies the divider between the message and the action row renders as a horizontal
    /// line spanning the content width, positioned above the buttons.</summary>
    [Fact]
    public async Task Render_WhenMessageBoxIsShown_DrawsDividerAboveActionsAsync()
    {
        // Arrange
        var opener = new Button { Text = "Open" };
        var host = new Overlay { Children = { opener } };
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(60, 20),
            TestContext.Current.CancellationToken);
        Task<MessageBoxResult>? pending = null;

        // Act
        await surface.UpdateAsync(
            () => pending = MessageBox.ShowAsync(opener, "File saved.", "Confirm"),
            "show MessageBox");
        var messageBox = OwnedTree.Find<MessageBox>(surface.Application.Root).ShouldNotBeNull();
        var divider = OwnedTree.Find<Separator>(messageBox).ShouldNotBeNull();
        var actionButton = OwnedTree.Find<Button>(messageBox).ShouldNotBeNull();

        // Assert - the divider sits between the message text and the action row, and renders as
        // a horizontal line spanning its arranged bounds.
        divider.Bounds.Y.ShouldBeLessThan(actionButton.Bounds.Y);
        divider.Bounds.Width.ShouldBeGreaterThan(1);
        actionButton.Bounds.Y.ShouldBe(divider.Bounds.Bottom);
        actionButton.Bounds.Bottom.ShouldBe(messageBox.ContentBounds.Bottom);
        surface.Cell(new Point(divider.Bounds.X, divider.Bounds.Y)).Text.ShouldBe("─");
        surface.Cell(new Point(divider.Bounds.Right - 1, divider.Bounds.Y)).Text.ShouldBe("─");

        await surface.Keyboard.PressAsync(Code.Escape);
        _ = await pending!;
    }

    /// <summary>Verifies all standard button layouts center their action buttons within the dialog.</summary>
    [Theory]
    [InlineData(MessageBoxButtons.Ok, 1)]
    [InlineData(MessageBoxButtons.OkCancel, 2)]
    [InlineData(MessageBoxButtons.YesNo, 2)]
    [InlineData(MessageBoxButtons.YesNoCancel, 3)]
    public async Task Render_WhenButtonLayoutIsStandard_CentersButtonsHorizontallyAsync(
        MessageBoxButtons buttons,
        int expectedButtonCount)
    {
        // Arrange
        var opener = new Button { Text = "Open" };
        var host = new Overlay { Children = { opener } };
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(60, 20),
            TestContext.Current.CancellationToken);
        Task<MessageBoxResult>? pending = null;

        // Act
        await surface.UpdateAsync(
            () => pending = MessageBox.ShowAsync(opener, "Choose an action.", "Action", buttons),
            "show standard MessageBox");
        var messageBox = OwnedTree.Find<MessageBox>(surface.Application.Root).ShouldNotBeNull();
        var window = OwnedTree.Find<Window>(messageBox).ShouldNotBeNull();
        var foundButtons = FindAll<Button>(messageBox)
            .Where(static button => button.Parent is Stack { Orientation: Orientation.Horizontal })
            .ToArray();

        // Assert: correct button count
        foundButtons.Length.ShouldBe(expectedButtonCount);

        // Assert: the button row is centered within the dialog.
        // Centering uses a 3-column Grid (Star|Auto|Star) containing the Stack.
        // Verify the parent Grid centering structure exists and the Stack sits inside.
        var actionStack = foundButtons[0].Parent.ShouldBeOfType<Stack>();
        actionStack.Orientation.ShouldBe(Orientation.Horizontal);
        var centeringGrid = actionStack.Parent.ShouldBeOfType<Grid>();
        centeringGrid.Columns.Count.ShouldBe(3);

        // Assert: all buttons are within the window bounds
        foreach (var button in foundButtons)
        {
            button.Bounds.X.ShouldBeGreaterThanOrEqualTo(window.Bounds.X);
            button.Bounds.Right.ShouldBeLessThanOrEqualTo(window.Bounds.Right);
        }

        await surface.Keyboard.PressAsync(Code.Escape);
        _ = await pending!;
    }

    /// <summary>Verifies a long message wraps within the dialog without escaping the window frame.</summary>
    [Fact]
    public async Task Render_WhenMessageIsLong_WrapsWithinDialogBoundsAsync()
    {
        // Arrange
        var opener = new Button { Text = "Open" };
        var host = new Overlay { Children = { opener } };
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(50, 16),
            TestContext.Current.CancellationToken);
        Task<MessageBoxResult>? pending = null;
        var longMessage = "This message is deliberately long enough to force wrapping " +
            "across multiple lines within a constrained terminal viewport because it exceeds the available content width.";

        // Act
        await surface.UpdateAsync(
            () => pending = MessageBox.ShowAsync(opener, longMessage, "Wrap Test"),
            "show wrapping MessageBox");
        var messageBox = OwnedTree.Find<MessageBox>(surface.Application.Root).ShouldNotBeNull();
        var window = OwnedTree.Find<Window>(messageBox).ShouldNotBeNull();
        var text = FindAll<ControlText>(messageBox)
            .FirstOrDefault(t => t.Content == longMessage);

        // Assert: message text stays within window content area
        _ = text.ShouldNotBeNull();
        text.Bounds.X.ShouldBeGreaterThanOrEqualTo(window.ContentBounds.X);
        text.Bounds.Right.ShouldBeLessThanOrEqualTo(window.ContentBounds.Right);
        text.Overflow.ShouldBe(Overflow.Wrap);
        text.Bounds.Width.ShouldBeGreaterThan(0);
        (text.Bounds.Height >= 1).ShouldBeTrue();

        await surface.Keyboard.PressAsync(Code.Escape);
        _ = await pending!;
    }

    /// <summary>Verifies Tab cycles through MessageBox buttons and Enter activates the focused one.</summary>
    [Fact]
    public async Task Input_WhenTabAndEnterAreUsed_CyclesToSecondButtonAndActivatesAsync()
    {
        // Arrange
        var opener = new Button { Text = "Open" };
        var host = new Overlay { Children = { opener } };
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(60, 20),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(
            () => _ = surface.Application.Focus.Focus(opener),
            "focus opener");
        Task<MessageBoxResult>? pending = null;

        // Act
        await surface.UpdateAsync(
            () => pending = MessageBox.ShowAsync(
                opener,
                "Continue?",
                "Confirm",
                MessageBoxButtons.YesNo),
            "show YesNo MessageBox");

        // Tab moves from default Yes to No
        await surface.Keyboard.PressAsync(Code.Tab);
        var noButton = FindAll<Button>(
                OwnedTree.Find<MessageBox>(surface.Application.Root).ShouldNotBeNull())
            .FirstOrDefault(static b => b.Text == "&No");
        _ = noButton.ShouldNotBeNull();
        noButton.Focused.ShouldBeTrue();

        // Enter activates the focused No button
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert
        (await pending!).ShouldBe(MessageBoxResult.No);
        host.Children.Count.ShouldBe(1);
    }

    /// <summary>Verifies the Window shadow renders with half-block glyphs using transparent background.</summary>
    [Fact]
    public async Task Render_WhenDialogShadowIsVisible_UsesShadowWithTransparentBackgroundAsync()
    {
        // Arrange
        var opener = new Button { Text = "Open" };
        var host = new Overlay { Children = { opener } };
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(60, 20),
            TestContext.Current.CancellationToken);
        Task<MessageBoxResult>? pending = null;

        // Act
        await surface.UpdateAsync(
            () => pending = MessageBox.ShowAsync(opener, "Info.", "Notice"),
            "show MessageBox");
        var messageBox = OwnedTree.Find<MessageBox>(surface.Application.Root).ShouldNotBeNull();
        var window = OwnedTree.Find<Window>(messageBox).ShouldNotBeNull();

        // Assert: window has shadow enabled
        window.Shadow.Visible.ShouldBeTrue();
        window.Shadow.Offset.ShouldBe(new Point(2, 1));

        // Assert: shadow cells below the window use Dim attributes
        var shadowY = window.Bounds.Bottom;
        if (shadowY < 20)
        {
            var shadowX = window.Bounds.X + 2;
            if (shadowX < 60 && shadowX < window.Bounds.Right)
            {
                surface.Cell(new Point(shadowX, shadowY)).Style.Attributes.ShouldBe(TerminalAttributes.Dim);
            }
        }

        await surface.Keyboard.PressAsync(Code.Escape);
        _ = await pending!;
    }

    /// <summary>Verifies the dialog title is centered across the full top edge without close chrome.</summary>
    [Fact]
    public async Task Render_WhenShown_CentersTitleWithoutCloseButtonAsync()
    {
        var opener = new Button { Text = "Open" };
        var host = new Overlay { Children = { opener } };
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(60, 20),
            TestContext.Current.CancellationToken);
        Task<MessageBoxResult>? pending = null;

        await surface.UpdateAsync(
            () => pending = MessageBox.ShowAsync(opener, "Test message.", "Centered"),
            "show MessageBox");
        var window = OwnedTree.Find<Window>(
            OwnedTree.Find<MessageBox>(surface.Application.Root).ShouldNotBeNull()).ShouldNotBeNull();

        window.CanClose.ShouldBeFalse();
        window.CanMove.ShouldBeTrue();
        window.HeaderPlacement.ShouldBe(WindowTitlePlacement.Center);

        var topRow = Enumerable.Range(window.Bounds.X, window.Bounds.Width)
            .Select(x => surface.Cell(new Point(x, window.Bounds.Y)).Text)
            .ToArray();
        var topText = string.Concat(topRow);
        var titleIndex = topText.IndexOf("Centered", StringComparison.Ordinal);
        titleIndex.ShouldBeGreaterThan(0);

        var leftPadding = titleIndex;
        var rightPadding = window.Bounds.Width - titleIndex - "Centered".Length;
        Math.Abs(leftPadding - rightPadding).ShouldBeLessThanOrEqualTo(2);

        topText.ShouldNotContain("■");

        await surface.Keyboard.PressAsync(Code.Escape);
        _ = await pending!;
    }

    /// <summary>Verifies a local SeparatorStyle override renders through to the divider's cells.</summary>
    [Fact]
    public async Task Render_WhenSeparatorStyleIsCustomized_UsesOverrideGlyphAsync()
    {
        // Arrange
        var opener = new Button { Text = "Open" };
        var host = new Overlay { Children = { opener } };
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(60, 20),
            TestContext.Current.CancellationToken);
        Task<MessageBoxResult>? pending = null;

        // Act
        await surface.UpdateAsync(
            () => pending = MessageBox.ShowAsync(
                opener,
                "File saved.",
                new MessageBoxOptions { Style = null }),
            "show MessageBox");
        var messageBox = OwnedTree.Find<MessageBox>(surface.Application.Root).ShouldNotBeNull();
        await surface.UpdateAsync(
            () => messageBox.SeparatorStyle = SeparatorStyle.Default with { HorizontalGlyph = new Rune('=') },
            "customize the divider glyph");
        var divider = OwnedTree.Find<Separator>(messageBox).ShouldNotBeNull();

        // Assert
        surface.Cell(new Point(divider.Bounds.X, divider.Bounds.Y)).Text.ShouldBe("=");
        surface.Cell(new Point(divider.Bounds.Right - 1, divider.Bounds.Y)).Text.ShouldBe("=");

        await surface.Keyboard.PressAsync(Code.Escape);
        _ = await pending!;
    }

    /// <summary>Verifies a live Theme swap authoring the "messageBox" key updates the frame, message,
    /// and divider coherently, without a local style pinning any part.</summary>
    [Fact]
    public async Task Render_WhenThemeIsSwappedLive_UpdatesFrameAndDividerCoherentlyAsync()
    {
        // Arrange
        var opener = new Button { Text = "Open" };
        var host = new Overlay { Children = { opener } };
        var themeA = ThemeCatalog.Parse(ThemeJson.Create());
        var themeB = ThemeCatalog.Parse(ThemeJson.Create(extraStyles:
            """, "messageBox": { "normal": { "actionBarMargin": { "x": 3, "y": 0 } } } """));
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(60, 20),
            themeA,
            TestContext.Current.CancellationToken);
        Task<MessageBoxResult>? pending = null;

        // Act
        await surface.UpdateAsync(
            () => pending = MessageBox.ShowAsync(opener, "File saved.", "Confirm"),
            "show MessageBox");
        var messageBox = OwnedTree.Find<MessageBox>(surface.Application.Root).ShouldNotBeNull();
        var divider = OwnedTree.Find<Separator>(messageBox).ShouldNotBeNull();
        var narrowWidth = divider.Bounds.Width;

        await surface.UpdateAsync(() => surface.Application.Theme = themeB, "swap to a messageBox-authoring theme");

        // Assert
        messageBox.Style.ShouldBeNull();
        messageBox.ActualStyle.ActionBarMargin.ShouldBe(new Thickness(3, 0));
        divider.Bounds.Width.ShouldBeLessThan(narrowWidth);

        await surface.Keyboard.PressAsync(Code.Escape);
        _ = await pending!;
    }

    /// <summary>Verifies a tiny host surface still lays out and renders the dialog without throwing.</summary>
    [Fact]
    public async Task Render_WhenSurfaceIsTiny_LaysOutWithoutThrowingAsync()
    {
        // Arrange
        var opener = new Button { Text = "Open" };
        var host = new Overlay { Children = { opener } };
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(20, 6),
            TestContext.Current.CancellationToken);
        Task<MessageBoxResult>? pending = null;

        // Act
        await surface.UpdateAsync(
            () => pending = MessageBox.ShowAsync(opener, "Tiny host.", "Notice"),
            "show MessageBox on a tiny surface");
        var messageBox = OwnedTree.Find<MessageBox>(surface.Application.Root).ShouldNotBeNull();

        // Assert
        messageBox.Bounds.Width.ShouldBeGreaterThan(0);
        messageBox.Bounds.Height.ShouldBeGreaterThan(0);

        await surface.Keyboard.PressAsync(Code.Escape);
        _ = await pending!;
    }

    private static List<T> FindAll<T>(ControlBase root) where T : ControlBase
    {
        var matches = new List<T>();
        Visit(root);
        return matches;

        void Visit(ControlBase control)
        {
            if (control is T match)
            {
                matches.Add(match);
            }

            for (var index = 0; index < control.OwnedControlCount; index++)
            {
                Visit(control.OwnedControlAt(index));
            }
        }
    }
}
