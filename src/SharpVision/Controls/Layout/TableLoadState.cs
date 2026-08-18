// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Layout;

/// <summary>Identifies one progressive <see cref="Table"/>'s table-wide aggregate loading state.</summary>
[PublicAPI]
public enum TableLoadState
{
    /// <summary>No fetch is currently in flight and no range has exhausted its retries.</summary>
    Idle,

    /// <summary>At least one fetch is currently in flight.</summary>
    Loading,

    /// <summary>At least one range has exhausted its retries and shows an error placeholder.</summary>
    Failed
}
