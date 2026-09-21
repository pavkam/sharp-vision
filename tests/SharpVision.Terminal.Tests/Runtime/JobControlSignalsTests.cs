// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Runtime;

/// <summary>
/// Verifies <see cref="JobControlSignals.InvokeSuspend"/> and
/// <see cref="JobControlSignals.InvokeResume"/> - the pure static helpers a real SIGTSTP/SIGCONT
/// registration delegates to - directly, with injected delegates, rather than raising a real
/// process-stopping signal. Real signal delivery is left to manual verification: raising an actual
/// SIGSTOP against a test process is not something a unit test can safely do.
/// </summary>
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
public sealed class JobControlSignalsTests
{
    /// <summary>
    /// Verifies the suspend callback always runs, and finishes, before the stop boundary is
    /// invoked - the ordering the whole feature depends on: cooked mode and every leased terminal
    /// mode must already be restored by the time the process actually stops.
    /// </summary>
    [Fact]
    public void InvokeSuspend_WhenCalled_RunsOnSuspendBeforeRaiseStop()
    {
        // Arrange
        var order = new List<string>();

        // Act
        JobControlSignals.InvokeSuspend(
            onSuspend: () => order.Add("suspend"),
            raiseStop: () => order.Add("stop"));

        // Assert
        order.ShouldBe(["suspend", "stop"]);
    }

    /// <summary>
    /// Verifies a failing suspend callback still lets the stop boundary run - a failed restore is
    /// still followed by the actual stop, matching the best-effort contract every other lease-walk
    /// failure in this codebase already has.
    /// </summary>
    [Fact]
    public void InvokeSuspend_WhenOnSuspendThrows_StillRaisesStop()
    {
        // Arrange
        var stopRaised = false;

        // Act & Assert
        Should.NotThrow(() => JobControlSignals.InvokeSuspend(
            onSuspend: () => throw new InvalidOperationException("restore failed"),
            raiseStop: () => stopRaised = true));
        stopRaised.ShouldBeTrue();
    }

    /// <summary>
    /// Verifies a failing stop boundary never escapes - the "must never throw onto the
    /// signal-handling thread" contract every registered signal callback in this codebase upholds.
    /// </summary>
    [Fact]
    public void InvokeSuspend_WhenRaiseStopThrows_DoesNotThrow()
    {
        Should.NotThrow(() => JobControlSignals.InvokeSuspend(
            onSuspend: () => { },
            raiseStop: () => throw new InvalidOperationException("kill failed")));
    }

    /// <summary>Verifies the resume callback runs.</summary>
    [Fact]
    public void InvokeResume_WhenCalled_RunsOnResume()
    {
        // Arrange
        var resumed = false;

        // Act
        JobControlSignals.InvokeResume(onResume: () => resumed = true);

        // Assert
        resumed.ShouldBeTrue();
    }

    /// <summary>
    /// Verifies a failing resume callback never escapes onto the caller - the same "must never
    /// throw onto the signal-handling thread" contract <see cref="InvokeSuspend_WhenRaiseStopThrows_DoesNotThrow"/>
    /// verifies for the suspend side.
    /// </summary>
    [Fact]
    public void InvokeResume_WhenOnResumeThrows_DoesNotThrow()
    {
        Should.NotThrow(() => JobControlSignals.InvokeResume(
            onResume: () => throw new InvalidOperationException("resume failed")));
    }

    /// <summary>Verifies every required callback is validated eagerly.</summary>
    [Fact]
    public void Register_WhenAnyCallbackIsNull_ThrowsArgumentNullException()
    {
        _ = Should.Throw<ArgumentNullException>(() => JobControlSignals.Register(
            onSuspend: null!,
            onResume: () => { },
            raiseStop: () => { }));
        _ = Should.Throw<ArgumentNullException>(() => JobControlSignals.Register(
            onSuspend: () => { },
            onResume: null!,
            raiseStop: () => { }));
        _ = Should.Throw<ArgumentNullException>(() => JobControlSignals.Register(
            onSuspend: () => { },
            onResume: () => { },
            raiseStop: null!));
    }

    /// <summary>Verifies every required argument is validated eagerly on the injectable-factory
    /// overload, including the factory delegate itself.</summary>
    [Fact]
    public void Register_WhenAnyArgumentOfFactoryOverloadIsNull_ThrowsArgumentNullException()
    {
        _ = Should.Throw<ArgumentNullException>(() => JobControlSignals.Register(
            onSuspend: null!,
            onResume: () => { },
            raiseStop: () => { },
            createRegistration: static (_, _) => new TrackingRestore()));
        _ = Should.Throw<ArgumentNullException>(() => JobControlSignals.Register(
            onSuspend: () => { },
            onResume: null!,
            raiseStop: () => { },
            createRegistration: static (_, _) => new TrackingRestore()));
        _ = Should.Throw<ArgumentNullException>(() => JobControlSignals.Register(
            onSuspend: () => { },
            onResume: () => { },
            raiseStop: null!,
            createRegistration: static (_, _) => new TrackingRestore()));
        _ = Should.Throw<ArgumentNullException>(() => JobControlSignals.Register(
            onSuspend: () => { },
            onResume: () => { },
            raiseStop: () => { },
            createRegistration: null!));
    }

    /// <summary>Verifies the injectable-factory <c>Register</c> overload calls the factory once per
    /// signal, in construction order, and that disposing the returned scope disposes every one of
    /// them exactly once - the baseline the throwing-factory test below builds on.</summary>
    [Fact]
    public void Register_WhenCalled_CallsFactoryForSuspendAndResumeInOrderAndDisposesThemOnScopeDispose()
    {
        // Arrange
        var requestedSignals = new List<PosixSignal>();
        var registrations = new List<TrackingRestore>();

        IDisposable CreateRegistration(PosixSignal signal, Action<PosixSignalContext> handler)
        {
            requestedSignals.Add(signal);
            var registration = new TrackingRestore();
            registrations.Add(registration);

            return registration;
        }

        // Act
        var scope = JobControlSignals.Register(
            onSuspend: () => { }, onResume: () => { }, raiseStop: () => { }, CreateRegistration);

        // Assert - registered before disposal, and not yet disposed.
        requestedSignals.ShouldBe([PosixSignal.SIGTSTP, PosixSignal.SIGCONT]);
        registrations.ShouldAllBe(registration => registration.Disposals == 0);

        scope.Dispose();

        registrations.ShouldAllBe(registration => registration.Disposals == 1);
    }

    /// <summary>Verifies that when the second registration (<c>SIGCONT</c>) fails, the first
    /// (<c>SIGTSTP</c>) is disposed exactly once instead of leaking, and the original exception
    /// propagates unchanged rather than being replaced by a disposal failure.</summary>
    [Fact]
    public void Register_WhenSecondRegistrationThrows_DisposesEarlierRegistrationAndRethrowsOriginalException()
    {
        // Arrange
        var expected = new InvalidOperationException("native handler allocation failed");
        var registrations = new List<TrackingRestore>();
        var callCount = 0;

        IDisposable CreateRegistration(PosixSignal signal, Action<PosixSignalContext> handler)
        {
            callCount++;

            if (callCount == 2)
            {
                throw expected;
            }

            var registration = new TrackingRestore();
            registrations.Add(registration);

            return registration;
        }

        // Act
        var actual = Should.Throw<InvalidOperationException>(() => JobControlSignals.Register(
            onSuspend: () => { }, onResume: () => { }, raiseStop: () => { }, CreateRegistration));

        // Assert
        actual.ShouldBeSameAs(expected);
        registrations.Count.ShouldBe(1);
        registrations[0].Disposals.ShouldBe(1);
    }
}
