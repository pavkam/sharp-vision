using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("SharpVision.Tests")]
[assembly: InternalsVisibleTo("SharpVision.Showcase")]
[assembly: InternalsVisibleTo("SharpVision.Showcase.Tests")]

namespace SharpVision;

/// <summary>
/// Identifies the user interface assembly to its test suite.
/// </summary>
internal sealed class AssemblyMarker;
