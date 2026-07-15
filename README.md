# WinUI Windowing Test Bench

This is a little WinUI 3 app for poking at Windows and AppWindow features by hand.
Think of it as a control panel with a bunch of buttons. You click a button, and a
separate "Target Window" reacts. You watch what happens.

It's a test bench, not a product. The whole point is to try windowing APIs live and
see how they behave.

<img width="1778" height="858" alt="image" src="https://github.com/user-attachments/assets/4dac76c3-a11c-41c9-b9c3-7a29f06a2518" />

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
  Width/Height and min/max size constraints, extend content into the title bar, activate,
  close.
- **AppWindow size and position** (`Microsoft.UI.Windowing.AppWindow`): Move, Resize,
  ResizeClient, MoveAndResize, and quick "snap to corner / center" buttons that use the
  DisplayArea work area.
- **Presenters**: switch between Overlapped, CompactOverlay, and FullScreen. For the
  overlapped presenter you can flip IsAlwaysOnTop, IsResizable, IsMaximizable,
  IsMinimizable, IsModal, HasBorder, HasTitleBar, and set min/max size constraints.
- **Z-order**: move to top/bottom, spawn a sibling window, and test relative ordering
  (MoveInZOrderBelow) and always-on-top behavior.
- **Visibility**: Show, Hide, hide-then-show-after-2s, and toggle IsShownInSwitchers.

The panel also shows which Windows App Runtime the app is actually **running against** -
handy to confirm which runtime a given build ended up using.

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

## Turning on in-progress SDK features

Some Windows App SDK APIs aren't in the public SDK yet. The code that uses them is guarded
with `#if` so the app always builds against the public SDK. Each feature is a build flag:
an MSBuild property whose name is *identical* to the C# `#if` symbol it turns on, so there's
nothing to map.

The `SupportWindowWidthHeight` flag enables the XAML `Window.Width` / `Window.Height`
APIs (WASDK 3.0+), while `SupportWindowMinMaxSize` enables `Window.MinWidth`,
`Window.MinHeight`, `Window.MaxWidth`, and `Window.MaxHeight`. Both are off by default.
Turn them on for a build whose Windows App SDK package contains the APIs:

```
dotnet build -p:SupportWindowWidthHeight=true -p:SupportWindowMinMaxSize=true
```

In code you guard the feature like this:

```csharp
#if SupportWindowWidthHeight
    window.Width = 800;   // not in the public SDK yet
#endif
```

To add a new feature flag later, copy the one line in `MyApp.csproj` (under "Feature flags")
and rename it. If the API needs a newer SDK, bump the `Microsoft.WindowsAppSDK` package
version too.

## Using a local or internal Windows App SDK build

The checked-in project uses the public `Microsoft.WindowsAppSDK` 2.2.0 package from
nuget.org.

### Microsoft internal setup

Run the setup script with the root of your local WinUI repository:

```powershell
.\Setup-InternalBuild.ps1 -WinUIRepoRoot C:\src\microsoft-ui-xaml
```

The script reads `NuGet.config` from that repository and copies its
`Project.Reunion.nuget.internal` source into an ignored `MyApp.local.props`. It also uses
the repo's `PackageStore` directory whenever that directory exists. To replace an
existing setup, add `-Force`.

The script does not select a Windows App SDK package or enable feature flags. Choose the
package and version in Visual Studio, then enable the flags needed by that build.

The internal feed URL is never stored in this public repository. It exists only in the
local WinUI checkout and the generated ignored props file, so public restores never
contact it.

### Manual or custom setup

To use different package sources, create an ignored `MyApp.local.props` file next to
`MyApp.csproj`:

```xml
<Project>
  <PropertyGroup>
    <WinUIRepoRoot>C:\path\to\microsoft-ui-xaml</WinUIRepoRoot>
    <RestoreAdditionalProjectSources>
      https://your-private-feed/v3/index.json
    </RestoreAdditionalProjectSources>
    <RestoreAdditionalProjectSources Condition="Exists('$(WinUIRepoRoot)\PackageStore')">
      $(WinUIRepoRoot)\PackageStore;$(RestoreAdditionalProjectSources)
    </RestoreAdditionalProjectSources>
  </PropertyGroup>
</Project>
```

Because `*.local.props` is ignored, each developer can select local package stores or
private feeds without making public restores contact those feeds.

## Project map

```
MyApp/
  App.xaml(.cs)            app startup, opens MainWindow
  MainWindow.xaml(.cs)     the control panel (all the buttons live here)
  TargetWindow.xaml(.cs)   the window everything gets applied to
  WindowIconHelper.cs      sets the little-window icon on each window
  WindowsAppSdkInfo.cs     reports the running-against runtime version
  MyApp.csproj             build config, arch default, feature flags
```

Have fun breaking windows.
