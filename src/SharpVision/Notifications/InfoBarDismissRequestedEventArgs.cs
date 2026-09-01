// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Notifications;

using System.ComponentModel;

/// <summary>Allows one requested InfoBar dismissal to be cancelled before state changes.</summary>
[PublicAPI]
public sealed class InfoBarDismissRequestedEventArgs: CancelEventArgs
{
    /// <summary>Initializes a non-cancelled dismissal request.</summary>
    public InfoBarDismissRequestedEventArgs()
    {
    }
}
