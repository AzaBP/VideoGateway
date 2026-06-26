using Xunit;
using VideoGateway.PublisherUI;

namespace VideoGateway.Tests.Publisher;

public class VideoFileScannerTests : IDisposable
{
    private readonly string _testDir;

    public VideoFileScannerTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"VideoGatewayScanner_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public void Scan_FolderWithVideos_ReturnsCorrectCount()
    {
        File.WriteAllText(Path.Combine(_testDir, "a.mp4"), "");
        File.WriteAllText(Path.Combine(_testDir, "b.mkv"), "");
        File.WriteAllText(Path.Combine(_testDir, "c.avi"), "");

        var scanner = new VideoFileScanner();
        var result = scanner.Scan(_testDir);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void Scan_FolderWithNonVideoFiles_SkipsThem()
    {
        File.WriteAllText(Path.Combine(_testDir, "doc.txt"), "");
        File.WriteAllText(Path.Combine(_testDir, "image.pdf"), "");
        File.WriteAllText(Path.Combine(_testDir, "video.mp4"), "");

        var scanner = new VideoFileScanner();
        var result = scanner.Scan(_testDir);

        Assert.Single(result);
        Assert.Contains("video.mp4", result[0]);
    }

    [Fact]
    public void Scan_EmptyFolder_ReturnsEmpty()
    {
        var scanner = new VideoFileScanner();
        var result = scanner.Scan(_testDir);

        Assert.Empty(result);
    }

    [Fact]
    public void Scan_NonexistentFolder_ReturnsEmpty()
    {
        var scanner = new VideoFileScanner();
        var result = scanner.Scan("C:\nonexistent_folder_12345");

        Assert.Empty(result);
    }

    [Fact]
    public void Scan_OrdersAlphabetically()
    {
        File.WriteAllText(Path.Combine(_testDir, "z.mp4"), "");
        File.WriteAllText(Path.Combine(_testDir, "a.mp4"), "");
        File.WriteAllText(Path.Combine(_testDir, "m.mkv"), "");

        var scanner = new VideoFileScanner();
        var result = scanner.Scan(_testDir);

        Assert.Equal(3, result.Count);
        Assert.Contains("a.mp4", result[0]);
        Assert.Contains("m.mkv", result[1]);
        Assert.Contains("z.mp4", result[2]);
    }
}
