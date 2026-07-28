// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Input;

using System.Reflection;
using System.Runtime.CompilerServices;

using SharpVision.Terminal.Input;

/// <summary>Observes private decoder ownership without adding production diagnostic surface.</summary>
internal static class DecoderOwnershipProbe
{
    private static readonly FieldInfo _matcherField =
        typeof(InputDecoder).GetField("_keyMatcher", BindingFlags.Instance | BindingFlags.NonPublic) ??
        throw new InvalidOperationException("Decoder matcher ownership field was not found.");

    private static readonly FieldInfo _replayField =
        typeof(InputDecoder).GetField("_keyReplay", BindingFlags.Instance | BindingFlags.NonPublic) ??
        throw new InvalidOperationException("Decoder replay ownership field was not found.");

    /// <summary>Creates a decoder after exercising shorter-match suffix rematching.</summary>
    /// <param name="options">The described-key options under test.</param>
    /// <returns>The live decoder and weak references to its matcher and replay workspace.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static (InputDecoder Decoder, WeakReference Matcher, WeakReference Replay)
        CreateAfterRematch(Options options)
    {
        var decoder = new InputDecoder(new RecordingInputSink(), options);
        decoder.Decode([0xff, 0xfe, (byte) 'x']);

        return (
            decoder,
            ReadOwnedReference(decoder, _matcherField),
            ReadOwnedReference(decoder, _replayField));
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

    private static WeakReference ReadOwnedReference(InputDecoder decoder, FieldInfo field)
    {
        var value = field.GetValue(decoder) ??
            throw new InvalidOperationException($"Decoder field {field.Name} did not retain an object.");
        return new WeakReference(value);
    }
}
