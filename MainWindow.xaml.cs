using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;

namespace MyApp
{
    /// <summary>
    /// A test bench window that exercises a large surface of the
    /// <see cref="Microsoft.UI.Xaml.Window"/> and <see cref="Microsoft.UI.Windowing.AppWindow"/>
    /// APIs, with an emphasis on positioning, size, z-order, presenters and visibility.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private readonly DispatcherTimer _liveStateTimer = new();
        private TargetWindow? _targetWindow;
        private Window? _siblingWindow;
        private bool _isSyncingControls;
        private bool _isShuttingDown;

        public MainWindow()
        {
            InitializeComponent();

            Title = "Window / AppWindow Test Bench (Control Panel)";

            // Give the control panel window its own little-window icon (title bar + taskbar).
            WindowIconHelper.Apply(this);

            // Surface which Windows App Runtime is actually loaded.
            RuntimeVersionText.Text = WindowsAppSdkInfo.RuntimeVersion;

            ConfigureWindowSizeAvailability();

            // Size the control panel itself. AppWindow.Resize always exists, so use it
            // here regardless of channel (the Window.Width/Height API is what we test).
            base.AppWindow.Resize(new SizeInt32(1180, 820));

            // The control panel stays put; closing it shuts the whole bench down.
            Closed += OnControlPanelClosed;

            // Light periodic refresh so manual drag/resize of the target is reflected.
            _liveStateTimer.Interval = TimeSpan.FromSeconds(1);
            _liveStateTimer.Tick += (_, _) => UpdateLiveState();
            _liveStateTimer.Start();

            // Create the secondary window that every control operates on.
            EnsureTargetWindow();

            Log("Test bench ready.");
        }

        /// <summary>
        /// All controls operate on the target window. This hides the inherited
        /// <see cref="Window.AppWindow"/> so the existing handlers transparently drive the
        /// secondary window; use <c>base.AppWindow</c> to reach the control panel's own window.
        /// </summary>
        private new AppWindow AppWindow => EnsureTargetWindow().AppWindow;

        /// <summary>
        /// Creates the secondary target window if needed, wires up its events and activates it.
        /// </summary>
        private TargetWindow EnsureTargetWindow()
        {
            if (_targetWindow is not null)
            {
                return _targetWindow;
            }

            var target = new TargetWindow();
            _targetWindow = target;

            target.Activated += OnWindowActivated;
            target.Closed += OnTargetWindowClosed;
            target.SizeChanged += OnWindowSizeChanged;
            target.VisibilityChanged += OnWindowVisibilityChanged;

            target.AppWindow.Changed += OnAppWindowChanged;
            target.AppWindow.Closing += OnAppWindowClosing;
            target.AppWindow.Destroying += OnAppWindowDestroying;

            // Offset from the control panel so both windows are visible at once.
            PointInt32 panelPosition = base.AppWindow.Position;
            target.AppWindow.Move(new PointInt32(panelPosition.X + 140, panelPosition.Y + 140));

            target.Activate();
            Log("Target window created.");

            SyncControlsFromState();
            UpdateLiveState();
            return target;
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private OverlappedPresenter? GetOverlappedPresenter()
        {
            if (AppWindow.Presenter is OverlappedPresenter overlapped)
            {
                return overlapped;
            }

            Log($"Current presenter is '{AppWindow.Presenter.Kind}', not OverlappedPresenter. Switch to Overlapped first.");
            return null;
        }

        private static int ReadInt(NumberBox box, int fallback = 0)
        {
            return double.IsNaN(box.Value) ? fallback : (int)Math.Round(box.Value);
        }

        private static string FormatDip(double? value)
        {
            return value.HasValue ? value.Value.ToString("0.##") : "n/a";
        }

        private static string WindowSizeUnavailableMessage =>
            "Window.Width / Window.Height aren't compiled into this build. They're guarded " +
            "by the SupportWindowWidthHeight feature flag (see MyApp.csproj), since those " +
            "APIs aren't in the public Windows App SDK yet.";

        // The only places that touch the Window.Width / Height properties, which aren't in
        // the public Windows App SDK yet. They're compiled in only behind the
        // SupportWindowWidthHeight feature flag so the app still builds against public SDKs.
        private static double? GetXamlWidth(Window window)
        {
#if SupportWindowWidthHeight
            return window.Width;
#else
            _ = window;
            return null;
#endif
        }

        private static double? GetXamlHeight(Window window)
        {
#if SupportWindowWidthHeight
            return window.Height;
#else
            _ = window;
            return null;
#endif
        }

        private static void SetXamlSize(Window window, int width, int height)
        {
#if SupportWindowWidthHeight
            window.Width = width;
            window.Height = height;
#else
            _ = (window, width, height);
#endif
        }

        /// <summary>
        /// Reflects whether the XAML Window.Width / Height API is available: when it is not,
        /// disables the size controls, shows an explanatory InfoBar, and adds a tooltip.
        /// </summary>
        private void ConfigureWindowSizeAvailability()
        {
            bool available = WindowsAppSdkInfo.IsXamlWindowSizeApiAvailable;

            WindowSizeUnavailableInfo.IsOpen = !available;
            WinWidthBox.IsEnabled = available;
            WinHeightBox.IsEnabled = available;
            SetWindowSizeButton.IsEnabled = available;

            if (!available)
            {
                WindowSizeUnavailableInfo.Message = WindowSizeUnavailableMessage;
                ToolTipService.SetToolTip(WindowSizePanel, WindowSizeUnavailableMessage);
                // A tooltip on a disabled button is suppressed, so put it on the wrapper too.
                ToolTipService.SetToolTip(SetWindowSizeButton, WindowSizeUnavailableMessage);
            }
        }

        private void Log(string message)
        {
            string line = $"{DateTime.Now:HH:mm:ss.fff}  {message}";
            Debug.WriteLine(line);

            if (DispatcherQueue is not null && !DispatcherQueue.HasThreadAccess)
            {
                DispatcherQueue.TryEnqueue(() => AddLog(line));
            }
            else
            {
                AddLog(line);
            }
        }

        private void AddLog(string line)
        {
            if (_isShuttingDown)
            {
                return;
            }

            EventLogList.Items.Insert(0, line);
            while (EventLogList.Items.Count > 200)
            {
                EventLogList.Items.RemoveAt(EventLogList.Items.Count - 1);
            }
        }

        private void UpdateLiveState()
        {
            if (_isShuttingDown)
            {
                return;
            }

            if (_targetWindow is null)
            {
                LiveStateText.Text = "(no target window)\r\nUse \"Create / Activate Target Window\" to spawn one.";
                return;
            }

            Window target = _targetWindow;
            AppWindow appWindow = _targetWindow.AppWindow;
            var sb = new System.Text.StringBuilder();

            sb.AppendLine("[Target Xaml Window]");
            sb.AppendLine($"  Title                 : {target.Title}");
            sb.AppendLine($"  Width x Height        : {FormatDip(GetXamlWidth(target))} x {FormatDip(GetXamlHeight(target))}");
            sb.AppendLine($"  Bounds (W x H)        : {target.Bounds.Width:0.##} x {target.Bounds.Height:0.##}");
            sb.AppendLine($"  Visible               : {target.Visible}");
            sb.AppendLine($"  ExtendsContentIntoTitleBar : {target.ExtendsContentIntoTitleBar}");
            sb.AppendLine();

            sb.AppendLine("[Target AppWindow]");
            sb.AppendLine($"  Id                    : 0x{appWindow.Id.Value:X}");
            sb.AppendLine($"  Position (X, Y)       : {appWindow.Position.X}, {appWindow.Position.Y}");
            sb.AppendLine($"  Size (W x H)          : {appWindow.Size.Width} x {appWindow.Size.Height}");
            sb.AppendLine($"  ClientSize (W x H)    : {appWindow.ClientSize.Width} x {appWindow.ClientSize.Height}");
            sb.AppendLine($"  IsVisible             : {appWindow.IsVisible}");
            sb.AppendLine($"  IsShownInSwitchers    : {appWindow.IsShownInSwitchers}");
            sb.AppendLine();

            sb.AppendLine("[DWM Extended Frame Bounds]");
            if (TryGetExtendedFrameBounds(out RectInt32 frameBounds))
            {
                sb.AppendLine($"  Bounds (X,Y,W,H)      : {frameBounds.X}, {frameBounds.Y}, {frameBounds.Width}, {frameBounds.Height}");

                // Difference vs AppWindow.Position/Size (GetWindowRect) reveals the
                // invisible resize borders / drop shadow that DWM excludes.
                int left = frameBounds.X - appWindow.Position.X;
                int top = frameBounds.Y - appWindow.Position.Y;
                int right = (appWindow.Position.X + appWindow.Size.Width) - (frameBounds.X + frameBounds.Width);
                int bottom = (appWindow.Position.Y + appWindow.Size.Height) - (frameBounds.Y + frameBounds.Height);
                sb.AppendLine($"  Invisible inset (L,T,R,B): {left}, {top}, {right}, {bottom}");
            }
            else
            {
                sb.AppendLine("  (unavailable)");
            }
            sb.AppendLine();

            sb.AppendLine("[Presenter]");
            sb.AppendLine($"  Kind                  : {appWindow.Presenter.Kind}");
            if (appWindow.Presenter is OverlappedPresenter op)
            {
                sb.AppendLine($"  State                 : {op.State}");
                sb.AppendLine($"  IsAlwaysOnTop         : {op.IsAlwaysOnTop}");
                sb.AppendLine($"  IsResizable           : {op.IsResizable}");
                sb.AppendLine($"  IsMaximizable         : {op.IsMaximizable}");
                sb.AppendLine($"  IsMinimizable         : {op.IsMinimizable}");
                sb.AppendLine($"  IsModal               : {op.IsModal}");
                sb.AppendLine($"  HasBorder             : {op.HasBorder}");
                sb.AppendLine($"  HasTitleBar           : {op.HasTitleBar}");
                sb.AppendLine($"  PreferredMin (W x H)  : {op.PreferredMinimumWidth?.ToString() ?? "-"} x {op.PreferredMinimumHeight?.ToString() ?? "-"}");
                sb.AppendLine($"  PreferredMax (W x H)  : {op.PreferredMaximumWidth?.ToString() ?? "-"} x {op.PreferredMaximumHeight?.ToString() ?? "-"}");
            }
            sb.AppendLine();

            try
            {
                DisplayArea area = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Nearest);
                sb.AppendLine("[DisplayArea (Nearest)]");
                sb.AppendLine($"  WorkArea (X,Y,W,H)    : {area.WorkArea.X}, {area.WorkArea.Y}, {area.WorkArea.Width}, {area.WorkArea.Height}");
                sb.AppendLine($"  OuterBounds (X,Y,W,H) : {area.OuterBounds.X}, {area.OuterBounds.Y}, {area.OuterBounds.Width}, {area.OuterBounds.Height}");
                sb.AppendLine($"  IsPrimary             : {area.IsPrimary}");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"[DisplayArea] error: {ex.Message}");
            }

            LiveStateText.Text = sb.ToString();
        }

        /// <summary>
        /// Pushes the current window/presenter state into the interactive controls
        /// without re-triggering their change handlers.
        /// </summary>
        private void SyncControlsFromState()
        {
            if (_targetWindow is null)
            {
                return;
            }

            Window target = _targetWindow;
            AppWindow appWindow = _targetWindow.AppWindow;

            _isSyncingControls = true;
            try
            {
                WindowTitleText.Text = target.Title;
                WinWidthBox.Value = GetXamlWidth(target) ?? appWindow.Size.Width;
                WinHeightBox.Value = GetXamlHeight(target) ?? appWindow.Size.Height;
                ExtendContentToggle.IsOn = target.ExtendsContentIntoTitleBar;

                PosXBox.Value = appWindow.Position.X;
                PosYBox.Value = appWindow.Position.Y;
                SizeWBox.Value = appWindow.Size.Width;
                SizeHBox.Value = appWindow.Size.Height;
                ClientWBox.Value = appWindow.ClientSize.Width;
                ClientHBox.Value = appWindow.ClientSize.Height;
                ShownInSwitchersToggle.IsOn = appWindow.IsShownInSwitchers;

                PresenterCombo.SelectedIndex = appWindow.Presenter.Kind switch
                {
                    AppWindowPresenterKind.CompactOverlay => 1,
                    AppWindowPresenterKind.FullScreen => 2,
                    _ => 0,
                };

                if (appWindow.Presenter is OverlappedPresenter op)
                {
                    AlwaysOnTopToggle.IsOn = op.IsAlwaysOnTop;
                    ResizableToggle.IsOn = op.IsResizable;
                    MaximizableToggle.IsOn = op.IsMaximizable;
                    MinimizableToggle.IsOn = op.IsMinimizable;
                    ModalToggle.IsOn = op.IsModal;
                    BorderToggle.IsOn = op.HasBorder;
                    TitleBarToggle.IsOn = op.HasTitleBar;
                    MinWBox.Value = op.PreferredMinimumWidth ?? 0;
                    MinHBox.Value = op.PreferredMinimumHeight ?? 0;
                    MaxWBox.Value = op.PreferredMaximumWidth ?? 0;
                    MaxHBox.Value = op.PreferredMaximumHeight ?? 0;
                }
            }
            finally
            {
                _isSyncingControls = false;
            }
        }

        // ------------------------------------------------------------------
        // Xaml Window
        // ------------------------------------------------------------------

        private void SetTitle_Click(object sender, RoutedEventArgs e)
        {
            TargetWindow target = EnsureTargetWindow();
            target.Title = WindowTitleText.Text;
            Log($"Target Window.Title = \"{target.Title}\"");
            UpdateLiveState();
        }

        private void SetWindowSize_Click(object sender, RoutedEventArgs e)
        {
            if (!WindowsAppSdkInfo.IsXamlWindowSizeApiAvailable)
            {
                Log(WindowSizeUnavailableMessage);
                return;
            }

            TargetWindow target = EnsureTargetWindow();
            int width = ReadInt(WinWidthBox);
            int height = ReadInt(WinHeightBox);
            SetXamlSize(target, width, height);
            Log($"Target Window.Width/Height = {width} x {height}");
            UpdateLiveState();
        }

        private void ExtendContentToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isSyncingControls)
            {
                return;
            }

            TargetWindow target = EnsureTargetWindow();
            target.ExtendsContentIntoTitleBar = ExtendContentToggle.IsOn;
            Log($"Target Window.ExtendsContentIntoTitleBar = {target.ExtendsContentIntoTitleBar}");
            UpdateLiveState();
        }

        private void CreateTarget_Click(object sender, RoutedEventArgs e)
        {
            EnsureTargetWindow().Activate();
        }

        private void Activate_Click(object sender, RoutedEventArgs e)
        {
            var dt = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(3000) };
            dt.Tick += (s, args) =>
            {
                Log("Target Window.Activate()");
                EnsureTargetWindow().Activate();
                dt.Stop();
            };
            dt.Start();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            if (_targetWindow is null)
            {
                Log("No target window to close.");
                return;
            }

            Log("Target Window.Close()");
            _targetWindow.Close();
        }

        // ------------------------------------------------------------------
        // AppWindow size & position
        // ------------------------------------------------------------------

        private void Move_Click(object sender, RoutedEventArgs e)
        {
            var point = new PointInt32(ReadInt(PosXBox), ReadInt(PosYBox));
            AppWindow.Move(point);
            Log($"AppWindow.Move({point.X}, {point.Y})");
        }

        private void Resize_Click(object sender, RoutedEventArgs e)
        {
            var size = new SizeInt32(ReadInt(SizeWBox), ReadInt(SizeHBox));
            AppWindow.Resize(size);
            Log($"AppWindow.Resize({size.Width} x {size.Height})");
        }

        private void ResizeClient_Click(object sender, RoutedEventArgs e)
        {
            var size = new SizeInt32(ReadInt(ClientWBox), ReadInt(ClientHBox));
            AppWindow.ResizeClient(size);
            Log($"AppWindow.ResizeClient({size.Width} x {size.Height})");
        }

        private void MoveAndResize_Click(object sender, RoutedEventArgs e)
        {
            var rect = new RectInt32(ReadInt(PosXBox), ReadInt(PosYBox), ReadInt(SizeWBox), ReadInt(SizeHBox));
            AppWindow.MoveAndResize(rect);
            Log($"AppWindow.MoveAndResize(X={rect.X}, Y={rect.Y}, W={rect.Width}, H={rect.Height})");
        }

        private void PopulateFields_Click(object sender, RoutedEventArgs e)
        {
            SyncControlsFromState();
            Log("Populated input fields from current AppWindow state.");
        }

        private void MoveToCorner_Click(object sender, RoutedEventArgs e)
        {
            string corner = (sender as FrameworkElement)?.Tag as string ?? "TopLeft";
            DisplayArea area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest);
            RectInt32 work = area.WorkArea;
            SizeInt32 size = AppWindow.Size;

            int x = work.X;
            int y = work.Y;
            switch (corner)
            {
                case "TopRight":
                    x = work.X + work.Width - size.Width;
                    y = work.Y;
                    break;
                case "Center":
                    x = work.X + (work.Width - size.Width) / 2;
                    y = work.Y + (work.Height - size.Height) / 2;
                    break;
                case "BottomLeft":
                    x = work.X;
                    y = work.Y + work.Height - size.Height;
                    break;
                case "BottomRight":
                    x = work.X + work.Width - size.Width;
                    y = work.Y + work.Height - size.Height;
                    break;
            }

            AppWindow.Move(new PointInt32(x, y));
            Log($"AppWindow.Move to {corner} -> ({x}, {y})");
        }

        // ------------------------------------------------------------------
        // Presenter
        // ------------------------------------------------------------------

        private void ApplyPresenter_Click(object sender, RoutedEventArgs e)
        {
            string tag = (PresenterCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "Overlapped";
            AppWindowPresenterKind kind = tag switch
            {
                "CompactOverlay" => AppWindowPresenterKind.CompactOverlay,
                "FullScreen" => AppWindowPresenterKind.FullScreen,
                _ => AppWindowPresenterKind.Overlapped,
            };

            AppWindow.SetPresenter(kind);
            Log($"AppWindow.SetPresenter({kind})");
            SyncControlsFromState();
        }

        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            if (GetOverlappedPresenter() is { } presenter)
            {
                presenter.Maximize();
                Log("OverlappedPresenter.Maximize()");
                SyncControlsFromState();
            }
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            if (GetOverlappedPresenter() is { } presenter)
            {
                presenter.Minimize();
                Log("OverlappedPresenter.Minimize()");
            }
        }

        private void Restore_Click(object sender, RoutedEventArgs e)
        {
            if (GetOverlappedPresenter() is { } presenter)
            {
                presenter.Restore();
                Log("OverlappedPresenter.Restore()");
                SyncControlsFromState();
            }
        }

        private void AlwaysOnTopToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isSyncingControls)
            {
                return;
            }

            if (GetOverlappedPresenter() is { } presenter)
            {
                presenter.IsAlwaysOnTop = AlwaysOnTopToggle.IsOn;
                Log($"OverlappedPresenter.IsAlwaysOnTop = {presenter.IsAlwaysOnTop}");
            }
        }

        private void ResizableToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isSyncingControls)
            {
                return;
            }

            if (GetOverlappedPresenter() is { } presenter)
            {
                presenter.IsResizable = ResizableToggle.IsOn;
                Log($"OverlappedPresenter.IsResizable = {presenter.IsResizable}");
            }
        }

        private void MaximizableToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isSyncingControls)
            {
                return;
            }

            if (GetOverlappedPresenter() is { } presenter)
            {
                presenter.IsMaximizable = MaximizableToggle.IsOn;
                Log($"OverlappedPresenter.IsMaximizable = {presenter.IsMaximizable}");
            }
        }

        private void MinimizableToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isSyncingControls)
            {
                return;
            }

            if (GetOverlappedPresenter() is { } presenter)
            {
                presenter.IsMinimizable = MinimizableToggle.IsOn;
                Log($"OverlappedPresenter.IsMinimizable = {presenter.IsMinimizable}");
            }
        }

        private void ModalToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isSyncingControls)
            {
                return;
            }

            if (GetOverlappedPresenter() is { } presenter)
            {
                presenter.IsModal = ModalToggle.IsOn;
                Log($"OverlappedPresenter.IsModal = {presenter.IsModal}");
            }
        }

        private void BorderOrTitleBar_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isSyncingControls)
            {
                return;
            }

            if (GetOverlappedPresenter() is { } presenter)
            {
                bool hasTitleBar = TitleBarToggle.IsOn;
                bool hasBorder = BorderToggle.IsOn || hasTitleBar; // A title bar requires a border.

                presenter.SetBorderAndTitleBar(hasBorder, hasTitleBar);
                Log($"OverlappedPresenter.SetBorderAndTitleBar(hasBorder: {hasBorder}, hasTitleBar: {hasTitleBar})");
                SyncControlsFromState();
            }
        }

        private void ApplyConstraints_Click(object sender, RoutedEventArgs e)
        {
            if (GetOverlappedPresenter() is { } presenter)
            {
                presenter.PreferredMinimumWidth = ReadInt(MinWBox) > 0 ? ReadInt(MinWBox) : null;
                presenter.PreferredMinimumHeight = ReadInt(MinHBox) > 0 ? ReadInt(MinHBox) : null;
                presenter.PreferredMaximumWidth = ReadInt(MaxWBox) > 0 ? ReadInt(MaxWBox) : null;
                presenter.PreferredMaximumHeight = ReadInt(MaxHBox) > 0 ? ReadInt(MaxHBox) : null;
                Log($"Constraints -> Min({MinWBox.Value:0} x {MinHBox.Value:0}) Max({MaxWBox.Value:0} x {MaxHBox.Value:0})");
                UpdateLiveState();
            }
        }

        // ------------------------------------------------------------------
        // Z-order
        // ------------------------------------------------------------------

        private void BringToTop_Click(object sender, RoutedEventArgs e)
        {
            AppWindow.MoveInZOrderAtTop();
            Log("AppWindow.MoveInZOrderAtTop()");
        }

        private void SendToBottom_Click(object sender, RoutedEventArgs e)
        {
            AppWindow.MoveInZOrderAtBottom();
            Log("AppWindow.MoveInZOrderAtBottom()");
        }

        private void SpawnSibling_Click(object sender, RoutedEventArgs e)
        {
            if (_siblingWindow is not null)
            {
                _siblingWindow.Activate();
                Log("Sibling window already exists; activated it.");
                return;
            }

            var sibling = new Window { Title = "Sibling Window" };
            var panel = new StackPanel { Padding = new Thickness(16), Spacing = 8 };
            panel.Children.Add(new TextBlock
            {
                Text = "Sibling window",
                Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"],
            });
            panel.Children.Add(new TextBlock
            {
                Text = "Use the z-order buttons in the control panel to reorder this and the target window.",
                TextWrapping = TextWrapping.Wrap,
            });

            var belowButton = new Button { Content = "MoveInZOrderBelow(target)" };
            belowButton.Click += (_, _) =>
            {
                sibling.AppWindow.MoveInZOrderBelow(AppWindow.Id);
                Log("Sibling.AppWindow.MoveInZOrderBelow(target)");
            };
            panel.Children.Add(belowButton);

            sibling.Content = panel;
            sibling.Closed += (_, _) =>
            {
                _siblingWindow = null;
                Log("Sibling window closed.");
            };

            _siblingWindow = sibling;
            sibling.AppWindow.Resize(new SizeInt32(440, 320));
            sibling.Activate();

            PointInt32 origin = AppWindow.Position;
            sibling.AppWindow.Move(new PointInt32(origin.X + 80, origin.Y + 80));
            Log("Spawned sibling window.");
        }

        private void MoveBelowSibling_Click(object sender, RoutedEventArgs e)
        {
            if (_siblingWindow is null)
            {
                Log("No sibling window. Click 'Spawn Sibling Window' first.");
                return;
            }

            AppWindow.MoveInZOrderBelow(_siblingWindow.AppWindow.Id);
            Log("AppWindow.MoveInZOrderBelow(sibling)");
        }

        private void SiblingToTop_Click(object sender, RoutedEventArgs e)
        {
            if (_siblingWindow is null)
            {
                Log("No sibling window. Click 'Spawn Sibling Window' first.");
                return;
            }

            _siblingWindow.AppWindow.MoveInZOrderAtTop();
            Log("Sibling.AppWindow.MoveInZOrderAtTop()");
        }

        // ------------------------------------------------------------------
        // Visibility
        // ------------------------------------------------------------------

        private void Show_Click(object sender, RoutedEventArgs e)
        {
            AppWindow.Show();
            Log("AppWindow.Show()");
        }

        private void Hide_Click(object sender, RoutedEventArgs e)
        {
            AppWindow.Hide();
            Log("AppWindow.Hide()");
        }

        private void HideTemporarily_Click(object sender, RoutedEventArgs e)
        {
            AppWindow.Hide();
            Log("AppWindow.Hide() for 2s...");

            var timer = DispatcherQueue.CreateTimer();
            timer.Interval = TimeSpan.FromSeconds(2);
            timer.IsRepeating = false;
            timer.Tick += (s, _) =>
            {
                AppWindow.Show();
                Log("AppWindow.Show() (after temporary hide)");
                ((DispatcherQueueTimer)s).Stop();
            };
            timer.Start();
        }

        private void ShownInSwitchersToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isSyncingControls)
            {
                return;
            }

            AppWindow.IsShownInSwitchers = ShownInSwitchersToggle.IsOn;
            Log($"AppWindow.IsShownInSwitchers = {AppWindow.IsShownInSwitchers}");
        }

        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            EventLogList.Items.Clear();
        }

        // ------------------------------------------------------------------
        // Window events
        // ------------------------------------------------------------------

        private void OnWindowActivated(object sender, WindowActivatedEventArgs e)
        {
            Log($"Target Window.Activated -> {e.WindowActivationState}");
        }

        private void OnControlPanelClosed(object sender, WindowEventArgs e)
        {
            _isShuttingDown = true;
            _liveStateTimer.Stop();
            _targetWindow?.Close();
        }

        private void OnTargetWindowClosed(object sender, WindowEventArgs e)
        {
            Log($"Target Window.Closed (Handled={e.Handled})");
            _targetWindow = null;
            UpdateLiveState();
        }

        private void OnWindowSizeChanged(object sender, WindowSizeChangedEventArgs e)
        {
            Log($"Target Window.SizeChanged -> {e.Size.Width:0.##} x {e.Size.Height:0.##}");
            UpdateLiveState();
        }

        private void OnWindowVisibilityChanged(object sender, WindowVisibilityChangedEventArgs e)
        {
            Log($"Target Window.VisibilityChanged -> Visible={e.Visible}");
            UpdateLiveState();
        }

        // ------------------------------------------------------------------
        // AppWindow events
        // ------------------------------------------------------------------

        private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
        {
            var changes = new System.Collections.Generic.List<string>();
            if (args.DidPositionChange)
            {
                changes.Add($"Position={sender.Position.X},{sender.Position.Y}");
            }

            if (args.DidSizeChange)
            {
                changes.Add($"Size={sender.Size.Width}x{sender.Size.Height}");
            }

            if (args.DidVisibilityChange)
            {
                changes.Add($"IsVisible={sender.IsVisible}");
            }

            if (args.DidPresenterChange)
            {
                changes.Add($"Presenter={sender.Presenter.Kind}");
            }

            if (args.DidZOrderChange)
            {
                string z = args.IsZOrderAtTop ? "Top"
                    : args.IsZOrderAtBottom ? "Bottom"
                    : $"Below(0x{args.ZOrderBelowWindowId.Value:X})";
                changes.Add($"ZOrder={z}");
            }

            if (changes.Count > 0)
            {
                Log($"AppWindow.Changed -> {string.Join(", ", changes)}");
                UpdateLiveState();
            }
        }

        private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
        {
            Log($"AppWindow.Closing (Cancel={args.Cancel})");
        }

        private void OnAppWindowDestroying(AppWindow sender, object args)
        {
            Log("AppWindow.Destroying");
        }

        // ------------------------------------------------------------------
        // DWM interop (DWMWA_EXTENDED_FRAME_BOUNDS)
        // ------------------------------------------------------------------

        private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);

        /// <summary>
        /// Reads the DWM extended frame bounds: the visible window rectangle, excluding the
        /// invisible resize borders and drop shadow that <see cref="AppWindow.Position"/> and
        /// <see cref="AppWindow.Size"/> include. Values are in physical pixels.
        /// </summary>
        private bool TryGetExtendedFrameBounds(out RectInt32 bounds)
        {
            bounds = default;

            if (_targetWindow is null)
            {
                return false;
            }

            IntPtr hwnd = Microsoft.UI.Win32Interop.GetWindowFromWindowId(_targetWindow.AppWindow.Id);
            if (hwnd == IntPtr.Zero)
            {
                return false;
            }

            int hr = DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, out RECT rect, Marshal.SizeOf<RECT>());
            if (hr != 0)
            {
                return false;
            }

            bounds = new RectInt32(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
            return true;
        }
    }
}
