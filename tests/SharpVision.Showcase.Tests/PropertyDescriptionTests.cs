using Shouldly;

namespace SharpVision.Showcase.Tests;

/// <summary>Verifies validated immutable showcase property documentation.</summary>
public sealed class PropertyDescriptionTests
{
    /// <summary>Verifies constructor values remain exact and readable.</summary>
    [Fact]
    public void Constructor_WhenValuesAreValid_PreservesDocumentation()
    {
        var description = new PropertyDescription(
            "Content",
            "Control?",
            "null",
            "Owns the visual content displayed by the control.");

        description.Name.ShouldBe("Content");
        description.Type.ShouldBe("Control?");
        description.Default.ShouldBe("null");
        description.Description.ShouldBe("Owns the visual content displayed by the control.");
    }

    /// <summary>Verifies every textual field rejects null, empty, and whitespace input.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Constructor_WhenOneValueIsBlank_ThrowsArgumentException(int field)
    {
        var values = new[] { "Content", "Control?", "null", "Description" };
        values[field] = " ";

        _ = Should.Throw<ArgumentException>(() => new PropertyDescription(
            values[0],
            values[1],
            values[2],
            values[3]));
    }
}
