// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Capabilities;
/// <summary>Verifies bounded compilation of ncurses terminfo parameter programs.</summary>
public sealed class CompilerTests
{
    #region Representative programs

    /// <summary>Verifies exact current ncurses terminal templates compile into owned operations.</summary>
    /// <remarks>
    /// Values are traced to official terminfo.src revision 1.1260, dated
    /// 2026-07-12 and accessed 2026-07-19. Local ncurses 6.6 infocmp output
    /// independently corroborates the xterm, screen, and tmux values.
    /// </remarks>
    [Theory]
    [InlineData("xterm cup", "\u001b[%i%p1%d;%p2%dH")]
    [InlineData("xterm-256color setaf", "\u001b[%?%p1%{8}%<%t3%p1%d%e%p1%{16}%<%t9%p1%{8}%-%d%e38;5;%p1%d%;m")]
    [InlineData("screen-256color setaf", "\u001b[%?%p1%{8}%<%t3%p1%d%e%p1%{16}%<%t9%p1%{8}%-%d%e38;5;%p1%d%;m")]
    [InlineData("tmux cup", "\u001b[%i%p1%d;%p2%dH")]
    [InlineData("xterm-direct setaf", "\u001b[%?%p1%{8}%<%t3%p1%d%e38:2::%p1%{65536}%/%d:%p1%{256}%/%{255}%&%d:%p1%{255}%&%d%;m")]
    [InlineData("kitty setaf via xterm+256color", "\u001b[%?%p1%{8}%<%t3%p1%d%e%p1%{16}%<%t9%p1%{8}%-%d%e38;5;%p1%d%;m")]
    [InlineData("kitty-direct setaf via xterm+direct2", "\u001b[%?%p1%{8}%<%t3%p1%d%e38:2:%p1%{65536}%/%d:%p1%{256}%/%{255}%&%d:%p1%{255}%&%d%;m")]
    public void Compile_WhenCurrentTerminalTemplateIsValid_ProducesOwnedProgram(
        string sourceName,
        string template)
    {
        // Arrange
        var bytes = Encoding.UTF8.GetBytes(template);

        // Act
        var program = Compiler.Compile(bytes, Limits.Default);
        bytes.AsSpan().Fill((byte) 'x');

        // Assert
        sourceName.ShouldNotBeNullOrWhiteSpace();
        program.IsEmpty.ShouldBeFalse();
        program.IsIntrinsic.ShouldBeFalse();
        program.OperationCount.ShouldBeGreaterThan(0);
        program.Representation.Span[0].ShouldNotBe((byte) 'x');
    }

    /// <summary>Verifies all nine positional parameter forms are accepted.</summary>
    [Fact]
    public void Compile_WhenAllParameterIndexesAreUsed_AcceptsProgram()
    {
        // Arrange
        var template = "%p1%d%p2%d%p3%d%p4%d%p5%d%p6%d%p7%d%p8%d%p9%d"u8;

        // Act
        var program = Compiler.Compile(template, Limits.Default);

        // Assert
        program.OperationCount.ShouldBe(18);
    }

    #endregion

    #region Rejection and bounds

    /// <summary>Verifies malformed and unsupported forms are rejected during compilation.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("%")]
    [InlineData("%p0")]
    [InlineData("%p:")]
    [InlineData("%{12")]
    [InlineData("%{x}")]
    [InlineData("%'x")]
    [InlineData("%P1")]
    [InlineData("%g1")]
    [InlineData("%?")]
    [InlineData("%?%p1%d")]
    [InlineData("%?%p1%ttrue")]
    [InlineData("%e")]
    [InlineData("%;")]
    [InlineData("%q")]
    [InlineData("%p1%.q")]
    [InlineData("%p1%:.q")]
    [InlineData("%B")]
    [InlineData("%D")]
    public void Compile_WhenProgramIsMalformedOrUnsupported_Throws(string template)
    {
        // Arrange
        var bytes = Encoding.UTF8.GetBytes(template);

        // Act / Assert
        _ = Should.Throw<FormatException>(() => Compiler.Compile(bytes, Limits.Default));
    }

    /// <summary>Verifies hardware padding requests are rejected rather than delayed or stripped.</summary>
    [Theory]
    [InlineData("$<5>")]
    [InlineData("abc$<10.5*/>def")]
    [InlineData("$<0>")]
    public void Compile_WhenPaddingMarkerExists_Throws(string template)
    {
        // Arrange
        var bytes = Encoding.UTF8.GetBytes(template);

        // Act / Assert
        _ = Should.Throw<NotSupportedException>(() => Compiler.Compile(bytes, Limits.Default));
    }

    /// <summary>Verifies a command cannot consume a value absent from the compile-time stack.</summary>
    [Theory]
    [InlineData("%d")]
    [InlineData("%+")]
    [InlineData("%Pz")]
    [InlineData("%t")]
    public void Compile_WhenStackUnderflows_Throws(string template)
    {
        // Arrange
        var bytes = Encoding.UTF8.GetBytes(template);

        // Act / Assert
        _ = Should.Throw<FormatException>(() => Compiler.Compile(bytes, Limits.Default));
    }

    /// <summary>Verifies raw string length is a stack-consuming directive.</summary>
    [Fact]
    public void Compile_WhenStringLengthHasNoOperand_Throws() =>
        // Arrange / Act / Assert
        _ = Should.Throw<FormatException>(() => Compiler.Compile("%l"u8, Limits.Default));

    /// <summary>Verifies the configured program-byte bound is enforced.</summary>
    [Fact]
    public void Compile_WhenProgramExceedsByteLimit_Throws()
    {
        // Arrange
        var limits = Limits.Default with { MaxProgramBytes = 3 };

        // Act / Assert
        _ = Should.Throw<ArgumentException>(() => Compiler.Compile("abcd"u8, limits));
    }

    /// <summary>Verifies the configured operation bound is enforced.</summary>
    [Fact]
    public void Compile_WhenProgramExceedsOperationLimit_Throws()
    {
        // Arrange
        var limits = Limits.Default with { MaxProgramOperations = 1 };

        // Act / Assert
        _ = Should.Throw<ArgumentException>(() => Compiler.Compile("a%{1}"u8, limits));
    }

    /// <summary>Verifies the configured evaluation-stack bound is enforced.</summary>
    [Fact]
    public void Compile_WhenProgramExceedsStackLimit_Throws()
    {
        // Arrange
        var limits = Limits.Default with { MaxProgramStackDepth = 2 };

        // Act / Assert
        _ = Should.Throw<ArgumentException>(() => Compiler.Compile("%{1}%{2}%{3}"u8, limits));
    }

    /// <summary>Verifies printf width and precision are bounded during compilation.</summary>
    [Theory]
    [InlineData("%p1%:4d")]
    [InlineData("%p1%:.4d")]
    public void Compile_WhenPrintfBoundExceedsOutputLimit_Throws(string template)
    {
        // Arrange
        var limits = Limits.Default with { MaxProgramOutputBytes = 3 };

        // Act / Assert
        _ = Should.Throw<ArgumentException>(() =>
            Compiler.Compile(Encoding.ASCII.GetBytes(template), limits));
    }

    /// <summary>Verifies numeric printf fields cannot overflow compiler arithmetic.</summary>
    [Fact]
    public void Compile_WhenPrintfBoundOverflowsInteger_Throws()
    {
        // Arrange
        var template = "%p1%:999999999999999999999d"u8.ToArray();

        // Act / Assert
        _ = Should.Throw<FormatException>(() => Compiler.Compile(template, Limits.Default));
    }

    /// <summary>Verifies non-directive bytes remain valid opaque terminal program data.</summary>
    [Fact]
    public void Compile_WhenProgramContainsNonUtf8Bytes_PreservesRawBytes()
    {
        // Arrange
        byte[] template = [0xc3, 0x28, 0xff];

        // Act
        var program = Compiler.Compile(template, Limits.Default);

        // Assert
        program.Representation.Span.ToArray().ShouldBe(template);
    }

    /// <summary>Verifies compilation requires a non-null limit profile.</summary>
    [Fact]
    public void Compile_WhenLimitsAreNull_Throws()
    {
        // Arrange
        var template = "literal"u8.ToArray();

        // Act / Assert
        _ = Should.Throw<ArgumentNullException>(() => Compiler.Compile(template, null!));
    }

    /// <summary>Verifies all new terminfo limits remain finite and positive.</summary>
    [Fact]
    public void Limits_WhenConstructed_RequirePositiveFiniteProgramBounds()
    {
        // Arrange / Act
        var limits = Limits.Default;

        // Assert
        limits.MaxProgramBytes.ShouldBeInRange(1, 1_048_576);
        limits.MaxProgramOperations.ShouldBeInRange(1, 16_384);
        limits.MaxProgramStackDepth.ShouldBeInRange(1, 256);
        limits.MaxProgramOutputBytes.ShouldBeInRange(1, 1_048_576);
        limits.MaxStringParameterBytes.ShouldBeInRange(1, 1_048_576);
        _ = Should.Throw<ArgumentOutOfRangeException>(static () => new Limits { MaxProgramBytes = 0 });
        _ = Should.Throw<ArgumentOutOfRangeException>(static () => new Limits { MaxProgramOperations = 0 });
        _ = Should.Throw<ArgumentOutOfRangeException>(static () => new Limits { MaxProgramStackDepth = 0 });
        _ = Should.Throw<ArgumentOutOfRangeException>(static () => new Limits { MaxProgramOutputBytes = 0 });
        _ = Should.Throw<ArgumentOutOfRangeException>(static () => new Limits { MaxStringParameterBytes = 0 });
        _ = Should.Throw<ArgumentOutOfRangeException>(static () => new Limits { MaxProgramBytes = 1_048_577 });
        _ = Should.Throw<ArgumentOutOfRangeException>(static () => new Limits { MaxProgramOperations = 16_385 });
        _ = Should.Throw<ArgumentOutOfRangeException>(static () => new Limits { MaxProgramStackDepth = 257 });
        _ = Should.Throw<ArgumentOutOfRangeException>(static () => new Limits { MaxProgramOutputBytes = 1_048_577 });
        _ = Should.Throw<ArgumentOutOfRangeException>(static () => new Limits { MaxStringParameterBytes = 1_048_577 });

        var ceilings = Limits.Default with
        {
            MaxProgramBytes = 1_048_576,
            MaxProgramOperations = 16_384,
            MaxProgramStackDepth = 256,
            MaxProgramOutputBytes = 1_048_576,
            MaxStringParameterBytes = 1_048_576
        };
        ceilings.MaxProgramBytes.ShouldBe(1_048_576);
        ceilings.MaxProgramOperations.ShouldBe(16_384);
        ceilings.MaxProgramStackDepth.ShouldBe(256);
        ceilings.MaxProgramOutputBytes.ShouldBe(1_048_576);
        ceilings.MaxStringParameterBytes.ShouldBe(1_048_576);
    }

    #endregion
}
