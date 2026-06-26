namespace VideoGateway.Tests.Integration.Helpers;

public class TempDirHelper : IDisposable
{
    public string DirectoryPath { get; }

    public TempDirHelper(string prefix = "VgTest")
    {
        DirectoryPath = Path.Combine(Path.GetTempPath(), $"{prefix}_{Guid.NewGuid():N}");
        Directory.CreateDirectory(DirectoryPath);
    }

    public string GetTempFilePath(string extension = ".mp4")
    {
        return Path.Combine(DirectoryPath, $"test_{Guid.NewGuid():N}{extension}");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(DirectoryPath))
                Directory.Delete(DirectoryPath, true);
        }
        catch { }
    }
}
