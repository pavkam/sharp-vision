// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

global using System.Diagnostics;
global using System.Globalization;
global using System.Runtime.CompilerServices;
global using System.Runtime.InteropServices;
global using System.Text;

global using SharpVision.Controls;
global using SharpVision.Fonts;
global using SharpVision.Input;
global using SharpVision.Layout;
global using SharpVision.Runtime;
global using SharpVision.Scrolling;
global using SharpVision.Styling;
global using SharpVision.Terminal.Capabilities;
global using SharpVision.Terminal.Geometry;
global using SharpVision.Terminal.Input;
global using SharpVision.Terminal.Protocols;
global using SharpVision.Terminal.Rendering;
global using SharpVision.Terminal.Runtime;
global using SharpVision.Terminal.Unicode;
global using SharpVision.Tests.Support;
global using SharpVision.Text;
global using SharpVision.Threading;

global using Shouldly;

global using ControlText = SharpVision.Controls.Text;
global using KeyAction = SharpVision.Terminal.Input.Action;
global using TerminalOptions = SharpVision.Terminal.Runtime.Options;
global using TerminalStyle = SharpVision.Terminal.Rendering.CellStyle;
