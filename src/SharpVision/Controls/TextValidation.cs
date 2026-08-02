// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Validates single-line text accepted by control label contracts.</summary>
internal static class TextValidation
{
    extension(string value)
    {
        /// <summary>Rejects a value containing a terminal control character.</summary>
        /// <param name="paramName">The validated parameter name.</param>
        /// <param name="message">The exception message naming the specific label.</param>
        /// <exception cref="ArgumentException"><paramref name="value"/> contains a control cluster.</exception>
        public void ThrowIfContainsControls(string paramName, string message)
        {
            if (Width.Measure(value).Controls > 0)
            {
                throw new ArgumentException(message, paramName);
            }
        }
    }
}
