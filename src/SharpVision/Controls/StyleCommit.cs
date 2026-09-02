// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using Styling;

/// <summary>Retains one fully preflighted style-slot mutation until its graph transaction applies
/// every value and begins ordered publication.</summary>
/// <typeparam name="TStyle">The immutable complete style value.</typeparam>
internal sealed class StyleCommit<TStyle>
    where TStyle : ControlStyle
{
    /// <summary>Initializes one preflighted commit.</summary>
    internal StyleCommit(
        StyleSlot<TStyle> slot,
        TStyle? value,
        TStyle previousStyle,
        TStyle currentStyle,
        InvalidationImpact impact,
        ResolvedAppearance previousAppearance,
        ResolvedAppearance currentAppearance,
        bool ambientFaceChanged,
        bool localOwnershipChanged)
    {
        Slot = slot;
        Value = value;
        PreviousStyle = previousStyle;
        CurrentStyle = currentStyle;
        Impact = impact;
        PreviousAppearance = previousAppearance;
        CurrentAppearance = currentAppearance;
        AmbientFaceChanged = ambientFaceChanged;
        LocalOwnershipChanged = localOwnershipChanged;
    }

    /// <summary>Gets the slot whose local value changes.</summary>
    internal StyleSlot<TStyle> Slot { get; }

    /// <summary>Gets the nullable local value to commit.</summary>
    internal TStyle? Value { get; }

    /// <summary>Gets the complete style before the transaction.</summary>
    internal TStyle PreviousStyle { get; }

    /// <summary>Gets the complete style after the transaction.</summary>
    internal TStyle CurrentStyle { get; }

    /// <summary>Gets the strongest invalidation required by the change.</summary>
    internal InvalidationImpact Impact { get; }

    /// <summary>Gets the resolved appearance before the transaction.</summary>
    internal ResolvedAppearance PreviousAppearance { get; }

    /// <summary>Gets the resolved appearance after the transaction.</summary>
    internal ResolvedAppearance CurrentAppearance { get; }

    /// <summary>Gets whether descendants inherit a changed ambient face.</summary>
    internal bool AmbientFaceChanged { get; }

    /// <summary>Gets whether the nullable local-style owner changed without necessarily changing
    /// the complete resolved style value.</summary>
    internal bool LocalOwnershipChanged { get; }

    /// <summary>Gets whether the complete resolved style changed.</summary>
    internal bool ResolvedStyleChanged => !PreviousStyle.Equals(CurrentStyle);

    /// <summary>Gets whether dependents must observe a changed resolved value or ownership policy.</summary>
    internal bool RequiresChangedCallback => ResolvedStyleChanged || LocalOwnershipChanged;

    /// <summary>Gets or sets the slot version assigned when all preflight succeeds.</summary>
    internal long SlotVersion { get; set; }

    /// <summary>Gets or sets the owner publication version assigned when all preflight succeeds.</summary>
    internal long OwnerVersion { get; set; }
}
