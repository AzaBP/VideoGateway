using System;
using System.IO;
using System.Windows.Forms;
using LibVLCSharp.Shared;

namespace VideoGateway.SubscriberUI
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VideoGatewayTesting", "startup.log");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(logPath) ?? ".");

                AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                {
                    try { File.AppendAllText(logPath, $"UnhandledException: {e.ExceptionObject}\n"); } catch { }
                };
                TaskScheduler.UnobservedTaskException += (s, e) =>
                {
                    try { File.AppendAllText(logPath, $"UnobservedTaskException: {e.Exception}\n"); } catch { }
                };

                Core.Initialize();
                ApplicationConfiguration.Initialize();
                Application.Run(new SubscriberForm());
            }
            catch (Exception ex)
            {
                try { File.AppendAllText(logPath, $"StartupException: {ex}\n"); } catch { }
                throw;
            }
        }
    }
}
