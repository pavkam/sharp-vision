// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Verifies HyperlinkButton ownership, command ordering, activation, and defaults.</summary>
public sealed class HyperlinkButtonTests
{
    /// <summary>Verifies documented defaults for an empty HyperlinkButton.</summary>
    [ComponentUnitEvidence(typeof(HyperlinkButton))]
    [Fact]
    public void Constructor_WhenCreated_UsesDocumentedDefaults()
    {
        var link = new HyperlinkButton();

        link.Text.ShouldBeEmpty();
        link.Command.ShouldBeNull();
        link.CommandParameter.ShouldBeNull();
        link.CanFocus.ShouldBeTrue();
        link.Face.Underline.ShouldBe(Underline.Straight);
        link.ActualBorder.Sides.ShouldBe(BorderSide.None);
        link.Padding.ShouldBe(default);
        link.ActualShadow.IsVisible.ShouldBeFalse();
        link.StartAffix.ShouldBeNull();
        link.EndAffix.ShouldBeNull();
    }

    /// <summary>Verifies HyperlinkButton proves direct and ancestor-inherited disabled state at
    /// the detached unit level, and that clearing IsEnabled on each recovers EffectiveIsEnabled -
    /// the same disabled contract exercised on a live mounted terminal surface.</summary>
    [ComponentUnitEvidence(typeof(HyperlinkButton), ComponentBehavior.Disabled)]
    [Fact]
    public void EffectiveIsEnabled_WhenLinkIsDisabledDirectlyOrByAncestor_ReportsDisabledAndRecovers()
    {
        var link = new HyperlinkButton("Visit");
        var host = new Stack();
        host.Children.Add(link);

        link.IsEnabled = false;
        link.EffectiveIsEnabled.ShouldBeFalse();

        link.IsEnabled = true;
        link.EffectiveIsEnabled.ShouldBeTrue();

        host.IsEnabled = false;
        link.IsEnabled.ShouldBeTrue();
        link.EffectiveIsEnabled.ShouldBeFalse();

        host.IsEnabled = true;
        link.EffectiveIsEnabled.ShouldBeTrue();
    }

    /// <summary>Verifies local style ownership overrides the theme fallback and clearing restores it.</summary>
    [Fact]
    public void Style_WhenLocalValueIsAssignedAndCleared_UsesDocumentedPrecedence()
    {
        var theme = new Theme();
        theme.Freeze();
        var link = new HyperlinkButton();
        link.SetTheme(theme);
        var custom = HyperlinkButtonStyle.Default with
        {
            Face = HyperlinkButtonStyle.Default.Face with { Foreground = SemanticColor.Warning }
        };

        link.Style.ShouldBeNull();
        link.ActualStyle.ShouldBe(HyperlinkButtonStyle.Default);

        link.Style = custom;

        link.Style.ShouldBe(custom);
        link.ActualStyle.ShouldBe(custom);

        link.Style = null;

        link.Style.ShouldBeNull();
        link.ActualStyle.ShouldBe(HyperlinkButtonStyle.Default);
    }

    /// <summary>Verifies the string constructor sets text content.</summary>
    [Fact]
    public void Constructor_WithText_SetsTextContent()
    {
        var link = new HyperlinkButton("Visit");
        link.Text.ShouldBe("Visit");
        link.TextControl.ShouldNotBeNull().Content.ShouldBe("Visit");
    }

    /// <summary>Verifies the string constructor rejects null text.</summary>
    [Fact]
    public void Constructor_WithNullText_Throws() =>
        _ = Should.Throw<ArgumentNullException>(() => new HyperlinkButton(null!));

    /// <summary>Verifies the Text property setter updates existing content in place.</summary>
    [Fact]
    public void Text_WhenSetOnExistingContent_UpdatesInPlace()
    {
        var link = new HyperlinkButton("Before");
        var content = link.TextControl;

        link.Text = "After";

        link.Text.ShouldBe("After");
        link.TextControl.ShouldBeSameAs(content);
    }

    /// <summary>Verifies the Text property setter creates content when none exists.</summary>
    [Fact]
    public void Text_WhenSetOnEmptyControl_CreatesContent()
    {
        var link = new HyperlinkButton { Text = "New" };

        link.Text.ShouldBe("New");
        link.TextControl.ShouldNotBeNull().Content.ShouldBe("New");
    }

    /// <summary>Verifies the Text property setter rejects null.</summary>
    [Fact]
    public void Text_WhenSetToNull_Throws()
    {
        var link = new HyperlinkButton("Visit");

        _ = Should.Throw<ArgumentNullException>(() => link.Text = null!);
        link.Text.ShouldBe("Visit");
    }

    /// <summary>Verifies Click observes released state and precedes command execution.</summary>
    [Fact]
    public void PerformClick_WhenCommandCanExecute_RaisesThenExecutesExactlyOnce()
    {
        List<string> order = [];
        var parameter = new object();
        var command = new ProbeCommand { Executing = _ => order.Add("command") };
        var link = new HyperlinkButton("Go") { Command = command, CommandParameter = parameter };
        link.Click += (_, eventArgs) =>
        {
            link.IsPressed.ShouldBeFalse();
            eventArgs.Cause.ShouldBe(ActivationCause.Programmatic);
            order.Add("click");
        };

        link.PerformClick();

        order.ShouldBe(["click", "command"]);
        command.Queries.ShouldBe([parameter]);
        command.Executions.ShouldBe([parameter]);
    }

    /// <summary>Verifies false CanExecute suppresses both Click and execution.</summary>
    [Fact]
    public void PerformClick_WhenCommandCannotExecute_DoesNothing()
    {
        var command = new ProbeCommand { CanExecuteValue = false };
        var link = new HyperlinkButton("Go") { Command = command };
        var clicks = 0;
        link.Click += (_, _) => clicks++;

        link.PerformClick();

        clicks.ShouldBe(0);
        command.Executions.ShouldBeEmpty();
    }

    /// <summary>Verifies unavailable controls reject programmatic activation.</summary>
    [Theory]
    [InlineData(false, Visibility.Visible)]
    [InlineData(true, Visibility.Hidden)]
    public void PerformClick_WhenControlIsUnavailable_DoesNothing(bool enabled, Visibility visibility)
    {
        var link = new HyperlinkButton("Go") { IsEnabled = enabled, Visibility = visibility };
        var clicks = 0;
        link.Click += (_, _) => clicks++;

        link.PerformClick();

        clicks.ShouldBe(0);
    }

    /// <summary>Verifies PerformClick rejects use after disposal.</summary>
    [Fact]
    public void PerformClick_WhenDisposed_Throws()
    {
        // Arrange
        var link = new HyperlinkButton("Go");
        link.Dispose();

        // Act and assert
        _ = Should.Throw<ObjectDisposedException>(link.PerformClick);
    }

    /// <summary>Verifies command replacement raises the standard property notification once.</summary>
    [Fact]
    public void Command_WhenReplacementChanges_RaisesPropertyChangedOnce()
    {
        var link = new HyperlinkButton();
        List<string?> names = [];
        link.PropertyChanged += (_, eventArgs) => names.Add(eventArgs.PropertyName);
        var command = new ProbeCommand();

        link.Command = command;
        link.Command = command;

        names.ShouldBe([nameof(HyperlinkButton.Command)]);
    }

    /// <summary>Verifies keyboard activation reaches the public event and command.</summary>
    [Fact]
    public void Route_WhenEnterIsPressed_ActivatesWithKeyboardCause()
    {
        var command = new ProbeCommand();
        var link = new HyperlinkButton("Go") { Command = command };
        ActivationCause? cause = null;
        link.Click += (_, eventArgs) => cause = eventArgs.Cause;

        _ = Router.Route(
            link,
            Events.Key,
            new KeyEventArgs(new Stroke(
                Code.Enter,
                character: null,
                nativeCode: 0,
                Modifiers.None,
                KeyAction.Press)));

        cause.ShouldBe(ActivationCause.Keyboard);
        command.Executions.Count.ShouldBe(1);
    }

    /// <summary>Verifies the control measures tight to text content with no chrome.</summary>
    [Fact]
    public void Layout_WhenTextIsSet_MeasuresTightToContent()
    {
        var link = new HyperlinkButton("Link");
        new LayoutEngine().Layout(link, new Size(20, 3));

        link.DesiredSize.ShouldBe(new Size(4, 1));
    }

    /// <summary>Verifies the control renders text content in the expected cells.</summary>
    [Fact]
    public void Render_WhenTextIsSet_WritesTextCells()
    {
        var link = new HyperlinkButton("Go");
        new LayoutEngine().Layout(link, new Size(4, 1));
        using Frame frame = new(new Size(4, 1));

        link.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("G");
        FrameOracle.Get(frame, new Point(1, 0)).ShouldBe("o");
    }

    /// <summary>Verifies the link contributes only its normal-state accent/underline presentation to
    /// the profile instead of a complete constructor Face, so IsFocused and Disabled still fold in the
    /// theme's own state attributes - a complete Face would have made every state identical.</summary>
    [Fact]
    public async Task GetActualFace_WhenStateChanges_DiffersFromNormalAsync()
    {
        var link = new HyperlinkButton("Visit");
        await using var surface = await ComponentSurface.MountAsync(
            link,
            new Size(10, 1),
            TestContext.Current.CancellationToken);

        var normal = link.GetActualFace(VisualState.Normal);
        var focused = link.GetActualFace(VisualState.Focused);
        var disabled = link.GetActualFace(VisualState.Disabled);

        focused.ShouldNotBe(normal);
        disabled.ShouldNotBe(normal);
        normal.Underline.ShouldBe(Underline.Straight);
        focused.Underline.ShouldBe(Underline.Straight);
    }

    /// <summary>Verifies a caller-assigned complete Face still outranks the link's own accent
    /// presentation contribution in every state, matching the documented precedence contract: a
    /// complete local Face suppresses ambient inheritance unconditionally.</summary>
    [Fact]
    public void Face_WhenAssignedExplicitly_OutranksLinkPresentationInEveryState()
    {
        var custom = new Face(Color.Rgb(1, 2, 3), Color.Rgb(4, 5, 6), default, Underline.None, Color.Default);
        var link = new HyperlinkButton("Visit") { Face = custom };

        link.GetActualFace(VisualState.Normal).ShouldBe(custom);
        link.GetActualFace(VisualState.Focused).ShouldBe(custom);
        link.GetActualFace(VisualState.Disabled).ShouldBe(custom);
    }

    /// <summary>Verifies desired width grows by exactly one reserved column per set affix, plus
    /// the shared theme gap, over an equivalent captionless link with neither set.</summary>
    [Theory]
    [InlineData(false, false, 0)]
    [InlineData(true, false, 2)]
    [InlineData(false, true, 2)]
    [InlineData(true, true, 4)]
    public void Measure_WhenAffixesAreSet_ReservesCellsPerAffixPlusGap(
        bool hasStart,
        bool hasEnd,
        int expectedExtraWidth)
    {
        var link = new HyperlinkButton
        {
            StartAffix = hasStart ? new Affix("!") : null,
            EndAffix = hasEnd ? new Affix("!") : null
        };

        new LayoutEngine().Layout(link, new Size(20, 3));

        link.DesiredSize.Width.ShouldBe(expectedExtraWidth);
    }

    /// <summary>Verifies null-to-set and set-to-null affix assignment requires Measure.</summary>
    [Fact]
    public void StartAffix_WhenAssignedOrCleared_InvalidatesMeasure()
    {
        using var link = new HyperlinkButton("Visit");
        link.Clear(Invalidation.All);

        link.StartAffix = new Affix("!");

        link.Pending.ShouldBe(Invalidation.All);
        link.Clear(Invalidation.All);

        link.StartAffix = null;

        link.Pending.ShouldBe(Invalidation.All);
    }

    /// <summary>Verifies a same-resolved-width content or color swap invalidates rendering only,
    /// the exact grading an animated affix (a spinner swapping frames) depends on.</summary>
    [Fact]
    public void StartAffix_WhenContentOrColorChangesAtTheSameResolvedWidth_InvalidatesRenderOnly()
    {
        using var link = new HyperlinkButton("Visit") { StartAffix = new Affix("|") };
        link.Clear(Invalidation.All);

        link.StartAffix = new Affix("/");

        link.Pending.ShouldBe(Invalidation.Render);
        link.Clear(Invalidation.All);

        link.StartAffix = new Affix("/", "?", SemanticColor.Warning);

        link.Pending.ShouldBe(Invalidation.Render);
    }

    /// <summary>Verifies a resolved-width change (one cell to two cells) invalidates Measure again,
    /// not just Render, even though both values are non-null.</summary>
    [Fact]
    public void EndAffix_WhenResolvedWidthChanges_InvalidatesMeasure()
    {
        using var link = new HyperlinkButton("Visit") { EndAffix = new Affix("!") };
        link.Clear(Invalidation.All);

        // U+4E16 '世' is a wide CJK ideograph (two cells wide), unlike the one-cell '!' above.
        link.EndAffix = new Affix("世");

        link.Pending.ShouldBe(Invalidation.All);
    }

    /// <summary>Verifies reassigning the identical affix value is a no-op, matching every other
    /// SetProperty-backed member.</summary>
    [Fact]
    public void StartAffix_WhenReassignedTheSameValue_DoesNotInvalidate()
    {
        var affix = new Affix("!");
        using var link = new HyperlinkButton("Visit") { StartAffix = affix };
        link.Clear(Invalidation.All);

        link.StartAffix = affix;

        link.Pending.ShouldBe(Invalidation.None);
    }
}
