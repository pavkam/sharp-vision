// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Dialogs;

/// <summary>Defines the standard action-button layouts offered by <see cref="MessageBox"/>.</summary>
[PublicAPI]
public enum MessageBoxButtons
{
    /// <summary>Displays only an OK button.</summary>
    Ok,

    /// <summary>Displays OK and Cancel buttons.</summary>
    OkCancel,

    /// <summary>Displays Yes and No buttons.</summary>
    YesNo,

    /// <summary>Displays Yes, No, and Cancel buttons.</summary>
    YesNoCancel
}
