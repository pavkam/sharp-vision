using SharpVision.Terminal.Protocols;

using Shouldly;

namespace SharpVision.Terminal.Tests.Protocols;

/// <summary>
/// Verifies allocation-free CSI parameter enumeration.
/// </summary>
public sealed class ParametersTests
{
    /// <summary>
    /// Verifies that empty raw parameters contain no fields.
    /// </summary>
    [Fact]
    public void Read_WhenInputIsEmpty_ReturnsEnd()
    {
        var parameters = new Parameters([]);

        var status = parameters.Read(out var value, out var separator);

        status.ShouldBe(ParameterStatus.End);
        value.ShouldBe(0);
        separator.ShouldBe(ParameterSeparator.None);
    }

    /// <summary>
    /// Verifies empty fields and their semicolon boundaries.
    /// </summary>
    [Fact]
    public void Read_WhenFieldsAreDefault_ReturnsDefaultValues()
    {
        var parameters = new Parameters(";"u8);

        parameters.Read(out var first, out var firstSeparator).ShouldBe(ParameterStatus.Default);
        parameters.Read(out var second, out var secondSeparator).ShouldBe(ParameterStatus.Default);
        parameters.Read(out _, out _).ShouldBe(ParameterStatus.End);

        first.ShouldBe(0);
        firstSeparator.ShouldBe(ParameterSeparator.Semicolon);
        second.ShouldBe(0);
        secondSeparator.ShouldBe(ParameterSeparator.None);
    }

    /// <summary>
    /// Verifies numeric values, colon subparameters, and semicolon parameters.
    /// </summary>
    [Fact]
    public void Read_WhenInputHasSubparameters_PreservesSeparators()
    {
        var parameters = new Parameters("38:2:1:2:3;4"u8);

        AssertField(ref parameters, 38, ParameterSeparator.Colon);
        AssertField(ref parameters, 2, ParameterSeparator.Colon);
        AssertField(ref parameters, 1, ParameterSeparator.Colon);
        AssertField(ref parameters, 2, ParameterSeparator.Colon);
        AssertField(ref parameters, 3, ParameterSeparator.Semicolon);
        AssertField(ref parameters, 4, ParameterSeparator.None);
        parameters.Read(out _, out _).ShouldBe(ParameterStatus.End);
    }

    /// <summary>
    /// Verifies that an initial private marker remains distinct from numbers.
    /// </summary>
    [Fact]
    public void PrivateMarker_WhenInputStartsWithMarker_IsExposed()
    {
        var parameters = new Parameters("?25"u8);

        parameters.PrivateMarker.ShouldBe((byte) '?');
        AssertField(ref parameters, 25, ParameterSeparator.None);
    }

    /// <summary>
    /// Verifies malformed numeric grammar is reported without throwing.
    /// </summary>
    [Theory]
    [InlineData("1A")]
    [InlineData("1?")]
    [InlineData(":?")]
    public void Read_WhenInputIsMalformed_EventuallyReturnsInvalid(string input)
    {
        var parameters = new Parameters(System.Text.Encoding.ASCII.GetBytes(input));
        ParameterStatus status;

        do
        {
            status = parameters.Read(out _, out _);
        }
        while (status is ParameterStatus.Default or ParameterStatus.Value);

        status.ShouldBe(ParameterStatus.Invalid);
    }

    /// <summary>
    /// Verifies checked arithmetic and the configured numeric bound.
    /// </summary>
    [Theory]
    [InlineData("2147483648", int.MaxValue)]
    [InlineData("1001", 1000)]
    [InlineData("2", 1)]
    public void Read_WhenValueExceedsMaximum_ReturnsOverflow(string input, int maximum)
    {
        var parameters = new Parameters(
            System.Text.Encoding.ASCII.GetBytes(input),
            maxCount: 8,
            maxValue: maximum);

        parameters.Read(out _, out _).ShouldBe(ParameterStatus.Overflow);
    }

    /// <summary>
    /// Verifies the configured number of fields cannot be exceeded.
    /// </summary>
    [Fact]
    public void Read_WhenFieldCountExceedsMaximum_ReturnsLimit()
    {
        var parameters = new Parameters("1;2;3"u8, maxCount: 2, maxValue: 100);

        parameters.Read(out _, out _).ShouldBe(ParameterStatus.Value);
        parameters.Read(out _, out _).ShouldBe(ParameterStatus.Value);
        parameters.Read(out _, out _).ShouldBe(ParameterStatus.Limit);
    }

    /// <summary>
    /// Verifies public limit validation.
    /// </summary>
    [Fact]
    public void Constructor_WhenLimitIsNotPositive_ThrowsArgumentOutOfRangeException()
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(
            static () => _ = new Parameters([], maxCount: 0, maxValue: 1));
        _ = Should.Throw<ArgumentOutOfRangeException>(
            static () => _ = new Parameters([], maxCount: 1, maxValue: 0));
    }

    private static void AssertField(
        ref Parameters parameters,
        int expectedValue,
        ParameterSeparator expectedSeparator)
    {
        parameters.Read(out var value, out var separator).ShouldBe(ParameterStatus.Value);
        value.ShouldBe(expectedValue);
        separator.ShouldBe(expectedSeparator);
    }
}
