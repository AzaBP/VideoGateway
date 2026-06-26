namespace VideoGateway.ConsoleApp;

public static class StdoutSampleParser
{
    public static bool ContainsFrameIndicator(string line)
    {
        if (string.IsNullOrEmpty(line)) return false;
        return line.Contains("frame:") || line.Contains("sample");
    }
}
