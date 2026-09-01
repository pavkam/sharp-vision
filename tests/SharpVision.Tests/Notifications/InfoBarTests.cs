// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Notifications;

/// <summary>Verifies InfoBar state, content ownership, and dismissal ordering.</summary>
public sealed class InfoBarTests
{
    /// <summary>Verifies the documented open informational defaults.</summary>
    [Fact]
    public void Constructor_WhenCreated_HasDocumentedDefaults()
    {
        var bar = new InfoBar();

        bar.Title.ShouldBeNull();
        bar.Adornment.ShouldBeNull();
        bar.IsOpen.ShouldBeTrue();
        bar.IsDismissible.ShouldBeTrue();
        bar.Style.ShouldBeNull();
        InfoBarStyle.Default.ShouldBeSameAs(InfoBarStyle.Info);
        bar.CanFocus.ShouldBeFalse();
        bar.CanTabStop.ShouldBeFalse();
    }

    /// <summary>Verifies invalid title text is rejected without changing retained state.</summary>
    [Fact]
    public void Title_WhenTextContainsControlCluster_ThrowsBeforeMutation()
    {
        var bar = new InfoBar { Title = "Current" };

        _ = Should.Throw<ArgumentException>(() => bar.Title = "bad\u0007title");

        bar.Title.ShouldBe("Current");
    }

    /// <summary>Verifies a cancellation request keeps the bar open and suppresses completion.</summary>
    [Fact]
    public void Dismiss_WhenRequestIsCancelled_RemainsOpen()
    {
        var bar = new InfoBar();
        var dismissed = 0;
        bar.DismissRequested += (_, eventArgs) => eventArgs.Cancel = true;
        bar.Dismissed += (_, _) => dismissed++;

        bar.Dismiss();

        bar.IsOpen.ShouldBeTrue();
        dismissed.ShouldBe(0);
    }

    /// <summary>Verifies successful dismissal commits state before ordered completion.</summary>
    [Fact]
    public void Dismiss_WhenAllowed_ClosesBeforeDismissed()
    {
        var bar = new InfoBar();
        List<string> order = [];
        bar.DismissRequested += (_, _) => order.Add($"requested:{bar.IsOpen}");
        bar.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(InfoBar.IsOpen))
            {
                order.Add($"property:{bar.IsOpen}");
            }
        };
        bar.Dismissed += (_, _) => order.Add($"dismissed:{bar.IsOpen}");

        bar.Dismiss();

        order.ShouldBe(["requested:True", "property:False", "dismissed:False"]);
    }

    /// <summary>Verifies caller content remains owned through the inherited single-content slot.</summary>
    [Fact]
    public void Content_WhenAssigned_UsesContentControlOwnership()
    {
        var body = new Button { Text = "Retry" };
        var bar = new InfoBar { Content = body };

        bar.Content.ShouldBeSameAs(body);
        body.Parent.ShouldBeSameAs(bar);

        bar.Content = null;

        body.Parent.ShouldBeNull();
    }

    /// <summary>Verifies closing imposes availability without losing the caller's latest visibility request.</summary>
    [Fact]
    public void IsOpen_WhenContentVisibilityChangesWhileClosed_RestoresLatestAuthoredValue()
    {
        var body = new ProbeControl { Visibility = Visibility.Hidden };
        var bar = new InfoBar { Content = body, IsOpen = false };
        body.Visibility.ShouldBe(Visibility.Collapsed);

        body.Visibility = Visibility.Visible;
        body.Visibility.ShouldBe(Visibility.Collapsed);

        bar.IsOpen = true;
        body.Visibility.ShouldBe(Visibility.Visible);
    }

    /// <summary>Verifies replacement retires the old availability lease and gates the new generation.</summary>
    [Fact]
    public void Content_WhenReplacedWhileClosed_RestoresOldAndCollapsesNewContent()
    {
        var previous = new ProbeControl { Visibility = Visibility.Hidden };
        var current = new ProbeControl();
        var bar = new InfoBar { Content = previous, IsOpen = false };

        bar.Content = current;

        previous.Parent.ShouldBeNull();
        previous.Visibility.ShouldBe(Visibility.Hidden);
        current.Parent.ShouldBeSameAs(bar);
        current.Visibility.ShouldBe(Visibility.Collapsed);

        bar.IsOpen = true;
        current.Visibility.ShouldBe(Visibility.Visible);
    }

    /// <summary>Verifies requested failures do not prevent required close publication and preserve earliest failure.</summary>
    [Fact]
    public void Dismiss_WhenSubscribersThrow_CommitsAndAttemptsDismissedBeforeRethrow()
    {
        var bar = new InfoBar();
        var requestedFailure = new InvalidOperationException("requested");
        var dismissedRan = false;
        bar.DismissRequested += (_, _) => throw requestedFailure;
        bar.DismissRequested += (_, _) => { };
        bar.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(InfoBar.IsOpen))
            {
                throw new InvalidOperationException("property");
            }
        };
        bar.Dismissed += (_, _) =>
        {
            dismissedRan = true;
            throw new InvalidOperationException("dismissed");
        };

        var exception = Should.Throw<InvalidOperationException>(bar.Dismiss);

        exception.ShouldBeSameAs(requestedFailure);
        bar.IsOpen.ShouldBeFalse();
        dismissedRan.ShouldBeTrue();
    }

    /// <summary>Verifies a reentrant reopen request supersedes the stale dismissal before it commits.</summary>
    [Fact]
    public void Dismiss_WhenRequestedHandlerReopens_DoesNotPublishStaleClose()
    {
        var bar = new InfoBar();
        var dismissed = 0;
        bar.DismissRequested += (_, _) => bar.IsOpen = true;
        bar.Dismissed += (_, _) => dismissed++;

        bar.Dismiss();

        bar.IsOpen.ShouldBeTrue();
        dismissed.ShouldBe(0);
    }
}
