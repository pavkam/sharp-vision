// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.DocumentConsumer;

using SharpVision.Controls.Documents;
using SharpVision.Controls.Input;
using SharpVision.Documents.Markdown;

using DocumentControl = SharpVision.Controls.Documents.Document;

/// <summary>Loads extended Markdown through only the public packed-package surface.</summary>
internal static class Program
{
    private static void Main()
    {
        var document = new DocumentControl();
        _ = document.Load(
            "# Packed\n\n- [x] works",
            new MarkdownDocumentReader(new MarkdownOptions
            {
                Extensions = MarkdownExtension.TaskLists
            }));

        var checkBox = document.Blocks[1].ShouldBeListControl();

        if (document.Blocks[0] is not DocumentHeading || checkBox.IsChecked != true)
        {
            throw new InvalidOperationException("The packed Document Markdown surface did not load correctly.");
        }
    }

    private static CheckBox ShouldBeListControl(this DocumentBlock block)
    {
        if (block is DocumentList list &&
            list.Items[0].Blocks[0] is DocumentBlockControl { Control: CheckBox checkBox })
        {
            return checkBox;
        }

        throw new InvalidOperationException("The packed task-list syntax did not create a checkbox control.");
    }
}
