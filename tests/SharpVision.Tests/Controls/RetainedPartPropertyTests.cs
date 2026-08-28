// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies current-aware retained-part property forwarding transactions.</summary>
public sealed class RetainedPartPropertyTests
{
    /// <summary>Verifies a source failure before commit publishes no owner change.</summary>
    [Fact]
    public void Value_WhenSourceThrowsBeforeCommit_PreservesOwnerStateAndNotificationSilence()
    {
        var source = new ProbeControl();
        var owner = new ProbeCompositeControl(source);
        var value = 1;
        using var property = owner.RegisterRetainedPartProperty(
            source,
            "SourceValue",
            "OwnerValue",
            () => value,
            _ => throw new InvalidOperationException("before"));
        var notifications = 0;
        owner.PropertyChanged += (_, eventArgs) =>
            notifications += eventArgs.PropertyName == "OwnerValue" ? 1 : 0;

        _ = Should.Throw<InvalidOperationException>(() => property.Value = 2);

        property.Value.ShouldBe(1);
        notifications.ShouldBe(0);
    }

    /// <summary>Verifies a source failure after commit still publishes the committed owner value
    /// exactly once before rethrowing the original failure.</summary>
    [Fact]
    public void Value_WhenSourceThrowsAfterCommit_PublishesCommittedOwnerStateOnce()
    {
        var source = new ProbeControl();
        var owner = new ProbeCompositeControl(source);
        var value = 1;
        using var property = owner.RegisterRetainedPartProperty(
            source,
            "SourceValue",
            "OwnerValue",
            () => value,
            next =>
            {
                value = next;
                throw new InvalidOperationException("after");
            });
        List<int> observed = [];
        owner.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == "OwnerValue")
            {
                observed.Add(property.Value);
            }
        };

        var exception = Should.Throw<InvalidOperationException>(() => property.Value = 2);

        exception.Message.ShouldBe("after");
        property.Value.ShouldBe(2);
        observed.ShouldBe([2]);
    }

    /// <summary>Verifies registration rejects a foreign source before invoking its getter or
    /// installing any subscription.</summary>
    [Fact]
    public void RegisterRetainedPartProperty_WhenSourceIsForeign_RejectsBeforeReadingSource()
    {
        var owner = new ProbeCompositeControl(new ProbeControl());
        var foreign = new ProbeControl();
        var getterCalls = 0;

        _ = Should.Throw<InvalidOperationException>(() => owner.RegisterRetainedPartProperty(
            foreign,
            "SourceValue",
            "OwnerValue",
            () =>
            {
                getterCalls++;
                return 1;
            }));

        getterCalls.ShouldBe(0);
    }

    /// <summary>Verifies the generic registration releases its source subscription as soon as the
    /// retained source leaves the owner, independently of any specialized forwarding wrapper.</summary>
    [Fact]
    public void RegisterRetainedPartProperty_WhenSourceDetaches_StopsForwardingChanges()
    {
        var source = new ProbeControl();
        var owner = new ProbeCompositeControl(source);
        var value = 1;
        _ = owner.RegisterRetainedPartProperty(
            source,
            nameof(ProbeControl.Tag),
            "OwnerValue",
            () => value);
        var notifications = 0;
        owner.PropertyChanged += (_, eventArgs) =>
            notifications += eventArgs.PropertyName == "OwnerValue" ? 1 : 0;

        value = 2;
        source.Tag = "first";
        _ = source.OwningSlot.ShouldNotBeNull().Remove(source);
        value = 3;
        source.Tag = "second";

        notifications.ShouldBe(1);
    }
}
