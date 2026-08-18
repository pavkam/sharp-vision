// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Dialogs;

using SharpVision.Tests.Styling;

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

    /// <summary>Verifies direct and ancestor-inherited disablement both resolve through
    /// EffectiveIsEnabled without requiring a mounted surface, and re-enabling restores it -
    /// the detached counterpart to the mounted disabled-appearance evidence below.</summary>
    [ComponentUnitEvidence(typeof(MessageBox), ComponentBehavior.Disabled)]
    [Fact]
    public void Enabled_WhenSetDirectlyOrByAncestor_UpdatesEffectiveIsEnabled()
    {
        using var messageBox = new MessageBox("Continue?", "Confirm");
        var stack = new Stack { Children = { messageBox } };

        messageBox.EffectiveIsEnabled.ShouldBeTrue();

        messageBox.IsEnabled = false;
        messageBox.EffectiveIsEnabled.ShouldBeFalse();
        messageBox.IsEnabled = true;
        messageBox.EffectiveIsEnabled.ShouldBeTrue();

        stack.IsEnabled = false;
        messageBox.IsEnabled.ShouldBeTrue();
        messageBox.EffectiveIsEnabled.ShouldBeFalse();

        stack.IsEnabled = true;
        messageBox.EffectiveIsEnabled.ShouldBeTrue();
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

    /// <summary>Verifies a short message does not expand toward the 80% cap unnecessarily.</summary>
    [Fact]
    public void Layout_WhenMessageIsShort_DoesNotExpandTowardTheWidthCap()
    {
        var messageBox = new MessageBox("OK", "Notice");

        new LayoutEngine().Layout(messageBox, new Size(200, 40));

        messageBox.DesiredSize.Width.ShouldBeLessThan(160);
    }

    /// <summary>Verifies a long message grows toward, but never past, 80% of the available
    /// presentation width instead of a fixed cell count.</summary>
    [Fact]
    public void Layout_WhenMessageIsLongOnAWideHost_CapsWidthAtEightyPercent()
    {
        var messageBox = new MessageBox(
            string.Join(' ', Enumerable.Repeat("A deliberately long sentence fragment.", 20)),
            "Notice");

        new LayoutEngine().Layout(messageBox, new Size(200, 60));

        messageBox.DesiredSize.Width.ShouldBeLessThanOrEqualTo(160);
        messageBox.DesiredSize.Width.ShouldBeGreaterThan(120);
    }

    /// <summary>Verifies the 80% width cap recomputes against a new host size on a later layout
    /// pass, without any explicit resize subscription, since it is derived from the incoming
    /// measure constraint every pass.</summary>
    [Fact]
    public void Layout_WhenHostIsResizedNarrower_RecapturesTheWidthCap()
    {
        var messageBox = new MessageBox(
            string.Join(' ', Enumerable.Repeat("A deliberately long sentence fragment.", 20)),
            "Notice");
        var engine = new LayoutEngine();
        engine.Layout(messageBox, new Size(200, 60));
        var wideWidth = messageBox.DesiredSize.Width;

        engine.Layout(messageBox, new Size(100, 60));

        messageBox.DesiredSize.Width.ShouldBeLessThan(wideWidth);
        messageBox.DesiredSize.Width.ShouldBeLessThanOrEqualTo(80);
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

    /// <summary>Verifies direct and ancestor-inherited disablement switch the mounted MessageBox
    /// to its disabled appearance, re-enabling restores Normal, and a disabled instance moved to a
    /// genuinely different size arranges identically to an independently-mounted enabled instance
    /// at that same size.</summary>
    [ComponentBehaviorEvidence(typeof(MessageBox), ComponentBehavior.Disabled)]
    [Fact]
    public async Task Enabled_WhenDisabledDirectlyOrByAncestor_ChangesAppearanceAndRecoversAsync()
    {
        // Arrange
        var messageBox = new MessageBox("Continue?", "Confirm");
        var stack = new Stack { Children = { messageBox } };
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(40, 12),
            TestContext.Current.CancellationToken);
        surface.ShouldHaveState(messageBox, VisualState.Normal);

        // Act and assert direct disable
        await surface.UpdateAsync(() => messageBox.IsEnabled = false, "disable MessageBox directly");
        surface.ShouldHaveState(messageBox, VisualState.Disabled);

        // Act and assert re-enable recovery
        await surface.UpdateAsync(() => messageBox.IsEnabled = true, "re-enable MessageBox");
        surface.ShouldHaveState(messageBox, VisualState.Normal);

        // Act and assert ancestor-inherited disable
        await surface.UpdateAsync(() => stack.IsEnabled = false, "disable ancestor Stack");
        surface.ShouldHaveState(messageBox, VisualState.Disabled);
        messageBox.IsEnabled.ShouldBeTrue();
        messageBox.EffectiveIsEnabled.ShouldBeFalse();
        await surface.UpdateAsync(() => stack.IsEnabled = true, "re-enable ancestor Stack");
        surface.ShouldHaveState(messageBox, VisualState.Normal);

        // Arrange geometry comparison: a disabled instance moved to a genuinely different size -
        // same-size arrange is a no-op - must match an independently-mounted enabled instance
        // arranged directly at that same size.
        var disabled = new MessageBox("Continue?", "Confirm");
        await using var disabledSurface = await ComponentSurface.MountAsync(
            disabled,
            new Size(40, 12),
            TestContext.Current.CancellationToken);
        await disabledSurface.UpdateAsync(() => disabled.IsEnabled = false, "disable independent MessageBox");

        // Act
        await disabledSurface.ResizeAsync(new Size(60, 18));

        var enabled = new MessageBox("Continue?", "Confirm");
        await using var enabledSurface = await ComponentSurface.MountAsync(
            enabled,
            new Size(60, 18),
            TestContext.Current.CancellationToken);

        // Assert
        disabled.Bounds.ShouldBe(enabled.Bounds);
        disabled.DesiredSize.ShouldBe(enabled.DesiredSize);
    }

    /// <summary>Verifies a disabled MessageBox's close glyph renders with the theme's disabled-text
    /// color and ignores pointer hover/press instead of closing the dialog.</summary>
    [ComponentBehaviorEvidence(typeof(MessageBox), ComponentBehavior.Disabled)]
    [Fact]
    public async Task CloseGlyph_WhenMessageBoxIsDisabled_ShowsDisabledColorAndIgnoresPointerAsync()
    {
        // Arrange - MessageBox's own Window defaults CanClose to false, so opt into a closable
        // frame explicitly for this evidence, matching every other closable Window subclass.
        var messageBox = new MessageBox("Saved successfully.", "Status") { CanClose = true };
        var closed = 0;
        messageBox.Closed += (_, _) => closed++;
        await using var surface = await ComponentSurface.MountAsync(
            messageBox,
            new Size(40, 12),
            TestContext.Current.CancellationToken);
        var theme = messageBox.Theme.ShouldNotBeNull();
        var disabledForeground = TerminalPalette.Project(
            ThemeColorHelper.DisabledForeground(theme),
            ColorDepth.Basic16);
        var glyphPoint = new Point(messageBox.Bounds.X + 4, messageBox.Bounds.Y);

        // Act
        await surface.UpdateAsync(() => messageBox.IsEnabled = false, "disable MessageBox");

        // Assert disabled close-glyph color
        surface.Cell(glyphPoint).Style.Foreground.ShouldBe(disabledForeground);

        // Act - aim a pointer press at the close glyph while disabled
        await surface.Pointer.MoveToAsync(messageBox, new Point(4, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.ReleaseAsync();

        // Assert the press neither closed the dialog nor changed the glyph's rendered color
        closed.ShouldBe(0);
        messageBox.IsDisposed.ShouldBeFalse();
        surface.Cell(glyphPoint).Style.Foreground.ShouldBe(disabledForeground);
    }

    /// <summary>Verifies a local Style override's close-mark color reaches the rendered close
    /// glyph, proving the close chrome consults MessageBox's own resolved style instead of always
    /// reading the generic "window" theme section.</summary>
    [Fact]
    public async Task CloseGlyph_WhenLocalStyleOverridesCloseMarkColor_UsesTheOverrideColorAsync()
    {
        // Arrange
        var messageBox = new MessageBox("Saved successfully.", "Status")
        {
            CanClose = true,
            Style = MessageBoxStyle.Default with { CloseMarkColor = SemanticColor.Error }
        };
        await using var surface = await ComponentSurface.MountAsync(
            messageBox,
            new Size(40, 12),
            TestContext.Current.CancellationToken);
        var theme = messageBox.Theme.ShouldNotBeNull();
        var glyphPoint = new Point(messageBox.Bounds.X + 4, messageBox.Bounds.Y);

        // Assert
        surface.Cell(glyphPoint).Style.Foreground.ShouldBe(
            TerminalPalette.Project(theme.Error, ColorDepth.Basic16));
    }

    /// <summary>Verifies the "messageBox" theme section's own closeMarkColor wins over a
    /// deliberately different "window" section value, proving the two sections resolve
    /// independently instead of the close chrome always tracking "window".</summary>
    [Fact]
    public async Task CloseGlyph_WhenThemeSectionDivergesFromWindowSection_UsesTheDialogsOwnColorAsync()
    {
        // Arrange
        var theme = ThemeCatalog.Parse(ThemeJson.Create(
            windowExtra: """, "closeMarkColor": "success" """,
            extraStyles: """, "messageBox": { "normal": { "closeMarkColor": "error" } } """));
        var messageBox = new MessageBox("Saved successfully.", "Status") { CanClose = true };
        await using var surface = await ComponentSurface.MountAsync(
            messageBox,
            new Size(40, 12),
            theme,
            TestContext.Current.CancellationToken);
        var glyphPoint = new Point(messageBox.Bounds.X + 4, messageBox.Bounds.Y);

        // Assert
        surface.Cell(glyphPoint).Style.Foreground.ShouldBe(
            TerminalPalette.Project(theme.Error, ColorDepth.Basic16));
    }

    /// <summary>Verifies a live Theme swap that authors only the "messageBox" section's
    /// closeMarkColor - leaving "window" untouched - repaints the close glyph on its own,
    /// proving <c>GetThemeChangeImpact</c> detects a change confined to MessageBox's own
    /// resolved close-chrome style rather than only the generic "window" section.</summary>
    [Fact]
    public async Task CloseGlyph_WhenThemeIsSwappedLiveWithDialogSpecificColor_RepaintsAsync()
    {
        // Arrange
        var themeA = ThemeCatalog.Parse(ThemeJson.Create());
        var themeB = ThemeCatalog.Parse(ThemeJson.Create(
            extraStyles: """, "messageBox": { "normal": { "closeMarkColor": "error" } } """));
        var messageBox = new MessageBox("Saved successfully.", "Status") { CanClose = true };
        await using var surface = await ComponentSurface.MountAsync(
            messageBox,
            new Size(40, 12),
            themeA,
            TestContext.Current.CancellationToken);
        var glyphPoint = new Point(messageBox.Bounds.X + 4, messageBox.Bounds.Y);
        var beforeColor = surface.Cell(glyphPoint).Style.Foreground;

        // Act
        await surface.UpdateAsync(
            () => surface.Application.Theme = themeB,
            "swap to a messageBox-authoring theme");

        // Assert
        surface.Cell(glyphPoint).Style.Foreground.ShouldNotBe(beforeColor);
        surface.Cell(glyphPoint).Style.Foreground.ShouldBe(
            TerminalPalette.Project(themeB.Error, ColorDepth.Basic16));
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
                            observedHandled = eventArgs.IsHandled;
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

    /// <summary>
    /// Verifies modeless Escape commits cancellation and stays handled, matching
    /// ShowAsync_WhenEscapeIsPressed_CompletesWithCancelAsync's presented-dialog behavior:
    /// Dialog.Cancel() delegates unconditionally to Complete(), whose own fallback lets a
    /// directly mounted modeless dialog publish ResultSelected instead of completing a
    /// presentation task (see the type-level ResultSelected remarks). Escape does not propagate
    /// to an ancestor handler, and the modeless dialog remains open (not disposed, still parented)
    /// exactly like every other modeless result selection.
    /// </summary>
    [Fact]
    public async Task Input_WhenModelessEscapeIsPressed_PublishesCancelResultAndStaysOpenAsync()
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
                eventArgs.IsHandled = true;
            }
        });
        var results = new List<MessageBoxResult>();
        messageBox.ResultSelected += (_, _) => results.Add(messageBox.SelectedResult);
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(40, 12),
            TestContext.Current.CancellationToken);
        var button = OwnedTree.Find<Button>(messageBox).ShouldNotBeNull();
        await surface.UpdateAsync(
            () => surface.Application.Focus.Focus(button).ShouldBeTrue(),
            "focus modeless MessageBox button");

        await surface.Keyboard.PressAsync(Code.Escape);

        observed.ShouldBe(0);
        results.ShouldBe([MessageBoxResult.Cancel]);
        messageBox.HasSelectedResult.ShouldBeTrue();
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
            AppearanceTestValues.Face(foreground: Color.Rgb(1, 2, 3)),
            AppearanceTestValues.Border(BorderSide.All),
            AppearanceTestValues.Shadow(visible: false),
            new Thickness(horizontal: 2, vertical: 1));

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
            AppearanceTestValues.Face(),
            AppearanceTestValues.Border(BorderSide.None),
            AppearanceTestValues.Shadow(visible: false),
            new Thickness(horizontal: 3, vertical: 0));
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
            ButtonStyle = ButtonStyle.Standard with { Padding = new Thickness(horizontal: 1, vertical: 3) }
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
            AppearanceTestValues.Face(foreground: Color.Rgb(4, 5, 6)),
            AppearanceTestValues.Border(BorderSide.All),
            AppearanceTestValues.Shadow(visible: false),
            new Thickness(horizontal: 2, vertical: 1));
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

    /// <summary>Verifies the resolved aggregate style defaults to the Window fallback until an
    /// explicit local style is assigned.</summary>
    [Fact]
    public void Style_WhenUnset_FollowsTheThemeLikeAnOrdinaryWindow()
    {
        using var messageBox = new MessageBox("Continue?", "Confirm");

        messageBox.Style.ShouldBeNull();
        messageBox.ActualStyle.Border.ShouldBe(ThemeCatalog.Dark.GetWindowStyleSet().Normal.Border);
    }

    /// <summary>Verifies a local style overrides the frame, message face, and content geometry
    /// coherently, and resetting it restores Theme ownership.</summary>
    [Fact]
    public void Style_WhenSet_OverridesFrameMessageFaceAndGeometryAndResetRestores()
    {
        using var messageBox = new MessageBox("Continue?", "Confirm");
        var defaultStyle = messageBox.ActualStyle;
        var style = defaultStyle with
        {
            MessageFace = AppearanceTestValues.Face(foreground: Color.Rgb(9, 9, 9)),
            MessageMargin = new Thickness(2, 3, 2, 1),
            ActionBarMargin = new Thickness(3, 1)
        };
        var message = OwnedTree.Find<ControlText>(messageBox).ShouldNotBeNull();
        var engine = new LayoutEngine();

        messageBox.Style = style;
        engine.Layout(messageBox, new Size(60, 20));

        messageBox.ActualStyle.ShouldBe(style);
        message.Face.ShouldBe(style.MessageFace);
        message.Margin.ShouldBe(style.MessageMargin);

        messageBox.Style = null;
        engine.Layout(messageBox, new Size(60, 20));

        messageBox.Style.ShouldBeNull();
        message.Face.ShouldBe(defaultStyle.MessageFace);
        message.Margin.ShouldBe(defaultStyle.MessageMargin);
    }

    /// <summary>Verifies the resolved Separator style defaults to the shared Theme "separator"
    /// presentation until an explicit local style is assigned.</summary>
    [Fact]
    public void SeparatorStyle_WhenUnset_FollowsTheThemeLikeAnOrdinarySeparator()
    {
        using var messageBox = new MessageBox("Continue?", "Confirm");
        using var expected = new Separator();

        messageBox.SeparatorStyle.ShouldBeNull();
        messageBox.ActualSeparatorStyle.ShouldBe(expected.ActualStyle);
    }

    /// <summary>Verifies an explicit local Separator style propagates to the retained divider, and
    /// clearing it returns the divider to Theme ownership rather than pinning a resolved value.</summary>
    [Fact]
    public void SeparatorStyle_WhenSet_PropagatesToDividerAndResetRestoresThemeOwnership()
    {
        using var messageBox = new MessageBox("Continue?", "Confirm");
        var divider = OwnedTree.Find<Separator>(messageBox).ShouldNotBeNull();
        var style = SeparatorStyle.Default with { HorizontalGlyph = new Rune('=') };

        messageBox.SeparatorStyle = style;

        messageBox.ActualSeparatorStyle.ShouldBe(style);
        divider.Style.ShouldBe(style);
        divider.ActualStyle.ShouldBe(style);

        messageBox.SeparatorStyle = null;

        messageBox.SeparatorStyle.ShouldBeNull();
        divider.Style.ShouldBeNull();
    }

    /// <summary>Verifies the four generated captions default to the established mnemonic-marked labels.</summary>
    [Fact]
    public void Captions_WhenUnset_DefaultToTheEstablishedMnemonicLabels()
    {
        using var messageBox = new MessageBox("Choose.", "Action", MessageBoxButtons.YesNoCancel);

        messageBox.OkText.ShouldBe("&OK");
        messageBox.CancelText.ShouldBe("&Cancel");
        messageBox.YesText.ShouldBe("&Yes");
        messageBox.NoText.ShouldBe("&No");
    }

    /// <summary>Verifies changing a caption updates only the retained Button owning that semantic
    /// action, in place, without replacing it - preserving its MessageBoxResult association.</summary>
    [Fact]
    public async Task OkText_WhenChanged_UpdatesTheRetainedOkButtonAndPreservesItsResultAsync()
    {
        using var messageBox = new MessageBox("Save changes?", "Confirm", MessageBoxButtons.OkCancel);
        var okButton = OwnedTree.FindAll<Button>(messageBox).Single(static button => button.IsDefault);
        var cancelButton = OwnedTree.FindAll<Button>(messageBox).Single(static button => button.IsCancel);

        messageBox.OkText = "&Save";

        okButton.Text.ShouldBe("&Save");
        cancelButton.Text.ShouldBe("&Cancel");
        okButton.ShouldBeSameAs(OwnedTree.FindAll<Button>(messageBox).Single(static button => button.IsDefault));

        var host = new Overlay { Children = { messageBox } };
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(40, 12),
            TestContext.Current.CancellationToken);
        var selections = new List<MessageBoxResult>();
        messageBox.ResultSelected += (_, _) => selections.Add(messageBox.SelectedResult);
        await surface.Pointer.ClickAsync(okButton);

        selections.ShouldBe([MessageBoxResult.Ok]);
    }

    /// <summary>Verifies setting a caption for an action absent from the current button layout stores
    /// the value without affecting any retained Button.</summary>
    [Fact]
    public void YesText_WhenLayoutHasNoYesAction_StoresValueWithoutAffectingButtons()
    {
        using var messageBox = new MessageBox("Proceed?", "Confirm", MessageBoxButtons.OkCancel);
        var beforeTexts = OwnedTree.FindAll<Button>(messageBox).Select(static button => button.Text).ToArray();

        messageBox.YesText = "&Confirm";

        messageBox.YesText.ShouldBe("&Confirm");
        OwnedTree.FindAll<Button>(messageBox).Select(static button => button.Text).ShouldBe(beforeTexts);
    }

    /// <summary>Verifies a longer localized caption remeasures every generated action to the widest
    /// current caption, keeping equal widths.</summary>
    [Fact]
    public void OkText_WhenReplacedWithALongerLocalizedCaption_RemeasuresEveryActionToEqualWidth()
    {
        using var messageBox = new MessageBox("Proceed?", "Confirm", MessageBoxButtons.OkCancel);
        var buttons = OwnedTree.FindAll<Button>(messageBox).ToArray();
        var initialWidth = buttons[0].Width;

        messageBox.OkText = "&Fortsätta ändå";

        var updatedWidths = buttons.Select(static button => button.Width).Distinct().ToArray();
        updatedWidths.Length.ShouldBe(1);
        updatedWidths[0].ShouldNotBe(initialWidth);
    }

    /// <summary>Verifies a null caption throws before any observable mutation.</summary>
    [Fact]
    public void Captions_WhenSetToNull_ThrowBeforeMutation()
    {
        using var messageBox = new MessageBox("Continue?", "Confirm", MessageBoxButtons.OkCancel);

        _ = Should.Throw<ArgumentNullException>(() => messageBox.OkText = null!);
        _ = Should.Throw<ArgumentNullException>(() => messageBox.CancelText = null!);
        _ = Should.Throw<ArgumentNullException>(() => messageBox.YesText = null!);
        _ = Should.Throw<ArgumentNullException>(() => messageBox.NoText = null!);

        messageBox.OkText.ShouldBe("&OK");
        messageBox.CancelText.ShouldBe("&Cancel");
        messageBox.YesText.ShouldBe("&Yes");
        messageBox.NoText.ShouldBe("&No");
    }

    /// <summary>Verifies a caller-supplied caption still carries its ampersand access-key marker
    /// onto the retained mnemonic-enabled Button, exactly like the built-in captions.</summary>
    [Fact]
    public void OkText_WhenChanged_PreservesTheAmpersandAccessKeyMarker()
    {
        using var messageBox = new MessageBox("Retry?", "Failure");
        var button = OwnedTree.Find<Button>(messageBox).ShouldNotBeNull();

        messageBox.OkText = "&Retry";

        button.Text.ShouldBe("&Retry");
        button.UseMnemonic.ShouldBeTrue();
    }

    /// <summary>Verifies ShowAsync configured through <see cref="MessageBoxOptions"/> applies the
    /// title, layout, captions, and style without exposing the generated child Buttons.</summary>
    [Fact]
    public async Task ShowAsync_WhenConfiguredThroughOptions_AppliesEveryConfiguredValueAsync()
    {
        // Arrange
        var opener = new Button { Text = "Open" };
        var host = new Overlay { Children = { opener } };
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(40, 12),
            TestContext.Current.CancellationToken);
        var style = MessageBoxStyle.Default with { ActionBarMargin = new Thickness(2, 0) };
        var options = new MessageBoxOptions
        {
            Title = "Localized",
            Buttons = MessageBoxButtons.YesNo,
            YesText = "&Sí",
            NoText = "&No, gracias",
            Style = style
        };
        Task<MessageBoxResult>? pending = null;

        // Act
        await surface.UpdateAsync(
            () => pending = MessageBox.ShowAsync(opener, "¿Continuar?", options),
            "show MessageBox from options");
        var messageBox = OwnedTree.Find<MessageBox>(surface.Application.Root).ShouldNotBeNull();

        // Assert
        messageBox.Title.ShouldBe("Localized");
        messageBox.Buttons.ShouldBe(MessageBoxButtons.YesNo);
        messageBox.ActualStyle.ShouldBe(style);
        OwnedTree.FindAll<Button>(messageBox).Select(static button => button.Text).ShouldBe(["&Sí", "&No, gracias"]);

        await surface.Keyboard.PressAsync(Code.Enter);
        (await pending!).ShouldBe(MessageBoxResult.Yes);
    }

    /// <summary>Verifies the options-carrier overload validates its required arguments before any
    /// observable mutation.</summary>
    [Fact]
    public void ShowAsync_WhenOptionsAreInvalid_ThrowsValidationExceptions()
    {
        var opener = new Button { Text = "Open" };
        var host = new Overlay { Children = { opener } };

        _ = Should.Throw<ArgumentNullException>(() => MessageBox.ShowAsync(opener, "Message", (MessageBoxOptions) null!));
        _ = Should.Throw<ArgumentNullException>(() => MessageBox.ShowAsync(null!, "Message", new MessageBoxOptions()));
        _ = Should.Throw<ArgumentNullException>(() => MessageBox.ShowAsync(opener, null!, new MessageBoxOptions()));
    }
}
