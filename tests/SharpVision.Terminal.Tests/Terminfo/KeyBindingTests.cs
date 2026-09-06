// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Terminfo;

using SharpVision.Terminal.Input;

/// <summary>
/// Verifies <see cref="KeyBinding"/> construction: structural signature compilation and the
/// raw-escape exemption for terminfo key strings that begin with Escape but describe a
/// non-ECMA-48 terminal dialect instead of one complete parser signature.
/// </summary>
public sealed class KeyBindingTests
{
    /// <summary>Verifies the Linux virtual console's <c>kf1</c>..<c>kf5</c> shape
    /// (<c>ESC [ [ &lt;letter&gt;</c>) no longer throws and is retained without a structural
    /// signature.</summary>
    [Theory]
    [InlineData((byte) 'A', Code.F1)]
    [InlineData((byte) 'B', Code.F2)]
    [InlineData((byte) 'C', Code.F3)]
    [InlineData((byte) 'D', Code.F4)]
    [InlineData((byte) 'E', Code.F5)]
    public void Constructor_WhenSequenceIsLinuxConsoleFunctionKey_IsRetainedWithoutSignature(byte final, Code code)
    {
        byte[] sequence = [0x1b, (byte) '[', (byte) '[', final];

        var binding = new KeyBinding(sequence, code);

        binding.Signature.ShouldBeNull();
        binding.Sequence.ToArray().ShouldBe(sequence);
        binding.Code.ShouldBe(code);
        binding.Modifiers.ShouldBe(Modifiers.None);
    }

    /// <summary>Verifies rxvt-unicode's Shift-modified navigation-key shape
    /// (<c>ESC [ &lt;n&gt; $</c>) no longer throws and is retained without a structural
    /// signature.</summary>
    [Fact]
    public void Constructor_WhenSequenceIsRxvtShiftModifiedKey_IsRetainedWithoutSignature()
    {
        byte[] sequence = [0x1b, (byte) '[', (byte) '2', (byte) '$'];

        var binding = new KeyBinding(sequence, Code.Insert, Modifiers.Shift);

        binding.Signature.ShouldBeNull();
        binding.Sequence.ToArray().ShouldBe(sequence);
    }

    /// <summary>Verifies an Escape-prefixed string shorter than three bytes still throws: every
    /// two-byte Escape-form sequence ECMA-48 admits already compiles into a signature, so one that
    /// still fails is genuinely malformed rather than a non-conformant dialect.</summary>
    [Fact]
    public void Constructor_WhenEscapePrefixIsTooShortForRawEscape_StillThrows()
    {
        byte[] sequence = [0x1b, (byte) '['];

        var exception = Should.Throw<ArgumentException>(() => new KeyBinding(sequence, Code.F1));

        exception.ParamName.ShouldBe("sequence");
    }

    /// <summary>Verifies a non-Escape parser-control-prefixed string that fails to compile still
    /// throws: the raw-escape exemption applies only to Escape (0x1b), not to other C0, DEL, or
    /// eight-bit C1 introducers.</summary>
    [Fact]
    public void Constructor_WhenNonEscapeParserControlPrefixFailsSignature_StillThrows()
    {
        byte[] sequence = [0x01, (byte) 'A', (byte) 'B'];

        _ = Should.Throw<ArgumentException>(() => new KeyBinding(sequence, Code.F1));
    }

    /// <summary>Verifies a string that would compile into a structural signature under a more
    /// permissive limit, but merely exceeds the active configured limit, still throws instead of
    /// silently becoming a raw-escape binding - only a sequence unrepresentable at every limit
    /// qualifies.</summary>
    [Fact]
    public void Constructor_WhenSequenceOnlyExceedsConfiguredLimit_StillThrows()
    {
        var limits = ParserLimits.Default with { MaxParameterBytes = 2 };
        byte[] sequence = [0x1b, (byte) '[', (byte) '1', (byte) '2', (byte) '3', (byte) 'A'];

        var exception = Should.Throw<ArgumentException>(() =>
            new KeyBinding(sequence, Code.Up, Modifiers.None, limits));

        exception.ParamName.ShouldBe("sequence");
    }

    /// <summary>Verifies a structurally representable Escape-form signature is unaffected by the
    /// raw-escape exemption and still compiles normally.</summary>
    [Fact]
    public void Constructor_WhenSequenceIsOrdinaryEscapeSignature_CompilesSignatureNormally()
    {
        var binding = new KeyBinding([0x1b, (byte) '[', (byte) 'A'], Code.Up);

        _ = binding.Signature.ShouldNotBeNull();
    }
}
