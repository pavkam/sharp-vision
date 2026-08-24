// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Dialogs;

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

    /// <summary>Verifies a message long enough to exceed the message row's own height ceiling
    /// scrolls within the message area instead of crushing the action bar's divider and buttons,
    /// the inverse of what <see cref="Render_WhenMessageIsLong_WrapsWithinDialogBoundsAsync"/>
    /// checks - that test only asserts the message stays visible, never the action bar. Capping
    /// the message row's own maximum (rather than floor-protecting the action-bar row) means the
    /// action bar is never actually forced to compete for space at all, so it keeps its full,
    /// natural, bordered-Button size - not merely a nonzero one.</summary>
    [Fact]
    public async Task Render_WhenMessageExceedsHeightBudget_KeepsActionBarVisibleAsync()
    {
        // Arrange
        var opener = new Button { Text = "Open" };
        var host = new Overlay { Children = { opener } };
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(40, 24),
            TestContext.Current.CancellationToken);
        Task<MessageBoxResult>? pending = null;

        // A narrow host keeps the wrapped line count high per character, so a message of an
        // ordinary detailed error/validation length (roughly 700-900 characters, not an extreme
        // case) reliably wraps into far more lines than the message row's own height ceiling
        // (10, see MessageBox.CreateContent) can hold without scrolling.
        var longMessage = string.Concat(Enumerable.Repeat(
            "The requested operation could not be completed because several validation rules " +
            "failed, and each failing rule is described here in enough detail for the operator " +
            "to diagnose it. ",
            5));

        // Act
        await surface.UpdateAsync(
            () => pending = MessageBox.ShowAsync(opener, longMessage, "Validation Failed", MessageBoxButtons.OkCancel),
            "show a MessageBox with a message taller than its row's height ceiling");
        var messageBox = OwnedTree.Find<MessageBox>(surface.Application.Root).ShouldNotBeNull();
        var window = OwnedTree.Find<Window>(messageBox).ShouldNotBeNull();
        var messageText = OwnedTree.Find<ControlText>(messageBox).ShouldNotBeNull();
        var messageHost = messageText.Parent.ShouldBeOfType<Stack>();
        var buttons = FindAll<Button>(messageBox)
            .Where(static button => button.Parent is Stack { Orientation: Orientation.Horizontal })
            .ToArray();
        var separator = OwnedTree.Find<Separator>(messageBox).ShouldNotBeNull();

        // Assert: the window still respects MaxHeight, and the message row's own ceiling actually
        // bound - its scrollable viewport is capped well below the message's full wrapped height,
        // confirming this exercises the scroll path rather than a box that simply grew to fit.
        window.Bounds.Height.ShouldBeLessThanOrEqualTo(20);
        messageHost.Bounds.Height.ShouldBeLessThanOrEqualTo(10);
        messageText.Bounds.Height.ShouldBeGreaterThan(messageHost.Bounds.Height);

        // Assert: because the message row's own ceiling - not a floor on the action-bar row -
        // is what keeps this dialog within budget, the action bar never had to compete for space
        // at all and keeps its full, natural size. A merely non-zero height would not be enough
        // evidence for a bordered button (this MessageBox uses the default, bordered ButtonStyle):
        // a 1-row box still reports Height > 0 while its Border.All content deflates to nothing,
        // rendering no label at all. Reading the button's own row back from the surface catches
        // that - the row must contain the button's actual caption text, not just occupy space.
        buttons.Length.ShouldBe(2);
        foreach (var button in buttons)
        {
            button.Bounds.Height.ShouldBeGreaterThanOrEqualTo(3);
            ReadRow(surface, button.Bounds).ShouldContain(button.Text.Replace("&", string.Empty));
        }
        separator.Bounds.Height.ShouldBeGreaterThan(0);

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
        noButton.IsFocused.ShouldBeTrue();

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
        window.Shadow.IsVisible.ShouldBeTrue();
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

    /// <summary>Verifies assigning a local style with a wider ActionBarMargin live updates the
    /// frame, message, and divider coherently. A leaf declares no theme section of its own any
    /// more, so a locally assigned Style is the only surviving way to move this member at all.</summary>
    [Fact]
    public async Task Render_WhenLocalStyleIsAssignedLive_UpdatesFrameAndDividerCoherentlyAsync()
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
        var narrowWidth = divider.Bounds.Width;

        await surface.UpdateAsync(
            () => messageBox.Style = MessageBoxStyle.Default with { ActionBarMargin = new Thickness(3, 0) },
            "assign a wider ActionBarMargin");

        // Assert
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

    private static string ReadRow(ComponentSurface surface, Rect bounds)
    {
        if (bounds.Width == 0 || bounds.Height == 0)
        {
            return string.Empty;
        }

        var row = new StringBuilder();
        var y = bounds.Y + (bounds.Height / 2);

        for (var x = bounds.X; x < bounds.Right; x++)
        {
            _ = row.Append(surface.Cell(new Point(x, y)).Text);
        }

        return row.ToString();
    }
}
