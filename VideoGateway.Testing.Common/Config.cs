using System;
using System.IO;
using System.Text.Json;

namespace VideoGateway.Testing.Common
{
    /// <summary>
    /// Simple JSON-backed configuration for executable paths.
    /// Stored in %APPDATA%\VideoGatewayTesting\settings.json
    /// </summary>
    public class Config
    {
        public string? FfmpegPath { get; set; }
        public string? FfplayPath { get; set; }
        public string? VlcPath { get; set; }

        private static string GetSettingsPath()
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VideoGatewayTesting");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "settings.json");
        }

        public static Config Load()
        {
            try
            {
                var p = GetSettingsPath();
                if (!File.Exists(p)) return new Config();
                var json = File.ReadAllText(p);
                var cfg = JsonSerializer.Deserialize<Config>(json);
                return cfg ?? new Config();
            }
            catch
            {
                return new Config();
            }
        }

        public void Save()
        {
            try
            {
                var p = GetSettingsPath();
                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(p, json);
            }
            catch { }
        }

        public static string? GetPathFor(string exeKey)
        {
            var cfg = Load();
            return exeKey.ToLowerInvariant() switch
            {
                "ffmpeg" => cfg.FfmpegPath,
                "ffplay" => cfg.FfplayPath,
                "vlc" => cfg.VlcPath,
                _ => null
            };
        }
    }
}
