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

        link.Text.ShouldBeNull();
        link.Content.ShouldBeNull();
        link.Command.ShouldBeNull();
        link.CommandParameter.ShouldBeNull();
        link.CanFocus.ShouldBeTrue();
        link.Face.Underline.ShouldBe(Underline.Straight);
        link.ActualBorder.Sides.ShouldBe(BorderSide.None);
        link.Padding.ShouldBe(default);
        link.ActualShadow.IsVisible.ShouldBeFalse();
    }

    /// <summary>Verifies the string constructor sets text content.</summary>
    [Fact]
    public void Constructor_WithText_SetsTextContent()
    {
        var link = new HyperlinkButton("Visit");
        link.Text.ShouldBe("Visit");
        link.Content.ShouldBeOfType<ControlText>().Content.ShouldBe("Visit");
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
        var content = link.Content;

        link.Text = "After";

        link.Text.ShouldBe("After");
        link.Content.ShouldBeSameAs(content);
    }

    /// <summary>Verifies the Text property setter creates content when none exists.</summary>
    [Fact]
    public void Text_WhenSetOnEmptyControl_CreatesContent()
    {
        var link = new HyperlinkButton { Text = "New" };

        link.Text.ShouldBe("New");
        link.Content.ShouldBeOfType<ControlText>().Content.ShouldBe("New");
    }

    /// <summary>Verifies the Text property setter rejects null.</summary>
    [Fact]
    public void Text_WhenSetToNull_Throws()
    {
        var link = new HyperlinkButton("Visit");

        _ = Should.Throw<ArgumentNullException>(() => link.Text = null);
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
    /// the profile instead of a complete constructor Face, so Focused and Disabled still fold in the
    /// theme's own state attributes - a complete Face would have made every state identical (see #162).</summary>
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
}
