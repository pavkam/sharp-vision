// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Terminfo.Ncurses;

/// <summary>Represents the ncurses 6 <c>use_extended_names</c> export.</summary>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate int UseExtendedNamesDelegate(int enabled);
