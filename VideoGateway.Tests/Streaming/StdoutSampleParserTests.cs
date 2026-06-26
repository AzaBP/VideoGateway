using Xunit;
using VideoGateway.ConsoleApp;

namespace VideoGateway.Tests.Streaming;

public class StdoutSampleParserTests
{
    [Theory]
    [InlineData("frame: 12345 received", true)]
    [InlineData("[frame: data]", true)]
    [InlineData("sample received from DDS", true)]
    [InlineData("has sample in buffer", true)]
    [InlineData("info: connection established", false)]
    [InlineData("error: timeout", false)]
    [InlineData("", false)]
    public void ContainsFrameIndicator_VariousLines_ReturnsExpected(string line, bool expected)
    {
        var result = StdoutSampleParser.ContainsFrameIndicator(line);
        Assert.Equal(expected, result);
    }
}
