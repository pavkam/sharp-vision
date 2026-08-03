// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Dialogs;

/// <summary>Defines the public and measured behavior of the standard MessageBox surface.</summary>
public sealed class MessageBoxTests
{
    /// <summary>Verifies the supported button layouts and result values remain stable.</summary>
    [Fact]
    public void Enums_WhenInspected_ExposeTheStandardLayoutsAndResults()
    {
        Enum.GetValues<MessageBoxButtons>().ShouldBe([
            MessageBoxButtons.Ok,
            MessageBoxButtons.OkCancel,
            MessageBoxButtons.YesNo,
            MessageBoxButtons.YesNoCancel]);
        Enum.GetValues<MessageBoxResult>().ShouldBe([
            MessageBoxResult.Ok,
            MessageBoxResult.Cancel,
            MessageBoxResult.Yes,
            MessageBoxResult.No]);
    }

    /// <summary>Verifies construction validates content and retains the requested public contract.</summary>
    [ComponentUnitEvidence(typeof(MessageBox))]
    [Fact]
    public void Constructor_WhenConfigured_RetainsTitleMessageAndButtons()
    {
        var messageBox = new MessageBox("Proceed with deployment?", "Confirm", MessageBoxButtons.YesNoCancel);

        messageBox.Message.ShouldBe("Proceed with deployment?");
        messageBox.Title.ShouldBe("Confirm");
        messageBox.Buttons.ShouldBe(MessageBoxButtons.YesNoCancel);
    }

    /// <summary>Verifies MessageBox is the explicit dialog Window identity and inherits shared defaults.</summary>
    [Fact]
    public void Constructor_WhenCreated_OwnsDialogWindowDefaults()
    {
        var messageBox = new MessageBox("Saved successfully.", "Status");
        var window = OwnedTree.Find<Window>(messageBox).ShouldNotBeNull();

        window.HeaderPlacement.ShouldBe(WindowTitlePlacement.Center);
        window.ShouldBeSameAs(messageBox);
        window.Border.GlyphStyle.ShouldBe(BorderGlyphStyle.Paired);
        window.CanMove.ShouldBeTrue();
        window.CanClose.ShouldBeFalse();
    }

    /// <summary>Verifies long messages wrap to the available surface instead of using a fixed width.</summary>
    [Fact]
    public void Layout_WhenMessageExceedsViewport_WrapsAndScalesToConstraint()
    {
        var messageBox = new MessageBox(
            "This is a deliberately long message that must wrap inside the available terminal viewport.",
            "Notice")
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var engine = new LayoutEngine();

        engine.Layout(messageBox, new Size(32, 12));

        messageBox.DesiredSize.Width.ShouldBeLessThanOrEqualTo(32);
        messageBox.DesiredSize.Height.ShouldBeGreaterThan(4);
        messageBox.Bounds.X.ShouldBe((32 - messageBox.Bounds.Width) / 2);
    }

    /// <summary>Verifies short messages retain the deliberate minimum dialog footprint.</summary>
    [Fact]
    public void Layout_WhenMessageIsShort_UsesQualityMinimumSize()
    {
        var messageBox = new MessageBox("OK", "Notice");

        new LayoutEngine().Layout(messageBox, new Size(80, 24));

        messageBox.DesiredSize.Width.ShouldBeGreaterThanOrEqualTo(32);
        messageBox.DesiredSize.Height.ShouldBeGreaterThanOrEqualTo(8);
    }

    /// <summary>Verifies invalid title, message, and button values are rejected before mutation.</summary>
    [Fact]
    public void Constructor_WhenArgumentsAreInvalid_ThrowsValidationExceptions()
    {
        _ = Should.Throw<ArgumentNullException>(() => new MessageBox(null!));
        _ = Should.Throw<ArgumentNullException>(() => new MessageBox("Message", null!));
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            new MessageBox("Message", "Title", (MessageBoxButtons) int.MaxValue));
    }

    /// <summary>Verifies ShowAsync confines focus, activates the default button, and removes its temporary surface.</summary>
    [ComponentBehaviorEvidence(
        typeof(MessageBox),
        ComponentBehavior.Mounted |
        ComponentBehavior.Hover |
        ComponentBehavior.Focus |
        ComponentBehavior.Tab |
        ComponentBehavior.DirectionalExcluded |
        ComponentBehavior.PressReleaseExcluded |
        ComponentBehavior.Activation |
        ComponentBehavior.KeyboardActivation |
        ComponentBehavior.UnavailableCleanup |
        ComponentBehavior.Transient |
        ComponentBehavior.Composition)]
    [Fact]
    public async Task ShowAsync_WhenDefaultButtonIsPressed_CompletesAndRestoresHostAsync()
    {
        // Arrange
        var opener = new Button { Text = "Open" };
        var host = new Overlay { Children = { opener } };
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(40, 12),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => _ = surface.Application.Focus.Focus(opener), "focus opener");
        Task<MessageBoxResult>? pending = null;

        // Act
        await surface.UpdateAsync(
            () => pending = MessageBox.ShowAsync(opener, "Saved successfully.", "Status"),
            "show MessageBox");
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert
        (await pending!).ShouldBe(MessageBoxResult.Ok);
        host.Children.Count.ShouldBe(1);
        opener.IsFocused.ShouldBeTrue();
    }

    /// <summary>Verifies a bounded owner does not constrain the application-wide MessageBox plane.</summary>
    [Fact]
    public async Task ShowAsync_WhenOwnerIsInsideBoundedSurface_CentersAgainstApplicationPlaneAsync()
    {
        // Arrange
        var opener = new Button { Text = "Open" };
        var bounded = new Overlay
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(40),
            Height = Length.Cells(10),
            Children = { opener }
        };
        var page = new Overlay { Children = { bounded } };
        await using var surface = await ComponentSurface.MountAsync(
            page,
            new Size(80, 24),
            TestContext.Current.CancellationToken);
        Task<MessageBoxResult>? pending = null;

        // Act
        await surface.UpdateAsync(
            () => pending = MessageBox.ShowAsync(opener, "Saved successfully.", "Status"),
            "show application-wide MessageBox");
        var messageBox = OwnedTree.Find<MessageBox>(surface.Application.Root).ShouldNotBeNull();
        var window = OwnedTree.Find<Window>(messageBox).ShouldNotBeNull();

        // Assert
        _ = messageBox.Parent.ShouldBeOfType<Overlay>();
        window.Bounds.X.ShouldBe((80 - window.Bounds.Width) / 2);
        window.Bounds.Y.ShouldBe((24 - window.Bounds.Height) / 2);

        await surface.Keyboard.PressAsync(Code.Escape);
        (await pending!).ShouldBe(MessageBoxResult.Cancel);
    }

    /// <summary>Verifies the message begins after one empty interior row below the title edge.</summary>
    [Fact]
    public void Layout_WhenMessageIsArranged_LeavesOneRowBelowTitle()
    {
        // Arrange
        using var messageBox = new MessageBox("Saved successfully.", "Status");
        var window = OwnedTree.Find<Window>(messageBox).ShouldNotBeNull();
        var message = OwnedTree.Find<ControlText>(messageBox).ShouldNotBeNull();

        // Act
        new LayoutEngine().Layout(messageBox, new Size(40, 12));

        // Assert
        message.Bounds.Y.ShouldBe(window.Bounds.Y + 3);
    }

    /// <summary>Verifies Escape dismisses every button layout with the Cancel semantic result.</summary>
    [Fact]
    public async Task ShowAsync_WhenEscapeIsPressed_CompletesWithCancelAsync()
    {
        // Arrange
        var opener = new Button { Text = "Open" };
        var host = new Overlay { Children = { opener } };
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(40, 12),
            TestContext.Current.CancellationToken);
        Task<MessageBoxResult>? pending = null;
        var observedHandled = false;

        // Act
        await surface.UpdateAsync(
            () =>
            {
                pending = MessageBox.ShowAsync(
                    opener,
                    "Dismiss this message.",
                    "Dismiss",
                    MessageBoxButtons.YesNo);
                var messageBox = OwnedTree.Find<MessageBox>(surface.Application.Root).ShouldNotBeNull();
                _ = messageBox.AddHandler(
                    Events.Key,
                    (_, eventArgs) =>
                    {
                        if (eventArgs.Stroke.Action == KeyAction.Press && eventArgs.Stroke.Code == Code.Escape)
                        {
                            observedHandled = eventArgs.Handled;
                        }
                    },
                    handledEventsToo: true);
            },
            "show dismissible MessageBox");
        await surface.Keyboard.PressAsync(Code.Escape);

        // Assert
        (await pending!).ShouldBe(MessageBoxResult.Cancel);
        observedHandled.ShouldBeTrue();
        host.Children.Count.ShouldBe(1);
    }

    /// <summary>Verifies modeless Escape remains unhandled when no dialog presentation can commit cancellation.</summary>
    [Fact]
    public async Task Input_WhenModelessEscapeIsPressed_PropagatesToParentAsync()
    {
        var messageBox = new MessageBox("Read only.", "Notice", MessageBoxButtons.YesNoCancel);
        var host = new Overlay { Children = { messageBox } };
        var observed = 0;
        _ = host.AddHandler(Events.Key, (_, eventArgs) =>
        {
            if (eventArgs.Phase == RoutingPhase.Bubble &&
                eventArgs.Stroke.Action == KeyAction.Press &&
                eventArgs.Stroke.Code == Code.Escape)
            {
                observed++;
                eventArgs.Handled = true;
            }
        });
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(40, 12),
            TestContext.Current.CancellationToken);
        var button = OwnedTree.Find<Button>(messageBox).ShouldNotBeNull();
        await surface.UpdateAsync(
            () => surface.Application.Focus.Focus(button).ShouldBeTrue(),
            "focus modeless MessageBox button");

        await surface.Keyboard.PressAsync(Code.Escape);

        observed.ShouldBe(1);
        messageBox.HasSelectedResult.ShouldBeFalse();
        messageBox.IsDisposed.ShouldBeFalse();
        messageBox.Parent.ShouldBeSameAs(host);
    }

    /// <summary>Verifies modeless keyboard and pointer actions publish typed selection state without closing.</summary>
    [Fact]
    public async Task Input_WhenMessageBoxIsModeless_PublishesButtonResultsWithoutDisposalAsync()
    {
        var messageBox = new MessageBox("Choose.", "Action", MessageBoxButtons.YesNo);
        var host = new Overlay { Children = { messageBox } };
        var selections = new List<MessageBoxResult>();
        messageBox.ResultSelected += (_, _) => selections.Add(messageBox.SelectedResult);
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(40, 12),
            TestContext.Current.CancellationToken);
        var yes = OwnedTree.FindAll<Button>(messageBox).Single(static candidate => candidate.IsDefault);
        var no = OwnedTree.FindAll<Button>(messageBox).Single(static candidate =>
            candidate.Text == "&No");
        await surface.UpdateAsync(
            () => surface.Application.Focus.Focus(yes).ShouldBeTrue(),
            "focus modeless default result");

        await surface.Keyboard.PressAsync(Code.Enter);
        await surface.Pointer.ClickAsync(no);

        selections.ShouldBe([MessageBoxResult.Yes, MessageBoxResult.No]);
        messageBox.HasSelectedResult.ShouldBeTrue();
        messageBox.SelectedResult.ShouldBe(MessageBoxResult.No);
        messageBox.IsDisposed.ShouldBeFalse();
        messageBox.Parent.ShouldBeSameAs(host);
    }

    /// <summary>Verifies each standard button layout can be constructed without fixed message-box geometry.</summary>
    [Theory]
    [InlineData(MessageBoxButtons.Ok)]
    [InlineData(MessageBoxButtons.OkCancel)]
    [InlineData(MessageBoxButtons.YesNo)]
    [InlineData(MessageBoxButtons.YesNoCancel)]
    public void Constructor_WhenButtonLayoutIsStandard_CreatesMeasuredSurface(MessageBoxButtons buttons)
    {
        var messageBox = new MessageBox("Choose an action.", "Action", buttons);
        var engine = new LayoutEngine();

        engine.Layout(messageBox, new Size(40, 12));

        messageBox.DesiredSize.Width.ShouldBeGreaterThan(0);
        messageBox.DesiredSize.Height.ShouldBeGreaterThan(0);
    }

    /// <summary>Verifies the resolved Button style defaults to the shared semantic input profile
    /// until an explicit local style is assigned.</summary>
    [Fact]
    public void ButtonStyle_WhenUnset_FollowsTheThemeLikeAnOrdinaryButton()
    {
        using var messageBox = new MessageBox("Continue?", "Confirm");
        using var expected = new Button();

        messageBox.ButtonStyle.ShouldBeNull();
        messageBox.ActualButtonStyle.ShouldBe(expected.ActualStyle);
    }

    /// <summary>Verifies an explicit local style propagates to every generated action across every
    /// standard button layout, and clearing it reverts every action to the semantic profile.</summary>
    [Theory]
    [InlineData(MessageBoxButtons.Ok)]
    [InlineData(MessageBoxButtons.OkCancel)]
    [InlineData(MessageBoxButtons.YesNo)]
    [InlineData(MessageBoxButtons.YesNoCancel)]
    public void ButtonStyle_WhenSet_PropagatesToEveryGeneratedAction(MessageBoxButtons buttons)
    {
        using var messageBox = new MessageBox("Choose an action.", "Action", buttons);
        var style = new ButtonStyle(
            new Thickness(horizontal: 2, vertical: 1),
            new ThemeProfile(
                new ThemeAppearance(
                    AppearanceTestValues.Face(foreground: Color.Rgb(1, 2, 3)),
                    AppearanceTestValues.Border(BorderSide.All),
                    AppearanceTestValues.Shadow(visible: false))));

        messageBox.ButtonStyle = style;

        messageBox.ActualButtonStyle.ShouldBe(style);

        foreach (var button in OwnedTree.FindAll<Button>(messageBox))
        {
            button.Style.ShouldBe(style);
            button.ActualStyle.ShouldBe(style);
        }

        messageBox.ButtonStyle = null;

        messageBox.ButtonStyle.ShouldBeNull();

        foreach (var button in OwnedTree.FindAll<Button>(messageBox))
        {
            button.Style.ShouldBeNull();
        }
    }

    /// <summary>Verifies replacing the Button style notifies observers exactly once per distinct value
    /// and reports no change for an equal resubmission.</summary>
    [Fact]
    public void ButtonStyle_WhenReplaced_RaisesPropertyChangedOnceForEachDistinctValue()
    {
        using var messageBox = new MessageBox("Continue?", "Confirm");
        var style = new ButtonStyle(
            new Thickness(horizontal: 3, vertical: 0),
            new ThemeProfile(
                new ThemeAppearance(
                    AppearanceTestValues.Face(),
                    AppearanceTestValues.Border(BorderSide.None),
                    AppearanceTestValues.Shadow(visible: false))));
        var notifications = new List<string?>();
        messageBox.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName);

        messageBox.ButtonStyle = style;
        messageBox.ButtonStyle = style;

        notifications.ShouldBe([nameof(MessageBox.ButtonStyle), nameof(MessageBox.ActualButtonStyle)]);
    }

    /// <summary>Verifies a padding change through the Button style changes the measured button
    /// height — MessageBox pins every generated action to an explicit computed Width, so only
    /// vertical padding remains free to change the measured box.</summary>
    [Fact]
    public void ButtonStyle_WhenPaddingChanges_ChangesGeneratedButtonHeight()
    {
        using var flat = new MessageBox("OK", "Notice");
        using var tall = new MessageBox("OK", "Notice")
        {
            ButtonStyle = new ButtonStyle(
                new Thickness(horizontal: 1, vertical: 3),
                ButtonStyle.Standard.Appearance)
        };
        var engine = new LayoutEngine();

        engine.Layout(flat, new Size(60, 20));
        engine.Layout(tall, new Size(60, 20));

        var flatButton = OwnedTree.Find<Button>(flat).ShouldNotBeNull();
        var tallButton = OwnedTree.Find<Button>(tall).ShouldNotBeNull();
        tallButton.DesiredSize.Height.ShouldBeGreaterThan(flatButton.DesiredSize.Height);
    }

    /// <summary>Verifies ShowAsync forwards an explicit Button style to the presented dialog's actions
    /// without exposing the underlying Button instances.</summary>
    [Fact]
    public async Task ShowAsync_WhenButtonStyleIsSupplied_AppliesItToEveryPresentedActionAsync()
    {
        // Arrange
        var opener = new Button { Text = "Open" };
        var host = new Overlay { Children = { opener } };
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(40, 12),
            TestContext.Current.CancellationToken);
        var style = new ButtonStyle(
            new Thickness(horizontal: 2, vertical: 1),
            new ThemeProfile(
                new ThemeAppearance(
                    AppearanceTestValues.Face(foreground: Color.Rgb(4, 5, 6)),
                    AppearanceTestValues.Border(BorderSide.All),
                    AppearanceTestValues.Shadow(visible: false))));
        Task<MessageBoxResult>? pending = null;

        // Act
        await surface.UpdateAsync(
            () => pending = MessageBox.ShowAsync(
                opener,
                "Saved successfully.",
                "Status",
                MessageBoxButtons.Ok,
                style),
            "show MessageBox with an explicit Button style");
        var messageBox = OwnedTree.Find<MessageBox>(surface.Application.Root).ShouldNotBeNull();

        // Assert
        messageBox.ActualButtonStyle.ShouldBe(style);

        foreach (var button in OwnedTree.FindAll<Button>(messageBox))
        {
            button.ActualStyle.ShouldBe(style);
        }

        await surface.Keyboard.PressAsync(Code.Enter);
        (await pending!).ShouldBe(MessageBoxResult.Ok);
    }
}
