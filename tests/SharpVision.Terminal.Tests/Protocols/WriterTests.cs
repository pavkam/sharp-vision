// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Protocols;

using System.Buffers;

using SharpVision.Terminal.Protocols;

using Shouldly;

/// <summary>
/// Verifies byte-exact ECMA-48 sequence encoding.
/// </summary>
public sealed class WriterTests
{
    /// <summary>
    /// Verifies a two-character escape sequence.
    /// </summary>
    [Fact]
    public void Escape_WhenFinalIsValid_WritesExactBytes()
    {
        ArrayBufferWriter<byte> destination = new ArrayBufferWriter<byte>();
        Writer writer = new Writer(destination);

        writer.Escape([], (byte) '7');

        destination.WrittenSpan.ToArray().ShouldBe([0x1b, (byte) '7']);
    }

    /// <summary>
    /// Verifies parameter and final byte placement in a CSI sequence.
    /// </summary>
    [Fact]
    public void Csi_WhenParametersAreValid_WritesExactBytes()
    {
        ArrayBufferWriter<byte> destination = new ArrayBufferWriter<byte>();
        Writer writer = new Writer(destination);

        writer.Csi("12;4"u8, [], (byte) 'H');

        destination.WrittenSpan.ToArray().ShouldBe(
            [0x1b, (byte) '[', (byte) '1', (byte) '2', (byte) ';', (byte) '4', (byte) 'H']);
    }

    /// <summary>
    /// Verifies that OSC uses a decimal selector and canonical ST terminator.
    /// </summary>
    [Fact]
    public void Osc_WhenPayloadIsValid_WritesExactBytes()
    {
        ArrayBufferWriter<byte> destination = new ArrayBufferWriter<byte>();
        Writer writer = new Writer(destination);

        writer.Osc(2, "title"u8);

        destination.WrittenSpan.ToArray().ShouldBe(
            [
                0x1b,
                (byte)']',
                (byte)'2',
                (byte)';',
                (byte)'t',
                (byte)'i',
                (byte)'t',
                (byte)'l',
                (byte)'e',
                0x1b,
                (byte)'\\',
            ]);
    }

    /// <summary>
    /// Verifies the three generic ECMA-48 string introducers.
    /// </summary>
    /// <param name="kind">The string family under test.</param>
    /// <param name="introducer">The expected byte following ESC.</param>
    [Theory]
    [InlineData(SequenceKind.Apc, (byte) '_')]
    [InlineData(SequenceKind.Pm, (byte) '^')]
    [InlineData(SequenceKind.Sos, (byte) 'X')]
    public void Command_WhenKindIsValid_WritesExactBytes(SequenceKind kind, byte introducer)
    {
        ArrayBufferWriter<byte> destination = new ArrayBufferWriter<byte>();
        Writer writer = new Writer(destination);

        writer.Command(kind, "payload"u8);

        destination.WrittenSpan.ToArray().ShouldBe(
            [
                0x1b,
                introducer,
                (byte)'p',
                (byte)'a',
                (byte)'y',
                (byte)'l',
                (byte)'o',
                (byte)'a',
                (byte)'d',
                0x1b,
                (byte)'\\',
            ]);
    }

    /// <summary>
    /// Verifies DCS header, payload, and ST placement.
    /// </summary>
    [Fact]
    public void Dcs_WhenHeaderAndPayloadAreValid_WritesExactBytes()
    {
        ArrayBufferWriter<byte> destination = new ArrayBufferWriter<byte>();
        Writer writer = new Writer(destination);

        writer.Dcs("1;2"u8, "$"u8, (byte) 'q', "data"u8);

        destination.WrittenSpan.ToArray().ShouldBe(
            [
                0x1b,
                (byte)'P',
                (byte)'1',
                (byte)';',
                (byte)'2',
                (byte)'$',
                (byte)'q',
                (byte)'d',
                (byte)'a',
                (byte)'t',
                (byte)'a',
                0x1b,
                (byte)'\\',
            ]);
    }

    /// <summary>
    /// Verifies that malformed raw grammar is rejected atomically.
    /// </summary>
    [Fact]
    public void Write_WhenGrammarIsInvalid_ThrowsBeforeWriting()
    {
        ArrayBufferWriter<byte> destination = new ArrayBufferWriter<byte>();
        Writer writer = new Writer(destination);

        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => writer.Escape([], 0x2f));
        _ = Should.Throw<ArgumentException>(
            () => writer.Escape("0"u8, (byte) '7'));
        _ = Should.Throw<ArgumentException>(
            () => writer.Csi("A"u8, [], (byte) 'H'));
        _ = Should.Throw<ArgumentException>(
            () => writer.Csi([], "0"u8, (byte) 'H'));
        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => writer.Csi([], [], 0x3f));
        _ = Should.Throw<ArgumentException>(
            () => writer.Osc(2, [(byte) 'a', 0x1b, (byte) 'b']));
        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => writer.Osc(-1, []));
        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => writer.Command(SequenceKind.Csi, []));
        _ = Should.Throw<ArgumentException>(
            () => writer.Dcs([], [], (byte) 'q', [(byte) 'a', 0x07]));

        destination.WrittenCount.ShouldBe(0);
    }

    /// <summary>
    /// Verifies constructor argument validation.
    /// </summary>
    [Fact]
    public void Constructor_WhenDestinationIsNull_ThrowsArgumentNullException() =>
        _ = Should.Throw<ArgumentNullException>(static () => new Writer(null!));
}
