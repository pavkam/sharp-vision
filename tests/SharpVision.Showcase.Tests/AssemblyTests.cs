using Shouldly;

namespace SharpVision.Showcase.Tests;

public sealed class AssemblyTests
{
    [Fact]
    public void Assembly_WhenLoaded_HasExpectedName()
    {
        var name = typeof(global::SharpVision.Showcase.AssemblyMarker).Assembly.GetName().Name;

        name.ShouldBe("SharpVision.Showcase");
    }
}
