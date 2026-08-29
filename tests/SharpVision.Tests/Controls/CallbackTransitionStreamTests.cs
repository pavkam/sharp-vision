// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies shared synchronous callback transition identity and publication.</summary>
public sealed class CallbackTransitionStreamTests
{
    /// <summary>Verifies an away-and-back nested commit invalidates the outer token and stops its
    /// captured invocation list before the next observer.</summary>
    [Fact]
    public void Value_WhenFirstPropertyObserverCommitsAwayAndBack_StopsSupersededObservers()
    {
        var probe = new TransitionPropertyProbe();
        var observations = new List<string>();
        var reentered = false;
        probe.PropertyChanged += (_, _) =>
        {
            if (probe.Value == 1 && !reentered)
            {
                reentered = true;
                probe.Value = 2;
                probe.Value = 1;
            }
        };
        probe.PropertyChanged += (_, _) => observations.Add($"property:{probe.Value}");
        probe.ValueChanged += (_, _) => observations.Add($"typed:{probe.Value}");

        probe.Value = 1;

        observations.ShouldBe(["property:2", "typed:2", "property:1", "typed:1"]);
        probe.RequiredContinuations.ShouldBe(3);
    }

    /// <summary>Verifies disposal during property publication suppresses stale typed publication
    /// without skipping mandatory invariant work.</summary>
    [Fact]
    public void Value_WhenPropertyObserverDisposesOwner_CompletesRequiredWorkAndSkipsTypedEvent()
    {
        var probe = new TransitionPropertyProbe();
        var typed = 0;
        probe.PropertyChanged += (_, _) => probe.Dispose();
        probe.ValueChanged += (_, _) => typed++;

        probe.Value = 1;

        probe.IsDisposed.ShouldBeTrue();
        probe.RequiredContinuations.ShouldBe(1);
        typed.ShouldBe(0);
    }

    /// <summary>Verifies the earliest observer failure is rethrown only after the remaining current
    /// observers and mandatory work complete.</summary>
    [Fact]
    public void Value_WhenObserversThrow_PreservesEarliestFailureAfterRequiredWork()
    {
        var probe = new TransitionPropertyProbe();
        var expected = new InvalidOperationException("first");
        var laterPropertyObserver = false;
        var typedObserver = false;
        probe.PropertyChanged += (_, _) => throw expected;
        probe.PropertyChanged += (_, _) => laterPropertyObserver = true;
        probe.ValueChanged += (_, _) =>
        {
            typedObserver = true;
            throw new InvalidOperationException("later");
        };

        var exception = Should.Throw<InvalidOperationException>(() => probe.Value = 1);

        exception.ShouldBeSameAs(expected);
        laterPropertyObserver.ShouldBeTrue();
        typedObserver.ShouldBeTrue();
        probe.RequiredContinuations.ShouldBe(1);
    }

    /// <summary>Verifies wrapping the numeric generation replaces its epoch so an ancient token
    /// cannot become current again.</summary>
    [Fact]
    public void Commit_WhenGenerationWraps_PreservesUniqueCurrentIdentity()
    {
        var owner = new ProbeControl();
        var stream = new CallbackTransitionStream(ulong.MaxValue);
        var beforeWrap = stream.Capture(owner);

        var afterWrap = stream.Commit(owner);

        beforeWrap.IsCurrent.ShouldBeFalse();
        afterWrap.IsCurrent.ShouldBeTrue();
    }
}
