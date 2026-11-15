using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace Tagsor
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
                LogCrash("AppDomain", ex.ExceptionObject as Exception);

            DispatcherUnhandledException += (s, ex) =>
            {
                LogCrash("Dispatcher", ex.Exception);
                ex.Handled = true;
            };

            base.OnStartup(e);
        }

        private static void LogCrash(string source, Exception? ex)
        {
            try
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Tagsor");
                Directory.CreateDirectory(folder);
                File.AppendAllText(
                    Path.Combine(folder, "crash.log"),
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ({source})\n{ex}\n\n");
            }
            catch { }
        }
    }
}
