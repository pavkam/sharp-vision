// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Abstractions;

using Input;

/// <summary>Receives stable typed terminal input synchronously and in order.</summary>
[PublicAPI]
public interface IInputSink
{
    /// <summary>Receives one keyboard transition.</summary>
    /// <param name="value">The immutable transition.</param>
    public void Input(in Stroke value);

    /// <summary>Receives one Unicode text scalar.</summary>
    /// <param name="value">The immutable text value.</param>
    public void Input(in TerminalText value);

    /// <summary>Receives one pointer event.</summary>
    /// <param name="value">The immutable pointer value.</param>
    public void Input(in Pointer value);

    /// <summary>Receives one owned paste payload.</summary>
    /// <param name="value">The immutable owned paste value.</param>
    public void Input(Paste value);

    /// <summary>Receives one focus transition.</summary>
    /// <param name="value">The immutable focus value.</param>
    public void Input(in TerminalFocus value);

    /// <summary>Receives one non-sensitive structured diagnostic.</summary>
    /// <param name="value">The immutable redacted diagnostic.</param>
    public void Input(in Diagnostic value);
}
