// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Input;

using System.Runtime.CompilerServices;

using SharpVision.Terminal.Input;

/// <summary>Observes decoder ownership through the internal test seam <see
/// cref="InputDecoder.OwnedKeyMatcherState"/>, which is excluded from the public API snapshot.</summary>
internal static class DecoderOwnershipProbe
{
    /// <summary>Creates a decoder after exercising shorter-match suffix rematching.</summary>
    /// <param name="options">The described-key options under test.</param>
    /// <returns>The live decoder and weak references to its matcher and replay workspace.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static (InputDecoder InputDecoder, WeakReference Matcher, WeakReference Replay)
        CreateAfterRematch(Options options)
    {
        var decoder = new InputDecoder(new RecordingInputSink(), options);
        decoder.Decode([0xff, 0xfe, (byte) 'x']);
        var (matcher, replay) = decoder.OwnedKeyMatcherState ??
            throw new InvalidOperationException("InputDecoder did not retain a matcher and replay workspace.");

        return (decoder, new WeakReference(matcher), new WeakReference(replay));
    }

    /// <summary>Disposes the decoder outside the caller's JIT lifetime scope.</summary>
    /// <param name="decoder">The decoder whose owned references must be released.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void Dispose(InputDecoder decoder) => decoder.Dispose();

    /// <summary>Forces bounded full collections until both former owned objects are unreachable.</summary>
    /// <param name="matcher">The weak matcher reference.</param>
    /// <param name="replay">The weak replay-workspace reference.</param>
    /// <returns>Whether both objects became unreachable within the bounded collection loop.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool WaitForRelease(WeakReference matcher, WeakReference replay)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);

            if (!matcher.IsAlive && !replay.IsAlive)
            {
                return true;
            }
        }

        return false;
    }
}
