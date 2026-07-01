using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace MyApp
{
    /// <summary>
    /// A secondary Xaml window that serves as the target for the
    /// <see cref="MainWindow"/> control panel. All positioning, size, z-order,
    /// presenter and visibility operations are applied to this window so the
    /// control panel itself stays stable and usable.
    /// </summary>
    public sealed partial class TargetWindow : Window
    {
        public TargetWindow()
        {
            InitializeComponent();
            Title = "Target Window";

            // Give the target window the same little-window icon (title bar + taskbar).
            WindowIconHelper.Apply(this);

            // The markup no longer carries an initial size, so set one here. AppWindow.Resize
            // always exists, so this works regardless of the SupportWindowWidthHeight flag.
            AppWindow.Resize(new SizeInt32(900, 640));
        }
    }
}
