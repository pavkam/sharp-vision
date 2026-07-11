using Shouldly;

namespace SharpVision.Terminal.Tests;

public sealed class AssemblyTests
{
    [Fact]
    public void Assembly_WhenLoaded_HasExpectedName()
    {
        var name = typeof(global::SharpVision.Terminal.AssemblyMarker).Assembly.GetName().Name;

        name.ShouldBe("SharpVision.Terminal");
    }
}
