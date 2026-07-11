using Shouldly;

namespace SharpVision.Showcase.Tests;

/// <summary>
/// Verifies the temporary repository-foundation startup message.
/// </summary>
public sealed class StartupMessageTests
{
    /// <summary>
    /// Verifies that the shell reports its honest implementation phase and docs entrypoint.
    /// </summary>
    [Fact]
    public void Get_WhenFoundationIsRunning_DescribesCurrentPhase()
    {
        var message = StartupMessage.Get();

        message.ShouldContain("foundation", Case.Insensitive);
        message.ShouldContain("docs/index.md");
    }
}
