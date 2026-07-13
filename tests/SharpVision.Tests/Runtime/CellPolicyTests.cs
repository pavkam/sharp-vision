// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;

using SharpVision.Runtime;
using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Runtime;
using SharpVision.Terminal.Unicode;


using LayoutEngine = Engine;
using RichTextControl = RichText;
using RunInline = Run;
using TerminalOptions = Terminal.Runtime.Options;
using TextControl = SharpVision.Controls.Text;
using TextInputControl = TextInput;
using TextWrapping = SharpVision.Text.Wrapping;

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
        TextControl text = new() { Content = "·" };
        Capabilities capabilities = Capabilities.Conservative with
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
        await using Dispatcher dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            ProbeContainer root = new();
            Policy policy = new(Ambiguous.Wide);
            root.Attach(dispatcher, policy);
            ProbeControl child = new();

            // Act
            root.Children.Add(child);

            // Assert
            root.CellPolicy.ShouldBeSameAs(policy);
            child.CellPolicy.ShouldBeSameAs(policy);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies rich text and editing geometry consume the inherited policy.</summary>
    [Fact]
    public async Task Layout_WhenTextConsumersInheritWidePolicy_MeasuresConsistentlyAsync()
    {
        // Arrange
        await using Dispatcher dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            Policy policy = new(Ambiguous.Wide);
            RichTextControl rich = new() { Wrapping = TextWrapping.None };
            rich.Inlines.Add(new RunInline("·"));
            TextInputControl input = new() { Text = "·" };
            rich.Attach(dispatcher, policy);
            input.Attach(dispatcher, policy);
            LayoutEngine engine = new();

            // Act
            engine.Layout(rich, new Size(10, 2));
            engine.Layout(input, new Size(10, 2));

            // Assert
            rich.DesiredSize.Width.ShouldBe(2);
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
        TextControl text = new() { Content = "·" };
        await using Application application = new(
            text,
            terminal,
            terminal,
            TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        Policy previous = application.CellPolicy;
        TaskCompletionSource frames = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        application.FrameRendered += (_, _) => _ = frames.TrySetResult();
        Capabilities wide = Capabilities.Conservative with
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
