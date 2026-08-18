// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Capabilities;
/// <summary>Verifies executable terminal-program contract classification.</summary>
public sealed class ProgramsTests
{
    /// <summary>Verifies FullScreenReady's shared constants name exactly the programs required
    /// to satisfy it -- registering a program under a different name (a stand-in for the
    /// consumption-site retyping this guards against) must not satisfy readiness.</summary>
    [Fact]
    public void IsFullScreenReady_WhenRequiredProgramsUseSharedConstants_IsSatisfiedByExactNames()
    {
        var programs = new Programs(new Dictionary<string, DescriptionProgram>
        {
            [CapabilityNames.Cup] = new DescriptionProgram("[%i%p1%d;%p2%dH"u8),
            [CapabilityNames.Sgr0] = new DescriptionProgram("[0m"u8),
            [CapabilityNames.El] = new DescriptionProgram("[K"u8),
            [CapabilityNames.Ed] = new DescriptionProgram("[J"u8)
        });

        programs.FullScreenReady.ShouldBeTrue();
        programs.Has(CapabilityNames.Cup).ShouldBeTrue();
        programs.Has("cup").ShouldBeTrue();
    }

    /// <summary>Verifies required programs need exact arity and representative output.</summary>
    /// <param name="name">The required program under test.</param>
    /// <param name="source">The compiled program source.</param>
    [Theory]
    [InlineData("cup", "\u001b[%p1%dH")]
    [InlineData("sgr0", "%{1}%PA")]
    [InlineData("clear", "%{1}%PA")]
    public void IsFullScreenReady_WhenRequiredContractCannotExecute_IsFalse(string name, string source)
    {
        var values = CorePrograms();
        values[name] = new DescriptionProgram(Encoding.ASCII.GetBytes(source));
        var programs = new Programs(values);

        programs.FullScreenReady.ShouldBeFalse();
    }

    /// <summary>Verifies optional renderer programs need exact arity and representative output.</summary>
    /// <param name="name">The optional program under test.</param>
    /// <param name="source">The compiled program source.</param>
    [Theory]
    [InlineData("el", "%{1}%PA")]
    [InlineData("bold", "%{1}%PA")]
    [InlineData("setaf", "\u001b[31m")]
    [InlineData("setdf", "%p1%d")]
    [InlineData("op", "%p1%d")]
    [InlineData("Ss", "\u001b[2 q")]
    [InlineData("Se", "%{1}%PA")]
    [InlineData("Setulc", "%{1}%PA")]
    public void Has_WhenRendererContractCannotExecute_IsFalse(string name, string source)
    {
        var programs = new Programs(new Dictionary<string, DescriptionProgram>
        {
            [name] = new DescriptionProgram(Encoding.ASCII.GetBytes(source))
        });

        programs.Has(name).ShouldBeFalse();
    }

    /// <summary>Verifies actual numeric failure publishes nothing and rolls back staged static variables.</summary>
    /// <param name="source">The program that succeeds for the representative value but fails for index two.</param>
    [Theory]
    [InlineData("%?%p1%{1}%=%tGOOD%e%p1%PA%;")]
    [InlineData("%?%p1%{1}%=%tGOOD%ePARTIAL%p1%PA%{1}%{0}%/%d%;")]
    public void TryWrite_WhenActualParametersDoNotProduceOutput_ReturnsFalseAndRollsBack(string source)
    {
        var programs = new Programs(new Dictionary<string, DescriptionProgram>
        {
            ["setaf"] = new DescriptionProgram(Encoding.ASCII.GetBytes(source)),
            ["read-static"] = new DescriptionProgram("%gA%d"u8)
        });
        var interpreter = new Interpreter(ProgramLimits.Default);
        var destination = new ArrayBufferWriter<byte>();

        programs.Has("setaf").ShouldBeTrue();
        programs.Has("read-static").ShouldBeTrue();
        var written = Should.NotThrow(() => programs.TryWrite("setaf", [2], interpreter, destination));
        var retained = programs.TryWrite("read-static", [], interpreter, destination);

        written.ShouldBeFalse();
        retained.ShouldBeTrue();
        destination.WrittenSpan.ToArray().ShouldBe("0"u8.ToArray());
    }

    /// <summary>Verifies "ed" is a fully first-class intrinsic capability like its sibling "el":
    /// both are markers a <see cref="TerminalProfile"/> can register as
    /// <see cref="DescriptionProgram.Intrinsic"/> without a compiled terminfo program, and both
    /// must round-trip through Has/TryWrite identically instead of "ed" silently failing to
    /// classify or emit (see FullScreenReady's documented el+ed fallback).</summary>
    [Theory]
    [InlineData("el")]
    [InlineData("ed")]
    public void Has_WhenProgramIsIntrinsic_ReportsSupportedAndWrites(string name)
    {
        var programs = new Programs(new Dictionary<string, DescriptionProgram>
        {
            [name] = DescriptionProgram.Intrinsic
        });
        var interpreter = new Interpreter(ProgramLimits.Default);
        var destination = new ArrayBufferWriter<byte>();

        programs.Has(name).ShouldBeTrue();
        var written = programs.TryWrite(name, [], interpreter, destination);

        written.ShouldBeTrue();
        destination.WrittenCount.ShouldBeGreaterThan(0);
    }

    /// <summary>Verifies a terminal profile whose "clear" capability cannot execute still reaches
    /// full-screen readiness through the intrinsic el+ed fallback pair.</summary>
    [Fact]
    public void IsFullScreenReady_WhenClearIsMissingButElAndEdAreIntrinsic_IsTrue()
    {
        var programs = new Programs(new Dictionary<string, DescriptionProgram>
        {
            [CapabilityNames.Cup] = new DescriptionProgram("[%i%p1%d;%p2%dH"u8),
            [CapabilityNames.Sgr0] = new DescriptionProgram("[0m"u8),
            [CapabilityNames.El] = DescriptionProgram.Intrinsic,
            [CapabilityNames.Ed] = DescriptionProgram.Intrinsic
        });

        programs.FullScreenReady.ShouldBeTrue();
    }

    /// <summary>Verifies the shared constants match their documented terminfo capability names.</summary>
    [Fact]
    public void CapabilityNames_WhenRead_MatchDocumentedNames()
    {
        CapabilityNames.Cup.ShouldBe("cup");
        CapabilityNames.Sgr0.ShouldBe("sgr0");
        CapabilityNames.El.ShouldBe("el");
        CapabilityNames.Ed.ShouldBe("ed");
        CapabilityNames.Clear.ShouldBe("clear");
        CapabilityNames.Civis.ShouldBe("civis");
        CapabilityNames.Cnorm.ShouldBe("cnorm");
        CapabilityNames.Smcup.ShouldBe("smcup");
        CapabilityNames.Rmcup.ShouldBe("rmcup");
        CapabilityNames.Smkx.ShouldBe("smkx");
        CapabilityNames.Rmkx.ShouldBe("rmkx");
    }

    private static Dictionary<string, DescriptionProgram> CorePrograms() => new()
    {
        ["cup"] = new DescriptionProgram("\u001b[%i%p1%d;%p2%dH"u8),
        ["sgr0"] = new DescriptionProgram("\u001b[0m"u8),
        ["clear"] = new DescriptionProgram("\u001b[2J"u8)
    };

    #region Lifecycle-pair expansion (Lease)

    /// <summary>Verifies exact lease byte ownership and empty-command validation.</summary>
    [Fact]
    public void Constructor_WhenLeaseCommandsAreInvalid_ThrowsOrOwnsBytes()
    {
        // Arrange
        var enable = "on"u8.ToArray();
        var disable = "off"u8.ToArray();

        // Act
        var lease = new Lease(enable, disable);
        enable[0] = (byte) 'x';
        disable[0] = (byte) 'x';

        // Assert
        Encoding.ASCII.GetString(lease.Enable.Span).ShouldBe("on");
        Encoding.ASCII.GetString(lease.Disable.Span).ShouldBe("off");
        _ = Should.Throw<ArgumentException>(() => new Lease([], "off"u8));
        _ = Should.Throw<ArgumentException>(() => new Lease("on"u8, []));
    }

    /// <summary>Verifies lifecycle-pair expansion validates names and its session interpreter.</summary>
    [Fact]
    public void TryExpandPair_WhenArgumentIsInvalid_Throws()
    {
        // Arrange
        var programs = new Programs(KeypadPrograms());
        var interpreter = new Interpreter(ProgramLimits.Default);

        // Act / Assert
        _ = Should.Throw<ArgumentException>(() =>
            programs.TryExpandPair("", "rmkx", interpreter, out _, out _));
        _ = Should.Throw<ArgumentException>(() =>
            programs.TryExpandPair("smkx", " ", interpreter, out _, out _));
        _ = Should.Throw<ArgumentNullException>(() =>
            programs.TryExpandPair("smkx", "rmkx", null!, out _, out _));
    }

    /// <summary>Verifies every unavailable pair result clears both returned byte snapshots.</summary>
    [Fact]
    public void TryExpandPair_WhenPairIsUnavailable_ReturnsEmptyOutputs()
    {
        // Arrange
        var programs = new Programs(new Dictionary<string, DescriptionProgram>
        {
            ["smkx"] = new DescriptionProgram("one-sided"u8)
        });
        var interpreter = new Interpreter(ProgramLimits.Default);

        // Act
        var expanded = programs.TryExpandPair(
            "smkx",
            "rmkx",
            interpreter,
            out var enable,
            out var disable);

        // Assert
        expanded.ShouldBeFalse();
        enable.IsEmpty.ShouldBeTrue();
        disable.IsEmpty.ShouldBeTrue();
    }

    /// <summary>Verifies successful pair expansion returns exact independently owned memories.</summary>
    [Fact]
    public void TryExpandPair_WhenProgramsAreValid_ReturnsIndependentExactOutputs()
    {
        // Arrange
        var programs = new Programs(KeypadPrograms());
        var interpreter = new Interpreter(ProgramLimits.Default);

        // Act
        var expanded = programs.TryExpandPair(
            "smkx",
            "rmkx",
            interpreter,
            out var enable,
            out var disable);

        // Assert
        expanded.ShouldBeTrue();
        Encoding.ASCII.GetString(enable.Span).ShouldBe("keys-in");
        Encoding.ASCII.GetString(disable.Span).ShouldBe("keys-out");
        MemoryMarshal.TryGetArray(enable, out var enableSegment).ShouldBeTrue();
        MemoryMarshal.TryGetArray(disable, out var disableSegment).ShouldBeTrue();
        enableSegment.Array.ShouldNotBeSameAs(disableSegment.Array);
    }

    /// <summary>Verifies mixed compiled/intrinsic pairs reject without changing interpreter statics.</summary>
    [Fact]
    public void TryExpandPair_WhenProgramKindsAreMixed_ReturnsEmptyAndPreservesStaticState()
    {
        // Arrange
        var programs = new Programs(new Dictionary<string, DescriptionProgram>
        {
            ["smcup"] = new DescriptionProgram("%{42}%PAenter"u8),
            ["rmcup"] = DescriptionProgram.Intrinsic
        });
        var interpreter = new Interpreter(ProgramLimits.Default);
        var readStatic = new ArrayBufferWriter<byte>();

        // Act
        var expanded = programs.TryExpandPair(
            "smcup",
            "rmcup",
            interpreter,
            out var enable,
            out var disable);
        interpreter.Write(new DescriptionProgram("%gA%d"u8), [], readStatic);

        // Assert
        expanded.ShouldBeFalse();
        enable.IsEmpty.ShouldBeTrue();
        disable.IsEmpty.ShouldBeTrue();
        Encoding.ASCII.GetString(readStatic.WrittenSpan).ShouldBe("0");
    }

    private static Dictionary<string, DescriptionProgram> KeypadPrograms() => new(StringComparer.Ordinal)
    {
        ["smkx"] = new DescriptionProgram("keys-in"u8),
        ["rmkx"] = new DescriptionProgram("keys-out"u8)
    };

    #endregion
}
