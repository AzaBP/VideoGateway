namespace VideoGateway.PublisherUI;

public static class PublishCommandBuilder
{
    public static string Build(string sourceFile, string rtspUrl, bool keepCodec)
    {
        var source = $"\"{sourceFile}\"";
        var codecArgs = keepCodec
            ? "-c copy"
            : "-c:v libx264 -preset ultrafast -tune zerolatency -c:a aac";

        return $"{source} {codecArgs} -f rtsp \"{rtspUrl}\"";
    }
}
