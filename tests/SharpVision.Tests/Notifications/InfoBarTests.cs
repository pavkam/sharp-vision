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
}
