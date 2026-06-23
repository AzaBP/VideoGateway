// VideoGateway.Testing.Common - ProcessRunner
// Simple helper utilities to start external processes (ffmpeg, ffprobe, vlc)
// and capture stderr output. Intentionally minimal and synchronous-friendly
// so UI projects can call and display output in their controls.
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace VideoGateway.Testing.Common
{
    /// <summary>
    /// Small helper to start processes and optionally capture stderr in background.
    /// </summary>
    public static class ProcessRunner
    {
        /// <summary>
        /// Start a process and optionally capture its standard error by invoking onError for each line.
        /// Returns the Process instance (caller is responsible for disposing/killing it).
        /// </summary>
        public static Process? StartProcess(string fileName, string arguments, Action<string>? onError = null)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardError = onError != null,
                RedirectStandardOutput = false,
                CreateNoWindow = true
            };

            try
            {
                var p = Process.Start(psi);
                if (p != null && onError != null)
                {
                    Task.Run(() => {
                        try
                        {
                            var se = p.StandardError;
                            while (!se.EndOfStream)
                            {
                                var line = se.ReadLine();
                                if (line == null) break;
                                onError(line);
                            }
                        }
                        catch { }
                    });
                }
                return p;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Open a URL or file with the default associated application using the shell (UseShellExecute = true).
        /// Returns true when the process was started successfully.
        /// </summary>
        public static bool OpenWithDefaultApp(string target)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = target,
                    UseShellExecute = true,
                    CreateNoWindow = true
                };
                Process.Start(psi);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Try to find an executable in PATH using 'where.exe' on Windows. Returns full path or null.
        /// </summary>
        public static string? TryFindExecutable(string exeName)
        {
            try
            {
                // First check if user configured an explicit path
                try
                {
                    var cfg = Config.GetPathFor(exeName);
                    if (!string.IsNullOrEmpty(cfg) && File.Exists(cfg)) return cfg;
                }
                catch { }

                var psi = new ProcessStartInfo
                {
                    FileName = "where",
                    Arguments = exeName,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi);
                if (p == null) return null;
                var outp = p.StandardOutput.ReadLine();
                p.WaitForExit(1000);
                if (!string.IsNullOrWhiteSpace(outp)) return outp.Trim();
            }
            catch { }
            return null;
        }
    }
}
