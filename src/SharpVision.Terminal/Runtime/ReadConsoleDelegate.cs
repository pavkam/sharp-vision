// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Runtime;

/// <summary>
/// Mirrors <see cref="RuntimeInterop.TryReadConsole"/>'s shape so
/// <see cref="WindowsConsoleInputStream"/> can substitute a test double for the native call.
/// </summary>
/// <param name="handle">The console input handle.</param>
/// <param name="buffer">The buffer receiving the code units read.</param>
/// <param name="charsToRead">The buffer's capacity, in <see cref="char"/> units.</param>
/// <param name="charsRead">Receives the number of code units actually read.</param>
/// <returns>True when the call completed, including a call that was aborted mid-flight.</returns>
internal unsafe delegate bool ReadConsoleDelegate(nint handle, char* buffer, uint charsToRead, out uint charsRead);
