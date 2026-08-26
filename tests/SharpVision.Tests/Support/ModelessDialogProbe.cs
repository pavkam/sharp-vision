// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Provides a handler-free concrete dialog for inherited default-routing tests.</summary>
internal sealed class ModelessDialogProbe: Dialog<bool>
{
    /// <summary>Initializes a detached modeless dialog probe.</summary>
    internal ModelessDialogProbe()
        : base(cancelledResult: false)
    {
    }
}
