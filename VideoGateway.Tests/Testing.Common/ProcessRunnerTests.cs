using Xunit;
using VideoGateway.Testing.Common;

namespace VideoGateway.Tests.Testing.Common;

public class ProcessRunnerTests
{
    [Fact]
    public void StartProcess_EchoCommand_ReturnsRunningProcess()
    {
        var process = ProcessRunner.StartProcess("cmd", "/c echo hello");

        Assert.NotNull(process);
        process!.Kill();
        process.Dispose();
    }

    [Fact]
    public void StartProcess_NonexistentExe_ReturnsNull()
    {
        var process = ProcessRunner.StartProcess("nonexistent_xyz_12345", "");

        Assert.Null(process);
    }

    [Fact]
    public void TryFindExecutable_Cmd_FindsPath()
    {
        var result = ProcessRunner.TryFindExecutable("cmd");

        Assert.NotNull(result);
        Assert.Contains("cmd", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryFindExecutable_Nonexistent_ReturnsNull()
    {
        var result = ProcessRunner.TryFindExecutable("nonexistent12345xyz");

        Assert.Null(result);
    }
}
