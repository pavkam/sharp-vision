// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using TerminalStyle = TerminalStyle;

internal readonly struct ResolvedAppearance
{
    internal TerminalStyle Style { get; init; }

    internal bool HasOpaqueFill { get; init; }
}
