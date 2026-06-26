using Xunit;
using VideoGateway.PublisherUI;

namespace VideoGateway.Tests.Publisher;

public class PublishCommandBuilderTests
{
    [Fact]
    public void Build_LocalFile_Reencode_ContainsEncoderSettings()
    {
        var result = PublishCommandBuilder.Build("C:/video.mp4", "rtsp://localhost:8554/live", keepCodec: false);

        Assert.Contains("-c:v libx264 -preset ultrafast -tune zerolatency -c:a aac", result);
    }

    [Fact]
    public void Build_LocalFile_CopyCodec_ContainsCopy()
    {
        var result = PublishCommandBuilder.Build("C:/video.mp4", "rtsp://localhost:8554/live", keepCodec: true);

        Assert.Contains("-c copy", result);
        Assert.DoesNotContain("-c:v libx264", result);
    }

    [Fact]
    public void Build_OutputContainsRtspFormat()
    {
        var url = "rtsp://myserver:8554/stream";
        var result = PublishCommandBuilder.Build("C:/video.mp4", url, keepCodec: false);

        Assert.Contains("-f rtsp", result);
        Assert.Contains(url, result);
    }

    [Fact]
    public void Build_FilenameWithSpaces_Quoted()
    {
        var result = PublishCommandBuilder.Build("C:/my videos/test file.mp4", "rtsp://localhost:8554/live", keepCodec: false);

        Assert.Contains("\"C:/my videos/test file.mp4\"", result);
    }
}
