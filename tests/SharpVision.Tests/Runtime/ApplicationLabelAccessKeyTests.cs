// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;

/// <summary>Verifies Text.AccessKeyTarget focuses its declared target directly instead of the
/// default next-tab-stop traversal for a standalone label-like caption.</summary>
public sealed class ApplicationLabelAccessKeyTests
{
    /// <summary>Verifies the mnemonic focuses the declared target even when an unrelated control
    /// sits between the label and its target in tab order — the exact scenario the default
    /// next-tab-stop behavior gets wrong.</summary>
    [Fact]
    public async Task Input_WhenLabelHasAccessKeyTarget_FocusesTargetDespiteInterveningTabStopAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 4)));
        var unrelated = new TextInput { Text = "unrelated" };
        var target = new TextInput { Text = "target" };
        var label = new ControlText("&Name") { UseMnemonic = true };
        var root = new Stack { Children = { label, unrelated, target } };
        label.AccessKeyTarget = target;
        await using Application application = new(root, terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        var stroke = Alt('n');

        application.Input(in stroke);
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        application.Focus.Focused.ShouldBeSameAs(target);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies an unset target preserves the documented default: focus moves to the next
    /// tab stop after the label.</summary>
    [Fact]
    public async Task Input_WhenLabelHasNoAccessKeyTarget_MovesToNextTabStopAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 4)));
        var next = new TextInput { Text = "next" };
        var label = new ControlText("&Name") { UseMnemonic = true };
        var root = new Stack { Children = { label, next } };
        await using Application application = new(root, terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        var stroke = Alt('n');

        application.Input(in stroke);
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        application.Focus.Focused.ShouldBeSameAs(next);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a disabled target declines the access key deterministically instead of
    /// falling back to tab-stop traversal or throwing.</summary>
    [Fact]
    public async Task Input_WhenAccessKeyTargetIsDisabled_DeclinesWithoutChangingFocusAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 4)));
        var target = new TextInput { Text = "target", Enabled = false };
        var next = new TextInput { Text = "next" };
        var label = new ControlText("&Name") { UseMnemonic = true };
        var root = new Stack { Children = { label, target, next } };
        label.AccessKeyTarget = target;
        await using Application application = new(root, terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        var stroke = Alt('n');

        application.Input(in stroke);
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        application.Focus.Focused.ShouldBeNull();
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a target that does not belong to the same tree declines deterministically
    /// rather than throwing.</summary>
    [Fact]
    public async Task Input_WhenAccessKeyTargetIsDetached_DeclinesWithoutThrowingAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 4)));
        var detachedTarget = new TextInput { Text = "detached" };
        var label = new ControlText("&Name") { UseMnemonic = true };
        var root = new Stack { Children = { label } };
        label.AccessKeyTarget = detachedTarget;
        await using Application application = new(root, terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        var stroke = Alt('n');

        application.Input(in stroke);
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        application.Focus.Focused.ShouldBeNull();
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    private static Stroke Alt(char value) =>
        new(Code.Character, new Rune(value), nativeCode: 0, Modifiers.Alt, KeyAction.Press);
}
