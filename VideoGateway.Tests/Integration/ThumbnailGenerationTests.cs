using Xunit;
using VideoGateway.Tests.Integration.Helpers;

namespace VideoGateway.Tests.Integration;

[Trait("Category", "Integration")]
public class ThumbnailGenerationTests : IDisposable
{
    private readonly TempDirHelper _tempDir;
    private readonly string _samplesDir;

    public ThumbnailGenerationTests()
    {
        _tempDir = new TempDirHelper("Thumbnails");
        _samplesDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Samples");
    }

    public void Dispose() => _tempDir.Dispose();

    private string GenerateThumbnail(string inputFile)
    {
        var outputPath = Path.Combine(_tempDir.DirectoryPath, $"thumb_{Guid.NewGuid():N}.jpg");
        var args = $"-y -hide_banner -loglevel quiet -i \"{inputFile}\" -ss 00:00:02 -vframes 1 -vf \"scale=198:112:force_original_aspect_ratio=decrease,pad=198:112:(ow-iw)/2:(oh-ih)/2:color=#1e1e2e\" -q:v 4 \"{outputPath}\"";
        var (exitCode, _, _) = FfmpegProcessRunner.Run(args, TimeSpan.FromSeconds(15));
        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(outputPath));
        return outputPath;
    }

    [Fact]
    public void Thumbnail_GeneratesValidJpeg()
    {
        var sample = Directory.GetFiles(_samplesDir, "*.mp4").First();
        var thumbPath = GenerateThumbnail(sample);
        var bytes = File.ReadAllBytes(thumbPath);
        Assert.True(bytes.Length > 100);
        Assert.Equal(0xFF, bytes[0]);
        Assert.Equal(0xD8, bytes[1]);
    }

    [Fact]
    public void Thumbnail_CorrectDimensions()
    {
        var sample = Directory.GetFiles(_samplesDir, "*.mp4").First();
        var thumbPath = GenerateThumbnail(sample);
        var (width, height, _) = FfprobeHelper.GetStreamInfo(thumbPath);
        Assert.Equal(198, width);
        Assert.Equal(112, height);
    }

    [Fact]
    public void Thumbnail_ForH264_Succeeds()
    {
        var h264Sample = Directory.GetFiles(_samplesDir, "*.mp4")
            .First(f => FfprobeHelper.DetectCodec(f) == "h264");
        var thumbPath = GenerateThumbnail(h264Sample);
        Assert.True(new FileInfo(thumbPath).Length > 0);
    }

    [Fact]
    public void Thumbnail_ForAV1_Succeeds()
    {
        var av1Sample = Directory.GetFiles(_samplesDir, "*.mp4")
            .FirstOrDefault(f => FfprobeHelper.DetectCodec(f) == "av1");
        Assert.NotNull(av1Sample);
        var thumbPath = GenerateThumbnail(av1Sample!);
        Assert.True(new FileInfo(thumbPath).Length > 0);
    }

    [Fact]
    public void Thumbnail_ForShortVideo_Succeeds()
    {
        var shortSample = Directory.GetFiles(_samplesDir, "*.mp4")
            .OrderBy(f => new FileInfo(f).Length)
            .First();
        var thumbPath = GenerateThumbnail(shortSample);
        Assert.True(new FileInfo(thumbPath).Length > 0);
    }
}
