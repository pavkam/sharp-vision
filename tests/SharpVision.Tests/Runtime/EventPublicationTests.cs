// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;

/// <summary>Verifies shared multicast publication with per-handler exception isolation for
/// non-control collaborators that have no <see cref="ControlBase"/> to inherit
/// <see cref="CallbackTransitionTransaction"/> from.</summary>
public sealed class EventPublicationTests
{
    /// <summary>Verifies a subscriber that supersedes the transition being published stops delivery
    /// to every subscriber added after it, without raising an error.</summary>
    [Fact]
    public void Publish_WhenSubscriberSupersedesTransition_SkipsLaterSubscribers()
    {
        // Arrange
        var version = 0;
        const int capturedVersion = 0;
        var invoked = new List<int>();
        Action first = () =>
        {
            invoked.Add(1);
            version = 1;
        };
        Action second = () => invoked.Add(2);
        Action third = () => invoked.Add(3);
        var handlers = Delegate.Combine(first, second, third);

        // Act
        EventPublication.Publish<Action>(
            handlers,
            () => version == capturedVersion,
            action => action());

        // Assert
        invoked.ShouldBe([1]);
    }

    /// <summary>Verifies every still-current subscriber runs even after an earlier one throws, and
    /// that only the earliest failure is rethrown once delivery completes.</summary>
    [Fact]
    public void Publish_WhenTwoSubscribersThrow_InvokesAllAndRethrowsFirst()
    {
        // Arrange
        var order = new List<string>();
        var firstFailure = new InvalidOperationException("first");
        var secondFailure = new InvalidOperationException("second");
        Action first = () =>
        {
            order.Add("first");
            throw firstFailure;
        };
        Action second = () =>
        {
            order.Add("second");
            throw secondFailure;
        };
        var handlers = Delegate.Combine(first, second);

        // Act
        var exception = Should.Throw<InvalidOperationException>(() =>
            EventPublication.Publish<Action>(handlers, () => true, action => action()));

        // Assert
        exception.ShouldBeSameAs(firstFailure);
        order.ShouldBe(["first", "second"]);
    }
}
