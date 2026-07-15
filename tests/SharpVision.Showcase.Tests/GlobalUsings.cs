// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

global using System.Diagnostics;
global using System.Text;
global using System.Threading.Channels;

global using SharpVision.Controls;
global using SharpVision.Layout;
global using SharpVision.Runtime;
global using SharpVision.Showcase.Controls;
global using SharpVision.Showcase.Panes;
global using SharpVision.Showcase.Tests.Support;
global using SharpVision.Styling;
global using SharpVision.Terminal.Capabilities;
global using SharpVision.Terminal.Geometry;
global using SharpVision.Terminal.Protocols;
global using SharpVision.Terminal.Rendering;
global using SharpVision.Terminal.Runtime;
global using SharpVision.Terminal.Transport;
global using SharpVision.Text;

global using Shouldly;

global using ControlText = SharpVision.Controls.Text;
global using TerminalOptions = SharpVision.Terminal.Runtime.Options;
