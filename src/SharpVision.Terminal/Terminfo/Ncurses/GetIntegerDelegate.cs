// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Terminfo.Ncurses;

/// <summary>Represents a name-to-integer ncurses capability export.</summary>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate int GetIntegerDelegate(nint name);
