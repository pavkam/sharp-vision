// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Protocols;

using Kitty.Keyboard;

using SharpVision.Terminal.Capabilities;

/// <summary>
/// Verifies exact Kitty keyboard commands, status replies, and query ordering.
/// </summary>
public sealed class KittyKeyboardTests
{
    /// <summary>
    /// Verifies query, push, pop, and direct flag commands use official bytes.
    /// </summary>
    [Fact]
    public void Commands_WhenValuesAreValid_WriteExactBytes()
    {
        var destination = new ArrayBufferWriter<byte>();
        var writer = new ProtocolWriter(destination);

        KittyKeyboard.Query(writer);
        KittyKeyboard.Push(writer, KittyKeyboardEnhancement.Disambiguate | KittyKeyboardEnhancement.EventTypes);
        KittyKeyboard.Pop(writer);
        KittyKeyboard.Pop(writer, 2);
        KittyKeyboard.Set(writer, KittyKeyboardEnhancement.Disambiguate, KittyKeyboardEnhancementMode.Replace);

        destination.WrittenSpan.ToArray().ShouldBe(
            "[?u[>3u[<u[<2u[=1;1u"u8.ToArray());
    }

    /// <summary>
    /// Verifies unknown flags, invalid dependencies, modes, and counts write nothing.
    /// </summary>
    [Fact]
    public void Commands_WhenValueIsInvalid_ThrowBeforeWriting()
    {
        var destination = new ArrayBufferWriter<byte>();
        var writer = new ProtocolWriter(destination);

        _ = Should.Throw<ArgumentOutOfRangeException>(() => KittyKeyboard.Push(writer, (KittyKeyboardEnhancement) 32));
        _ = Should.Throw<ArgumentException>(() => KittyKeyboard.Push(writer, KittyKeyboardEnhancement.AssociatedText));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => KittyKeyboard.Set(writer, KittyKeyboardEnhancement.Disambiguate, 0));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => KittyKeyboard.Pop(writer, 0));

        destination.WrittenCount.ShouldBe(0);
    }

    /// <summary>
    /// Verifies <see cref="KittyKeyboard.Set"/> with <see cref="KittyKeyboardEnhancementMode.Clear"/>
    /// accepts associated text alone, since clearing a subset of flags can only remove
    /// capability and never produces the invalid "associated text without all keys" state.
    /// </summary>
    [Fact]
    public void Set_WhenClearModeHasAssociatedTextAlone_WritesFlagsUnchanged()
    {
        var destination = new ArrayBufferWriter<byte>();
        var writer = new ProtocolWriter(destination);

        KittyKeyboard.Set(writer, KittyKeyboardEnhancement.AssociatedText, KittyKeyboardEnhancementMode.Clear);

        destination.WrittenSpan.ToArray().ShouldBe("\x1b[=16;3u"u8.ToArray());
    }

    /// <summary>
    /// Verifies <see cref="KittyKeyboard.Set"/> still rejects associated text without all-key
    /// reporting for the modes that can enable reporting.
    /// </summary>
    [Theory]
    [InlineData(KittyKeyboardEnhancementMode.Replace)]
    [InlineData(KittyKeyboardEnhancementMode.Set)]
    public void Set_WhenEnablingModeHasAssociatedTextWithoutAllKeys_ThrowsBeforeWriting(
        KittyKeyboardEnhancementMode mode)
    {
        var destination = new ArrayBufferWriter<byte>();
        var writer = new ProtocolWriter(destination);

        _ = Should.Throw<ArgumentException>(() => KittyKeyboard.Set(writer, KittyKeyboardEnhancement.AssociatedText, mode));

        destination.WrittenCount.ShouldBe(0);
    }

    /// <summary>
    /// Verifies a keyboard-status reply is a bounded typed CSI response.
    /// </summary>
    [Fact]
    public void TryCsi_WhenKeyboardStatusIsValid_ReturnsFlags()
    {
        XtermResponses.TryCsi("?31"u8, [], (byte) 'u', out var response).ShouldBeTrue();

        response.Kind.ShouldBe(ResponseKind.Keyboard);
        response.Values.ToArray().ShouldBe([31]);
    }

    /// <summary>
    /// Verifies DA arriving before keyboard status closes support conservatively.
    /// </summary>
    [Fact]
    public void Match_WhenDeviceAttributesArriveFirst_ClassifiesKeyboardUnsupported()
    {
        var tracker = new QueryTracker();
        _ = tracker.TryRegister(QueryKind.Keyboard, null, out _);
        _ = tracker.TryRegister(QueryKind.PrimaryAttributes, null, out _);
        _ = XtermResponses.TryCsi("?1"u8, [], (byte) 'c', out var response);

        tracker.Match(response).ShouldBe(QueryMatch.Matched);

        tracker.ActiveCount.ShouldBe(0);
        tracker.LastDiagnostic!.Value.Code.ShouldBe(DiagnosticCode.Unsupported);
    }

    /// <summary>
    /// Verifies keyboard status arriving before DA proves support independently.
    /// </summary>
    [Fact]
    public void Match_WhenKeyboardStatusArrivesFirst_MatchesBothQueries()
    {
        var tracker = new QueryTracker();
        _ = tracker.TryRegister(QueryKind.Keyboard, null, out _);
        _ = tracker.TryRegister(QueryKind.PrimaryAttributes, null, out _);
        _ = XtermResponses.TryCsi("?3"u8, [], (byte) 'u', out var keyboard);
        _ = XtermResponses.TryCsi("?1"u8, [], (byte) 'c', out var attributes);

        tracker.Match(keyboard).ShouldBe(QueryMatch.Matched);
        tracker.Match(attributes).ShouldBe(QueryMatch.Matched);

        tracker.ActiveCount.ShouldBe(0);
        tracker.LastDiagnostic.ShouldBeNull();
    }
}
