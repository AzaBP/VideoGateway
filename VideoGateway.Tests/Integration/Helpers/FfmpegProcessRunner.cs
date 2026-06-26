using System.Diagnostics;

namespace VideoGateway.Tests.Integration.Helpers;

public static class FfmpegProcessRunner
{
    public static (int exitCode, string stderr, TimeSpan duration) Run(string arguments, TimeSpan timeout)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start ffmpeg");
        var stderrTask = process.StandardError.ReadToEndAsync();
        var sw = Stopwatch.StartNew();

        if (!process.WaitForExit((int)timeout.TotalMilliseconds))
        {
            try { process.Kill(); } catch { }
            return (-1, "Process timed out", sw.Elapsed);
        }

        sw.Stop();
        var stderr = stderrTask.Result;
        return (process.ExitCode, stderr, sw.Elapsed);
    }

    public static bool IsAvailable()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = "-version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (p == null) return false;
            p.WaitForExit(5000);
            return p.ExitCode == 0;
        }
        catch { return false; }
    }
}
