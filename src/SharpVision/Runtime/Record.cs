namespace SharpVision.Runtime;

using SharpVision.Terminal.Input;
using SharpVision.Terminal.Protocols;

using TerminalDiagnostic = Terminal.Protocols.Diagnostic;
using TerminalFocus = Focus;
using TerminalText = Terminal.Input.Text;

/// <summary>Stores one copied terminal input queue value without borrowed memory.</summary>
internal readonly record struct Record
{
    private Record(RecordKind kind) => Kind = kind;

    /// <summary>Gets the stored value family.</summary>
    internal RecordKind Kind { get; }

    /// <summary>Gets a stored key value.</summary>
    internal Stroke Stroke { get; private init; }

    /// <summary>Gets a stored text value.</summary>
    internal TerminalText Text { get; private init; }

    /// <summary>Gets a stored pointer value.</summary>
    internal Pointer Pointer { get; private init; }

    /// <summary>Gets a stored owned paste.</summary>
    internal Paste? Paste { get; private init; }

    /// <summary>Gets a stored terminal focus value.</summary>
    internal TerminalFocus Focus { get; private init; }

    /// <summary>Gets a stored terminal diagnostic.</summary>
    internal TerminalDiagnostic Diagnostic { get; private init; }

    /// <summary>Gets a typed terminal protocol response.</summary>
    internal Response Response { get; private init; }

    /// <summary>Gets a stored input fault.</summary>
    internal Exception? Exception { get; private init; }

    /// <summary>Creates a key record.</summary>
    internal static Record From(Stroke value) => new(RecordKind.Key) { Stroke = value };

    /// <summary>Creates a text record.</summary>
    internal static Record From(TerminalText value) => new(RecordKind.Text) { Text = value };

    /// <summary>Creates a pointer record.</summary>
    internal static Record From(Pointer value) => new(RecordKind.Pointer) { Pointer = value };

    /// <summary>Creates a paste record.</summary>
    internal static Record From(Paste value) => new(RecordKind.Paste) { Paste = value };

    /// <summary>Creates a terminal-focus record.</summary>
    internal static Record From(TerminalFocus value) =>
        new(RecordKind.Focus) { Focus = value };

    /// <summary>Creates a diagnostic record.</summary>
    internal static Record From(TerminalDiagnostic value) =>
        new(RecordKind.Diagnostic) { Diagnostic = value };

    /// <summary>Creates a typed terminal protocol response record.</summary>
    internal static Record From(Response value) => new(RecordKind.Response) { Response = value };

    /// <summary>Creates an orderly closure record.</summary>
    internal static Record Closed() => new(RecordKind.Closed);

    /// <summary>Creates an input fault record.</summary>
    internal static Record Fault(Exception value) =>
        new(RecordKind.Fault) { Exception = value };
}
