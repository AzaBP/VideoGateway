using Xunit;
using VideoGateway.Testing.Common;

namespace VideoGateway.Tests.Testing.Common;

public class MediaInfoTests
{
    [Theory]
    [InlineData("video.mp4", "mp4")]
    [InlineData("video.mkv", "mkv")]
    [InlineData("video.avi", "avi")]
    [InlineData("video.mov", "mov")]
    [InlineData("video.mjpeg", "mjpeg")]
    [InlineData("image.jpg", "jpeg")]
    [InlineData("image.jpeg", "jpeg")]
    [InlineData("stream.h264", "h264")]
    [InlineData("stream.264", "h264")]
    [InlineData("stream.h265", "h265")]
    [InlineData("stream.hevc", "h265")]
    public void DetectFormatFromExtension_KnownExtensions_ReturnsCorrectFormat(string path, string expected)
    {
        var result = MediaInfo.DetectFormatFromExtension(path);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void DetectFormatFromExtension_UnknownExtension_ReturnsExtension()
    {
        var result = MediaInfo.DetectFormatFromExtension("file.xyz");
        Assert.Equal("xyz", result);
    }

    [Fact]
    public void DetectFormatFromExtension_NoExtension_ReturnsEmpty()
    {
        var result = MediaInfo.DetectFormatFromExtension("file");
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void DetectFormatFromExtension_UpperCase_ReturnsLowerCase()
    {
        var result = MediaInfo.DetectFormatFromExtension("VIDEO.MP4");
        Assert.Equal("mp4", result);
    }
}
