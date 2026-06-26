using Xunit;
using VideoGateway.Engine;

namespace VideoGateway.Tests.Engine;

public class VideoFrameTests
{
    [Fact]
    public void DefaultValues_HasCorrectDefaults()
    {
        var frame = new VideoFrame();

        Assert.Equal(0, frame.SequenceNumber);
        Assert.Equal(string.Empty, frame.Format);
        Assert.Equal(0, frame.Width);
        Assert.Equal(0, frame.Height);
        Assert.Empty(frame.Data);
    }

    [Fact]
    public void Properties_CanBeAssignedAndRetrieved()
    {
        var frame = new VideoFrame
        {
            SequenceNumber = 42,
            Format = "mjpeg",
            Width = 1920,
            Height = 1080,
            Data = new byte[] { 0x01, 0x02, 0x03 }
        };

        Assert.Equal(42, frame.SequenceNumber);
        Assert.Equal("mjpeg", frame.Format);
        Assert.Equal(1920, frame.Width);
        Assert.Equal(1080, frame.Height);
        Assert.Equal(new byte[] { 0x01, 0x02, 0x03 }, frame.Data);
    }

    [Fact]
    public void Data_CanAcceptLargePayload()
    {
        var largeData = new byte[1024 * 1024];
        var frame = new VideoFrame { Data = largeData };

        Assert.Equal(1024 * 1024, frame.Data.Length);
    }

    [Fact]
    public void Data_CanBeEmpty()
    {
        var frame = new VideoFrame { Data = Array.Empty<byte>() };

        Assert.NotNull(frame.Data);
        Assert.Empty(frame.Data);
    }

    [Fact]
    public void Format_CanBeAnyString()
    {
        var frame = new VideoFrame { Format = "custom-format-123" };

        Assert.Equal("custom-format-123", frame.Format);
    }

    [Fact]
    public void SequenceNumber_SupportsLargeValues()
    {
        var frame = new VideoFrame { SequenceNumber = long.MaxValue };

        Assert.Equal(long.MaxValue, frame.SequenceNumber);
    }
}
