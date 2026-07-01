using System;
using System.IO;
using Microsoft.UI.Xaml;

namespace MyApp
{
    /// <summary>
    /// Helper for giving every Xaml <see cref="Window"/> the app's little-window icon
    /// in its title bar and taskbar button.
    ///
    /// This is separate from the tile images in <c>Package.appxmanifest</c>: those only
    /// show for an installed MSIX package (Start menu, Store). A running window's title
    /// bar / taskbar icon comes from the window itself, so we set it explicitly here so
    /// it also shows up when the app runs unpackaged (e.g. "dotnet run").
    /// </summary>
    internal static class WindowIconHelper
    {
        // Copied next to the exe by the build (Content / CopyToOutputDirectory).
        private static readonly string IconPath =
            Path.Combine(AppContext.BaseDirectory, "Assets", "WindowIcon.ico");

        /// <summary>
        /// Sets the little-window icon on the given window's own AppWindow. The parameter
        /// is typed as <see cref="Window"/> so this always reaches the window's real
        /// <see cref="Window.AppWindow"/> (MainWindow hides that member to point at its
        /// target window).
        /// </summary>
        public static void Apply(Window window)
        {
            try
            {
                if (File.Exists(IconPath))
                {
                    window.AppWindow.SetIcon(IconPath);
                }
            }
            catch
            {
                // A missing icon file should never take the app down; just skip it.
            }
        }
    }
}
