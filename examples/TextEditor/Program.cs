// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

var filePath = args.Length > 0 ? args[0] : null;
var status = await ConsoleApplication.RunAsync(
    new TextEditor.EditorScreen(filePath),
    static builder => builder.TreatControlCAsInput());
return status == ConsoleRunStatus.Failed ? 1 : 0;
