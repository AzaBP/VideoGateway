using Xunit;
using VideoGateway.Tests.Integration.Helpers;
using VideoGateway.Streaming;
using VideoGateway.Engine;

namespace VideoGateway.Tests.Integration;

[Trait("Category", "Integration")]
public class PipeStreamingTests
{
    [Fact]
    public void PipeStream_Mjpeg_ProcessStarts()
    {
        var streamer = new FFmpegRtspStreamer("rtsp://127.0.0.1:8554/test_mjpeg");
        streamer.Start();

        var frame = new VideoFrame
        {
            Format = "mjpeg",
            Data = SyntheticFileGenerator.GenerateMjpegFrame()
        };

        var ex = Record.Exception(() => streamer.PushFrame(frame));
        streamer.Stop();
        Assert.Null(ex);
    }

    [Fact]
    public void PipeStream_H264_ProcessStarts()
    {
        var streamer = new FFmpegRtspStreamer("rtsp://127.0.0.1:8554/test_h264");
        streamer.Start();

        var frame = new VideoFrame
        {
            Format = "h264",
            Data = new byte[] { 0x00, 0x00, 0x00, 0x01, 0x65, 0x88, 0x80, 0x40 }
        };

        var ex = Record.Exception(() => streamer.PushFrame(frame));
        streamer.Stop();
        Assert.Null(ex);
    }

    [Fact]
    public void PipeStream_H265_ProcessStarts()
    {
        var streamer = new FFmpegRtspStreamer("rtsp://127.0.0.1:8554/test_h265");
        streamer.Start();

        var frame = new VideoFrame
        {
            Format = "h265",
            Data = new byte[] { 0x00, 0x00, 0x00, 0x01, 0x26, 0x01, 0x10, 0x01 }
        };

        var ex = Record.Exception(() => streamer.PushFrame(frame));
        streamer.Stop();
        Assert.Null(ex);
    }

    [Fact]
    public void PipeStream_Raw_ProcessStarts()
    {
        var streamer = new FFmpegRtspStreamer("rtsp://127.0.0.1:8554/test_raw");
        streamer.Start();

        var frame = new VideoFrame
        {
            Format = "raw",
            Width = 160,
            Height = 120,
            Data = new byte[160 * 120 * 3]
        };

        var ex = Record.Exception(() => streamer.PushFrame(frame));
        streamer.Stop();
        Assert.Null(ex);
    }

    [Fact]
    public void PipeStream_WritesBytesToStdin()
    {
        var streamer = new FFmpegRtspStreamer("rtsp://127.0.0.1:8554/test_write");
        streamer.Start();

        var jpegData = SyntheticFileGenerator.GenerateMjpegFrame();
        var frame = new VideoFrame
        {
            Format = "mjpeg",
            Data = jpegData
        };

        var ex = Record.Exception(() => streamer.PushFrame(frame));
        streamer.Stop();
        Assert.Null(ex);
    }

    [Fact]
    public void PipeStream_EmptyFrame_NoCrash()
    {
        var streamer = new FFmpegRtspStreamer("rtsp://127.0.0.1:8554/test_empty");
        streamer.Start();

        var frame = new VideoFrame
        {
            Format = "mjpeg",
            Data = Array.Empty<byte>()
        };

        var ex = Record.Exception(() => streamer.PushFrame(frame));
        streamer.Stop();
        Assert.Null(ex);
    }

    [Fact]
    public void PipeStream_NullFrame_NoCrash()
    {
        var streamer = new FFmpegRtspStreamer("rtsp://127.0.0.1:8554/test_null");
        streamer.Start();

        var ex = Record.Exception(() => streamer.PushFrame(null!));
        streamer.Stop();
        Assert.Null(ex);
    }
}
