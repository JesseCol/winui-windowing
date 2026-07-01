using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace MyApp
{
    /// <summary>
    /// Reports the Windows App SDK version this app was <b>built against</b> versus the
    /// Windows App Runtime it is actually <b>running against</b>. These can differ: the
    /// build version is fixed at compile time (the WindowsAppSDKVersion MSBuild property,
    /// embedded as assembly metadata), while the runtime version is whatever framework
    /// package / self-contained runtime is loaded into the process.
    /// </summary>
    internal static class WindowsAppSdkInfo
    {
        /// <summary>
        /// The WASDK version the app was built against, resolved to the concrete version
        /// NuGet picked. If the requested string was a float (e.g. "2.*") that differs
        /// from the resolved version, both are shown as "resolved (requested)".
        /// </summary>
        public static string BuiltAgainstVersion { get; } = ReadBuiltAgainstVersion();

        /// <summary>
        /// The version of the Windows App Runtime actually loaded and in use by the
        /// process, from <c>WindowsAppRuntime.RuntimeInfo.AsString</c>. Falls back to the
        /// loaded native module's file version if that API is unavailable.
        /// </summary>
        public static string RuntimeVersion { get; } = ReadRuntimeVersion();

        /// <summary>
        /// True when the XAML <see cref="Microsoft.UI.Xaml.Window.Width"/> /
        /// <see cref="Microsoft.UI.Xaml.Window.Height"/> properties exist in the Windows
        /// App SDK this app was built against (added in WASDK 3.0). Driven by the
        /// WINAPPSDK_HAS_WINDOW_SIZE compile symbol set from WindowsAppSDKVersion.
        /// </summary>
        public static bool IsXamlWindowSizeApiAvailable =>
#if WINAPPSDK_HAS_WINDOW_SIZE
            true;
#else
            false;
#endif

        private static string ReadBuiltAgainstVersion()
        {
            string requested = ReadMetadata("WindowsAppSDKVersionRequested");
            string resolved = ReadMetadata("WindowsAppSDKVersionResolved");

            if (string.IsNullOrWhiteSpace(resolved))
            {
                return string.IsNullOrWhiteSpace(requested) ? "(unknown)" : requested;
            }

            // Only show the requested string too when it differs (i.e. it was a float).
            return string.IsNullOrWhiteSpace(requested) || requested == resolved
                ? resolved
                : $"{resolved} (requested {requested})";
        }

        private static string ReadMetadata(string key)
        {
            return typeof(WindowsAppSdkInfo).Assembly
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(a => a.Key == key)?
                .Value ?? string.Empty;
        }

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
                // bootstrapper) — fall back to inspecting the loaded native module below.
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
