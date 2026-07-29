// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

var status = await ConsoleApplication.RunAsync(
    new Gallery(),
    static builder => builder.TreatControlCAsInput());

return status == ConsoleRunStatus.Failed ? 1 : 0;
