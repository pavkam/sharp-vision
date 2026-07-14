// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("SharpVision.Terminal.Tests")]
[assembly: InternalsVisibleTo("SharpVision")]

namespace SharpVision.Terminal;

/// <summary>
/// Identifies the terminal protocol assembly to its test suite.
/// </summary>
internal sealed class AssemblyMarker;
