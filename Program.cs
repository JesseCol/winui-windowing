using System;
using System.Threading;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace MyApp
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            WinRT.ComWrappersSupport.InitializeComWrappers();

            /*

            var dqc = DispatcherQueueController.CreateOnCurrentThread();

            var dq = dqc.DispatcherQueue;

            dq.TryEnqueue(() =>
            {
                var aw = AppWindow.Create();
                aw.Show();
            });

            dq.RunEventLoop();
            */


            //Microsoft.UI.Xaml.Settings.XamlOptionalChanges.EnableChange(Microsoft.UI.Xaml.Settings.XamlChangeId.WindowActivateForeground);
            Application.Start(_ =>
            {
                var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                new App();
            });
        }
    }
}
