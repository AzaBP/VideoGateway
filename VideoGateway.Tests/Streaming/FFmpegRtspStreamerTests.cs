using Xunit;
using VideoGateway.Engine;
using VideoGateway.Streaming;

namespace VideoGateway.Tests.Streaming;

public class FFmpegRtspStreamerTests
{
    [Fact]
    public void Constructor_DefaultUrl_CreatesInstance()
    {
        var streamer = new FFmpegRtspStreamer();
        Assert.NotNull(streamer);
    }

    [Fact]
    public void Constructor_CustomUrl_CreatesInstance()
    {
        var streamer = new FFmpegRtspStreamer("rtsp://custom:8554/stream");
        Assert.NotNull(streamer);
    }

    [Fact]
    public void Start_DoesNotThrow()
    {
        var streamer = new FFmpegRtspStreamer();
        var exception = Record.Exception(() => streamer.Start());
        Assert.Null(exception);
        streamer.Stop();
    }

    [Fact]
    public void PushFrame_BeforeStart_DoesNotThrow()
    {
        var streamer = new FFmpegRtspStreamer();
        var frame = new VideoFrame { Format = "mjpeg", Data = new byte[] { 0x01 } };

        var exception = Record.Exception(() => streamer.PushFrame(frame));
        Assert.Null(exception);
    }

    [Fact]
    public void PushFrame_NullFrame_DoesNotThrow()
    {
        var streamer = new FFmpegRtspStreamer();
        streamer.Start();

        var exception = Record.Exception(() => streamer.PushFrame(null!));
        Assert.Null(exception);
        streamer.Stop();
    }

    [Fact]
    public void PushFrame_EmptyData_DoesNotThrow()
    {
        var streamer = new FFmpegRtspStreamer();
        streamer.Start();
        var frame = new VideoFrame { Format = "mjpeg", Data = Array.Empty<byte>() };

        var exception = Record.Exception(() => streamer.PushFrame(frame));
        Assert.Null(exception);
        streamer.Stop();
    }

    [Fact]
    public void Stop_CanBeCalledMultipleTimes()
    {
        var streamer = new FFmpegRtspStreamer();
        streamer.Start();

        var ex1 = Record.Exception(() => streamer.Stop());
        var ex2 = Record.Exception(() => streamer.Stop());
        var ex3 = Record.Exception(() => streamer.Stop());

        Assert.Null(ex1);
        Assert.Null(ex2);
        Assert.Null(ex3);
    }

    [Fact]
    public void Stop_WithoutStart_DoesNotThrow()
    {
        var streamer = new FFmpegRtspStreamer();
        var exception = Record.Exception(() => streamer.Stop());
        Assert.Null(exception);
    }
}
