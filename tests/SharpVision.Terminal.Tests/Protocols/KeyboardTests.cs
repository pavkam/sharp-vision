namespace SharpVision.Terminal.Tests.Protocols;

using System.Buffers;

using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Protocols;

using Shouldly;

/// <summary>
/// Verifies exact Kitty keyboard commands, status replies, and query ordering.
/// </summary>
public sealed class KeyboardTests
{
    /// <summary>
    /// Verifies query, push, pop, and direct flag commands use official bytes.
    /// </summary>
    [Fact]
    public void Commands_WhenValuesAreValid_WriteExactBytes()
    {
        var destination = new ArrayBufferWriter<byte>();
        var writer = new Writer(destination);

        Keyboard.Query(writer);
        Keyboard.Push(writer, Enhancement.Disambiguate | Enhancement.EventTypes);
        Keyboard.Pop(writer);
        Keyboard.Pop(writer, 2);
        Keyboard.Set(writer, Enhancement.Disambiguate, EnhancementMode.Replace);

        destination.WrittenSpan.ToArray().ShouldBe(
            "\u001b[?u\u001b[>3u\u001b[<u\u001b[<2u\u001b[=1;1u"u8.ToArray());
    }

    /// <summary>
    /// Verifies unknown flags, invalid dependencies, modes, and counts write nothing.
    /// </summary>
    [Fact]
    public void Commands_WhenValueIsInvalid_ThrowBeforeWriting()
    {
        var destination = new ArrayBufferWriter<byte>();
        var writer = new Writer(destination);

        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => Keyboard.Push(writer, (Enhancement) 32));
        _ = Should.Throw<ArgumentException>(
            () => Keyboard.Push(writer, Enhancement.AssociatedText));
        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => Keyboard.Set(writer, Enhancement.Disambiguate, 0));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => Keyboard.Pop(writer, 0));

        destination.WrittenCount.ShouldBe(0);
    }

    /// <summary>
    /// Verifies a keyboard-status reply is a bounded typed CSI response.
    /// </summary>
    [Fact]
    public void TryCsi_WhenKeyboardStatusIsValid_ReturnsFlags()
    {
        Responses.TryCsi("?31"u8, [], (byte) 'u', out var response).ShouldBeTrue();

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
        _ = Responses.TryCsi("?1"u8, [], (byte) 'c', out var response);

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
        _ = Responses.TryCsi("?3"u8, [], (byte) 'u', out var keyboard);
        _ = Responses.TryCsi("?1"u8, [], (byte) 'c', out var attributes);

        tracker.Match(keyboard).ShouldBe(QueryMatch.Matched);
        tracker.Match(attributes).ShouldBe(QueryMatch.Matched);

        tracker.ActiveCount.ShouldBe(0);
        tracker.LastDiagnostic.ShouldBeNull();
    }
}
