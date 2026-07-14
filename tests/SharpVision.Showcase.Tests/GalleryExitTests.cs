// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Tests;

using System.Text;

using SharpVision.Runtime;
using SharpVision.Showcase.Tests.Support;
using SharpVision.Terminal.Runtime;

using ControlText = SharpVision.Controls.Text;

/// <summary>Verifies the running gallery exits through its visible control and Ctrl+C.</summary>
public sealed class GalleryExitTests
{
    /// <summary>Verifies activating the sidebar Quit button stops the application cleanly.</summary>
    [Fact]
    public async Task Activation_WhenQuitButtonIsActivated_StopsApplicationAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(100, 30)));
        using Gallery gallery = new();
        await using Application application = new(
            gallery,
            terminal,
            terminal,
            ShowcaseStartupOptions.Create(new Dictionary<string, string?>()));
        gallery.Attach(application);
        await application.StartAsync(TestContext.Current.CancellationToken);

        Button? quit = await application.Dispatcher.InvokeAsync(
            () => Find<Button>(gallery.Sidebar, static value => value.Content is ControlText { Content: "Quit" }),
            TestContext.Current.CancellationToken);
        Button button = quit.ShouldNotBeNull();

        await application.Dispatcher.InvokeAsync(
            button.PerformClick,
            TestContext.Current.CancellationToken);

        await application.Completion.WaitAsync(
            TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken);
        application.Failure.ShouldBeNull();
    }

    /// <summary>Verifies a decoded Ctrl+C key stops the application even when the terminal reports it
    /// as a key event instead of raising a host cancellation signal.</summary>
    [Fact]
    public async Task Input_WhenCtrlCIsPressed_StopsApplicationAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(100, 30)));
        using Gallery gallery = new();
        await using Application application = new(
            gallery,
            terminal,
            terminal,
            ShowcaseStartupOptions.Create(new Dictionary<string, string?>()));
        gallery.Attach(application);
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(
            () => application.Focus.Focus(gallery.Navigation[0]).ShouldBeTrue(),
            TestContext.Current.CancellationToken);

        // Kitty keyboard protocol reports Ctrl+C as CSI 99;5u, decoded to Character 'c' with Control.
        terminal.QueueInput(Encoding.ASCII.GetBytes("\u001b[99;5u"));

        await application.Completion.WaitAsync(
            TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken);
        application.Failure.ShouldBeNull();
    }

    private static T? Find<T>(Control root, Func<T, bool> predicate)
        where T : Control
    {
        if (root is T match && predicate(match))
        {
            return match;
        }

        if (root is Container container)
        {
            foreach (Control child in container.Children)
            {
                if (Find(child, predicate) is { } found)
                {
                    return found;
                }
            }
        }

        return null;
    }
}
