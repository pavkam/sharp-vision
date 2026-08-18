// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Runtime;


/// <summary>
/// Verifies <see cref="ConsoleHost.Open"/> argument validation.
/// </summary>
public sealed class ConsoleHostTests
{
    /// <summary>
    /// Verifies that a null options argument throws.
    /// </summary>
    [Fact]
    public void Open_WhenOptionsNull_Throws() =>
        Should.Throw<ArgumentNullException>(() => ConsoleHost.Open(options: null!));

    /// <summary>Verifies the static members delegate to the same Default seam instance, so a
    /// caller depending on IConsoleHost observes identical behavior to the static class.</summary>
    [Fact]
    public void Default_WhenQueried_BacksTheStaticMembers()
    {
        _ = ConsoleHost.Default.ShouldNotBeNull();
        ConsoleHost.Interactive.ShouldBe(ConsoleHost.Default.Interactive);
        _ = Should.Throw<ArgumentNullException>(() => ConsoleHost.Default.Open(options: null!));
    }

    /// <summary>Verifies a caller can substitute a fake IConsoleHost instead of hand-wrapping the
    /// static members in delegates.</summary>
    [Fact]
    public void IConsoleHost_WhenImplementedByAFake_IsSubstitutableForTheRealHost()
    {
        IConsoleHost fake = new FakeConsoleHost();

        fake.Interactive.ShouldBeTrue();
        _ = Should.Throw<ArgumentNullException>(() => fake.Open(options: null!));
    }

    private sealed class FakeConsoleHost: IConsoleHost
    {
        public bool Interactive => true;

        public ConsoleConnection Open(ConsoleHostOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            throw new NotSupportedException("The fake host does not open a real connection.");
        }
    }
}
