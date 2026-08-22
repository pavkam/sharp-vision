// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

global using System.Diagnostics;
global using System.Globalization;
global using System.Runtime.CompilerServices;
global using System.Runtime.InteropServices;
global using System.Text;

global using SharpVision;
global using SharpVision.Controls;
global using SharpVision.Controls.Charts;
global using SharpVision.Controls.Collections;
global using SharpVision.Controls.Display;
global using SharpVision.Controls.Documents;
global using SharpVision.Controls.Input;
global using SharpVision.Controls.Layout;
global using SharpVision.Controls.Scrolling;
global using SharpVision.Dialogs;
global using SharpVision.Fonts;
global using SharpVision.Input;
global using SharpVision.Layout;
global using SharpVision.Menus;
global using SharpVision.Navigation;
global using SharpVision.Popups;
global using SharpVision.Scrolling;
global using SharpVision.Showcase.Controls;
global using SharpVision.Styling;
global using SharpVision.Terminal.Abstractions;
global using SharpVision.Terminal.Capabilities;
global using SharpVision.Terminal.Geometry;
global using SharpVision.Terminal.Input;
global using SharpVision.Terminal.Protocols;
global using SharpVision.Terminal.Rendering;
global using SharpVision.Terminal.Runtime;
global using SharpVision.Terminal.Terminfo;
global using SharpVision.Terminal.Unicode;
global using SharpVision.Terminal.Xterm;
global using SharpVision.Tests.Support;
global using SharpVision.Text;
global using SharpVision.Threading;
global using SharpVision.Windows;

global using Shouldly;

global using ControlText = SharpVision.Controls.Display.Text;
global using KeyAction = SharpVision.Terminal.Input.KeyAction;
global using TerminalStyle = SharpVision.Terminal.Rendering.CellStyle;
global using UiCalendar = SharpVision.Controls.Input.Calendar;
global using UiListView = SharpVision.Controls.Collections.ListView;
