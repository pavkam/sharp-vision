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

    /// <summary>Verifies a retiring content callback cannot prevent the replacement availability gate.</summary>
    [Fact]
    public void Content_WhenRetiringVisibilityObserverThrows_StillGatesReplacement()
    {
        var previous = new ProbeControl { Visibility = Visibility.Hidden };
        var current = new ProbeControl();
        var bar = new InfoBar { Content = previous, IsOpen = false };
        var failure = new InvalidOperationException("retiring visibility");
        previous.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(ControlBase.Visibility))
            {
                throw failure;
            }
        };

        var exception = Should.Throw<InvalidOperationException>(() => bar.Content = current);

        exception.ShouldBeSameAs(failure);
        previous.Parent.ShouldBeNull();
        previous.Visibility.ShouldBe(Visibility.Hidden);
        current.Parent.ShouldBeSameAs(bar);
        current.Visibility.ShouldBe(Visibility.Collapsed);
    }

    /// <summary>Verifies reopening publishes committed state after availability restoration fails.</summary>
    [Fact]
    public void IsOpen_WhenBodyRestorationObserverThrows_StillPublishesCommittedOpenState()
    {
        var body = new ProbeControl { Visibility = Visibility.Hidden };
        var bar = new InfoBar { Content = body, IsOpen = false };
        var dismiss = OwnedTree.Find<InfoBarDismissButton>(bar).ShouldNotBeNull();
        var restorationFailure = new InvalidOperationException("body restoration");
        var propertyPublished = false;
        body.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(ControlBase.Visibility))
            {
                throw restorationFailure;
            }
        };
        bar.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(InfoBar.IsOpen))
            {
                propertyPublished = true;
                throw new InvalidOperationException("open property");
            }
        };

        var exception = Should.Throw<InvalidOperationException>(() => bar.IsOpen = true);

        exception.ShouldBeSameAs(restorationFailure);
        bar.IsOpen.ShouldBeTrue();
        body.Visibility.ShouldBe(Visibility.Hidden);
        dismiss.Visibility.ShouldBe(Visibility.Visible);
        propertyPublished.ShouldBeTrue();
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

    /// <summary>Verifies cancellation remains authoritative while every requested subscriber is attempted.</summary>
    [Fact]
    public void Dismiss_WhenRequestIsCancelledAndSubscriberThrows_RethrowsAfterKeepingOpen()
    {
        var bar = new InfoBar();
        var failure = new InvalidOperationException("requested");
        var requested = 0;
        var propertyChanges = 0;
        var dismissed = 0;
        bar.DismissRequested += (_, _) =>
        {
            requested++;
            throw failure;
        };
        bar.DismissRequested += (_, eventArgs) =>
        {
            requested++;
            eventArgs.Cancel = true;
        };
        bar.PropertyChanged += (_, eventArgs) => propertyChanges +=
            eventArgs.PropertyName == nameof(InfoBar.IsOpen) ? 1 : 0;
        bar.Dismissed += (_, _) => dismissed++;

        var exception = Should.Throw<InvalidOperationException>(bar.Dismiss);

        exception.ShouldBeSameAs(failure);
        requested.ShouldBe(2);
        propertyChanges.ShouldBe(0);
        dismissed.ShouldBe(0);
        bar.IsOpen.ShouldBeTrue();
    }

    /// <summary>Verifies a nested dismissal request is absorbed by the active transition.</summary>
    [Fact]
    public void Dismiss_WhenRequestedHandlerDismissesAgain_PublishesOnce()
    {
        var bar = new InfoBar();
        var requested = 0;
        var propertyChanges = 0;
        var dismissed = 0;
        bar.DismissRequested += (_, _) =>
        {
            requested++;
            bar.Dismiss();
        };
        bar.PropertyChanged += (_, eventArgs) => propertyChanges +=
            eventArgs.PropertyName == nameof(InfoBar.IsOpen) ? 1 : 0;
        bar.Dismissed += (_, _) => dismissed++;

        bar.Dismiss();
        bar.Dismiss();

        requested.ShouldBe(1);
        propertyChanges.ShouldBe(1);
        dismissed.ShouldBe(1);
        bar.IsOpen.ShouldBeFalse();
    }

    /// <summary>Verifies replacing content from a requested callback supersedes stale close publication.</summary>
    [Fact]
    public void Dismiss_WhenRequestedHandlerReplacesContent_SuppressesStaleTransition()
    {
        var previous = new ProbeControl();
        var current = new ProbeControl();
        var bar = new InfoBar { Content = previous };
        var laterRequested = 0;
        var dismissed = 0;
        bar.DismissRequested += (_, _) => bar.Content = current;
        bar.DismissRequested += (_, _) => laterRequested++;
        bar.Dismissed += (_, _) => dismissed++;

        bar.Dismiss();

        bar.IsOpen.ShouldBeTrue();
        bar.Content.ShouldBeSameAs(current);
        previous.Parent.ShouldBeNull();
        laterRequested.ShouldBe(0);
        dismissed.ShouldBe(0);
    }

    /// <summary>Verifies hiding from a requested callback supersedes stale close publication.</summary>
    [Fact]
    public void Dismiss_WhenRequestedHandlerHidesBar_SuppressesStaleTransition()
    {
        var bar = new InfoBar();
        var laterRequested = 0;
        var dismissed = 0;
        bar.DismissRequested += (_, _) => bar.Visibility = Visibility.Hidden;
        bar.DismissRequested += (_, _) => laterRequested++;
        bar.Dismissed += (_, _) => dismissed++;

        bar.Dismiss();

        bar.Visibility.ShouldBe(Visibility.Hidden);
        bar.IsOpen.ShouldBeTrue();
        laterRequested.ShouldBe(0);
        dismissed.ShouldBe(0);
    }

    /// <summary>Verifies disposal from a requested callback supersedes every stale dismissal stage.</summary>
    [Fact]
    public void Dismiss_WhenRequestedHandlerDisposesBar_SuppressesStaleTransition()
    {
        var bar = new InfoBar { Content = new ProbeControl() };
        var laterRequested = 0;
        var propertyChanges = 0;
        var dismissed = 0;
        bar.DismissRequested += (_, _) => bar.Dispose();
        bar.DismissRequested += (_, _) => laterRequested++;
        bar.PropertyChanged += (_, eventArgs) => propertyChanges +=
            eventArgs.PropertyName == nameof(InfoBar.IsOpen) ? 1 : 0;
        bar.Dismissed += (_, _) => dismissed++;

        bar.Dismiss();

        bar.IsDisposed.ShouldBeTrue();
        laterRequested.ShouldBe(0);
        propertyChanges.ShouldBe(0);
        dismissed.ShouldBe(0);
    }

    /// <summary>Verifies reopening from the committed-property callback suppresses stale completion.</summary>
    [Fact]
    public void Dismiss_WhenPropertyObserverReopens_SuppressesDismissed()
    {
        var bar = new InfoBar();
        var dismissed = 0;
        bar.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(InfoBar.IsOpen) && !bar.IsOpen)
            {
                bar.IsOpen = true;
            }
        };
        bar.Dismissed += (_, _) => dismissed++;

        bar.Dismiss();

        bar.IsOpen.ShouldBeTrue();
        dismissed.ShouldBe(0);
    }

    /// <summary>Verifies reopening from completion supersedes the remaining completion subscribers.</summary>
    [Fact]
    public void Dismiss_WhenDismissedSubscriberReopens_SuppressesLaterSubscribers()
    {
        var bar = new InfoBar();
        var laterDismissed = 0;
        bar.Dismissed += (_, _) => bar.IsOpen = true;
        bar.Dismissed += (_, _) => laterDismissed++;

        bar.Dismiss();

        bar.IsOpen.ShouldBeTrue();
        laterDismissed.ShouldBe(0);
    }

    /// <summary>Verifies all completion subscribers are attempted and the earliest failure is preserved.</summary>
    [Fact]
    public void Dismiss_WhenDismissedSubscribersThrow_AttemptsAllBeforeRethrow()
    {
        var bar = new InfoBar();
        var firstFailure = new InvalidOperationException("first dismissed");
        var callbacks = 0;
        bar.Dismissed += (_, _) =>
        {
            callbacks++;
            throw firstFailure;
        };
        bar.Dismissed += (_, _) =>
        {
            callbacks++;
            throw new InvalidOperationException("second dismissed");
        };

        var exception = Should.Throw<InvalidOperationException>(bar.Dismiss);

        exception.ShouldBeSameAs(firstFailure);
        callbacks.ShouldBe(2);
        bar.IsOpen.ShouldBeFalse();
    }

    /// <summary>Verifies directly disposing retained content retires the lease and clears the inherited slot.</summary>
    [Fact]
    public void Content_WhenDisposedWhileClosed_RetiresAvailabilityWithoutRestoration()
    {
        var body = new ProbeControl { Visibility = Visibility.Hidden };
        var bar = new InfoBar { Content = body, IsOpen = false };
        var visibilityChanges = 0;
        body.PropertyChanged += (_, eventArgs) => visibilityChanges +=
            eventArgs.PropertyName == nameof(ControlBase.Visibility) ? 1 : 0;

        body.Dispose();

        body.IsDisposed.ShouldBeTrue();
        bar.Content.ShouldBeNull();
        visibilityChanges.ShouldBe(0);
    }

    /// <summary>Verifies owner disposal tears down retained content without restoring closed live state.</summary>
    [Fact]
    public void Dispose_WhenBarIsClosed_DisposesCurrentContentWithoutRestoration()
    {
        var body = new ProbeControl { Visibility = Visibility.Hidden };
        var bar = new InfoBar { Content = body, IsOpen = false };
        var visibilityChanges = 0;
        body.PropertyChanged += (_, eventArgs) => visibilityChanges +=
            eventArgs.PropertyName == nameof(ControlBase.Visibility) ? 1 : 0;

        bar.Dispose();

        bar.IsDisposed.ShouldBeTrue();
        body.IsDisposed.ShouldBeTrue();
        visibilityChanges.ShouldBe(0);
    }

    /// <summary>Verifies title, adornment, dismiss affordance, padding, and body determine intrinsic size.</summary>
    [Fact]
    public void Measure_WhenHeaderAndContentExist_ReservesCompleteGeometry()
    {
        using var bar = new InfoBar
        {
            Title = "Build",
            Adornment = new Affix("!"),
            Content = new ProbeControl(new Size(10, 2)),
            Style = InfoBarStyle.Info with
            {
                Padding = new Thickness(1),
                ContentGap = 1,
                AdornmentGap = 1
            }
        };

        new LayoutEngine().Layout(bar, new Size(40, 10));

        bar.DesiredSize.ShouldBe(new Size(14, 8));
    }
}
