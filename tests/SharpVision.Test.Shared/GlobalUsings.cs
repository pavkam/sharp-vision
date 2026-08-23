// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

global using System.Diagnostics;
global using System.Globalization;
global using System.Text;
global using System.Threading.Channels;

global using SharpVision;
global using SharpVision.Controls;
global using SharpVision.Controls.Layout;
global using SharpVision.Input;
global using SharpVision.Layout;
global using SharpVision.Scrolling;
global using SharpVision.Styling;
global using SharpVision.Terminal.Abstractions;
global using SharpVision.Terminal.Geometry;
global using SharpVision.Terminal.Input;
global using SharpVision.Terminal.Protocols;
global using SharpVision.Terminal.Rendering;
global using SharpVision.Terminal.Runtime;
global using SharpVision.Terminal.Unicode;

global using Shouldly;

global using TerminalStyle = SharpVision.Terminal.Rendering.CellStyle;
