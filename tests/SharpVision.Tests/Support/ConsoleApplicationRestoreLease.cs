// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Records synchronous host-mode restoration during console preflight.</summary>
internal sealed class ConsoleApplicationRestoreLease: IDisposable
{
    private readonly List<string>? _disposalOrder;

    /// <summary>Initializes a recorder with an optional shared disposal-order log.</summary>
    /// <param name="disposalOrder">The optional shared log.</param>
    internal ConsoleApplicationRestoreLease(List<string>? disposalOrder = null) =>
        _disposalOrder = disposalOrder;

    /// <summary>Gets the number of disposal calls.</summary>
    internal int Disposals { get; private set; }

    /// <summary>Gets or sets the exact disposal failure raised after recording the attempt.</summary>
    internal Exception? DisposalFailure { get; set; }

    /// <inheritdoc/>
    public void Dispose()
    {
        Disposals++;
        _disposalOrder?.Add("restore");

        if (DisposalFailure is { } failure)
        {
            Throw(failure);
        }
    }

    private static void Throw(Exception failure) =>
        System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
}
