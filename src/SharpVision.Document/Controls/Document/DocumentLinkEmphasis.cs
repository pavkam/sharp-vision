// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Document;

/// <summary>Identifies which <see cref="DocumentStyle"/> face family paints one
/// <see cref="DocumentLink"/>.</summary>
[PublicAPI]
public enum DocumentLinkEmphasis
{
    /// <summary>Paints with <see cref="DocumentStyle.LinkFace"/> and
    /// <see cref="DocumentStyle.ActiveLinkFace"/> - an ordinary link that reads as part of the
    /// surrounding text.</summary>
    Standard,

    /// <summary>Paints with <see cref="DocumentStyle.ActionLinkFace"/> and
    /// <see cref="DocumentStyle.ActiveActionLinkFace"/> - a solid, high-contrast chip that reads as a
    /// call-to-action button, while remaining exactly as interactive as a standard link.</summary>
    Action
}
