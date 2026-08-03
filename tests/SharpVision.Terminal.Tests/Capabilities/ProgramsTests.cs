// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Capabilities;
/// <summary>Verifies executable terminal-program contract classification.</summary>
public sealed class ProgramsTests
{
    /// <summary>Verifies IsFullScreenReady's shared constants name exactly the programs required
    /// to satisfy it -- registering a program under a different name (a stand-in for the
    /// consumption-site retyping #93 describes) must not satisfy readiness.</summary>
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

        programs.IsFullScreenReady.ShouldBeTrue();
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

        programs.IsFullScreenReady.ShouldBeFalse();
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
}
