using Xunit;
using VideoGateway.Tests.Integration.Helpers;

namespace VideoGateway.Tests.Integration;

[Trait("Category", "Integration")]
public class FormatDetectionTests : IDisposable
{
    private readonly TempDirHelper _tempDir;

    public FormatDetectionTests()
    {
        _tempDir = new TempDirHelper("FormatDetection");
    }

    public void Dispose() => _tempDir.Dispose();

    [Fact]
    public void Ffprobe_DetectsH264_InMp4()
    {
        var samplesDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Samples");
        var sampleFile = Directory.GetFiles(samplesDir, "*.mp4").First(f => File.ReadAllText(f).Length > 0);

        var codec = FfprobeHelper.DetectCodec(sampleFile);
        Assert.Contains("h264", codec, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Ffprobe_DetectsAv1_InMp4()
    {
        var samplesDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Samples");
        var av1File = Directory.GetFiles(samplesDir, "*.mp4")
            .FirstOrDefault(f => FfprobeHelper.DetectCodec(f) == "av1");

        Assert.NotNull(av1File);
        var codec = FfprobeHelper.DetectCodec(av1File!);
        Assert.Equal("av1", codec);
    }

    [Fact]
    public void Ffprobe_DetectsH265_InMkv()
    {
        var mkvFile = SyntheticFileGenerator.GenerateH265Mkv(_tempDir.DirectoryPath, 2);
        var codec = FfprobeHelper.DetectCodec(mkvFile);
        Assert.Contains(codec, new[] { "hevc", "h265" }, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Ffprobe_DetectsMjpeg_InAvi()
    {
        var aviFile = SyntheticFileGenerator.GenerateMjpegAvi(_tempDir.DirectoryPath, 2);
        var codec = FfprobeHelper.DetectCodec(aviFile);
        Assert.Equal("mjpeg", codec);
    }

    [Fact]
    public void Ffprobe_ReturnsCompleteMetadata_ForAllSamples()
    {
        var samplesDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Samples");
        var sampleFiles = Directory.GetFiles(samplesDir, "*.mp4");

        foreach (var file in sampleFiles)
        {
            var (width, height, fps) = FfprobeHelper.GetStreamInfo(file);
            var duration = FfprobeHelper.GetDuration(file);

            Assert.True(width > 0, $"Width should be > 0 for {Path.GetFileName(file)}");
            Assert.True(height > 0, $"Height should be > 0 for {Path.GetFileName(file)}");
            Assert.True(fps > 0, $"FPS should be > 0 for {Path.GetFileName(file)}");
            Assert.True(duration > 0, $"Duration should be > 0 for {Path.GetFileName(file)}");
        }
    }
}
