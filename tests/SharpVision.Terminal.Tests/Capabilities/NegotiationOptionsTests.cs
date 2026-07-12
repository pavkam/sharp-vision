using SharpVision.Terminal.Capabilities;

using Shouldly;

namespace SharpVision.Terminal.Tests.Capabilities;

/// <summary>Verifies negotiation policy ownership and validation.</summary>
public sealed class NegotiationOptionsTests
{
    /// <summary>Verifies later caller mutation cannot change negotiation evidence.</summary>
    [Fact]
    public void Constructor_WhenEnvironmentChanges_RetainsOwnedSnapshot()
    {
        // Arrange
        var environment = new Dictionary<string, string?>
        {
            ["TERM"] = "xterm-kitty",
        };
        var options = new NegotiationOptions(environment);

        // Act
        environment["TERM"] = "dumb";

        // Assert
        options.Environment["TERM"].ShouldBe("xterm-kitty");
    }

    /// <summary>Verifies a missing environment is rejected before construction.</summary>
    [Fact]
    public void Constructor_WhenEnvironmentIsNull_Throws()
    {
        _ = Should.Throw<ArgumentNullException>(
            () => new NegotiationOptions(null!));
    }
}
