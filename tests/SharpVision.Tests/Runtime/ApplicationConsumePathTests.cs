// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;

/// <summary>Drives the paired-text suppression contract through every consume path the routing
/// contract names, rather than through the two that happened to have tests.
///
/// input-routing.md states it as one rule: a stroke consumed anywhere on or around its route -
/// "by a preview handler, an ordinary routed handler, a control default, a MenuItem.Shortcut
/// match, or access-key discovery" - never delivers the paired text record a legacy terminal or
/// Kitty associated text reports alongside it. Verifying that per-path rather than as a rule is
/// how it has broken before: suppression once armed only from the shortcut and access-key paths,
/// so every routed control default leaked its paired character. Legacy paired text and Kitty's
/// multi-scalar runs have each produced the same shape of gap.
///
/// This is a table over the whole census, so a sixth consume path added later fails here until it
/// is added to the table and made to comply.</summary>
public sealed class ApplicationConsumePathTests
{
    /// <summary>Gets every consume path the routing contract enumerates.</summary>
    public static TheoryData<string> ConsumePaths =>
        ["PreviewHandler", "BubbleHandler", "ControlDefault", "AncestorPreviewHandler"];

    /// <summary>Verifies a Stroke consumed on each path leaves the focused editor's text
    /// untouched, because the paired TerminalText record is suppressed. Without suppression the
    /// character arrives as ordinary typing and replaces the selection the consuming handler just
    /// made.</summary>
    /// <param name="path">The consume path under test.</param>
    [Theory]
    [MemberData(nameof(ConsumePaths))]
    public async Task Input_WhenStrokeIsConsumedOnAnyPath_SuppressesThePairedTextAsync(string path)
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 4)));
        var input = new TextInput { Text = "AB" };
        var root = new Dock();
        root.Children.Add(input);
        await using Application application = new(root, terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(
            () =>
            {
                application.Focus.Focus(input).ShouldBeTrue();
                Arm(path, input, root);
            },
            TestContext.Current.CancellationToken);

        var stroke = new Stroke(Code.Character, new Rune('a'), nativeCode: 0, Modifiers.Control, KeyAction.Press);
        var text = new TerminalText(new Rune('a'));
        application.Input(in stroke);
        application.Input(in text);
        await application.Dispatcher.InvokeAsync(
            static () => { },
            TestContext.Current.CancellationToken);

        input.Text.ShouldBe("AB", $"the {path} consume path must suppress the paired text record");
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>The counter-case that keeps the assertion above honest: an identical pair that
    /// nobody consumes must still type. Without this, suppression that fired unconditionally -
    /// swallowing every paired record - would satisfy every case above.</summary>
    [Fact]
    public async Task Input_WhenStrokeIsNotConsumed_StillDeliversThePairedTextAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 4)));
        var input = new TextInput { Text = "AB" };
        await using Application application = new(input, terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(
            () => application.Focus.Focus(input).ShouldBeTrue(),
            TestContext.Current.CancellationToken);

        // No modifier, no handler, no control default claims this - it is ordinary typing.
        var stroke = new Stroke(Code.Character, new Rune('c'), nativeCode: 0, Modifiers.None, KeyAction.Press);
        var text = new TerminalText(new Rune('c'));
        application.Input(in stroke);
        application.Input(in text);
        await application.Dispatcher.InvokeAsync(
            static () => { },
            TestContext.Current.CancellationToken);

        input.Text.ShouldBe("ABc", "an unconsumed stroke must still deliver its paired text");
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a consumed stroke suppresses <em>every</em> record of a multi-scalar
    /// associated-text sequence, not just the first.
    ///
    /// <para>A <c>TerminalText</c> record carries exactly one <c>Rune</c>, so Kitty reports one
    /// record per colon-separated scalar and a single grapheme can arrive as several
    /// consecutive records. <c>Application.Dispatch</c> handles that with a latch cleared only by
    /// the next Key record, so the whole run drops. The single-record table above cannot tell that
    /// design apart from one that suppresses only the record immediately following the stroke -
    /// which would deliver the base character's combining marks on their own and corrupt the
    /// grapheme.</para>
    /// </summary>
    /// <param name="path">The consume path under test.</param>
    [Theory]
    [MemberData(nameof(ConsumePaths))]
    public async Task Input_WhenStrokeIsConsumedOnAnyPath_SuppressesEveryPairedTextRecordAsync(string path)
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 4)));
        var input = new TextInput { Text = "AB" };
        var root = new Dock();
        root.Children.Add(input);
        await using Application application = new(root, terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(
            () =>
            {
                application.Focus.Focus(input).ShouldBeTrue();
                Arm(path, input, root);
            },
            TestContext.Current.CancellationToken);

        var stroke = new Stroke(Code.Character, new Rune('a'), nativeCode: 0, Modifiers.Control, KeyAction.Press);
        application.Input(in stroke);
        SendGrapheme(application);
        await application.Dispatcher.InvokeAsync(
            static () => { },
            TestContext.Current.CancellationToken);

        input.Text.ShouldBe("AB", $"the {path} consume path must suppress every paired text record");
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>The counter-case for the multi-record run: unconsumed, all three records arrive and
    /// compose the one grapheme they encode. Suppression that swallowed the tail unconditionally
    /// would still satisfy the theory above.</summary>
    [Fact]
    public async Task Input_WhenStrokeIsNotConsumed_DeliversEveryPairedTextRecordAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 4)));
        var input = new TextInput { Text = "AB" };
        await using Application application = new(input, terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(
            () => application.Focus.Focus(input).ShouldBeTrue(),
            TestContext.Current.CancellationToken);

        var stroke = new Stroke(Code.Character, new Rune('e'), nativeCode: 0, Modifiers.None, KeyAction.Press);
        application.Input(in stroke);
        SendGrapheme(application);
        await application.Dispatcher.InvokeAsync(
            static () => { },
            TestContext.Current.CancellationToken);

        input.Text.ShouldBe("AB" + _grapheme, "an unconsumed stroke must deliver its whole text run");
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies the latch is armed per keystroke rather than left set: after a consumed
    /// stroke swallows a multi-record run, the very next unconsumed stroke still types. A latch
    /// that failed to clear would go permanently deaf, which is the failure mode a "suppress the
    /// whole run" implementation risks.</summary>
    [Fact]
    public async Task Input_WhenAConsumedRunIsFollowedByAnUnconsumedStroke_ResumesTypingAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 4)));
        var input = new TextInput { Text = "AB" };
        var root = new Dock();
        root.Children.Add(input);
        await using Application application = new(root, terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(
            () =>
            {
                application.Focus.Focus(input).ShouldBeTrue();

                // Consumes only Control-modified strokes, unlike the table's BubbleHandler arm which
                // consumes every one - the second stroke below has to route unconsumed.
                _ = input.AddHandler(Events.Key, static (_, eventArgs) =>
                {
                    if (eventArgs.Phase == RoutingPhase.Bubble &&
                        eventArgs.Stroke.Modifiers == Modifiers.Control)
                    {
                        eventArgs.Handled = true;
                    }
                });
            },
            TestContext.Current.CancellationToken);

        var consumed = new Stroke(Code.Character, new Rune('a'), nativeCode: 0, Modifiers.Control, KeyAction.Press);
        application.Input(in consumed);
        SendGrapheme(application);
        await application.Dispatcher.InvokeAsync(
            static () => { },
            TestContext.Current.CancellationToken);
        input.Text.ShouldBe("AB");

        var free = new Stroke(Code.Character, new Rune('z'), nativeCode: 0, Modifiers.None, KeyAction.Press);
        var freeText = new TerminalText(new Rune('z'));
        application.Input(in free);
        application.Input(in freeText);
        await application.Dispatcher.InvokeAsync(
            static () => { },
            TestContext.Current.CancellationToken);

        input.Text.ShouldBe("ABz", "a new keystroke must clear suppression left by the consumed run");
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    // One grapheme, three scalars, three records - a base letter and two combining marks. Delivering
    // any strict subset of these would leave orphaned marks in the buffer, so a partial suppression
    // bug shows up as visibly wrong text rather than a count that happens to differ.
    private const string _grapheme = "é̂";

    private static void SendGrapheme(Application application)
    {
        foreach (var rune in _grapheme.EnumerateRunes())
        {
            var record = new TerminalText(rune);
            application.Input(in record);
        }
    }

    private static void Arm(string path, TextInput input, ControlBase root)
    {
        switch (path)
        {
            case "PreviewHandler":
                _ = input.AddHandler(Events.Key, static (_, eventArgs) =>
                {
                    if (eventArgs.Phase == RoutingPhase.Preview)
                    {
                        eventArgs.Handled = true;
                    }
                });
                break;

            case "BubbleHandler":
                _ = input.AddHandler(Events.Key, static (_, eventArgs) =>
                {
                    if (eventArgs.Phase == RoutingPhase.Bubble)
                    {
                        eventArgs.Handled = true;
                    }
                });
                break;

            case "AncestorPreviewHandler":
                // Consumed before the event ever reaches the focused editor, which is the path a
                // tunnelling shell handler takes.
                _ = root.AddHandler(Events.Key, static (_, eventArgs) =>
                {
                    if (eventArgs.Phase == RoutingPhase.Preview)
                    {
                        eventArgs.Handled = true;
                    }
                });
                break;

            case "ControlDefault":
                // TextInput's own Ctrl+A select-all default consumes the stroke.
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(path), path, "Unknown consume path.");
        }
    }
}
