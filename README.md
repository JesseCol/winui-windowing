# MyApp - WinUI Windowing Test Bench

This is a little WinUI 3 app for poking at Windows and AppWindow features by hand.
Think of it as a control panel with a bunch of buttons. You click a button, and a
separate "Target Window" reacts. You watch what happens.

It's a test bench, not a product. The whole point is to try windowing APIs live and
see how they behave.

## What it does

The app opens with two windows in mind:

```
  +---------------------------+        drives        +------------------+
  |   Control Panel           |  ------------------> |  Target Window   |
  |   (MainWindow)            |                      |  (the guinea pig)|
  |                           |  <------------------ |                  |
  |   buttons + toggles       |     state + events   |                  |
  +---------------------------+                      +------------------+
```

The control panel stays put so it's always usable. Every knob you turn is applied to
the Target Window instead. The right side of the panel shows the target's live state
plus a running log of Window / AppWindow events.

## Stuff you can play with

- **Xaml Window** (`Microsoft.UI.Xaml.Window`): create/activate, set Title, set
  Width/Height, extend content into the title bar, activate, close.
- **AppWindow size and position** (`Microsoft.UI.Windowing.AppWindow`): Move, Resize,
  ResizeClient, MoveAndResize, and quick "snap to corner / center" buttons that use the
  DisplayArea work area.
- **Presenters**: switch between Overlapped, CompactOverlay, and FullScreen. For the
  overlapped presenter you can flip IsAlwaysOnTop, IsResizable, IsMaximizable,
  IsMinimizable, IsModal, HasBorder, HasTitleBar, and set min/max size constraints.
- **Z-order**: move to top/bottom, spawn a sibling window, and test relative ordering
  (MoveInZOrderBelow) and always-on-top behavior.
- **Visibility**: Show, Hide, hide-then-show-after-2s, and toggle IsShownInSwitchers.

The panel also shows which Windows App SDK version the app was **built against** versus
the Windows App Runtime it's actually **running against**. Those two can differ, and it's
handy to see both.

## Running it

You need the .NET SDK and the WinUI / Windows App SDK workload. Then, from this folder:

```
dotnet run
```

That's it. It defaults to x64 now (see the note below). You can still pick another
architecture if you want:

```
dotnet run -a arm64
dotnet run -a x86
```

### Why "dotnet run" just works

WinUI self-contained builds need a real architecture. A bare `dotnet run` would build
as AnyCPU and fail with "requires a supported Windows architecture". So the csproj sets
a default `RuntimeIdentifier` of `win-x64` when nothing else asks for one:

```xml
<PropertyGroup Condition="'$(RuntimeIdentifier)' == ''">
  <RuntimeIdentifier Condition="'$(Platform)' == 'x64'">win-x64</RuntimeIdentifier>
  <RuntimeIdentifier Condition="'$(Platform)' == 'x86'">win-x86</RuntimeIdentifier>
  <RuntimeIdentifier Condition="'$(Platform)' == 'ARM64'">win-arm64</RuntimeIdentifier>
  <RuntimeIdentifier Condition="'$(RuntimeIdentifier)' == ''">win-x64</RuntimeIdentifier>
</PropertyGroup>
```

Anything explicit still wins. `dotnet run -a arm64` sets the RID itself, and Visual
Studio sets the Platform, so both are honored. The default only kicks in when nobody
else said anything.

## Picking a Windows App SDK version

This project can build against different WASDK versions, and some markup even changes
between them (WASDK 3.0+ adds the XAML `Window.Width` / `Window.Height` APIs). You set
the version with `WindowsAppSDKVersion`, in priority order:

1. The "Windows App SDK" page in Project Properties (VS UI)
2. `-p:WindowsAppSDKVersion=2.2.0` on the command line
3. The `WINAPPSDK_VERSION` environment variable
4. The default in the csproj

Example:

```
dotnet run -p:WindowsAppSDKVersion=2.2.0
```

The leading number is treated as the major version. Major 3 or higher turns on the newer
Window sizing APIs; lower majors compile them out. There are two `TargetWindow.xaml`
variants under `Windows\v2` and `Windows\v3` for exactly this reason, since XAML has no
`#if`.

## Project map

```
MyApp/
  App.xaml(.cs)            app startup, opens MainWindow
  MainWindow.xaml(.cs)     the control panel (all the buttons live here)
  TargetWindow.xaml.cs     the window everything gets applied to
  Windows/v2/, Windows/v3/ version-specific TargetWindow markup
  WindowsAppSdkInfo.cs     reports built-against vs running-against versions
  MyApp.csproj             build config, arch default, WASDK version logic
```

Have fun breaking windows.
