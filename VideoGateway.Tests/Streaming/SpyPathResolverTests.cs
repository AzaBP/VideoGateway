using Xunit;
using VideoGateway.ConsoleApp;

namespace VideoGateway.Tests.Streaming;

public class SpyPathResolverTests
{
    [Fact]
    public void Resolve_WithCliArgEqual_ReturnsArgValue()
    {
        var args = new[] { "--rtiddsspy=C:/tools/rtiddsspy.exe" };
        var result = SpyPathResolver.Resolve(args, null);
        Assert.Equal("C:/tools/rtiddsspy.exe", result);
    }

    [Fact]
    public void Resolve_WithCliArgSpace_ReturnsNextArg()
    {
        var args = new[] { "--rtiddsspy", "C:/tools/rtiddsspy.exe" };
        var result = SpyPathResolver.Resolve(args, null);
        Assert.Equal("C:/tools/rtiddsspy.exe", result);
    }

    [Fact]
    public void Resolve_WithEnvVar_ReturnsEnvValue()
    {
        var result = SpyPathResolver.Resolve(null, "C:/env/rtiddsspy.exe");
        Assert.Equal("C:/env/rtiddsspy.exe", result);
    }

    [Fact]
    public void Resolve_WithNoArgsNoEnv_ReturnsDefault()
    {
        var result = SpyPathResolver.Resolve(null, null);
        Assert.Equal("rtiddsspy", result);
    }

    [Fact]
    public void Resolve_CliArgTakesPriorityOverEnv()
    {
        var args = new[] { "--rtiddsspy=C:/cli/rtiddsspy.exe" };
        var result = SpyPathResolver.Resolve(args, "C:/env/rtiddsspy.exe");
        Assert.Equal("C:/cli/rtiddsspy.exe", result);
    }
}
