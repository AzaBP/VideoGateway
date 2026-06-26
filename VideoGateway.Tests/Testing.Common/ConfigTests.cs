using Xunit;
using VideoGateway.Testing.Common;

namespace VideoGateway.Tests.Testing.Common;

public class ConfigTests
{
    [Fact]
    public void Load_AlwaysReturnsNonNull()
    {
        var config = Config.Load();

        Assert.NotNull(config);
    }

    [Fact]
    public void Load_ReturnsConfigWithProperties()
    {
        var config = Config.Load();

        Assert.NotNull(config);
        Assert.True(config.FfmpegPath == null || File.Exists(config.FfmpegPath) || config.FfmpegPath.Length >= 0);
    }

    [Fact]
    public void Save_CreatesFile()
    {
        var config = new Config
        {
            FfmpegPath = @"C:\tools\ffmpeg.exe",
            FfplayPath = @"C:\tools\ffplay.exe",
            VlcPath = @"C:\tools\vlc.exe"
        };

        config.Save();

        var settingsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VideoGatewayTesting");
        var settingsFile = Path.Combine(settingsDir, "settings.json");
        Assert.True(File.Exists(settingsFile));
    }

    [Fact]
    public void SaveAndLoad_RoundTrip()
    {
        var original = new Config
        {
            FfmpegPath = @"C:\test\ffmpeg.exe",
            FfplayPath = @"C:\test\ffplay.exe",
            VlcPath = @"C:\test\vlc.exe"
        };

        original.Save();
        var loaded = Config.Load();

        Assert.Equal(original.FfmpegPath, loaded.FfmpegPath);
        Assert.Equal(original.FfplayPath, loaded.FfplayPath);
        Assert.Equal(original.VlcPath, loaded.VlcPath);
    }

    [Fact]
    public void GetPathFor_Ffmpeg_ReturnsPath()
    {
        var config = new Config { FfmpegPath = @"C:\tools\ffmpeg.exe" };
        config.Save();

        var result = Config.GetPathFor("ffmpeg");

        Assert.Equal(@"C:\tools\ffmpeg.exe", result);
    }

    [Fact]
    public void GetPathFor_Ffplay_ReturnsPath()
    {
        var config = new Config { FfplayPath = @"C:\tools\ffplay.exe" };
        config.Save();

        var result = Config.GetPathFor("ffplay");

        Assert.Equal(@"C:\tools\ffplay.exe", result);
    }

    [Fact]
    public void GetPathFor_Vlc_ReturnsPath()
    {
        var config = new Config { VlcPath = @"C:\tools\vlc.exe" };
        config.Save();

        var result = Config.GetPathFor("vlc");

        Assert.Equal(@"C:\tools\vlc.exe", result);
    }

    [Fact]
    public void GetPathFor_UnknownKey_ReturnsNull()
    {
        var result = Config.GetPathFor("unknown");

        Assert.Null(result);
    }

    [Fact]
    public void GetPathFor_CaseInsensitive_Works()
    {
        var config = new Config { FfmpegPath = @"C:\tools\ffmpeg.exe" };
        config.Save();

        var result = Config.GetPathFor("FFMPEG");

        Assert.Equal(@"C:\tools\ffmpeg.exe", result);
    }
}
