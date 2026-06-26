using System.Diagnostics;
using Xunit;
using VideoGateway.Tests.Integration.Helpers;

namespace VideoGateway.Tests.Integration;

[Trait("Category", "Integration")]
public class EndToEndTests : IDisposable
{
    private readonly TempDirHelper _tempDir;
    private readonly string _samplesDir;
    private readonly RtspServerManager _rtspServer;

    public EndToEndTests()
    {
        _tempDir = new TempDirHelper("E2E");
        _samplesDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Samples");
        _rtspServer = new RtspServerManager();
    }

    public void Dispose()
    {
        _rtspServer.Dispose();
        _tempDir.Dispose();
    }

    [Fact]
    public void E2E_MediaMTX_StartsAndStops()
    {
        _rtspServer.Start();
        Assert.True(_rtspServer.IsRunning);
        _rtspServer.Stop();
        Assert.False(_rtspServer.IsRunning);
    }

    [Fact]
    public void E2E_PublishToRTSP_StreamActive()
    {
        _rtspServer.Start();
        var sample = Directory.GetFiles(_samplesDir, "*.mp4")
            .OrderBy(f => new FileInfo(f).Length)
            .First();

        var psi = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-re -i \"{sample}\" -c:v libx264 -preset ultrafast -tune zerolatency -c:a aac -f rtsp rtsp://127.0.0.1:8554/test_stream -rtsp_transport tcp",
            UseShellExecute = false,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)!;
        Thread.Sleep(3000);

        var isRunning = !process.HasExited;
        if (isRunning) try { process.Kill(); } catch { }

        Assert.True(isRunning, "FFmpeg process should still be running after 3s");
    }

    [Fact]
    public void E2E_PublishH264_CanProbeOutput()
    {
        _rtspServer.Start();
        var sample = Directory.GetFiles(_samplesDir, "*.mp4")
            .First(f => FfprobeHelper.DetectCodec(f) == "h264");

        var psi = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-re -i \"{sample}\" -c:v libx264 -preset ultrafast -tune zerolatency -c:a aac -f rtsp rtsp://127.0.0.1:8554/test_probe -rtsp_transport tcp",
            UseShellExecute = false,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var publishProcess = Process.Start(psi)!;
        Thread.Sleep(3000);

        try
        {
            var probeOutput = FfprobeHelper.RunFfprobe(
                "rtsp://127.0.0.1:8554/test_probe",
                "-rtsp_transport tcp -analyzeduration 2000000 -probesize 2000000");

            Assert.False(string.IsNullOrWhiteSpace(probeOutput), "ffprobe should return output");
        }
        finally
        {
            try { publishProcess.Kill(); } catch { }
        }
    }

    [Fact]
    public void E2E_PublishH264_OutputIsH264()
    {
        _rtspServer.Start();
        var sample = Directory.GetFiles(_samplesDir, "*.mp4")
            .First(f => FfprobeHelper.DetectCodec(f) == "h264");

        var psi = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-re -i \"{sample}\" -c:v libx264 -preset ultrafast -tune zerolatency -c:a aac -f rtsp rtsp://127.0.0.1:8554/test_h264_out -rtsp_transport tcp",
            UseShellExecute = false,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var publishProcess = Process.Start(psi)!;
        Thread.Sleep(3000);

        try
        {
            var codec = FfprobeHelper.DetectCodec("rtsp://127.0.0.1:8554/test_h264_out");
            Assert.Equal("h264", codec);
        }
        finally
        {
            try { publishProcess.Kill(); } catch { }
        }
    }

    [Fact]
    public void E2E_PublishWithTranscode_OutputIsH264()
    {
        _rtspServer.Start();
        var av1Sample = Directory.GetFiles(_samplesDir, "*.mp4")
            .FirstOrDefault(f => FfprobeHelper.DetectCodec(f) == "av1");
        Assert.NotNull(av1Sample);

        var psi = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-re -i \"{av1Sample!}\" -c:v libx264 -preset ultrafast -tune zerolatency -c:a aac -f rtsp rtsp://127.0.0.1:8554/test_av1_to_h264 -rtsp_transport tcp",
            UseShellExecute = false,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var publishProcess = Process.Start(psi)!;
        Thread.Sleep(4000);

        try
        {
            var codec = FfprobeHelper.DetectCodec("rtsp://127.0.0.1:8554/test_av1_to_h264");
            Assert.Equal("h264", codec);
        }
        finally
        {
            try { publishProcess.Kill(); } catch { }
        }
    }

    [Fact]
    public void E2E_PublisherUI_Formats_AllWork()
    {
        _rtspServer.Start();
        var sampleFiles = Directory.GetFiles(_samplesDir, "*.mp4");

        foreach (var file in sampleFiles)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-re -i \"{file}\" -c:v libx264 -preset ultrafast -tune zerolatency -c:a aac -f rtsp rtsp://127.0.0.1:8554/test_all_{Path.GetFileNameWithoutExtension(file)} -rtsp_transport tcp",
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi)!;
            Thread.Sleep(2000);

            var isRunning = !process.HasExited;
            if (isRunning) try { process.Kill(); } catch { }

            Assert.True(isRunning, $"Failed to publish {Path.GetFileName(file)}");
        }
    }
}
