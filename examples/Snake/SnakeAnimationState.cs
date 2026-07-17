// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Snake;

/// <summary>
/// Tracks the deterministic, bounded frame counters used by the Snake presentation.
/// </summary>
/// <remarks>
/// The owner advances this state once per visual pulse. The rainbow counter runs continuously,
/// while the death counter remains inactive until <see cref="BeginDeath"/> starts a new sequence.
/// </remarks>
internal sealed class SnakeAnimationState
{
    /// <summary>Defines the number of visual pulses in one complete rainbow cycle.</summary>
    internal const int RainbowFrames = 60;

    /// <summary>Defines the number of visual pulses used to reveal the complete death wave.</summary>
    internal const int DeathWaveFrames = 12;

    /// <summary>Defines the number of visual pulses that hold the completed death wave.</summary>
    internal const int DeathHoldFrames = 3;

    /// <summary>Initializes animation state at the first rainbow frame with no active death sequence.</summary>
    internal SnakeAnimationState()
    {
    }

    /// <summary>Gets the current zero-based rainbow frame in the range 0 through 59.</summary>
    internal int Frame { get; private set; }

    /// <summary>Gets the current rainbow phase in the half-open range [0, 1).</summary>
    internal double PrismPhase => Frame / (double) RainbowFrames;

    /// <summary>Gets whether a death-wave sequence is currently active.</summary>
    internal bool IsDeathActive => DeathPulse >= 0;

    /// <summary>
    /// Gets the current zero-based death pulse, or <c>-1</c> when no death sequence is active.
    /// </summary>
    internal int DeathPulse { get; private set; } = -1;

    /// <summary>
    /// Starts a death-wave sequence at pulse zero, replacing any sequence already in progress.
    /// </summary>
    internal void BeginDeath() => DeathPulse = 0;

    /// <summary>
    /// Advances the rainbow counter and, when active, the death-wave counter by one visual pulse.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> exactly once when an active death sequence expires; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    internal bool Advance()
    {
        Frame = (Frame + 1) % RainbowFrames;

        if (!IsDeathActive)
        {
            return false;
        }

        var lastVisiblePulse = DeathWaveFrames + DeathHoldFrames - 1;

        if (DeathPulse < lastVisiblePulse)
        {
            DeathPulse++;
            return false;
        }

        DeathPulse = -1;
        return true;
    }

    /// <summary>
    /// Calculates how many leading snake-body segments are visible for the current death pulse.
    /// </summary>
    /// <param name="bodyLength">The total body length, measured in segments.</param>
    /// <returns>
    /// The visible segment count in the inclusive range from zero through
    /// <paramref name="bodyLength"/>. An inactive sequence or empty body returns zero.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="bodyLength"/> is negative.
    /// </exception>
    internal int VisibleDeathSegments(int bodyLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bodyLength);

        if (!IsDeathActive || bodyLength == 0)
        {
            return 0;
        }

        var wave = Math.Min(DeathPulse + 1, DeathWaveFrames);
        var visible = (((long) bodyLength * wave) + DeathWaveFrames - 1) / DeathWaveFrames;
        return Math.Min(bodyLength, (int) visible);
    }
}
