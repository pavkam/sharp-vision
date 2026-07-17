// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;



using LayoutEngine = Engine;
using TextControl = ControlText;
using TextInputControl = TextInput;

/// <summary>Verifies one immutable Unicode cell policy reaches the complete tree.</summary>
public sealed class CellPolicyTests
{
    /// <summary>Verifies first layout and frame share the wide ambiguous policy.</summary>
    [Fact]
    public async Task StartAsync_WhenAmbiguousWidthIsWide_MeasuresTextAsWideAsync()
    {
        // Arrange
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(4, 1)));
        var text = new TextControl() { Content = "·" };
        var capabilities = Capabilities.Conservative with
        {
            AmbiguousWidth = Ambiguous.Wide,
        };
        await using Application application = new(
            text,
            terminal,
            terminal,
            TerminalOptions.Minimal with { Capabilities = capabilities });

        // Act
        await application.StartAsync(TestContext.Current.CancellationToken);

        // Assert
        application.CellPolicy.AmbiguousWidth.ShouldBe(Ambiguous.Wide);
        text.CellPolicy.ShouldBeSameAs(application.CellPolicy);
        text.DesiredSize.Width.ShouldBe(2);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies children added after attachment inherit the current reference.</summary>
    [Fact]
    public async Task Children_WhenAddedAfterAttachment_InheritCurrentPolicyAsync()
    {
        // Arrange
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var policy = new Policy(Ambiguous.Wide);
            root.Attach(dispatcher, policy);
            var child = new ProbeControl();

            // Act
            root.Children.Add(child);

            // Assert
            root.CellPolicy.ShouldBeSameAs(policy);
            child.CellPolicy.ShouldBeSameAs(policy);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies display and editing geometry consume the inherited policy.</summary>
    [Fact]
    public async Task Layout_WhenTextConsumersInheritWidePolicy_MeasuresConsistentlyAsync()
    {
        // Arrange
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var policy = new Policy(Ambiguous.Wide);
            var text = new TextControl("<b>·</b>");
            var input = new TextInputControl() { BorderThickness = default, Text = "·" };
            text.Attach(dispatcher, policy);
            input.Attach(dispatcher, policy);
            var engine = new LayoutEngine();

            // Act
            engine.Layout(text, new Size(10, 2));
            engine.Layout(input, new Size(10, 2));

            // Assert
            text.DesiredSize.Width.ShouldBe(2);
            input.DesiredSize.Width.ShouldBe(2);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a profile update replaces policy before one new measure.</summary>
    [Fact]
    public async Task Profile_WhenGeometryChanges_ReplacesTreePolicyAndRemeasuresAsync()
    {
        // Arrange
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(4, 1)));
        var text = new TextControl() { Content = "·" };
        await using Application application = new(
            text,
            terminal,
            terminal,
            TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        var previous = application.CellPolicy;
        var frames = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        application.FrameRendered += (_, _) => _ = frames.TrySetResult();
        var wide = Capabilities.Conservative with
        {
            AmbiguousWidth = Ambiguous.Wide,
        };

        // Act
        application.Profile(wide);
        await frames.Task.WaitAsync(TestContext.Current.CancellationToken);

        // Assert
        application.CellPolicy.ShouldNotBeSameAs(previous);
        application.CellPolicy.AmbiguousWidth.ShouldBe(Ambiguous.Wide);
        text.CellPolicy.ShouldBeSameAs(application.CellPolicy);
        text.DesiredSize.Width.ShouldBe(2);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }
}
