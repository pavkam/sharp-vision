// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;


/// <summary>Defines one mutable, single-owner RichText content item.</summary>
public abstract class Inline
{
    /// <summary>Initializes a detached inline.</summary>
    protected Inline()
    {
    }

    /// <summary>Gets the owning document, or null while detached.</summary>
    public RichText? Owner { get; private set; }

    /// <summary>Attaches this validated detached inline to one document.</summary>
    /// <param name="owner">The non-null owner.</param>
    internal void Attach(RichText owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        Debug.Assert(Owner is null, "Collection validation permits only detached inlines.");
        Owner = owner;
    }

    /// <summary>Releases this inline from its current owner.</summary>
    internal void Detach() => Owner = null;

    /// <summary>Validates dispatcher access and invalidates document layout.</summary>
    internal void Changed()
    {
        Owner?.VerifyMutable();
        Owner?.InlineChanged();
    }

    /// <summary>Validates dispatcher access before a derived mutation.</summary>
    internal void VerifyMutable() => Owner?.VerifyMutable();
}
