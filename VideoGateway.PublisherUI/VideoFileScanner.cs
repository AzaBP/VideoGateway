namespace VideoGateway.PublisherUI;

public class VideoFileScanner
{
    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mkv", ".avi", ".mov", ".mjpeg", ".jpg", ".jpeg",
        ".h264", ".264", ".h265", ".hevc", ".webm", ".flv", ".wmv", ".ts"
    };

    public IReadOnlyList<string> Scan(string folder)
    {
        if (!Directory.Exists(folder))
            return Array.Empty<string>();

        return Directory.EnumerateFiles(folder)
            .Where(f => VideoExtensions.Contains(Path.GetExtension(f)))
            .OrderBy(f => f)
            .ToList();
    }
}
