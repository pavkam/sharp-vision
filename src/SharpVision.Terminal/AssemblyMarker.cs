// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.


[assembly: InternalsVisibleTo("SharpVision.Terminal.Tests")]
[assembly: InternalsVisibleTo("SharpVision.Terminal.Probe")]
[assembly: InternalsVisibleTo("SharpVision.Tests")]

namespace SharpVision.Terminal;

/// <summary>
/// Identifies the terminal protocol assembly to its test suite.
/// </summary>
internal sealed class AssemblyMarker;
