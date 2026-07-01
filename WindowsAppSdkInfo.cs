using System;
using System.Diagnostics;
using System.Linq;

namespace MyApp
{
    /// <summary>
    /// Reports the Windows App Runtime version the app is actually <b>running against</b>:
    /// whatever framework package / self-contained runtime is loaded into the process.
    /// Handy to confirm which runtime a given build ended up using.
    /// </summary>
    internal static class WindowsAppSdkInfo
    {
        /// <summary>
        /// The version of the Windows App Runtime actually loaded and in use by the
        /// process, from <c>WindowsAppRuntime.RuntimeInfo.AsString</c>. Falls back to the
        /// loaded native module's file version if that API is unavailable.
        /// </summary>
        public static string RuntimeVersion { get; } = ReadRuntimeVersion();

        /// <summary>
        /// True when the XAML <see cref="Microsoft.UI.Xaml.Window.Width"/> /
        /// <see cref="Microsoft.UI.Xaml.Window.Height"/> code API is compiled in. Driven by
        /// the SupportWindowWidthHeight feature flag in MyApp.csproj (off by default, since
        /// those APIs aren't in the public Windows App SDK yet).
        /// </summary>
        public static bool IsXamlWindowSizeApiAvailable =>
#if SupportWindowWidthHeight
            true;
#else
            false;
#endif

        private static string ReadRuntimeVersion()
        {
            // Preferred: the Windows App SDK runtime-version API. RuntimeInfo reports the
            // runtime actually loaded and in use by the process (available on WASDK 1.2+,
            // so on both the v2 and v3 packages this app can build against).
            try
            {
                string asString = Microsoft.Windows.ApplicationModel.WindowsAppRuntime.RuntimeInfo.AsString;
                if (!string.IsNullOrWhiteSpace(asString))
                {
                    return asString.Trim();
                }
            }
            catch
            {
                // API unavailable or runtime not initialized (e.g. unpackaged without the
                // bootstrapper) - fall back to inspecting the loaded native module below.
            }

            // Fallback: the Windows App SDK loads native modules into the process; the
            // runtime DLL carries a (coarser) version in its file version info.
            foreach (ProcessModule module in Process.GetCurrentProcess().Modules.Cast<ProcessModule>())
            {
                string name = module.ModuleName;
                if (name.StartsWith("Microsoft.WindowsAppRuntime", StringComparison.OrdinalIgnoreCase) ||
                    name.StartsWith("Microsoft.UI.Xaml", StringComparison.OrdinalIgnoreCase))
                {
                    FileVersionInfo info = module.FileVersionInfo;
                    string version = info.ProductVersion ?? info.FileVersion ?? "";
                    if (!string.IsNullOrWhiteSpace(version))
                    {
                        return $"{version.Trim()} ({name})";
                    }
                }
            }

            return "(runtime module not found)";
        }
    }
}
