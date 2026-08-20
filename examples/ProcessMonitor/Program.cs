// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

// Process Monitor samples /proc (Linux) or shells out to top/sysctl (macOS); neither exists on
// Windows, and there is no third code path to maintain, so it declines to start there rather than
// silently showing an empty or wrong dashboard.
if (OperatingSystem.IsWindows())
{
    Console.Error.WriteLine("Process Monitor supports Linux and macOS only.");
    return 1;
}

var status = await ConsoleApplication.RunAsync(
    new ProcessMonitor.MonitorScreen(),
    static builder => builder.TreatControlCAsInput());
return status == ConsoleRunStatus.Failed ? 1 : 0;
