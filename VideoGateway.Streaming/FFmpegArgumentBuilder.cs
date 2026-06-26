namespace VideoGateway.Streaming;

public static class FFmpegArgumentBuilder
{
    public static string BuildInputArgs(string format, int width, int height)
    {
        return (format ?? string.Empty).ToLowerInvariant() switch
        {
            "mjpeg" or "jpeg" => "-f image2pipe -vcodec mjpeg",
            "h264" => "-f h264",
            "h265" or "hevc" => "-f hevc",
            "raw" => $"-f rawvideo -pix_fmt bgr24 -s {(width > 0 ? width : 1920)}x{(height > 0 ? height : 1080)}",
            _ => string.Empty
        };
    }

    public static string BuildOutputArgs(string rtspUrl)
    {
        return $"-c:v libx264 -preset ultrafast -tune zerolatency -f rtsp {rtspUrl}";
    }
}
