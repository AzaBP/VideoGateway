using System.Diagnostics;

namespace VideoGateway.Tests.Integration.Helpers;

public static class FfprobeHelper
{
    public static string RunFfprobe(string filePath, string extraArgs = "")
    {
        var psi = new ProcessStartInfo
        {
            FileName = "ffprobe",
            Arguments = $"-v error {extraArgs} \"{filePath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start ffprobe");
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit(10000);
        return output;
    }

    public static string DetectCodec(string filePath)
    {
        var output = RunFfprobe(filePath, "-select_streams v:0 -show_entries stream=codec_name -of default=nw=1:nk=1");
        return output.Trim();
    }

    public static (int width, int height, double fps) GetStreamInfo(string filePath)
    {
        var widthOutput = RunFfprobe(filePath, "-select_streams v:0 -show_entries stream=width -of default=nw=1:nk=1");
        var heightOutput = RunFfprobe(filePath, "-select_streams v:0 -show_entries stream=height -of default=nw=1:nk=1");
        var fpsOutput = RunFfprobe(filePath, "-select_streams v:0 -show_entries stream=r_frame_rate -of default=nw=1:nk=1");

        int.TryParse(widthOutput.Trim(), out int width);
        int.TryParse(heightOutput.Trim(), out int height);

        double fps = 0;
        var fpsStr = fpsOutput.Trim();
        if (fpsStr.Contains('/'))
        {
            var parts = fpsStr.Split('/');
            if (double.TryParse(parts[0], out double num) && double.TryParse(parts[1], out double den) && den > 0)
                fps = num / den;
        }
        else
        {
            double.TryParse(fpsStr, out fps);
        }

        return (width, height, fps);
    }

    public static double GetDuration(string filePath)
    {
        var output = RunFfprobe(filePath, "-show_entries format=duration -of default=nw=1:nk=1");
        double.TryParse(output.Trim(), out double duration);
        return duration;
    }

    public static bool IsAvailable()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "ffprobe",
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
