// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies the public header ownership role shared by GroupBox and Expander.</summary>
public sealed class HeaderedContentControlTests
{
    /// <summary>Verifies Header defaults to null and is independent of Content.</summary>
    [Fact]
    public void Header_WhenConstructed_DefaultsToNullAndDoesNotAffectContent()
    {
        var owner = new ProbeHeaderedContentControl();

        owner.Header.ShouldBeNull();
        owner.HeaderText.ShouldBeEmpty();
        owner.Content.ShouldBeNull();
    }

    /// <summary>Verifies assignment, replacement, equivalence, and clear publish exactly one committed change,
    /// independently of the inherited Content slot.</summary>
    [Fact]
    public void Header_WhenAssignedReplacedAndCleared_PublishesExactlyOncePerChangeAndLeavesContentAlone()
    {
        var owner = new ProbeHeaderedContentControl();
        var content = new ProbeControl();
        var first = new ProbeControl();
        var second = new ProbeControl();
        var notifications = new List<string?>();
        owner.Content = content;
        owner.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName);

        owner.Header = first;
        owner.Header = first;
        owner.Header = second;
        owner.Header = null;
        owner.Header = null;

        notifications.ShouldBe([
            nameof(HeaderedContentControl.Header),
            nameof(HeaderedContentControl.Header),
            nameof(HeaderedContentControl.Header)
        ]);
        owner.HeaderChanges.Count.ShouldBe(3);
        owner.HeaderChanges[0].ShouldBe((null, first));
        owner.HeaderChanges[1].ShouldBe((first, second));
        owner.HeaderChanges[2].ShouldBe((second, null));
        first.Parent.ShouldBeNull();
        second.Parent.ShouldBeNull();
        owner.Content.ShouldBeSameAs(content);
    }

    /// <summary>Verifies every invalid candidate preserves the complete existing header edge.</summary>
    [Fact]
    public void Header_WhenReplacementIsInvalid_PreservesExistingHeader()
    {
        var owner = new ProbeHeaderedContentControl();
        var existing = new ProbeControl();
        var other = new ProbeHeaderedContentControl();
        var crossOwned = new ProbeControl();
        var disposed = new ProbeControl();
        owner.Header = existing;
        other.Header = crossOwned;
        disposed.Dispose();

        _ = Should.Throw<ArgumentException>(() => owner.Header = owner);
        _ = Should.Throw<ArgumentException>(() => owner.Header = crossOwned);
        _ = Should.Throw<ObjectDisposedException>(() => owner.Header = disposed);

        owner.Header.ShouldBeSameAs(existing);
        existing.Parent.ShouldBeSameAs(owner);
        crossOwned.Parent.ShouldBeSameAs(other);
    }

    /// <summary>Verifies a Content control cannot also be assigned as Header, and vice versa - the two slots
    /// each own a distinct control, never the same instance simultaneously.</summary>
    [Fact]
    public void Header_WhenAssignedTheControlAlreadyOwnedAsContent_Throws()
    {
        var owner = new ProbeHeaderedContentControl();
        var shared = new ProbeControl();
        owner.Content = shared;

        _ = Should.Throw<ArgumentException>(() => owner.Header = shared);

        owner.Header.ShouldBeNull();
        owner.Content.ShouldBeSameAs(shared);
    }

    /// <summary>Verifies HeaderText materializes a Text child on first assignment and mutates it in place
    /// thereafter, notifying exactly once per committed change and staying silent on repeats.</summary>
    [Fact]
    public void HeaderText_WhenAssigned_MaterializesOnceAndMutatesInPlace()
    {
        var owner = new ProbeHeaderedContentControl();
        var notifications = new List<string?>();
        owner.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName);

        owner.HeaderText = "First";
        var materialized = owner.Header.ShouldBeOfType<ControlText>();
        owner.HeaderText = "First";
        owner.HeaderText = "Second";

        owner.HeaderText.ShouldBe("Second");
        owner.Header.ShouldBeSameAs(materialized);
        notifications.ShouldBe([
            nameof(HeaderedContentControl.Header),
            nameof(HeaderedContentControl.HeaderText),
            nameof(HeaderedContentControl.HeaderText)
        ]);
    }

    /// <summary>Verifies assigning HeaderText null throws before mutating.</summary>
    [Fact]
    public void HeaderText_WhenAssignedNull_ThrowsBeforeMutation()
    {
        var owner = new ProbeHeaderedContentControl();

        _ = Should.Throw<ArgumentNullException>(() => owner.HeaderText = null!);

        owner.Header.ShouldBeNull();
    }

    /// <summary>Verifies HeaderText replaces a non-Text header with a materialized Text child instead of
    /// mutating the unrelated control, and reading it back through a non-Text header reports empty.</summary>
    [Fact]
    public void HeaderText_WhenHeaderIsARichControl_ReplacesItInsteadOfMutatingIt()
    {
        var owner = new ProbeHeaderedContentControl();
        var rich = new ProbeControl();
        owner.Header = rich;

        owner.HeaderText.ShouldBeEmpty();

        owner.HeaderText = "Replaced";

        owner.Header.ShouldBeOfType<ControlText>().Content.ShouldBe("Replaced");
        rich.Parent.ShouldBeNull();
    }

    /// <summary>Verifies the access key resolves through a Text header and matches nothing for a rich or
    /// absent header, since <see cref="HeaderedContentControl.AccessKeyText"/> only projects
    /// <see cref="IAccessKeyCaption"/> headers.</summary>
    [Fact]
    public async Task MatchesAccessKey_WhenHeaderVaries_MatchesOnlyThroughATextCaptionAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var owner = new ProbeHeaderedContentControl { UseMnemonic = true };
        var key = new Rune('S');

        await dispatcher.InvokeAsync(() =>
        {
            owner.Attach(dispatcher);

            owner.MatchesAccessKey(key).ShouldBeFalse();

            owner.HeaderText = "&Save";
            owner.MatchesAccessKey(key).ShouldBeTrue();

            owner.Header = new ProbeControl();
            owner.MatchesAccessKey(key).ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a consumer-derived control built on this role composes Header alongside its own
    /// state, matching the documented "reusable header ownership role" contract.</summary>
    [Fact]
    public void ConsumerDerivedControl_WhenComposingHeaderAndOwnState_BehavesLikeAnyOtherHeaderedControl()
    {
        var badge = new ProbeHeaderedContentControl { HeaderText = "Status" };

        badge.HeaderText.ShouldBe("Status");
        _ = badge.Header.ShouldBeOfType<ControlText>();
        typeof(HeaderedContentControl).IsAssignableFrom(badge.GetType()).ShouldBeTrue();
    }
}
