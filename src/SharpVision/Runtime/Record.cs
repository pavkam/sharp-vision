// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Runtime;

using SharpVision.Terminal.Graphics;
using SharpVision.Terminal.Input;

using TerminalClipboardReply = ClipboardReply;
using TerminalDiagnostic = Diagnostic;
using TerminalFocus = Terminal.Input.Focus;
using TerminalKittyClipboardPacket = Terminal.Kitty.Clipboard.Packet;
using TerminalText = Terminal.Input.Text;

/// <summary>Stores one copied terminal input queue value without borrowed memory.</summary>
internal readonly record struct Record
{
    private Record(RecordKind kind) => Kind = kind;

    /// <summary>Gets the stored value family.</summary>
    public RecordKind Kind { get; }

    /// <summary>Gets a stored key value.</summary>
    public Stroke Stroke { get; private init; }

    /// <summary>Gets a stored text value.</summary>
    public TerminalText Text { get; private init; }

    /// <summary>Gets a stored pointer value.</summary>
    public Pointer Pointer { get; private init; }

    /// <summary>Gets a stored owned paste.</summary>
    public Paste? Paste { get; private init; }

    /// <summary>Gets a stored terminal focus value.</summary>
    public TerminalFocus Focus { get; private init; }

    /// <summary>Gets a stored terminal diagnostic.</summary>
    public TerminalDiagnostic Diagnostic { get; private init; }

    /// <summary>Gets a typed terminal protocol response.</summary>
    public Response Response { get; private init; }

    /// <summary>Gets a typed terminal palette response.</summary>
    public PaletteResponse PaletteResponse { get; private init; }

    /// <summary>Gets a typed terminal metrics response.</summary>
    public MetricsResponse MetricsResponse { get; private init; }

    /// <summary>Gets a typed terminal status-string response.</summary>
    public StatusResponse StatusResponse { get; private init; }

    /// <summary>Gets a typed terminal capability-string response.</summary>
    public CapabilityResponse? CapabilityResponse { get; private init; }

    /// <summary>Gets a decoded OSC 52 clipboard reply.</summary>
    public TerminalClipboardReply ClipboardReply { get; private init; }

    /// <summary>Gets a decoded Kitty OSC 5522 clipboard packet.</summary>
    public TerminalKittyClipboardPacket? KittyClipboardPacket { get; private init; }

    /// <summary>Gets graphics placements that fell back to ordinary cells during one committed frame.</summary>
    public IReadOnlyList<GraphicsPlacementDiagnostic> GraphicsDiagnostics { get; private init; } =
        Array.Empty<GraphicsPlacementDiagnostic>();

    /// <summary>Gets a stored input fault.</summary>
    public Exception? Exception { get; private init; }

    /// <summary>Creates a key record.</summary>
    public static Record From(Stroke value) => new(RecordKind.Key) { Stroke = value };

    /// <summary>Creates a text record.</summary>
    public static Record From(TerminalText value) => new(RecordKind.Text) { Text = value };

    /// <summary>Creates a pointer record.</summary>
    public static Record From(Pointer value) => new(RecordKind.Pointer) { Pointer = value };

    /// <summary>Creates a paste record.</summary>
    public static Record From(Paste value) => new(RecordKind.Paste) { Paste = value };

    /// <summary>Creates a terminal-focus record.</summary>
    public static Record From(TerminalFocus value) =>
        new(RecordKind.Focus) { Focus = value };

    /// <summary>Creates a diagnostic record.</summary>
    public static Record From(TerminalDiagnostic value) =>
        new(RecordKind.Diagnostic) { Diagnostic = value };

    /// <summary>Creates a typed terminal protocol response record.</summary>
    public static Record From(Response value) => new(RecordKind.Response) { Response = value };

    /// <summary>Creates a typed terminal palette response record.</summary>
    public static Record From(PaletteResponse value) =>
        new(RecordKind.PaletteResponse) { PaletteResponse = value };

    /// <summary>Creates a typed terminal metrics response record.</summary>
    public static Record From(MetricsResponse value) =>
        new(RecordKind.MetricsResponse) { MetricsResponse = value };

    /// <summary>Creates a typed terminal status-string response record.</summary>
    public static Record From(StatusResponse value) =>
        new(RecordKind.StatusResponse) { StatusResponse = value };

    /// <summary>Creates a typed terminal capability-string response record.</summary>
    public static Record From(CapabilityResponse value) =>
        new(RecordKind.CapabilityResponse) { CapabilityResponse = value };

    /// <summary>Creates a decoded OSC 52 clipboard reply record.</summary>
    public static Record From(TerminalClipboardReply value) =>
        new(RecordKind.ClipboardReply) { ClipboardReply = value };

    /// <summary>Creates a decoded Kitty OSC 5522 clipboard packet record.</summary>
    public static Record From(TerminalKittyClipboardPacket value) =>
        new(RecordKind.KittyClipboardPacket) { KittyClipboardPacket = value };

    /// <summary>Creates a graphics diagnostic record.</summary>
    public static Record From(IReadOnlyList<GraphicsPlacementDiagnostic> value) =>
        new(RecordKind.GraphicsDiagnostic) { GraphicsDiagnostics = value };

    /// <summary>Creates an orderly closure record.</summary>
    public static Record Closed() => new(RecordKind.Closed);

    /// <summary>Creates an input fault record.</summary>
    public static Record Fault(Exception value) =>
        new(RecordKind.Fault) { Exception = value };
}
