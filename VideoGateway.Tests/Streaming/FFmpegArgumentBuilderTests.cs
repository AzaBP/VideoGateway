using Xunit;
using VideoGateway.Streaming;

namespace VideoGateway.Tests.Streaming;

public class FFmpegArgumentBuilderTests
{
    [Fact]
    public void BuildInputArgs_Mjpeg_ReturnsImage2pipeArgs()
    {
        var result = FFmpegArgumentBuilder.BuildInputArgs("mjpeg", 0, 0);
        Assert.Equal("-f image2pipe -vcodec mjpeg", result);
    }

    [Fact]
    public void BuildInputArgs_Jpeg_ReturnsImage2pipeArgs()
    {
        var result = FFmpegArgumentBuilder.BuildInputArgs("jpeg", 0, 0);
        Assert.Equal("-f image2pipe -vcodec mjpeg", result);
    }

    [Fact]
    public void BuildInputArgs_H264_ReturnsH264Args()
    {
        var result = FFmpegArgumentBuilder.BuildInputArgs("h264", 0, 0);
        Assert.Equal("-f h264", result);
    }

    [Fact]
    public void BuildInputArgs_H265_ReturnsHevcArgs()
    {
        var result = FFmpegArgumentBuilder.BuildInputArgs("h265", 0, 0);
        Assert.Equal("-f hevc", result);
    }

    [Fact]
    public void BuildInputArgs_Hevc_ReturnsHevcArgs()
    {
        var result = FFmpegArgumentBuilder.BuildInputArgs("hevc", 0, 0);
        Assert.Equal("-f hevc", result);
    }

    [Fact]
    public void BuildInputArgs_Raw_WithDimensions_ReturnsRawVideoArgs()
    {
        var result = FFmpegArgumentBuilder.BuildInputArgs("raw", 1280, 720);
        Assert.Equal("-f rawvideo -pix_fmt bgr24 -s 1280x720", result);
    }

    [Fact]
    public void BuildInputArgs_Raw_DefaultDimensions_Uses1920x1080()
    {
        var result = FFmpegArgumentBuilder.BuildInputArgs("raw", 0, 0);
        Assert.Equal("-f rawvideo -pix_fmt bgr24 -s 1920x1080", result);
    }

    [Fact]
    public void BuildInputArgs_Unknown_ReturnsEmpty()
    {
        var result = FFmpegArgumentBuilder.BuildInputArgs("unknown", 0, 0);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void BuildInputArgs_Null_ReturnsEmpty()
    {
        var result = FFmpegArgumentBuilder.BuildInputArgs(null!, 0, 0);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void BuildOutputArgs_ContainsEncoderSettings()
    {
        var result = FFmpegArgumentBuilder.BuildOutputArgs("rtsp://localhost:8554/live");
        Assert.Contains("-c:v libx264 -preset ultrafast -tune zerolatency -f rtsp", result);
    }

    [Fact]
    public void BuildOutputArgs_ContainsRtspUrl()
    {
        var url = "rtsp://myserver:8554/stream";
        var result = FFmpegArgumentBuilder.BuildOutputArgs(url);
        Assert.Contains(url, result);
    }
}
