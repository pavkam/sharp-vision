// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Rendering;

/// <summary>Maps a dash pattern to its on/off run-length cycle over a step counter.</summary>
internal static class LinePatternResolver
{
    extension(LinePattern value)
    {
        /// <summary>Determines whether one monotonic step is drawn or skipped.</summary>
        /// <param name="step">The zero-based step, incremented once per Bresenham iteration
        /// regardless of slope.</param>
        /// <returns><see langword="true"/> when the step falls in the pattern's "on" run.</returns>
        /// <remarks>
        /// Each pattern repeats an on-run followed by an off-run: <see cref="LinePattern.Solid"/>
        /// is always on; <see cref="LinePattern.DoubleDash"/> uses 3 on, 2 off;
        /// <see cref="LinePattern.TripleDash"/> uses 2 on, 2 off; and
        /// <see cref="LinePattern.QuadrupleDash"/> uses 1 on, 2 off, all in half-cell steps.
        /// </remarks>
        public bool IsStepOn(int step)
        {
            var (on, cycle) = value switch
            {
                LinePattern.DoubleDash => (3, 5),
                LinePattern.TripleDash => (2, 4),
                LinePattern.QuadrupleDash => (1, 3),
                LinePattern.Solid or _ => (1, 1)
            };

            return ((step % cycle) + cycle) % cycle < on;
        }
    }
}
