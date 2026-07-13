// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SharpVision.Runtime;
using SharpVision.Showcase;

ConsoleRunStatus status = await Application.RunConsoleAsync(
    new Gallery(),
    new ConsoleRunOptions
    {
        RedirectedMessage = StartupMessage.Get(),
    });
return status == ConsoleRunStatus.Failed ? 1 : 0;
