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

            // On WASDK 3.0+ the initial size comes from the markup (Window.Width/Height
            // in Windows\v3\TargetWindow.xaml). On older versions that markup can't exist,
            // so apply the size from code via AppWindow.Resize.
            if (!WindowsAppSdkInfo.IsXamlWindowSizeApiAvailable)
            {
                AppWindow.Resize(new SizeInt32(900, 640));
            }
        }
    }
}
