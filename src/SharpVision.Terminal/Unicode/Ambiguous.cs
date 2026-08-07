// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Unicode;

/// <summary>Selects the terminal width of Unicode East Asian Ambiguous scalars.</summary>
[PublicAPI]
public enum Ambiguous
{
    /// <summary>Measure ambiguous scalars as one terminal cell.</summary>
    Narrow,

    /// <summary>Measure ambiguous scalars as two terminal cells.</summary>
    Wide
}
