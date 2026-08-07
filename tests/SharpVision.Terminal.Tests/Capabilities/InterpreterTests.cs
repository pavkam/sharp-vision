// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Capabilities;
/// <summary>Verifies bounded execution of compiled ncurses terminfo programs.</summary>
public sealed class InterpreterTests
{
    #region Real terminal templates

    /// <summary>Verifies exact current indexed, direct-color, screen, and cursor templates.</summary>
    [Theory]
    [InlineData("\u001b[%i%p1%d;%p2%dH", "\u001b[1;5H", 0, 4)]
    [InlineData("\u001b[%?%p1%{8}%<%t3%p1%d%e%p1%{16}%<%t9%p1%{8}%-%d%e38;5;%p1%d%;m", "\u001b[31m", 1)]
    [InlineData("\u001b[%?%p1%{8}%<%t3%p1%d%e%p1%{16}%<%t9%p1%{8}%-%d%e38;5;%p1%d%;m", "\u001b[94m", 12)]
    [InlineData("\u001b[%?%p1%{8}%<%t3%p1%d%e%p1%{16}%<%t9%p1%{8}%-%d%e38;5;%p1%d%;m", "\u001b[38;5;200m", 200)]
    [InlineData("\u001b[%?%p1%{8}%<%t3%p1%d%e38:2::%p1%{65536}%/%d:%p1%{256}%/%{255}%&%d:%p1%{255}%&%d%;m", "\u001b[38:2::17:34:51m", 1122867)]
    [InlineData("\u001b[0%?%p6%t;1%;%?%p1%t;3%;%?%p2%t;4%;m", "\u001b[0;3;4m", 1, 1, 0, 0, 0, 0)]
    public void Write_WhenRealTemplateExecutes_ProducesExactBytes(
        string template,
        string expected,
        params int[] values)
    {
        // Arrange
        var parameters = values.Cast<object?>().ToArray();

        // Act
        var actual = Expand(template, parameters);

        // Assert
        actual.ShouldBe(expected);
    }

    #endregion

    #region Language operations

    /// <summary>Verifies positional parameters one through nine preserve order.</summary>
    [Fact]
    public void Write_WhenAllParametersAreUsed_ProducesEachValue()
    {
        // Arrange
        var template = "%p9%d,%p8%d,%p7%d,%p6%d,%p5%d,%p4%d,%p3%d,%p2%d,%p1%d";
        object?[] parameters = [1, 2, 3, 4, 5, 6, 7, 8, 9];

        // Act
        var actual = Expand(template, parameters);

        // Assert
        actual.ShouldBe("9,8,7,6,5,4,3,2,1");
    }

    /// <summary>Verifies incrementing the first two copied parameters never mutates caller storage.</summary>
    [Fact]
    public void Write_WhenIncrementDirectiveIsUsed_DoesNotMutateCallerParameters()
    {
        // Arrange
        object?[] parameters = [0, 4];

        // Act
        var actual = Expand("%i%p1%d;%p2%d", parameters);

        // Assert
        actual.ShouldBe("1;5");
        parameters.ShouldBe([0, 4]);
    }

    /// <summary>Verifies decimal constants, character constants, and literal percent output.</summary>
    [Fact]
    public void Write_WhenConstantsAndPercentAreUsed_ProducesExactText()
    {
        // Arrange / Act
        var actual = Expand("%{65}%c:%'B'%c:%%", []);

        // Assert
        actual.ShouldBe("A:B:%");
    }

    /// <summary>Verifies dynamic and static variable forms store and reload values.</summary>
    [Fact]
    public void Write_WhenVariablesAreUsed_ReloadsStoredValues()
    {
        // Arrange / Act
        var actual = Expand("%{7}%Pa%ga%d:%{9}%PA%gA%d", []);

        // Assert
        actual.ShouldBe("7:9");
    }

    /// <summary>Verifies dynamic variables reset while static variables persist within one interpreter.</summary>
    [Fact]
    public void Write_WhenInterpreterIsReused_AppliesNcursesVariableLifetimes()
    {
        // Arrange
        var store = "%p1%PA%p1%Pa"u8.Compile(ProgramLimits.Default);
        var load = "%gA%d:%ga%d"u8.Compile(ProgramLimits.Default);
        var interpreter = new Interpreter(ProgramLimits.Default);
        var destination = new ArrayBufferWriter<byte>();

        // Act
        interpreter.Write(store, [7], destination);
        interpreter.Write(load, [], destination);

        // Assert
        Encoding.ASCII.GetString(destination.WrittenSpan).ShouldBe("7:0");
    }

    /// <summary>Verifies a persisted static string owns bytes beyond the caller's mutation boundary.</summary>
    [Fact]
    public void Write_WhenStaticStringIsStored_OwnsPersistedBytes()
    {
        // Arrange
        var store = "%p1%PA"u8.Compile(ProgramLimits.Default);
        var load = "%gA%s"u8.Compile(ProgramLimits.Default);
        var interpreter = new Interpreter(ProgramLimits.Default);
        var destination = new ArrayBufferWriter<byte>();
        var source = "owned"u8.ToArray();

        // Act
        interpreter.Write(store, [source], destination);
        source.AsSpan().Fill((byte) 'x');
        interpreter.Write(load, [], destination);

        // Assert
        Encoding.ASCII.GetString(destination.WrittenSpan).ShouldBe("owned");
    }

    /// <summary>Verifies every required arithmetic, bitwise, logical, and comparison operator.</summary>
    [Theory]
    [InlineData("%{7}%{3}%+%d", "10")]
    [InlineData("%{7}%{3}%-%d", "4")]
    [InlineData("%{7}%{3}%*%d", "21")]
    [InlineData("%{7}%{3}%/%d", "2")]
    [InlineData("%{7}%{3}%m%d", "1")]
    [InlineData("%{6}%{3}%&%d", "2")]
    [InlineData("%{6}%{3}%|%d", "7")]
    [InlineData("%{6}%{3}%^%d", "5")]
    [InlineData("%{7}%{7}%=%d", "1")]
    [InlineData("%{3}%{7}%<%d", "1")]
    [InlineData("%{7}%{3}%>%d", "1")]
    [InlineData("%{1}%{0}%A%d", "0")]
    [InlineData("%{1}%{0}%O%d", "1")]
    [InlineData("%{0}%!%d", "1")]
    [InlineData("%{0}%~%d", "-1")]
    public void Write_WhenOperatorIsUsed_ProducesExpectedValue(string template, string expected)
    {
        // Arrange / Act
        var actual = Expand(template, []);

        // Assert
        actual.ShouldBe(expected);
    }

    /// <summary>Verifies numeric arithmetic uses documented unchecked signed Int32 semantics.</summary>
    [Fact]
    public void Write_WhenArithmeticOverflows_WrapsSignedInt32()
    {
        // Arrange / Act
        var actual = Expand("%p1%{1}%+%d", [int.MaxValue]);

        // Assert
        actual.ShouldBe(int.MinValue.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>Verifies parameter increment also wraps without mutating caller storage.</summary>
    [Fact]
    public void Write_WhenIncrementOverflows_WrapsSignedInt32()
    {
        // Arrange
        object?[] parameters = [int.MaxValue, int.MaxValue];

        // Act
        var actual = Expand("%i%p1%d;%p2%d", parameters);

        // Assert
        actual.ShouldBe($"{int.MinValue};{int.MinValue}");
        parameters.ShouldBe([int.MaxValue, int.MaxValue]);
    }

    /// <summary>Verifies nested true and false conditionals execute only the selected branch.</summary>
    [Theory]
    [InlineData(0, "zero")]
    [InlineData(1, "one")]
    [InlineData(2, "many")]
    public void Write_WhenConditionalIsNested_ProducesSelectedBranch(int value, string expected)
    {
        // Arrange
        var template = "%?%p1%{0}%=%tzero%e%?%p1%{1}%=%tone%emany%;%;";

        // Act
        var actual = Expand(template, [value]);

        // Assert
        actual.ShouldBe(expected);
    }

    /// <summary>Verifies ncurses chained else-if conditionals select each branch.</summary>
    [Theory]
    [InlineData(1, "one")]
    [InlineData(2, "two")]
    [InlineData(3, "other")]
    public void Write_WhenConditionalUsesElseIfChain_ProducesSelectedBranch(int value, string expected)
    {
        // Arrange
        var template = "%?%p1%{1}%=%tone%e%p1%{2}%=%ttwo%eother%;";

        // Act
        var actual = Expand(template, [value]);

        // Assert
        actual.ShouldBe(expected);
    }

    /// <summary>Verifies ncurses printf flags, width, precision, bases, character, and string output.</summary>
    [Theory]
    [InlineData("%p1%:+5d", "  +42", 42)]
    [InlineData("%p1%: d", " 42", 42)]
    [InlineData("%p1%:-5d", "42   ", 42)]
    [InlineData("%p1%:04d", "0042", 42)]
    [InlineData("%p1%:.4d", "0042", 42)]
    [InlineData("%p1%.d", "", 0)]
    [InlineData("%p1%.d", "42", 42)]
    [InlineData("%p1%:.d", "", 0)]
    [InlineData("%p1%:.d", "42", 42)]
    [InlineData("%p1%:#x", "0x2a", 42)]
    [InlineData("%p1%:#X", "0X2A", 42)]
    [InlineData("%p1%:#o", "052", 42)]
    [InlineData("%p1%:#.0o", "0", 0)]
    [InlineData("%p1%:#.3o", "010", 8)]
    [InlineData("%p1%:#.0x", "", 0)]
    [InlineData("%p1%:#.3x", "0x008", 8)]
    [InlineData("%p1%:#.3X", "0X008", 8)]
    [InlineData("%p1%c", "A", 65)]
    public void Write_WhenPrintfFormatIsUsed_ProducesExpectedText(
        string template,
        string expected,
        int value)
    {
        // Arrange / Act
        var actual = Expand(template, [value]);

        // Assert
        actual.ShouldBe(expected);
    }

    /// <summary>Verifies owned raw string parameters can be formatted and precision-limited.</summary>
    [Fact]
    public void Write_WhenRawStringParameterIsUsed_ProducesBoundedBytes()
    {
        // Arrange
        object?[] parameters = ["hello"u8.ToArray()];

        // Act
        var actual = Expand("%p1%:.4s", parameters);

        // Assert
        actual.ShouldBe("hell");
    }

    /// <summary>Verifies omitted string precision digits mean a precision of zero.</summary>
    [Theory]
    [InlineData("%p1%.s")]
    [InlineData("%p1%:.s")]
    public void Write_WhenRawStringPrecisionDigitsAreOmitted_ProducesEmptyBytes(string template)
    {
        // Arrange
        object?[] parameters = ["hello"u8.ToArray()];

        // Act
        var actual = Expand(template, parameters);

        // Assert
        actual.ShouldBeEmpty();
    }

    /// <summary>Verifies raw string length counts bytes, including non-UTF-8 values.</summary>
    [Fact]
    public void Write_WhenStringLengthIsUsed_ProducesRawByteCount()
    {
        // Arrange
        object?[] parameters = [new byte[] { 0xc3, 0x28, 0xff }];

        // Act
        var actual = Expand("%p1%l%d", parameters);

        // Assert
        actual.ShouldBe("3");
    }

    /// <summary>Verifies raw string length rejects a numeric operand before destination mutation.</summary>
    [Fact]
    public void Write_WhenStringLengthReceivesNumber_ThrowsWithoutWriting()
    {
        // Arrange
        var program = "%p1%l%d"u8.Compile(ProgramLimits.Default);
        var interpreter = new Interpreter(ProgramLimits.Default);
        var destination = new ArrayBufferWriter<byte>();

        // Act / Assert
        _ = Should.Throw<ArgumentException>(() =>
            interpreter.Write(program, [3], destination));
        destination.WrittenCount.ShouldBe(0);
    }

    /// <summary>Verifies loading a static string snapshots the value before a later assignment.</summary>
    [Fact]
    public void Write_WhenLoadedStaticStringIsReassigned_PreservesLoadedSnapshot()
    {
        // Arrange
        var program = "%p1%PA%gA%p2%PA%s"u8.Compile(ProgramLimits.Default);
        var interpreter = new Interpreter(ProgramLimits.Default);
        var destination = new ArrayBufferWriter<byte>();
        object?[] parameters = ["first"u8.ToArray(), "other"u8.ToArray()];

        // Act
        interpreter.Write(program, parameters, destination);

        // Assert
        Encoding.ASCII.GetString(destination.WrittenSpan).ShouldBe("first");
    }

    /// <summary>Verifies repeated persisted assignment owns the latest successful source bytes.</summary>
    [Fact]
    public void Write_WhenStaticStringIsAssignedRepeatedly_OwnsLatestSuccessfulBytes()
    {
        // Arrange
        var store = "%p1%PA"u8.Compile(ProgramLimits.Default);
        var load = "%gA%s"u8.Compile(ProgramLimits.Default);
        var interpreter = new Interpreter(ProgramLimits.Default);
        var destination = new ArrayBufferWriter<byte>();
        var first = "first"u8.ToArray();
        var second = "second"u8.ToArray();

        // Act
        interpreter.Write(store, [first], destination);
        interpreter.Write(store, [second], destination);
        first.AsSpan().Fill((byte) 'x');
        second.AsSpan().Fill((byte) 'y');
        interpreter.Write(load, [], destination);

        // Assert
        Encoding.ASCII.GetString(destination.WrittenSpan).ShouldBe("second");
    }

    /// <summary>Verifies opaque non-UTF-8 literal and character bytes reach the destination exactly.</summary>
    [Fact]
    public void Write_WhenProgramContainsRawBytes_PreservesExactBytes()
    {
        // Arrange
        byte[] template = [0xc3, 0x28, (byte) '%', (byte) '{', (byte) '2', (byte) '5', (byte) '5', (byte) '}', (byte) '%', (byte) 'c'];
        var program = template.Compile(ProgramLimits.Default);
        var interpreter = new Interpreter(ProgramLimits.Default);
        var destination = new ArrayBufferWriter<byte>();

        // Act
        interpreter.Write(program, [], destination);

        // Assert
        destination.WrittenSpan.ToArray().ShouldBe([0xc3, 0x28, 0xff]);
    }

    #endregion

    #region Validation before destination mutation

    /// <summary>Verifies missing and mismatched parameter kinds fail before destination mutation.</summary>
    [Theory]
    [InlineData("%p1%d", null)]
    [InlineData("%p1%d", "bytes")]
    [InlineData("%p1%s", 42)]
    public void Write_WhenParameterIsMissingOrWrongKind_ThrowsWithoutWriting(
        string template,
        object? parameter)
    {
        // Arrange
        var program = Encoding.UTF8.GetBytes(template).Compile(ProgramLimits.Default);
        var interpreter = new Interpreter(ProgramLimits.Default);
        var destination = new ArrayBufferWriter<byte>();
        destination.Write("prior"u8);
        object?[] parameters = parameter is null ? [] : [parameter is string ? "bytes"u8.ToArray() : parameter];

        // Act / Assert
        _ = Should.Throw<ArgumentException>(() => interpreter.Write(program, parameters, destination));
        Encoding.UTF8.GetString(destination.WrittenSpan).ShouldBe("prior");
    }

    /// <summary>Verifies string parameters are bounded opaque bytes rather than UTF-8 text.</summary>
    [Fact]
    public void Write_WhenStringParameterContainsNonUtf8Bytes_PreservesRawBytes()
    {
        // Arrange
        var program = "%p1%s"u8.Compile(ProgramLimits.Default);
        var interpreter = new Interpreter(ProgramLimits.Default);
        var destination = new ArrayBufferWriter<byte>();
        object?[] parameters = [new byte[] { 0xc3, 0x28, 0xff }];

        // Act
        interpreter.Write(program, parameters, destination);

        // Assert
        destination.WrittenSpan.ToArray().ShouldBe([0xc3, 0x28, 0xff]);
    }

    /// <summary>Verifies a string parameter cannot exceed its configured byte bound.</summary>
    [Theory]
    [InlineData("%p1%s")]
    [InlineData("%p1%l%d")]
    public void Write_WhenStringParameterExceedsLimit_ThrowsWithoutWriting(string template)
    {
        // Arrange
        var limits = ProgramLimits.Default with { MaxStringParameterBytes = 3 };
        var program = Encoding.ASCII.GetBytes(template).Compile(limits);
        var interpreter = new Interpreter(limits);
        var destination = new ArrayBufferWriter<byte>();
        object?[] parameters = ["four"u8.ToArray()];

        // Act / Assert
        _ = Should.Throw<ArgumentException>(() => interpreter.Write(program, parameters, destination));
        destination.WrittenCount.ShouldBe(0);
    }

    /// <summary>Verifies unsigned and wider numeric inputs are rejected before destination mutation.</summary>
    [Theory]
    [InlineData((uint) 1)]
    [InlineData((long) 1)]
    [InlineData((ulong) 1)]
    public void Write_WhenNumericParameterIsNotSignedInt32_ThrowsWithoutWriting(object value)
    {
        // Arrange
        var program = "%p1%d"u8.Compile(ProgramLimits.Default);
        var interpreter = new Interpreter(ProgramLimits.Default);
        var destination = new ArrayBufferWriter<byte>();

        // Act / Assert
        _ = Should.Throw<ArgumentException>(() =>
            interpreter.Write(program, [value], destination));
        destination.WrittenCount.ShouldBe(0);
    }

    /// <summary>Verifies division and modulo by zero fail before destination mutation.</summary>
    [Theory]
    [InlineData("/")]
    [InlineData("m")]
    public void Write_WhenDivisorIsZero_ThrowsWithoutWriting(string operation)
    {
        // Arrange
        var program = Encoding.UTF8.GetBytes($"%p1%p2%{operation}%d").Compile(ProgramLimits.Default);
        var interpreter = new Interpreter(ProgramLimits.Default);
        var destination = new ArrayBufferWriter<byte>();
        object?[] parameters = [7, 0];

        // Act / Assert
        _ = Should.Throw<DivideByZeroException>(() => interpreter.Write(program, parameters, destination));
        destination.WrittenCount.ShouldBe(0);
    }

    /// <summary>
    /// Verifies dividing <see cref="int.MinValue"/> by -1 (the one CPU-level division overflow
    /// case, since the mathematical result exceeds <see cref="int.MaxValue"/>) saturates back to
    /// <see cref="int.MinValue"/> instead of throwing <see cref="OverflowException"/> and aborting
    /// an otherwise well-formed terminal capability expansion.
    /// </summary>
    [Fact]
    public void Write_WhenDividingIntMinValueByNegativeOne_SaturatesToIntMinValue()
    {
        // Arrange
        var program = "%p1%p2%/%d"u8.Compile(ProgramLimits.Default);
        var interpreter = new Interpreter(ProgramLimits.Default);
        var destination = new ArrayBufferWriter<byte>();
        object?[] parameters = [int.MinValue, -1];

        // Act
        interpreter.Write(program, parameters, destination);

        // Assert
        Encoding.ASCII.GetString(destination.WrittenSpan).ShouldBe(int.MinValue.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Verifies the modulo counterpart of the same CPU-level overflow case (int.MinValue % -1)
    /// evaluates to 0, matching integer division identity (dividend - divisor * quotient == 0
    /// here), instead of throwing OverflowException.
    /// </summary>
    [Fact]
    public void Write_WhenTakingModuloOfIntMinValueByNegativeOne_EvaluatesToZero()
    {
        // Arrange
        var program = "%p1%p2%m%d"u8.Compile(ProgramLimits.Default);
        var interpreter = new Interpreter(ProgramLimits.Default);
        var destination = new ArrayBufferWriter<byte>();
        object?[] parameters = [int.MinValue, -1];

        // Act
        interpreter.Write(program, parameters, destination);

        // Assert
        Encoding.ASCII.GetString(destination.WrittenSpan).ShouldBe("0");
    }

    /// <summary>Verifies failed evaluation does not publish staged static-variable changes.</summary>
    [Fact]
    public void Write_WhenEvaluationFails_DoesNotCommitStaticVariables()
    {
        // Arrange
        var failing = "%{9}%PA%{1}%{0}%/%d"u8.Compile(ProgramLimits.Default);
        var load = "%gA%d"u8.Compile(ProgramLimits.Default);
        var interpreter = new Interpreter(ProgramLimits.Default);
        var destination = new ArrayBufferWriter<byte>();

        // Act
        _ = Should.Throw<DivideByZeroException>(() => interpreter.Write(failing, [], destination));
        interpreter.Write(load, [], destination);

        // Assert
        Encoding.ASCII.GetString(destination.WrittenSpan).ShouldBe("0");
    }

    /// <summary>Verifies a failed static-string assignment preserves the previous owned value.</summary>
    [Fact]
    public void Write_WhenStaticStringAssignmentFails_PreservesPreviousValue()
    {
        // Arrange
        var store = "%p1%PA"u8.Compile(ProgramLimits.Default);
        var failing = "%p1%PA%{1}%{0}%/%d"u8.Compile(ProgramLimits.Default);
        var load = "%gA%s"u8.Compile(ProgramLimits.Default);
        var interpreter = new Interpreter(ProgramLimits.Default);
        var destination = new ArrayBufferWriter<byte>();

        // Act
        interpreter.Write(store, ["old"u8.ToArray()], destination);
        _ = Should.Throw<DivideByZeroException>(() =>
            interpreter.Write(failing, ["new"u8.ToArray()], destination));
        interpreter.Write(load, [], destination);

        // Assert
        Encoding.ASCII.GetString(destination.WrittenSpan).ShouldBe("old");
    }

    /// <summary>Verifies expansion output is bounded before destination mutation.</summary>
    [Fact]
    public void Write_WhenExpansionExceedsOutputLimit_ThrowsWithoutWriting()
    {
        // Arrange
        var limits = ProgramLimits.Default with { MaxProgramOutputBytes = 3 };
        var program = "four"u8.Compile(limits);
        var interpreter = new Interpreter(limits);
        var destination = new ArrayBufferWriter<byte>();

        // Act / Assert
        _ = Should.Throw<ArgumentException>(() => interpreter.Write(program, [], destination));
        destination.WrittenCount.ShouldBe(0);
    }

    /// <summary>Verifies intrinsic, default, and empty values are not interpreted as database programs.</summary>
    [Fact]
    public void Write_WhenProgramIsIntrinsic_ThrowsWithoutWriting()
    {
        // Arrange
        var interpreter = new Interpreter(ProgramLimits.Default);
        var destination = new ArrayBufferWriter<byte>();

        // Act / Assert
        _ = Should.Throw<InvalidOperationException>(() =>
            interpreter.Write(DescriptionProgram.Intrinsic, [], destination));
        _ = Should.Throw<InvalidOperationException>(() =>
            interpreter.Write(default, [], destination));
        _ = Should.Throw<InvalidOperationException>(() =>
            interpreter.Write(new DescriptionProgram([]), [], destination));
        destination.WrittenCount.ShouldBe(0);
    }

    /// <summary>Verifies required owned interpreter arguments are rejected.</summary>
    [Fact]
    public void ConstructorOrWrite_WhenRequiredArgumentIsNull_Throws()
    {
        // Arrange
        var program = "ok"u8.Compile(ProgramLimits.Default);
        var interpreter = new Interpreter(ProgramLimits.Default);

        // Act / Assert
        _ = Should.Throw<ArgumentNullException>(() => new Interpreter(null!));
        _ = Should.Throw<ArgumentNullException>(() =>
            interpreter.Write(program, [], null!));
    }

    /// <summary>Verifies paired execution rejects either missing destination.</summary>
    [Fact]
    public void WritePair_WhenDestinationIsNull_Throws()
    {
        // Arrange
        var program = "ok"u8.Compile(ProgramLimits.Default);
        var interpreter = new Interpreter(ProgramLimits.Default);
        var destination = new ArrayBufferWriter<byte>();

        // Act / Assert
        _ = Should.Throw<ArgumentNullException>(() =>
            interpreter.WritePair(program, program, null!, destination));
        _ = Should.Throw<ArgumentNullException>(() =>
            interpreter.WritePair(program, program, destination, null!));
    }

    /// <summary>Verifies either paired program must fit the interpreter limits before destination mutation.</summary>
    [Fact]
    public void WritePair_WhenProgramExceedsInterpreterLimit_ThrowsWithoutWriting()
    {
        // Arrange
        var withinLimit = "ok"u8.Compile(ProgramLimits.Default);
        var outsideLimit = "four"u8.Compile(ProgramLimits.Default);
        var interpreter = new Interpreter(ProgramLimits.Default with { MaxProgramBytes = 3 });
        var first = new ArrayBufferWriter<byte>();
        var second = new ArrayBufferWriter<byte>();

        // Act / Assert
        _ = Should.Throw<InvalidOperationException>(() =>
            interpreter.WritePair(withinLimit, outsideLimit, first, second));
        first.WrittenCount.ShouldBe(0);
        second.WrittenCount.ShouldBe(0);
    }

    #endregion

    private static string Expand(string template, object?[] parameters)
    {
        var program = Encoding.UTF8.GetBytes(template).Compile(ProgramLimits.Default);
        var interpreter = new Interpreter(ProgramLimits.Default);
        var destination = new ArrayBufferWriter<byte>();

        interpreter.Write(program, parameters, destination);

        return Encoding.UTF8.GetString(destination.WrittenSpan);
    }
}
