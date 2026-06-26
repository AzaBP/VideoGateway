namespace VideoGateway.Tests.Integration.Helpers;

public static class SyntheticFileGenerator
{
    public static string Generate(string outputDir, string format, string codec, int width = 320, int height = 240, int durationSeconds = 3)
    {
        var ext = format.ToLowerInvariant() switch
        {
            "h264" => ".h264",
            "h265" or "hevc" => ".h265",
            "mjpeg" => ".mjpeg",
            "rawvideo" => ".raw",
            _ => $".{format}"
        };

        var outputPath = Path.Combine(outputDir, $"test_{format}_{codec}{ext}");

        string codecArgs = codec.ToLowerInvariant() switch
        {
            "libx264" => "-c:v libx264 -preset ultrafast -tune zerolatency",
            "libx265" => "-c:v libx265 -preset ultrafast -tune zerolatency",
            "mjpeg" => "-c:v mjpeg -q:v 5",
            "rawvideo" => $"-c:v rawvideo -pix_fmt bgr24",
            "av1" => "-c:v libaom-av1 -cpu-used 8 -still-picture 0",
            _ => $"-c:v {codec}"
        };

        string formatArgs = format.ToLowerInvariant() switch
        {
            "h264" => "-f h264",
            "h265" or "hevc" => "-f hevc",
            "mjpeg" => "-f image2pipe",
            "rawvideo" => $"-f rawvideo -pix_fmt bgr24 -s {width}x{height}",
            _ => ""
        };

        string sizeArg = format.ToLowerInvariant() is "rawvideo" ? "" : $"-s {width}x{height}";

        var args = $"-y -f lavfi -i testsrc=duration={durationSeconds}:size={width}x{height}:rate=25 {codecArgs} {formatArgs} {sizeArg} \"{outputPath}\"";

        var result = FfmpegProcessRunner.Run(args, TimeSpan.FromSeconds(30));

        if (result.exitCode != 0 || !File.Exists(outputPath))
            throw new InvalidOperationException($"Failed to generate {format} file: {result.stderr}");

        return outputPath;
    }

    public static string GenerateMjpegAvi(string outputDir, int durationSeconds = 3)
    {
        var outputPath = Path.Combine(outputDir, "test_mjpeg.avi");
        var args = $"-y -f lavfi -i testsrc=duration={durationSeconds}:size=320x240:rate=25 -c:v mjpeg -q:v 5 \"{outputPath}\"";
        var result = FfmpegProcessRunner.Run(args, TimeSpan.FromSeconds(30));
        if (result.exitCode != 0 || !File.Exists(outputPath))
            throw new InvalidOperationException($"Failed to generate MJPEG AVI: {result.stderr}");
        return outputPath;
    }

    public static string GenerateH265Mkv(string outputDir, int durationSeconds = 3)
    {
        var outputPath = Path.Combine(outputDir, "test_h265.mkv");
        var args = $"-y -f lavfi -i testsrc=duration={durationSeconds}:size=320x240:rate=25 -c:v libx265 -preset ultrafast -tune zerolatency \"{outputPath}\"";
        var result = FfmpegProcessRunner.Run(args, TimeSpan.FromSeconds(30));
        if (result.exitCode != 0 || !File.Exists(outputPath))
            throw new InvalidOperationException($"Failed to generate H265 MKV: {result.stderr}");
        return outputPath;
    }

    public static string GenerateH264Mov(string outputDir, int durationSeconds = 3)
    {
        var outputPath = Path.Combine(outputDir, "test_h264.mov");
        var args = $"-y -f lavfi -i testsrc=duration={durationSeconds}:size=320x240:rate=25 -c:v libx264 -preset ultrafast -tune zerolatency \"{outputPath}\"";
        var result = FfmpegProcessRunner.Run(args, TimeSpan.FromSeconds(30));
        if (result.exitCode != 0 || !File.Exists(outputPath))
            throw new InvalidOperationException($"Failed to generate H264 MOV: {result.stderr}");
        return outputPath;
    }

    public static string GenerateH264Elementary(string outputDir, int durationSeconds = 3)
    {
        return Generate(outputDir, "h264", "libx264", durationSeconds: durationSeconds);
    }

    public static string GenerateH265Elementary(string outputDir, int durationSeconds = 3)
    {
        return Generate(outputDir, "hevc", "libx265", durationSeconds: durationSeconds);
    }

    public static string GenerateMjpegSequence(string outputDir, int durationSeconds = 3)
    {
        var outputPath = Path.Combine(outputDir, "test_mjpeg.mjpeg");
        var args = $"-y -f lavfi -i testsrc=duration={durationSeconds}:size=320x240:rate=25 -c:v mjpeg -q:v 5 -f mjpeg \"{outputPath}\"";
        var result = FfmpegProcessRunner.Run(args, TimeSpan.FromSeconds(30));
        if (result.exitCode != 0 || !File.Exists(outputPath))
            throw new InvalidOperationException($"Failed to generate MJPEG sequence: {result.stderr}");
        return outputPath;
    }

    public static string GenerateRawVideo(string outputDir, int durationSeconds = 2)
    {
        var outputPath = Path.Combine(outputDir, "test_raw.raw");
        var args = $"-y -f lavfi -i testsrc=duration={durationSeconds}:size=160x120:rate=25 -c:v rawvideo -pix_fmt bgr24 -f rawvideo \"{outputPath}\"";
        var result = FfmpegProcessRunner.Run(args, TimeSpan.FromSeconds(30));
        if (result.exitCode != 0 || !File.Exists(outputPath))
            throw new InvalidOperationException($"Failed to generate raw video: {result.stderr}");
        return outputPath;
    }

    public static byte[] GenerateMjpegFrame(int width = 320, int height = 240)
    {
        using var ms = new MemoryStream();
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-y -f lavfi -i testsrc=size={width}x{height}:rate=1 -vframes 1 -f mjpeg pipe:1",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };
        using var process = System.Diagnostics.Process.Start(psi)!;
        var data = process.StandardOutput.BaseStream;
        data.CopyTo(ms);
        process.WaitForExit(5000);
        return ms.ToArray();
    }
}
