// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.PackageConsumer;

using SharpVision.Consumer.Tests.PackageSpecimens;

/// <summary>Starts the isolated packed-package contract probe.</summary>
public static class Program
{
    /// <summary>Runs the externally compiled component proof.</summary>
    /// <returns>A task that completes after the public-API probe succeeds.</returns>
    public static Task Main() => PackageProbe.RunAsync();
}
