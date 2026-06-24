// VideoGateway.Testing.Common - MediaInfo
// Utilities to detect media format using file extension and optionally ffprobe.
using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace VideoGateway.Testing.Common
{
    /// <summary>
    /// Provide simple media format detection helpers used by the test UIs.
    /// First maps common extensions to friendly format names; if ffprobe exists
    /// it can be invoked to gather more accurate information.
    /// </summary>
    public static class MediaInfo
    {
        public sealed class StreamDetectionResult
        {
            public string? VideoCodec { get; set; }
            public string? AudioCodec { get; set; }
            public int? Width { get; set; }
            public int? Height { get; set; }
            public string Source { get; set; } = "unknown";
        }

        /// <summary>
        /// Try to detect stream codecs and resolution using ffprobe. Attempts UDP first then TCP.
        /// Returns null if detection fails.
        /// </summary>
        public static StreamDetectionResult? DetectStreamInfoViaFfprobe(string path, int timeoutMs = 3000)
        {
            string[] transports = new[] { "udp", "tcp" };
            foreach (var transport in transports)
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "ffprobe",
                        Arguments = $"-rtsp_transport {transport} -analyzeduration 5000000 -probesize 5000000 -v error -select_streams v:0,a:0 -show_streams -print_format json \"{path}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    using var p = Process.Start(psi);
                    if (p == null) continue;
                    var exited = p.WaitForExit(timeoutMs);
                    string output = string.Empty;
                    try { output = p.StandardOutput.ReadToEnd(); } catch { }
                    if (!exited) { try { p.Kill(); } catch { } continue; }
                    if (string.IsNullOrWhiteSpace(output)) continue;

                    using var doc = JsonDocument.Parse(output);
                    var root = doc.RootElement;
                    if (!root.TryGetProperty("streams", out var streams)) continue;

                    string? vcodec = null; string? acodec = null; int? width = null; int? height = null;
                    foreach (var s in streams.EnumerateArray())
                    {
                        if (s.TryGetProperty("codec_type", out var t))
                        {
                            var kind = t.GetString();
                            if (kind == "video" && vcodec == null)
                            {
                                if (s.TryGetProperty("codec_name", out var cn)) vcodec = cn.GetString();
                                if (s.TryGetProperty("width", out var w) && w.TryGetInt32(out var wi)) width = wi;
                                if (s.TryGetProperty("height", out var h) && h.TryGetInt32(out var hi)) height = hi;
                            }
                            else if (kind == "audio" && acodec == null)
                            {
                                if (s.TryGetProperty("codec_name", out var cn)) acodec = cn.GetString();
                            }
                        }
                    }

                    return new StreamDetectionResult
                    {
                        VideoCodec = vcodec,
                        AudioCodec = acodec,
                        Width = width,
                        Height = height,
                        Source = $"ffprobe ({transport})"
                    };
                }
                catch { }
            }
            return null;
        }
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
        /// Attempts to retrieve the first video and audio codec names using ffprobe.
        /// Returns (videoCodec, audioCodec) where each can be null if not found.
        /// </summary>
        public static (string? videoCodec, string? audioCodec) DetectCodecsWithFfprobe(string path)
        {
            string? GetCodec(string streamSpecifier)
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "ffprobe",
                        Arguments = $"-v error -select_streams {streamSpecifier} -show_entries stream=codec_name -of default=nw=1:nk=1 \"{path}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    using var p = Process.Start(psi);
                    if (p == null) return null;
                    var outp = p.StandardOutput.ReadLine();
                    p.WaitForExit(1500);
                    if (!string.IsNullOrWhiteSpace(outp)) return outp.Trim();
                }
                catch { }
                return null;
            }

            var v = GetCodec("v:0");
            var a = GetCodec("a:0");
            return (v, a);
        }

        /// <summary>
        /// Attempts to retrieve the first video stream's width and height using ffprobe.
        /// Returns null on failure or when ffprobe is not available.
        /// </summary>
        public static (int width, int height)? DetectVideoResolutionWithFfprobe(string path)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "ffprobe",
                    Arguments = $"-v error -select_streams v:0 -show_entries stream=width,height -of default=nw=1:nk=1 \"{path}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var p = Process.Start(psi);
                if (p == null) return null;
                var outp = p.StandardOutput.ReadToEnd();
                p.WaitForExit(2000);
                if (string.IsNullOrWhiteSpace(outp)) return null;
                var lines = outp.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length >= 2 && int.TryParse(lines[0].Trim(), out var w) && int.TryParse(lines[1].Trim(), out var h))
                {
                    return (w, h);
                }
            }
            catch { }
            return null;
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
