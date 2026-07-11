using Shouldly;

namespace SharpVision.Terminal.Tests;

/// <summary>
/// Verifies the terminal project test harness and assembly reference.
/// </summary>
public sealed class AssemblyTests
{
    /// <summary>
    /// Verifies that tests load the intended production assembly.
    /// </summary>
    [Fact]
    public void Assembly_WhenLoaded_HasExpectedName()
    {
        var name = typeof(AssemblyMarker).Assembly.GetName().Name;

        name.ShouldBe("SharpVision.Terminal");
    }
}
