// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Runtime;

using SharpVision.Terminal.Runtime;


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
}
