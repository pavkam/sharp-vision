// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Provides a non-built-in semantic caption owner for access-key infrastructure tests.</summary>
internal sealed class ProbeAccessKeyCaptionOwner: ContentControl, IAccessKeyCaptionOwner
{
    /// <summary>Gets the number of accepted semantic access-key invocations.</summary>
    internal int AccessKeyInvocations { get; private set; }

    /// <inheritdoc/>
    protected override string? AccessKeyText => Content is IAccessKeyCaption caption ? caption.Text : null;

    /// <inheritdoc/>
    protected override bool OnAccessKey(Rune key)
    {
        _ = key;

        if (!EffectiveIsEnabled || !EffectiveIsVisible)
        {
            return false;
        }

        AccessKeyInvocations++;
        return true;
    }

    /// <inheritdoc/>
    bool IAccessKeyCaptionOwner.OwnsAccessKeyCaption(ControlBase candidate) =>
        ReferenceEquals(Content, candidate);
}
