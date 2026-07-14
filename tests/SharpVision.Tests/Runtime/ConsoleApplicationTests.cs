// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;

using SharpVision.Runtime;

/// <summary>Verifies <see cref="ConsoleApplication"/> validation and the redirected-console fast path.</summary>
public sealed class ConsoleApplicationTests
{
    /// <summary>Verifies the builder factory rejects a null screen.</summary>
    [Fact]
    public void CreateBuilder_WhenScreenNull_Throws() =>
        Should.Throw<ArgumentNullException>(() => ConsoleApplication.CreateBuilder(screen: null!));

    /// <summary>Verifies a redirected console short-circuits without starting the application.</summary>
    [Fact]
    public async Task RunAsync_WhenConsoleRedirected_ReturnsRedirected()
    {
        // The test host runs with redirected standard streams, so ConsoleHost.IsInteractive is false.
        ConsoleRunStatus status = await ConsoleApplication.RunAsync(new ProbeScreen());

        status.ShouldBe(ConsoleRunStatus.Redirected);
    }
}
