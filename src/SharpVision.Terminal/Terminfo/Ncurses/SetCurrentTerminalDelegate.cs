// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Terminfo.Ncurses;

/// <summary>Represents the ncurses 6 <c>set_curterm</c> export.</summary>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate nint SetCurrentTerminalDelegate(nint terminal);
