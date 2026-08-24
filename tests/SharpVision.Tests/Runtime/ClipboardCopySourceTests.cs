// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;

/// <summary>Verifies application clipboard-copy fallback across focused control ancestry.</summary>
public sealed class ClipboardCopySourceTests
{
    /// <summary>Verifies a focused source publishes its owned result through the application buffer.</summary>
    [Fact]
    public async Task Input_WhenFocusedControlIsCopySource_PublishesReturnedTextOnceAsync()
    {
        var target = new TextInput();
        var source = new ClipboardCopySourceProbe("copied", target);
        await using var surface = await ComponentSurface.MountAsync(
            source,
            new Size(20, 4),
            TestContext.Current.CancellationToken);
        await FocusAsync(surface, source);

        await SendShortcutAsync(surface, 'c');
        await FocusAsync(surface, target);
        await SendShortcutAsync(surface, 'v');

        source.CopyCalls.ShouldBe(1);
        target.Text.ShouldBe("copied");
    }

    /// <summary>Verifies the focused source wins over a matching source in its parent chain.</summary>
    [Fact]
    public async Task Input_WhenFocusedSourceIsNested_UsesNearestSourceAsync()
    {
        var inner = new ClipboardCopySourceProbe("inner");
        var outer = new ClipboardCopySourceProbe("outer", inner);
        await using var surface = await ComponentSurface.MountAsync(
            outer,
            new Size(20, 4),
            TestContext.Current.CancellationToken);
        await FocusAsync(surface, inner);

        await SendShortcutAsync(surface, 'c');

        inner.CopyCalls.ShouldBe(1);
        outer.CopyCalls.ShouldBe(0);
    }

    /// <summary>Verifies an empty nearest result consumes copy without consulting a stale ancestor.</summary>
    [Fact]
    public async Task Input_WhenNearestSourceReturnsEmpty_DoesNotFallThroughAsync()
    {
        var inner = new ClipboardCopySourceProbe(string.Empty);
        var outer = new ClipboardCopySourceProbe("stale", inner);
        await using var surface = await ComponentSurface.MountAsync(
            outer,
            new Size(20, 4),
            TestContext.Current.CancellationToken);
        await FocusAsync(surface, inner);

        await SendShortcutAsync(surface, 'c');

        inner.CopyCalls.ShouldBe(1);
        outer.CopyCalls.ShouldBe(0);
    }

    /// <summary>Verifies an ordinary focused child resolves the first copy source in its ancestry.</summary>
    [Fact]
    public async Task Input_WhenFocusedChildIsNotCopySource_UsesAncestorSourceAsync()
    {
        var child = new ProbeControl { IsFocusable = true };
        var source = new ClipboardCopySourceProbe("ancestor", child);
        await using var surface = await ComponentSurface.MountAsync(
            source,
            new Size(20, 4),
            TestContext.Current.CancellationToken);
        await FocusAsync(surface, child);

        await SendShortcutAsync(surface, 'c');

        source.CopyCalls.ShouldBe(1);
    }

    /// <summary>Verifies copy-source ancestry cannot escape the captured active modal plane.</summary>
    [Fact]
    public async Task Input_WhenCopySourceAncestorIsOutsideModalBoundary_DoesNotUseSourceAsync()
    {
        var target = new ProbeControl { IsFocusable = true };
        var plane = new ProbeContainer { Children = { target } };
        var outside = new ClipboardCopySourceProbe("outside", plane);
        await using var surface = await ComponentSurface.MountAsync(
            outside,
            new Size(20, 4),
            TestContext.Current.CancellationToken);
        ModalScope? scope = null;
        await surface.Application.Dispatcher.InvokeAsync(
            () => { scope = surface.Application.Modality.Enter(plane, initialFocus: target); },
            TestContext.Current.CancellationToken);

        await SendShortcutAsync(surface, 'c');

        outside.CopyCalls.ShouldBe(0);
        await surface.Application.Dispatcher.InvokeAsync(
            () => scope.ShouldNotBeNull().Dispose(),
            TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies the captured modal boundary remains eligible as the nearest copy source.</summary>
    [Fact]
    public async Task Input_WhenModalBoundaryIsCopySource_UsesBoundaryForFocusedDescendantAsync()
    {
        var target = new ProbeControl { IsFocusable = true };
        var boundary = new ClipboardCopySourceProbe("boundary", target);
        await using var surface = await ComponentSurface.MountAsync(
            boundary,
            new Size(20, 4),
            TestContext.Current.CancellationToken);
        ModalScope? scope = null;
        await surface.Application.Dispatcher.InvokeAsync(
            () => { scope = surface.Application.Modality.Enter(boundary, initialFocus: target); },
            TestContext.Current.CancellationToken);

        await SendShortcutAsync(surface, 'c');

        boundary.CopyCalls.ShouldBe(1);
        await surface.Application.Dispatcher.InvokeAsync(
            () => scope.ShouldNotBeNull().Dispose(),
            TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies routed interception prevents the application copy fallback.</summary>
    [Fact]
    public async Task Input_WhenRoutedControlCopyIsHandled_DoesNotUseFallbackAsync()
    {
        var source = new ClipboardCopySourceProbe("blocked");
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 4)));
        _ = source.AddHandler(Events.Key, (_, eventArgs) =>
        {
            if (eventArgs.Phase == RoutingPhase.Preview && IsControlC(eventArgs.Stroke))
            {
                eventArgs.IsHandled = true;
            }
        });
        await using Application application = new(
            source,
            terminal,
            terminal,
            TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(
            () => application.Focus.Focus(source).ShouldBeTrue(),
            TestContext.Current.CancellationToken);
        var stroke = new Stroke(
            Code.Character,
            new Rune('c'),
            nativeCode: 0,
            Modifiers.Control,
            KeyAction.Press);

        application.Input(in stroke);
        await application.Dispatcher.InvokeAsync(
            static () => { },
            TestContext.Current.CancellationToken);

        source.CopyCalls.ShouldBe(0);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies copy accepts only pressed Control+C while tolerating lock modifiers.</summary>
    [Fact]
    public async Task Input_WhenStrokeIsNotEligible_DoesNotCopyAndToleratesLocksAsync()
    {
        var source = new ClipboardCopySourceProbe("copy");
        await using var surface = await ComponentSurface.MountAsync(
            source,
            new Size(20, 4),
            TestContext.Current.CancellationToken);
        await FocusAsync(surface, source);

        foreach (var input in new[]
                 {
                     "\u001b[99;1u",
                     "\u001b[99;3u",
                     "\u001b[99;6u",
                     "\u001b[99;5:2u",
                     "\u001b[99;5:3u",
                     "\u001b[13;5u",
                     "\u001b[120;5u"
                 })
        {
            await surface.SendAsync(Encoding.ASCII.GetBytes(input), "send ineligible clipboard stroke");
        }

        source.CopyCalls.ShouldBe(0);

        foreach (var input in new[] { "\u001b[99;69u", "\u001b[99;133u", "\u001b[67;197u" })
        {
            await surface.SendAsync(Encoding.ASCII.GetBytes(input), "send lock-tolerant clipboard stroke");
        }

        source.CopyCalls.ShouldBe(3);
    }

    /// <summary>Verifies generic copy sources do not acquire TextInput-only cut or paste behavior.</summary>
    [Fact]
    public async Task Input_WhenGenericSourceReceivesCutOrPaste_DoesNotHandleEditCommandAsync()
    {
        var source = new ClipboardCopySourceProbe("copy");
        var unhandled = 0;
        _ = source.AddHandler(Events.Key, (_, eventArgs) =>
        {
            if (eventArgs.Phase == RoutingPhase.Bubble &&
                eventArgs.Stroke.Character is { Value: 'x' or 'v' })
            {
                eventArgs.IsHandled.ShouldBeFalse();
                unhandled++;
            }
        });
        await using var surface = await ComponentSurface.MountAsync(
            source,
            new Size(20, 4),
            TestContext.Current.CancellationToken);
        await FocusAsync(surface, source);

        await SendShortcutAsync(surface, 'x');
        await SendShortcutAsync(surface, 'v');

        source.CopyCalls.ShouldBe(0);
        unhandled.ShouldBe(2);
    }

    /// <summary>Verifies TextInput retains copy, cut, paste, undo, and password-disclosure policy.</summary>
    [Fact]
    public async Task Input_WhenTextInputUsesClipboardCommands_PreservesEditingAndPasswordPolicyAsync()
    {
        var source = new TextInput { Text = "safe" };
        var password = new TextInput { Text = "secret", PasswordCharacter = new Rune('*') };
        var target = new TextInput();
        var root = new Stack { Children = { source, password, target } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 6),
            TestContext.Current.CancellationToken);
        await FocusAndSelectAllAsync(surface, source);

        await SendShortcutAsync(surface, 'c');
        await FocusAndSelectAllAsync(surface, password);
        await SendShortcutAsync(surface, 'c');
        await FocusAsync(surface, target);
        await SendShortcutAsync(surface, 'v');
        await FocusAndSelectAllAsync(surface, target);
        await SendShortcutAsync(surface, 'x');
        await FocusAsync(surface, source);
        await surface.Application.Dispatcher.InvokeAsync(
            () => { source.CaretIndex = source.Text.Length; },
            TestContext.Current.CancellationToken);
        await SendShortcutAsync(surface, 'v');

        target.Text.ShouldBeEmpty();
        target.CanUndo.ShouldBeTrue();
        password.Text.ShouldBe("secret");
        source.Text.ShouldBe("safesafe");
    }

    private static bool IsControlC(Stroke stroke) =>
        stroke.Action == KeyAction.Press &&
        stroke.Code == Code.Character &&
        stroke.Character == new Rune('c') &&
        (stroke.Modifiers & ~(Modifiers.CapsLock | Modifiers.NumLock)) == Modifiers.Control;

    private static Task SendShortcutAsync(ComponentSurface surface, char character) =>
        surface.SendAsync(
            Encoding.ASCII.GetBytes(FormattableString.Invariant($"\u001b[{(int) character};5u")),
            $"press Control+{character}");

    private static async Task FocusAsync(ComponentSurface surface, ControlBase control) =>
        await surface.Application.Dispatcher.InvokeAsync(
            () => surface.Application.Focus.Focus(control).ShouldBeTrue(),
            TestContext.Current.CancellationToken);

    private static async Task FocusAndSelectAllAsync(ComponentSurface surface, TextInput input) =>
        await surface.Application.Dispatcher.InvokeAsync(
            () =>
            {
                surface.Application.Focus.Focus(input).ShouldBeTrue();
                input.Select(0, input.Text.Length);
            },
            TestContext.Current.CancellationToken);
}
