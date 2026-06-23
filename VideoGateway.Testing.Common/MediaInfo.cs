// VideoGateway.Testing.Common - MediaInfo
// Utilities to detect media format using file extension and optionally ffprobe.
using System;
using System.Diagnostics;
using System.IO;

namespace VideoGateway.Testing.Common
{
    /// <summary>
    /// Provide simple media format detection helpers used by the test UIs.
    /// First maps common extensions to friendly format names; if ffprobe exists
    /// it can be invoked to gather more accurate information.
    /// </summary>
    public static class MediaInfo
    {
        public static string DetectFormatFromExtension(string path)
        {
            var ext = Path.GetExtension(path)?.TrimStart('.').ToLowerInvariant() ?? string.Empty;
            return ext switch
            {
                "mp4" => "mp4",
                "mkv" => "mkv",
                "avi" => "avi",
                "mov" => "mov",
                "mjpeg" => "mjpeg",
                "jpg" or "jpeg" => "jpeg",
                "h264" or "264" => "h264",
                "h265" or "hevc" => "h265",
                _ => ext
            };
        }

        /// <summary>
        /// If ffprobe is available in PATH, attempts to retrieve codec_name for the first video stream.
        /// Falls back to extension-based detection on any error.
        /// </summary>
        public static string DetectFormatWithFfprobe(string path)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "ffprobe",
                    Arguments = $"-v error -select_streams v:0 -show_entries stream=codec_name -of default=nw=1:nk=1 \"{path}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var p = Process.Start(psi);
                if (p == null) return DetectFormatFromExtension(path);
                var outp = p.StandardOutput.ReadLine();
                p.WaitForExit(2000);
                if (!string.IsNullOrWhiteSpace(outp)) return outp.Trim();
            }
            catch { }
            return DetectFormatFromExtension(path);
        }

        /// <summary>
        /// Retrieves detailed stream and format information from ffprobe.
        /// </summary>
        public static string GetMetadataWithFfprobe(string path)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "ffprobe",
                    Arguments = $"-v error -show_format -show_streams -print_format default \"{path}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var p = Process.Start(psi);
                if (p == null) return "No se pudo ejecutar ffprobe.";
                var output = p.StandardOutput.ReadToEnd();
                p.WaitForExit(3000);
                return string.IsNullOrWhiteSpace(output) ? "No se encontraron metadatos." : output;
            }
            catch (Exception ex)
            {
                return $"Error ejecutando ffprobe: {ex.Message}";
            }
        }
    }
}
