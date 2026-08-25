// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

[assembly: InternalsVisibleTo("SharpVision.Tests")]
[assembly: InternalsVisibleTo("SharpVision.Test.Shared")]
[assembly: InternalsVisibleTo("SharpVision.Document")]
[assembly: InternalsVisibleTo("SharpVision.Document.Tests")]
[assembly: InternalsVisibleTo("SharpVision.FigletFonts.Tests")]
[assembly: InternalsVisibleTo("SharpVision.SyntaxHighlighting.Tests")]

namespace SharpVision;

/// <summary>
/// Identifies the user interface assembly to its test suite.
/// </summary>
internal sealed class AssemblyMarker;
