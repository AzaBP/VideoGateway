using Xunit;
using VideoGateway.Tests.Integration.Helpers;

namespace VideoGateway.Tests.Integration;

[Trait("Category", "Integration")]
public class FfmpegAvailabilityTests
{
    [Fact]
    public void Ffmpeg_IsAvailable()
    {
        Assert.True(FfmpegProcessRunner.IsAvailable(), "ffmpeg is not available in PATH");
    }

    [Fact]
    public void Ffprobe_IsAvailable()
    {
        Assert.True(FfprobeHelper.IsAvailable(), "ffprobe is not available in PATH");
    }

    [Fact]
    public void Ffmpeg_CanShowVersion()
    {
        var (exitCode, stderr, _) = FfmpegProcessRunner.Run("-version", TimeSpan.FromSeconds(5));
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public void MediaMTX_IsInstalled()
    {
        Assert.True(File.Exists(@"C:\tools\mediamtx\mediamtx.exe"), "MediaMTX not found");
    }
}
