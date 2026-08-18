// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Terminfo.Ncurses;

/// <summary>Abstracts the serialized ncurses terminal-description boundary.</summary>
internal interface INative: IDisposable
{
    /// <summary>Gets the process-global <c>cur_term</c> pointer.</summary>
    public nint CurrentTerminal { get; }

    /// <summary>Invokes <c>setupterm</c> with a non-null error result.</summary>
    public int Setup(string name, int outputFileDescriptor, out int error);

    /// <summary>Changes extended-name lookup and returns the previous process-global state.</summary>
    public bool EnableExtendedNames(bool enabled);

    /// <summary>Returns the raw <c>tigetflag</c> result, including <c>-1</c> for wrong type.</summary>
    public int GetFlag(string name);

    /// <summary>Returns the raw <c>tigetnum</c> result, including <c>-2</c> for wrong type.</summary>
    public int GetNumber(string name);

    /// <summary>Copies one <c>tigetstr</c> value before native terminal teardown.</summary>
    public NativeString GetString(string name, int maximumBytes);

    /// <summary>Restores one previously active <c>TERMINAL</c> pointer.</summary>
    public nint SetCurrentTerminal(nint terminal);

    /// <summary>Releases one <c>TERMINAL</c> allocated by <c>setupterm</c>.</summary>
    public void DeleteTerminal(nint terminal);
}
