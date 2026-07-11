using Shouldly;

namespace SharpVision.Tests;

public sealed class AssemblyTests
{
    [Fact]
    public void Assembly_WhenLoaded_HasExpectedName()
    {
        var name = typeof(global::SharpVision.AssemblyMarker).Assembly.GetName().Name;

        name.ShouldBe("SharpVision");
    }
}
